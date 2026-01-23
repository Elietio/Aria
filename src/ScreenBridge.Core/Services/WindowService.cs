using System.Runtime.InteropServices;
using System.Text;

namespace ScreenBridge.Core;

/// <summary>
/// 窗口管理服务 - 监控和移动窗口
/// </summary>
public class WindowService
{
    #region Win32 API

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint EVENT_OBJECT_CREATE = 0x8000;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    private const uint WM_GETICON = 0x007F;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const int GCL_HICON = -14;
    private const int GCL_HICONSM = -34;

    #endregion

    private IntPtr _hookHandle = IntPtr.Zero;
    private readonly WinEventDelegate _winEventDelegate;  // 使用readonly确保不会被重新赋值
    private GCHandle _delegateHandle;  // 使用GCHandle防止委托被GC
    private readonly MonitorService _monitorService;
    private MonitorInfo? _targetMonitor;
    private bool _isAutoMoveEnabled = false;
    private readonly HashSet<string> _excludedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ShellExperienceHost", "SearchHost", "StartMenuExperienceHost",
        "TextInputHost", "SystemSettings"
    };

    private readonly HashSet<string> _ignoredClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd", "Progman", "WorkerW", "Windows.UI.Core.CoreWindow",
        "DV2ControlHost", "Button", "SysListView32", "ms-stickynotes"
    };

    /// <summary>
    /// 是否启用了自动移动
    /// </summary>
    public bool IsAutoMoveEnabled => _isAutoMoveEnabled;

    /// <summary>
    /// 自动移动的目标显示器
    /// </summary>
    public MonitorInfo? TargetMonitor => _targetMonitor;

    public WindowService(MonitorService monitorService)
    {
        _monitorService = monitorService;
        // 在构造函数中创建委托并固定，防止被GC
        _winEventDelegate = WinEventCallback;
        _delegateHandle = GCHandle.Alloc(_winEventDelegate);
    }

    /// <summary>
    /// 窗口信息
    /// </summary>
    public class WindowInfo
    {
        public IntPtr Handle { get; init; }
        public required string Title { get; init; }
        public int Left { get; init; }
        public int Top { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public IntPtr IconHandle { get; init; }
    }

    /// <summary>
    /// 启用新窗口自动移动到指定显示器
    /// </summary>
    public void EnableAutoMoveToMonitor(MonitorInfo targetMonitor)
    {
        _targetMonitor = targetMonitor;
        _isAutoMoveEnabled = true;

        if (_hookHandle == IntPtr.Zero)
        {
            _hookHandle = SetWinEventHook(
                EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW,
                IntPtr.Zero, _winEventDelegate,
                0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }
    }

    /// <summary>
    /// 禁用自动移动
    /// </summary>
    public void DisableAutoMove()
    {
        _isAutoMoveEnabled = false;
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// 获取所有可见窗口
    /// </summary>
    public List<WindowInfo> GetAllWindows()
    {
        var windows = new List<WindowInfo>();

        EnumWindows((hWnd, lParam) =>
        {
            if (IsWindowVisible(hWnd))
            {
                var title = GetWindowTitle(hWnd);
                if (!string.IsNullOrWhiteSpace(title) && GetWindowRect(hWnd, out var rect))
                {
                    windows.Add(new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        Left = rect.Left,
                        Top = rect.Top,
                        Width = rect.Right - rect.Left,
                        Height = rect.Bottom - rect.Top,
                        IconHandle = GetWindowIcon(hWnd)
                    });
                }
            }
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// 将窗口移动到指定显示器
    /// </summary>
    public void MoveWindowToMonitor(IntPtr windowHandle, MonitorInfo monitor)
    {
        if (GetWindowRect(windowHandle, out var rect))
        {
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            var newWidth = width;
            var newHeight = height;
            bool resizeNeeded = false;

            // 优先使用工作区（排除任务栏），如果无效则回退到整个显示器区域
            var destLeft = monitor.WorkWidth > 0 ? monitor.WorkLeft : monitor.Left;
            var destTop = monitor.WorkHeight > 0 ? monitor.WorkTop : monitor.Top;
            var destWidth = monitor.WorkWidth > 0 ? monitor.WorkWidth : monitor.Width;
            var destHeight = monitor.WorkHeight > 0 ? monitor.WorkHeight : monitor.Height;

            // 定义安全边距，防止DWM阴影导致视觉跨屏（通常阴影约7-8px）
            int safeMargin = 10;
            
            // 考虑边距后的最大可用尺寸
            var maxAvailableWidth = destWidth - (safeMargin * 2);
            var maxAvailableHeight = destHeight - (safeMargin * 2);

            // 检查窗口是否超出目标区域大小，如果是则缩小
            if (newWidth > maxAvailableWidth)
            {
                newWidth = maxAvailableWidth;
                resizeNeeded = true;
            }
            if (newHeight > maxAvailableHeight)
            {
                newHeight = maxAvailableHeight;
                resizeNeeded = true;
            }

            // 计算在目标显示器上的中心位置
            var newX = destLeft + (destWidth - newWidth) / 2;
            var newY = destTop + (destHeight - newHeight) / 2;

            // 确保窗口在显示器范围内（Clamp），并应用安全边距
            var minX = destLeft + safeMargin;
            var maxX = destLeft + destWidth - newWidth - safeMargin;
            var minY = destTop + safeMargin;
            var maxY = destTop + destHeight - newHeight - safeMargin;

            newX = Math.Max(minX, Math.Min(newX, maxX));
            newY = Math.Max(minY, Math.Min(newY, maxY));

            var uFlags = SWP_NOZORDER | SWP_SHOWWINDOW;
            if (!resizeNeeded)
            {
                uFlags |= SWP_NOSIZE;
            }

            SetWindowPos(windowHandle, IntPtr.Zero, newX, newY, newWidth, newHeight, (uint)uFlags);
        }
    }

    /// <summary>
    /// 激活并移动窗口到指定显示器
    /// </summary>
    public void ActivateAndMoveToMonitor(IntPtr windowHandle, MonitorInfo monitor)
    {
        // 获取当前窗口状态
        var placement = new WINDOWPLACEMENT();
        placement.length = Marshal.SizeOf(placement);
        GetWindowPlacement(windowHandle, ref placement);

        bool wasMaximized = placement.showCmd == SW_SHOWMAXIMIZED;

        // 1. 先还原窗口，确保能够被移动
        // 如果窗口是最小化的，这会将其恢复到正常状态
        ShowWindow(windowHandle, SW_RESTORE);
        
        // 2. 移动窗口
        MoveWindowToMonitor(windowHandle, monitor);
        
        // 3. 恢复之前的状态
        if (wasMaximized)
        {
            ShowWindow(windowHandle, SW_SHOWMAXIMIZED);
        }
        else
        {
            // 如果原来是普通状态，或者最小化状态，移动后保持普通状态（MoveWindowToMonitor已做了自适应）
            ShowWindow(windowHandle, SW_SHOW);
        }

        SetForegroundWindow(windowHandle);
    }

    /// <summary>
    /// 仅激活窗口（前置显示）
    /// </summary>
    public void ActivateWindow(IntPtr windowHandle)
    {
        // 尝试还原最小化的窗口
        if (!IsWindowVisible(windowHandle) || IsIconic(windowHandle))
        {
            ShowWindow(windowHandle, SW_RESTORE);
        }
        
        SetForegroundWindow(windowHandle);
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int SW_SHOWMAXIMIZED = 3;

    private IntPtr GetWindowIcon(IntPtr hWnd)
    {
        var iconHandle = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);
        if (iconHandle == IntPtr.Zero)
            iconHandle = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
        if (iconHandle == IntPtr.Zero)
            iconHandle = GetClassLongPtr(hWnd, GCL_HICON);
        if (iconHandle == IntPtr.Zero)
            iconHandle = GetClassLongPtr(hWnd, GCL_HICONSM);
        return iconHandle;
    }

    /// <summary>
    /// 添加排除的进程名（这些进程的窗口不会被自动移动）
    /// </summary>
    public void AddExcludedProcess(string processName)
    {
        _excludedProcesses.Add(processName);
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!_isAutoMoveEnabled || _targetMonitor == null)
            return;

        // 只处理顶层窗口
        if (idObject != 0) return;

        if (!IsWindowVisible(hwnd))
            return;

        var title = GetWindowTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title))
            return;

        string? processName = null;
        // 检查是否是需要排除的进程
        try
        {
            GetWindowThreadProcessId(hwnd, out var processId);
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            processName = process.ProcessName;
            if (_excludedProcesses.Contains(processName))
                return;
        }
        catch
        {
            // 忽略无法获取进程信息的窗口
        }

        // 检查窗口是否在主显示器上
        if (GetWindowRect(hwnd, out var rect))
        {
            // 过滤特定的类名（防止移动任务栏、桌面等）
            var className = GetWindowClassName(hwnd);
            if (!string.IsNullOrEmpty(className))
            {
                if (_ignoredClasses.Contains(className)) return;

                // 针对 explorer.exe 的特殊处理：只允许 CabinetWClass (文件夹窗口) 和 ExplorerWClass
                if (string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase))
                {
                    if (!className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) &&
                        !className.Equals("ExplorerWClass", StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }
            var primaryMonitor = _monitorService.GetPrimaryMonitor();
            if (primaryMonitor != null)
            {
                var windowCenterX = rect.Left + (rect.Right - rect.Left) / 2;
                var windowCenterY = rect.Top + (rect.Bottom - rect.Top) / 2;

                // 如果窗口中心在主显示器上，移动到目标显示器
                if (windowCenterX >= primaryMonitor.Left &&
                    windowCenterX < primaryMonitor.Left + primaryMonitor.Width &&
                    windowCenterY >= primaryMonitor.Top &&
                    windowCenterY < primaryMonitor.Top + primaryMonitor.Height)
                {
                    MoveWindowToMonitor(hwnd, _targetMonitor);
                }
            }
        }
    }

    private string GetWindowTitle(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
