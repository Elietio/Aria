namespace Aria.Core;

/// <summary>
/// 应用程序模式
/// </summary>
public enum AppMode
{
    /// <summary>
    /// Windows模式 - 主显示器用于Windows
    /// </summary>
    WindowsMode,

    /// <summary>
    /// PS5模式 - 主显示器用于PS5，音频和新窗口自动切换到副显示器
    /// </summary>
    PS5Mode
}

/// <summary>
/// 模式管理器 - 协调各个服务实现模式切换
/// </summary>
public class ModeManager : IDisposable
{
    private readonly AudioService _audioService;
    private readonly MonitorService _monitorService;
    private readonly WindowService _windowService;
    private readonly DDCService _ddcService;
    private readonly AppConfig _config;

    private AppMode _currentMode = AppMode.WindowsMode;
    private Timer? _ddcCheckTimer;
    private int _consecutiveDdcFailures = 0;
    private bool _disposed;

    public AppMode CurrentMode => _currentMode;

    public event EventHandler<AppMode>? ModeChanged;

    public ModeManager(
        AudioService audioService,
        MonitorService monitorService,
        WindowService windowService,
        DDCService ddcService,
        AppConfig config)
    {
        _audioService = audioService;
        _monitorService = monitorService;
        _windowService = windowService;
        _ddcService = ddcService;
        _config = config;
    }

    /// <summary>
    /// 启动DDC/CI自动检测
    /// </summary>
    public void StartDDCAutoDetect()
    {
        StopDDCAutoDetect();

        if (!_config.EnableDDCAutoDetect)
            return;

        _ddcCheckTimer = new Timer(
            CheckPrimaryMonitorInput,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(_config.DDCCheckIntervalSeconds));
    }

    /// <summary>
    /// 停止DDC/CI自动检测
    /// </summary>
    public void StopDDCAutoDetect()
    {
        _ddcCheckTimer?.Dispose();
        _ddcCheckTimer = null;
    }

    /// <summary>
    /// 切换到模式 B (原 PS5 模式)
    /// </summary>
    public async Task SwitchToPS5ModeAsync()
    {
        if (_currentMode == AppMode.PS5Mode)
            return;

        _currentMode = AppMode.PS5Mode;
        await ApplyModeProfileAsync(_config.ModeB);
        MoveAppWindow(_config.ModeB.AppWindowTargetMonitor);
        OnModeChanged(AppMode.PS5Mode);
    }

    /// <summary>
    /// 切换到模式 A (原 Windows 模式)
    /// </summary>
    public async Task SwitchToWindowsModeAsync()
    {
        if (_currentMode == AppMode.WindowsMode)
            return;

        _currentMode = AppMode.WindowsMode;
        await ApplyModeProfileAsync(_config.ModeA);
        MoveAppWindow(_config.ModeA.AppWindowTargetMonitor);
        OnModeChanged(AppMode.WindowsMode);
    }

