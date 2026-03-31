## 1. 托盘双击打开设置

- [x] 1.1 在 App.xaml 的 TaskbarIcon 元素上添加 TrayMouseDoubleClick 事件绑定
- [x] 1.2 在 App.xaml.cs 中添加 OnTrayMouseDoubleClick 处理器，调用现有 OpenSettings() 方法

## 2. SettingsWindow 热键录制 UI

- [x] 2.1 在 SettingsWindow.xaml 新增一行：Label「触发热键」+ TextBox 样式的录制控件（Border+TextBlock），位置在「查看用量」行之后、API Base URL 行之前，Window Height 适当增加
- [x] 2.2 录制控件点击后进入监听状态，显示「请按下快捷键...」，通过 PreviewKeyDown 捕获按键（包括修饰键组合），Escape 取消并恢复原值
- [x] 2.3 将捕获的 Key/ModifierKeys 转换为可读字符串（如「RightCtrl」「RightAlt+Shift」），与 LowLevelKeyboardHook.SetFallbackHotkey 接受的格式一致
- [x] 2.4 在 LoadConfigToUi() 中读取 config.FallbackHotkey 并填入录制控件显示
- [x] 2.5 在 SettingsWindow 构造函数新增 Action<string> onHotkeyChanged 参数并保存
- [x] 2.6 在 OnSaveButtonClick 中读取录制控件当前值写入 config.FallbackHotkey，若值有变化则调用 onHotkeyChanged 回调

## 3. App.xaml.cs 传入回调

- [x] 3.1 修改 App.xaml.cs 中创建 SettingsWindow 的代码，传入 key => _keyboardHook?.SetFallbackHotkey(key) 作为 onHotkeyChanged 回调

