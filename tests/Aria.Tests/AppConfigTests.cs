using Aria.Core;

namespace Aria.Tests;

/// <summary>
/// AppConfig 单元测试
/// </summary>
public class AppConfigTests
{
    [Fact]
    public void DefaultConfig_HasValidModeNames()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        Assert.NotNull(config.ModeA);
        Assert.NotNull(config.ModeB);
        Assert.False(string.IsNullOrEmpty(config.ModeA.Name));
        Assert.False(string.IsNullOrEmpty(config.ModeB.Name));
    }

    [Fact]
    public void DefaultConfig_ModesHaveDistinctNames()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        Assert.NotEqual(config.ModeA.Name, config.ModeB.Name);
    }

    [Fact]
    public void DefaultConfig_HasDefaultHotkeys()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        Assert.NotEqual(0u, config.SwitchModeKey);
        Assert.NotEqual(0u, config.WindowOverviewKey);
    }

    [Fact]
    public void DefaultConfig_GlassOpacity_InValidRange()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        Assert.InRange(config.GlassOpacity, 0.0, 1.0);
        Assert.InRange(config.CleanOpacity, 0.0, 1.0);
    }

    [Fact]
    public void UIStyle_Enum_HasExpectedValues()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(AppConfig.UIStyle), AppConfig.UIStyle.Classic));
        Assert.True(Enum.IsDefined(typeof(AppConfig.UIStyle), AppConfig.UIStyle.MoeGlass));
        Assert.True(Enum.IsDefined(typeof(AppConfig.UIStyle), AppConfig.UIStyle.MoeClean));
    }

    [Fact]
    public void DefaultConfig_Theme_IsClassic()
    {
        // Arrange & Act
        var config = new AppConfig();

        // Assert
        Assert.Equal(AppConfig.UIStyle.Classic, config.Theme);
    }
}
