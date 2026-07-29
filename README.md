# VoiceInput

## 项目简介

VoiceInput 是一个 Windows 10 后台语音输入工具：按住录音热键开始录音，松开后自动转写并注入到当前焦点输入框。

语音转写支持 Azure Speech 和 Cloudflare Workers AI，二者可在 Settings 中切换：

- Azure Speech 使用流式识别，录音期间显示中间转写
- Cloudflare Workers AI 使用 VAD 分段并行上传，录音期间显示已完成段落，松键后等待剩余结果

核心流程：

1. 全局键盘钩子监听 `Fn` / 回退热键（默认 `RightCtrl`）
2. NAudio 以 16kHz/16bit/mono 采集麦克风音频
3. 按当前供应商执行 Azure 流式识别或 Cloudflare VAD 分段转写
4. 悬浮窗实时显示波形和当前转写进度
5. 可选 LLM 保守纠错
6. 通过剪贴板 + `Ctrl+V` 注入文本

## 环境要求

- Windows 10
- .NET 8 SDK（开发构建）
- Azure Speech Key/Region 或 Cloudflare Account ID/API Token（二选一）

可选：

- OpenAI 兼容接口（用于 LLM 纠错）

## 安装与运行

1. 克隆仓库后进入目录：`E:\VoiceInput`
2. 构建（需 .NET 8 SDK）：
   `dotnet build VoiceInput\VoiceInput.csproj -c Release`
3. 运行：
   `dotnet run --project VoiceInput\VoiceInput.csproj -c Release`

首次运行后，应用会在系统托盘常驻，不显示任务栏主窗口。

## 配置

配置文件路径：`%AppData%\VoiceInput\config.json`

示例：

```json
{
  "language": "zh-CN",
  "llmEnabled": false,
  "apiBaseUrl": "https://api.openai.com/v1",
  "apiKey": "",
  "model": "gpt-4o-mini",
  "speechProvider": "Azure",
  "azureSpeechKey": "",
  "azureSpeechRegion": "",
  "cloudflareAccountId": "",
  "cloudflareApiToken": "",
  "fallbackHotkey": "RightCtrl"
}
```

字段说明：

- `language`: 识别语言，支持 `en-US/zh-CN/zh-TW/ja-JP/ko-KR`
- `speechProvider`: 语音供应商，取值为 `Azure` 或 `Cloudflare`，默认 `Azure`
- `azureSpeechKey/azureSpeechRegion`: Azure Speech 认证信息
- `cloudflareAccountId/cloudflareApiToken`: Cloudflare Workers AI 认证信息
- `llmEnabled`: 是否开启 LLM 纠错
- `apiBaseUrl/apiKey/model`: LLM 纠错接口参数
- `fallbackHotkey`: Fn 不可捕获时的回退热键（默认 `RightCtrl`，支持组合键如 `RightCtrl+R`）

旧配置没有 `speechProvider` 字段时会自动使用 Azure。应用会保留两组供应商凭据，切换供应商不会覆盖另一组配置。

## 使用说明

1. 托盘右键菜单可切换语言、开关 LLM、打开 Settings、退出；双击托盘图标也可直接打开 Settings
2. 按住录音热键开始录音，松开触发转写
3. Cloudflare 模式等待剩余分段时，悬浮窗显示已有文本；尚无结果时显示 `Transcribing...`
4. 若启用 LLM，悬浮窗会显示 `Refining...` 后再注入

## 语音服务设置

在 Settings 窗口选择 `Azure Speech` 或 `Cloudflare Workers AI`，填写当前供应商的凭据后点击 `Test Speech` 验证连接，再点击 `Save` 保存。

当前供应商凭据缺失时，触发录音会打开 Settings 并显示错误提示。

## 语言切换

在托盘菜单 `Language` 子菜单中切换，选择后立即生效并写入配置文件。

## LLM 设置

在托盘 `Settings` 窗口填写：

- API Base URL
- API Key
- Model

可点 `Test LLM` 验证连通性，`Save` 保存。API Key 支持清空后保存。

## 热键设置与录制

在 `Settings` 窗口点击「触发热键」录制框后：

1. 进入监听状态，显示「请按下快捷键...」
2. 支持单键或组合键（如 `RightCtrl+R`、`RightCtrl+RightShift+R`）
3. 按 `Esc` 取消本次录制并恢复原值
4. 点击 `Save` 后立即生效，无需重启应用

录制期间会临时屏蔽语音触发，避免在设置热键时误启动语音识别。

### RightAlt (AltGr) 说明

- 在部分键盘布局中，按 `RightAlt` 会被系统上报为 `RightCtrl+RightAlt`（AltGr 行为）
- 为避免误判，录制器会将这类系统附带的 `RightCtrl` 过滤掉，最终显示为 `RightAlt`
- 因此当前不建议使用 `RightCtrl+RightAlt` 作为热键组合

## 构建发布

已配置单文件非自包含发布参数（`win-x64`）：

- `PublishSingleFile=true`
- `SelfContained=false`
- `IncludeNativeLibrariesForSelfExtract=true`

发布命令：
`dotnet publish VoiceInput\VoiceInput.csproj -c Release -r win-x64`

也可运行 `repack.bat`：脚本会关闭正在运行的程序、恢复依赖并重新发布。

默认输出目录：`Release\`，目标文件为 `Release\VoiceInput.exe`。

说明：
- 该模式会尽量将 native 依赖打包进单文件，减少同目录 DLL 文件。
- 目标机器需安装 `.NET 8 Desktop Runtime (x64)`。

