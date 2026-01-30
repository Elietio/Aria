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
    public void GetDialogue_WindowsMode_ReturnsValidItem()
    {
        // Act
        var item = _dialogueManager.GetDialogue(isPS5Mode: false);

        // Assert
        Assert.NotNull(item);
        Assert.False(string.IsNullOrEmpty(item.WinText));
        Assert.False(string.IsNullOrEmpty(item.WinVoice));
    }

    [Fact]
    public void GetDialogue_PS5Mode_ReturnsValidItem()
    {
        // Act
        var item = _dialogueManager.GetDialogue(isPS5Mode: true);

        // Assert
        Assert.NotNull(item);
        Assert.False(string.IsNullOrEmpty(item.PS5Text));
        Assert.False(string.IsNullOrEmpty(item.PS5Voice));
    }

    [Fact]
    public void GetDialogue_ShuffleBag_ReducesRepetition()
    {
        // Act - Call multiple times
        // Since we have a small pool (~3-4 per slot), calling 10 times should trigger refill.
        // We just verify it always returns something valid.
        
        var distinctIds = new HashSet<string>();
        for (int i = 0; i < 20; i++)
        {
            var item = _dialogueManager.GetDialogue(isPS5Mode: false);
            Assert.NotNull(item);
            distinctIds.Add(item.Id);
        }
        
        // Assert we got multiple distinct items (at least 2 for any slot)
        Assert.True(distinctIds.Count >= 2);
    }

    [Fact]
    public void PlayVoice_DoesNotThrow_WhenVoiceDisabled()
    {
        // Arrange
        _config.EnableMoeVoice = false;
        var item = new DialogueItem { WinVoice = "test.mp3" };

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => _dialogueManager.PlayVoice(false, item));
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
