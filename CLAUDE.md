# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# 开发构建
dotnet build VoiceInput/VoiceInput.csproj -c Release

# 运行
dotnet run --project VoiceInput/VoiceInput.csproj -c Release

# 单文件自包含发布 → Release\VoiceInput.exe
dotnet publish VoiceInput/VoiceInput.csproj -c Release -r win-x64
```

## Architecture

整体为单进程 WPF 应用，无任务栏窗口，仅托盘图标常驻。核心流程由 `App.xaml.cs` 编排，所有服务在 `OnStartup` 中实例化并连线。

### 核心流水线

```
LowLevelKeyboardHook (Fn/RightCtrl 按下)
  → AudioRecorder.Start() → PushAudioInputStream → SpeechTranscriptionService
  → OverlayWindow 实时显示波形 + 中间转写
  → (可选) LlmRefinementService.RefineAsync()
  → TextInjector.InjectAsync()
  → OverlayWindow.HideOverlayAsync()
```

录音期间 `_pipelineLock`（SemaphoreSlim）防止并发触发。

### 服务层 `VoiceInput/Services/`

| 文件 | 职责 |
|------|------|
| `LowLevelKeyboardHook.cs` | `WH_KEYBOARD_LL` 全局钩子，监听 Fn（vkCode 0xFF/0xE8/0xE9）及可配置回退键（默认 RightCtrl = 0xA3），返回 1 抑制事件传播 |
| `AudioRecorder.cs` | NAudio `WaveInEvent`（16kHz/16bit/mono），推送帧到 `PushAudioInputStream`，计算 RMS 触发 `RmsChanged` 事件 |
| `SpeechTranscriptionService.cs` | Azure SDK `SpeechRecognizer` 连续识别，`Recognizing`→`InterimTextUpdated`，`Recognized`→`FinalTextReady` |
| `LlmRefinementService.cs` | `HttpClient` 调用 OpenAI 兼容 API，5s 超时，超时返回原文 |
| `TextInjector.cs` | 保存剪贴板 → `SendInput` Ctrl+V → 100ms → 恢复，最多重试 3 次（50ms 间隔）|
| `ConfigService.cs` | 读写 `%AppData%\VoiceInput\config.json`，`Current` 属性持有当前配置 |
| `AppLogger.cs` | 简单文件日志，输出到 `voiceinput.log` |

### UI 层 `VoiceInput/UI/`

- **`OverlayWindow`**：无边框胶囊悬浮窗（56px 高，圆角 28），底部居中，`AllowsTransparency=True`。5 根波形条由 `DispatcherTimer`（60fps）驱动，权重 `[0.5, 0.8, 1.0, 0.75, 0.55]`，attack 40% / release 15% 包络。文字宽度通过 `DoubleAnimation` 平滑过渡。入场 0.35s ElasticEase，退场 0.22s。
- **`SettingsWindow`**：Azure Key/Region + LLM API 配置，Test/Save 按钮。

### 配置模型 `VoiceInput/Models/AppConfig.cs`

字段：`Language`（默认 zh-CN）、`LlmEnabled`、`ApiBaseUrl`、`ApiKey`、`Model`、`AzureSpeechKey`、`AzureSpeechRegion`、`FallbackHotkey`（默认 RightCtrl）。

## Key Constraints

- Fn 键在部分笔记本固件层处理，不经 OS，需用 `FallbackHotkey` 替代
- Azure Speech Key/Region 未配置时，钩子触发会弹出 balloon 提示而非崩溃
- 单文件发布依赖 `IncludeNativeLibrariesForSelfExtract=true` 打包 Azure Speech native DLL
- 所有 UI 操作须通过 `Dispatcher.BeginInvoke` 回到主线程
