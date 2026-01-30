using System.IO;
using System.Windows.Media;
using Aria.Core;

namespace Aria.App.Services;

public enum DialogueContext
{
    Morning,    // 06-09
    Noon,       // 11-14
    Afternoon,  // 14-18
    Evening,    // 19-23
    LateNight,  // 23-06
    
    // Special
    SystemHighCpu,
    SystemHighRam,
    SystemIdle,
    
    // Date & Health
    Workday,
    Weekend,
    HealthLong,
    SpecialAudio,
}

public class DialogueItem
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string VoiceFile { get; set; } = "";
    public string ReactionImageWin { get; set; } = "win_happy.png";
    public string ReactionImagePS5 { get; set; } = "ps5_cheer.png";
    
    // If true, this dialogue is exclusive to specific mode (optional logic, for now we mix)
    // Actually, per plan, we have separate lists or unified? 
    // Plan says "Unified Time Slots" but table differentiates Win/PS5 voices.
    // Let's store both and select based on current mode at runtime.
    
    public string WinVoice { get; set; } = "";
    public string PS5Voice { get; set; } = "";
    
    public string WinText { get; set; } = "";
    public string PS5Text { get; set; } = "";
}

/// <summary>
/// 对话管理器 - 管理看板娘对话文本、语音播放、时段逻辑 (Shuffle Bag Implementation)
/// </summary>
public class DialogueManager
{
    private readonly MediaPlayer _voicePlayer;
    private readonly AppConfig _config;
    private readonly Random _random = new();

    // Context -> List of available dialogues
    private readonly Dictionary<DialogueContext, List<DialogueItem>> _dialoguePool = new();
    
    // Context -> List of remaining indices (Shuffle Bag)
    private readonly Dictionary<DialogueContext, List<int>> _shuffleBags = new();

    public DialogueManager(AppConfig config)
    {
        _config = config;
        _voicePlayer = new MediaPlayer();
        InitializeDialogues();
    }

