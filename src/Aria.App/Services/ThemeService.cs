using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Wpf.Ui.Controls;
using Aria.Core;

namespace Aria.App.Services;

/// <summary>
/// 主题服务 - 管理 UI 风格切换、强调色注入、卡片样式
/// </summary>
public class ThemeService
{
    // PS5 粉色 / Windows 蓝色
    public static readonly Color PS5AccentColor = Color.FromRgb(0xF4, 0x8F, 0xB1);
    public static readonly Color WindowsAccentColor = Color.FromRgb(0x21, 0x96, 0xF3);
    
    public static readonly Color PS5GlowColor = Color.FromRgb(255, 105, 180); // HotPink
    public static readonly Color WindowsGlowColor = Color.FromRgb(0, 191, 255); // DeepSkyBlue

    /// <summary>
    /// 应用强调色到全局资源
    /// </summary>
    public void ApplyAccentColor(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var brushKeys = new[]
        {
            "SystemAccentBrush", "SystemAccentBrushPrimary", "SystemAccentBrushSecondary", "SystemAccentBrushTertiary",
            "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush",
            "AccentTextFillColorPrimaryBrush", "AccentTextFillColorSecondaryBrush", "AccentTextFillColorTertiaryBrush",
            "ControlFillColorDefaultBrush"
        };

        var colorKeys = new List<string>
        {
            "SystemAccentColor", "SystemAccentColorPrimary", "SystemAccentColorSecondary", "SystemAccentColorTertiary"
        };
        
        foreach (var k in brushKeys)
        {
            if (k.EndsWith("Brush")) colorKeys.Add(k.Substring(0, k.Length - 5));
        }

        void InjectResources(ResourceDictionary target)
        {
            foreach (var key in colorKeys) target[key] = color;
            foreach (var key in brushKeys) target[key] = brush;
        }

        // Apply to Application Resources
        InjectResources(Application.Current.Resources);

        // Apply to all Windows
        foreach (Window win in Application.Current.Windows)
        {
            InjectResources(win.Resources);
            if (win.Content is UIElement ui) ui.InvalidateVisual();
        }

        System.Diagnostics.Debug.WriteLine($"[ThemeService] Applied Accent Color: {color}");
    }

    /// <summary>
    /// 根据模式应用强调色
    /// </summary>
    public void ApplyAccentForMode(bool isPS5Mode)
    {
        ApplyAccentColor(isPS5Mode ? PS5AccentColor : WindowsAccentColor);
    }

    /// <summary>
    /// 获取氛围光颜色
    /// </summary>
    public Color GetGlowColor(bool isPS5Mode)
    {
        return isPS5Mode ? PS5GlowColor : WindowsGlowColor;
    }

    /// <summary>
    /// 应用卡片样式 (Glass / Clean / Classic)
    /// </summary>
    public void ApplyCardStyles(IEnumerable<Card?> cards, AppConfig config)
    {
        var style = config.Theme;
        
        foreach (var card in cards)
        {
            if (card == null) continue;

            if (style == AppConfig.UIStyle.Classic)
            {
                // 恢复默认
                card.ClearValue(Card.BackgroundProperty);
                card.ClearValue(Card.BorderBrushProperty);
                card.ClearValue(Card.BorderThicknessProperty);
                card.ClearValue(Card.EffectProperty);
            }
            else if (style == AppConfig.UIStyle.MoeGlass)
            {
                ApplyGlassStyle(card, config);
            }
            else if (style == AppConfig.UIStyle.MoeClean)
            {
                ApplyCleanStyle(card, config);
            }
        }
    }

    private void ApplyGlassStyle(Card card, AppConfig config)
    {
        var baseColor = config.EnableMoeMascot
            ? Color.FromRgb(0, 0, 0)       // 纯黑
            : Color.FromRgb(255, 255, 255); // 纯白

        double effectiveOpacity = config.GlassOpacity;
        if (config.EnableMoeMascot && effectiveOpacity < 0.2) effectiveOpacity = 0.2;

        byte alpha = (byte)(Math.Max(0.01, Math.Min(1.0, effectiveOpacity)) * 255);

        var glassBrush = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        var borderBrush = new SolidColorBrush(Color.FromArgb((byte)(alpha * 3), 255, 255, 255));

        card.Background = glassBrush;
        card.BorderBrush = borderBrush;
        card.BorderThickness = new Thickness(1);

        card.Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            Opacity = 0.25,
            BlurRadius = 20,
            ShadowDepth = 6,
            Direction = 270
        };
    }

    private void ApplyCleanStyle(Card card, AppConfig config)
    {
        var baseColor = config.EnableMoeMascot
            ? Color.FromRgb(0, 0, 0)
            : Color.FromRgb(255, 255, 255);

        byte alpha = (byte)(Math.Max(0.01, Math.Min(1.0, config.CleanOpacity)) * 255);

        var cleanBrush = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));

        card.Background = cleanBrush;
        card.BorderBrush = Brushes.Transparent;
        card.BorderThickness = new Thickness(0);
        card.ClearValue(Card.EffectProperty);
    }
}
