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
    
    // State Tracking
    private string _currentBaseImageWin = "win_day.png";
    private string _currentBaseImagePS5 = "ps5_day.png";
    
    // Reaction Control
    private CancellationTokenSource? _reactionCts;
    private bool _isReacting => _reactionCts != null; // Derived property

    public MascotManager(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Update visuals based on current state (Base or Reaction)
    /// </summary>
    public void UpdateVisuals(Image mascotImage, Border ambientGlow, GradientStop? glowGradientStop, bool isPS5Mode, double? opacityOverride = null)
    {
        bool isMoe = _config.Theme == AppConfig.UIStyle.MoeGlass || _config.Theme == AppConfig.UIStyle.MoeClean;
        
        if (!isMoe || !_config.EnableMoeMascot)
        {
            if (ambientGlow != null) ambientGlow.Visibility = Visibility.Collapsed;
            if (mascotImage != null) mascotImage.Visibility = Visibility.Collapsed;
            return;
        }

        if (ambientGlow != null) ambientGlow.Visibility = Visibility.Visible;
        if (mascotImage != null)
        {
            mascotImage.Visibility = Visibility.Visible;
            
            // 1. Calculate Base State if not reacting
            // If reacting, do NOT touch the image source!
            if (!_isReacting)
            {
                UpdateBaseState();
                var imgName = isPS5Mode ? _currentBaseImagePS5 : _currentBaseImageWin;
                SetMascotImage(mascotImage, imgName, opacityOverride);
            }
        }

        // 2. Animate Ambient Glow Color
        if (glowGradientStop != null)
        {
            AnimateGlowColor(glowGradientStop, isPS5Mode);
        }
    }

    /// <summary>
    /// Trigger a transient reaction image
    /// </summary>
    public async Task TriggerReactionAsync(Image mascotImage, string reactionImgWin, string reactionImgPS5, bool isPS5Mode, int durationMs = 3000, double? opacityOverride = null)
    {
        if (mascotImage == null) return;
        
        // Cancel previous reaction
        _reactionCts?.Cancel();
        _reactionCts = new CancellationTokenSource();
        var token = _reactionCts.Token;
        
        try 
        {
            // Show Reaction
            var imgName = isPS5Mode ? reactionImgPS5 : reactionImgWin;
            SetMascotImage(mascotImage, imgName, opacityOverride);
            
            // Wait
            await Task.Delay(durationMs, token);
            
            // If cancelled, do nothing (new reaction took over)
        }
        catch (TaskCanceledException)
        {
            return;
        }
        finally
        {
            // Only revert if WE are still the active reaction (token wasn't cancelled)
            // But wait, if cancelled, the new reaction has already set the image.
            // If we are here and NOT cancelled, we need to revert.
            if (!token.IsCancellationRequested)
            {
                // Revert
                _reactionCts = null; // Clear active flag
                
                // Refresh base state to ensure we revert to the correct time/season context
                UpdateBaseState(); 
                var baseImg = isPS5Mode ? _currentBaseImagePS5 : _currentBaseImageWin;
                SetMascotImage(mascotImage, baseImg, opacityOverride);
            }
        }
    }

    private void SetMascotImage(Image img, string imageName, double? opacityOverride)
    {
        try
        {
            // Avoid reloading if same source? 
            // WPF Image.Source comparison is tricky.
            // Let's just set it for now.
            var uri = new Uri($"pack://application:,,,/Aria.App;component/Assets/Moe/Variations/{imageName}");
            var bitmap = new BitmapImage(uri);
            img.Source = bitmap;
            img.Opacity = opacityOverride ?? _config.MascotOpacity;
        }
        catch (Exception)
        {
            // Fail silently
        }
    }
    
    // ... (Remainder of file is same)

    private void UpdateBaseState()
    {
        var now = DateTime.Now;
        int month = now.Month;
        int hour = now.Hour;

        // Priority 1: Time (Sleepy / Late Night) overrides everything
        // 00:00 - 06:00 -> Sleepy
        if (hour >= 0 && hour < 6)
        {
            _currentBaseImageWin = "win_sleepy.png";
            _currentBaseImagePS5 = "ps5_tired.png";
            return;
        }
        
        // Priority 2: Time (Night) -> 21:00 - 24:00
        if (hour >= 21)
        {
            _currentBaseImageWin = "win_night.png";
            _currentBaseImagePS5 = "ps5_night.png";
            return;
        }
        
        // From here on (06:00 - 21:00), we are in "Daytime" logic.
        // Base is Day, but can be overridden by Holiday or Season.
        
        string winBase = "win_day.png";
        string ps5Base = "ps5_day.png";

        // Logic: Holiday > Season > Day
        
        // Check Holiday
        if (IsHoliday(now, out string hWin, out string hPS5))
        {
            winBase = hWin;
            ps5Base = hPS5;
        }
        else
        {
            // Check Season
            GetSeasonalBase(month, out string sWin, out string sPS5);
            winBase = sWin;
            ps5Base = sPS5;
        }

        // Apply Time-of-Day Overrides on top of "Day" base?
        // Plan says:
        // Morning (06-09): Morning (Plan table says "Switch and Persist")
        // But logic says: Season/Holiday is Day Skin.
        // Wait, "Morning" has specific image? Plan table: Morning -> win_day / ps5_day.
        // So Morning actually visually IS Day.
        // So we are fine.
        
        _currentBaseImageWin = winBase;
        _currentBaseImagePS5 = ps5Base;
    }

    private void GetSeasonalBase(int month, out string win, out string ps5)
    {
        // Default
        win = "win_day.png";
        ps5 = "ps5_day.png";

        // Spring: 3,4,5
        if (month >= 3 && month <= 5)
        {
            win = "win_spring.png";
            ps5 = "ps5_spring.png";
        }
        // Summer: 6,7,8
        else if (month >= 6 && month <= 8)
        {
            win = "win_summer.png";
            ps5 = "ps5_summer.png";
        }
        // Autumn: 9,10,11
        else if (month >= 9 && month <= 11)
        {
            win = "win_autumn.png";
            ps5 = "ps5_autumn.png";
        }
        // Winter: 12,1,2
        else if (month == 12 || month == 1 || month == 2)
        {
            win = "win_winter.png";
            ps5 = "ps5_winter.png";
        }
    }

    private bool IsHoliday(DateTime date, out string win, out string ps5)
    {
        win = "";
        ps5 = "";
        
        // New Year (1.1)
        if (date.Month == 1 && date.Day == 1)
        {
            win = "win_new_year.png";
            ps5 = "ps5_new_year.png";
            return true;
        }
        
        // Christmas (12.25)
        if (date.Month == 12 && date.Day == 25)
        {
             win = "win_christmas.png";
             ps5 = "ps5_christmas.png";
             return true;
        }

        // National Day (10.1)
        if (date.Month == 10 && date.Day == 1)
        {
            win = "win_national_day.png";
            ps5 = "ps5_national_day.png";
            return true;
        }
        
        // Labor Day (5.1)
        if (date.Month == 5 && date.Day == 1)
        {
            win = "win_labor_day.png";
            ps5 = "ps5_labor_day.png";
            return true;
        }

        // TODO: Lunar Holidays (CNY, MidAutumn) require Lunar Calendar lib.
        // Skipping for V1.
        
        return false;
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
    /// 加载模式图标
    /// </summary>
    public BitmapImage? LoadModeImage(bool isPS5Mode)
    {
        string imagePath = isPS5Mode ? "Assets/Moe/mode_ps5.png" : "Assets/Moe/mode_windows.png";
        
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
