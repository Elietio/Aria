namespace ScreenBridge.Core;

/// <summary>
/// 模式配置档 - 定义一个模式的名称、触发条件和行为
/// </summary>
public class ModeProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "模式";
    public string Icon { get; set; } = "Desktop24"; // Segoe MDL2 Assets 字体图标名称或 Symbol枚举名
    public string Description { get; set; } = "";

    /// <summary>
    /// 触发该模式的输入源 VCP 代码列表 (如 15, 17, 27)
    /// </summary>
    public List<int> TriggerInputs { get; set; } = new();

    /// <summary>
    /// 激活该模式时切换到的音频设备名称关键字
    /// </summary>
    public string TargetAudioDeviceName { get; set; } = "";

    /// <summary>
    /// 新窗口自动移动的目标显示器 (Primary, Secondary, 或具体名称)
    /// </summary>
    public string TargetWindowMonitor { get; set; } = "None"; // "None", "Primary", "Secondary"

    /// <summary>
    /// 是否在这个模式下自动移动新窗口
    /// </summary>
    public bool EnableWindowAutoMove => TargetWindowMonitor != "None";
    /// <summary>
    /// ScreenBridge 主程序窗口的目标位置 (Main, Primary, Side, None)
    /// </summary>
    public string AppWindowTargetMonitor { get; set; } = "None";
}
