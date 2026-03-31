## Context

SettingsWindow 已有 Azure Speech Key/Region 输入框，但没有任何途径让用户查看 Azure Speech 的用量或配额。Azure Speech 服务没有公开的配额查询 REST API，用量数据只能在 Azure Portal 查看。

## Goals / Non-Goals

**Goals:**
- 在 SettingsWindow 的 Azure Speech 区域新增「查看用量」超链接按钮
- 点击跳转到 Azure Portal 对应页面
- 若用户已填写 Region，构造带定位的深链接

**Non-Goals:**
- 不在应用内显示具体数字（无可用 API）
- 不做 ARM API 鉴权集成
- 不修改配置存储结构

## Decisions

### 跳转 URL 构造
**决策**: 固定跳转到 Azure Portal Cognitive Services 概览页：
```
https://portal.azure.com/#view/Microsoft_Azure_ProjectOxford/CognitiveServicesHub/~/overview
```
若已填写 Region，仍使用同一 URL（Portal 会根据账户自动展示资源列表），无需拼接 region 参数。

**理由**: Portal 深链接格式不稳定，固定链接更可靠；用户登录后可自行筛选 region。

### UI 形式
**决策**: 使用 WPF `TextBlock` + `Hyperlink` 样式，而非普通 `Button`，放置在 Azure Region 输入框右侧或下方独立行。

**理由**: 超链接视觉上明确表达「跳转到外部」语义，比按钮更直观。

### 打开浏览器
**决策**: `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`

**理由**: UseShellExecute=true 是 .NET 5+ 在 Windows 上打开 URL 的标准方式。

## Risks / Trade-offs

- **Portal URL 变更** → URL 硬编码，若微软调整 Portal 路由会失效；风险低，Portal 主路由稳定
- **用户未登录 Portal** → 跳转后需要登录，属正常流程，无需处理
