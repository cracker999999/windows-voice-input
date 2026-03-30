## Context

目标平台：Windows 10，C# + .NET 8 + WPF。应用后台常驻，通过全局键盘钩子监听 Fn 键，触发录音→转录→注入流程。无现有代码库，从零构建。

## Goals / Non-Goals

**Goals:**
- 全局 Fn 键监听，按住录音松开注入，对用户完全透明
- Azure Speech SDK 流式转录，实时显示中间结果
- 精致胶囊悬浮窗，实时波形 + 转录文本弹性显示
- LLM 保守纠错（可选），聚焦中英混杂技术术语
- 单文件自包含 exe，双击运行

**Non-Goals:**
- 不支持 macOS/Linux
- 不支持离线语音识别（依赖 Azure 云端）
- 不提供安装程序/MSI
- 不做多用户配置隔离

## Decisions

### 1. Fn 键监听方案
**决策**: `SetWindowsHookEx(WH_KEYBOARD_LL)` + `CallNextHookEx` 拦截，返回 1 抑制传递。

**理由**: Fn 键在大多数硬件上通过扫描码 0x00（vkCode 区分）或特定 OEM 键码上报。WH_KEYBOARD_LL 是唯一可在无焦点时全局捕获并抑制的方案。需要注意部分笔记本 Fn 键由固件处理不经过 OS，此时改用可配置热键（如 F13/ScrollLock）作为备选。

**替代方案**: RegisterHotKey API —— 无法抑制系统默认行为，排除。

### 2. 语音识别
**决策**: Azure Cognitive Services Speech SDK，`SpeechRecognizer` 连续识别模式（`StartContinuousRecognitionAsync`），监听 `Recognizing`（中间结果）和 `Recognized`（最终结果）事件。

**理由**: 流式返回中间结果可实时刷新悬浮窗文字；SDK 对中文支持成熟；免费层足够个人使用。

**替代方案**: Whisper 本地模型 —— 无流式支持、首次延迟高，排除；Windows Speech API —— 中文识别质量差，排除。

### 3. 音频采集
**决策**: NAudio `WaveInEvent`，16kHz 单声道 16bit PCM，推送给 Azure SDK 的 `PushAudioInputStream`。同时计算每帧 RMS 供波形显示。

**理由**: NAudio 是 .NET 最成熟的音频库；PushAudioInputStream 允许自定义音频来源，与 NAudio 解耦良好。

### 4. 悬浮窗架构
**决策**: 独立 WPF `Window`（`WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False`），`VerticalAlignment=Bottom` 定位到屏幕底部居中。波形通过 `DispatcherTimer`（60fps）驱动，每帧更新 5 个 `Rectangle` 的 `Height`。

**波形算法**:
- 权重数组 `[0.5, 0.8, 1.0, 0.75, 0.55]`
- 平滑包络：新值 > 旧值时 `envelope = rms * 0.4 + old * 0.6`（attack 40%），否则 `envelope = rms * 0.15 + old * 0.85`（release 15%）
- 每帧加入 `±4%` 随机抖动
- 最小高度 4px，最大高度 32px

**文字宽度**: `TextBlock` 用 `MinWidth=160, MaxWidth=560`，外层 `Border` 宽度通过 `DoubleAnimation`（0.25s `QuadraticEase`）平滑过渡。

**动画**:
- 入场：`ScaleTransform` 从 0.6→1.0 + `OpacityAnimation` 0→1，0.35s `ElasticEase`
- 退场：`ScaleTransform` 1.0→0.8 + `Opacity` 1→0，0.22s `QuadraticEase`

### 5. 文字注入
**决策**: 保存原剪贴板 → 写入转录文本 → `SendInput` 模拟 Ctrl+V → 等待 100ms → 还原剪贴板。

**理由**: `SendInput` 是最兼容的跨应用注入方式；100ms 延迟确保目标应用处理完粘贴事件。

**风险**: 部分应用（如终端）禁用 Ctrl+V，可后续扩展 `SendKeys` 字符逐个输入作为 fallback。

### 6. LLM 纠错
**决策**: 异步调用 OpenAI 兼容 API（`/v1/chat/completions`），System Prompt 明确要求保守纠错，Fn 松开后悬浮窗显示 "Refining..."，LLM 返回后再注入。

**System Prompt 设计**:
```
你是语音识别后处理助手。只修复明显的语音识别错误：
- 中文谐音导致的技术术语错误（如「配森」→「Python」、「杰森」→「JSON」）
- 明显的同音字错误
绝对禁止：改写句子结构、润色措辞、删除任何内容、添加标点。
如果输入看起来正确，原样返回。只返回修正后的文本，不加任何解释。
```

### 7. 配置持久化
**决策**: `%AppData%\VoiceInput\config.json`，JSON 序列化（`System.Text.Json`）。包含：语言代码、LLM 开关、API Base URL、API Key（明文存储，个人工具可接受）、Model 名称。

### 8. 托盘图标
**决策**: `Hardcodet.NotifyIcon.Wpf` NuGet 包，嵌入 WPF 资源，`ContextMenu` 用原生 WinForms `ContextMenuStrip`（兼容性更好）。

### 9. 单文件构建
**决策**: `<PublishSingleFile>true</PublishSingleFile> <SelfContained>true</SelfContained> <RuntimeIdentifier>win-x64</RuntimeIdentifier>`，输出到 `Release\VoiceInput.exe`。Azure Speech SDK 原生库通过 `<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>` 打包。

## Risks / Trade-offs

- **Fn 键硬件差异** → 应用设置中提供备选热键配置（ScrollLock、F13等），用户可自行切换
- **Azure 网络依赖** → 断网时给出 Toast 提示，不崩溃；识别失败超时 10s
- **剪贴板竞争** → 注入前检查剪贴板所有权，失败时重试 3 次，间隔 50ms
- **透明窗口性能** → AllowsTransparency 在部分旧驱动下有性能问题；可提供实色模式降级选项
- **LLM 延迟** → 设置超时 5s，超时后直接注入原始转录文本
- **API Key 明文** → 个人工具场景可接受；后续可用 Windows DPAPI 加密
