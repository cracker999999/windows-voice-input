# VoiceInput

## 项目简介
VoiceInput 是一个 Windows 10 后台语音输入工具：按住录音热键开始录音，松开后自动转写并注入到当前焦点输入框。

核心流程：
1. 全局键盘钩子监听 `Fn` / 回退热键（默认 `RightCtrl`）
2. NAudio 采集麦克风音频并流式推送到 Azure Speech
3. 悬浮窗实时显示波形和中间转写文本
4. 可选 LLM 保守纠错
5. 通过剪贴板 + `Ctrl+V` 注入文本

## 环境要求
- Windows 10
- .NET 8 SDK（开发构建）
- Azure Speech 服务 Key 与 Region

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
  "azureSpeechKey": "",
  "azureSpeechRegion": "",
  "fallbackHotkey": "RightCtrl"
}
```

字段说明：
- `language`: 识别语言，支持 `en-US/zh-CN/zh-TW/ja-JP/ko-KR`
- `llmEnabled`: 是否开启 LLM 纠错
- `apiBaseUrl/apiKey/model`: LLM 纠错接口参数
- `azureSpeechKey/azureSpeechRegion`: Azure Speech 认证信息
- `fallbackHotkey`: Fn 不可捕获时的回退热键（默认右 Ctrl）

## 使用说明
1. 托盘右键菜单可切换语言、开关 LLM、打开 Settings、退出
2. 按住录音热键开始录音，松开触发转写
3. 若启用 LLM，悬浮窗会显示 `Refining...` 后再注入

## 语言切换
在托盘菜单 `Language` 子菜单中切换，选择后立即生效并写入配置文件。

## LLM 设置
在托盘 `Settings` 窗口填写：
- API Base URL
- API Key
- Model

可点 `Test` 验证连通性，`Save` 保存。API Key 支持清空后保存。

## 构建发布
已配置单文件自包含发布参数（`win-x64`）：
- `PublishSingleFile=true`
- `SelfContained=true`
- `IncludeNativeLibrariesForSelfExtract=true`

发布命令：
`dotnet publish VoiceInput\VoiceInput.csproj -c Release -r win-x64`

默认输出目录：`Release\`，目标文件为 `Release\VoiceInput.exe`。

