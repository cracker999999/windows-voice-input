## ADDED Requirements

### Requirement: Clipboard-based text injection
The system SHALL inject transcribed text by: saving current clipboard, writing text to clipboard, sending Ctrl+V via SendInput, waiting 100ms, then restoring original clipboard content.

#### Scenario: Text injected into focused input
- **WHEN** transcription is ready and an input field is focused
- **THEN** text appears in the input field

#### Scenario: Clipboard restored after injection
- **WHEN** injection completes
- **THEN** original clipboard content is restored within ~100ms

### Requirement: Clipboard conflict retry
The system SHALL retry clipboard write up to 3 times with 50ms intervals if clipboard is locked by another process.

#### Scenario: Retry on clipboard lock
- **WHEN** clipboard is locked
- **THEN** system retries up to 3 times before failing silently
