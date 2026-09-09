# 多帧生成+DLSS5开启工具

一款基于 C# WinForms 开发的桌面工具，一键为支持 DLSS 的游戏开启多帧生成和 DLSS5 功能，让 RTX 50 系以下显卡也能使用多帧生成功能。

## ✨ 功能特性

### 多帧生成（MFG）
- 支持 RTX 20系 / 30系 / 40系显卡开启多帧生成
- 经典模式：适合老驱动（616.56之前），对310.8及之前的帧生成版本有效
- 高级模式：适合新驱动，对310.9及以上的帧生成版本有效，兼容性最强
- RE引擎专用模式：支持部分新游戏（生化危机9、鬼武者、识质存在等）
- 配置名修改功能：支持修改补丁配置名以适配部分游戏（异环、鸣潮等）

### DLSS5
- 支持 N卡和 A卡开启 DLSS5
- A卡仅支持 RX 7000系和9000系显卡
- 通用方案：适用于大部分 DX11+ 游戏
- RE引擎通用方案：适用于 RE 引擎游戏
- DX9 DLSS5：支持 DX9 32位和64位游戏开启 DLSS5
- 燕云十六声特殊适配：支持 DLSS5 + 多帧生成同时开启

### 游戏扫描
- 自动扫描 Steam / Epic / EA / 育碧 / GOG 平台游戏库
- 手动添加游戏（支持默认模式和 DX9 手动选择 exe）
- 智能识别游戏真正运行的 exe 目录
- 自动识别 RE 引擎游戏（检测 re_chunk_000.pak）
- 自动识别渲染 API 类型（DX9 / DX11+）

### 其他功能
- 一键还原 / 无责还原
- 排错修复功能（经典模式和高级模式分别使用不同补丁）
- 右键菜单快速操作
- 浅色 / 深色主题切换
- 检查更新（GitHub Gist 托管 version.json）
- 2077 专用补丁
- 使用说明和更新内容内嵌查看
- 软件启动时弹出小黑盒分享提示

## 🖥️ 系统要求

- **操作系统**：Windows 10 / Windows 11（64位）
- **运行时**：.NET 8.0 Desktop Runtime（单文件版已内置，无需单独安装）
- **显卡**：
  - N卡：RTX 20系及以上（多帧生成），RTX 20系及以上（DLSS5）
  - A卡：RX 7000系 / 9000系（DLSS5）
- **磁盘空间**：约 800 MB（单文件版，含所有补丁）

## 📦 下载

从 [发布页面](https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases) 下载最新版本的单文件 exe，无需安装，直接运行即可。

## 🚀 使用方法

### 1. 扫描游戏
- 点击「自动扫描」自动扫描已安装的游戏库
- 或点击「手动添加」选择游戏文件夹

### 2. 开启多帧生成
- 选中游戏，点击「一键开启多帧生成」
- 根据显卡型号和游戏类型选择模式（经典 / 高级 / RE引擎专用）
- 等待补丁安装完成

### 3. 开启 DLSS5
- 选中游戏，点击「开启DLSS5」
- 软件自动检测显卡类型并安装对应补丁
- A卡用户安装后需运行 AMD 设置程序

### 4. 启动游戏
- 从游戏目录直接启动游戏（部分网游需绕过启动器）
- 按 HOME 键打开 ReShade 配置界面进行调整

## 🛠️ 如何编译

### 环境要求
- Visual Studio 2022（需安装 .NET 桌面开发工作负载）
- .NET 8.0 SDK

### 编译步骤
1. 克隆仓库：`git clone https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5.git`
2. 打开 `DLSSFrameGenEnabler.csproj`
3. 将补丁文件放入 `Patches/` 目录（详见下方补丁说明）
4. 编译：`dotnet build -c Release`
5. 发布单文件版：
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
   ```

## 📁 补丁文件说明

本软件的补丁文件（nvngx_dlssg.dll、nvngx_dlssnr.dll、ReShade 相关文件等）由于涉及第三方版权，**不包含在开源仓库中**。

如需自行编译，请按照以下目录结构放置补丁文件：

```
Patches/
├── Shared/                    # 共享大文件
│   ├── nvngx_dlssnr.dll
│   ├── nvngx_dlss.dll
│   ├── dxgi.dll
│   ├── reshade-shaders-common/
│   └── Fonts/
├── Advanced/                  # 高级模式补丁
├── RTX20/                     # RTX20系多帧生成补丁
├── RTX30/                     # RTX30系多帧生成补丁
├── REFrameGen/                # RE引擎多帧生成补丁
├── DLSS5/                     # DLSS5补丁
│   ├── Common/
│   ├── Nvidia/
│   ├── AMD/
│   └── REEngine/
├── DX9/                       # DX9 DLSS5补丁
├── Cyberpunk2077/             # 2077专用补丁
├── Troubleshoot/              # 经典模式排错补丁
└── TroubleshootAdvanced/      # 高级模式排错补丁
```

所有补丁文件会在编译时嵌入到 exe 中，实现单文件独立运行。

## ⚠️ 免责声明

1. **不建议在网游和带反作弊的游戏上使用本软件**
2. 本软件本质是修改游戏文件，修改文件可能导致游戏封号
3. 如遇游戏封号，本软件概不负责
4. A卡用户开启 DLSS5 后，需要在游戏中开启 FSR3.0 以上，不要选择 FSR2.0，否则会导致游戏崩溃
5. RE引擎游戏开启 DLSS5 后，暂时无法开启游戏自带的帧生成，建议使用 DLSS5 自带的 AI 插帧
6. 米哈游系列游戏暂不适配，建议使用【大力喜鹊】或【HoYoshade】【XXMI】
7. Vulkan 游戏暂不适配

## 🙏 致谢

本软件的实现离不开以下社区项目和作者的贡献：

- **ReShade** - 通用的游戏后处理注入框架
- **RenoDX** - DLSS5 神经渲染插件
- **Uncle Burrito** - nvngx_dlssnr.dll 补丁
- **DLSS5-Feeder** - DX9 游戏 DLSS5 支持
- **dgVoodoo2** - DX9 转 DX11 包装层
- **NVIDIA** - DLSS 技术和相关运行时文件

所有补丁版权归原作者及 NVIDIA 所有，本软件仅作学习交流使用。

## 📄 许可证

[MIT License](LICENSE)

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

- 发现 Bug 请提交 [Issue](https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/issues)
- 代码贡献请提交 [Pull Request](https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/pulls)

## 📞 联系方式

- GitHub：[@QSXK1314](https://github.com/QSXK1314)
- 哔哩哔哩：[空间链接](https://space.bilibili.com/414911649)
- 小黑盒：[分享链接](https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=d2b27269b51e&new_post_share_style=true)
