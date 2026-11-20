# DFGE WebUI 测试版 / DFGE WebUI Beta

一款基于 WebView2 + WebUI 界面的 DLSS5 / 多帧生成 / Smooth Motion 开启工具。
A WebView2-based tool for enabling DLSS5, Frame Generation and Smooth Motion.

- - ## 下载哪个版本？

- 稳定版用户：先下载 **V1.30 基线完整包**，再应用最新稳定版增量包。 / Stable version users: Download the **V1.30 baseline full package** first, then apply the latest stable version incremental package.https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases/tag/V1.30
- 测试版用户：先下载 **V1.61-Beta 基线完整包**，再应用最新测试版增量包。 / Beta users: Download the **V1.61-Beta baseline full package** first, then apply the latest beta incremental package.https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases#release-V1.61-beta
- 新用户：需要下载 **V1.30 基线完整包**或者下载 **V1.61-Beta 基线完整包**。 / New users: Download either the **V1.30 Baseline Full Package** or the **V1.61-Beta Baseline Full Package**.https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases#release-V1.30 **or** https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases#release-V1.61-beta

- 当前测试版 / Current beta version：**V1.62.0.0**
- 测试版源码分支 / Beta source branch：`beta`
- 稳定版源码分支 / Stable source branch：`master`
- 当前为测试版，功能和界面仍可能调整 / This is a beta build; features and UI may change.

## V1.62 更新内容 / V1.62 Changelog

1. **精确渲染 API 识别 / Accurate render API detection**
   改为读取 PE 普通导入表和延迟导入表，识别 DX9 / DX11 / DX12 / Vulkan / OpenGL；不再用 exe 位数或目录里是否有 `d3d9.dll` 猜测，ReShade 自己放的代理 DLL 不会再把 DX11 误判成 DX9。
   API detection now reads the PE import and delay-import tables to identify DX9 / DX11 / DX12 / Vulkan / OpenGL. It no longer guesses from exe bitness or the presence of `d3d9.dll`, so ReShade’s own proxy cannot make a DX11 game look like DX9.

2. **Source 1 DX9 自动安装到 bin / Source 1 DX9 installs into bin**
   识别 `bin\engine.dll`、`materialsystem.dll`、`shaderapidx9/11.dll` 和 Source 游戏目录；Source 1 DX9 游戏自动把 ReShade / HSF 安装到 `bin`，游戏卡片显示实际安装目录，并允许在安装前选择「自动识别 / 游戏根目录 / bin 子目录」。
   Detects Source engine markers and automatically installs ReShade / HSF into `bin` for Source 1 DX9 games. The game card shows the actual install folder and allows Auto / Game root / bin overrides before installation.

3. **安装记录与旧版兼容 / Install record and legacy compatibility**
   `install.json` 新增 `InstallDir`；状态检测、安装、还原、LaunchPad、MFG-Unlocker 和 ReShade 代理检测统一使用记录目录。旧版根目录安装仍能还原，并提示还原后重新安装会自动使用 `bin`。
   `install.json` now records `InstallDir`; status, install, restore, LaunchPad, MFG-Unlocker and ReShade proxy checks all use it. Older root-folder installs still restore correctly and show a migration hint.

4. **API 兼容页 / API compatibility page**
   新增 DX9 · 起源引擎条目，说明 ReShade / HSF 必须装到 `bin`；DX11、DX12 与 Vulkan 行为不变。
   Added a DX9 / Source engine entry explaining that ReShade / HSF must be installed into `bin`; DX11, DX12 and Vulkan behavior is unchanged.

## V1.61 更新内容 / V1.61 Changelog

1. **HSF 菜单语言 / HSF menu language**
   中文界面安装汉化版 HSF add-on，英文界面安装 HSF 0.9.9 原版英文 add-on；「跟随软件语言」保持 HSF 菜单与软件界面一致。
   Chinese installs the translated HSF add-on; English installs the original HSF 0.9.9 add-on. “Follow app language” keeps the HSF menu consistent with the UI.

2. **ReShade 外壳语言 / ReShade shell language**
   中文写入 `zh-CN` + MiSans 字体；英文写入 `en-US`，不再强制给外国用户安装中文字体。
   Chinese writes `zh-CN` + MiSans; English writes `en-US` without forcing the Chinese font.

3. **已安装游戏切换语言 / Switch language for existing installs**
   游戏卡片显示当前 HSF 菜单语言；语言不一致时提供「切换菜单语言」按钮，先按安装记录还原旧文件，再按当前语言重新安装。
   Game cards show the current HSF menu language. When it differs from the setting, a “Switch menu language” button restores the old install first, then reinstalls with the selected language.

