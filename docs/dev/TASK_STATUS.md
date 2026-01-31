# 任务追踪 (Task Tracker)

> **归档时间**: v1.2.1 Release 后
> **当前状态**: 核心互动功能已交付，进入维护与细节优化阶段。

## 🚀 进行中 (In Progress)

- [ ] **Wpf.Ui 控件颜色同步**: 切换主题后 Slider/Toggle 可能不随主题变色，或卡在粉色。需深入排查 `ApplicationAccentColorManager` 与自定义字典的冲突。

---

## 📅 待办事项 (Backlog)

### 季节与节日 (Seasons & Holidays) [Blocked]
- [ ] **农历算法集成**: 引入 `ChineseCalendar` 或类似库以支持农历计算。
- [ ] **全节日支持**: 实装 春节(`win_cny`)、元宵(`win_lantern`)、端午(`win_dragonboat`)、中秋(`win_midautumn`) 等立绘与台词。
- [ ] **节气逻辑**: 细化季节判断（不仅是月份）。

### 多语言支持 (I18n) [Backlog]
- [ ] 提取字符串到资源文件
- [ ] 实现语言切换逻辑

---

## ✅ 已完成 (Recently Completed)

### 更多互动 (Enhanced Interactions)
- [x] **资产审计与利用**:
  - [x] 修复语音台词错配问题
  - [x] 实装 `Workday`/`Weekend` 专属对话
  - [x] 复活 Legacy 语音作为随机填充
- [x] **服务增强**: 
  - [x] `DialogueManager`: 上下文感知 (Time/System/Health)
  - [x] `MascotManager`: 动态反应表情与自动复原
- [x] **触发器实装**:
  - [x] 系统状态监听 (High CPU/RAM, Idle)
  - [x] 健康提醒 (Long Session > 2h)
  - [x] 设备切换触发 (SpecialAudio)
- [x] **交互优化**:
  - [x] 移动互动入口至头像 (ModeIcon)
  - [x] 恢复大立绘为静态背景
  - [x] 修复立绘闪烁问题

### 基础架构
- [x] 更新 `MainWindow.xaml` 确保使用 `DynamicResource`
- [x] 窗口概览背景一致性 (Overview Backdrop Sync)
- [x] 项目重命名 (ScreenBridge → Aria)
- [x] 核心服务拆分 (Theme/Dialogue/Mascot/TrayIcon)
- [x] 单元测试覆盖与 CI/CD 优化
