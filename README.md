# WinNotes

使用 Electron 实现的桌面备忘录应用，交互参考 macOS Notes，当前版本聚焦以下能力：

- 三栏布局：文件夹、笔记列表、编辑区
- 智能分组：全部备忘录、已置顶
- 富文本编辑：粗体、斜体、下划线、标题、列表、引用
- 文件夹管理：新建、自定义文件夹删除
- 本地持久化：自动保存到 Electron `userData` 目录
- 快捷键：`Ctrl/Cmd + N` 新建备忘录，`Ctrl/Cmd + Shift + N` 新建文件夹，`Ctrl/Cmd + F` 搜索

## 启动

```bash
npm.cmd install
npm.cmd start
```

## 数据存储

应用数据会保存到 Electron 的 `app.getPath("userData")` 路径下，文件名为 `winnotes-data.json`。
