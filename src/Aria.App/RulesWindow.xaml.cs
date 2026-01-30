using System.Windows;
using System.Windows.Controls;
using Aria.Core;
using Wpf.Ui.Controls;

namespace Aria.App;

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

    // 临时的透明度变量
    private double _tempGlassOpacity;
    private double _tempCleanOpacity;
    // 临时的背景材质变量
    private AppConfig.BackdropStyle _tempGlassBackdrop;
    private AppConfig.BackdropStyle _tempCleanBackdrop;
    // 上一次选中的 Style，用于保存 slider 值
    private AppConfig.UIStyle _lastSelectedStyle;
    // 防止加载时触发事件导致数据覆盖
    private bool _isLoaded = false;

    private async void RulesWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;

        // 0. 应用当前UI风格
        ApplyUIStyle();

        // 1. 加载显示器列表 (DDC)
        var monitors = _monitorService.GetAllMonitors();
        DDCMonitorCombo.ItemsSource = monitors;
         // ... (Selection logic)
        var currentDdcMonitor = monitors.FirstOrDefault(m => 
            m.DeviceName == _config.DDCMonitorId || 
            m.FriendlyName == _config.DDCMonitorId);
        
        if (currentDdcMonitor != null) DDCMonitorCombo.SelectedItem = currentDdcMonitor;
        else DDCMonitorCombo.SelectedIndex = 0;

        // 2. 加载设置逻辑
        SelectComboBoxByTag(DdcLossActionCombo, _config.DdcLossAction.ToString());
        
        // 初始化临时变量
        _tempGlassOpacity = _config.GlassOpacity;
        _tempCleanOpacity = _config.CleanOpacity;
        _tempGlassBackdrop = _config.GlassBackdrop;
        _tempCleanBackdrop = _config.CleanBackdrop;
        _lastSelectedStyle = _config.Theme;

        // 绑定事件 (确保在设置 ItemSource 之前或之后适当时机)
        ThemeCombo.SelectionChanged += ThemeCombo_SelectionChanged;
        
        // 触发一次加载 current opacity
        SelectComboBoxByTag(ThemeCombo, _config.Theme.ToString());
        
        // 手动初始化 Slider，不触发事件逻辑
        UpdateSliderForStyle(_config.Theme);
        
        // 初始化背景材质选择
        UpdateBackdropComboForStyle(_config.Theme);
        
        // 初始化 Mascot Toggle
        MascotToggle.IsChecked = _config.EnableMoeMascot;
        VoiceToggle.IsChecked = _config.EnableMoeVoice;
        MascotOpacitySlider.Value = _config.MascotOpacity;
        UpdateMascotOpacityText();
        UpdateMascotOpacityPanelVisibility();

        // 初始化可见性 (Classic 隐藏透明度和背景材质控件)
        bool showControls = _config.Theme != AppConfig.UIStyle.Classic;
        UpdateMoeControlsVisibility(showControls);

        // 3. 加载音频设备
        var audioDevices = await _audioService.GetPlaybackDevicesAsync();
        // ... (rest as before)
        var audioList = audioDevices.ToList();
        ModeAAudioCombo.ItemsSource = audioList;
        ModeBAudioCombo.ItemsSource = audioList;

        // ... (rest of loading)
        // 3. 填充 Mode A 数据
        ModeAName.Text = _config.ModeA.Name;
        var modeAAudio = audioList.FirstOrDefault(a => a.Name == _config.ModeA.TargetAudioDeviceName);
        if (modeAAudio != null) ModeAAudioCombo.SelectedItem = modeAAudio;
        PopulateAppWindowCombo(ModeAWindowCombo, monitors, _config.ModeA.TargetWindowMonitor);
        PopulateAppWindowCombo(ModeAAppWindowCombo, monitors, _config.ModeA.AppWindowTargetMonitor);
        CreateTriggerCheckboxes(ModeATriggersPanel, _config.ModeA.TriggerInputs);

        // 4. Mode B
        ModeBName.Text = _config.ModeB.Name;
        var modeBAudio = audioList.FirstOrDefault(a => a.Name == _config.ModeB.TargetAudioDeviceName);
        if (modeBAudio != null) ModeBAudioCombo.SelectedItem = modeBAudio;
        PopulateAppWindowCombo(ModeBWindowCombo, monitors, _config.ModeB.TargetWindowMonitor);
        PopulateAppWindowCombo(ModeBAppWindowCombo, monitors, _config.ModeB.AppWindowTargetMonitor);
        CreateTriggerCheckboxes(ModeBTriggersPanel, _config.ModeB.TriggerInputs);

        // 5. 加载热键和通知设置
        LoadHotkeySettings();

        _isLoaded = true;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;

        if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            if (Enum.TryParse<AppConfig.UIStyle>(item.Tag.ToString(), out var newStyle))
            {
                // 1. 保存旧值 (从 Slider 和 BackdropCombo 当前值)
                SaveOpacityToTemp(_lastSelectedStyle, OpacitySlider.Value);
                SaveBackdropToTemp(_lastSelectedStyle, GetCurrentBackdropFromCombo());
                
                // 2. 更新指针
                _lastSelectedStyle = newStyle;
                
                // 3. 更新 Slider 和 BackdropCombo 到新值
                UpdateSliderForStyle(newStyle);
                UpdateBackdropComboForStyle(newStyle);

                // 4. 更新控件状态 (Classic 隐藏 Slider 和 Backdrop)
                bool showControls = newStyle != AppConfig.UIStyle.Classic;
                UpdateMoeControlsVisibility(showControls);
            }
        }
    }

    private void UpdateMoeControlsVisibility(bool visible)
    {
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (OpacityControlPanel != null) OpacityControlPanel.Visibility = visibility;
        if (BackdropCombo != null) BackdropCombo.Visibility = visibility;
        if (MascotToggle != null) MascotToggle.Visibility = visibility;
        if (VoiceToggle != null) VoiceToggle.Visibility = visibility;
        if (MascotOpacityPanel != null) 
        {
            // Only show slider if Theme is NOT Classic AND Toggle is Checked
            bool showSlider = visible && (MascotToggle.IsChecked == true);
            MascotOpacityPanel.Visibility = showSlider ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SaveOpacityToTemp(AppConfig.UIStyle style, double value)
    {
        if (style == AppConfig.UIStyle.MoeGlass) _tempGlassOpacity = value;
        else if (style == AppConfig.UIStyle.MoeClean) _tempCleanOpacity = value;
    }

    /// <summary>
    /// 应用当前UI风格的背景材质
    /// </summary>
    private void ApplyUIStyle()
    {
        try
        {
            bool isMoe = _config.Theme == AppConfig.UIStyle.MoeGlass || _config.Theme == AppConfig.UIStyle.MoeClean;
            
            if (isMoe)
            {
                // 根据当前Theme选择对应的背景材质
                var backdrop = (_config.Theme == AppConfig.UIStyle.MoeGlass) 
                    ? _config.GlassBackdrop 
                    : _config.CleanBackdrop;
                
                this.WindowBackdropType = backdrop == AppConfig.BackdropStyle.Acrylic 
                    ? Wpf.Ui.Controls.WindowBackdropType.Acrylic 
                    : Wpf.Ui.Controls.WindowBackdropType.Mica;
                this.Background = null;
            }
            else
            {
                this.WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.None;
                this.ClearValue(Window.BackgroundProperty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyUIStyle failed: {ex.Message}");
        }
    }

    private void UpdateSliderForStyle(AppConfig.UIStyle style)
    {
        if (style == AppConfig.UIStyle.MoeGlass) OpacitySlider.Value = _tempGlassOpacity;
        else if (style == AppConfig.UIStyle.MoeClean) OpacitySlider.Value = _tempCleanOpacity;
        else OpacitySlider.Value = 1.0; // Classic fallback
        UpdateOpacityText();
    }

    private void UpdateBackdropComboForStyle(AppConfig.UIStyle style)
    {
        var backdrop = style switch
        {
            AppConfig.UIStyle.MoeGlass => _tempGlassBackdrop,
            AppConfig.UIStyle.MoeClean => _tempCleanBackdrop,
            _ => AppConfig.BackdropStyle.Mica
        };
        SelectComboBoxByTag(BackdropCombo, backdrop.ToString());
    }

    private void SaveBackdropToTemp(AppConfig.UIStyle style, AppConfig.BackdropStyle backdrop)
    {
        switch (style)
        {
            case AppConfig.UIStyle.MoeGlass:
                _tempGlassBackdrop = backdrop;
                break;
            case AppConfig.UIStyle.MoeClean:
                _tempCleanBackdrop = backdrop;
                break;
        }
    }

    private AppConfig.BackdropStyle GetCurrentBackdropFromCombo()
    {
        if (BackdropCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            if (Enum.TryParse<AppConfig.BackdropStyle>(item.Tag.ToString(), out var backdrop))
            {
                return backdrop;
            }
        }
        return AppConfig.BackdropStyle.Mica;
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

    private void PopulateAppWindowCombo(ComboBox combo, IEnumerable<MonitorInfo> monitors, string currentSelection)
    {
        combo.Items.Clear();
        
        // 默认选项
        combo.Items.Add(new ComboBoxItem { Content = "不移动", Tag = "None" });
        // combo.Items.Add(new ComboBoxItem { Content = "主显示器", Tag = "Main" });
        // combo.Items.Add(new ComboBoxItem { Content = "副显示器 (非主屏)", Tag = "Secondary" });

        // 具体显示器
        foreach (var m in monitors)
        {
            // 避免重复显示泛型名称，如果有 FriendlyName 最好
            string name = !string.IsNullOrEmpty(m.FriendlyName) ? m.FriendlyName : "显示器";
            string tag = !string.IsNullOrEmpty(m.FriendlyName) ? m.FriendlyName : m.DeviceName;
            
            // 标识是否为主/副
            string suffix = m.IsPrimary ? " (当前主)" : "";
            
            combo.Items.Add(new ComboBoxItem { 
                Content = $"{name}{suffix}", 
                Tag = tag 
            });
        }

        SelectComboBoxByTag(combo, currentSelection);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;

        // 实时更新当前临时变量
        SaveOpacityToTemp(_lastSelectedStyle, e.NewValue);
        UpdateOpacityText();
    }

    private void UpdateOpacityText()
    {
        if (OpacityValueText != null)
        {
            OpacityValueText.Text = $"{(int)(OpacitySlider.Value * 100)}%";
        }
    }
    
    // ...

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // ... (DDC save)
        if (DDCMonitorCombo.SelectedItem is MonitorInfo monitor)
        {
            _config.DDCMonitorId = !string.IsNullOrEmpty(monitor.FriendlyName) && monitor.FriendlyName != "Generic PnP Monitor"
                ? monitor.FriendlyName 
                : monitor.DeviceName;
        }

        if (DdcLossActionCombo.SelectedItem is ComboBoxItem lossItem && lossItem.Tag != null)
        {
            if (Enum.TryParse<AppConfig.DDCLossAction>(lossItem.Tag.ToString(), out var action))
                _config.DdcLossAction = action;
        }

        if (ThemeCombo.SelectedItem is ComboBoxItem themeItem && themeItem.Tag != null)
        {
            if (Enum.TryParse<AppConfig.UIStyle>(themeItem.Tag.ToString(), out var style))
            {
                _config.Theme = style;
            }
        }
        
        // 保存Opacity
        _config.GlassOpacity = _tempGlassOpacity;
        _config.CleanOpacity = _tempCleanOpacity;

        // 保存背景材质 (per-theme)
        SaveBackdropToTemp(_lastSelectedStyle, GetCurrentBackdropFromCombo());
        _config.GlassBackdrop = _tempGlassBackdrop;
        _config.GlassBackdrop = _tempGlassBackdrop;
        _config.CleanBackdrop = _tempCleanBackdrop;
        
        // 保存 Mascot Toggle
        _config.EnableMoeMascot = MascotToggle.IsChecked ?? false;
        _config.EnableMoeVoice = VoiceToggle.IsChecked ?? false;
        _config.MascotOpacity = MascotOpacitySlider.Value;
        
        // ... (Rest of save)
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
        if (ModeAAppWindowCombo.SelectedItem is ComboBoxItem appWinA && appWinA.Tag != null)
        {
            _config.ModeA.AppWindowTargetMonitor = appWinA.Tag.ToString() ?? "None";
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
        if (ModeBAppWindowCombo.SelectedItem is ComboBoxItem appWinB && appWinB.Tag != null)
        {
            _config.ModeB.AppWindowTargetMonitor = appWinB.Tag.ToString() ?? "None";
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
                // 使用 MonitorInfo 对象
                var result = _ddcService.GetCurrentInputSource(monitor);
                if (result.HasValue)
                {
                    // 尝试匹配已知输入源名称
                    var known = _knownInputs.FirstOrDefault(k => k.Code == result.Value);
                    string name = known.Name != null ? known.Name : "未知设备";
                    
                    DDCReadoutText.Text = $"0x{result.Value:X2} ({result.Value}) - {name}";
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

    private void MascotOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MascotOpacityValueText != null) UpdateMascotOpacityText();
    }

    private void UpdateMascotOpacityText()
    {
        if (MascotOpacityValueText != null)
        {
            MascotOpacityValueText.Text = $"{(int)(MascotOpacitySlider.Value * 100)}%";
        }
    }

    private void MascotToggle_CheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateMascotOpacityPanelVisibility();
    }

    private void UpdateMascotOpacityPanelVisibility()
    {
        if (MascotOpacityPanel != null)
        {
            MascotOpacityPanel.Visibility = (MascotToggle.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }
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
