using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WindowMemory
{
    public sealed class MainWindow : Window
    {
        private readonly ConfigService _config;
        private readonly WindowService _windows;
        private readonly AutoRestoreEngine _engine;
        private readonly HotkeyService _hotkeys;
        private readonly AppState _state;
        private readonly Dictionary<string, FrameworkElement> _pages = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();
        private readonly bool _startHidden;
        private Forms.NotifyIcon _tray;
        private Drawing.Icon _trayIcon;
        private Grid _contentHost;
        private DataGrid _rulesGrid;
        private DataGrid _layoutsGrid;
        private TextBlock _rulesEmpty;
        private TextBlock _layoutsEmpty;
        private TextBlock _statusText;
        private TextBlock _ruleCount;
        private TextBlock _layoutCount;
        private TextBlock _autoState;
        private readonly List<TextBlock> _captureHotkeyTexts = new List<TextBlock>();
        private ComboBox _scanInterval;
        private CheckBox _autoStart;
        private CheckBox _paused;
        private CheckBox _minimizeToTray;
        private bool _allowExit;
        private bool _loadingSettings;
        private int _layoutRestoreBusy;

        public MainWindow(bool startHidden) : this(startHidden, false)
        {
        }

        internal MainWindow(bool startHidden, bool previewOnly)
        {
            _startHidden = startHidden;
            _config = new ConfigService();
            _windows = new WindowService();
            _state = _config.Load();
            _engine = new AutoRestoreEngine(_windows);
            _hotkeys = new HotkeyService();

            Title = "Window Memory · 窗口与布局记忆";
            Width = 1180;
            Height = 760;
            MinWidth = 980;
            MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Brush("BackgroundBrush");
            ShowInTaskbar = true;
            using (Stream iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WindowMemory.app.ico"))
                if (iconStream != null) Icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

            BuildUi();
            if (!previewOnly) CreateTray();
            RefreshEverything();

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += OnClosing;
            StateChanged += OnStateChanged;
            _engine.StatusChanged += OnEngineStatus;
        }

        internal void SavePreview(string path, string page)
        {
            if (!string.IsNullOrWhiteSpace(page) && _pages.ContainsKey(page)) ShowPage(page);
            FrameworkElement element = Content as FrameworkElement;
            if (element == null) throw new InvalidOperationException("界面尚未创建");
            const int width = 1180;
            const int height = 760;
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (System.IO.FileStream stream = System.IO.File.Create(path)) encoder.Save(stream);
        }

        private void BuildUi()
        {
            Grid root = new Grid { Background = Ui.Brush("BackgroundBrush") };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border sidebar = new Border
            {
                Background = Ui.Brush("SidebarBrush"),
                BorderBrush = Ui.Brush("BorderBrush"),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(18, 22, 18, 18)
            };
            Grid sideGrid = new Grid();
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 26) };
            Border mark = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(10),
                Background = Ui.Brush("AccentBrush"),
                Child = Ui.Heading("W", 18)
            };
            ((TextBlock)mark.Child).HorizontalAlignment = HorizontalAlignment.Center;
            brand.Children.Add(mark);
            StackPanel brandText = new StackPanel { Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            brandText.Children.Add(Ui.Heading("Window Memory", 15));
            brandText.Children.Add(Ui.Muted("窗口与布局记忆", 11));
            brand.Children.Add(brandText);
            sideGrid.Children.Add(brand);

            StackPanel nav = new StackPanel();
            AddNav(nav, "dashboard", "概览");
            AddNav(nav, "rules", "窗口规则");
            AddNav(nav, "layouts", "布局存档");
            AddNav(nav, "settings", "设置");
            Grid.SetRow(nav, 1);
            sideGrid.Children.Add(nav);

            Border statusCard = new Border
            {
                Background = Ui.Brush("SurfaceBrush"),
                BorderBrush = Ui.Brush("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 12, 0, 0)
            };
            StackPanel statusStack = new StackPanel();
            TextBlock statusLabel = Ui.Muted("运行状态", 11);
            statusLabel.Margin = new Thickness(0, 0, 0, 4);
            statusStack.Children.Add(statusLabel);
            _statusText = Ui.Text("准备就绪", 12, Ui.Brush("TextBrush"), FontWeights.SemiBold);
            _statusText.TextWrapping = TextWrapping.Wrap;
            statusStack.Children.Add(_statusText);
            statusCard.Child = statusStack;
            Grid.SetRow(statusCard, 3);
            sideGrid.Children.Add(statusCard);
            sidebar.Child = sideGrid;
            root.Children.Add(sidebar);

            _contentHost = new Grid { Margin = new Thickness(32, 26, 32, 24) };
            Grid.SetColumn(_contentHost, 1);
            root.Children.Add(_contentHost);

            AddPage("dashboard", BuildDashboard());
            AddPage("rules", BuildRulesPage());
            AddPage("layouts", BuildLayoutsPage());
            AddPage("settings", BuildSettingsPage());
            ShowPage("dashboard");
            Content = root;
        }

        private void AddNav(Panel panel, string key, string label)
        {
            Button button = Ui.Button(label, delegate { ShowPage(key); }, "NavButton");
            button.Tag = key;
            _navButtons[key] = button;
            panel.Children.Add(button);
        }

        private void AddPage(string key, FrameworkElement page)
        {
            page.Visibility = Visibility.Collapsed;
            _pages[key] = page;
            _contentHost.Children.Add(page);
        }

        private void ShowPage(string key)
        {
            foreach (KeyValuePair<string, FrameworkElement> page in _pages)
                page.Value.Visibility = page.Key == key ? Visibility.Visible : Visibility.Collapsed;
            foreach (KeyValuePair<string, Button> nav in _navButtons)
            {
                bool selected = nav.Key == key;
                nav.Value.Background = selected ? new SolidColorBrush(Color.FromRgb(28, 48, 72)) : Brushes.Transparent;
                nav.Value.Foreground = selected ? Brushes.White : Ui.Brush("MutedTextBrush");
                nav.Value.BorderBrush = selected ? Ui.Brush("BorderBrush") : Brushes.Transparent;
            }
        }

        private FrameworkElement BuildDashboard()
        {
            Grid root = PageGrid("概览", "自动恢复常用窗口，并用一个快捷键召回整套工作布局。", out StackPanel actions, out StackPanel body);
            Button capture = Ui.Button("记忆一个窗口", AddRule, "PrimaryButton");
            Button layout = Ui.Button("保存当前布局", CaptureLayout, null);
            layout.Margin = new Thickness(10, 0, 0, 0);
            actions.Children.Add(capture);
            actions.Children.Add(layout);

            Grid hero = new Grid();
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            StackPanel heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            heroText.Children.Add(Ui.Heading("把桌面恢复到你熟悉的样子", 25));
            TextBlock description = Ui.Muted("窗口规则负责“打开即归位”，布局存档负责“一次还原多个窗口”。配置保存在程序旁边，可随程序一起带走。", 14);
            description.Margin = new Thickness(0, 10, 24, 0);
            heroText.Children.Add(description);
            hero.Children.Add(heroText);
            Border keyCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 29, 38)),
                BorderBrush = Ui.Brush("AccentBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(16),
                VerticalAlignment = VerticalAlignment.Center
            };
            StackPanel keyStack = new StackPanel();
            keyStack.Children.Add(Ui.Muted("快速记忆当前窗口", 11));
            TextBlock dashboardHotkey = Ui.Heading(_state.Preferences.CaptureHotkey, 20);
            dashboardHotkey.Margin = new Thickness(0, 4, 0, 0);
            _captureHotkeyTexts.Add(dashboardHotkey);
            keyStack.Children.Add(dashboardHotkey);
            keyCard.Child = keyStack;
            Grid.SetColumn(keyCard, 1);
            hero.Children.Add(keyCard);
            body.Children.Add(Ui.Card(hero, new Thickness(0, 0, 0, 20)));

            Grid stats = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            for (int i = 0; i < 3; i++) stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _ruleCount = AddStat(stats, 0, "窗口规则", "0", "自动识别并归位");
            _layoutCount = AddStat(stats, 1, "布局存档", "0", "多窗口同时还原");
            _autoState = AddStat(stats, 2, "自动恢复", "运行中", "后台低频检测");
            body.Children.Add(stats);

            Grid workflow = new Grid();
            workflow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            workflow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            workflow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            workflow.Children.Add(InfoCard("窗口规则", "为单个程序保存识别条件。窗口再次出现时，自动恢复尺寸、位置和显示器。", "管理规则", delegate { ShowPage("rules"); }));
            Border layoutCard = InfoCard("布局存档", "选择两个或更多窗口保存为一套布局；按自定义快捷键即可整体召回。", "管理布局", delegate { ShowPage("layouts"); });
            Grid.SetColumn(layoutCard, 2);
            workflow.Children.Add(layoutCard);
            body.Children.Add(workflow);
            return root;
        }

        private TextBlock AddStat(Grid grid, int column, string label, string value, string detail)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(Ui.Muted(label, 11));
            TextBlock number = Ui.Heading(value, 24);
            number.Margin = new Thickness(0, 4, 0, 3);
            stack.Children.Add(number);
            stack.Children.Add(Ui.Muted(detail, 12));
            Border card = Ui.Card(stack, new Thickness(column == 0 ? 0 : 8, 0, column == 2 ? 0 : 8, 0));
            Grid.SetColumn(card, column);
            grid.Children.Add(card);
            return number;
        }

        private Border InfoCard(string title, string text, string action, RoutedEventHandler click)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(Ui.Heading(title, 17));
            TextBlock body = Ui.Muted(text, 13);
            body.Margin = new Thickness(0, 8, 0, 16);
            stack.Children.Add(body);
            Button button = Ui.Button(action, click, null);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            stack.Children.Add(button);
            return Ui.Card(stack, new Thickness(0));
        }

        private FrameworkElement BuildRulesPage()
        {
            Grid root = PageGrid("窗口规则", "识别指定窗口，在它出现时自动恢复保存的位置和大小。", out StackPanel actions, out StackPanel body);
            actions.Children.Add(Ui.Button("新建规则", AddRule, "PrimaryButton"));

            Border table = Ui.Card(null, new Thickness(0));
            table.Height = 430;
            table.Padding = new Thickness(0);
            _rulesGrid = new DataGrid { IsReadOnly = true };
            _rulesGrid.Columns.Add(new DataGridTextColumn { Header = "规则", Binding = new Binding("Name"), Width = new DataGridLength(170) });
            _rulesGrid.Columns.Add(new DataGridTextColumn { Header = "匹配条件", Binding = new Binding("MatcherSummary"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _rulesGrid.Columns.Add(new DataGridTextColumn { Header = "目标位置", Binding = new Binding("PlacementSummary"), Width = new DataGridLength(190) });
            _rulesGrid.Columns.Add(new DataGridTextColumn { Header = "状态", Binding = new Binding("EnabledLabel"), Width = new DataGridLength(76) });
            _rulesGrid.MouseDoubleClick += EditRule;
            Grid tableLayer = new Grid();
            tableLayer.Children.Add(_rulesGrid);
            _rulesEmpty = Ui.Muted("还没有窗口规则。点击右上角“新建规则”，选择一个正在运行的窗口。", 14);
            _rulesEmpty.HorizontalAlignment = HorizontalAlignment.Center;
            _rulesEmpty.VerticalAlignment = VerticalAlignment.Center;
            _rulesEmpty.MaxWidth = 420;
            _rulesEmpty.TextAlignment = TextAlignment.Center;
            tableLayer.Children.Add(_rulesEmpty);
            table.Child = tableLayer;
            body.Children.Add(table);

            StackPanel toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            toolbar.Children.Add(Ui.Button("立即还原", ApplySelectedRule, null));
            Button edit = Ui.Button("编辑", EditRule, null);
            edit.Margin = new Thickness(8, 0, 0, 0);
            toolbar.Children.Add(edit);
            Button delete = Ui.Button("删除", DeleteRule, "DangerButton");
            delete.Margin = new Thickness(8, 0, 0, 0);
            toolbar.Children.Add(delete);
            body.Children.Add(toolbar);
            return root;
        }

        private FrameworkElement BuildLayoutsPage()
        {
            Grid root = PageGrid("布局存档", "把多个窗口保存为一组，通过按钮或全局快捷键整体恢复。", out StackPanel actions, out StackPanel body);
            actions.Children.Add(Ui.Button("保存当前布局", CaptureLayout, "PrimaryButton"));

            Border table = Ui.Card(null, new Thickness(0));
            table.Height = 430;
            table.Padding = new Thickness(0);
            _layoutsGrid = new DataGrid { IsReadOnly = true };
            _layoutsGrid.Columns.Add(new DataGridTextColumn { Header = "布局名称", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _layoutsGrid.Columns.Add(new DataGridTextColumn { Header = "窗口", Binding = new Binding("WindowCountLabel"), Width = new DataGridLength(130) });
            _layoutsGrid.Columns.Add(new DataGridTextColumn { Header = "快捷键", Binding = new Binding("HotkeyLabel"), Width = new DataGridLength(180) });
            _layoutsGrid.MouseDoubleClick += RestoreSelectedLayout;
            Grid tableLayer = new Grid();
            tableLayer.Children.Add(_layoutsGrid);
            _layoutsEmpty = Ui.Muted("还没有布局存档。保存一组当前窗口，并为它设置一个全局快捷键。", 14);
            _layoutsEmpty.HorizontalAlignment = HorizontalAlignment.Center;
            _layoutsEmpty.VerticalAlignment = VerticalAlignment.Center;
            _layoutsEmpty.MaxWidth = 420;
            _layoutsEmpty.TextAlignment = TextAlignment.Center;
            tableLayer.Children.Add(_layoutsEmpty);
            table.Child = tableLayer;
            body.Children.Add(table);

            StackPanel toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            toolbar.Children.Add(Ui.Button("还原布局", RestoreSelectedLayout, "PrimaryButton"));
            Button edit = Ui.Button("编辑名称与快捷键", EditLayout, null);
            edit.Margin = new Thickness(8, 0, 0, 0);
            toolbar.Children.Add(edit);
            Button delete = Ui.Button("删除", DeleteLayout, "DangerButton");
            delete.Margin = new Thickness(8, 0, 0, 0);
            toolbar.Children.Add(delete);
            body.Children.Add(toolbar);
            return root;
        }

        private FrameworkElement BuildSettingsPage()
        {
            Grid root = PageGrid("设置", "快捷键、后台检测和配置存储。所有改变都会立即保存。", out StackPanel actions, out StackPanel body);

            StackPanel general = new StackPanel();
            general.Children.Add(Ui.Heading("常规", 17));
            general.Children.Add(SettingRow("快速记忆快捷键", "记忆当前活动窗口的位置与大小", CreateHotkeyButton()));

            _scanInterval = new ComboBox { Width = 150, HorizontalAlignment = HorizontalAlignment.Right };
            _scanInterval.Items.Add(new IntervalOption(350, "快速 · 350 ms"));
            _scanInterval.Items.Add(new IntervalOption(700, "平衡 · 700 ms"));
            _scanInterval.Items.Add(new IntervalOption(1200, "省电 · 1.2 s"));
            _scanInterval.SelectionChanged += ScanIntervalChanged;
            general.Children.Add(SettingRow("检测频率", "窗口出现后多久开始自动归位", _scanInterval));

            _paused = new CheckBox { Content = "暂停自动恢复", IsChecked = _state.Preferences.AutoRestorePaused, HorizontalAlignment = HorizontalAlignment.Right };
            _paused.Checked += PauseChanged;
            _paused.Unchecked += PauseChanged;
            general.Children.Add(SettingRow("自动恢复", "暂停后仍可手动还原布局", _paused));

            _minimizeToTray = new CheckBox { Content = "最小化或关闭时收进托盘", HorizontalAlignment = HorizontalAlignment.Right };
            _minimizeToTray.Checked += MinimizeToTrayChanged;
            _minimizeToTray.Unchecked += MinimizeToTrayChanged;
            general.Children.Add(SettingRow("任务栏行为", "开启后后台运行，但普通任务栏按钮会隐藏", _minimizeToTray));

            _autoStart = new CheckBox { Content = "登录 Windows 后自动运行", HorizontalAlignment = HorizontalAlignment.Right };
            _autoStart.Checked += AutoStartChanged;
            _autoStart.Unchecked += AutoStartChanged;
            general.Children.Add(SettingRow("开机启动", "后台启动并隐藏到系统托盘", _autoStart));
            body.Children.Add(Ui.Card(general, new Thickness(0, 0, 0, 20)));

            StackPanel storage = new StackPanel();
            storage.Children.Add(Ui.Heading("数据与便携模式", 17));
            TextBlock text = Ui.Muted("配置以 JSON 保存。当前版本附带 portable.flag，因此数据默认保存在程序旁边的 Data 目录。", 13);
            text.Margin = new Thickness(0, 8, 0, 14);
            storage.Children.Add(text);
            TextBox path = new TextBox { Text = _config.ConfigPath, IsReadOnly = true };
            storage.Children.Add(path);
            Button open = Ui.Button("打开数据目录", delegate { OpenDataDirectory(); }, null);
            open.HorizontalAlignment = HorizontalAlignment.Left;
            open.Margin = new Thickness(0, 14, 0, 0);
            storage.Children.Add(open);
            body.Children.Add(Ui.Card(storage, new Thickness(0)));
            return root;
        }

        private Button CreateHotkeyButton()
        {
            Button button = Ui.Button("", EditCaptureHotkey, null);
            TextBlock settingsHotkey = Ui.Heading(_state.Preferences.CaptureHotkey, 14);
            _captureHotkeyTexts.Add(settingsHotkey);
            button.Content = settingsHotkey;
            button.HorizontalAlignment = HorizontalAlignment.Right;
            return button;
        }

        private FrameworkElement SettingRow(string title, string description, FrameworkElement control)
        {
            Grid row = new Grid { Margin = new Thickness(0, 15, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel text = new StackPanel();
            text.Children.Add(Ui.Text(title, 14, Ui.Brush("TextBrush"), FontWeights.SemiBold));
            text.Children.Add(Ui.Muted(description, 12));
            row.Children.Add(text);
            Grid.SetColumn(control, 1);
            control.VerticalAlignment = VerticalAlignment.Center;
            control.Margin = new Thickness(20, 0, 0, 0);
            row.Children.Add(control);
            return row;
        }

        private Grid PageGrid(string title, string description, out StackPanel actions, out StackPanel body)
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 22) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel heading = new StackPanel();
            heading.Children.Add(Ui.Heading(title, 26));
            TextBlock desc = Ui.Muted(description, 13);
            desc.Margin = new Thickness(0, 5, 0, 0);
            heading.Children.Add(desc);
            header.Children.Add(heading);
            actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
            root.Children.Add(header);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            body = new StackPanel();
            scroll.Content = body;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);
            return root;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            if (_trayIcon != null)
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                SendMessage(handle, 0x0080, IntPtr.Zero, _trayIcon.Handle);
                SendMessage(handle, 0x0080, new IntPtr(1), _trayIcon.Handle);
            }
            _hotkeys.Attach(this);
            RegisterHotkeys();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _engine.Update(_state.Rules, _state.Preferences.ScanIntervalMs, _state.Preferences.AutoRestorePaused);
            _engine.Start();
            if (!string.IsNullOrWhiteSpace(_config.LastError)) SetStatus(_config.LastError);
            if (_startHidden) Hide();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (!_allowExit && _state.Preferences.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                ShowTrayMessage("Window Memory", "程序仍在后台运行，可从托盘重新打开。");
                return;
            }
            _engine.Dispose();
            _hotkeys.Dispose();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            if (_trayIcon != null) _trayIcon.Dispose();
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _state.Preferences.MinimizeToTray)
            {
                Hide();
                WindowState = WindowState.Normal;
            }
        }

        private void CreateTray()
        {
            _trayIcon = Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            if (_trayIcon == null) _trayIcon = (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
            Icon = Imaging.CreateBitmapSourceFromHIcon(_trayIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            _tray = new Forms.NotifyIcon
            {
                Icon = _trayIcon,
                Text = "Window Memory",
                Visible = true
            };
            _tray.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(ShowMainWindow)); };
            RebuildTrayMenu();
        }

        private void RebuildTrayMenu()
        {
            if (_tray == null) return;
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开 Window Memory", null, delegate { Dispatcher.BeginInvoke(new Action(ShowMainWindow)); });
            menu.Items.Add("记忆当前窗口", null, delegate { Dispatcher.BeginInvoke(new Action(CaptureForegroundWindow)); });
            Forms.ToolStripMenuItem layouts = new Forms.ToolStripMenuItem("还原布局");
            if (_state.Layouts.Count == 0) layouts.DropDownItems.Add("暂无布局").Enabled = false;
            foreach (LayoutProfile profile in _state.Layouts)
            {
                LayoutProfile captured = profile;
                layouts.DropDownItems.Add(profile.Name + (string.IsNullOrWhiteSpace(profile.Hotkey) ? string.Empty : "    " + profile.Hotkey), null,
                    delegate { RestoreLayout(captured); });
            }
            menu.Items.Add(layouts);
            menu.Items.Add(new Forms.ToolStripSeparator());
            Forms.ToolStripMenuItem pause = new Forms.ToolStripMenuItem("暂停自动恢复") { Checked = _state.Preferences.AutoRestorePaused, CheckOnClick = true };
            pause.CheckedChanged += delegate { Dispatcher.BeginInvoke(new Action(delegate { SetPaused(pause.Checked); })); };
            menu.Items.Add(pause);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Dispatcher.BeginInvoke(new Action(ExitApplication)); });
            if (_tray.ContextMenuStrip != null) _tray.ContextMenuStrip.Dispose();
            _tray.ContextMenuStrip = menu;
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            _allowExit = true;
            Close();
            Application.Current.Shutdown();
        }

        private void AddRule(object sender, RoutedEventArgs e)
        {
            IList<WindowDescriptor> list = _windows.EnumerateWindows();
            WindowPickerDialog picker = new WindowPickerDialog(this, list, "选择要记忆的窗口", "选中一个窗口，然后设置它的识别方式与自动恢复行为。");
            if (picker.ShowDialog() != true) return;
            RuleEditorDialog editor = new RuleEditorDialog(this, null, picker.SelectedWindow, _windows);
            if (editor.ShowDialog() != true) return;
            _state.Rules.Add(editor.Result);
            SaveAndRefresh("已创建窗口规则：“" + editor.Result.Name + "”");
        }

        private void EditRule(object sender, RoutedEventArgs e)
        {
            WindowRule selected = _rulesGrid.SelectedItem as WindowRule;
            if (selected == null) { SetStatus("请先选择一条窗口规则"); return; }
            RuleEditorDialog editor = new RuleEditorDialog(this, selected, null, _windows);
            if (editor.ShowDialog() != true) return;
            editor.Result.Id = selected.Id;
            int index = _state.Rules.IndexOf(selected);
            _state.Rules[index] = editor.Result;
            SaveAndRefresh("已更新窗口规则：“" + editor.Result.Name + "”");
        }

        private void DeleteRule(object sender, RoutedEventArgs e)
        {
            WindowRule selected = _rulesGrid.SelectedItem as WindowRule;
            if (selected == null) { SetStatus("请先选择一条窗口规则"); return; }
            if (MessageBox.Show(this, "删除窗口规则“" + selected.Name + "”？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _state.Rules.Remove(selected);
            SaveAndRefresh("已删除窗口规则");
        }

        private void ApplySelectedRule(object sender, RoutedEventArgs e)
        {
            WindowRule selected = _rulesGrid.SelectedItem as WindowRule;
            if (selected == null) { SetStatus("请先选择一条窗口规则"); return; }
            IList<WindowDescriptor> windows = _windows.EnumerateWindows();
            WindowDescriptor target = _windows.FindBestMatch(selected.Matcher, windows, null);
            if (target == null) { SetStatus("没有找到与“" + selected.Name + "”匹配的窗口"); return; }
            string error;
            if (_windows.ApplyPlacement(target.Handle, selected.Placement, true, out error)) SetStatus("已还原：“" + selected.Name + "”");
            else SetStatus("还原失败：" + error);
        }

        private void CaptureLayout(object sender, RoutedEventArgs e)
        {
            IList<WindowDescriptor> list = _windows.EnumerateWindows();
            string suggested = SuggestLayoutHotkey();
            LayoutCaptureDialog dialog = new LayoutCaptureDialog(this, list, _windows, suggested);
            if (dialog.ShowDialog() != true) return;
            if (!ValidateUniqueHotkey(dialog.Result.Hotkey, null)) return;
            _state.Layouts.Add(dialog.Result);
            SaveAndRefresh("已保存布局：“" + dialog.Result.Name + "”");
        }

        private string SuggestLayoutHotkey()
        {
            HashSet<string> used = new HashSet<string>(_state.Layouts.Select(delegate(LayoutProfile x) { return x.Hotkey; }), StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i <= 9; i++)
            {
                string value = "Ctrl+Alt+" + i;
                if (!used.Contains(value)) return value;
            }
            return string.Empty;
        }

        private void RestoreSelectedLayout(object sender, RoutedEventArgs e)
        {
            LayoutProfile selected = _layoutsGrid.SelectedItem as LayoutProfile;
            if (selected == null) { SetStatus("请先选择一个布局"); return; }
            RestoreLayout(selected);
        }

        private void RestoreLayout(LayoutProfile profile)
        {
            if (Interlocked.Exchange(ref _layoutRestoreBusy, 1) != 0)
            {
                SetStatus("已有布局正在还原，请稍候");
                return;
            }
            SetStatus("正在还原布局：“" + profile.Name + "”…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    RestoreSummary summary = _engine.RestoreLayout(profile);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        SetStatus(profile.Name + " · " + summary.ToDisplayText());
                        if (summary.Missing > 0 || summary.Failed > 0)
                            ShowTrayMessage("布局还原完成", summary.ToDisplayText());
                    }));
                }
                finally { Interlocked.Exchange(ref _layoutRestoreBusy, 0); }
            });
        }

        private void EditLayout(object sender, RoutedEventArgs e)
        {
            LayoutProfile selected = _layoutsGrid.SelectedItem as LayoutProfile;
            if (selected == null) { SetStatus("请先选择一个布局"); return; }
            LayoutPropertiesDialog dialog = new LayoutPropertiesDialog(this, selected);
            if (dialog.ShowDialog() != true) return;
            if (!ValidateUniqueHotkey(dialog.Result.Hotkey, selected.Id)) return;
            int index = _state.Layouts.IndexOf(selected);
            _state.Layouts[index] = dialog.Result;
            SaveAndRefresh("已更新布局：“" + dialog.Result.Name + "”");
        }

        private void DeleteLayout(object sender, RoutedEventArgs e)
        {
            LayoutProfile selected = _layoutsGrid.SelectedItem as LayoutProfile;
            if (selected == null) { SetStatus("请先选择一个布局"); return; }
            if (MessageBox.Show(this, "删除布局“" + selected.Name + "”？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _state.Layouts.Remove(selected);
            SaveAndRefresh("已删除布局");
        }

        private bool ValidateUniqueHotkey(string hotkey, string excludedLayoutId)
        {
            if (string.IsNullOrWhiteSpace(hotkey)) return true;
            if (string.Equals(hotkey, _state.Preferences.CaptureHotkey, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "这个快捷键已经用于快速记忆窗口。", "快捷键重复", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            foreach (LayoutProfile profile in _state.Layouts)
            {
                if (profile.Id == excludedLayoutId) continue;
                if (string.Equals(profile.Hotkey, hotkey, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "这个快捷键已经用于布局“" + profile.Name + "”。", "快捷键重复", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }

        private void CaptureForegroundWindow()
        {
            WindowDescriptor window = _windows.GetForegroundDescriptor();
            if (window == null)
            {
                SetStatus("没有检测到可记忆的活动窗口");
                ShowTrayMessage("无法记忆窗口", "请先激活一个普通程序窗口，再按快捷键。");
                return;
            }

            WindowRule existing = null;
            foreach (WindowRule rule in _state.Rules)
            {
                int score;
                if (_windows.Matches(rule.Matcher, window, out score) && rule.Matcher.TitleMode == TitleMatchMode.Exact)
                {
                    existing = rule;
                    break;
                }
            }

            if (existing == null)
            {
                existing = new WindowRule
                {
                    Name = window.AppLabel,
                    Matcher = _windows.CreateMatcher(window, TitleMatchMode.Exact),
                    Placement = _windows.CreatePlacement(window),
                    Enabled = true
                };
                _state.Rules.Add(existing);
                SaveAndRefresh("已记忆：“" + window.Title + "”");
                ShowTrayMessage("已记忆窗口", window.AppLabel + " · " + window.SizeLabel);
            }
            else
            {
                existing.Placement = _windows.CreatePlacement(window);
                SaveAndRefresh("已更新：“" + window.Title + "”");
                ShowTrayMessage("已更新窗口位置", window.AppLabel + " · " + window.SizeLabel);
            }
        }

        private void EditCaptureHotkey(object sender, RoutedEventArgs e)
        {
            HotkeyDialog dialog = new HotkeyDialog(this, _state.Preferences.CaptureHotkey);
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.GestureText)) return;
            foreach (LayoutProfile layout in _state.Layouts)
            {
                if (string.Equals(layout.Hotkey, dialog.GestureText, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "这个快捷键已经用于布局“" + layout.Name + "”。", "快捷键重复", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            _state.Preferences.CaptureHotkey = dialog.GestureText;
            SaveAndRefresh("快速记忆快捷键已改为 " + dialog.GestureText);
        }

        private void ScanIntervalChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingSettings) return;
            IntervalOption option = _scanInterval.SelectedItem as IntervalOption;
            if (option == null) return;
            _state.Preferences.ScanIntervalMs = option.Value;
            SaveAndRefresh("检测频率已更新");
        }

        private void PauseChanged(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings) return;
            SetPaused(_paused.IsChecked == true);
        }

        private void SetPaused(bool paused)
        {
            _state.Preferences.AutoRestorePaused = paused;
            SaveAndRefresh(paused ? "自动恢复已暂停" : "自动恢复已继续");
        }

        private void MinimizeToTrayChanged(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings) return;
            _state.Preferences.MinimizeToTray = _minimizeToTray.IsChecked == true;
            SaveAndRefresh(_state.Preferences.MinimizeToTray ? "已开启最小化到托盘" : "任务栏图标将保持显示");
        }

        private void AutoStartChanged(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings) return;
            bool enabled = _autoStart.IsChecked == true;
            string error;
            if (!StartupService.SetEnabled(enabled, out error))
            {
                MessageBox.Show(this, "无法修改开机启动：" + error, "设置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshSettings();
                return;
            }
            _state.Preferences.AutoStart = enabled;
            SaveAndRefresh(enabled ? "已开启开机启动" : "已关闭开机启动");
        }

        private void OpenDataDirectory()
        {
            try { Process.Start("explorer.exe", "\"" + _config.DataDirectory + "\""); }
            catch (Exception ex) { SetStatus("无法打开数据目录：" + ex.Message); }
        }

        private void SaveAndRefresh(string message)
        {
            if (!_config.Save(_state)) SetStatus(_config.LastError);
            else SetStatus(message);
            RefreshEverything();
            RegisterHotkeys();
            _engine.Update(_state.Rules, _state.Preferences.ScanIntervalMs, _state.Preferences.AutoRestorePaused);
            RebuildTrayMenu();
        }

        private void RefreshEverything()
        {
            if (_rulesGrid != null)
            {
                _rulesGrid.ItemsSource = null;
                _rulesGrid.ItemsSource = _state.Rules;
            }
            if (_layoutsGrid != null)
            {
                _layoutsGrid.ItemsSource = null;
                _layoutsGrid.ItemsSource = _state.Layouts;
            }
            if (_ruleCount != null) _ruleCount.Text = _state.Rules.Count.ToString();
            if (_layoutCount != null) _layoutCount.Text = _state.Layouts.Count.ToString();
            if (_autoState != null) _autoState.Text = _state.Preferences.AutoRestorePaused ? "已暂停" : "运行中";
            foreach (TextBlock hotkeyText in _captureHotkeyTexts) hotkeyText.Text = _state.Preferences.CaptureHotkey;
            if (_rulesEmpty != null) _rulesEmpty.Visibility = _state.Rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_layoutsEmpty != null) _layoutsEmpty.Visibility = _state.Layouts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            _loadingSettings = true;
            try
            {
                if (_paused != null) _paused.IsChecked = _state.Preferences.AutoRestorePaused;
                if (_minimizeToTray != null) _minimizeToTray.IsChecked = _state.Preferences.MinimizeToTray;
                if (_autoStart != null) _autoStart.IsChecked = StartupService.IsEnabled();
                if (_scanInterval != null)
                {
                    IntervalOption nearest = null;
                    int distance = int.MaxValue;
                    foreach (IntervalOption option in _scanInterval.Items)
                    {
                        int current = Math.Abs(option.Value - _state.Preferences.ScanIntervalMs);
                        if (current < distance) { distance = current; nearest = option; }
                    }
                    _scanInterval.SelectedItem = nearest;
                }
            }
            finally { _loadingSettings = false; }
        }

        private void RegisterHotkeys()
        {
            if (!IsInitialized) return;
            _hotkeys.Clear();
            List<string> errors = new List<string>();
            string error;
            if (!_hotkeys.Register(_state.Preferences.CaptureHotkey,
                delegate { Dispatcher.BeginInvoke(new Action(CaptureForegroundWindow)); }, out error))
                errors.Add(_state.Preferences.CaptureHotkey + "：" + error);

            foreach (LayoutProfile layout in _state.Layouts)
            {
                if (string.IsNullOrWhiteSpace(layout.Hotkey)) continue;
                LayoutProfile captured = layout;
                if (!_hotkeys.Register(layout.Hotkey, delegate { RestoreLayout(captured); }, out error))
                    errors.Add(layout.Hotkey + "：" + error);
            }
            if (errors.Count > 0) SetStatus("部分快捷键未启用：" + string.Join("；", errors.ToArray()));
        }

        private void OnEngineStatus(string message)
        {
            Dispatcher.BeginInvoke(new Action(delegate { SetStatus(message); }));
        }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.Text = message;
        }

        private void ShowTrayMessage(string title, string message)
        {
            if (_tray == null) return;
            _tray.BalloonTipTitle = title;
            _tray.BalloonTipText = message;
            _tray.ShowBalloonTip(2600);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, IntPtr value);

        private sealed class IntervalOption
        {
            public int Value { get; private set; }
            public string Label { get; private set; }
            public IntervalOption(int value, string label) { Value = value; Label = label; }
            public override string ToString() { return Label; }
        }
    }

}
