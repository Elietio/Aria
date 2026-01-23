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
}
