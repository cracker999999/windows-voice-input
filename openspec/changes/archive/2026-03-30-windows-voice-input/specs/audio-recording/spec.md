## ADDED Requirements

### Requirement: Microphone capture
The system SHALL capture microphone audio using NAudio WaveInEvent at 16kHz, mono, 16-bit PCM and push frames to Azure Speech SDK PushAudioInputStream.

#### Scenario: Audio capture starts with recording
- **WHEN** recording begins
- **THEN** system opens microphone and begins capturing audio frames

#### Scenario: Audio capture stops cleanly
- **WHEN** recording ends
- **THEN** system stops WaveInEvent and flushes the audio stream

### Requirement: Real-time RMS computation
The system SHALL compute RMS (root mean square) amplitude per audio frame and expose it for waveform visualization.

#### Scenario: RMS reflects loudness
- **WHEN** user speaks loudly
- **THEN** RMS value is high; when silent, RMS value is near zero