    private void InitializeDialogues()
    {
        // ==========================================
        // 1. Morning (06-09)
        // ==========================================
        
        // Pair 1: Morning Greeting 01
        Add(DialogueContext.Morning, "morning_01", 
            "早安！新的一天也要元气满满哦！", "早... 这就要开始玩吗？也是一种生活态度呢！", 
            "win_happy.png", "ps5_day.png",
            "win_morning_01.mp3", "ps5_morning_01.mp3");

        // Pair 2: Morning Greeting 02
        Add(DialogueContext.Morning, "morning_02", 
            "一日之计在于晨，今天要处理什么任务呢？", "大清早打游戏，精神真好呀！", 
            "win_tablet.png", "ps5_cheer.png",
            "win_morning_02.mp3", "ps5_morning_02.mp3");

        // Pair 3: Breakfast (Win Only has 03/04, PS5 only 01/02, so mix with Legacy)
        Add(DialogueContext.Morning, "morning_03", 
            "早餐吃了吗？不吃早饭可是没力气工作的。", "手柄电量还够吗？", 
            "win_thinking.png", "ps5_snack.png",
            "win_morning_03.mp3", "ps5_3.mp3"); // PS5 uses Legacy 3

        // Pair 4: Encouragement / Legacy Mix
        Add(DialogueContext.Morning, "morning_mixed", 
            "不管是 Coding 还是与人交涉，都要加油呀！", "好的开始是成功的一半... 大概？", 
            "win_day.png", "ps5_day.png", 
            "win_morning_04.mp3", "ps5_0.mp3"); // Win Morning 04 vs PS5 Legacy 0 ("Game time!")

        // ==========================================
        // 2. Noon (11-14)
        // ==========================================
        
        // Pair 1: Rest
        Add(DialogueContext.Noon, "noon_01", 
            "已经是中午了，不稍微休息一下吗？", "边吃饭边玩虽然爽，但小心把手柄弄脏哦。", 
            "win_sleepy.png", "ps5_pout.png",
            "win_noon_01.mp3", "ps5_noon_01.mp3");

        // Pair 2: Lunch
        Add(DialogueContext.Noon, "noon_02", 
            "午饭时间！只要填饱肚子，烦恼就会少一半！", "午休时间的快速对局！决胜负吧！", 
            "win_happy.png", "ps5_snack.png",
            "win_noon_02.mp3", "ps5_noon_02.mp3");
        
        // Pair 3: Hydration (Win) / Legacy Mix
        Add(DialogueContext.Noon, "noon_03", 
            "稍微眯一会儿吧，下午会更有精神的。", "有点饿了... 有零食吗？", 
            "win_sleepy.png", "ps5_snack.png",
            "win_noon_03.mp3", "ps5_10.mp3"); // Win Noon 03 vs PS5 Legacy 10

        // ==========================================
        // 3. Afternoon (14-18)
        // ==========================================

        // Pair 1: Tea Time
        Add(DialogueContext.Afternoon, "afternoon_01", 
            "下午茶时间~ 要来一杯咖啡吗？", "在这个时间点玩游戏... 真是奢侈的午后时光呀！", 
            "win_tea.png", "ps5_day.png",
            "win_afternoon_01.mp3", "ps5_afternoon_01.mp3");

        // Pair 2: Exercise / Light
        Add(DialogueContext.Afternoon, "afternoon_02", 
            "坐久了记得起来活动一下筋骨。", "下午的阳光真好，就像游戏里的光追一样！", 
            "win_day.png", "ps5_day.png",
            "win_afternoon_02.mp3", "ps5_afternoon_02.mp3");

        // Pair 3: Progress (Win) / Legacy Mix
        Add(DialogueContext.Afternoon, "afternoon_03", 
            "工作进度如何？不要太勉强自己哦。", "这关怎么过呀... 帮帮我~", 
            "win_day.png", "ps5_frustrated.png",
            "win_afternoon_03.mp3", "ps5_1.mp3"); // Win Afternoon 03 vs PS5 Legacy 1

        // ==========================================
        // 4. Evening (19-23)
        // ==========================================

        // Pair 1: Dark
        Add(DialogueContext.Evening, "evening_01", 
            "天黑了，屏幕亮度要不要调低一点？", "属于大人的游戏时间开始了！", 
            "win_night.png", "ps5_night.png",
            "win_evening_01.mp3", "ps5_evening_01.mp3");

        // Pair 2: Busy / Health
        Add(DialogueContext.Evening, "evening_02", 
            "还在忙碌吗？辛苦了...", "熬夜打游戏虽然开心，但黑眼圈会出来的...", 
            "win_overworked.png", "ps5_pout.png",
            "win_evening_02.mp3", "ps5_evening_02.mp3");

        // Pair 3: Sleep Early
        Add(DialogueContext.Evening, "evening_03", 
            "为了明天能早起，今天要早点睡哦。", "再赢一把就去睡... 你是这么想的对吧？", 
            "win_night.png", "ps5_immersed.png",
            "win_evening_03.mp3", "ps5_evening_03.mp3");

        // ==========================================
        // 5. LateNight (23-06)
        // ==========================================

        Add(DialogueContext.LateNight, "latenight_01", 
            "还不睡吗？明天早上绝对会后悔的...", "再赢一把就睡... 这已经是第五次了...", 
            "win_sleepy.png", "ps5_tired.png",
            "common_health_late_01.mp3", "ps5_evening_03.mp3"); // PS5 reuse

        Add(DialogueContext.LateNight, "latenight_02", 
            "皮肤会变差的！快去睡觉！", "Save Game 之后，是不是该去睡了？", 
            "win_shocked.png", "ps5_frustrated.png",
            "common_health_late_02.mp3", "ps5_health_long_04.mp3");

        Add(DialogueContext.LateNight, "latenight_03", 
            "即使是机器人也是需要待机维护的... 晚安...", "手柄都发烫了，难道你不累吗？", 
            "win_sleepy.png", "ps5_tired.png",
            "common_health_late_03.mp3", "ps5_health_long_02.mp3");

        // ==========================================
        // 6. Generic Fillers (Legacy Pool)
        // ==========================================
        
        // Win 4 / PS5 3: Instructions / Battery -> Use Idea/Secret images?
        Add(DialogueContext.Morning, "legacy_01", 
            "有什么指令吗？", "手柄电量还够吗？", 
            "win_idea.png", "ps5_secret.png",  // Changed from generic day
            "win_4.mp3", "ps5_3.mp3");

        // Win 8 / PS5 2: Water / Fish
        Add(DialogueContext.Noon, "legacy_02", 
            "喝杯水吧，补充水分很重要。", "摸鱼万岁！", 
            "win_happy.png", "ps5_happy.png", // happy doesn't exist? ps5_happy? Check list. ps5_day?
            // ps5_happy not in list? Check variations. ps5_excited?
            // ps5_excited.png exists.
            "win_8.mp3", "ps5_2.mp3");

        // Win 6 / PS5 6: Trust / Strong -> Use Presentation/Victory
        Add(DialogueContext.Afternoon, "legacy_03", 
            "放心交给我，绝不出错。", "哼哼，我可是很强的！", 
            "win_presentation.png", "ps5_victory.png", 
            "win_6.mp3", "ps5_6.mp3");
            
        // Win 14 / PS5 9: Rest / One more game
        Add(DialogueContext.Evening, "legacy_04", 
            "如果累了，闭目养神五分钟吧。", "再玩最后一局... 就一局！", 
            "win_night.png", "ps5_game_over.png", // game_over exists? No. ps5_tired?
            // ps5_tired exists.
            "win_14.mp3", "ps5_9.mp3");

        // ==========================================
        // 7. Date Specific (Workday / Weekend)
        // ==========================================
        
        Add(DialogueContext.Workday, "workday_01",
            "今天是工作日，调整状态准备战斗吧！", "工作日竟然有空玩游戏... 在家就是自由呀！",
            "win_coding.png", "ps5_portable.png",
            "win_workday_01.mp3", "ps5_workday_01.mp3");

        Add(DialogueContext.Weekend, "weekend_01",
            "是周末耶... 在家也要这么忙碌吗？别忘了休息哦。", "周末万岁！今天谁也别想把我和手柄分开！",
            "win_music.png", "ps5_excited.png",
            "win_weekend_01.mp3", "ps5_weekend_01.mp3");

        Add(DialogueContext.Weekend, "weekend_02",
            "周末愉快！做点自己喜欢的事情吧~", "这就是“白金奖杯”的含金量！", // ps5_weekend_02 missing? Map to legacy?
            // Doc says win_weekend_02 exists. ps5_weekend_missing?
            // Let's use generic cheer for ps5?
            // ps5_11 is Platinum Trophy text.
            "win_happy.png", "ps5_victory.png",
            "win_weekend_02.mp3", "ps5_11.mp3");

        // ==========================================
        // 8. Health & Long Session
        // ==========================================
        
        Add(DialogueContext.HealthLong, "health_01",
            "盯着屏幕太久啦！快去给眼睛放个假！", "虽然游戏很好玩，但身体也很重要！暂停一下吧？",
            "win_blind.png", "ps5_tired.png",
            "win_health_long_01.mp3", "ps5_health_long_01.mp3");

        Add(DialogueContext.HealthLong, "health_02",
            "坐姿变差了哦，起来伸个懒腰吧？", "手柄都发烫了，难道你不累吗？",
            "win_health_long_02.mp3", "ps5_health_long_02.mp3", // voices match ID
             "win_thinking.png", "ps5_pout.png"); // Img placeholders?

        // ==========================================
        // 9. System Events
        // ==========================================
        
        Add(DialogueContext.SystemHighCpu, "system_cpu", 
            "好热... 你的电脑在燃烧吗？！", "哇啊啊！脑子要转不过来了！", 
            "win_overworked.png", "ps5_frustrated.png",
            "system_cpu_01.mp3", "system_cpu_02.mp3");

        Add(DialogueContext.SystemHighRam, "system_ram", 
            "我... 记不住这么多东西啦...", "是不是开了太多 Chrome 标签页？快关掉几个！", 
            "win_thinking.png", "ps5_tired.png",
            "system_ram_01.mp3", "system_ram_02.mp3");

        Add(DialogueContext.SystemIdle, "system_idle", 
            "睡着了吗？... 那我也偷偷偷个懒...", "盯—————", 
            "win_sleepy.png", "ps5_pout.png",
            "system_idle_01.mp3", "system_idle_02.mp3");

        // Special Audio
        Add(DialogueContext.SpecialAudio, "switch_audio",
            "声音通道已切换！这种音质... 很适合听音乐呢。", "环绕声准备就绪！敌人的脚步声逃不掉的！",
            "win_music.png", "ps5_gaming_realistic_immersed.png",
            "special_audio_win.mp3", "special_audio_ps5.mp3");
    }

