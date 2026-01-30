using System.Runtime.InteropServices;
using H.NotifyIcon;
using Aria.Core;

namespace Aria.App.Services;

/// <summary>
/// 托盘图标助手 - 管理托盘图标更新
/// </summary>
public class TrayIconHelper
{
    private readonly AppConfig _config;
    private IntPtr _lastIconHandle = IntPtr.Zero;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public TrayIconHelper(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 更新托盘图标
    /// </summary>
    public void UpdateIcon(TaskbarIcon trayIcon, AppMode mode)
    {
        try
        {
            string subPath = GetIconPath(mode);
            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, subPath);

            if (System.IO.File.Exists(fullPath))
            {
                var icon = new System.Drawing.Icon(fullPath);
                trayIcon.Icon = icon;

                // 清理之前的 handle
                if (_lastIconHandle != IntPtr.Zero)
                {
                    DestroyIcon(_lastIconHandle);
                    _lastIconHandle = IntPtr.Zero;
                }

                // 更新 Tooltip
                string suffix = IsMoeTheme() ? " (Moe)" : "";
                trayIcon.ToolTipText = mode == AppMode.PS5Mode
                    ? $"Aria - {_config.ModeB.Name}{suffix}"
                    : $"Aria - {_config.ModeA.Name}{suffix}";
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[TrayIconHelper] Icon file not found: {fullPath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrayIconHelper] Failed to update icon: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取图标路径
    /// </summary>
    private string GetIconPath(AppMode mode)
    {
        if (IsMoeTheme())
        {
            return mode == AppMode.PS5Mode ? "Assets/Moe/tray_ps5.ico" : "Assets/Moe/tray_windows.ico";
        }
        else
        {
            return mode == AppMode.PS5Mode ? "Assets/tray_ps5.ico" : "Assets/tray_windows.ico";
        }
    }

    private bool IsMoeTheme()
    {
        return _config.Theme == AppConfig.UIStyle.MoeGlass || _config.Theme == AppConfig.UIStyle.MoeClean;
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Dispose()
    {
        if (_lastIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_lastIconHandle);
            _lastIconHandle = IntPtr.Zero;
        }
    }
}
