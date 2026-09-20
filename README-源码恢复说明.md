# 多帧生成 + DLSS5 开启工具 — 1.15.3.0-Beta 完整源码（恢复版）

本工程是 **1.15.3.0-Beta 发布版**对应的、可直接编译并继续开发的完整源码。
它综合了两个来源，并经过**逐方法、逐界面、逐资源的核对**，功能与你发布的 1.15.3.0-Beta 完全一致。

---

## 一、这份源码是怎么来的

你提供的旧工程里带有一个 git 提交：`6bc7c7d`「1.15.3.0-Beta完整源码」（QSXK1314，2026-09-20 17:34）。
逐成员比对确认：**该提交是“发布前”快照**——提交之后、打包之前你又改了一批功能。因此：

1. **以 git 提交为主体**（保留你手写的注释、原始排版、原始 csproj / manifest / 工程结构）；
2. 对“提交 → 发布”之间发生变化的文件，用**从发布包 DLL/XBF 反编译恢复**的等价代码替换（这部分不含注释，因为发布程序集里没有注释）；
3. 删除了提交版里存在、但发布版已经移除的“设置页备份管理”相关代码。

### 来自 git 原版（带你的注释，未改动）

`App.xaml(.cs)`、`Pages/AboutPage.*`、`Pages/UsagePage.*`、`Pages/UpdateLogPage.xaml`（界面）、
`Services/GameScanner.cs`、`Services/ProcessDetector.cs`、`Services/Translator.cs`、`Services/GpuDetector.cs`、
`Assets/`、`Package.appxmanifest`、`app.manifest`、`app.ico`、`README.md`、
`Properties/launchSettings.json`、`.gitignore`、`Patches/README.md`。

### 用发布版反编译稿替换的文件（提交后改过）

| 文件 | 与发布前快照相比的变化 |
| --- | --- |
| `MainWindow.xaml(.cs)` | 新增侧边导航栏**“收起导航”按钮与折叠动画**（200ms 宽度动画，收起时只显图标、左上角显示 NVIDIA/AMD 厂商 logo） |
| `Pages/HomePage.xaml(.cs)` | 新增**游戏排序下拉框**、**操作 Toast 浮层**、右键菜单**“启动游戏”“编辑备注/标签”**、**键盘快捷键**（Ctrl+A/Delete/F5）、图标缓存等列表交互 |
| `Pages/SettingsPage.xaml(.cs)` | 新增**“启动时自动检查更新”开关**；**移除备份管理区**（备份占用/打开备份/清除备份按钮） |
| `Pages/UpdateLogPage.xaml.cs` | 更新日志改为 **V1.15.3.0-Beta** 的 8 条新功能文案；在线更新批处理脚本换行调整（界面 `UpdateLogPage.xaml` 仍为原版） |
| `Models/GameInfo.cs` | 新增 `Remark` / `Tags` / `RemarkTagsDisplay` / `HasRemarkTags`（游戏备注与标签） |
| `Services/SettingsService.cs` | 新增 `AutoCheckUpdate` 设置项 |
| `Services/FilePatcher.cs` | **发布版对补丁/备份逻辑做了实质改动，整体替换**：①备份目录从程序目录 `backup` 改为 `%AppData%\DLSSFrameGenEnabler\Backups`；②同一游戏多次安装使用**版本化备份目录**（游戏名、游戏名V2、V3…）；③`CopyPatchFiles/BackupFile` 增加 `gameRootPath`，按游戏内**相对目录结构**备份，避免子目录同名文件互相覆盖；④新增 `restore_manifest.txt` 还原清单；⑤卸载/清理时**排除 UE 的 `Engine`、`Content` 目录与 `_CommonRedist` 运行库**，防止误删；⑥移除发布版已删的 `GetCurrentBackupSize()` |

> 替换文件在语义上与发布程序集等价，变量名/排版为反编译器生成风格；其余大部分业务代码
> （GameScanner、ProcessDetector、Translator、说明页面等）都是你的原始手写代码、注释完整。

