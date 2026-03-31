## Why

目前打开设置面板只能通过托盘右键菜单 → Settings，操作路径较长。同时，触发热键（FallbackHotkey）虽然存储在 config.json，但没有任何 UI 入口供用户修改，只能手动编辑配置文件。

## What Changes

- 双击托盘图标直接打开 SettingsWindow
- SettingsWindow 新增「触发热键」下拉选择框，允许用户从预设列表中选择录音触发键
- Save 时将新热键写入配置并实时更新 LowLevelKeyboardHook

## Capabilities

### New Capabilities

- `tray-double-click`: 双击托盘图标打开设置面板
- `hotkey-config-ui`: 在 SettingsWindow 中配置触发热键

### Modified Capabilities

- `settings-ui`: 新增触发热键下拉选择框
- `fn-key-listener`: 支持运行时动态更新 FallbackHotkey 无需重启

## Impact

- `App.xaml.cs`：为 TaskbarIcon 添加 TrayMouseDoubleClick 事件处理
- `VoiceInput/UI/SettingsWindow.xaml`：新增热键 ComboBox
- `VoiceInput/UI/SettingsWindow.xaml.cs`：读写 FallbackHotkey，保存时通知 hook 更新
- `VoiceInput/Services/LowLevelKeyboardHook.cs`：已有 SetFallbackHotkey 方法，可直接复用
- 无破坏性变更，无新依赖
