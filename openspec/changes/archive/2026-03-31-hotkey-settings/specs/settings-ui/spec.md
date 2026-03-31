## MODIFIED Requirements

### Requirement: Settings UI layout
SettingsWindow SHALL display a "触发热键" hotkey recorder row (a clickable TextBox-style control). The control SHALL be positioned after the Azure Region / quota link rows and before the API Base URL row.

#### Scenario: Hotkey recorder visible
- **WHEN** user opens SettingsWindow
- **THEN** a "触发热键" recorder control is visible showing the current hotkey

#### Scenario: Existing fields unaffected
- **WHEN** user opens SettingsWindow
- **THEN** all existing fields and buttons remain in their original positions and behavior
