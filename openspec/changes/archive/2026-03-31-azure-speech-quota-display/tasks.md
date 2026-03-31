## 1. UI 变更

- [x] 1.1 在 SettingsWindow.xaml 的 Azure Region 行之后新增一行，放置「查看用量 →」超链接（TextBlock + Hyperlink），Grid.Row 顺序相应调整
- [x] 1.2 Window Height 适当增加以容纳新行（390 → 420）

## 2. 逻辑实现

- [x] 2.1 在 SettingsWindow.xaml.cs 添加 Hyperlink RequestNavigate 事件处理，调用 `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })` 打开 Azure Portal
