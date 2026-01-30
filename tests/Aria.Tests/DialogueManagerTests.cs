using Aria.App.Services;
using Aria.Core;

namespace Aria.Tests;

/// <summary>
/// DialogueManager 单元测试
/// </summary>
public class DialogueManagerTests
{
    private readonly DialogueManager _dialogueManager;
    private readonly AppConfig _config;

    public DialogueManagerTests()
    {
        _config = new AppConfig { EnableMoeVoice = false };
        _dialogueManager = new DialogueManager(_config);
    }

    [Fact]
    public void GetRandomDialogue_WindowsMode_ReturnsNonEmptyText()
    {
        // Act
        var (text, index) = _dialogueManager.GetRandomDialogue(isPS5Mode: false);

        // Assert
        Assert.False(string.IsNullOrEmpty(text));
        Assert.InRange(index, 0, 100); // Reasonable index range
    }

    [Fact]
    public void GetRandomDialogue_PS5Mode_ReturnsNonEmptyText()
    {
        // Act
        var (text, index) = _dialogueManager.GetRandomDialogue(isPS5Mode: true);

        // Assert
        Assert.False(string.IsNullOrEmpty(text));
        Assert.InRange(index, 0, 100);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetRandomDialogue_ReturnsValidIndex(bool isPS5Mode)
    {
        // Act - Call multiple times to test randomness
        for (int i = 0; i < 10; i++)
        {
            var (text, index) = _dialogueManager.GetRandomDialogue(isPS5Mode);
            Assert.True(index >= 0);
            Assert.False(string.IsNullOrEmpty(text));
        }
    }

    [Fact]
    public void PlayDialogueVoice_DoesNotThrow_WhenVoiceDisabled()
    {
        // Arrange
        _config.EnableMoeVoice = false;

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => _dialogueManager.PlayDialogueVoice(false, 0));
        Assert.Null(exception);
    }

    [Fact]
    public void PlayModeSwitchVoice_DoesNotThrow_WhenVoiceDisabled()
    {
        // Arrange
        _config.EnableMoeVoice = false;

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => _dialogueManager.PlayModeSwitchVoice(isPS5Mode: true));
        Assert.Null(exception);
    }
}
