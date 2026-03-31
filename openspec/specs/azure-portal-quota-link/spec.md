## ADDED Requirements

### Requirement: Azure Portal quota link in SettingsWindow
SettingsWindow 的 Azure Speech 配置区域 SHALL 提供一个可点击的超链接，点击后在默认浏览器中打开 Azure Portal Cognitive Services 页面。

#### Scenario: 点击超链接跳转 Portal
- **WHEN** 用户在 SettingsWindow 点击「查看用量」超链接
- **THEN** 默认浏览器打开 Azure Portal Cognitive Services 页面

#### Scenario: Key/Region 为空时链接仍可用
- **WHEN** AzureSpeechKey 和 AzureSpeechRegion 均为空
- **THEN** 超链接仍然可点击，跳转到 Portal 概览页
