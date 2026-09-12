# DLSS FrameGen Enabler & DLSS5

一款用于开启游戏DLSS5和多帧生成（Frame Generation）功能的Windows桌面工具。
A Windows desktop tool for enabling DLSS5 and Frame Generation in games.

## 功能特性 / Features

### 多帧生成 / Frame Generation
- **RTX 40系 / RTX 40 Series**：支持经典模式和高级模式 / Classic and Advanced modes
- **RTX 30系 / RTX 30 Series**：支持30系专属多帧生成补丁 / 30-series exclusive MFG patch
- **RTX 20系 / RTX 20 Series**：支持20系专属多帧生成补丁 / 20-series exclusive MFG patch
- **RE引擎游戏 / RE Engine Games**：支持RE引擎专属多帧生成 / RE Engine exclusive MFG
- **赛博朋克2077 / Cyberpunk 2077**：支持2077专用多帧生成补丁 / 2077 exclusive MFG patch
- **老驱动 / Older Drivers**：支持老驱动多帧生成 / MFG for older drivers

### DLSS5
- **通用DLSS5 / Universal DLSS5**：支持NVIDIA和AMD（7000系/9000系）显卡 / Supports NVIDIA and AMD (7000/9000 series) GPUs
- **RE引擎DLSS5 / RE Engine DLSS5**：RE引擎游戏专属DLSS5补丁 / RE Engine exclusive DLSS5 patch
- **DX9 32位DLSS5 / DX9 32-bit DLSS5**：支持DX9 32位老游戏 / Supports DX9 32-bit older games
- **DX9 64位DLSS5 / DX9 64-bit DLSS5**：支持DX9 64位游戏 / Supports DX9 64-bit games
- **燕云十六声 / Where Winds Meet**：专属DLSS5补丁 / Exclusive DLSS5 patch

### 其他功能 / Other Features
- 自动扫描Steam/EA/育碧/GOG游戏库 / Auto-scan Steam/EA/Ubisoft/GOG game libraries
- 手动添加游戏（支持选择游戏exe）/ Manual game addition (supports selecting game exe)
- 游戏列表管理（右键菜单、移除列表、打开目录）/ Game list management (context menu, remove, open directory)
- 排错功能（检测并替换游戏文件）/ Troubleshooting (detect and replace game files)
- 一键还原/无责还原 / One-click restore / unconditional restore
- 配置名称修改 / Config name modification
- 显卡自动检测与手动选择（优先独立显卡）/ Auto GPU detection and manual selection (dedicated GPU priority)
- 中英文语言切换 / Chinese-English language switching
- 深色/浅色模式 / Dark/Light mode
- 界面材质选择（默认/云母/亚克力/纯透明/自定义壁纸）/ Backdrop selection (Default/Mica/Acrylic/Transparent/Custom wallpaper)
- 深浅色切换动画 / Theme switching animation
- 页面切换动画 / Page transition animation
- 自动检查更新（支持GitHub直接下载、断点续传、自动安装Beta）/ Auto update check (GitHub direct download, resume, auto-install Beta)
- 记住游戏列表 / Remember game list
- 拖动窗口性能优化 / Window dragging performance optimization

## 系统要求 / System Requirements

- Windows 10/11
- .NET 10 Runtime
- Visual Studio 2022（用于编译 / for building）
- Windows App SDK 2.4.0

## 编译说明 / Build Instructions

### 前置要求 / Prerequisites
- Visual Studio 2022 或更高版本 / Visual Studio 2022 or later
- .NET 10 SDK
- Windows App SDK 工作负载 / Windows App SDK workload

### 编译步骤 / Build Steps
1. 克隆仓库 / Clone the repository
2. 打开 `DLSSFrameGenEnabler_WinUI3.csproj` / Open the project file
3. 选择 Release 配置 / Select Release configuration
4. 生成解决方案 / Build the solution

### 补丁文件 / Patch Files
由于补丁文件体积较大（约2.3GB），未包含在仓库中。编译后需要：
Due to the large size of patch files (~2.3GB), they are not included in the repository. After building:
1. 从发布版获取完整的 `Patches` 目录 / Get the complete `Patches` directory from the release
2. 将 `Patches` 目录放到编译输出目录中（与exe同级）/ Place the `Patches` directory in the build output directory (same level as exe)
3. 软件运行时会自动读取所需补丁 / The software will automatically read required patches at runtime

## 项目结构 / Project Structure

```
DLSSFrameGenEnabler_WinUI3/
├── Assets/              # 资源文件（图标、图片等）/ Assets (icons, images)
├── Models/              # 数据模型 / Data models
├── Pages/               # 页面（主页、设置、关于、更新内容等）/ Pages (Home, Settings, About, Update Log, etc.)
├── Patches/             # 补丁文件（需自行添加）/ Patch files (add manually)
├── Properties/          # 项目属性 / Project properties
├── Services/            # 服务（补丁安装、翻译、设置等）/ Services (patching, translation, settings)
├── App.xaml             # 应用入口 / App entry point
├── App.xaml.cs
├── MainWindow.xaml      # 主窗口 / Main window
├── MainWindow.xaml.cs
└── DLSSFrameGenEnabler_WinUI3.csproj
```

## 技术栈 / Tech Stack

- **语言 / Language**：C#
- **框架 / Framework**：WinUI 3 (Windows App SDK 2.4.0)
- **目标框架 / Target Framework**：.NET 10
- **目标平台 / Target Platform**：Windows 10.0.26100.0

## 免责声明 / Disclaimer

本软件仅供学习和研究使用。使用本软件修改游戏文件可能导致游戏无法正常运行或被反作弊系统检测。使用前请备份游戏文件，使用者需自行承担风险。
This software is for learning and research purposes only. Modifying game files with this software may cause games to malfunction or be detected by anti-cheat systems. Please backup game files before use, and users assume all risks.

- 米哈游系列游戏因反作弊较为严苛，暂不适配 / miHoYo games are not currently supported due to strict anti-cheat
- Vulkan渲染游戏暂不适配 / Vulkan-rendered games are not currently supported
- 育碧与EA游戏可能存在兼容性问题 / Ubisoft and EA games may have compatibility issues

## 致谢 / Credits

感谢以下社区项目和作者的开源贡献：
Thanks to the following community projects and authors for their open-source contributions:
- ReShade
- DLSS5 Feeder
- dgVoodoo2
- RTX 40 MFG Unlock
- Magpie (大力喜鹊)
- 以及所有为DLSS和帧生成技术做出贡献的社区开发者 / And all community developers who have contributed to DLSS and frame generation technology

## 许可证 / License

MIT License
