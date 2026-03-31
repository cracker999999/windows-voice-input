## MODIFIED Requirements

### Requirement: Settings UI layout
SettingsWindow SHALL display an Azure Portal quota link below the Azure Region field. The link SHALL be styled as a hyperlink (not a button) and labeled "查看用量 →".

#### Scenario: Quota link visible in settings
- **WHEN** user opens SettingsWindow
- **THEN** a "查看用量 →" hyperlink is visible below the Azure Region row

#### Scenario: Existing fields unaffected
- **WHEN** user opens SettingsWindow
- **THEN** all existing fields (Azure Speech Key, Azure Region, API Base URL, API Key, Model) and buttons (Test, Save) remain in their original positions and behavior
