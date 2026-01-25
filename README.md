# ScreenBridge 🖥️🎮

> 智能显示器模式切换工具 - 在 PC 和 PS5 之间无缝切换你的显示器设置


<div align="center">
  <img src="docs/images/app_icon.png" width="128" alt="App Icon" />
  <br/>
  
  <img src="docs/images/mode_windows.svg" width="64" alt="Windows Mode" />
  &nbsp;&nbsp;&nbsp;&nbsp;
  <img src="docs/images/mode_ps5.svg" width="64" alt="PS5 Mode" />
</div>

ScreenBridge 是一款 Windows 桌面应用程序，专为同时使用 PC 和游戏主机（如 PS5）共享同一显示器的用户设计。它能自动检测显示器输入源变化，并智能切换音频设备、管理窗口位置，让你的多设备工作流程更加顺畅。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-UI-0078D4?style=flat-square&logo=windows)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

## ✨ 核心功能

### 🔄 自动模式切换
- **DDC/CI 自动检测**：通过 DDC/CI 协议实时监控显示器输入源
- **智能触发**：当显示器切换到 HDMI（PS5）或 DP（PC）时自动切换模式
- **兼容模式**：针对 LG 等切源后关闭 DDC 的显示器，支持信号丢失自动切换

### 🔊 音频切换
- 模式切换时自动切换默认音频输出设备
- 支持音量调节
- 实时显示当前活动音频设备

### 🪟 窗口管理
- 新窗口自动移动到指定显示器
- 窗口概览面板，快速查看和管理所有窗口
- 支持将窗口移动到任意显示器
- 智能过滤系统窗口（任务栏、桌面等）

### 💻 桌面集成
- **系统托盘**：最小化到托盘，右键快速切换模式
- **动态图标**：任务栏和托盘图标随模式自动变化（💻 Windows / 🎮 PS5）
- **Toast 通知**：模式切换时发送系统通知提醒
- **高分屏适配**：完美支持 4K 及高 DPI 缩放

### ⌨️ 全局热键
- `Ctrl + Alt + S`：快速切换模式
- `Ctrl + Alt + W`：打开窗口概览
- *(可在设置中自定义热键)*

## 📸 截图 (Screenshots)

<div align="center">
  <img src="docs/images/screenshot_windows.png" width="800" alt="Windows Mode - Standard" />
  <p><b>Windows 模式 (Moe Glass 主题)</b> - 优雅磨砂玻璃质感，显示器信息实时刷新</p>
  <br/>
  <img src="docs/images/screenshot_ps5_settings.png" width="800" alt="PS5 Mode - Settings" />
  <p><b>PS5 模式 (Moe Clean 主题 + 规则配置)</b> - 沉浸式游戏体验，透明悬浮卡片</p>
</div>

## ✨ v1.1 新特性 (Updates)

### 🎨 萌化主题引擎 (Visual Polish)
- **双重风格**: 提供 **Moe Glass** (磨砂玻璃) 和 **Moe Clean** (极简透明) 两种截然不同的视觉体验。
- **质感升级**: 深度集成 Windows 11 Mica 材质，卡片采用优雅的圆角和阴影处理。
- **动态图标**: 状态栏和托盘图标现在是圆润的渐变风格，更具现代感。

### 🛡️ 稳定性增强
- **亮度防抖**: 优化了 DDC/CI 读取逻辑，增加缓存层，彻底消除了亮度数值闪烁 (--%) 的问题。
- **配置迁移**: 智能检测配置文件版本，自动将旧版本不透明度参数迁移到最佳推荐值。

### ⌨️ 体验优化
- **音频选择器**: 主界面新增实时音频设备下拉列表，切换更直观。
- **显示器详情**: 新增刷新率 (Hz) 和色彩深度 (Bit) 显示，主显示器高亮标记。

## 🚀 快速开始

### 系统要求
- Windows 10/11
- .NET 8.0 Runtime
- 支持 DDC/CI 的显示器（用于自动检测功能）

### 安装

1. 从 [Releases](../../releases) 下载最新版本
2. 解压并运行 `ScreenBridge.App.exe`

### 从源码构建

```bash
# 克隆仓库
git clone https://github.com/elietio/ScreenBridge.git
cd ScreenBridge

# 构建项目
dotnet build

# 运行应用
dotnet run --project src/ScreenBridge.App/ScreenBridge.App.csproj
```

## ⚙️ 配置

点击主界面右下角的 **「配置规则...」** 按钮打开配置窗口：

### 检测设置
- 选择要监控的显示器（支持查看当前 VCP 输入源值）
- 开启/关闭 DDC 信号丢失自动切换（兼容 LG 等显示器）

### 模式配置
为每个模式设置：
- **触发条件**：选择触发该模式的输入源（支持动态读取当前显示器 VCP 值）
- **音频设备**：模式激活时切换到指定的音频输出
- **窗口规则**：新窗口自动移动到指定显示器（支持精确选择型号）
- **程序跟随**：ScreenBridge 主窗口跟随模式移动到指定显示器

## 🏗️ 项目结构

```
ScreenBridge/
├── src/
│   ├── ScreenBridge.Core/          # 核心业务逻辑
│   │   ├── Services/
│   │   │   ├── AudioService.cs     # 音频设备管理
│   │   │   ├── DDCService.cs       # DDC/CI 通信
│   │   │   ├── HotkeyService.cs    # 全局热键
│   │   │   ├── MonitorService.cs   # 显示器检测
│   │   │   └── WindowService.cs    # 窗口管理
│   │   ├── AppConfig.cs            # 应用配置
│   │   ├── ModeManager.cs          # 模式管理器
│   │   └── ModeProfile.cs          # 模式配置模型
│   │
│   └── ScreenBridge.App/           # WPF 应用程序
│       ├── MainWindow.xaml(.cs)    # 主窗口
│       ├── RulesWindow.xaml(.cs)   # 规则配置窗口
│       └── WindowOverview.xaml(.cs) # 窗口概览
│
└── README.md
```

## 🔧 技术栈

- **框架**: .NET 8.0 + WPF
- **UI**: [WPF UI](https://github.com/lepoco/wpfui) (Fluent Design)
- **音频**: AudioSwitcher.AudioApi
- **DDC/CI**: Windows dxva2.dll (GetVCPFeatureAndVCPFeatureReply)
- **窗口管理**: Win32 API (SetWinEventHook, SetWindowPos)

## 📝 常见问题

### DDC/CI 不工作？
1. 确保显示器支持并开启了 DDC/CI 功能
2. 检查线缆连接（某些 HDMI 线不支持 DDC）
3. 尝试在显示器 OSD 菜单中查找 DDC/CI 设置

### 切换到 PS5 后无法自动切回？
这是因为某些显示器（如 LG）在切换输入源后会关闭 DDC 通信。请开启「DDC 信号断开时自动切到模式 B」选项。

### 窗口没有自动移动？
1. 确认在规则配置中设置了正确的窗口移动目标
2. 系统进程（资源管理器、任务栏等）会被自动排除
3. 检查是否有多显示器连接

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 🐛 已知问题 (Known Issues)

- **PS5 模式主题色**: 在切换到 PS5 模式时，部分 UI 控件（如按钮、滑块）可能仍保持默认的蓝色强调色，未正确切换为粉色。目前仅复选框 (CheckBox) 能正确响应颜色切换。此问题受限于 UI 库的主题锁定机制，将在未来版本中修复。

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

---

Made with ❤️ for the PC + Console gaming community
