## ADDED Requirements

### Requirement: Global Fn key hook
The system SHALL install a WH_KEYBOARD_LL low-level keyboard hook via SetWindowsHookEx to globally monitor Fn key press and release events, suppressing OS-level propagation by returning 1 from the hook callback.

#### Scenario: Fn key press starts recording
- **WHEN** user presses and holds the Fn key
- **THEN** system starts audio recording and shows the floating overlay

#### Scenario: Fn key release stops recording
- **WHEN** user releases the Fn key
- **THEN** system stops audio recording and begins transcription

#### Scenario: Fn key event suppressed
- **WHEN** Fn key is pressed
- **THEN** system default Fn behavior SHALL NOT be triggered

### Requirement: Fallback hotkey configuration
The system SHALL provide a configurable fallback hotkey (e.g. ScrollLock, F13) for hardware where Fn key is handled by firmware and not exposed to the OS.

#### Scenario: Fallback hotkey works
- **WHEN** user configures a fallback hotkey in Settings
- **THEN** that key triggers recording instead of Fn