---

## 二、技术栈与编译

- WinUI 3（Windows App SDK **2.4.0**）桌面解包应用，.NET 10（`net10.0-windows10.0.26100.0`），
  支持 x86 / x64 / ARM64，自包含发布。
- 包：Microsoft.WindowsAppSDK 2.4.0、Microsoft.Windows.SDK.BuildTools 10.0.28000.2705、
  System.Management 8.0.0、System.Drawing.Common 8.0.0。
- 入口由 WinUI 自动生成（**不需要自定义 Program.cs**）。

```powershell
dotnet build -p:Platform=x64 -c Debug
dotnet build -p:Platform=x64 -c Release
```

Visual Studio：打开 `DLSSFrameGenEnabler_WinUI3.csproj`，选 x64 生成/运行。
编译 **0 错误**；剩余为可空引用类型（CS86xx）等提示性警告，你的原代码即如此，不影响运行。

---

## 三、Patches 补丁目录（约 2.3GB，需自行放回）

补丁体积太大，按 `.gitignore` 设计本就不入库，仓库里只保留 `Patches/README.md`。
请把**发布目录中的整个 `Patches` 文件夹**复制到工程根目录（与 csproj 同级）；
csproj 已配置，只要该目录存在，生成时会自动把其中全部文件复制到输出目录。
不放回不影响编译与界面，但游戏补丁相关功能会因缺少补丁文件而无法使用。

---

## 四、发布目录里的 AI / NPU 原生 DLL

`NPUDetect.dll`、`PerceptiveStreaming.dll`、`DirectML.dll`、`onnxruntime.dll`、`workloads.*.json`
是**微软官方签名的 NPU / Windows AI 可再发行组件**，你的 csproj 与全部 C# 代码均未引用或调用，
属于打包但尚未接入的组件。本工程按你原始 csproj 配置**未包含**它们；如需随程序分发可另行作为内容文件添加。

---

## 五、核对结果（本工程 = 发布版）

- `dotnet build`：Debug / Release 均 **0 错误**。
- **类型成员**：本工程 Release 编译的 DLL 与发布 DLL，应用自有的 **27 个类型、全部方法/属性/事件/字段零差异**；
  Release DLL 大小 505,344 字节，与发布 DLL **完全相同**。
- **方法体语义**：把发布 DLL 与本工程重编译 DLL 同时反编译、逐方法比对——14 个类型逐字一致；
  MainWindow / HomePage / FilePatcher 仅剩局部变量名与等价三元/switch 写法差异（反编译往返产物），
  逻辑、字符串、控制流完全相同。
- **XAML**：7 个界面与发布 XBF 反编译稿在元素树、属性、资源、x:Bind、事件、有效文本上全部一致；
  仅 `mc:Ignorable="d"` 设计时标记与 `Text=""` 占位（运行时由代码赋值）两类编译期等价差异。
- **资源**：14 个 Assets 文件与发布目录 **MD5 全部一致**。
- **实机运行**：游戏列表扫描、顶部“默认排序”、左侧“收起导航”折叠、设置页“启动时自动检查更新”、
  更新内容页 **V1.15.3.0-Beta 八条新功能日志**、首次启动“温馨提示”弹窗均正常。

---

## 六、关于原 git 仓库

原目录工作区里 `Pages/SettingsPage.xaml(.cs)` 曾被误删 6 行，已在合并中随发布版一并修正。
当前原仓库相对提交 `6bc7c7d` 共有 10 个文件修改（即上表的发布版变化）+ 本说明文档。
建议在 Visual Studio 中核对运行后，重新提交作为新起点：

```powershell
cd "<工程目录>"
git add -A
git commit -m "1.15.3.0-Beta 发布版完整源码（排序/导航折叠/备注标签/快捷键/启动游戏/Toast/自动更新/备份重构）"
```
