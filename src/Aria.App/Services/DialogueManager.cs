using System.IO;
using System.Windows.Media;
using Aria.Core;

namespace Aria.App.Services;

/// <summary>
/// 对话管理器 - 管理看板娘对话文本、语音播放、时段逻辑
/// </summary>
public class DialogueManager
{
    private readonly MediaPlayer _voicePlayer;
    private readonly AppConfig _config;
    private readonly Random _random = new();

    // Windows 模式对话
    private static readonly string[] WindowsDialogues =
    {
        "工作中请勿打扰哦... (认真)",
        "主人的效率真高呢！",
        "记得适时休息一下眼睛~",
        "Aria 正在监控一切 ( •̀ ω •́ )✧",
        "有什么指令吗？",
        "累了吗？要注意劳逸结合哦。",
        "放心交给我，绝不出错。",
        "看到你这么努力，我也更有干劲了！",
        "喝杯水吧，补充水分很重要。",
        "今天的日程安排得如何了？",
        "记得随手保存哦！数据丢失很可怕的。",
        "坐姿端正了吗？不要弯腰驼背哦。",
        "虽然很想聊天，但工作优先！",
        "加油加油！你是最棒的！",
        "如果累了，闭目养神五分钟吧。"
    };

    // PS5 模式对话
    private static readonly string[] PS5Dialogues =
    {
        "好耶！打游戏时间到！(≧∇≦)ﾉ",
        "这关怎么过呀... 帮帮我~",
        "摸鱼万岁！",
        "手柄电量还够吗？",
        "冲鸭！拿下这局！",
        "别，别过来！救命呀！",
        "哼哼，我可是很强的！",
        "下一款游戏玩什么呢？",
        "快看快看！这个连招帅不帅！",
        "再玩最后一局... 就一局！",
        "有点饿了... 有零食吗？",
        "这就是'白金奖杯'的含金量！",
        "玄学时刻！这次一定能出货！",
        "呜呜... 被队友坑了...",
        "熬夜打游戏虽然爽，但也要注意身体呀！"
    };

    public DialogueManager(AppConfig config)
    {
        _config = config;
        _voicePlayer = new MediaPlayer();
    }

    /// <summary>
    /// 获取随机对话
    /// </summary>
    public (string Text, int Index) GetRandomDialogue(bool isPS5Mode)
    {
        var list = isPS5Mode ? PS5Dialogues : WindowsDialogues;
        int index = _random.Next(list.Length);
        return (list[index], index);
    }

    /// <summary>
    /// 获取基于时段的对话 (未来扩展)
    /// </summary>
    public string GetTimeBasedDialogue(bool isPS5Mode)
    {
        var hour = DateTime.Now.Hour;
        string prefix = isPS5Mode ? "ps5" : "win";
        
        // 早晨 (6-11)
        if (hour >= 6 && hour < 12)
        {
            PlayVoiceFile($"{prefix}_morning_01.mp3");
            return isPS5Mode ? "早上好！今天也要开心地玩游戏呀~" : "早上好！新的一天，加油工作吧！";
        }
        // 中午 (12-14)
        else if (hour >= 12 && hour < 14)
        {
            PlayVoiceFile($"{prefix}_noon_01.mp3");
            return isPS5Mode ? "午饭时间！吃饱了才有力气打游戏~" : "午饭时间到！别忘了休息一下哦。";
        }
        // 下午 (14-18)
        else if (hour >= 14 && hour < 18)
        {
            PlayVoiceFile($"{prefix}_afternoon_01.mp3");
            return isPS5Mode ? "下午了，来一把休闲小游戏？" : "下午茶时间！喝杯咖啡提提神？";
        }
        // 晚上 (18-23)
        else if (hour >= 18 && hour < 23)
        {
            PlayVoiceFile($"{prefix}_evening_01.mp3");
            return isPS5Mode ? "晚上是游戏的黄金时段！" : "晚上了，注意不要太晚休息哦~";
        }
        // 深夜 (23-6)
        else
        {
            return isPS5Mode ? "这么晚了还在打游戏...注意身体呀！" : "夜深了，该休息了吧？";
        }
    }

    /// <summary>
    /// 播放语音文件
    /// </summary>
    public void PlayVoiceFile(string filename)
    {
        if (!_config.EnableMoeVoice) return;

        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Moe/Voice", filename);

            if (File.Exists(path))
            {
                _voicePlayer.Open(new Uri(path));
                _voicePlayer.Volume = 1.0;
                _voicePlayer.Play();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DialogueManager] Voice file not found: {path}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DialogueManager] Voice Play Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 播放模式切换语音
    /// </summary>
    public void PlayModeSwitchVoice(bool isPS5Mode)
    {
        string filename = isPS5Mode ? "switch_to_ps5.mp3" : "switch_to_win.mp3";
        PlayVoiceFile(filename);
    }

    /// <summary>
    /// 播放随机对话语音
    /// </summary>
    public void PlayDialogueVoice(bool isPS5Mode, int index)
    {
        string prefix = isPS5Mode ? "ps5" : "win";
        PlayVoiceFile($"{prefix}_{index}.mp3");
    }
}
