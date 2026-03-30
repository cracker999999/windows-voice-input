## 1. Project Setup

- [ ] 1.1 Create .NET 8 WPF solution and project (VoiceInput.csproj) with correct TargetFramework, OutputType=WinExe
- [ ] 1.2 Add NuGet packages: Microsoft.CognitiveServices.Speech, NAudio, Hardcodet.NotifyIcon.Wpf
- [ ] 1.3 Configure csproj for single-file self-contained publish (PublishSingleFile, SelfContained, win-x64, IncludeNativeLibrariesForSelfExtract)
- [ ] 1.4 Add publish profile or Directory.Build.props targeting Release\VoiceInput.exe output
- [ ] 1.5 Set ApplicationIcon and suppress taskbar window (ShowInTaskbar=false on main window, hide from App.xaml.cs startup)

## 2. Config Persistence

- [ ] 2.1 Define AppConfig model class (language, llmEnabled, apiBaseUrl, apiKey, model)
- [ ] 2.2 Implement ConfigService: Load() reads %AppData%\VoiceInput\config.json with defaults, Save() writes atomically
- [ ] 2.3 Wire ConfigService as singleton, load on app startup

## 3. Global Fn Key Listener

- [ ] 3.1 Implement LowLevelKeyboardHook class using SetWindowsHookEx(WH_KEYBOARD_LL) via P/Invoke
- [ ] 3.2 Detect Fn key press/release (handle OEM vkCode; add configurable fallback key from config)
- [ ] 3.3 Suppress Fn event propagation by returning 1 from hook callback
- [ ] 3.4 Expose RecordingStarted and RecordingStopped events; unhook on Dispose

## 4. Audio Recording

- [ ] 4.1 Implement AudioRecorder class using NAudio WaveInEvent (16kHz, mono, 16-bit PCM)
- [ ] 4.2 Push audio frames to Azure Speech SDK PushAudioInputStream
- [ ] 4.3 Compute RMS per frame and expose RmsChanged event for waveform
- [ ] 4.4 Implement Start() and Stop() with clean resource disposal

## 5. Speech Transcription

- [ ] 5.1 Implement SpeechTranscriptionService wrapping Azure SpeechRecognizer in continuous mode
- [ ] 5.2 Wire Recognizing event to expose interim text updates
- [ ] 5.3 Wire Recognized event to return final transcription result
- [ ] 5.4 Implement language switching (reinitialize recognizer with new locale)
- [ ] 5.5 Implement 10s timeout with cancellation and error notification
- [ ] 5.6 Load Azure subscription key and region from config (prompt user if missing)

## 6. Floating Overlay Window

- [ ] 6.1 Create OverlayWindow.xaml: WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False, Height=56, positioned bottom-center
- [ ] 6.2 Implement capsule shape: Border with CornerRadius=28, dark background, horizontal StackPanel
- [ ] 6.3 Implement 5-bar waveform: 5 Rectangle elements in WrapPanel (44×32px area), weight array [0.5,0.8,1.0,0.75,0.55]
- [ ] 6.4 Implement waveform animation logic: envelope (attack 40%, release 15%), ±4% jitter, min 4px max 32px height, driven by DispatcherTimer at 60fps
- [ ] 6.5 Implement elastic text label: TextBlock with MinWidth=160 MaxWidth=560, DoubleAnimation 0.25s QuadraticEase on width change
- [ ] 6.6 Implement entry Storyboard: ScaleTransform 0.6→1.0 + Opacity 0→1, 0.35s ElasticEase
- [ ] 6.7 Implement exit Storyboard: ScaleTransform 1.0→0.8 + Opacity 1→0, 0.22s QuadraticEase; hide window on complete
- [ ] 6.8 Expose UpdateText(string) and ShowRefining() methods on OverlayWindow

## 7. LLM Refinement

- [ ] 7.1 Implement LlmRefinementService calling /v1/chat/completions via HttpClient
- [ ] 7.2 Write conservative system prompt (correct homophones/technical terms only, return unchanged if correct)
- [ ] 7.3 Implement 5s timeout with CancellationToken; return original text on timeout
- [ ] 7.4 Wire enable/disable flag from ConfigService

## 8. Text Injection

- [ ] 8.1 Implement TextInjector: save clipboard, write text, SendInput Ctrl+V, await 100ms, restore clipboard
- [ ] 8.2 Implement clipboard retry logic (3 attempts, 50ms interval) for locked clipboard

## 9. Tray Menu & Settings UI

- [ ] 9.1 Implement NotifyIcon setup using Hardcodet.NotifyIcon.Wpf in App.xaml
- [ ] 9.2 Build tray ContextMenu: language submenu (5 languages, checkmark on active), LLM toggle (checkmark), Settings, Exit
- [ ] 9.3 Wire language menu items to SpeechTranscriptionService and ConfigService
- [ ] 9.4 Wire LLM toggle to LlmRefinementService and ConfigService
- [ ] 9.5 Create SettingsWindow.xaml: API Base URL, API Key, Model fields; Test and Save buttons
- [ ] 9.6 Implement Test button: call LLM API with minimal request, show success/error inline
- [ ] 9.7 Implement Save button: validate non-empty URL, persist via ConfigService, close window
- [ ] 9.8 Support fully clearing API Key field

## 10. Main Orchestration

- [ ] 10.1 Wire all services in App.xaml.cs: hook → recorder → transcription → overlay → LLM → injector
- [ ] 10.2 On Fn press: start AudioRecorder, show OverlayWindow with waveform
- [ ] 10.3 On Fn release: stop AudioRecorder, await final transcription, update overlay
- [ ] 10.4 If LLM enabled: show "Refining..." in overlay, await LlmRefinementService, then inject
- [ ] 10.5 If LLM disabled: inject transcription immediately after recognition
- [ ] 10.6 After injection: trigger overlay exit animation
- [ ] 10.7 Handle errors (no mic, Azure key missing, network failure) with tray balloon notification

## 11. Build & Publish

- [ ] 11.1 Verify dotnet publish -c Release -r win-x64 produces Release\VoiceInput.exe
- [ ] 11.2 Test single-file exe runs on clean Windows 10 without .NET runtime installed
- [ ] 11.3 Verify no taskbar window appears, tray icon present, Fn key recording works end-to-end
