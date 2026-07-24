# Lumen

Lumen 是一个基于 .NET 10 / WPF 的本地 Windows 启动器 MVP：按需索引开始菜单、桌面、App Paths、配置的便携 EXE，以及本地 Chrome 历史，并在一个居中的深色搜索窗口内统一检索。

## 构建与运行

只需安装 .NET 10 SDK，不需要完整 Visual Studio：

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Lumen.App
```

## 项目结构

- `Lumen.Core`：领域模型、接口、文本匹配、候选评分和搜索聚合。
- `Lumen.Infrastructure`：JSON 设置、SQLite、应用与便携程序扫描、Chrome Profile/History 导入、执行器和搜索提供者。
- `Lumen.App`：WPF / MVVM 搜索窗口与 DI 组合根。
- `Lumen.Tests`：时间转换、评分及匹配算法测试。

## 配置与隐私

首次运行会创建 `%LOCALAPPDATA%\Lumen\settings.json`。在 `portableApplicationDirectories` 中加入如 `D:\Tools` 的目录。数据库保存在 `%LOCALAPPDATA%\Lumen\lumen.db`。

Chrome History 从本地 Chrome Profile 的 `History` SQLite 数据库复制后导入本地数据库；不会上传网络、不采集遥测。可将 `chromeHistory.enabled` 改为 `false` 关闭它。

## 开机自启动

在 Lumen 托盘菜单勾选“开机自启动”。Lumen 会创建名为 `Lumen` 的计划任务，在当前用户登录时以普通权限在交互会话启动；若设备策略禁止创建计划任务，会自动改用当前用户的 Windows `Run` 注册表项。


## 当前限制与路线

该 MVP 已有后台扫描、取消、SQLite 事务、搜索并发聚合、Chrome 多 Profile 导入、应用启动和 WPF 搜索界面。下一步应补齐 Win32 `RegisterHotKey`/托盘、`.lnk` 目标解析、PATH 与 MSIX 发现、异步图标缓存、索引重建命令，以及 Windows 实机上的 DPI/失焦/快捷键冲突验证。
