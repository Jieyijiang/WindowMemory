using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace WindowMemory
{
    public sealed class ConfigService
    {
        public string DataDirectory { get; private set; }
        public string ConfigPath { get { return Path.Combine(DataDirectory, "settings.json"); } }
        public string LastError { get; private set; }

        public ConfigService()
            : this(null)
        {
        }

        internal ConfigService(string dataDirectory)
        {
            LastError = string.Empty;
            if (!string.IsNullOrWhiteSpace(dataDirectory))
            {
                DataDirectory = dataDirectory;
                Directory.CreateDirectory(DataDirectory);
                return;
            }
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string portableDirectory = Path.Combine(appDirectory, "Data");
            bool portableRequested = File.Exists(Path.Combine(appDirectory, "portable.flag")) || Directory.Exists(portableDirectory);

            if (portableRequested && CanUseDirectory(portableDirectory))
                DataDirectory = portableDirectory;
            else
                DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowMemory");

            Directory.CreateDirectory(DataDirectory);
        }

        private static bool CanUseDirectory(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "ok", Encoding.ASCII);
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public AppState Load()
        {
            LastError = string.Empty;
            if (!File.Exists(ConfigPath)) return new AppState();

            try
            {
                AppState state;
                using (FileStream stream = File.OpenRead(ConfigPath))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppState));
                    state = serializer.ReadObject(stream) as AppState;
                }
                if (state == null) return new AppState();
                int previousSchema = state.SchemaVersion;
                Normalize(state);
                if (state.SchemaVersion != previousSchema) Save(state);
                return state;
            }
            catch (Exception ex)
            {
                LastError = "配置读取失败：" + ex.Message;
                return new AppState();
            }
        }

        public bool Save(AppState state)
        {
            LastError = string.Empty;
            try
            {
                Normalize(state);
                Directory.CreateDirectory(DataDirectory);
                string temp = ConfigPath + ".tmp";
                string backup = ConfigPath + ".bak";

                using (FileStream stream = File.Create(temp))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppState));
                    serializer.WriteObject(stream, state);
                    stream.Flush(true);
                }

                if (File.Exists(ConfigPath))
                {
                    try
                    {
                        File.Replace(temp, ConfigPath, backup, true);
                    }
                    catch
                    {
                        File.Copy(ConfigPath, backup, true);
                        File.Delete(ConfigPath);
                        File.Move(temp, ConfigPath);
                    }
                }
                else
                {
                    File.Move(temp, ConfigPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = "配置保存失败：" + ex.Message;
                return false;
            }
        }

        private static void Normalize(AppState state)
        {
            if (state.Preferences == null) state.Preferences = new AppPreferences();
            if (state.Rules == null) state.Rules = new System.Collections.Generic.List<WindowRule>();
            if (state.Layouts == null) state.Layouts = new System.Collections.Generic.List<LayoutProfile>();
            if (string.IsNullOrWhiteSpace(state.Preferences.CaptureHotkey)) state.Preferences.CaptureHotkey = "Ctrl+Alt+Z";
            if (state.Preferences.ScanIntervalMs < 250) state.Preferences.ScanIntervalMs = 700;
            if (state.SchemaVersion < 2)
            {
                state.Preferences.MinimizeToTray = false;
                state.SchemaVersion = 2;
            }
            if (state.SchemaVersion < 3)
            {
                // 旧版本默认在关闭主窗口时退出，后台恢复也随之停止。
                // v3 起默认常驻托盘，并将已有“忽略标题”规则升级为纯程序绑定。
                state.Preferences.MinimizeToTray = true;
                foreach (WindowRule rule in state.Rules)
                {
                    if (rule != null && rule.Matcher != null && rule.Matcher.TitleMode == TitleMatchMode.Ignore)
                        rule.Matcher.ClassName = string.Empty;
                }
                state.SchemaVersion = 3;
            }
        }
    }
}