## V1.60 更新内容 / V1.60 Changelog

1. **新增 HSF 帧生成**
   基于 ReShade add-on 捕获画面插帧，把游戏画面、深度和运动数据交给 NVIDIA DLSS-G，支持 DX9 / DX11 / DX12 / Vulkan，最高 6X；MFG-Unlocker 可在 HSF helper 的 mods 目录解锁 3X-6X。
   HSF is a ReShade add-on that feeds the game image, depth and motion data to NVIDIA DLSS-G. Supports DX9 / DX11 / DX12 / Vulkan, up to 6X; MFG-Unlocker unlocks 3X-6X through the HSF helper mods folder.

2. **帧生成mod 合并入口**
   Smooth Motion 与 HSF 合并为一个导航入口，进入后先选择「英伟达 Smooth Motion」或「HSF 帧生成」，子页面可返回选择页。
   Smooth Motion and HSF are merged into one navigation entry with a two-choice landing page; each sub-page can return to the selector.

3. **HSF API 兼容性说明**
   页面标注 DX11、DX12 原生 DLSS、DX12 无 DLSS、Vulkan 的差异；Vulkan 游戏建议使用窗口模式。
   The HSF page documents DX11, native-DLSS DX12, DX12 without DLSS and Vulkan; Vulkan games should use windowed mode.

4. **LaunchPad 画质提示**
   安装 LaunchPad 后的光流质量优于默认 NVOF，鬼影会明显减少；游戏卡片按钮改为「安装 LaunchPad（推荐）」。
   LaunchPad optical flow is better than the default NVOF and significantly reduces ghosting; the game-card button is now “Install LaunchPad (recommended)”.

5. **下载进度与任务数字**
   HSF 的 LaunchPad 下载会显示在「下载」页面，包含真实下载进度、速度与已下载/总大小；导航栏「下载」会用高亮数字显示当前正在进行的下载任务数量。
   LaunchPad downloads appear on the Downloads page with real progress, speed and downloaded/total size; the Downloads navigation item shows the active task count as a highlighted number.

## V1.50 更新内容 / V1.50 Changelog

1. **集成 Smooth Motion**
   为不支持原生帧生成的游戏提供 AI 插帧；导航栏已增加 `Beta` 标识。
   Smooth Motion provides AI frame interpolation for games without native frame generation; the navigation item is marked `Beta`.

2. **RTX 40/50 与 RTX 20/30 分流**
   RTX 40/50 走官方驱动支持；RTX 20/30 使用 `Patches\SmoothMotion` 内置补丁。
   RTX 40/50 use the official driver path; RTX 20/30 use the built-in patch under `Patches\SmoothMotion`.

3. **NVAPI profile 开关**
   Smooth Motion 页面可以为游戏列表逐项开关 Smooth Motion，并选择 `DX12 + DX11 + Vulkan` 或单一 API。
   The Smooth Motion page can toggle Smooth Motion for each game and select combined or single APIs.

4. **单 API 写入权限修复**
   单独选择 DX12 / DX11 / Vulkan 时，受保护的 `Enabled APIs` 写入会在需要时通过管理员权限的短命进程完成，避免 `NVAPI_137` 权限错误。
   When a single API is selected, the protected `Enabled APIs` write is performed by a short-lived elevated process if needed, avoiding `NVAPI_137`.

5. **补丁致谢**
   SM86 多帧生成补丁与 Smooth Motion Mod 的社区作者已在软件“关于”页致谢。
   Community authors of the SM86 patch and Smooth Motion Mod are credited on the About page.

## 功能特性 / Features

### 多帧生成 / Frame Generation
- RTX 20/30/40/50 系统一安装流程。
- RTX 40 系经典模式与高级模式；高级模式可选 MFG 或 SM86。
- SM86 方案不替换游戏原文件，自带内核优化，可搭配 DLSS 超分 + XeSS 补帧。
- SM86 与 MFG 只能二选一开启。
- RE 引擎、赛博朋克 2077、老驱动分支保留。

### DLSS5 / GITC DLSS5
- GITC DLSS5 1.7.0 与 2.0.0 双版本可选。
- 2.0.0 与 MFG 1.7.1 同时开启有崩溃风险，软件会提示但仍可继续。
- 支持 DX11/DX12 游戏、RE 引擎、DX9 32/64 位与燕云十六声。
- AMD 显卡不再由本软件直接安装补丁；检测到 A 卡时显示 NeuralHub v0.8.0 操作指引。

