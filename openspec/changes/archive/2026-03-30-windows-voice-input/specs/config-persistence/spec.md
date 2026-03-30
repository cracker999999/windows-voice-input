## ADDED Requirements

### Requirement: JSON config file
The system SHALL persist all user configuration to %AppData%\VoiceInput\config.json using System.Text.Json. Fields: language (string), llmEnabled (bool), apiBaseUrl (string), apiKey (string), model (string).

#### Scenario: Config loaded on startup
- **WHEN** application starts
- **THEN** settings are read from config.json; defaults used if file absent

#### Scenario: Config saved on change
- **WHEN** user changes language or LLM toggle or saves Settings
- **THEN** config.json is updated immediately

### Requirement: Default configuration
The system SHALL use sensible defaults when config.json is absent: language=zh-CN, llmEnabled=false, apiBaseUrl=https://api.openai.com/v1, model=gpt-4o-mini.

#### Scenario: Fresh install defaults
- **WHEN** no config.json exists
- **THEN** app starts with zh-CN language and LLM disabled
