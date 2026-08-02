# Lumen

Lumen 是一个基于 .NET 10 和 WPF 构建的本地 Windows 启动器。它将常用应用、便携程序、配置目录中的文件夹和网页 Quicklink 汇集到一个搜索框中，支持键盘快速启动或打开。

> 当前项目处于 MVP 阶段，仅支持 Windows。

## 功能

- 搜索并启动开始菜单、桌面、`App Paths`、`PATH` 和 Windows 内置应用。
- 发现并启动 Microsoft Store / MSIX 应用。
- 扫描配置目录下的便携 `.exe` 程序。
- 索引配置目录下最多三层的文件夹，并在资源管理器中打开。
- 配置静态或带查询参数的网页 Quicklink。
- 记录使用次数和最近打开结果。
- 支持全局快捷键（默认 `Alt+W`）、托盘菜单、开机自启动和窗口位置保存。
- 使用 SQLite 保存本地索引；重建时会同步移除已卸载、已删除或不再位于索引目录中的项目；不上传索引或使用数据。

## 使用

启动后输入名称搜索，使用 `↑` / `↓` 选择结果，按 `Enter` 执行，按 `Esc` 隐藏窗口。默认全局快捷键 `Alt+W` 可显示或隐藏启动器，也可在设置中修改。

托盘菜单提供以下操作：

- 显示 Lumen
- 打开设置
- 重建索引（应用、便携程序和文件夹）
- 开机自启动
- 退出

### 配置便携应用

在“设置”的“便携应用目录”中，每行填写一个目录，例如：

```text
D:\Tools
E:\PortableApps
```

保存后通过托盘菜单的“重建索引”刷新结果。

### 配置文件夹搜索

在“设置”的“文件夹索引目录”中，每行填写一个根目录，例如：

```text
D:\Projects
E:\Documents
```

Lumen 仅索引文件夹，默认最多递归三层；保存配置后会在后台刷新索引。为控制索引规模和避免循环，`.git`、`.svn`、`node_modules`、`bin`、`obj`、隐藏/系统目录及链接目录会被跳过。

### 配置 Quicklinks

Quicklink 每行格式为 `名称 | URL | 别名`，别名可省略：

```text
GitHub | https://github.com
Google | https://www.google.com/search?q={query} | g
```

对于带 `{query}` 的 Quicklink，输入 `g Lumen Launcher` 即可用 Google 搜索该文本。Quicklink 目前仅接受 `http` 或 `https` 地址。

## 构建与运行

前置要求：Windows 和 [.NET 10 SDK](https://dotnet.microsoft.com/download)。不需要完整 Visual Studio。

```powershell
dotnet restore Lumen.sln
dotnet build Lumen.sln -c Release
dotnet test Lumen.sln -c Release
dotnet run --project src/Lumen.App
```

## 发布

生成 Windows x64 自包含版本：

```powershell
dotnet restore src/Lumen.App/Lumen.App.csproj -r win-x64
dotnet publish src/Lumen.App/Lumen.App.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

GitHub Actions 会在 `main` 分支推送和 Pull Request 时运行测试，并构建 `win-x64` 与 `win-arm64` 产物。推送形如 `v0.1.0` 的标签时，会创建草稿 GitHub Release 并上传 ZIP 安装包。

## 本地数据与隐私

首次运行会在以下位置创建数据：

- 设置：`%LOCALAPPDATA%\Lumen\settings.json`
- SQLite 数据库：`%LOCALAPPDATA%\Lumen\lumen.db`

所有索引、设置和使用记录均保存在本地；Lumen 不包含遥测或网络同步功能。

## 项目结构

- `src/Lumen.Core`：领域模型、接口、文本匹配和搜索聚合。
- `src/Lumen.Infrastructure`：SQLite、设置、应用/文件夹扫描、搜索提供程序和执行器。
- `src/Lumen.App`：WPF 用户界面、托盘、全局快捷键和索引协调。
- `src/Lumen.Indexer`：应用名称拼音索引器。
- `src/Lumen.Tests`：核心算法与 Quicklink 测试。

## 开机自启动

在托盘菜单勾选“开机自启动”。Lumen 优先创建当前用户登录时运行的计划任务；若设备策略禁止创建任务，则回退到当前用户的 Windows `Run` 注册表项。
