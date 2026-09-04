using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowMemory
{
    public sealed class WindowService
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int SW_RESTORE = 9;
        private const int SW_MAXIMIZE = 3;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int DWMWA_CLOAKED = 14;

        public IList<WindowDescriptor> EnumerateWindows()
        {
            List<WindowDescriptor> result = new List<WindowDescriptor>();
            IntPtr shell = GetShellWindow();
            int ownProcess = Process.GetCurrentProcess().Id;

            EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                if (hwnd == shell || !IsWindowVisible(hwnd)) return true;
                int length = GetWindowTextLength(hwnd);
                if (length <= 0) return true;

                int cloaked = 0;
                try { DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, Marshal.SizeOf(typeof(int))); }
                catch { cloaked = 0; }
                if (cloaked != 0) return true;

                uint processId;
                GetWindowThreadProcessId(hwnd, out processId);
                if (processId == ownProcess) return true;

                NativeRect rect;
                if (!GetWindowRect(hwnd, out rect) || rect.Width < 40 || rect.Height < 30) return true;

                string title = ReadText(hwnd, length);
                string className = ReadClass(hwnd);
                string processPath = ReadProcessPath(processId);
                string processName = string.IsNullOrWhiteSpace(processPath)
                    ? ReadProcessName(processId)
                    : Path.GetFileNameWithoutExtension(processPath);

                MonitorInfo monitor = ReadMonitorForWindow(hwnd);
                result.Add(new WindowDescriptor
                {
                    Handle = hwnd,
                    Title = title,
                    ClassName = className,
                    ProcessPath = processPath,
                    ProcessName = processName,
                    Bounds = rect,
                    Maximized = IsZoomed(hwnd),
                    MonitorDevice = monitor.Device,
                    MonitorWorkArea = monitor.WorkArea
                });
                return true;
            }, IntPtr.Zero);

            result.Sort(delegate(WindowDescriptor a, WindowDescriptor b)
            {
                int app = string.Compare(a.AppLabel, b.AppLabel, StringComparison.CurrentCultureIgnoreCase);
                return app != 0 ? app : string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        public WindowDescriptor GetForegroundDescriptor()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return null;
            IList<WindowDescriptor> windows = EnumerateWindows();
            foreach (WindowDescriptor item in windows)
                if (item.Handle == foreground) return item;
            return null;
        }

        public WindowMatcher CreateMatcher(WindowDescriptor window, TitleMatchMode mode)
        {
            return new WindowMatcher
            {
                ProcessPath = window.ProcessPath,
                ProcessName = window.ProcessName,
                ClassName = window.ClassName,
                TitleText = mode == TitleMatchMode.Ignore ? string.Empty : window.Title,
                TitleMode = mode
            };
        }

        public SavedPlacement CreatePlacement(WindowDescriptor window)
        {
            NativeRect work = window.MonitorWorkArea;
            int workWidth = Math.Max(1, work.Width);
            int workHeight = Math.Max(1, work.Height);
            return new SavedPlacement
            {
                X = window.Bounds.Left,
                Y = window.Bounds.Top,
                Width = window.Bounds.Width,
                Height = window.Bounds.Height,
                Maximized = window.Maximized,
                MonitorDevice = window.MonitorDevice,
                WorkX = work.Left,
                WorkY = work.Top,
                WorkWidth = workWidth,
                WorkHeight = workHeight,
                RelativeX = (double)(window.Bounds.Left - work.Left) / workWidth,
                RelativeY = (double)(window.Bounds.Top - work.Top) / workHeight,
                RelativeWidth = (double)window.Bounds.Width / workWidth,
                RelativeHeight = (double)window.Bounds.Height / workHeight,
                ScaleWithMonitor = true
            };
        }

        public WindowDescriptor FindBestMatch(WindowMatcher matcher, IList<WindowDescriptor> windows, ISet<IntPtr> excluded)
        {
            WindowDescriptor best = null;
            int bestScore = -1;
            foreach (WindowDescriptor window in windows)
            {
                if (excluded != null && excluded.Contains(window.Handle)) continue;
                int score;
                if (!Matches(matcher, window, out score)) continue;
                if (score > bestScore)
                {
                    best = window;
                    bestScore = score;
                }
            }
            return best;
        }

        public bool Matches(WindowMatcher matcher, WindowDescriptor window, out int score)
        {
            score = 0;
            if (matcher == null || window == null) return false;

            if (!string.IsNullOrWhiteSpace(matcher.ProcessPath))
            {
                if (!string.Equals(NormalizePath(matcher.ProcessPath), NormalizePath(window.ProcessPath), StringComparison.OrdinalIgnoreCase))
                    return false;
                score += 100;
            }
            else if (!string.IsNullOrWhiteSpace(matcher.ProcessName))
            {
                if (!string.Equals(matcher.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase)) return false;
                score += 60;
            }

            if (!string.IsNullOrWhiteSpace(matcher.ClassName))
            {
                if (!string.Equals(matcher.ClassName, window.ClassName, StringComparison.Ordinal)) return false;
                score += 30;
            }

            if (matcher.TitleMode == TitleMatchMode.Ignore || string.IsNullOrWhiteSpace(matcher.TitleText))
                return score > 0;

            bool titleMatch;
            switch (matcher.TitleMode)
            {
                case TitleMatchMode.Exact:
                    titleMatch = string.Equals(window.Title, matcher.TitleText, StringComparison.CurrentCultureIgnoreCase);
                    score += titleMatch ? 80 : 0;
                    break;
                case TitleMatchMode.Contains:
                    titleMatch = window.Title.IndexOf(matcher.TitleText, StringComparison.CurrentCultureIgnoreCase) >= 0;
                    score += titleMatch ? 50 : 0;
                    break;
                case TitleMatchMode.StartsWith:
                    titleMatch = window.Title.StartsWith(matcher.TitleText, StringComparison.CurrentCultureIgnoreCase);
                    score += titleMatch ? 60 : 0;
                    break;
                case TitleMatchMode.Regex:
                    try { titleMatch = Regex.IsMatch(window.Title, matcher.TitleText, RegexOptions.IgnoreCase); }
                    catch { titleMatch = false; }
                    score += titleMatch ? 45 : 0;
                    break;
                default:
                    titleMatch = true;
                    break;
            }
            return titleMatch;
        }

        public bool ApplyPlacement(IntPtr hwnd, SavedPlacement placement, bool activate, out string error)
        {
            error = string.Empty;
            if (hwnd == IntPtr.Zero || placement == null)
            {
                error = "窗口或位置无效";
                return false;
            }

            NativeRect target = ResolveTarget(hwnd, placement);
            if (IsIconic(hwnd) || IsZoomed(hwnd)) ShowWindow(hwnd, SW_RESTORE);

            if (!SetWindowPos(hwnd, IntPtr.Zero, target.Left, target.Top, target.Width, target.Height,
                SWP_NOZORDER | (activate ? 0u : SWP_NOACTIVATE)))
            {
                error = "Windows 拒绝调整，错误码 " + Marshal.GetLastWin32Error();
                return false;
            }

            if (placement.Maximized) ShowWindow(hwnd, SW_MAXIMIZE);
            if (activate) SetForegroundWindow(hwnd);
            return true;
        }

        private NativeRect ResolveTarget(IntPtr hwnd, SavedPlacement placement)
        {
            MonitorInfo targetMonitor = FindMonitor(placement.MonitorDevice);
            if (targetMonitor.Handle == IntPtr.Zero) targetMonitor = ReadMonitorForWindow(hwnd);

            if (!placement.ScaleWithMonitor || targetMonitor.WorkArea.Width <= 0 || targetMonitor.WorkArea.Height <= 0)
                return Clamp(new NativeRect(placement.X, placement.Y, placement.X + placement.Width, placement.Y + placement.Height), targetMonitor.WorkArea);

            NativeRect work = targetMonitor.WorkArea;
            int width = Math.Max(120, (int)Math.Round(placement.RelativeWidth * work.Width));
            int height = Math.Max(80, (int)Math.Round(placement.RelativeHeight * work.Height));
            int x = work.Left + (int)Math.Round(placement.RelativeX * work.Width);
            int y = work.Top + (int)Math.Round(placement.RelativeY * work.Height);
            return Clamp(new NativeRect(x, y, x + width, y + height), work);
        }

        private static NativeRect Clamp(NativeRect rect, NativeRect work)
        {
            int width = Math.Min(Math.Max(120, rect.Width), Math.Max(120, work.Width));
            int height = Math.Min(Math.Max(80, rect.Height), Math.Max(80, work.Height));
            int x = Math.Max(work.Left, Math.Min(rect.Left, work.Right - width));
            int y = Math.Max(work.Top, Math.Min(rect.Top, work.Bottom - height));
            return new NativeRect(x, y, x + width, y + height);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try { return Path.GetFullPath(path).TrimEnd('\\'); }
            catch { return path.Trim(); }
        }

        private static string ReadText(IntPtr hwnd, int length)
        {
            StringBuilder text = new StringBuilder(length + 1);
            GetWindowText(hwnd, text, text.Capacity);
            return text.ToString();
        }

        private static string ReadClass(IntPtr hwnd)
        {
            StringBuilder text = new StringBuilder(256);
            GetClassName(hwnd, text, text.Capacity);
            return text.ToString();
        }

        private static string ReadProcessName(uint processId)
        {
            try { return Process.GetProcessById((int)processId).ProcessName; }
            catch { return string.Empty; }
        }

        private static string ReadProcessPath(uint processId)
        {
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero) return string.Empty;
            try
            {
                StringBuilder path = new StringBuilder(1024);
                int length = path.Capacity;
                return QueryFullProcessImageName(process, 0, path, ref length) ? path.ToString() : string.Empty;
            }
            finally { CloseHandle(process); }
        }

        private static MonitorInfo ReadMonitorForWindow(IntPtr hwnd)
        {
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            return ReadMonitorHandle(monitor);
        }

        private static MonitorInfo ReadMonitorHandle(IntPtr monitor)
        {
            MonitorInfoEx info = new MonitorInfoEx();
            info.Size = Marshal.SizeOf(typeof(MonitorInfoEx));
            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                return new MonitorInfo { Handle = monitor, Device = info.Device, WorkArea = info.WorkArea };
            return new MonitorInfo { Handle = IntPtr.Zero, Device = string.Empty, WorkArea = new NativeRect(0, 0, 1920, 1080) };
        }

        private static MonitorInfo FindMonitor(string device)
        {
            MonitorInfo found = new MonitorInfo();
            if (string.IsNullOrWhiteSpace(device)) return found;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr monitor, IntPtr hdc, ref NativeRect rect, IntPtr data)
            {
                MonitorInfo item = ReadMonitorHandle(monitor);
                if (string.Equals(item.Device, device, StringComparison.OrdinalIgnoreCase))
                {
                    found = item;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private struct MonitorInfo
        {
            public IntPtr Handle;
            public string Device;
            public NativeRect WorkArea;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfoEx
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref NativeRect rect, IntPtr data);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int maxCount);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
        [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder path, ref int size);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    }
}