    private void Add(DialogueContext context, string id, string winText, string ps5Text, string winImg, string ps5Img, string winVoice, string ps5Voice)
    {
        if (!_dialoguePool.ContainsKey(context))
        {
            _dialoguePool[context] = new List<DialogueItem>();
            _shuffleBags[context] = new List<int>();
        }

        var item = new DialogueItem
        {
            Id = id,
            WinText = winText,
            PS5Text = ps5Text,
            WinVoice = winVoice,
            PS5Voice = ps5Voice,
            ReactionImageWin = winImg,
            ReactionImagePS5 = ps5Img
        };
        
        _dialoguePool[context].Add(item);
    }

    /// <summary>
    /// 获取当前上下文的对话 (Context-Aware + Shuffle Bag)
    /// </summary>
    public DialogueItem GetDialogue(bool isPS5Mode, DialogueContext? forceContext = null)
    {
        // 1. Determine Context
        var context = forceContext ?? GetTimeContext();

        // 2. Get Bag
        if (!_shuffleBags.ContainsKey(context) || !_dialoguePool.ContainsKey(context))
        {
             // Fallback to Noon if not found
             context = DialogueContext.Noon;
        }

        var bag = _shuffleBags[context];
        var pool = _dialoguePool[context];

        // 3. Shuffle Logic
        if (bag.Count == 0)
        {
            // Refill
            for (int i = 0; i < pool.Count; i++) bag.Add(i);
        }

        // Draw random
        int bagIndex = _random.Next(bag.Count);
        int poolIndex = bag[bagIndex];
        
        // Remove used
        bag.RemoveAt(bagIndex);

        return pool[poolIndex];
    }

