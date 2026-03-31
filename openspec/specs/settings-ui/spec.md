## MODIFIED Requirements

### Requirement: Settings UI layout
SettingsWindow SHALL display an Azure Portal quota link below the Azure Region field. The link SHALL be styled as a hyperlink (not a button) and labeled "查看用量 →".

#### Scenario: Quota link visible in settings
- **WHEN** user opens SettingsWindow
- **THEN** a "查看用量 →" hyperlink is visible below the Azure Region row

#### Scenario: Existing fields unaffected
- **WHEN** user opens SettingsWindow
- **THEN** all existing fields (Azure Speech Key, Azure Region, API Base URL, API Key, Model) and buttons (Test, Save) remain in their original positions and behavior

## ADDED Requirements

### Requirement: Settings window UI
The system SHALL provide a WPF Settings window with input fields for: API Base URL, API Key (supports full clear), and Model name. The window SHALL have Test and Save buttons.

#### Scenario: Save persists configuration
- **WHEN** user fills fields and clicks Save
- **THEN** values are written to config.json and window closes

#### Scenario: API Key can be fully cleared
- **WHEN** user clears the API Key field and saves
- **THEN** stored API Key is empty string

### Requirement: Test API connection
The system SHALL send a minimal test request to the configured API when user clicks Test, and display a success or error message.

#### Scenario: Test success
- **WHEN** API credentials are valid and user clicks Test
- **THEN** a success message is shown in the Settings window

#### Scenario: Test failure
- **WHEN** API credentials are invalid
- **THEN** an error message with details is shown in the Settings window
