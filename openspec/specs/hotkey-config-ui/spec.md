## ADDED Requirements

### Requirement: Hotkey recording in settings
SettingsWindow SHALL display a hotkey recorder control labeled "触发热键"，点击后进入监听状态，捕获用户实际按下的键（支持单键和组合键如 Ctrl+Shift+R），并将捕获结果显示为可读文本。

#### Scenario: Click to start recording
- **WHEN** user clicks the hotkey input control
- **THEN** control enters listening state, displaying "请按下快捷键..."

#### Scenario: Single key captured
- **WHEN** user presses a single key (e.g. ScrollLock) while control is in listening state
- **THEN** control displays the key name and exits listening state

#### Scenario: Modifier + key combination captured
- **WHEN** user presses a modifier key combination (e.g. RightCtrl+Shift) while control is in listening state
- **THEN** control displays the combination (e.g. "RightCtrl+Shift") and exits listening state

#### Scenario: Escape cancels recording
- **WHEN** user presses Escape while control is in listening state
- **THEN** control reverts to previously configured hotkey and exits listening state

#### Scenario: Current hotkey pre-filled
- **WHEN** user opens SettingsWindow
- **THEN** the hotkey control shows the currently configured FallbackHotkey

#### Scenario: Save updates hotkey immediately
- **WHEN** user clicks Save after recording a new hotkey
- **THEN** new hotkey is written to config.json AND takes effect immediately without restart
