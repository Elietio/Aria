using System.Text.Json;
using System.IO;

namespace Aria.Core;

/// <summary>
/// 应用程序配置
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 主显示器音频设备名称关键字
    /// </summary>
    public string PrimaryAudioKeyword { get; set; } = "";

    /// <summary>
    /// 副显示器音频设备名称关键字
    /// </summary>
    public string SecondaryAudioKeyword { get; set; } = "";

    /// <summary>
    /// 切换模式的热键修饰键
    /// </summary>
    public uint SwitchModeModifiers { get; set; } = (uint)(HotkeyService.ModifierKeys.Ctrl | HotkeyService.ModifierKeys.Alt);

    /// <summary>
    /// 切换模式的热键
    /// </summary>
    public uint SwitchModeKey { get; set; } = HotkeyService.VirtualKeys.VK_S;

    /// <summary>
    /// 窗口概览的热键修饰键
    /// </summary>
    public uint WindowOverviewModifiers { get; set; } = (uint)(HotkeyService.ModifierKeys.Ctrl | HotkeyService.ModifierKeys.Alt);

    /// <summary>
    /// 窗口概览的热键
    /// </summary>
    public uint WindowOverviewKey { get; set; } = HotkeyService.VirtualKeys.VK_W;

    /// <summary>
    /// 是否开机自启
    /// </summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>
    /// 是否启动时最小化到托盘
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// 是否启用DDC/CI自动检测
    /// </summary>
    public bool EnableDDCAutoDetect { get; set; } = true;

    /// <summary>
    /// DDC/CI检测间隔（秒）
    /// </summary>
    public int DDCCheckIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 当 DDC 读取失败时的行为 (针对某些显示器切走后关闭 DDC 的情况)
    /// </summary>
    public DDCLossAction DdcLossAction { get; set; } = DDCLossAction.SwitchToModeB;

    public enum DDCLossAction
    {
        DoNothing,
        SwitchToModeA,
        SwitchToModeB
    }

    /// <summary>
    /// UI 界面风格
    /// </summary>
    public UIStyle Theme { get; set; } = UIStyle.Classic;

    public enum UIStyle
    {
        Classic,
        MoeGlass, // Option 1: Standard Glass (Border + Shadow)
        MoeClean  // Option 2: Ultra Clean (Borderless)
    }

    /// <summary>
    /// MoeGlass 模式的背景不透明度 (默认 60% 以确保在复杂立绘上清晰)
    /// </summary>
    public double GlassOpacity { get; set; } = 0.60;

    /// <summary>
    /// MoeClean 模式的背景不透明度 (默认 30%)
    /// </summary>
    public double CleanOpacity { get; set; } = 0.30;



    /// <summary>
    /// 是否启用 Moe 模式下的看板娘立绘
    /// </summary>
    public bool EnableMoeMascot { get; set; } = true;

    /// <summary>
    /// 是否启用看板娘语音
    /// </summary>
    public bool EnableMoeVoice { get; set; } = true;

    /// <summary>
    /// 看板娘立绘不透明度 (默认 10%)
    /// </summary>
    public double MascotOpacity { get; set; } = 0.10;

    /// <summary>
    /// 窗口背景材质类型
    /// </summary>
    public enum BackdropStyle
    {
        None,    // 不使用任何背景材质
        Mica,    // 云母 (微妙的壁纸色调)
        Acrylic  // 亚克力 (更明显的模糊透明)
    }

    /// <summary>
    /// MoeGlass 模式的背景材质 (默认 Acrylic - 强模糊)
    /// </summary>
    public BackdropStyle GlassBackdrop { get; set; } = BackdropStyle.Acrylic;

    /// <summary>
    /// MoeClean 模式的背景材质 (默认 Mica - 轻微)
    /// </summary>
    public BackdropStyle CleanBackdrop { get; set; } = BackdropStyle.Mica;

    /// <summary>
    /// 是否启用模式切换 Toast 通知
    /// </summary>
    public bool EnableToastNotifications { get; set; } = true;

    /// <summary>
    /// 排除的进程名列表（不自动移动这些进程的窗口）
    /// </summary>
    public List<string> ExcludedProcesses { get; set; } = new();

    /// <summary>
    /// 要监控DDC输入的显示器ID (DevicePath 或 FriendlyName)
    /// 如果为空，则自动使用主显示器
    /// </summary>
    public string DDCMonitorId { get; set; } = "";

    /// <summary>
    /// 模式 A 配置 (默认为 Windows 办公模式)
    /// </summary>
    public ModeProfile ModeA { get; set; } = new ModeProfile 
    { 
        Name = "Windows 模式", 
        Icon = "Desktop24",
        Description = "检测到 DisplayPort/USB-C 输入时激活",
        TriggerInputs = new List<int> { 15, 16, 27 }, // DP1, DP2, USBC
        TargetWindowMonitor = "None"
    };

    /// <summary>
    /// 模式 B 配置 (默认为 游戏/娱乐模式)
    /// </summary>
    public ModeProfile ModeB { get; set; } = new ModeProfile 
    { 
        Name = "PS5 模式", 
        Icon = "XboxController24",
        Description = "检测到 HDMI 输入时激活",
        TriggerInputs = new List<int> { 17, 18 }, // HDMI1, HDMI2
        TargetWindowMonitor = "Secondary"
    };

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aria", "config.json");

    /// <summary>
    /// 加载配置
    /// </summary>
    public static AppConfig Load()
    {
        AppConfig config = new AppConfig();
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
            // 配置加载失败，使用默认配置
        }

        // 迁移旧逻辑
        config.MigrateMoreLegacySettings();

        // 确保非空
        if (config.ModeA == null) config.ModeA = new ModeProfile();
        if (config.ModeB == null) config.ModeB = new ModeProfile();

        return config;
    }

    private void MigrateMoreLegacySettings()
    {
        // 如果 ModeA 的音频没设置，且有旧的 PrimaryAudioKeyword
        if (string.IsNullOrEmpty(ModeA.TargetAudioDeviceName) && !string.IsNullOrEmpty(PrimaryAudioKeyword))
        {
            ModeA.TargetAudioDeviceName = PrimaryAudioKeyword;
        }

        // 如果 ModeB 的音频没设置，且有旧的 SecondaryAudioKeyword
        if (string.IsNullOrEmpty(ModeB.TargetAudioDeviceName) && !string.IsNullOrEmpty(SecondaryAudioKeyword))
        {
            ModeB.TargetAudioDeviceName = SecondaryAudioKeyword;
        }
        
        // 确保 TriggerInputs 初始化
        if (ModeB.TriggerInputs == null) ModeB.TriggerInputs = new List<int> { 17, 18 };

        // [Removed] 移除旧的 Opacity 强制降级逻辑，允许高透明度 (Contrast Mode)
        // if (GlassOpacity > 0.1) GlassOpacity = 0.05;
        // if (CleanOpacity < 0.01) CleanOpacity = 0.02;

        // [Removed] 移除旧的 Opacity 强制降级逻辑，允许高透明度 (Contrast Mode)
        // if (GlassOpacity > 0.1) GlassOpacity = 0.05;
        // if (CleanOpacity < 0.01) CleanOpacity = 0.02;

        // 强制刷新 Backdrop 默认值 (如果它们是旧的反向配置: Glass=Mica, Clean=Acrylic)
        // 我们改为 Glass=Acrylic, Clean=Mica
        if (GlassBackdrop == BackdropStyle.Mica && CleanBackdrop == BackdropStyle.Acrylic)
        {
            GlassBackdrop = BackdropStyle.Acrylic;
            CleanBackdrop = BackdropStyle.Mica;
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public void Save()
    {
        try
        {
            // 同步回旧字段以保持向前兼容（可选）
            if (!string.IsNullOrEmpty(ModeA.TargetAudioDeviceName)) PrimaryAudioKeyword = ModeA.TargetAudioDeviceName;
            if (!string.IsNullOrEmpty(ModeB.TargetAudioDeviceName)) SecondaryAudioKeyword = ModeB.TargetAudioDeviceName;

            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // 配置保存失败
        }
    }
}
