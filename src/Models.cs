using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace WindowMemory
{
    public enum TitleMatchMode
    {
        Ignore = 0,
        Exact = 1,
        Contains = 2,
        StartsWith = 3,
        Regex = 4
    }

    [DataContract]
    public sealed class AppState
    {
        [DataMember(Order = 1)] public int SchemaVersion { get; set; }
        [DataMember(Order = 2)] public AppPreferences Preferences { get; set; }
        [DataMember(Order = 3)] public List<WindowRule> Rules { get; set; }
        [DataMember(Order = 4)] public List<LayoutProfile> Layouts { get; set; }

        public AppState()
        {
            SchemaVersion = 2;
            Preferences = new AppPreferences();
            Rules = new List<WindowRule>();
            Layouts = new List<LayoutProfile>();
        }
    }

    [DataContract]
    public sealed class AppPreferences
    {
        [DataMember(Order = 1)] public string CaptureHotkey { get; set; }
        [DataMember(Order = 2)] public int ScanIntervalMs { get; set; }
        [DataMember(Order = 3)] public bool MinimizeToTray { get; set; }
        [DataMember(Order = 4)] public bool AutoStart { get; set; }
        [DataMember(Order = 5)] public bool AutoRestorePaused { get; set; }

        public AppPreferences()
        {
            CaptureHotkey = "Ctrl+Alt+Z";
            ScanIntervalMs = 700;
            MinimizeToTray = false;
            AutoStart = false;
            AutoRestorePaused = false;
        }
    }

    [DataContract]
    public sealed class WindowMatcher
    {
        [DataMember(Order = 1)] public string ProcessPath { get; set; }
        [DataMember(Order = 2)] public string ProcessName { get; set; }
        [DataMember(Order = 3)] public string ClassName { get; set; }
        [DataMember(Order = 4)] public string TitleText { get; set; }
        [DataMember(Order = 5)] public TitleMatchMode TitleMode { get; set; }

        public WindowMatcher()
        {
            ProcessPath = string.Empty;
            ProcessName = string.Empty;
            ClassName = string.Empty;
            TitleText = string.Empty;
            TitleMode = TitleMatchMode.Exact;
        }

        public WindowMatcher Clone()
        {
            return new WindowMatcher
            {
                ProcessPath = ProcessPath,
                ProcessName = ProcessName,
                ClassName = ClassName,
                TitleText = TitleText,
                TitleMode = TitleMode
            };
        }

        public string Summary
        {
            get
            {
                string app = !string.IsNullOrWhiteSpace(ProcessName)
                    ? ProcessName
                    : (!string.IsNullOrWhiteSpace(ProcessPath) ? Path.GetFileName(ProcessPath) : "任意程序");
                if (TitleMode == TitleMatchMode.Ignore || string.IsNullOrWhiteSpace(TitleText))
                    return app + " · 忽略标题";
                return app + " · " + ModeLabel + "“" + TitleText + "”";
            }
        }

        public string ModeLabel
        {
            get
            {
                switch (TitleMode)
                {
                    case TitleMatchMode.Exact: return "标题等于 ";
                    case TitleMatchMode.Contains: return "标题包含 ";
                    case TitleMatchMode.StartsWith: return "标题开头是 ";
                    case TitleMatchMode.Regex: return "正则 ";
                    default: return string.Empty;
                }
            }
        }
    }

    [DataContract]
    public sealed class SavedPlacement
    {
        [DataMember(Order = 1)] public int X { get; set; }
        [DataMember(Order = 2)] public int Y { get; set; }
        [DataMember(Order = 3)] public int Width { get; set; }
        [DataMember(Order = 4)] public int Height { get; set; }
        [DataMember(Order = 5)] public bool Maximized { get; set; }
        [DataMember(Order = 6)] public string MonitorDevice { get; set; }
        [DataMember(Order = 7)] public int WorkX { get; set; }
        [DataMember(Order = 8)] public int WorkY { get; set; }
        [DataMember(Order = 9)] public int WorkWidth { get; set; }
        [DataMember(Order = 10)] public int WorkHeight { get; set; }
        [DataMember(Order = 11)] public double RelativeX { get; set; }
        [DataMember(Order = 12)] public double RelativeY { get; set; }
        [DataMember(Order = 13)] public double RelativeWidth { get; set; }
        [DataMember(Order = 14)] public double RelativeHeight { get; set; }
        [DataMember(Order = 15)] public bool ScaleWithMonitor { get; set; }

        public SavedPlacement()
        {
            MonitorDevice = string.Empty;
            ScaleWithMonitor = true;
        }

        public SavedPlacement Clone()
        {
            return (SavedPlacement)MemberwiseClone();
        }

        public string Summary
        {
            get
            {
                string state = Maximized ? " · 最大化" : string.Empty;
                return Width + " × " + Height + "  @  " + X + ", " + Y + state;
            }
        }
    }

    [DataContract]
    public sealed class WindowRule
    {
        [DataMember(Order = 1)] public string Id { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public bool Enabled { get; set; }
        [DataMember(Order = 4)] public bool KeepPosition { get; set; }
        [DataMember(Order = 5)] public WindowMatcher Matcher { get; set; }
        [DataMember(Order = 6)] public SavedPlacement Placement { get; set; }

        public WindowRule()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "未命名窗口";
            Enabled = true;
            KeepPosition = false;
            Matcher = new WindowMatcher();
            Placement = new SavedPlacement();
        }

        public string EnabledLabel { get { return Enabled ? "自动" : "暂停"; } }
        public string MatcherSummary { get { return Matcher == null ? "未配置" : Matcher.Summary; } }
        public string PlacementSummary { get { return Placement == null ? "未配置" : Placement.Summary; } }

        public WindowRule Clone()
        {
            return new WindowRule
            {
                Id = Id,
                Name = Name,
                Enabled = Enabled,
                KeepPosition = KeepPosition,
                Matcher = Matcher == null ? new WindowMatcher() : Matcher.Clone(),
                Placement = Placement == null ? new SavedPlacement() : Placement.Clone()
            };
        }
    }

    [DataContract]
    public sealed class LayoutWindowEntry
    {
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public WindowMatcher Matcher { get; set; }
        [DataMember(Order = 3)] public SavedPlacement Placement { get; set; }

        public LayoutWindowEntry()
        {
            Name = "窗口";
            Matcher = new WindowMatcher();
            Placement = new SavedPlacement();
        }

        public LayoutWindowEntry Clone()
        {
            return new LayoutWindowEntry
            {
                Name = Name,
                Matcher = Matcher == null ? new WindowMatcher() : Matcher.Clone(),
                Placement = Placement == null ? new SavedPlacement() : Placement.Clone()
            };
        }
    }

    [DataContract]
    public sealed class LayoutProfile
    {
        [DataMember(Order = 1)] public string Id { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public string Hotkey { get; set; }
        [DataMember(Order = 4)] public List<LayoutWindowEntry> Windows { get; set; }

        public LayoutProfile()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "新布局";
            Hotkey = string.Empty;
            Windows = new List<LayoutWindowEntry>();
        }

        public int WindowCount { get { return Windows == null ? 0 : Windows.Count; } }
        public string WindowCountLabel { get { return WindowCount + " 个窗口"; } }
        public string HotkeyLabel { get { return string.IsNullOrWhiteSpace(Hotkey) ? "未设置" : Hotkey; } }

        public LayoutProfile Clone()
        {
            LayoutProfile copy = new LayoutProfile { Id = Id, Name = Name, Hotkey = Hotkey };
            if (Windows != null)
                foreach (LayoutWindowEntry item in Windows)
                    copy.Windows.Add(item.Clone());
            return copy;
        }
    }

    public sealed class WindowDescriptor : INotifyPropertyChanged
    {
        private bool _isSelected;
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public string ProcessPath { get; set; }
        public string ProcessName { get; set; }
        public string ClassName { get; set; }
        public NativeRect Bounds { get; set; }
        public bool Maximized { get; set; }
        public string MonitorDevice { get; set; }
        public NativeRect MonitorWorkArea { get; set; }

        public WindowDescriptor()
        {
            Title = string.Empty;
            ProcessPath = string.Empty;
            ProcessName = string.Empty;
            ClassName = string.Empty;
            MonitorDevice = string.Empty;
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("IsSelected"));
            }
        }

        public string AppLabel { get { return string.IsNullOrWhiteSpace(ProcessName) ? "未知程序" : ProcessName; } }
        public string SizeLabel { get { return Bounds.Width + " × " + Bounds.Height; } }
        public string PositionLabel { get { return Bounds.Left + ", " + Bounds.Top; } }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width { get { return Math.Max(0, Right - Left); } }
        public int Height { get { return Math.Max(0, Bottom - Top); } }

        public NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    public sealed class RestoreSummary
    {
        public int Restored { get; set; }
        public int Missing { get; set; }
        public int Failed { get; set; }
        public List<string> Messages { get; private set; }

        public RestoreSummary()
        {
            Messages = new List<string>();
        }

        public string ToDisplayText()
        {
            if (Failed == 0 && Missing == 0) return "已还原 " + Restored + " 个窗口";
            return "已还原 " + Restored + " 个，未找到 " + Missing + " 个，失败 " + Failed + " 个";
        }
    }
}
