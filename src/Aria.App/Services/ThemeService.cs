using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;
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
    /// switch theme
    /// </summary>
    public void SwitchTheme(bool isPS5Mode)
    {
        try
        {
            string dictName = isPS5Mode ? "Themes/PS5.xaml" : "Themes/Windows.xaml";
            // Use absolute Pack URI to ensure correct resource loading
            var dictUri = new Uri($"pack://application:,,,/Aria.App;component/{dictName}", UriKind.Absolute);

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            
                // Verify dictionary contents for debugging
            var debugInfo = "Loaded Dictionaries:\n";
            foreach (var d in dictionaries)
            {
                debugInfo += $"- Source: {d.Source}\n";
            }
            // System.Windows.MessageBox.Show(debugInfo); // Comment out after debugging

            // Find ALL existing theme dictionaries to ensure clean cleanup
            var existingThemes = new List<ResourceDictionary>();
            foreach (var d in dictionaries)
            {
                if (d.Source != null && (d.Source.OriginalString.Contains("Themes/Windows.xaml") || d.Source.OriginalString.Contains("Themes/PS5.xaml")))
                {
                    existingThemes.Add(d);
                }
            }

            // Remove old themes
            foreach (var oldTheme in existingThemes)
            {
                dictionaries.Remove(oldTheme);
            }

            // Create and add new dictionary
            var newTheme = new ResourceDictionary { Source = dictUri };
            dictionaries.Add(newTheme);
            
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Switched theme to: {dictName}");

            // Wpf.Ui (4.x) specific: Update library controls
            var accentColor = isPS5Mode ? PS5AccentColor : WindowsAccentColor;
            ApplicationAccentColorManager.Apply(accentColor);
        }
        catch (Exception ex)
        {
            // Log fallback or error
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Error switching theme: {ex.Message}");
            System.Windows.MessageBox.Show($"主题切换出错: {ex.Message}", "Theme Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 获取氛围光颜色
    /// </summary>
    public Color GetGlowColor(bool isPS5Mode)
    {
         // Keep for compatibility if used by other components, though now handled by ResourceDictionary mostly.
         // Or strictly return color for non-UI logic (like led controller if any)
         // For now, return hardcoded values matching the XAML
         return isPS5Mode ? Color.FromRgb(255, 105, 180) : Color.FromRgb(0, 191, 255);
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
