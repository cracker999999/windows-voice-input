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
