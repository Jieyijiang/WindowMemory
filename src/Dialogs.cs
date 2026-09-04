using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace WindowMemory
{
    public sealed class HotkeyDialog : Window
    {
        private readonly TextBlock _display;
        public string GestureText { get; private set; }

        public HotkeyDialog(Window owner, string current)
        {
            Owner = owner;
            Title = "设置快捷键";
            Width = 430;
            Height = 260;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            GestureText = current ?? string.Empty;

            StackPanel body = new StackPanel { Margin = new Thickness(28) };
            body.Children.Add(Ui.Heading("按下新的快捷键", 22));
            TextBlock help = Ui.Muted("请同时按下至少一个修饰键和主按键，例如 Ctrl + 1。Esc 取消。", 13);
            help.Margin = new Thickness(0, 8, 0, 20);
            body.Children.Add(help);

            Border box = new Border
            {
                Background = Ui.Brush("SurfaceRaisedBrush"),
                BorderBrush = Ui.Brush("FocusBrush"),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Height = 72
            };
            _display = Ui.Heading(string.IsNullOrWhiteSpace(GestureText) ? "等待输入…" : GestureText, 20);
            _display.HorizontalAlignment = HorizontalAlignment.Center;
            box.Child = _display;
            body.Children.Add(box);

            Button clear = Ui.Button("清除快捷键", delegate { GestureText = string.Empty; DialogResult = true; }, null);
            clear.HorizontalAlignment = HorizontalAlignment.Right;
            clear.Margin = new Thickness(0, 16, 0, 0);
            body.Children.Add(clear);
            Content = body;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { DialogResult = false; return; }
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            string gesture = HotkeyService.HotkeyGesture.FromKeyEvent(key, Keyboard.Modifiers);
            if (string.IsNullOrWhiteSpace(gesture)) return;
            GestureText = gesture;
            _display.Text = gesture;
            e.Handled = true;
            DialogResult = true;
        }
    }

    public sealed class WindowPickerDialog : Window
    {
        private readonly DataGrid _grid;
        public WindowDescriptor SelectedWindow { get; private set; }

        public WindowPickerDialog(Window owner, IList<WindowDescriptor> windows, string title, string description)
        {
            Owner = owner;
            Title = title;
            Width = 860;
            Height = 610;
            MinWidth = 720;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            Grid root = new Grid { Margin = new Thickness(28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(Ui.Heading(title, 24));
            TextBlock help = Ui.Muted(description, 13);
            help.Margin = new Thickness(0, 7, 0, 18);
            Ui.SetRow(help, 1);
            root.Children.Add(help);

            Border card = Ui.Card(null, new Thickness(0));
            _grid = new DataGrid { ItemsSource = windows, IsReadOnly = true };
            _grid.Columns.Add(new DataGridTextColumn { Header = "程序", Binding = new Binding("AppLabel"), Width = new DataGridLength(150) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "窗口标题", Binding = new Binding("Title"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "尺寸", Binding = new Binding("SizeLabel"), Width = new DataGridLength(110) });
            card.Padding = new Thickness(0);
            card.Child = _grid;
            Ui.SetRow(card, 2);
            root.Children.Add(card);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            Button cancel = Ui.Button("取消", delegate { DialogResult = false; }, null);
            Button choose = Ui.Button("选择窗口", Choose, "PrimaryButton");
            choose.Margin = new Thickness(10, 0, 0, 0);
            actions.Children.Add(cancel);
            actions.Children.Add(choose);
            Ui.SetRow(actions, 3);
            root.Children.Add(actions);
            Content = root;

            _grid.MouseDoubleClick += delegate { if (_grid.SelectedItem != null) Choose(null, null); };
        }

        private void Choose(object sender, RoutedEventArgs e)
        {
            SelectedWindow = _grid.SelectedItem as WindowDescriptor;
            if (SelectedWindow == null)
            {
                MessageBox.Show(this, "请先选择一个窗口。", "尚未选择", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        }
    }

    public sealed class RuleEditorDialog : Window
    {
        private readonly TextBox _name;
        private readonly TextBox _title;
        private readonly TextBox _process;
        private readonly TextBox _className;
        private readonly ComboBox _mode;
        private readonly CheckBox _enabled;
        private readonly CheckBox _keep;
        private readonly CheckBox _scale;
        private readonly PlacementPreview _preview;
        private SavedPlacement _placement;
        private string _processName;
        public WindowRule Result { get; private set; }

        public RuleEditorDialog(Window owner, WindowRule existing, WindowDescriptor captured, WindowService service)
        {
            Owner = owner;
            Title = existing == null ? "新建窗口规则" : "编辑窗口规则";
            Width = 720;
            Height = 720;
            MinHeight = 640;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            WindowRule rule = existing == null ? new WindowRule() : existing.Clone();
            if (captured != null)
            {
                rule.Name = captured.AppLabel;
                rule.Matcher = service.CreateMatcher(captured, TitleMatchMode.Exact);
                rule.Placement = service.CreatePlacement(captured);
            }
            _placement = rule.Placement;
            _processName = rule.Matcher.ProcessName;

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel root = new StackPanel { Margin = new Thickness(28) };
            scroll.Content = root;
            root.Children.Add(Ui.Heading(Title, 24));
            TextBlock help = Ui.Muted("匹配到这个窗口后，程序会自动恢复下方记录的位置和尺寸。", 13);
            help.Margin = new Thickness(0, 7, 0, 22);
            root.Children.Add(help);

            root.Children.Add(Ui.Label("规则名称"));
            _name = new TextBox { Text = rule.Name };
            root.Children.Add(_name);

            Grid matchGrid = Ui.TwoColumn(170, 14);
            matchGrid.Margin = new Thickness(0, 18, 0, 0);
            StackPanel modeStack = new StackPanel();
            modeStack.Children.Add(Ui.Label("标题匹配方式"));
            _mode = new ComboBox();
            _mode.Items.Add(new ModeOption(TitleMatchMode.Exact, "标题完全一致"));
            _mode.Items.Add(new ModeOption(TitleMatchMode.Contains, "标题包含文字"));
            _mode.Items.Add(new ModeOption(TitleMatchMode.StartsWith, "标题以文字开头"));
            _mode.Items.Add(new ModeOption(TitleMatchMode.Regex, "正则表达式"));
            _mode.Items.Add(new ModeOption(TitleMatchMode.Ignore, "忽略标题"));
            foreach (ModeOption option in _mode.Items) if (option.Value == rule.Matcher.TitleMode) _mode.SelectedItem = option;
            modeStack.Children.Add(_mode);
            matchGrid.Children.Add(modeStack);

            StackPanel titleStack = new StackPanel();
            Ui.SetColumn(titleStack, 2);
            titleStack.Children.Add(Ui.Label("标题文字"));
            _title = new TextBox { Text = rule.Matcher.TitleText };
            titleStack.Children.Add(_title);
            matchGrid.Children.Add(titleStack);
            root.Children.Add(matchGrid);

            root.Children.Add(FieldLabel("程序路径", 18));
            _process = new TextBox { Text = rule.Matcher.ProcessPath };
            root.Children.Add(_process);
            root.Children.Add(FieldLabel("窗口类（高级匹配条件）", 14));
            _className = new TextBox { Text = rule.Matcher.ClassName };
            root.Children.Add(_className);

            TextBlock placementTitle = Ui.Heading("目标位置", 16);
            placementTitle.Margin = new Thickness(0, 22, 0, 10);
            root.Children.Add(placementTitle);
            _preview = new PlacementPreview { Placement = _placement };
            root.Children.Add(_preview);
            TextBlock placementText = Ui.Muted(_placement == null ? "未记录" : _placement.Summary, 13);
            placementText.HorizontalAlignment = HorizontalAlignment.Center;
            placementText.Margin = new Thickness(0, 8, 0, 0);
            root.Children.Add(placementText);

            _enabled = new CheckBox { Content = "启用自动恢复", IsChecked = rule.Enabled, Margin = new Thickness(0, 16, 0, 0) };
            _keep = new CheckBox { Content = "持续保持位置（手动移动后仍会拉回）", IsChecked = rule.KeepPosition };
            _scale = new CheckBox { Content = "显示器变化时按比例适配", IsChecked = _placement == null || _placement.ScaleWithMonitor };
            root.Children.Add(_enabled);
            root.Children.Add(_keep);
            root.Children.Add(_scale);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
            actions.Children.Add(Ui.Button("取消", delegate { DialogResult = false; }, null));
            Button save = Ui.Button("保存规则", Save, "PrimaryButton");
            save.Margin = new Thickness(10, 0, 0, 0);
            actions.Children.Add(save);
            root.Children.Add(actions);
            Content = scroll;
        }

        private static TextBlock FieldLabel(string text, double top)
        {
            TextBlock label = Ui.Label(text);
            label.Margin = new Thickness(0, top, 0, 7);
            return label;
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                MessageBox.Show(this, "请输入规则名称。", "信息不完整", MessageBoxButton.OK, MessageBoxImage.Information);
                _name.Focus();
                return;
            }
            ModeOption option = _mode.SelectedItem as ModeOption;
            if (option == null) return;
            if (option.Value != TitleMatchMode.Ignore && string.IsNullOrWhiteSpace(_title.Text))
            {
                MessageBox.Show(this, "当前匹配方式需要填写标题文字。", "信息不完整", MessageBoxButton.OK, MessageBoxImage.Information);
                _title.Focus();
                return;
            }
            if (_placement == null)
            {
                MessageBox.Show(this, "没有可保存的窗口位置。", "缺少位置", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _placement.ScaleWithMonitor = _scale.IsChecked == true;
            Result = new WindowRule
            {
                Name = _name.Text.Trim(),
                Enabled = _enabled.IsChecked == true,
                KeepPosition = _keep.IsChecked == true,
                Matcher = new WindowMatcher
                {
                    ProcessPath = _process.Text.Trim(),
                    ProcessName = _processName,
                    ClassName = _className.Text.Trim(),
                    TitleText = option.Value == TitleMatchMode.Ignore ? string.Empty : _title.Text.Trim(),
                    TitleMode = option.Value
                },
                Placement = _placement.Clone()
            };
            DialogResult = true;
        }
    }

    public sealed class LayoutCaptureDialog : Window
    {
        private readonly TextBox _name;
        private readonly TextBlock _hotkeyLabel;
        private readonly IList<WindowDescriptor> _windows;
        private string _hotkey;
        private readonly WindowService _service;
        public LayoutProfile Result { get; private set; }

        public LayoutCaptureDialog(Window owner, IList<WindowDescriptor> windows, WindowService service, string suggestedHotkey)
        {
            Owner = owner;
            Title = "保存当前布局";
            Width = 900;
            Height = 680;
            MinWidth = 760;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            _windows = windows;
            _service = service;
            _hotkey = suggestedHotkey;

            Grid root = new Grid { Margin = new Thickness(28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(Ui.Heading("保存当前布局", 24));
            TextBlock help = Ui.Muted("勾选要一起还原的窗口。保存时会记录每个窗口所在显示器、位置和大小。", 13);
            help.Margin = new Thickness(0, 7, 0, 18);
            Ui.SetRow(help, 1);
            root.Children.Add(help);

            Grid fields = Ui.TwoColumn(1, 16);
            fields.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            fields.ColumnDefinitions[2].Width = new GridLength(230);
            StackPanel nameStack = new StackPanel();
            nameStack.Children.Add(Ui.Label("布局名称"));
            _name = new TextBox { Text = "布局 " + DateTime.Now.ToString("HHmm") };
            nameStack.Children.Add(_name);
            fields.Children.Add(nameStack);
            StackPanel hotkeyStack = new StackPanel();
            Ui.SetColumn(hotkeyStack, 2);
            hotkeyStack.Children.Add(Ui.Label("还原快捷键"));
            Button hotkeyButton = Ui.Button("", EditHotkey, null);
            _hotkeyLabel = Ui.Heading(string.IsNullOrWhiteSpace(_hotkey) ? "点击设置" : _hotkey, 14);
            hotkeyButton.Content = _hotkeyLabel;
            hotkeyStack.Children.Add(hotkeyButton);
            fields.Children.Add(hotkeyStack);
            Ui.SetRow(fields, 2);
            root.Children.Add(fields);

            DataGrid grid = new DataGrid { ItemsSource = windows, Margin = new Thickness(0, 18, 0, 0) };
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "选择", Binding = new Binding("IsSelected") { Mode = BindingMode.TwoWay }, Width = new DataGridLength(64) });
            grid.Columns.Add(new DataGridTextColumn { Header = "程序", Binding = new Binding("AppLabel"), IsReadOnly = true, Width = new DataGridLength(145) });
            grid.Columns.Add(new DataGridTextColumn { Header = "窗口标题", Binding = new Binding("Title"), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "尺寸", Binding = new Binding("SizeLabel"), IsReadOnly = true, Width = new DataGridLength(105) });
            Border card = Ui.Card(grid, new Thickness(0));
            card.Padding = new Thickness(0);
            Ui.SetRow(card, 3);
            root.Children.Add(card);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            actions.Children.Add(Ui.Button("取消", delegate { DialogResult = false; }, null));
            Button save = Ui.Button("保存布局", Save, "PrimaryButton");
            save.Margin = new Thickness(10, 0, 0, 0);
            actions.Children.Add(save);
            Ui.SetRow(actions, 4);
            root.Children.Add(actions);
            Content = root;
        }

        private void EditHotkey(object sender, RoutedEventArgs e)
        {
            HotkeyDialog dialog = new HotkeyDialog(this, _hotkey);
            if (dialog.ShowDialog() == true)
            {
                _hotkey = dialog.GestureText;
                _hotkeyLabel.Text = string.IsNullOrWhiteSpace(_hotkey) ? "点击设置" : _hotkey;
            }
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            List<WindowDescriptor> selected = _windows.Where(delegate(WindowDescriptor w) { return w.IsSelected; }).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "请至少选择一个窗口。", "尚未选择", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                MessageBox.Show(this, "请输入布局名称。", "信息不完整", MessageBoxButton.OK, MessageBoxImage.Information);
                _name.Focus();
                return;
            }

            Result = new LayoutProfile { Name = _name.Text.Trim(), Hotkey = _hotkey };
            Dictionary<string, int> appWindowCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (WindowDescriptor window in selected)
            {
                string key = (window.ProcessPath ?? window.ProcessName ?? string.Empty) + "\u001f" + (window.ClassName ?? string.Empty);
                int count;
                appWindowCounts.TryGetValue(key, out count);
                appWindowCounts[key] = count + 1;
            }
            foreach (WindowDescriptor window in selected)
            {
                string key = (window.ProcessPath ?? window.ProcessName ?? string.Empty) + "\u001f" + (window.ClassName ?? string.Empty);
                TitleMatchMode titleMode = appWindowCounts[key] == 1 ? TitleMatchMode.Ignore : TitleMatchMode.Exact;
                Result.Windows.Add(new LayoutWindowEntry
                {
                    Name = window.AppLabel + " · " + window.Title,
                    Matcher = _service.CreateMatcher(window, titleMode),
                    Placement = _service.CreatePlacement(window)
                });
            }
            DialogResult = true;
        }
    }

    public sealed class LayoutPropertiesDialog : Window
    {
        private readonly TextBox _name;
        private readonly TextBlock _hotkeyLabel;
        private string _hotkey;
        private readonly LayoutProfile _original;
        public LayoutProfile Result { get; private set; }

        public LayoutPropertiesDialog(Window owner, LayoutProfile profile)
        {
            Owner = owner;
            Title = "编辑布局";
            Width = 520;
            Height = 330;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            _original = profile;
            _hotkey = profile.Hotkey;

            StackPanel root = new StackPanel { Margin = new Thickness(28) };
            root.Children.Add(Ui.Heading("编辑布局", 24));
            TextBlock help = Ui.Muted(profile.WindowCountLabel + "；这里只修改名称和快捷键，窗口位置保持不变。", 13);
            help.Margin = new Thickness(0, 7, 0, 20);
            root.Children.Add(help);
            root.Children.Add(Ui.Label("布局名称"));
            _name = new TextBox { Text = profile.Name };
            root.Children.Add(_name);
            TextBlock keyLabel = Ui.Label("还原快捷键");
            keyLabel.Margin = new Thickness(0, 16, 0, 7);
            root.Children.Add(keyLabel);
            Button hotkey = Ui.Button("", EditHotkey, null);
            _hotkeyLabel = Ui.Heading(string.IsNullOrWhiteSpace(_hotkey) ? "点击设置" : _hotkey, 14);
            hotkey.Content = _hotkeyLabel;
            root.Children.Add(hotkey);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
            actions.Children.Add(Ui.Button("取消", delegate { DialogResult = false; }, null));
            Button save = Ui.Button("保存", Save, "PrimaryButton");
            save.Margin = new Thickness(10, 0, 0, 0);
            actions.Children.Add(save);
            root.Children.Add(actions);
            Content = root;
        }

        private void EditHotkey(object sender, RoutedEventArgs e)
        {
            HotkeyDialog dialog = new HotkeyDialog(this, _hotkey);
            if (dialog.ShowDialog() == true)
            {
                _hotkey = dialog.GestureText;
                _hotkeyLabel.Text = string.IsNullOrWhiteSpace(_hotkey) ? "点击设置" : _hotkey;
            }
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) return;
            Result = _original.Clone();
            Result.Name = _name.Text.Trim();
            Result.Hotkey = _hotkey;
            DialogResult = true;
        }
    }
}
