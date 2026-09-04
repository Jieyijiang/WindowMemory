using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;

namespace WindowMemory
{
    public sealed class AutoRestoreEngine : IDisposable
    {
        private readonly WindowService _windows;
        private readonly object _sync = new object();
        private readonly HashSet<string> _applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Timer _timer;
        private List<WindowRule> _rules = new List<WindowRule>();
        private int _interval = 700;
        private bool _paused;
        private int _busy;

        public event Action<string> StatusChanged;

        public AutoRestoreEngine(WindowService windows)
        {
            _windows = windows;
        }

        public void Update(IEnumerable<WindowRule> rules, int interval, bool paused)
        {
            lock (_sync)
            {
                _rules = new List<WindowRule>();
                if (rules != null)
                    foreach (WindowRule rule in rules)
                        _rules.Add(rule.Clone());
                _interval = Math.Max(250, Math.Min(5000, interval));
                _paused = paused;
            }
            if (_timer != null) _timer.Change(_interval, _interval);
        }

        public void Start()
        {
            if (_timer != null) return;
            _timer = new Timer(Scan, null, _interval, _interval);
        }

        private void Scan(object state)
        {
            if (Interlocked.Exchange(ref _busy, 1) != 0) return;
            try
            {
                List<WindowRule> rules;
                bool paused;
                lock (_sync)
                {
                    paused = _paused;
                    rules = new List<WindowRule>();
                    foreach (WindowRule rule in _rules) rules.Add(rule.Clone());
                }
                if (paused) return;

                IList<WindowDescriptor> visible = _windows.EnumerateWindows();
                HashSet<string> alive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (WindowRule rule in rules)
                {
                    if (!rule.Enabled) continue;
                    foreach (WindowDescriptor window in visible)
                    {
                        int score;
                        if (!_windows.Matches(rule.Matcher, window, out score)) continue;
                        string token = rule.Id + ":" + window.Handle.ToInt64();
                        alive.Add(token);
                        if (!rule.KeepPosition && _applied.Contains(token)) continue;

                        string error;
                        if (_windows.ApplyPlacement(window.Handle, rule.Placement, false, out error))
                        {
                            _applied.Add(token);
                            RaiseStatus("自动还原：" + rule.Name);
                        }
                        else
                        {
                            RaiseStatus("无法还原“" + rule.Name + "”：" + error);
                        }
                    }
                }
                _applied.RemoveWhere(delegate(string token) { return !alive.Contains(token); });
            }
            catch (Exception ex)
            {
                RaiseStatus("自动还原检查失败：" + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        public RestoreSummary RestoreLayout(LayoutProfile profile)
        {
            RestoreSummary summary = new RestoreSummary();
            if (profile == null || profile.Windows == null) return summary;
            IList<WindowDescriptor> windows = _windows.EnumerateWindows();
            HashSet<IntPtr> used = new HashSet<IntPtr>();
            foreach (LayoutWindowEntry entry in profile.Windows)
            {
                WindowDescriptor target = _windows.FindBestMatch(entry.Matcher, windows, used);
                if (target == null)
                {
                    summary.Missing++;
                    summary.Messages.Add("未找到：" + entry.Name);
                    continue;
                }

                string error;
                if (_windows.ApplyPlacement(target.Handle, entry.Placement, false, out error))
                {
                    used.Add(target.Handle);
                    summary.Restored++;
                }
                else
                {
                    summary.Failed++;
                    summary.Messages.Add(entry.Name + "：" + error);
                }
            }
            RaiseStatus(profile.Name + " · " + summary.ToDisplayText());
            return summary;
        }

        private void RaiseStatus(string message)
        {
            Action<string> handler = StatusChanged;
            if (handler != null) handler(message);
        }

        public void Dispose()
        {
            if (_timer != null) _timer.Dispose();
            _timer = null;
        }
    }

    public sealed class HotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();
        private readonly List<int> _ids = new List<int>();
        private IntPtr _handle;
        private HwndSource _source;
        private int _nextId = 4100;

        public void Attach(System.Windows.Window window)
        {
            WindowInteropHelper helper = new WindowInteropHelper(window);
            _handle = helper.Handle;
            _source = HwndSource.FromHwnd(_handle);
            if (_source != null) _source.AddHook(WndProc);
        }

        public void Clear()
        {
            foreach (int id in _ids) UnregisterHotKey(_handle, id);
            _ids.Clear();
            _actions.Clear();
        }

        public bool Register(string gestureText, Action action, out string error)
        {
            error = string.Empty;
            HotkeyGesture gesture;
            if (!HotkeyGesture.TryParse(gestureText, out gesture, out error)) return false;
            int id = _nextId++;
            if (!RegisterHotKey(_handle, id, gesture.Modifiers | MOD_NOREPEAT, gesture.VirtualKey))
            {
                error = "快捷键可能已被其他程序占用";
                return false;
            }
            _ids.Add(id);
            _actions[id] = action;
            return true;
        }

        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WM_HOTKEY)
            {
                Action action;
                if (_actions.TryGetValue(wParam.ToInt32(), out action))
                {
                    handled = true;
                    if (action != null) action();
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Clear();
            if (_source != null) _source.RemoveHook(WndProc);
            _source = null;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        public sealed class HotkeyGesture
        {
            public uint Modifiers { get; private set; }
            public uint VirtualKey { get; private set; }
            public string DisplayText { get; private set; }

            public static bool TryParse(string text, out HotkeyGesture gesture, out string error)
            {
                gesture = null;
                error = string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    error = "快捷键不能为空";
                    return false;
                }

                uint modifiers = 0;
                Key key = Key.None;
                string[] parts = text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string raw in parts)
                {
                    string part = raw.Trim();
                    if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                        modifiers |= MOD_CONTROL;
                    else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                        modifiers |= MOD_ALT;
                    else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                        modifiers |= MOD_SHIFT;
                    else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                        modifiers |= MOD_WIN;
                    else
                    {
                        string keyName = part.Length == 1 && char.IsDigit(part[0]) ? "D" + part : part;
                        if (!Enum.TryParse(keyName, true, out key) || key == Key.None)
                        {
                            error = "无法识别按键：“" + part + "”";
                            return false;
                        }
                    }
                }

                if (key == Key.None)
                {
                    error = "需要指定一个主按键";
                    return false;
                }
                if (modifiers == 0)
                {
                    error = "全局快捷键至少需要 Ctrl、Alt、Shift 或 Win 中的一个修饰键";
                    return false;
                }

                int virtualKey = KeyInterop.VirtualKeyFromKey(key);
                if (virtualKey <= 0)
                {
                    error = "该按键不能注册为全局快捷键";
                    return false;
                }

                gesture = new HotkeyGesture
                {
                    Modifiers = modifiers,
                    VirtualKey = (uint)virtualKey,
                    DisplayText = Format(modifiers, key)
                };
                return true;
            }

            public static string FromKeyEvent(Key key, ModifierKeys modifiers)
            {
                if (key == Key.System) key = Keyboard.PrimaryDevice.ActiveSource == null ? Key.None : Key.System;
                if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin || key == Key.None)
                    return string.Empty;

                uint nativeModifiers = 0;
                if ((modifiers & ModifierKeys.Control) != 0) nativeModifiers |= MOD_CONTROL;
                if ((modifiers & ModifierKeys.Alt) != 0) nativeModifiers |= MOD_ALT;
                if ((modifiers & ModifierKeys.Shift) != 0) nativeModifiers |= MOD_SHIFT;
                if ((modifiers & ModifierKeys.Windows) != 0) nativeModifiers |= MOD_WIN;
                if (nativeModifiers == 0) return string.Empty;
                return Format(nativeModifiers, key);
            }

            private static string Format(uint modifiers, Key key)
            {
                List<string> parts = new List<string>();
                if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
                if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
                if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
                if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
                string keyText = key.ToString();
                if (keyText.Length == 2 && keyText[0] == 'D' && char.IsDigit(keyText[1])) keyText = keyText.Substring(1);
                parts.Add(keyText);
                return string.Join("+", parts.ToArray());
            }
        }
    }

    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "WindowMemory";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return key != null && key.GetValue(ValueName) != null;
            }
            catch { return false; }
        }

        public static bool SetEnabled(bool enabled, out string error)
        {
            error = string.Empty;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled)
                    {
                        string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(ValueName, "\"" + exe + "\" --background", RegistryValueKind.String);
                    }
                    else key.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
