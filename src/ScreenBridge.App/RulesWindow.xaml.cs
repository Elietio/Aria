using System.Windows;
using System.Windows.Controls;
using ScreenBridge.Core;
using Wpf.Ui.Controls;

namespace ScreenBridge.App;

public partial class RulesWindow : FluentWindow
{
    private readonly AppConfig _config;
    private readonly MonitorService _monitorService;
    private readonly AudioService _audioService;

    // 已知输入源代码列表
    private readonly List<(int Code, string Name)> _knownInputs = new()
    {
        (15, "DisplayPort 1 (0x0F)"),
        (16, "DisplayPort 2 (0x10)"),
        (17, "HDMI 1 (0x11)"),
        (18, "HDMI 2 (0x12)"),
        (27, "USB-C (0x1B)"),
    };

    private readonly DDCService _ddcService;

    public RulesWindow(AppConfig config, MonitorService monitorService, AudioService audioService)
    {
        InitializeComponent();
        _config = config;
        _monitorService = monitorService;
        _audioService = audioService;
        _ddcService = new DDCService();
        
        Loaded += RulesWindow_Loaded;
        DDCMonitorCombo.SelectionChanged += (s, e) => RefreshDDCReadout();
    }

    private async void RulesWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. 加载显示器列表 (DDC)
        var monitors = _monitorService.GetAllMonitors();
        DDCMonitorCombo.ItemsSource = monitors;
        
        // 选中当前 DDC Monitor
        var currentDdcMonitor = monitors.FirstOrDefault(m => 
            m.DeviceName == _config.DDCMonitorId || 
            m.FriendlyName == _config.DDCMonitorId);
        
        if (currentDdcMonitor != null)
        {
            DDCMonitorCombo.SelectedItem = currentDdcMonitor;
        }
        else
        {
            DDCMonitorCombo.SelectedIndex = 0; // 默认第一个
        }

        // 2. 加载设置逻辑
        SwitchOnLossToggle.IsChecked = _config.SwitchToPS5OnDDCLoss;

        // 3. 加载音频设备
        var audioDevices = await _audioService.GetPlaybackDevicesAsync();
        var audioList = audioDevices.ToList();
        ModeAAudioCombo.ItemsSource = audioList;
        ModeBAudioCombo.ItemsSource = audioList;

        // 3. 填充 Mode A 数据
        ModeAName.Text = _config.ModeA.Name;
        // 选中音频
        var modeAAudio = audioList.FirstOrDefault(a => a.Name == _config.ModeA.TargetAudioDeviceName);
        if (modeAAudio != null) ModeAAudioCombo.SelectedItem = modeAAudio;
        // 窗口目标
        SelectComboBoxByTag(ModeAWindowCombo, _config.ModeA.TargetWindowMonitor);
        // 触发条件
        CreateTriggerCheckboxes(ModeATriggersPanel, _config.ModeA.TriggerInputs);

        // 4. 填充 Mode B 数据
        ModeBName.Text = _config.ModeB.Name;
        // 选中音频
        var modeBAudio = audioList.FirstOrDefault(a => a.Name == _config.ModeB.TargetAudioDeviceName);
        if (modeBAudio != null) ModeBAudioCombo.SelectedItem = modeBAudio;
        // 窗口目标
        SelectComboBoxByTag(ModeBWindowCombo, _config.ModeB.TargetWindowMonitor);
        // 触发条件
        CreateTriggerCheckboxes(ModeBTriggersPanel, _config.ModeB.TriggerInputs);

