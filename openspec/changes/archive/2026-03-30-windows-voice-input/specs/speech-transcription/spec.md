## ADDED Requirements

### Requirement: Azure Speech streaming transcription
The system SHALL use Azure Cognitive Services Speech SDK continuous recognition mode (StartContinuousRecognitionAsync) with PushAudioInputStream, listening to Recognizing (interim) and Recognized (final) events.

#### Scenario: Interim results shown in overlay
- **WHEN** user speaks during recording
- **THEN** interim transcription text SHALL appear in the floating overlay in real time

#### Scenario: Final result used for injection
- **WHEN** recording stops
- **THEN** the final Recognized result SHALL be used for text injection

### Requirement: Default language zh-CN
The system SHALL default to zh-CN (Simplified Chinese) out of the box with no user configuration required.

#### Scenario: Chinese speech recognized without setup
- **WHEN** user speaks Chinese with default settings
- **THEN** transcription SHALL return correct Simplified Chinese text

### Requirement: Multi-language support
The system SHALL support en-US, zh-CN, zh-TW, ja-JP, ko-KR switchable at runtime via tray menu without restart.

#### Scenario: Language switch takes effect immediately
- **WHEN** user switches language in tray menu
- **THEN** next recording SHALL use the newly selected language

### Requirement: Transcription timeout
The system SHALL abort recognition and show an error notification if Azure does not respond within 10 seconds.

#### Scenario: Timeout handled gracefully
- **WHEN** Azure Speech API does not respond within 10s
- **THEN** overlay closes, user sees a toast notification, app does not crash
