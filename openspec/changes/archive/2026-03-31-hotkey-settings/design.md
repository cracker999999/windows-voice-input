## Context

现有架构：`App.xaml.cs` 持有 `TaskbarIcon`，`LowLevelKeyboardHook` 已实现 `SetFallbackHotkey(string)` 方法可运行时更新热键。`SettingsWindow` 构造注入 `ConfigService` 和 `LlmRefinementService`，目前无法直接访问 hook 实例。

## Goals / Non-Goals

**Goals:**
- 双击托盘图标打开设置窗口（复用现有 `OpenSettings()` 逻辑）
- SettingsWindow 提供热键下拉选框，Save 时更新配置并实时生效

**Non-Goals:**
- 不支持自定义任意键组合（只提供预设列表）
- 不修改 Fn 键 OEM 检测逻辑

## Decisions

### 1. 双击托盘图标
**决策**：在 `App.xaml` 的 `TaskbarIcon` 上添加 `TrayMouseDoubleClick` 事件，处理器调用现有 `OpenSettings()` 方法。

**理由**：一行代码，零额外复杂度。

### 2. 热键下拉选项
**决策**：预设列表：`RightCtrl`、`RightAlt`、`ScrollLock`、`F13`、`F14`、`F15`、`CapsLock`，以 ComboBox 呈现，显示友好名称。

**理由**：有限的预设比自由输入更安全，避免用户填入无效键名导致钩子异常。

### 3. 运行时热键更新
**决策**：`SettingsWindow` 新增构造参数 `Action<string> onHotkeyChanged`，Save 时若 FallbackHotkey 有变化则调用回调。`App.xaml.cs` 传入 `key => _keyboardHook?.SetFallbackHotkey(key)`。

**理由**：保持 SettingsWindow 与 App 解耦，通过回调而非直接引用 hook。

**替代方案**：将 hook 引用直接注入 SettingsWindow —— 增加耦合，排除。

## Risks / Trade-offs

- **CapsLock 作为热键会干扰大小写切换** → 列表中保留但在 UI 旁加注释提示
- **热键冲突（如 F13 部分键盘无此键）** → 用户自行选择，不做强制校验
