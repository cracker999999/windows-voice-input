## ADDED Requirements

### Requirement: System tray icon
The system SHALL display a NotifyIcon in the Windows system tray as the sole UI entry point. No taskbar window SHALL appear.

#### Scenario: Tray icon visible on startup
- **WHEN** application starts
- **THEN** tray icon is visible in system tray and no taskbar button exists

### Requirement: Language switching menu
The system SHALL provide a tray context menu with language options: English (en-US), Simplified Chinese (zh-CN), Traditional Chinese (zh-TW), Japanese (ja-JP), Korean (ko-KR). Selected language SHALL be checkmarked.

#### Scenario: Language switch from tray
- **WHEN** user selects a language from tray menu
- **THEN** speech recognition language updates immediately and selection is persisted

### Requirement: LLM refinement toggle in tray
The system SHALL provide a tray menu item to enable/disable LLM refinement, showing current state with a checkmark.

#### Scenario: Toggle LLM from tray
- **WHEN** user clicks LLM toggle in tray menu
- **THEN** LLM refinement state flips and is persisted to config

### Requirement: Settings window access
The system SHALL provide a "Settings" menu item in the tray that opens the Settings WPF window.

#### Scenario: Settings opens from tray
- **WHEN** user clicks Settings in tray menu
- **THEN** Settings window opens

### Requirement: Exit menu item
The system SHALL provide an "Exit" menu item that cleanly shuts down the application.

#### Scenario: Exit from tray
- **WHEN** user clicks Exit
- **THEN** application unregisters hooks, releases resources, and terminates
