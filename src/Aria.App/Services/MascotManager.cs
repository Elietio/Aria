using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Aria.Core;

namespace Aria.App.Services;

/// <summary>
/// 看板娘管理器 - 管理立绘切换、氛围光效果、节日/季节差分
/// </summary>
public class MascotManager
{
    private readonly AppConfig _config;

    public MascotManager(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 更新看板娘视觉效果
    /// </summary>
    public void UpdateVisuals(Image mascotImage, Border ambientGlow, GradientStop? glowGradientStop, bool isPS5Mode)
    {
        bool isMoe = _config.Theme == AppConfig.UIStyle.MoeGlass || _config.Theme == AppConfig.UIStyle.MoeClean;
        
        if (!isMoe || !_config.EnableMoeMascot)
        {
            ambientGlow.Visibility = Visibility.Collapsed;
            mascotImage.Visibility = Visibility.Collapsed;
            return;
        }

        ambientGlow.Visibility = Visibility.Visible;
        mascotImage.Visibility = Visibility.Visible;

        // 1. 切换立绘
        UpdateMascotImage(mascotImage, isPS5Mode);

        // 2. 动画更新氛围光颜色
        if (glowGradientStop != null)
        {
            AnimateGlowColor(glowGradientStop, isPS5Mode);
        }
    }

    /// <summary>
    /// 更新立绘图片
    /// </summary>
    public void UpdateMascotImage(Image mascotImage, bool isPS5Mode)
    {
        string imagePath = isPS5Mode ? "Assets/Moe/standee_ps5.png" : "Assets/Moe/standee_windows.png";
        
        try
        {
            var uri = new Uri($"pack://application:,,,/Aria.App;component/{imagePath}");
            var bitmap = new BitmapImage(uri);
            mascotImage.Source = bitmap;
            mascotImage.Opacity = _config.MascotOpacity;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MascotManager] Failed to load mascot image: {ex.Message}");
        }
    }

    /// <summary>
    /// 动画更新氛围光颜色
    /// </summary>
    public void AnimateGlowColor(GradientStop glowGradientStop, bool isPS5Mode)
    {
        var targetColor = isPS5Mode
            ? Color.FromRgb(255, 105, 180) // HotPink
            : Color.FromRgb(0, 191, 255);  // DeepSkyBlue

        var anim = new ColorAnimation
        {
            To = targetColor,
            Duration = TimeSpan.FromSeconds(0.6),
            EasingFunction = new QuadraticEase()
        };

        glowGradientStop.BeginAnimation(GradientStop.ColorProperty, anim);
    }

    /// <summary>
    /// 获取模式图标路径
    /// </summary>
    public string GetModeImagePath(bool isPS5Mode)
    {
        return isPS5Mode ? "Assets/Moe/mode_ps5.png" : "Assets/Moe/mode_windows.png";
    }

    /// <summary>
    /// 加载模式图标
    /// </summary>
    public BitmapImage? LoadModeImage(bool isPS5Mode)
    {
        string imagePath = GetModeImagePath(isPS5Mode);
        
        try
        {
            var uri = new Uri($"pack://application:,,,/Aria.App;component/{imagePath}");
            return new BitmapImage(uri);
        }
        catch
        {
            return null;
        }
    }
}
