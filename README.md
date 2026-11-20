# DLSS FrameGen Enabler & DLSS5

一款用于开启游戏 DLSS5 和多帧生成（Frame Generation）功能的 Windows 桌面工具。
A Windows desktop tool for enabling DLSS5 and Frame Generation in games.

- ## 下载哪个版本？

- 稳定版用户：先下载 **V1.30 基线完整包**，再应用最新稳定版增量包。 / Stable version users: Download the **V1.30 baseline full package** first, then apply the latest stable version incremental package.https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases/tag/V1.30
- 测试版用户：先下载 **V1.61-Beta 基线完整包**，再应用最新测试版增量包。 / Beta users: Download the **V1.61-Beta baseline full package** first, then apply the latest beta incremental package.https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases#release-V1.61-beta
- 新用户：需要下载 **V1.30 基线完整包**或者下载 **V1.61-Beta 基线完整包**。 / New users: Download either the **V1.30 Baseline Full Package** or the **V1.61-Beta Baseline Full Package**.https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases#release-V1.30 **or** https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases#release-V1.61-beta

- 当前稳定版 / Current stable version：**V1.40.0.0**
- 主程序 / Main executable：`WebUI.exe`
- 更新通道 / Update channel：正式版 `version.json`
- 稳定版源码分支 / Stable source branch：`master`
- 本版本已并入 HSF 帧生成、精确渲染 API 识别和 Source 1 DX9 `bin` 安装；Smooth Motion 仍保留 Beta 标识。

## V1.40 更新内容 / V1.40 Changelog

1. **HSF 帧生成并入稳定版 / HSF frame generation**
   基于 ReShade add-on 捕获画面插帧，把游戏画面、深度和运动数据交给 NVIDIA DLSS-G，支持 DX9 / DX11 / DX12 / Vulkan，最高 6X；MFG-Unlocker 可在 HSF helper 的 mods 目录解锁 3X-6X。
   HSF is a ReShade add-on that feeds the game image, depth and motion data to NVIDIA DLSS-G. Supports DX9 / DX11 / DX12 / Vulkan, up to 6X; MFG-Unlocker unlocks 3X-6X through the HSF helper mods folder.

2. **HSF 菜单语言 / HSF menu language**
   中文界面安装汉化版 add-on，英文界面安装 HSF 0.9.9 原版；「跟随软件语言」保持 HSF 菜单与软件界面一致，已安装游戏可以在卡片上切换。
   Chinese installs the translated add-on; English installs the original HSF 0.9.9 add-on. “Follow app language” keeps the menu consistent, and installed games can switch from their card.

3. **精确渲染 API 识别 / Accurate render API detection**
   改为读取 PE 普通导入表和延迟导入表，识别 DX9 / DX11 / DX12 / Vulkan / OpenGL；不再用 exe 位数或目录里是否有 `d3d9.dll` 猜测，ReShade 自己放的代理 DLL 不会再把 DX11 误判成 DX9。
   API detection now reads the PE import and delay-import tables to identify DX9 / DX11 / DX12 / Vulkan / OpenGL. It no longer guesses from exe bitness or the presence of `d3d9.dll`.

4. **Source 1 DX9 自动安装到 bin / Source 1 DX9 installs into bin**
   识别 `bin\engine.dll`、`materialsystem.dll`、`shaderapidx9/11.dll` 和 Source 游戏目录；Source 1 DX9 游戏自动把 ReShade / HSF 安装到 `bin`，游戏卡片显示实际安装目录，并允许在安装前选择「自动识别 / 游戏根目录 / bin 子目录」。
   Detects Source engine markers and automatically installs ReShade / HSF into `bin` for Source 1 DX9 games. The card shows the actual install folder and allows Auto / Game root / bin overrides.

5. **安装记录与旧版兼容 / Install record and legacy compatibility**
   `install.json` 新增 `InstallDir`；状态检测、安装、还原、LaunchPad、MFG-Unlocker 和 ReShade 代理检测统一使用记录目录。旧版根目录安装仍能还原，并提示还原后重新安装会自动使用 `bin`。
   `install.json` now records `InstallDir`; status, install, restore, LaunchPad, MFG-Unlocker and ReShade proxy checks all use it. Older root-folder installs still restore correctly and show a migration hint.

6. **API 兼容页 / API compatibility page**
   新增 DX9 · 起源引擎条目，说明 ReShade / HSF 必须装到 `bin`；DX11、DX12 与 Vulkan 行为不变。
   Added a DX9 / Source engine entry explaining that ReShade / HSF must be installed into `bin`; DX11, DX12 and Vulkan behavior is unchanged.

7. **启动更新提示顺序 / Startup update prompt order**
   自动检查更新时先等「温馨提示」或测试指引关闭，再显示新版本弹窗，避免更新提示被启动提示覆盖。
   Automatic update checks now wait until startup notices are closed before showing the new-version dialog, so the prompt is no longer replaced.

## V1.30 更新内容 / V1.30 Changelog

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

### HSF 帧生成
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

## 使用提醒 / Usage Notes

- 先在游戏内开启 DLSS / 帧生成相关选项，再关闭游戏，最后安装补丁。
- 安装或还原前建议备份游戏文件。
- SM86 与 MFG 只能二选一；切换前先还原当前方案。
- GITC DLSS5 2.0.0 与 MFG 1.7.1 同时开启存在崩溃风险。
- Smooth Motion 仍为 Beta；RTX 20/30 的补丁兼容性以实际游戏测试为准。
- Source 1 DX9 游戏的 ReShade / HSF 会自动装到 in；如果卡片显示旧版根目录安装，先还原再重新安装。
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
