using System.Runtime.InteropServices;

namespace ScreenBridge.Core;

/// <summary>
/// 显示器信息
/// </summary>
public class MonitorInfo
{
    public required string DeviceName { get; init; }
    public required string FriendlyName { get; init; }
    public IntPtr Handle { get; init; }
    public bool IsPrimary { get; init; }
    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int WorkLeft { get; init; }
    public int WorkTop { get; init; }
    public int WorkWidth { get; init; }
    public int WorkHeight { get; init; }
    public int RefreshRate { get; set; }
    public int BitDepth { get; set; }
    public string DevicePath { get; init; } = string.Empty;
}

/// <summary>
/// 显示器服务 - 枚举显示器和检测信号状态
/// </summary>
public class MonitorService
{
    #region Win32 API

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, 
        MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, 
        ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private const uint MONITORINFOF_PRIMARY = 1;

    #endregion

    private readonly List<MonitorInfo> _monitors = new();

    /// <summary>
    /// 获取所有显示器
    /// </summary>
    public IReadOnlyList<MonitorInfo> GetAllMonitors()
    {
        _monitors.Clear();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);

        return _monitors.AsReadOnly();
    }

    private bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
    {
        var info = new MONITORINFOEX();
        info.cbSize = Marshal.SizeOf(info);

        if (GetMonitorInfo(hMonitor, ref info))
        {
            var monitor = new MonitorInfo
            {
                DeviceName = info.szDevice,
                FriendlyName = GetFriendlyName(info.szDevice),
                Handle = hMonitor,
                IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                Left = info.rcMonitor.Left,
                Top = info.rcMonitor.Top,
                Width = info.rcMonitor.Right - info.rcMonitor.Left,
                Height = info.rcMonitor.Bottom - info.rcMonitor.Top,
                WorkLeft = info.rcWork.Left,
                WorkTop = info.rcWork.Top,
                WorkWidth = info.rcWork.Right - info.rcWork.Left,
                WorkHeight = info.rcWork.Bottom - info.rcWork.Top,
                DevicePath = info.szDevice
            };

            // 获取详细信息 (Hz, Bit)
            var devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (EnumDisplaySettings(info.szDevice, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                monitor.RefreshRate = devMode.dmDisplayFrequency;
                monitor.BitDepth = devMode.dmBitsPerPel;
            }

            _monitors.Add(monitor);
        }

        return true;
    }

    /// <summary>
    /// 获取主显示器
    /// </summary>
    public MonitorInfo? GetPrimaryMonitor()
    {
        return GetAllMonitors().FirstOrDefault(m => m.IsPrimary);
    }

    /// <summary>
    /// 获取副显示器（非主显示器中的第一个）
    /// </summary>
    public MonitorInfo? GetSecondaryMonitor()
    {
        return GetAllMonitors().FirstOrDefault(m => !m.IsPrimary);
    }

    /// <summary>
    /// 检测指定显示器是否可能处于非Windows输入状态
    /// 通过检测最近是否有鼠标活动来判断
    /// </summary>
    public bool IsMonitorActivelyUsed(MonitorInfo monitor)
    {
        var cursorPos = GetCursorPosition();
        return cursorPos.X >= monitor.Left && 
               cursorPos.X < monitor.Left + monitor.Width &&
               cursorPos.Y >= monitor.Top && 
               cursorPos.Y < monitor.Top + monitor.Height;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    private (int X, int Y) GetCursorPosition()
    {
        GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    private string GetFriendlyName(string deviceName)
    {
        try
        {
            // 尝试使用 QueryDisplayConfig 获取更友好的名称 (从 EDID)
            var name = GetMonitorFriendlyNameFromPath(deviceName);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            // 回退到 EnumDisplayDevices
            var device = new DISPLAY_DEVICE();
            device.cb = Marshal.SizeOf(device);
            if (EnumDisplayDevices(deviceName, 0, ref device, 0))
            {
                if (!string.IsNullOrEmpty(device.DeviceString) && device.DeviceString != "Generic PnP Monitor")
                   return device.DeviceString;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting friendly name: {ex.Message}");
        }

        return deviceName.Replace(@"\\.\", "");
    }

    private string? GetMonitorFriendlyNameFromPath(string gdiDeviceName)
    {
        uint numPathArrayElements = 0;
        uint numModeInfoArrayElements = 0;
        const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        const int ERROR_SUCCESS = 0;

        if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, ref numModeInfoArrayElements) != ERROR_SUCCESS)
            return null;

        var pathInfoArray = new DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
        var modeInfoArray = new DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

        if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, pathInfoArray, ref numModeInfoArrayElements, modeInfoArray, IntPtr.Zero) != ERROR_SUCCESS)
            return null;

        foreach (var path in pathInfoArray)
        {
            // 获取源名称 (GDI Device Name, e.g. \\.\DISPLAY1)
            var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            sourceName.header.size = Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
            sourceName.header.adapterId = path.sourceInfo.adapterId;
            sourceName.header.id = path.sourceInfo.id;

            if (DisplayConfigGetDeviceInfo(ref sourceName) == ERROR_SUCCESS)
            {
                if (sourceName.viewGdiDeviceName == gdiDeviceName)
                {
                    // 匹配到了，获取目标名称 (Friendly Name)
                    var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    targetName.header.size = Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
                    targetName.header.adapterId = path.targetInfo.adapterId;
                    targetName.header.id = path.targetInfo.id;

                    if (DisplayConfigGetDeviceInfo(ref targetName) == ERROR_SUCCESS)
                    {
                         return targetName.monitorFriendlyDeviceName;
                    }
                }
            }
        }
        return null;
    }

    #region DisplayConfig API

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(uint flags, ref uint numPathArrayElements, ref uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx; // index into mode info array
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        public bool targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        public DISPLAYCONFIG_TARGET_MODE targetMode;
        public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_TARGET_MODE 
    {
        public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public DISPLAYCONFIG_RATIONAL hSyncFreq;
        public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize;
        public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard; 
        public uint scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width;
        public uint height;
        public uint pixelFormat;
        public POINT position;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
    {
        public POINT PathSourceSize;
        public RECT DesktopImageRegion;
        public RECT DesktopImageClip;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_2DREGION
    {
        public uint cx;
        public uint cy;
    }
     
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_HEADER
    {
        public uint type;
        public int size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

    #endregion

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    const int ENUM_CURRENT_SETTINGS = -1;

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }
}