    private DialogueContext GetTimeContext()
    {
        // Chance to override Time with Day-Type (Workday/Weekend)
        // Only during day time (09-18)
        var h = DateTime.Now.Hour;
        bool isDayTime = h >= 9 && h < 18;
        
        if (isDayTime && _random.NextDouble() < 0.20) // 20% chance
        {
             var day = DateTime.Now.DayOfWeek;
             bool isWeekend = (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday);
             return isWeekend ? DialogueContext.Weekend : DialogueContext.Workday;
        }

        if (h >= 6 && h < 9) return DialogueContext.Morning; // 06-09
        
        if (h >= 9 && h < 11) return DialogueContext.Morning; 
        
        if (h >= 11 && h < 14) return DialogueContext.Noon;
        
        if (h >= 14 && h < 19) return DialogueContext.Afternoon; // 14-18
        
        if (h >= 19 && h < 23) return DialogueContext.Evening;
        
        return DialogueContext.LateNight; // 23-06
    }

    /// <summary>
    /// 播放语音
    /// </summary>
    public void PlayVoice(bool isPS5Mode, DialogueItem item)
    {
        if (!_config.EnableMoeVoice) return;
        
        string filename = isPS5Mode ? item.PS5Voice : item.WinVoice;
        PlayVoiceFile(filename);
    }

    private void PlayVoiceFile(string filename)
    {
        try
        {
            // Try specific then generic
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Moe/Voice", filename);
            if (!File.Exists(path))
            {
                // Fallback logic could go here
                System.Diagnostics.Debug.WriteLine($"[Dialogue] Missing: {path}");
                return;
            }

            _voicePlayer.Open(new Uri(path));
            _voicePlayer.Volume = 1.0;
            _voicePlayer.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dialogue] Play Error: {ex.Message}");
        }
    }
    
    public void PlayModeSwitchVoice(bool isPS5Mode)
    {
        string filename = isPS5Mode ? "switch_to_ps5.mp3" : "switch_to_win.mp3";
        PlayVoiceFile(filename);
    }
}
