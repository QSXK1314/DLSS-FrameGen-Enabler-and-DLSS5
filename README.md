# DLSS FrameGen Enabler & DLSS5

一款用于开启游戏 DLSS5 和多帧生成（Frame Generation）功能的 Windows 桌面工具。
A Windows desktop tool for enabling DLSS5 and Frame Generation in games.

- 当前稳定版 / Current stable version：**V1.20.0.0**
- 稳定版源码分支 / Stable source branch：`master`
- 测试版与 WebUI 版为独立发布，不在本仓库的稳定版源码内 / Beta and WebUI builds are released separately.

## V1.20 更新内容 / V1.20 Changelog

1. **GITC DLSS5 还原不再误删游戏原生文件**
   移除了固定删除 `dxgi.dll`、`d3d9.dll`、`vulkan.dll`、`dinput8.dll`、`ReShade.ini` 和整个 `Fonts` 目录的逻辑，改为按安装时生成的备份清单精确还原，只删除本软件真正新增的文件。
   GITC DLSS5 restore now follows the install backup manifest instead of deleting known filenames and the whole `Fonts` folder, so native game files are no longer removed.

2. **一键还原只处理实际已开启的功能**
   不再无差别执行多帧生成、GITC DLSS5 的全部还原流程；只还原当前游戏实际开启过的功能，未安装的补丁不会被误动。
   One-click restore now only restores features that were actually enabled for the selected game.

3. **GITC 安装覆盖前先备份**
   覆盖游戏目录中已存在的同名文件前会先写入备份，还原时可以恢复原始文件，而不是直接删除。
   Existing files are backed up before being overwritten during GITC installation.

4. **RE 引擎游戏可同时开启多帧生成和 GITC DLSS5**
   移除了启动时强制“DLSS5 与多帧生成只能开其一”的限制。RE 引擎游戏现在可以同时开启两项功能。
   RE Engine games can now enable Multi Frame Generation and GITC DLSS5 at the same time.

5. **GITC 按钮状态重启后保持**
   修复按钮显示“开启 DLSS5”而列表显示已开启的状态不同步问题，也避免重复安装产生 V2/V3 备份。
   The GITC button state now persists after restart, and duplicate installs no longer create V2/V3 backups.

6. **GITC-Uplift 残留清理**
   GITC DLSS5 还原后会清理 `GITC-Uplift` 文件夹及其中的 `nvngx_dlssnr.dll`，旧版本安装留下的残留也会一并处理。
   The `GITC-Uplift` folder and its `nvngx_dlssnr.dll` are cleaned up on restore, including leftovers from older installs.

7. **状态列修复**
   RE 引擎游戏同时开启多帧生成和 DLSS5 时，状态列不再错误显示为“只开启 DLSS5”。
   The status column no longer incorrectly shows “DLSS5 only” when both features are enabled.

8. **备份上限与说明统一**
   备份上限提示与实际阈值统一为 **1024MB**；使用说明删除了已过时的“RE 引擎两者冲突”和“A 卡 7000/9000 系支持”描述。
   The backup limit hint and actual threshold are both 1024MB; outdated notes were removed from the usage guide.

## 功能特性 / Features

### 多帧生成 / Frame Generation
- **RTX 20/30/40/50 系统一安装流程**：使用 MFGUnlocker 方案，安装文件更少，兼容性更集中。
- **RTX 40 系经典 / 高级模式**：经典模式面向老驱动（616.56 之前），高级模式面向新驱动与 310.9 及以上帧生成版本。
- **RE 引擎多帧生成**：支持 RE 引擎游戏的专属安装流程，可与 GITC DLSS5 共存。
- **赛博朋克 2077**：提供 2077 专用多帧生成补丁。
- **老驱动多帧生成**：为旧驱动环境保留独立分支。

### DLSS5 / GITC DLSS5
- **GITC 统一方案**：DLSS5 使用 GITC 补丁体系，支持 DX11/DX12 与 Vulkan 安装分支，具体可用性取决于游戏渲染路径和补丁兼容性。
- **RE 引擎 DLSS5**：RE 引擎游戏提供专属安装适配。
- **DX9 32 位 / 64 位**：32 位通过 dgVoodoo2 + DLSS5 Feeder 转译，64 位使用直接加载方案。
- **燕云十六声**：提供专属 DLSS5 适配。
- **AMD 显卡过渡方案**：本软件不再直接为 AMD 显卡安装补丁；安装包内提供 NeuralHub v0.8.0 与所需 DLL，检测到 AMD 显卡时会弹出完整操作指引。

