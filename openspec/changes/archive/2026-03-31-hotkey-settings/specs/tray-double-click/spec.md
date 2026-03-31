## ADDED Requirements

### Requirement: Tray icon double-click opens settings
The system SHALL open SettingsWindow when the user double-clicks the tray icon, reusing the same OpenSettings() logic as the tray menu item.

#### Scenario: Double-click opens settings
- **WHEN** user double-clicks the tray icon
- **THEN** SettingsWindow opens (or is brought to front if already open)

#### Scenario: Double-click while recording
- **WHEN** user double-clicks the tray icon while recording is in progress
- **THEN** SettingsWindow opens without interrupting the recording pipeline
