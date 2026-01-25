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

    // 缓存显示器亮度值，避免刷新时闪烁 "--"
    // Key: DeviceName or FriendlyName, Value: Brightness %
    private Dictionary<string, int> _brightnessCache = new Dictionary<string, int>();

    private const int WM_HOTKEY = 0x0312;
    private IntPtr _lastIconHandle = IntPtr.Zero;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

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
            // 强制背景透明以启用 Mica
            Background = System.Windows.Media.Brushes.Transparent;

            // 加载设置到UI
            LoadSettingsToUI();
            
            // 初始应用主题
            ApplyUITheme(_config.ModeA); 

            // 刷新显示器信息
            try
            {
                RefreshMonitorInfo();
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Monitor Info Error: {ex.Message}");
            }

            // 初始化音频设备
            try
            {
                await RefreshStatusAsync();
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Audio Init Error: {ex.Message}");
            }

            _refreshTimer.Start();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"MainWindow_Loaded Fatal: {ex.Message}\n{ex.StackTrace}", "Fatal Error");
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
        TrayIcon.Dispose();
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

            // 1. 获取当前模式
            var mode = _modeManager.CurrentMode;
            UpdateUIForMode(mode);

            // 2. 更新音频信息 (Populate ComboBox)
            IEnumerable<ScreenBridge.Core.AudioDeviceInfo> devices = new List<ScreenBridge.Core.AudioDeviceInfo>();
            try 
            {
                 devices = await _audioService.GetPlaybackDevicesAsync();
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"GetPlaybackDevicesAsync Failed: {ex.Message}");
            }

            var audioDevices = devices.ToList();
            
            ScreenBridge.Core.AudioDeviceInfo? currentDevice = null;
            try
            {
                currentDevice = await _audioService.GetDefaultPlaybackDeviceAsync();
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"GetDefaultPlaybackDeviceAsync Failed: {ex.Message}");
            }

            AudioDeviceComboBox.ItemsSource = audioDevices;
            if (currentDevice != null)
            {
                AudioDeviceComboBox.SelectionChanged -= AudioDeviceComboBox_SelectionChanged;
                AudioDeviceComboBox.SelectedValue = currentDevice.Id;
                AudioDeviceComboBox.SelectionChanged += AudioDeviceComboBox_SelectionChanged;

                var volume = await _audioService.GetVolumeAsync();
                
                VolumeSlider.ValueChanged -= VolumeSlider_ValueChanged;
                VolumeSlider.Value = volume;
                if (VolumeText != null) VolumeText.Text = $"{(int)volume}%";
                VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            }
        
            // 3. 更新显示器信息
            RefreshMonitorInfo();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshStatusAsync Error: {ex.Message}");
        }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    private async void AudioDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUI) return;

        if (AudioDeviceComboBox.SelectedValue is string deviceId)
        {
            await _audioService.SetDefaultPlaybackDeviceAsync(deviceId);
            // 更新音量 (不同设备音量不同)
            var volume = await _audioService.GetVolumeAsync();
            
            VolumeSlider.ValueChanged -= VolumeSlider_ValueChanged;
            VolumeSlider.Value = volume;
            if (VolumeText != null) VolumeText.Text = $"{(int)volume}%";
            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
        }
    }



    private void RefreshMonitorInfo()
    {
        MonitorInfoPanel.Children.Clear();

        var monitors = _monitorService.GetAllMonitors()
            .OrderByDescending(m => m.IsPrimary) // 主显示器排最前
            .ToList();

        if (monitors.Count == 0)
        {
            MonitorInfoPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "未检测到显示器", 
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush") 
            });
            return;
        }

        foreach (var monitor in monitors)
        {
            // 构造显示器卡片
            var card = new Wpf.Ui.Controls.Card
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(16)
            };

            // 获取当前 VCP 输入源
            string vcpInfo = "未知";
            var vcpCode = _ddcService.GetVCPInputCode(monitor);
            if (vcpCode.HasValue)
            {
                var commonName = _ddcService.GetCommonInputName(vcpCode.Value);
                if (!string.IsNullOrEmpty(commonName))
                {
                    vcpInfo = $"{commonName} (0x{vcpCode.Value:X2})";
                }
                else
                {
                   vcpInfo = $"0x{vcpCode.Value:X2}";
                }
            }

            // 图标与重音色 Key
            var iconSymbol = monitor.IsPrimary ? Wpf.Ui.Controls.SymbolRegular.Star24 : Wpf.Ui.Controls.SymbolRegular.Desktop24;
            
            // 主显示器增加边框强调
            if (monitor.IsPrimary)
            {
                 // 使用资源引用以支持动态主题
                card.SetResourceReference(Wpf.Ui.Controls.Card.BorderBrushProperty, "SystemAccentBrush");
                card.BorderThickness = new Thickness(2);
                card.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(15, 128, 128, 128)); // 微亮背景
            }

            // 内容布局
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Brightness Area

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            
            // 标题行
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
            headerPanel.Children.Add(new Wpf.Ui.Controls.SymbolIcon 
            { 
                Symbol = iconSymbol, 
                FontSize = 18, 
                // 主显示器: 金色; 副显示器: 灰色
                Foreground = monitor.IsPrimary ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)) : (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
                Filled = monitor.IsPrimary,
                Margin = new Thickness(0,0,8,0)
            });
            
            var titleText = new System.Windows.Controls.TextBlock 
            { 
                Text = monitor.IsPrimary ? $"主显示器 - {monitor.FriendlyName}" : $"副显示器 - {monitor.FriendlyName}", 
                FontWeight = FontWeights.Bold,
                FontSize = 14
            };
            
            if (monitor.IsPrimary)
            {
                 titleText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SystemAccentBrush");
            }
            else
            {
                titleText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            }

            headerPanel.Children.Add(titleText);
            stack.Children.Add(headerPanel);

            // 详细信息
            // 1. 分辨率 & 位置
            var infoText = new System.Windows.Controls.TextBlock 
            { 
                Text = $"分辨率: {monitor.Width}x{monitor.Height} | 位置: ({monitor.Left}, {monitor.Top})",
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
                FontSize = 12,
                Margin = new Thickness(26, 0, 0, 4)
            };
            stack.Children.Add(infoText);

            // 2. 技术参数 (Hz, Bit)
            var techText = new System.Windows.Controls.TextBlock 
            { 
                Text = $"刷新率: {monitor.RefreshRate}Hz | 颜色深度: {monitor.BitDepth}-bit",
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
                FontSize = 12,
                Margin = new Thickness(26, 0, 0, 4)
            };
            stack.Children.Add(techText);
            
            // 3. 当前输入源
            var inputInfoText = new System.Windows.Controls.TextBlock 
            { 
                Text = $"当前输入源: {vcpInfo}",
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorTertiaryBrush"),
                FontSize = 12,
                Margin = new Thickness(26, 0, 0, 0)
            };
            stack.Children.Add(inputInfoText);
            
            grid.Children.Add(stack);

            // 亮度显示 (右侧)
            var brightnessStack = new StackPanel 
            { 
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 60
            };
            Grid.SetColumn(brightnessStack, 1);
            
            var brightnessIcon = new Wpf.Ui.Controls.SymbolIcon 
            { 
                Symbol = Wpf.Ui.Controls.SymbolRegular.WeatherSunny24, 
                FontSize=20, 
                Margin=new Thickness(0,0,0,4), 
                HorizontalAlignment=HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush")
            };

            // 尝试从缓存获取初始值
            string initialBrightness = "--%";
            string cacheKey = monitor.DeviceName; // Prefer DeviceName as ID
            if (_brightnessCache.ContainsKey(cacheKey))
            {
                initialBrightness = $"{_brightnessCache[cacheKey]}%";
            }

            var brightnessText = new System.Windows.Controls.TextBlock 
            { 
                Text = initialBrightness,
                FontSize = 12, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush")
            };
            
            brightnessStack.Children.Add(brightnessIcon);
            brightnessStack.Children.Add(brightnessText);
            grid.Children.Add(brightnessStack);
            
            card.Content = grid;
            MonitorInfoPanel.Children.Add(card);

            // 异步加载亮度 (VCP 0x10)
            if (monitor.Handle != IntPtr.Zero)
            {
               Task.Run(() => 
               {
                   try 
                   {
                       var brightness = _ddcService.GetVCPValue(monitor, 0x10);
                       if (brightness >= 0)
                       {
                           // 更新缓存
                           lock(_brightnessCache) 
                           {
                               _brightnessCache[cacheKey] = brightness.Value;
                           }

                           Application.Current.Dispatcher.Invoke(() => 
                           {
                               brightnessText.Text = $"{brightness}%";
                           });
                       }
                   }
                   catch (Exception ex)
                   {
                       // 失败时记录日志，但不更新UI (保持缓存值或"--")
                       System.Diagnostics.Debug.WriteLine($" brightness fetch failed: {ex.Message}");
                   }
               });
            }
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
            
            // 更新模式图标为游戏手柄
            ModeIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.XboxController24;
            ModeIcon.Foreground = System.Windows.Media.Brushes.White;
            
            var ps5Gradient = new System.Windows.Media.LinearGradientBrush();
            ps5Gradient.StartPoint = new System.Windows.Point(0, 0);
            ps5Gradient.EndPoint = new System.Windows.Point(1, 1);
            ps5Gradient.GradientStops.Add(new System.Windows.Media.GradientStop(
                System.Windows.Media.Color.FromRgb(0x7B, 0x1F, 0xA2), 0.0)); // Deep Purple
            ps5Gradient.GradientStops.Add(new System.Windows.Media.GradientStop(
                System.Windows.Media.Color.FromRgb(0x7C, 0x4D, 0xFF), 1.0)); // Deep Purple Accent
            ModeIconBorder.Background = ps5Gradient;
        }
        else
        {
            ModeTitle.Text = _config.ModeA.Name; // "Windows 模式"
            ModeDescription.Text = _config.ModeA.Description;
            SwitchModeButton.Content = $"切换到 {_config.ModeB.Name}";
            
            // 更新模式图标为桌面
            ModeIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Desktop24;
            ModeIcon.Foreground = System.Windows.Media.Brushes.White;
            
            var winGradient = new System.Windows.Media.LinearGradientBrush();
            winGradient.StartPoint = new System.Windows.Point(0, 0);
            winGradient.EndPoint = new System.Windows.Point(1, 1);
            winGradient.GradientStops.Add(new System.Windows.Media.GradientStop(
                System.Windows.Media.Color.FromRgb(0x19, 0x76, 0xD2), 0.0)); // Blue
            winGradient.GradientStops.Add(new System.Windows.Media.GradientStop(
                System.Windows.Media.Color.FromRgb(0x42, 0xA5, 0xF5), 1.0)); // Light Blue
            ModeIconBorder.Background = winGradient;
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
        // _ = RefreshStatusAsync();

        // 应用主题 (包含托盘图标更新)
        ApplyUITheme(mode == AppMode.PS5Mode ? _config.ModeB : _config.ModeA);
        
        // 更新托盘图标 (ApplyUITheme 中可能会处理界面部分，TrayIcon 还是走 UpdateTrayIcon 统一处理比较好，或者集成)
        // 这里的 UpdateTrayIcon 是针对 Tray 的。
        // 我们修改 UpdateTrayIcon 内部逻辑去支持 Moe 风格。
        UpdateTrayIcon(mode);
    }

    private void UpdateTrayIcon(AppMode mode)
    {
        try
        {
            string subPath;
            
            if (_config.Theme == AppConfig.UIStyle.MoeGlass || _config.Theme == AppConfig.UIStyle.MoeClean)
            {
                // Moe 风格
                subPath = mode == AppMode.PS5Mode ? "Assets/Moe/tray_ps5.ico" : "Assets/Moe/tray_windows.ico";
            }
            else
            {
                // Classic 风格
                subPath = mode == AppMode.PS5Mode ? "Assets/tray_ps5.ico" : "Assets/tray_windows.ico";
            }
            
            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, subPath);
            
            if (System.IO.File.Exists(fullPath))
            {
                // 直接使用 Icon 构造函数加载 .ico 文件，以支持多尺寸 (16/32/48/64/256)
                // 这样能保证在不同 DPI 下都清晰
                var icon = new System.Drawing.Icon(fullPath);
                
                TrayIcon.Icon = icon;
                
                // 注意: new Icon(path) 不需要手动 destroy handle，GC 会处理 (或者 Icon Dispose)
                // 但之前的代码用了 DestroyIcon，如果 _lastIconHandle 还是旧的 handle (来自 bitmap.GetHicon)，还是需要清理。
                // 不过既然这里用了新的 managed Icon 对象，TrayIcon.Icon setter 会自己管理吗？
                // H.NotifyIcon.Wpf/WinForms 这种通常接受 System.Drawing.Icon.
                // 之前的 _lastIconHandle 是为了清理 GetHicon() 产生的 GDI 资源。
                // 这次我们不需要 _lastIconHandle 了，或者保留清理逻辑以防切换回旧 Drawing 逻辑 (虽然已经被我删了)。
                if (_lastIconHandle != IntPtr.Zero) 
                {
                    DestroyIcon(_lastIconHandle);
                    _lastIconHandle = IntPtr.Zero;
                }
                
                string suffix = (_config.Theme == AppConfig.UIStyle.MoeGlass || _config.Theme == AppConfig.UIStyle.MoeClean) 
                    ? " (Moe)" : "";
                TrayIcon.ToolTipText = mode == AppMode.PS5Mode 
                    ? $"ScreenBridge - {_config.ModeB.Name}{suffix}" 
                    : $"ScreenBridge - {_config.ModeA.Name}{suffix}";
            }
            else
            {
                Console.WriteLine($"[TrayIcon] Icon file not found: {fullPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrayIcon] Failed to update icon: {ex.Message}");
        }
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
            // 确保主题也被刷新
            ApplyUITheme(_modeManager.CurrentMode == AppMode.PS5Mode ? _config.ModeB : _config.ModeA);
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

    private ModeProfile _currentProfile;

    /// <summary>
    /// 应用界面风格 (Classic / Moe)
    /// </summary>
    private void ApplyUITheme(ModeProfile profile)
    {
        if (profile == null) profile = _config.ModeA;
        _currentProfile = profile;

        bool isMoe = _config.Theme == AppConfig.UIStyle.MoeGlass || _config.Theme == AppConfig.UIStyle.MoeClean;
        bool isModeB = profile.Name == _config.ModeB.Name;

        // 1. 设置窗口背景透明以启用 Mica/Acrylic
        if (isMoe)
        {
            // 根据当前Theme选择对应的背景材质
            var backdrop = (_config.Theme == AppConfig.UIStyle.MoeGlass) 
                ? _config.GlassBackdrop 
                : _config.CleanBackdrop;
            
            this.WindowBackdropType = backdrop == AppConfig.BackdropStyle.Acrylic 
                ? WindowBackdropType.Acrylic 
                : WindowBackdropType.Mica;
            
            // 必须设为透明/null，否则背景材质被遮挡
            this.Background = null;
            
            // 确保根 Grid 也是透明的
            if (this.Content is Grid rootGrid) rootGrid.Background = System.Windows.Media.Brushes.Transparent;
        }
        else
        {
            // Classic: 恢复默认主题背景 (通常是深色)
            this.WindowBackdropType = WindowBackdropType.None;
            this.ClearValue(Window.BackgroundProperty);
        }

        // 2. 处理图标/立绘
        if (isMoe)
        {
            ModeIcon.Visibility = Visibility.Collapsed;
            ModeImage.Visibility = Visibility.Visible;
            
            string imagePath = isModeB ? "Assets/Moe/mode_ps5.png" : "Assets/Moe/mode_windows.png";
            try
            {
                var uri = new Uri($"pack://application:,,,/ScreenBridge.App;component/{imagePath}");
                var bitmap = new System.Windows.Media.Imaging.BitmapImage(uri);
                ModeImage.Source = bitmap;
            }
            catch {}
        }
        else
        {
            ModeIcon.Visibility = Visibility.Visible;
            ModeImage.Visibility = Visibility.Collapsed;
            
            if (!string.IsNullOrEmpty(profile.Icon) && Enum.TryParse<Wpf.Ui.Controls.SymbolRegular>(profile.Icon, out var symbol))
            {
                ModeIcon.Symbol = symbol;
            }
        }
        // 4. 更新强调色 (Galaxy Brain Option: Colors + Brushes)
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                bool isPinkMode = (profile.Name != null && profile.Name.Contains("PS5", StringComparison.OrdinalIgnoreCase)) 
                                  || profile.Name == _config.ModeB.Name;

                var color = isPinkMode 
                    ? System.Windows.Media.Color.FromRgb(0xF4, 0x8F, 0xB1) // PS5 Pink
                    : System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3); // Windows Blue

                var brush = new System.Windows.Media.SolidColorBrush(color);
                brush.Freeze();

                // 核心: Wpf.Ui 很多控件直接绑定 Color 资源而不是 Brush 资源，所以必须同时覆盖 Color Key
                var brushKeys = new[] 
                {
                    // System Keys
                    "SystemAccentBrush", "SystemAccentBrushPrimary", "SystemAccentBrushSecondary", "SystemAccentBrushTertiary",
                    // Accent Fills
                    "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush",
                    // Accent Text
                    "AccentTextFillColorPrimaryBrush", "AccentTextFillColorSecondaryBrush", "AccentTextFillColorTertiaryBrush",
                    // Fallback
                    "ControlFillColorDefaultBrush"
                };

                // 对应的 Color Key (去掉 "Brush" 后缀)
                var colorKeys = new List<string> 
                { 
                    "SystemAccentColor", "SystemAccentColorPrimary", "SystemAccentColorSecondary", "SystemAccentColorTertiary" 
                };
                foreach(var k in brushKeys) 
                {
                    // Wpf.Ui 命名惯例: AccentFillColorDefaultBrush -> AccentFillColorDefault (Color)
                    if (k.EndsWith("Brush")) colorKeys.Add(k.Substring(0, k.Length - 5));
                }

                void InjectResources(ResourceDictionary target)
                {
                    foreach (var key in colorKeys) target[key] = color;
                    foreach (var key in brushKeys) target[key] = brush;
                }

                // A. Apply to Application Resources
                InjectResources(Application.Current.Resources);

                // B. Apply to MainWindow
                InjectResources(this.Resources);

                // C. Apply to All Other Windows
                foreach (Window win in Application.Current.Windows)
                {
                    if (win != this)
                    {
                        InjectResources(win.Resources);
                        if (win.Content is UIElement ui) ui.InvalidateVisual();
                    }
                }
                
                this.InvalidateVisual();
                
                System.Diagnostics.Debug.WriteLine($"[Theme] Forced Accent Color & Brushes: {color}");
            }

            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update accent color: {ex.Message}");
            }
            
            // 5. 更新卡片样式 (Glass vs Clean)
            UpdateCardStyles(_config.Theme);
        });
    }

    private void UpdateCardStyles(AppConfig.UIStyle style)
    {
        var cards = new[] { ModeStatusCard, AudioSettingsCard, HotkeySettingsCard, MonitorInfoCard };

        if (style == AppConfig.UIStyle.Classic)
        {
            // 恢复默认
            foreach (var card in cards)
            {
                if (card == null) continue;
                card.ClearValue(Wpf.Ui.Controls.Card.BackgroundProperty);
                card.ClearValue(Wpf.Ui.Controls.Card.BorderBrushProperty);
                card.ClearValue(Wpf.Ui.Controls.Card.BorderThicknessProperty);
                card.ClearValue(Wpf.Ui.Controls.Card.EffectProperty); 
            }
        }
        else if (style == AppConfig.UIStyle.MoeGlass)
        {
            // Refined Glass (精致玻璃)
            byte alpha = (byte)(Math.Max(0.01, Math.Min(1.0, _config.GlassOpacity)) * 255);
            
            var glassBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 255, 255, 255));
            var borderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(alpha * 3), 255, 255, 255)); // 边框稍微亮一点
            
            foreach (var card in cards)
            {
                if (card == null) continue;
                card.Background = glassBrush;
                card.BorderBrush = borderBrush;
                card.BorderThickness = new Thickness(1);
                
                // 柔和投影
                var shadow = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    Opacity = 0.25,
                    BlurRadius = 20,
                    ShadowDepth = 6,
                    Direction = 270
                };
                card.Effect = shadow;
            }
        }
        else if (style == AppConfig.UIStyle.MoeClean)
        {
            // Refined Clean (极简隐形 - Stealth)
            byte alpha = (byte)(Math.Max(0.01, Math.Min(1.0, _config.CleanOpacity)) * 255);
            
            var cleanBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 255, 255, 255));

            foreach (var card in cards)
            {
                if (card == null) continue;
                card.Background = cleanBrush;
                card.BorderThickness = new Thickness(0); 
                card.BorderBrush = System.Windows.Media.Brushes.Transparent;
                
                // 彻底移除阴影
                card.Effect = null;
            }
        }
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