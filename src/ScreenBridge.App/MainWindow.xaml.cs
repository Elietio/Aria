using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using ScreenBridge.Core;
using Wpf.Ui.Controls;

namespace ScreenBridge.App;

/// <summary>
/// MainWindow 交互逻辑
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly AppConfig _config;
    private readonly AudioService _audioService;
    private readonly MonitorService _monitorService;
    private readonly WindowService _windowService;
    private readonly DDCService _ddcService;
    private readonly HotkeyService _hotkeyService;
    private readonly ModeManager _modeManager;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isExplicitExit;

    private const int WM_HOTKEY = 0x0312;

    public MainWindow()
    {
        try
        {
            InitializeComponent();

            // 初始化服务
            _config = AppConfig.Load();
            _audioService = new AudioService();
            _monitorService = new MonitorService();
            _ddcService = new DDCService();
            _windowService = new WindowService(_monitorService);
            _hotkeyService = new HotkeyService();
            _modeManager = new ModeManager(_audioService, _monitorService, _windowService, _ddcService, _config);

            // 绑定事件
            _modeManager.ModeChanged += ModeManager_ModeChanged;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            SourceInitialized += MainWindow_SourceInitialized;

            // 初始化定时器
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"程序启动初始化失败:\n{ex.Message}\n\n堆栈:\n{ex.StackTrace}", "启动错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            _isExplicitExit = true;
            System.Windows.Application.Current?.Shutdown();
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 加载设置到UI
            LoadSettingsToUI();

            // 刷新显示器信息
            try
            {
                RefreshMonitorInfo();
            }
            catch
            {
                // 忽略显示器信息刷新错误
            }

            // 处理启动最小化 (暂时禁用以排查空白窗口问题)
            // if (_config.StartMinimized)
            // {
            //     WindowState = WindowState.Minimized;
            //     Hide();
            // }

            // 初始化音频设备
            try
            {
                await InitializeAudioDevicesAsync();
            }
            catch
            {
                // 忽略音频设备初始化错误
            }
            
            // 启动定时刷新
            _refreshTimer.Start();
            await RefreshStatusAsync();

            // 热键功能暂时禁用，因为可能导致崩溃
            // 用户可以通过UI按钮来切换模式
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载错误: {ex.Message}");
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            _hotkeyService.Initialize(hwnd);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            // 注册热键
            _hotkeyService.RegisterHotkey(
                (HotkeyService.ModifierKeys)_config.SwitchModeModifiers,
                _config.SwitchModeKey,
                async () => await _modeManager.ToggleModeAsync());

            _hotkeyService.RegisterHotkey(
                (HotkeyService.ModifierKeys)_config.WindowOverviewModifiers,
                _config.WindowOverviewKey,
                ShowWindowOverview);
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"热键注册失败: {ex.Message}");
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _refreshTimer.Stop();
        
        // 保存设置
        SaveSettings();

        // 清理资源
        _hotkeyService.Dispose();
        _modeManager.Dispose();
        _audioService.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            _hotkeyService.ProcessHotkeyMessage(wParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void LoadSettingsToUI()
    {
        AutoStartToggle.IsChecked = _config.AutoStart;
        DDCAutoDetectToggle.IsChecked = _config.EnableDDCAutoDetect;
    }

    private void SaveSettings()
    {
        _config.AutoStart = AutoStartToggle.IsChecked ?? false;
        _config.EnableDDCAutoDetect = DDCAutoDetectToggle.IsChecked ?? false;
        _config.Save();
    }

    private async Task InitializeAudioDevicesAsync()
    {
        // 音频设备初始化逻辑已移至 ModeManager，此处无需操作
        await Task.CompletedTask;
    }

    private bool _isUpdatingUI;

    private async Task RefreshStatusAsync()
    {
        try
        {
            _isUpdatingUI = true;
            var device = await _audioService.GetDefaultPlaybackDeviceAsync();
            if (device != null)
            {
                CurrentAudioDeviceText.Text = device.Name;
                ToolTipService.SetToolTip(CurrentAudioDeviceText, device.FullName);
                
                // 更新音量
                var volume = await _audioService.GetVolumeAsync();
                VolumeSlider.Value = volume;
                VolumeText.Text = $"{(int)volume}%";
            }
        }
        catch { }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    private void RefreshMonitorInfo()
    {
        MonitorInfoPanel.Children.Clear();
        var monitors = _monitorService.GetAllMonitors();

        if (!monitors.Any())
        {
             MonitorInfoPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = "未检测到显示器", Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush") });
             return;
        }

        foreach (var monitor in monitors)
        {
            var card = new Wpf.Ui.Controls.Card 
            { 
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12)
            };
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题: 主/副 + 名称
            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = $"{(monitor.IsPrimary ? "⭐ 主显示器" : "🖥️ 副显示器")} - {monitor.FriendlyName}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);

            // 详细信息
            var detailsBlock = new System.Windows.Controls.TextBlock
            {
                Text = $"分辨率: {monitor.Width}x{monitor.Height}  |  位置: ({monitor.Left}, {monitor.Top})",
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
                FontSize = 12
            };
            Grid.SetRow(detailsBlock, 1);
            grid.Children.Add(detailsBlock);

            // 额外信息 (DDC 输入源)
            var inputSource = _ddcService.GetCurrentInputSource(monitor.Handle);
            if (inputSource.HasValue)
            {
                var (source, rawValue) = inputSource.Value;
                var inputBlock = new System.Windows.Controls.TextBlock
                {
                    Text = $"当前输入源: {source} (VCP: 0x{rawValue:X2})",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorTertiaryBrush"),
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                Grid.SetRow(inputBlock, 2);
                grid.Children.Add(inputBlock);
            }
            else
            {
                var errorBlock = new System.Windows.Controls.TextBlock
                {
                    Text = "⚠️ 无法读取输入源 (DDC/CI 未响应)",
                    Foreground = System.Windows.Media.Brushes.OrangeRed,
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                Grid.SetRow(errorBlock, 2);
                grid.Children.Add(errorBlock);
            }

            card.Content = grid;
            MonitorInfoPanel.Children.Add(card);
        }
    }

    private async void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUI) return;
        
        if (VolumeText != null)
        {
            VolumeText.Text = $"{(int)e.NewValue}%";
        }
        
        await _audioService.SetVolumeAsync(e.NewValue);
    }

    private void ModeManager_ModeChanged(object? sender, AppMode mode)
    {
        Dispatcher.Invoke(() => UpdateUIForMode(mode));
    }

    private void UpdateUIForMode(AppMode mode)
    {
        if (mode == AppMode.PS5Mode)
        {
            ModeTitle.Text = _config.ModeB.Name; // "PS5 模式"
            ModeDescription.Text = _config.ModeB.Description;
            SwitchModeButton.Content = $"切换到 {_config.ModeA.Name}";
            
            // Icon handling (Simple mapping or look up from config icon string)
            // For now hardcode or try to parse Icon string if possible
             try { ModeIcon.Symbol = (Wpf.Ui.Controls.SymbolRegular)Enum.Parse(typeof(Wpf.Ui.Controls.SymbolRegular), _config.ModeB.Icon); } catch { ModeIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.XboxController24; }
        }
        else
        {
            ModeTitle.Text = _config.ModeA.Name; // "Windows 模式"
            ModeDescription.Text = _config.ModeA.Description;
            SwitchModeButton.Content = $"切换到 {_config.ModeB.Name}";
            
            try { ModeIcon.Symbol = (Wpf.Ui.Controls.SymbolRegular)Enum.Parse(typeof(Wpf.Ui.Controls.SymbolRegular), _config.ModeA.Icon); } catch { ModeIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Desktop24; }
        }

        // 更新窗口规则状态
        if (_windowService.IsAutoMoveEnabled && _windowService.TargetMonitor != null)
        {
            WindowRuleStatus.Text = $"✅ 窗口自动移动: 已启用 (目标: {_windowService.TargetMonitor.FriendlyName})";
            WindowRuleStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);
        }
        else
        {
            WindowRuleStatus.Text = "⏸️ 窗口自动移动: 未启用";
            WindowRuleStatus.Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorTertiaryBrush");
        }
        
        // 模式切换后立即刷新状态
        _ = RefreshStatusAsync();
    }

    private async void SwitchModeButton_Click(object sender, RoutedEventArgs e)
    {
        await _modeManager.ToggleModeAsync();
        await RefreshStatusAsync();
    }

    private void ConfigureRulesButton_Click(object sender, RoutedEventArgs e)
    {
        var rulesWindow = new RulesWindow(_config, _monitorService, _audioService)
        {
            Owner = this
        };
        
        if (rulesWindow.ShowDialog() == true)
        {
            // 配置已保存，刷新 UI
            UpdateUIForMode(_modeManager.CurrentMode);
        }
    }

    private void OpenOverviewButton_Click(object sender, RoutedEventArgs e)
    {
        var overview = new WindowOverview(_windowService, _monitorService);
        overview.Show();
    }

    private void ShowWindowOverview()
    {
        // TODO: 显示窗口概览界面
        var overviewWindow = new WindowOverview(_windowService, _monitorService);
        overviewWindow.Show();
    }

    private void AutoStartToggle_Checked(object sender, RoutedEventArgs e)
    {
        SetAutoStart(true);
    }

    private void AutoStartToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        SetAutoStart(false);
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            
            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key?.SetValue("ScreenBridge", $"\"{exePath}\"");
                }
            }
            else
            {
                key?.DeleteValue("ScreenBridge", false);
            }
        }
        catch
        {
            // 忽略注册表操作错误
        }
    }

    private void DDCAutoDetectToggle_Checked(object sender, RoutedEventArgs e)
    {
        _config.EnableDDCAutoDetect = true;
        _modeManager.StartDDCAutoDetect();
    }

    private void DDCAutoDetectToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _config.EnableDDCAutoDetect = false;
        _modeManager.StopDDCAutoDetect();
    }

    #region Tray Event Handlers

    private void FluentWindow_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void TrayShow_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async void TrayWindowsMode_Click(object sender, RoutedEventArgs e)
    {
        await _modeManager.SwitchToWindowsModeAsync();
    }

    private async void TrayPS5Mode_Click(object sender, RoutedEventArgs e)
    {
        await _modeManager.SwitchToPS5ModeAsync();
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _isExplicitExit = true;
        Close();
    }

    #endregion
}