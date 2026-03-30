## Why

Windows 10 缺乏便捷的语音输入工具，尤其对中英文混杂的技术场景支持差。本项目提供一个按住 Fn 键即可录音、松开自动转录并注入文字的后台常驻应用，解决开发者日常输入效率问题。

## What Changes

- 新增全局 Fn 键监听（低级键盘钩子），按住录音、松开转录注入
- 新增 Azure Speech SDK 流式语音转录（默认 zh-CN）
- 新增录音状态悬浮窗（胶囊形，底部居中，实时波形 + 转录文本）
- 新增 LLM 纠错层（OpenAI 兼容 API，保守纠正语音识别错误）
- 新增文字注入（剪贴板 + Ctrl+V SendInput）
- 新增系统托盘菜单（语言切换、LLM 开关、Settings 窗口）
- 新增本地 JSON 配置持久化（语言、LLM 配置）
- 构建为单文件自包含 .exe（无需安装）

## Capabilities

### New Capabilities

- `fn-key-listener`: 通过 WH_KEYBOARD_LL 全局钩子监听 Fn 键按下/松开，抑制系统事件传递，触发录音开始/停止
- `audio-recording`: 使用 NAudio 捕获麦克风音频，计算实时 RMS 电平用于波形显示
- `speech-transcription`: Azure Speech SDK 流式转录，支持 zh-CN/en-US/zh-TW/ja-JP/ko-KR 多语言切换
- `floating-overlay`: WPF 无边框胶囊悬浮窗，含五柱波形动画和实时转录文本，弹性宽度，入场/退场动画
- `llm-refinement`: 通过 OpenAI 兼容 API 对转录结果进行保守纠错，支持中英文混杂场景
- `text-injection`: 剪贴板 + SendInput 模拟 Ctrl+V 将文字注入当前聚焦输入框
- `tray-menu`: NotifyIcon 系统托盘，提供语言切换、LLM 开关、Settings 窗口入口
- `settings-ui`: WPF Settings 窗口，配置 API Base URL、API Key、Model，支持 Test/Save
- `config-persistence`: 本地 JSON 文件存储用户配置（语言偏好、LLM 设置）

### Modified Capabilities

（无现有 capability）

## Impact

- **依赖**: .NET 8、WPF、Azure.CognitiveServices.Speech、NAudio、Hardcodet.NotifyIcon.Wpf
- **系统权限**: 麦克风访问、全局键盘钩子（需以普通用户权限运行）
- **构建**: 单文件自包含 exe，输出到 Release\VoiceInput.exe
- **无任务栏窗口**: 仅托盘图标常驻