### Smooth Motion（Beta）
- 为不支持原生帧生成的游戏提供 AI 插帧。
- RTX 40/50 使用官方驱动能力；RTX 20/30 使用内置 Smooth Motion Mod。
- 支持逐游戏开关、API 选择、状态检测与日志查看。
- `Patches\SmoothMotion` 已随源码包含 RTX20 / RTX30 的 `version.dll`。

### HSF 帧生成（Beta）
- 在 HSF 页面按游戏一键安装 / 还原 ReShade 6.8 add-on + HSF。
- Vulkan / DX11 / 无 DLSS 的 DX12 游戏可一键安装 iMMERSE LaunchPad 光流源（从官方仓库下载，不随软件分发）。
- RTX 20/30/40 可一键把 MFG-Unlocker 放入 `HSF_streamline\mods`，解锁 3X-6X；DX12 原生路线仍按官方说明放到游戏 exe 旁。

### WebUI 与其他功能
- WebView2 前端界面，深浅色、卡片列表、右键菜单、设置与关于页。
- 游戏库扫描、手动添加游戏、进程检测、配置名修改、备注标签。
- 一键开启 / 还原、备份管理、补丁排错、日志导出。
- 中英文切换、自动检查更新、GitHub 下载与断点续传。

## 系统要求 / System Requirements

- Windows 10 1809 或更高版本，推荐 Windows 11。
- .NET 8 Runtime（或使用自包含发布）。
- Microsoft Edge WebView2 Runtime。
- Visual Studio 2022（仅编译需要）。

## 编译说明 / Build Instructions

### 直接编译 / Build
```powershell
dotnet build .\WebUI测试版.csproj -c Release
```

### 自包含发布 / Self-contained publish
```powershell
dotnet publish .\WebUI测试版.csproj -c Release -r win-x64 --self-contained true
```

发布输出中的 `WebUI_Beta.exe` 需要与 `wwwroot`、`Patches` 保持同级目录结构。
The published `WebUI_Beta.exe` expects `wwwroot` and `Patches` beside it.

## 项目结构 / Project Structure

```text
WebUI测试版/
├── Models/              # 数据模型 / Data models
├── Services/            # 补丁、扫描、Smooth Motion、设置、翻译 / Services
├── Patches/
│   ├── SmoothMotion/    # RTX20 / RTX30 Smooth Motion 补丁 / Built-in patches
│   └── HSF/             # HSF、ReShade、字体、MFG-Unlocker 与许可 / HSF pack
├── wwwroot/             # HTML / CSS / JS / 图标 / WebUI frontend
├── App.cs               # 应用入口 / App entry
├── MainForm.cs          # WinForms 主窗口与 WebView2 桥接 / Host and bridge
├── Program.cs
├── WebUI测试版.csproj
└── app.ico
```

## 使用提醒 / Usage Notes

- 先在游戏内开启 DLSS / 帧生成相关选项，再关闭游戏，最后安装补丁。
- 安装或还原前建议备份游戏文件。
- SM86 与 MFG 只能二选一；切换前先还原当前方案。
- GITC DLSS5 2.0.0 与 MFG 1.7.1 同时开启存在崩溃风险。
- Smooth Motion 仍为 Beta；RTX 20/30 的补丁兼容性以实际游戏测试为准。
- 米哈游系列游戏因反作弊较严，暂不适配。
- 本软件不再直接为 AMD 显卡安装补丁；A 卡用户请按软件内 NeuralHub 指引操作。

## 致谢 / Credits

- Universal RTX 40 MFG Unlock（社区版）
- MFG-Unlocker（Michael Robles / dashdogy，MIT License）
- HSF 0.9.9（Capt-Hunter，PolyForm Noncommercial License 1.0.0）
- iMMERSE LaunchPad（Pascal Gilcher / Marty McFly，按原作者许可仅从官方 GitHub 下载，不随本软件分发）
- ReShade（Crosire）
- RE Framework / DLSS5 on RE（社区版）
- GITC DLSS5（ghostinthecamera）
- DLSSG for SM86 / SM86 多帧生成补丁（sdli1995、pipotoufikxyz-lgtm 及社区分支）
- Smooth Motion Mod（ItsAdeline / NVSmooth30；RTXMFG SmoothMotion-next 社区版）
- Magpie（大力喜鹊）

## 免责声明 / Disclaimer

本软件仅供学习交流使用，请勿用于商业用途。不建议在网游和带反作弊的游戏上使用；修改游戏文件可能导致封号，本软件概不负责。
This software is for learning and research only. Modifying game files may lead to bans in online or anti-cheat games; use at your own risk.

## 许可证 / License

MIT License