    /// <summary>
    /// 应用模式配置
    /// </summary>
    private async Task ApplyModeProfileAsync(ModeProfile profile)
    {
        // 1. 切换音频
        if (!string.IsNullOrEmpty(profile.TargetAudioDeviceName))
        {
            await _audioService.SwitchToDeviceByNameAsync(profile.TargetAudioDeviceName);
        }

        // 2. 窗口自动移动规则
        MonitorInfo? targetMonitor = null;
        if (string.Equals(profile.TargetWindowMonitor, "Primary", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(profile.TargetWindowMonitor, "Main", StringComparison.OrdinalIgnoreCase))
        {
            targetMonitor = _monitorService.GetPrimaryMonitor();
        }
        else if (string.Equals(profile.TargetWindowMonitor, "Secondary", StringComparison.OrdinalIgnoreCase))
        {
            targetMonitor = _monitorService.GetSecondaryMonitor();
        }
        else if (!string.IsNullOrEmpty(profile.TargetWindowMonitor) && profile.TargetWindowMonitor != "None")
        {
             // 尝试匹配具体名称
             targetMonitor = _monitorService.GetAllMonitors().FirstOrDefault(m => m.DeviceName == profile.TargetWindowMonitor || m.FriendlyName == profile.TargetWindowMonitor);
        }

        if (targetMonitor != null)
        {
            _windowService.EnableAutoMoveToMonitor(targetMonitor);
        }
        else
        {
            _windowService.DisableAutoMove();
        }
    }

    /// <summary>
    /// 切换模式（自动判断当前模式并切换到另一个）
    /// </summary>
    public async Task ToggleModeAsync()
    {
        if (_currentMode == AppMode.WindowsMode)
        {
            await SwitchToPS5ModeAsync();
        }
        else
        {
            await SwitchToWindowsModeAsync();
        }
    }

    private void CheckPrimaryMonitorInput(object? state)
    {
        try
        {
            // 确定要监控的显示器 (默认使用主显示器)
            MonitorInfo? targetMonitor = null;
            if (!string.IsNullOrEmpty(_config.DDCMonitorId))
            {
                var monitors = _monitorService.GetAllMonitors();
                targetMonitor = monitors.FirstOrDefault(m => 
                    m.DeviceName.Contains(_config.DDCMonitorId) || 
                    m.FriendlyName.Contains(_config.DDCMonitorId));
            }
            
            if (targetMonitor == null)
            {
                targetMonitor = _monitorService.GetPrimaryMonitor();
            }

            if (targetMonitor == null) 
            {
                System.Console.WriteLine("[DDC] No target monitor found.");
                return;
            }

            // 获取输入源
            var result = _ddcService.GetCurrentInputSource(targetMonitor.Handle);
            if (result == null) 
            {
                _consecutiveDdcFailures++;
                System.Console.WriteLine($"[DDC] Failed to read input from {targetMonitor.FriendlyName} (Failures: {_consecutiveDdcFailures})");
                
                // 如果在 Windows 模式下且连续失败（通常连续 2 次即可确认），判定为显示器已切走
                // 如果在 Windows 模式下且连续失败（通常连续 2 次即可确认），判定为显示器已切走
                if (_currentMode == AppMode.WindowsMode && _consecutiveDdcFailures >= 2)
                {
                    System.Console.WriteLine($"[DDC] Signal lost in Windows Mode. Action: {_config.DdcLossAction}");
                    
                    if (_config.DdcLossAction == AppConfig.DDCLossAction.SwitchToModeB)
                    {
                        _consecutiveDdcFailures = 0; // 切换前重置
                        _ = SwitchToPS5ModeAsync();
                    }
                    else if (_config.DdcLossAction == AppConfig.DDCLossAction.SwitchToModeA)
                    {
                        _consecutiveDdcFailures = 0;
                        _ = SwitchToWindowsModeAsync();
                    }
                    // DoNothing does nothing
                }
                return;
            }

            // 读取成功，重置失败计数
            _consecutiveDdcFailures = 0;
            int rawValue = result.Value.RawValue;
            System.Console.WriteLine($"[DDC] Read Input: {rawValue} ({result.Value.Source}) from {targetMonitor.FriendlyName}");

            // 匹配模式触发条件
            if (_config.ModeB.TriggerInputs.Contains(rawValue))
            {
                // 触发模式 B (PS5)
                if (_currentMode != AppMode.PS5Mode)
                {
                    System.Console.WriteLine($"[DDC] Switching to PS5 Mode (Trigger: {rawValue})");
                    _ = SwitchToPS5ModeAsync();
                }
            }
            else if (_config.ModeA.TriggerInputs.Contains(rawValue))
            {
                // 触发模式 A (Windows)
                if (_currentMode != AppMode.WindowsMode)
                {
                    System.Console.WriteLine($"[DDC] Switching to Windows Mode (Trigger: {rawValue})");
                    _ = SwitchToWindowsModeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[DDC] Error: {ex.Message}");
        }
    }

    private void MoveAppWindow(string targetMonitor)
    {
        if (string.IsNullOrEmpty(targetMonitor) || targetMonitor == "None") return;

        try
        {
            // 在 WPF 主线程操作窗口
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow == null) return;

                MonitorInfo? target = null;
                if (targetMonitor == "Main" || targetMonitor == "Primary")
                {
                    target = _monitorService.GetPrimaryMonitor();
                }
                else if (targetMonitor == "Secondary")
                {
                    // 简单的获取非主显示器逻辑
                    target = _monitorService.GetAllMonitors().FirstOrDefault(m => !m.IsPrimary);
                }
                else
                {
                    // 尝试匹配名称
                     target = _monitorService.GetAllMonitors().FirstOrDefault(m => m.DeviceName == targetMonitor || m.FriendlyName == targetMonitor);
                }

                if (target != null)
                {
                    // DPI 感知处理
                    var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(mainWindow);
                    double scaleX = dpi.DpiScaleX;
                    double scaleY = dpi.DpiScaleY;

                    // 将显示器坐标(像素)转换为 WPF 坐标(DIP)
                    double workLeftDip = target.WorkLeft / scaleX;
                    double workTopDip = target.WorkTop / scaleY;
                    double workWidthDip = target.WorkWidth / scaleX;
                    double workHeightDip = target.WorkHeight / scaleY;

                    double targetX = workLeftDip + (workWidthDip - mainWindow.ActualWidth) / 2;
                    double targetY = workTopDip + (workHeightDip - mainWindow.ActualHeight) / 2;
                    
                    mainWindow.Left = targetX;
                    mainWindow.Top = targetY;
                    
                    // 确保窗口可见
                    if (mainWindow.WindowState == System.Windows.WindowState.Minimized)
                    {
                        mainWindow.WindowState = System.Windows.WindowState.Normal;
                    }
                    mainWindow.Activate();
                }
            });
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[Window] Failed to move app window: {ex.Message}");
        }
    }

    protected virtual void OnModeChanged(AppMode mode)
    {
        ModeChanged?.Invoke(this, mode);

        // 发送 Toast 通知
        if (_config.EnableToastNotifications)
        {
            string modeName = mode == AppMode.WindowsMode 
                ? _config.ModeA.Name 
                : _config.ModeB.Name;
            NotificationService.ShowModeChangeNotification(mode, modeName);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ddcCheckTimer?.Dispose();
    }
}
