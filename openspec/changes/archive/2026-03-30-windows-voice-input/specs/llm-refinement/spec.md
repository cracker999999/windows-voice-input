## ADDED Requirements

### Requirement: LLM-based transcription refinement
The system SHALL call an OpenAI-compatible API (/v1/chat/completions) with a conservative system prompt to correct obvious speech recognition errors (homophones, technical terms misrecognized in Chinese). If input appears correct, SHALL return it unchanged.

#### Scenario: Technical term corrected
- **WHEN** transcription contains "配森"
- **THEN** LLM returns "Python"

#### Scenario: Correct input unchanged
- **WHEN** transcription is already correct
- **THEN** LLM returns the exact same text

### Requirement: LLM timeout fallback
The system SHALL fall back to raw transcription if LLM does not respond within 5 seconds.

#### Scenario: LLM timeout fallback
- **WHEN** LLM API does not respond within 5s
- **THEN** system injects the original transcription without LLM correction

### Requirement: LLM enable/disable toggle
The system SHALL allow users to enable or disable LLM refinement via tray menu without restart.

#### Scenario: LLM disabled skips refinement
- **WHEN** LLM is disabled
- **THEN** transcription is injected immediately without any API call
