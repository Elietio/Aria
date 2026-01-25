using System.Runtime.InteropServices;

namespace ScreenBridge.Core;

/// <summary>
/// DDC/CI 服务 - 用于通过DDC/CI协议与显示器通信
/// 可以检测显示器输入源
/// </summary>
public class DDCService
{
    #region Win32 API for DDC/CI

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor, uint dwPhysicalMonitorArraySize, 
        [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(
        uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(
        IntPtr hPhysicalMonitor, byte bVCPCode, 
        out LPMC_VCP_CODE_TYPE pvct, out uint pdwCurrentValue, out uint pdwMaximumValue);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetVCPFeature(
        IntPtr hPhysicalMonitor, byte bVCPCode, uint dwNewValue);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    private enum LPMC_VCP_CODE_TYPE
    {
        MC_MOMENTARY,
        MC_SET_PARAMETER
    }

    // VCP代码
    private const byte VCP_INPUT_SELECT = 0x60;  // 输入源选择

    #endregion

    /// <summary>
    /// 输入源类型
    /// </summary>
    public enum InputSource
    {
        Unknown = 0,
        VGA1 = 1,
        VGA2 = 2,
        DVI1 = 3,
        DVI2 = 4,
        CompositeVideo1 = 5,
        CompositeVideo2 = 6,
        SVideo1 = 7,
        SVideo2 = 8,
        Tuner1 = 9,
        Tuner2 = 10,
        Tuner3 = 11,
        ComponentVideo1 = 12,
        ComponentVideo2 = 13,
        ComponentVideo3 = 14,
        DisplayPort1 = 15,
        DisplayPort2 = 16,
        HDMI1 = 17,
        HDMI2 = 18,
        USBC = 27
    }

    /// <summary>
    /// 获取显示器当前输入源及原始值
    /// </summary>
    public (InputSource Source, int RawValue)? GetCurrentInputSource(IntPtr monitorHandle)
    {
        try
        {
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitorHandle, out var count) || count == 0)
                return null;

            var physicalMonitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(monitorHandle, count, physicalMonitors))
                return null;

            try
            {
                var physicalMonitor = physicalMonitors[0].hPhysicalMonitor;
                if (GetVCPFeatureAndVCPFeatureReply(physicalMonitor, VCP_INPUT_SELECT, 
                    out _, out var currentValue, out _))
                {
                    return (MapToInputSource((int)currentValue), (int)currentValue);
                }
            }
            finally
            {
                DestroyPhysicalMonitors(count, physicalMonitors);
            }
        }
        catch
        {
            // DDC/CI可能不被支持
        }

        return null;
    }

    /// <summary>
    /// 检测显示器是否在使用HDMI输入（可能是PS5）
    /// </summary>
    public bool IsUsingHDMI(IntPtr monitorHandle)
    {
        var result = GetCurrentInputSource(monitorHandle);
        if (result == null) return false;
        var input = result.Value.Source;
        return input == InputSource.HDMI1 || input == InputSource.HDMI2;
    }

    /// <summary>
    /// 检测显示器是否在使用DisplayPort（通常是PC）
    /// </summary>
    public bool IsUsingDisplayPort(IntPtr monitorHandle)
    {
        var result = GetCurrentInputSource(monitorHandle);
        if (result == null) return false;
        var input = result.Value.Source;
        // USB-C 也是 DP 协议，视为 PC 模式
        return input == InputSource.DisplayPort1 || input == InputSource.DisplayPort2 || input == InputSource.USBC;
    }

    private InputSource MapToInputSource(int value)
    {
        // 标准DDC/CI输入源值映射
        // 注意：不同显示器可能有不同的映射
        return value switch
        {
            1 => InputSource.VGA1,
            2 => InputSource.VGA2,
            3 => InputSource.DVI1,
            4 => InputSource.DVI2,
            5 => InputSource.CompositeVideo1,
            6 => InputSource.CompositeVideo2,
            7 => InputSource.SVideo1,
            8 => InputSource.SVideo2,
            9 => InputSource.Tuner1,
            10 => InputSource.Tuner2,
            11 => InputSource.Tuner3,
            12 => InputSource.ComponentVideo1,
            13 => InputSource.ComponentVideo2,
            14 => InputSource.ComponentVideo3,
            15 => InputSource.DisplayPort1,
            16 => InputSource.DisplayPort2,
            17 => InputSource.HDMI1,
            18 => InputSource.HDMI2,
            27 => InputSource.USBC,
            _ => InputSource.Unknown
        };
    }
    /// <summary>
    /// 获取指定显示器 (MonitorInfo) 的当前 VCP 输入源 (0x60)
    /// </summary>
    public int? GetCurrentInputSource(MonitorInfo monitor)
    {
        return GetVCPValue(monitor, VCP_INPUT_SELECT);
    }

    public int? GetVCPInputCode(MonitorInfo monitor)
    {
        return GetCurrentInputSource(monitor);
    }
    
    public string GetCommonInputName(int code)
    {
        var source = MapToInputSource(code);
        return source.ToString();
    }

    /// <summary>
    /// 读取指定显示器的 VCP 值 (通过遍历所有物理显示器匹配)
    /// </summary>
    /// <summary>
    /// 读取指定显示器的 VCP 值
    /// </summary>
    public int? GetVCPValue(MonitorInfo monitor, byte vcpCode)
    {
        if (monitor.Handle == IntPtr.Zero) return null;

        int? result = null;
        uint count = 0;

        try
        {
            if (GetNumberOfPhysicalMonitorsFromHMONITOR(monitor.Handle, out count) && count > 0)
            {
                var physicalMonitors = new PHYSICAL_MONITOR[count];
                if (GetPhysicalMonitorsFromHMONITOR(monitor.Handle, count, physicalMonitors))
                {
                    // 通常一个 HMONITOR 对应一个物理显示器，但如果是镜像模式可能多个
                    // 我们只读取第一个，或者遍历尝试读取直到成功
                    foreach (var pm in physicalMonitors)
                    {
                        if (GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, vcpCode, out _, out uint currentValue, out _))
                        {
                            result = (int)currentValue;
                            break; // 读取成功即停止
                        }
                    }
                    DestroyPhysicalMonitors(count, physicalMonitors);
                }
            }
        }
        catch
        {
            // Ignore
        }
        
        return result;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);
}
