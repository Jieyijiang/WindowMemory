using System;
using System.Collections.Generic;
using System.Diagnostics;
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
[assembly: AssemblyVersion("1.0.5.0")]
[assembly: AssemblyFileVersion("1.0.5.0")]

namespace WindowMemory
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool selfTest = HasArgument(args, "--self-test");
            if (selfTest) return SelfTests.Run();
            if (HasArgument(args, "--integration-test")) return SelfTests.RunIntegration();
            if (HasArgument(args, "--test-window")) return SelfTests.RunTestWindow();

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
                    try
                    {
                        using (EventWaitHandle activation = EventWaitHandle.OpenExisting(@"Local\WindowMemory.ShowMainWindow"))
                            activation.Set();
                    }
                    catch
                    {
                        MessageBox.Show("Window Memory 已经在运行，但暂时无法唤回主窗口。", "Window Memory",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return 0;
                }

                try
                {
                    Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.Resources.MergedDictionaries.Add(Theme.Create());
                    bool activationCreated;
                    using (EventWaitHandle activation = new EventWaitHandle(false, EventResetMode.AutoReset,
                        @"Local\WindowMemory.ShowMainWindow", out activationCreated))
                    {
                        MainWindow window = new MainWindow(HasArgument(args, "--background"), false, activation);
                        app.Run(window);
                    }
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
                Assert(state.Preferences.MinimizeToTray, "默认应保持后台托盘运行");
                Assert(state.Preferences.ScanIntervalMs == 0, "默认应使用窗口事件即时检测");
                WindowMatcher programMatcher = service.CreateProgramMatcher(new WindowDescriptor
                {
                    ProcessPath = @"C:\Tools\demo.exe",
                    ProcessName = "demo",
                    ClassName = "OldClass",
                    Title = "旧标题"
                });
                Assert(programMatcher.TitleMode == TitleMatchMode.Ignore && string.IsNullOrEmpty(programMatcher.ClassName),
                    "默认规则应按程序匹配并忽略标题和窗口类");
                Assert(service.Matches(programMatcher, new WindowDescriptor
                {
                    ProcessPath = @"C:\Tools\demo.exe",
                    ProcessName = "demo",
                    ClassName = "NewClass",
                    Title = "新标题"
                }, out score), "程序绑定不应受标题或窗口类变化影响");
                TestDuplicateRulePrecedence();
                TestConfigMigration();
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

        public static int RunIntegration()
        {
            Process child = null;
            AutoRestoreEngine engine = null;
            try
            {
                WindowService service = new WindowService();
                SavedPlacement target = new SavedPlacement
                {
                    X = 360,
                    Y = 260,
                    Width = 480,
                    Height = 320,
                    ScaleWithMonitor = false
                };
                WindowRule rule = new WindowRule
                {
                    Name = "集成测试",
                    Matcher = new WindowMatcher
                    {
                        ProcessPath = Assembly.GetExecutingAssembly().Location,
                        ProcessName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location),
                        TitleText = "WindowMemory 集成测试窗口",
                        TitleMode = TitleMatchMode.Exact
                    },
                    Placement = target,
                    Enabled = true
                };
                WindowRule supersededRule = rule.Clone();
                supersededRule.Id = Guid.NewGuid().ToString("N");
                supersededRule.Name = "已覆盖的旧位置";
                supersededRule.Placement = target.Clone();
                supersededRule.Placement.X = 80;
                supersededRule.Placement.Y = 80;
                engine = new AutoRestoreEngine(service);
                engine.Update(new[] { supersededRule, rule }, 0, false);
                Assert(engine.EffectiveRuleCount == 1, "自动恢复引擎没有排除被覆盖的重复规则");
                engine.Start();

                child = Process.Start(new ProcessStartInfo
                {
                    FileName = Assembly.GetExecutingAssembly().Location,
                    Arguments = "--test-window",
                    UseShellExecute = false
                });

                bool moved = false;
                Stopwatch response = null;
                for (int attempt = 0; attempt < 100 && !moved; attempt++)
                {
                    Thread.Sleep(20);
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { }));
                    foreach (WindowDescriptor candidate in service.EnumerateWindows())
                    {
                        if (candidate.Title != "WindowMemory 集成测试窗口") continue;
                        if (response == null) response = Stopwatch.StartNew();
                        if (Math.Abs(candidate.Bounds.Left - target.X) <= 2 && Math.Abs(candidate.Bounds.Top - target.Y) <= 2)
                        {
                            moved = true;
                            break;
                        }
                    }
                }
                Assert(moved, "自动恢复未实际移动测试窗口");
                Assert(engine.ImmediateEventCount > 0, "没有收到 Windows 窗口事件");
                Assert(engine.ImmediateAppliedCount > 0, "窗口创建事件没有直接执行归位");
                Assert(response != null && response.ElapsedMilliseconds < 250, "窗口事件触发后恢复不够及时");
                return 0;
            }
            catch
            {
                return 4;
            }
            finally
            {
                if (engine != null) engine.Dispose();
                if (child != null && !child.HasExited) child.Kill();
            }
        }

        public static int RunTestWindow()
        {
            Application app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            Window window = new Window
            {
                Title = "WindowMemory 集成测试窗口",
                Width = 480,
                Height = 320,
                Left = 120,
                Top = 120,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false
            };
            System.Windows.Threading.DispatcherTimer timeout = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            timeout.Tick += delegate { timeout.Stop(); window.Close(); };
            timeout.Start();
            app.Run(window);
            return 0;
        }

        private static void TestConfigMigration()
        {
            string directory = Path.Combine(Path.GetTempPath(), "WindowMemory-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                AppState legacy = new AppState { SchemaVersion = 2 };
                legacy.Preferences.MinimizeToTray = false;
                legacy.Rules.Add(new WindowRule
                {
                    Matcher = new WindowMatcher
                    {
                        ProcessPath = @"C:\Tools\demo.exe",
                        ProcessName = "demo",
                        ClassName = "LegacyWindowClass",
                        TitleMode = TitleMatchMode.Ignore
                    }
                });
                string path = Path.Combine(directory, "settings.json");
                using (FileStream stream = File.Create(path))
                    new DataContractJsonSerializer(typeof(AppState)).WriteObject(stream, legacy);

                ConfigService config = new ConfigService(directory);
                AppState migrated = config.Load();
                Assert(migrated.SchemaVersion == 4 && migrated.Preferences.MinimizeToTray,
                    "旧配置没有升级为后台托盘模式");
                Assert(migrated.Preferences.ScanIntervalMs == 0, "旧配置没有升级为即时检测");
                Assert(string.IsNullOrEmpty(migrated.Rules[0].Matcher.ClassName),
                    "旧的忽略标题规则没有升级为程序绑定");
                string saved = File.ReadAllText(path);
                Assert(saved.Contains("\"SchemaVersion\":4"), "升级后的配置没有写回磁盘");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void TestDuplicateRulePrecedence()
        {
            WindowMatcher firstMatcher = new WindowMatcher
            {
                ProcessPath = @"C:\Tools\demo.exe",
                ProcessName = "demo",
                ClassName = string.Empty,
                TitleMode = TitleMatchMode.Ignore
            };
            List<WindowRule> rules = new List<WindowRule>
            {
                new WindowRule { Name = "旧位置", Matcher = firstMatcher, Enabled = true },
                new WindowRule { Name = "新位置", Matcher = firstMatcher.Clone(), Enabled = true }
            };

            WindowRule.RefreshShadowedState(rules);
            Assert(rules[0].IsShadowed && !rules[1].IsShadowed, "重复规则没有遵循最后一条生效");
            Assert(rules[0].EnabledLabel == "已覆盖" && rules[1].EnabledLabel == "自动", "重复规则状态标签错误");

            rules[1].Enabled = false;
            WindowRule.RefreshShadowedState(rules);
            Assert(rules[0].IsShadowed && rules[1].EnabledLabel == "暂停", "最后一条暂停时不应启用旧规则");

            rules.RemoveAt(1);
            WindowRule.RefreshShadowedState(rules);
            Assert(!rules[0].IsShadowed && rules[0].EnabledLabel == "自动", "删除最后一条后旧规则没有恢复");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
