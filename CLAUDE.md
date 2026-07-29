# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# 开发构建
dotnet build VoiceInput/VoiceInput.csproj -c Release

# 运行
dotnet run --project VoiceInput/VoiceInput.csproj -c Release

# 单文件框架依赖发布 → Release\VoiceInput.exe
dotnet publish VoiceInput/VoiceInput.csproj -c Release -r win-x64
```

## Architecture

整体为单进程 WPF 应用，无任务栏窗口，仅托盘图标常驻。核心流程由 `App.xaml.cs` 编排，所有服务在 `OnStartup` 中实例化并连线。

### 核心流水线

转写供应商由 `AppConfig.SpeechProvider` 二选一，两条分支的音频采集方式不同。

Azure（流式，有中间转写）：

```
LowLevelKeyboardHook (Fn/RightCtrl 按下)
  → AudioRecorder.StartStreaming() → PushAudioInputStream → SpeechTranscriptionService
  → OverlayWindow 实时显示波形 + 中间转写
  → (可选) LlmRefinementService.RefineAsync()
  → TextInjector.InjectAsync()
  → OverlayWindow.HideOverlayAsync()
```

Cloudflare（VAD 分段并行上传）：

```
LowLevelKeyboardHook (Fn/RightCtrl 按下)
  → AudioRecorder.StartBuffering() → 内存累积 PCM
  → SegmentedTranscriptionCoordinator 监听 RmsChanged 做静音检测
  → 检测到句子边界 → DrainWav() → TranscribeAsync()（请求构造在线程池执行，段落并行在飞）
  → 单段返回 → PartialTextUpdated → OverlayWindow 显示已完成前缀
  → 松键 → CompleteAsync() 切出末段、等全部返回、按发出顺序拼接
  → (可选) LlmRefinementService.RefineAsync()
  → TextInjector.InjectAsync()
```

录音期间 `_pipelineLock`（SemaphoreSlim）防止并发触发。`_currentRoundUsesCloudflare` 在按下热键时锁定本轮供应商，避免录音中改配置导致停止流程走错分支。

`OnAudioRms` 跑在 NAudio 录音线程：波形更新经 `Dispatcher.BeginInvoke` 切回 UI，分段判断留在录音线程，避免 UI 繁忙时错过切段时机。

### 服务层 `VoiceInput/Services/`

| 文件 | 职责 |
|------|------|
| `LowLevelKeyboardHook.cs` | `WH_KEYBOARD_LL` 全局钩子，监听 Fn（vkCode 0xFF/0xE8/0xE9）及可配置回退键（默认 RightCtrl = 0xA3），返回 1 抑制事件传播 |
| `AudioRecorder.cs` | NAudio `WaveInEvent`（16kHz/16bit/mono，`BufferMs = 50`，对外暴露为 `FrameDuration`）。`StartStreaming()` 推送帧到 `PushAudioInputStream`；`StartBuffering()` 在线程同步保护下累积 PCM，`DrainWav()` 取走并清空缓冲、补 44 字节 RIFF 头产出 WAV，`ClearBuffer()` 丢弃纯静音缓冲，`BufferedDuration` 供分段器判断段长。计算 RMS 触发 `RmsChanged` 事件 |
| `SpeechTranscriptionService.cs` | Azure SDK `SpeechRecognizer` 连续识别，`Recognizing`→`InterimTextUpdated`，`Recognized`→`FinalTextReady`。静态 `TestConnectionAsync` 用 0.5s 静音探活 |
| `CloudflareTranscriptionService.cs` | POST `api.cloudflare.com/client/v4/accounts/{id}/ai/run/@cf/openai/whisper-large-v3-turbo`。在线程池完成 WAV 的 base64/JSON 构造，30s 超时，取 `result.text` 并过滤已知静音幻觉短语；通过 `CultureInfo.TwoLetterISOLanguageName` 将配置语言转为 Whisper 所需的 ISO 639-1 代码 |
| `SegmentedTranscriptionCoordinator.cs` | CF 路径的 VAD 分段器。由 `RmsChanged` 驱动：RMS < 0.015 累计静音，静音 ≥ 600ms 且段长 ≥ 1.5s 即切段，段长 ≥ 15s 强制切。有效语音不足 300ms 的段落不上传。`_pendingSegments` 按发出顺序保序拼接，已完成前缀增量发布，`CompleteAsync()` 切末段后 `Task.WhenAll` 汇总 |
| `TranscriptionText.cs` | Azure 与 Cloudflare 共用的分段文本拼接规则：英文段落按需补空格并避免在标点前插空格，中日韩直接拼接 |
| `LlmRefinementService.cs` | `HttpClient` 调用 OpenAI 兼容 API，5s 超时，超时返回原文 |
| `TextInjector.cs` | 保存剪贴板 → 写入转写文本 → `SendInput` Ctrl+V → 100ms → 恢复。每个剪贴板操作最多尝试 8 次，按 30ms 递增退避（总等待 840ms）；备份/恢复失败只记日志，关键写入失败会抛出 |
| `ConfigService.cs` | 读写 `%AppData%\VoiceInput\config.json`，`Current` 属性持有当前配置 |
| `AppLogger.cs` | 简单文件日志，输出到 `voiceinput.log` |

### UI 层 `VoiceInput/UI/`

- **`OverlayWindow`**：无边框胶囊悬浮窗（56px 高，圆角 28），底部居中，`AllowsTransparency=True`。5 根波形条由 `DispatcherTimer`（60fps）驱动，权重 `[0.5, 0.8, 1.0, 0.75, 0.55]`，attack 40% / release 15% 包络。文字宽度通过 `DoubleAnimation` 平滑过渡。入场 0.35s ElasticEase，退场 0.22s。
- **`SettingsWindow`**：供应商单选（Azure / Cloudflare）切换互斥的凭据面板，Azure Key/Region 与 CF Account ID/API Token 同时保存互不覆盖。另有 LLM API 配置，Test Speech / Test LLM / Save 按钮。

### 配置模型 `VoiceInput/Models/AppConfig.cs`

字段：`Language`（默认 zh-CN）、`LlmEnabled`、`ApiBaseUrl`、`ApiKey`、`Model`、`SpeechProvider`（默认 Azure）、`AzureSpeechKey`、`AzureSpeechRegion`、`CloudflareAccountId`、`CloudflareApiToken`、`FallbackHotkey`（默认 RightCtrl）。`UsesCloudflare` 为供应商判定的便捷属性。

## Key Constraints

- Fn 键在部分笔记本固件层处理，不经 OS，需用 `FallbackHotkey` 替代
- 当前供应商的凭据未配置时，钩子触发会弹出 balloon 提示并打开设置窗口，而非崩溃
- Cloudflare 分支没有中间转写，松键后到结果返回之间胶囊显示 `Transcribing...`
- Whisper 需要 ISO 639-1 语言码，`CloudflareTranscriptionService` 通过 `CultureInfo` 将 `zh-CN` 等 BCP-47 标签转换为 `zh` 等两字母代码
- 单文件发布依赖 `IncludeNativeLibrariesForSelfExtract=true` 打包 Azure Speech native DLL
- 后台线程触发的 UI 操作须通过 `Dispatcher.BeginInvoke`、`Invoke` 或 `InvokeAsync` 回到主线程
