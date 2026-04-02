# WinNotes C#

一个使用 C# WPF 实现的桌面备忘录客户端，目标是提供接近 macOS Notes 的核心体验：

- 三栏布局：智能文件夹、笔记列表、编辑区
- 文件夹管理：默认文件夹、自定义文件夹、新建、删除
- 智能视图：全部备忘录、已置顶
- 富文本编辑：粗体、斜体、下划线、标题、项目符号、编号、引用
- 搜索过滤：按标题和正文实时搜索
- 本地存储：自动保存到当前用户的 AppData 目录
- 快捷键：`Ctrl+N` 新建笔记，`Ctrl+Shift+N` 新建文件夹，`Ctrl+F` 聚焦搜索

## 项目结构

- `WinNotes.sln`
- `WinNotes.Client/`

## 运行

项目当前目标框架是 `.NET 8 WPF`，可以直接执行：

```powershell
.\.dotnet\dotnet.exe build .\WinNotes.sln
.\.dotnet\dotnet.exe run --project .\WinNotes.Client\WinNotes.Client.csproj
```

也可以使用调试脚本：

```powershell
.\scripts\start-debug.ps1
```

默认会在后台启动应用并输出进程 ID。
如果你明确想以前台方式运行，可以执行：

```powershell
.\scripts\start-debug.ps1 -Wait
```

## 数据文件

应用数据默认保存在：

`%AppData%\WinNotes\winnotes-data.json`

## 当前环境说明

我已经在仓库根目录通过官方 `dotnet-install` 脚本安装了一份本地 `.NET 8 SDK` 到 `.dotnet/`，并完成了构建与启动验证；`.gitignore` 已忽略该目录，避免把本地 SDK 一起提交。
