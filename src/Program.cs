using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows;

[assembly: AssemblyTitle("Window Memory")]
[assembly: AssemblyDescription("记忆并还原 Windows 窗口与多窗口布局")]
[assembly: AssemblyCompany("Personal Utility")]
[assembly: AssemblyProduct("Window Memory")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace WindowMemory
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool selfTest = HasArgument(args, "--self-test");
            if (selfTest) return SelfTests.Run();

            string previewPath = ArgumentValue(args, "--render-preview=");
            if (!string.IsNullOrWhiteSpace(previewPath))
            {
                try
                {
                    Application previewApp = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    previewApp.Resources.MergedDictionaries.Add(Theme.Create());
                    MainWindow preview = new MainWindow(false, true);
                    preview.SavePreview(previewPath, ArgumentValue(args, "--preview-page="));
                    return 0;
                }
                catch (Exception ex)
                {
                    TryWriteCrashLog(ex);
                    return 3;
                }
            }

            bool created;
            using (Mutex mutex = new Mutex(true, @"Local\WindowMemory.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("Window Memory 已经在运行，请从系统托盘打开。", "Window Memory",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return 0;
                }

                try
                {
                    Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.Resources.MergedDictionaries.Add(Theme.Create());
                    MainWindow window = new MainWindow(HasArgument(args, "--background"));
                    app.Run(window);
                    return 0;
                }
                catch (Exception ex)
                {
                    TryWriteCrashLog(ex);
                    MessageBox.Show("Window Memory 无法启动。\n\n" + ex.Message, "启动失败",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            if (args == null) return false;
            foreach (string arg in args)
                if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ArgumentValue(string[] args, string prefix)
        {
            if (args == null) return string.Empty;
            foreach (string arg in args)
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return arg.Substring(prefix.Length).Trim('"');
            return string.Empty;
        }

        private static void TryWriteCrashLog(Exception ex)
        {
            try
            {
                string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "crash.log"), DateTime.Now.ToString("s") + Environment.NewLine + ex);
            }
            catch { }
        }
    }

    internal static class SelfTests
    {
        public static int Run()
        {
            try
            {
                HotkeyService.HotkeyGesture gesture;
                string error;
                Assert(HotkeyService.HotkeyGesture.TryParse("Ctrl+Alt+1", out gesture, out error), "快捷键解析失败");
                Assert(gesture.DisplayText == "Ctrl+Alt+1", "快捷键格式错误");
                Assert(!HotkeyService.HotkeyGesture.TryParse("1", out gesture, out error), "无修饰键快捷键应被拒绝");

                WindowService service = new WindowService();
                WindowMatcher matcher = new WindowMatcher
                {
                    ProcessName = "demo",
                    ClassName = "DemoWindow",
                    TitleText = "工作台",
                    TitleMode = TitleMatchMode.Contains
                };
                WindowDescriptor descriptor = new WindowDescriptor
                {
                    ProcessName = "demo",
                    ClassName = "DemoWindow",
                    Title = "项目工作台 - Demo"
                };
                int score;
                Assert(service.Matches(matcher, descriptor, out score), "窗口匹配失败");
                Assert(score >= 100, "窗口匹配评分异常");

                AppState state = new AppState();
                state.Rules.Add(new WindowRule { Name = "测试规则", Matcher = matcher });
                state.Layouts.Add(new LayoutProfile { Name = "布局 1", Hotkey = "Ctrl+1" });
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppState));
                using (MemoryStream stream = new MemoryStream())
                {
                    serializer.WriteObject(stream, state);
                    stream.Position = 0;
                    AppState roundTrip = serializer.ReadObject(stream) as AppState;
                    Assert(roundTrip != null && roundTrip.Rules.Count == 1 && roundTrip.Layouts.Count == 1, "配置序列化往返失败");
                }

                Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(Theme.Create());
                WindowDescriptor sample = new WindowDescriptor
                {
                    Title = "项目工作台 - Demo",
                    ProcessName = "demo",
                    ProcessPath = @"C:\Tools\demo.exe",
                    ClassName = "DemoWindow",
                    Bounds = new NativeRect(0, 0, 960, 1040),
                    MonitorWorkArea = new NativeRect(0, 0, 1920, 1040),
                    MonitorDevice = @"\\.\DISPLAY1"
                };
                new MainWindow(false, true);
                new HotkeyDialog(null, "Ctrl+1");
                new WindowPickerDialog(null, new List<WindowDescriptor> { sample }, "测试", "测试");
                new RuleEditorDialog(null, null, sample, service);
                new LayoutCaptureDialog(null, new List<WindowDescriptor> { sample }, service, "Ctrl+1");
                new LayoutPropertiesDialog(null, new LayoutProfile { Name = "布局 1", Hotkey = "Ctrl+1" });
                return 0;
            }
            catch
            {
                return 2;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