### 其他功能 / Other Features
- 自动扫描 Steam / EA / Ubisoft / GOG 游戏库。
- 手动添加游戏，支持直接选择游戏 exe。
- 通过 Win32 API 枚举系统进程，检测正在运行的游戏并定位真实 exe 路径。
- 游戏列表管理：右键菜单、移除列表、打开目录、修改配置名、备注标签。
- 排错功能：检测并替换缺失或异常的补丁文件。
- 一键还原：只还原实际开启过的功能。
- 显卡自动检测与手动选择，优先独立显卡。
- 中英文切换、深色/浅色模式、云母/亚克力/纯透明/自定义壁纸。
- 深浅色切换动画、页面切换动画。
- 自动检查更新，支持 GitHub 直接下载、断点续传与 Beta 安装。
- 记住游戏列表、窗口大小与使用前说明的显示状态。
- 游戏名称数据库：根据 exe 名称识别更准确的中英文游戏名。

## 系统要求 / System Requirements

- Windows 10 1809 或更高版本，推荐 Windows 11。
- .NET 10 Runtime。
- Visual Studio 2022 或更高版本（仅编译需要）。
- Windows App SDK 2.4.0。

## 编译说明 / Build Instructions

### 前置要求 / Prerequisites
- Visual Studio 2022 或更高版本。
- .NET 10 SDK。
- Windows App SDK 工作负载。

### 编译步骤 / Build Steps
1. 克隆仓库。
2. 打开 `DLSSFrameGenEnabler_WinUI3.csproj`。
3. 选择 `Release` 配置。
4. 生成解决方案。

### 补丁文件 / Patch Files
补丁文件体积较大，未包含在源码仓库中。编译后需要：

1. 从发布版获取完整的 `Patches` 目录。
2. 将 `Patches` 放到编译输出目录中，与 exe 同级。
3. 软件运行时会自动读取对应补丁。

The patch files are large and are not included in this source repository. After building, place the complete `Patches` directory next to the executable.

## 项目结构 / Project Structure

```text
DLSSFrameGenEnabler_WinUI3/
├── Assets/              # 图标、图片等资源 / Icons and images
├── Models/              # 数据模型 / Data models
├── Pages/               # 主页、设置、关于、更新内容、使用说明 / Pages
├── Patches/             # 补丁说明（完整补丁从发布版获取）/ Patch notes; full patches come with releases
├── Properties/          # 项目属性 / Project properties
├── Services/            # 补丁安装、扫描、翻译、设置、游戏名数据库 / Services
├── App.xaml             # 应用入口 / App entry
├── App.xaml.cs
├── MainWindow.xaml      # 主窗口 / Main window
├── MainWindow.xaml.cs
└── DLSSFrameGenEnabler_WinUI3.csproj
```

## 技术栈 / Tech Stack

- **语言 / Language**：C#
- **UI 框架 / UI framework**：WinUI 3（Windows App SDK 2.4.0）
- **目标框架 / Target framework**：.NET 10
- **目标平台 / Target platform**：Windows 10.0.26100.0
- **发布方式 / Packaging**：自包含 WinUI 3 桌面应用，无需安装 Windows App SDK Runtime。

## 使用提醒 / Usage Notes

- 先在游戏内开启 DLSS / 帧生成相关选项，再关闭游戏，最后安装补丁。
- 安装或还原前建议备份游戏文件。
- 米哈游系列游戏因反作弊较严，暂不适配。
- 育碧与 EA 游戏可能存在兼容性问题。
- GITC DLSS5 提供 Vulkan 安装分支，但并非所有 Vulkan 游戏都能直接使用；需按游戏实际渲染 API 选择。
- 本软件不再直接为 AMD 显卡安装补丁；A 卡用户请按软件内弹出的 NeuralHub 指引操作。

## 免责声明 / Disclaimer

本软件仅供学习和研究使用。使用本软件修改游戏文件可能导致游戏无法正常运行或被反作弊系统检测。使用前请备份游戏文件，使用者需自行承担风险。

This software is for learning and research purposes only. Modifying game files may cause games to malfunction or be detected by anti-cheat systems. Backup game files before use; users assume all risks.

## 致谢 / Credits

感谢以下社区项目和作者的开源贡献：

- ReShade
- DLSS5 Feeder
- dgVoodoo2
- RTX 40 MFG Unlock
- MFGUnlocker
- NeuralHub
- Magpie（大力喜鹊）
- 以及所有为 DLSS 和帧生成技术做出贡献的社区开发者

## 许可证 / License

MIT License