        // 5. 加载热键和通知设置
        LoadHotkeySettings();
    }

    private void CreateTriggerCheckboxes(StackPanel panel, List<int> selectedCodes)
    {
        panel.Children.Clear();
        foreach (var input in _knownInputs)
        {
            var checkBox = new System.Windows.Controls.CheckBox
            {
                Content = input.Name,
                Tag = input.Code,
                IsChecked = selectedCodes.Contains(input.Code),
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(checkBox);
        }
    }

    private void SelectComboBoxByTag(System.Windows.Controls.ComboBox comboBox, string tagValue)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (item.Tag?.ToString() == tagValue)
            {
                comboBox.SelectedItem = item;
                break;
            }
        }
    }

    private List<int> GetSelectedTriggers(StackPanel panel)
    {
        var list = new List<int>();
        foreach (var child in panel.Children)
        {
            if (child is System.Windows.Controls.CheckBox cb && cb.IsChecked == true && cb.Tag is int code)
            {
                list.Add(code);
            }
        }
        return list;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // 1. DDC Monitor
        if (DDCMonitorCombo.SelectedItem is MonitorInfo monitor)
        {
            // 对于 Generic PnP Monitor，使用 DeviceName 更稳妥？但 DeviceName 比如 \\.\DISPLAY1 会变。
            // FriendlyName "LG UltraFine" 比较稳定。
            // 使用 FriendlyName 如果可用，否则 DeviceName.
            _config.DDCMonitorId = !string.IsNullOrEmpty(monitor.FriendlyName) && monitor.FriendlyName != "Generic PnP Monitor"
                ? monitor.FriendlyName 
                : monitor.DeviceName;
        }

        _config.SwitchToPS5OnDDCLoss = SwitchOnLossToggle.IsChecked ?? false;

        // 2. Mode A
        _config.ModeA.Name = ModeAName.Text;
        if (ModeAAudioCombo.SelectedItem is AudioDeviceInfo audioA)
        {
            _config.ModeA.TargetAudioDeviceName = audioA.Name;
        }
        if (ModeAWindowCombo.SelectedItem is ComboBoxItem winA && winA.Tag != null)
        {
            _config.ModeA.TargetWindowMonitor = winA.Tag.ToString() ?? "None";
        }
        _config.ModeA.TriggerInputs = GetSelectedTriggers(ModeATriggersPanel);

        // 3. Mode B
        _config.ModeB.Name = ModeBName.Text;
        if (ModeBAudioCombo.SelectedItem is AudioDeviceInfo audioB)
        {
            _config.ModeB.TargetAudioDeviceName = audioB.Name;
        }
        if (ModeBWindowCombo.SelectedItem is ComboBoxItem winB && winB.Tag != null)
        {
            _config.ModeB.TargetWindowMonitor = winB.Tag.ToString() ?? "None";
        }
        _config.ModeB.TriggerInputs = GetSelectedTriggers(ModeBTriggersPanel);

        // 4. 保存热键和通知设置
        SaveHotkeySettings();

        // Save
        _config.Save();

        DialogResult = true;
        Close();
    }

    private void RefreshDDCReadout_Click(object sender, RoutedEventArgs e)
    {
        RefreshDDCReadout();
    }

    private void RefreshDDCReadout()
    {
        if (DDCMonitorCombo.SelectedItem is MonitorInfo monitor)
        {
            try
            {
                var result = _ddcService.GetCurrentInputSource(monitor.Handle);
                if (result.HasValue)
                {
                    DDCReadoutText.Text = $"0x{result.Value.RawValue:X2} ({result.Value.Source})";
                    DDCReadoutText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);
                }
                else
                {
                    DDCReadoutText.Text = "无法读取";
                    DDCReadoutText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                }
            }
            catch
            {
                DDCReadoutText.Text = "读取错误";
                DDCReadoutText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        else
        {
            DDCReadoutText.Text = "未选择显示器";
            DDCReadoutText.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #region Hotkey Settings

    // 临时存储正在编辑的热键
    private uint _tempSwitchModeModifiers;
    private uint _tempSwitchModeKey;
    private uint _tempWindowOverviewModifiers;
    private uint _tempWindowOverviewKey;
    private bool _isRecordingHotkey = false;

    private void LoadHotkeySettings()
    {
        _tempSwitchModeModifiers = _config.SwitchModeModifiers;
        _tempSwitchModeKey = _config.SwitchModeKey;
        _tempWindowOverviewModifiers = _config.WindowOverviewModifiers;
        _tempWindowOverviewKey = _config.WindowOverviewKey;

        SwitchModeHotkeyBox.Text = FormatHotkey(_tempSwitchModeModifiers, _tempSwitchModeKey);
        WindowOverviewHotkeyBox.Text = FormatHotkey(_tempWindowOverviewModifiers, _tempWindowOverviewKey);
        
        // 加载通知设置
        EnableToastToggle.IsChecked = _config.EnableToastNotifications;
    }

    private void SaveHotkeySettings()
    {
        _config.SwitchModeModifiers = _tempSwitchModeModifiers;
        _config.SwitchModeKey = _tempSwitchModeKey;
        _config.WindowOverviewModifiers = _tempWindowOverviewModifiers;
        _config.WindowOverviewKey = _tempWindowOverviewKey;
        _config.EnableToastNotifications = EnableToastToggle.IsChecked ?? true;
    }

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.Text = "按下新的快捷键...";
            textBox.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(60, 100, 149, 237));
            _isRecordingHotkey = true;
        }
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.Background = System.Windows.Media.Brushes.Transparent;
            _isRecordingHotkey = false;
            
            // 恢复显示当前热键
            string tag = textBox.Tag?.ToString() ?? "";
            if (tag == "SwitchMode")
            {
                textBox.Text = FormatHotkey(_tempSwitchModeModifiers, _tempSwitchModeKey);
            }
            else if (tag == "WindowOverview")
            {
                textBox.Text = FormatHotkey(_tempWindowOverviewModifiers, _tempWindowOverviewKey);
            }
        }
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isRecordingHotkey) return;

        e.Handled = true;

        // 忽略单独的修饰键
        if (e.Key == System.Windows.Input.Key.LeftCtrl || e.Key == System.Windows.Input.Key.RightCtrl ||
            e.Key == System.Windows.Input.Key.LeftAlt || e.Key == System.Windows.Input.Key.RightAlt ||
            e.Key == System.Windows.Input.Key.LeftShift || e.Key == System.Windows.Input.Key.RightShift ||
            e.Key == System.Windows.Input.Key.LWin || e.Key == System.Windows.Input.Key.RWin ||
            e.Key == System.Windows.Input.Key.System)
        {
            return;
        }

        // 获取修饰键状态
        uint modifiers = 0;
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
            System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl))
        {
            modifiers |= (uint)HotkeyService.ModifierKeys.Ctrl;
        }
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftAlt) ||
            System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt))
        {
            modifiers |= (uint)HotkeyService.ModifierKeys.Alt;
        }
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
            System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
        {
            modifiers |= (uint)HotkeyService.ModifierKeys.Shift;
        }

        // 要求至少有一个修饰键
        if (modifiers == 0)
        {
            HotkeyConflictWarning.Text = "⚠️ 快捷键需要包含 Ctrl、Alt 或 Shift 修饰键";
            HotkeyConflictWarning.Visibility = Visibility.Visible;
            return;
        }

        // 转换 WPF Key 到 Virtual Key Code
        uint vkCode = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key);

        if (sender is System.Windows.Controls.TextBox textBox)
        {
            string tag = textBox.Tag?.ToString() ?? "";
            
            // 检查冲突
            if (CheckHotkeyConflict(modifiers, vkCode, tag))
            {
                HotkeyConflictWarning.Text = "⚠️ 此快捷键已被使用，请选择其他组合";
                HotkeyConflictWarning.Visibility = Visibility.Visible;
                return;
            }

            HotkeyConflictWarning.Visibility = Visibility.Collapsed;

            if (tag == "SwitchMode")
            {
                _tempSwitchModeModifiers = modifiers;
                _tempSwitchModeKey = vkCode;
            }
            else if (tag == "WindowOverview")
            {
                _tempWindowOverviewModifiers = modifiers;
                _tempWindowOverviewKey = vkCode;
            }

            textBox.Text = FormatHotkey(modifiers, vkCode);
            textBox.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(60, 50, 205, 50));
            
            // 自动取消焦点
            System.Windows.Input.Keyboard.ClearFocus();
        }
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button button)
        {
            string tag = button.Tag?.ToString() ?? "";
            
            if (tag == "SwitchMode")
            {
                _tempSwitchModeModifiers = (uint)(HotkeyService.ModifierKeys.Ctrl | HotkeyService.ModifierKeys.Alt);
                _tempSwitchModeKey = HotkeyService.VirtualKeys.VK_S;
                SwitchModeHotkeyBox.Text = FormatHotkey(_tempSwitchModeModifiers, _tempSwitchModeKey);
            }
            else if (tag == "WindowOverview")
            {
                _tempWindowOverviewModifiers = (uint)(HotkeyService.ModifierKeys.Ctrl | HotkeyService.ModifierKeys.Alt);
                _tempWindowOverviewKey = HotkeyService.VirtualKeys.VK_W;
                WindowOverviewHotkeyBox.Text = FormatHotkey(_tempWindowOverviewModifiers, _tempWindowOverviewKey);
            }

            HotkeyConflictWarning.Visibility = Visibility.Collapsed;
        }
    }

    private bool CheckHotkeyConflict(uint modifiers, uint key, string excludeTag)
    {
        if (excludeTag != "SwitchMode" && 
            _tempSwitchModeModifiers == modifiers && _tempSwitchModeKey == key)
        {
            return true;
        }
        if (excludeTag != "WindowOverview" && 
            _tempWindowOverviewModifiers == modifiers && _tempWindowOverviewKey == key)
        {
            return true;
        }
        return false;
    }

    private string FormatHotkey(uint modifiers, uint key)
    {
        var parts = new List<string>();
        
        if ((modifiers & (uint)HotkeyService.ModifierKeys.Ctrl) != 0)
            parts.Add("Ctrl");
        if ((modifiers & (uint)HotkeyService.ModifierKeys.Alt) != 0)
            parts.Add("Alt");
        if ((modifiers & (uint)HotkeyService.ModifierKeys.Shift) != 0)
            parts.Add("Shift");
        
        // 转换 Virtual Key 到可读名称
        string keyName = GetKeyName(key);
        parts.Add(keyName);
        
        return string.Join(" + ", parts);
    }

    private string GetKeyName(uint vkCode)
    {
        // 常用键映射
        return vkCode switch
        {
            >= 0x30 and <= 0x39 => ((char)vkCode).ToString(), // 0-9
            >= 0x41 and <= 0x5A => ((char)vkCode).ToString(), // A-Z
            >= 0x70 and <= 0x7B => $"F{vkCode - 0x6F}",       // F1-F12
            0x20 => "Space",
            0x0D => "Enter",
            0x1B => "Esc",
            0x09 => "Tab",
            0xBD => "-",
            0xBB => "=",
            0xDC => "\\",
            0xC0 => "`",
            0xDB => "[",
            0xDD => "]",
            0xBA => ";",
            0xDE => "'",
            0xBC => ",",
            0xBE => ".",
            0xBF => "/",
            _ => $"Key(0x{vkCode:X2})"
        };
    }

    #endregion
}
