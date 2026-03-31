## Why

SettingsWindow 目前没有任何方式让用户了解 Azure Speech 的用量和配额状态。由于 Azure Speech 没有公开的剩余额度查询 API，在设置界面提供直接跳转 Azure Portal 的入口，是最简洁可靠的方案。

## What Changes

- 在 SettingsWindow 的 Azure Speech 配置区域，新增一个「查看用量」按钮（或超链接样式控件）
- 点击后在默认浏览器中打开 Azure Portal 对应的 Cognitive Services 配额/用量页面
- 可选：若用户已填写 AzureSpeechRegion，拼接带 region 参数的 Portal 深链接

## Capabilities

### New Capabilities

- `azure-portal-quota-link`: 在 SettingsWindow 中提供跳转 Azure Portal 查看 Speech 用量的入口

### Modified Capabilities

- `settings-ui`: SettingsWindow 新增「查看用量」UI 元素

## Impact

- `VoiceInput/UI/SettingsWindow.xaml`：新增按钮/超链接
- `VoiceInput/UI/SettingsWindow.xaml.cs`：新增点击处理，调用 `Process.Start` 打开浏览器
- 无新依赖，无破坏性变更
