using Microsoft.Toolkit.Uwp.Notifications;

namespace Aria.Core;

/// <summary>
/// 通知服务 - 发送 Windows Toast 通知
/// </summary>
public static class NotificationService
{
    /// <summary>
    /// 发送模式切换通知
    /// </summary>
    /// <param name="mode">新的应用模式</param>
    /// <param name="modeName">模式显示名称</param>
    public static void ShowModeChangeNotification(AppMode mode, string modeName)
    {
        try
        {
            string icon = mode == AppMode.WindowsMode ? "🖥️" : "🎮";
            string title = $"{icon} 模式已切换";
            string message = $"当前模式: {modeName}";

            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .SetToastDuration(ToastDuration.Short)
                .Show();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Notification] Failed to show toast: {ex.Message}");
        }
    }

    /// <summary>
    /// 清理通知历史
    /// </summary>
    public static void ClearNotifications()
    {
        try
        {
            ToastNotificationManagerCompat.History.Clear();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// 在应用退出时取消注册 Toast
    /// </summary>
    public static void Unregister()
    {
        try
        {
            ToastNotificationManagerCompat.Uninstall();
        }
        catch
        {
            // Ignore uninstall errors
        }
    }
}
