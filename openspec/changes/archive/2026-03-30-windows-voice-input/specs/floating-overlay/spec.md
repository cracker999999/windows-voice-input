## ADDED Requirements

### Requirement: Capsule overlay window
The system SHALL display a borderless WPF Window (WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False) positioned at the bottom-center of the screen, height 56px, corner radius 28px.

#### Scenario: Overlay appears on recording start
- **WHEN** Fn key is pressed
- **THEN** capsule overlay SHALL animate in at screen bottom-center

#### Scenario: Overlay hides on injection complete
- **WHEN** text injection finishes
- **THEN** overlay SHALL animate out and hide

### Requirement: Five-bar waveform visualization
The system SHALL render 5 vertical rectangle bars (total area 44×32px) driven by real-time audio RMS. Bar weights SHALL be [0.5, 0.8, 1.0, 0.75, 0.55]. Envelope: attack 40%, release 15%. Each bar SHALL have ±4% random jitter. Min height 4px, max height 32px.

#### Scenario: Waveform responds to voice
- **WHEN** user speaks
- **THEN** bars grow taller proportionally to loudness

#### Scenario: Waveform shows silence
- **WHEN** user is silent
- **THEN** bars remain at minimum height (4px)

### Requirement: Elastic text label
The system SHALL display interim transcription text in a right-side TextBlock with MinWidth=160px, MaxWidth=560px. The capsule width SHALL animate smoothly (0.25s QuadraticEase) as text length changes.

#### Scenario: Capsule widens with text
- **WHEN** transcription text grows
- **THEN** capsule width increases with smooth 0.25s transition

### Requirement: Entry and exit animations
Entry: ScaleTransform 0.6→1.0 + Opacity 0→1, 0.35s ElasticEase. Exit: ScaleTransform 1.0→0.8 + Opacity 1→0, 0.22s QuadraticEase. Implemented via WPF Storyboard.

#### Scenario: Entry animation plays
- **WHEN** overlay is shown
- **THEN** capsule springs in from center with elastic bounce over 0.35s

#### Scenario: Exit animation plays
- **WHEN** overlay is dismissed
- **THEN** capsule scales down and fades out over 0.22s

### Requirement: Refining state display
The system SHALL show "Refining..." text in the overlay when LLM refinement is in progress.

#### Scenario: Refining state shown
- **WHEN** LLM is enabled and processing the transcription
- **THEN** overlay text SHALL display "Refining..."
