# DLSS Frame Gen Enabler and DLSS5
# DLSS多帧生成开启工具

2026-09-14最新版本（latest version）：V1.14.0.0

A tool to enable DLSS5 and Frame Generation for games.
一款用于给游戏开启DLSS5和多帧生成功能的工具。

## Features / 功能特性

- **DLSS5 Support** - Enable DLSS5 for games that support it
- **DLSS5支持** - 为支持的游戏开启DLSS5

- **Frame Generation Support** - Enable Frame Generation for RTX 20/30/40 series
- **多帧生成支持** - 为RTX 20/30/40系显卡开启多帧生成

- **DX9 DLSS5** - Enable DLSS5 for DX9 games via dgVoodoo translation
- **DX9 DLSS5** - 通过dgVoodoo转译为DX9游戏开启DLSS5

- **RE Engine Support** - Special support for RE Engine games (Resident Evil etc.)
- **RE引擎支持** - 为RE引擎游戏（生化危机等）提供特殊支持

- **Cyberpunk 2077 Special Patch** - Dedicated patch for Cyberpunk 2077
- **赛博朋克2077专用补丁** - 为赛博朋克2077提供专用补丁

- **Game Auto Scan** - Automatically scan and detect installed games
- **游戏自动扫描** - 自动扫描并检测已安装的游戏

- **Manual Add** - Manually add games by directory or exe
- **手动添加** - 通过目录或exe手动添加游戏

- **Detect Running Games** - Detect currently running games via process enumeration
- **检测运行中游戏** - 通过进程枚举检测当前正在运行的游戏

- **One-Click Restore** - Restore game files to original state
- **一键还原** - 将游戏文件还原到原始状态

- **Troubleshooting** - Replace problematic patch files with alternatives
- **排错修复** - 将有问题的补丁文件替换为备选文件

- **Multi-language** - Support Chinese and English
- **多语言支持** - 支持中文和英文

- **Dark/Light Mode** - Support dark and light themes
- **深浅色模式** - 支持深色和浅色主题

## System Requirements / 系统要求

- Windows 10/11 64-bit
- Windows 10/11 64位系统

- NVIDIA RTX 20/30/40 series GPU (for DLSS/Frame Generation)
- NVIDIA RTX 20/30/40系显卡（用于DLSS/多帧生成）

- .NET 10 Runtime (included in release)
- .NET 10运行时（发布版已包含）

## Important Notes / 重要说明

### miHoYo Games / 米哈游游戏

Due to strict anti-cheat systems, miHoYo games (Genshin Impact, Honkai: Star Rail, Zenless Zone Zero etc.) are NOT supported.
由于米哈游系列游戏反作弊较为严苛，所以暂不加入对米哈游游戏的适配。

For miHoYo games, we recommend using [Magpie](https://github.com/Blinue/Magpie) or [HoYoshade](https://github.com/ho-yoshade/HoYoshade) and [XXMI](https://github.com/leotorrez/XXMI) to enable DLSS5 and Frame Generation.
对于米哈游游戏，建议玩家们可以去使用【大力喜鹊(Magpie)】或者下载【HoYoshade】和【XXMI】开启DLSS5和多帧生成功能。

### Vulkan Games / Vulkan游戏

Vulkan API is not as widely used as DirectX, so Vulkan support is not currently implemented.
由于Vulkan使用率并没有DX广泛，所以暂时也不加入适配。

### Ubisoft and EA Games / 育碧与EA游戏

Ubisoft and EA games may not be fully compatible. Some games may not work properly. Please test yourself.
育碧与EA游戏暂未广泛适配，可能有些游戏无法使用其中的功能，至于是否生效需要玩家自行检测。

## Usage / 使用方法

1. **Important**: Enable DLSS/Frame Generation in game settings FIRST, then close the game.
1. **重要**：请先在游戏设置中开启DLSS/帧生成相关功能，然后关闭游戏。

2. Launch the application
2. 启动软件

3. Add your game via Auto Scan, Manual Add, or Detect Running Games
3. 通过自动扫描、手动添加或检测运行中游戏添加游戏

4. Select the game and click "Enable DLSS5" or "Enable Frame Generation"
4. 选中游戏，点击"开启DLSS5"或"开启多帧生成"

5. Launch the game and enjoy!
5. 启动游戏即可体验！

## Building from Source / 从源码编译

### Prerequisites / 前置要求

- Visual Studio 2022 or later
- Visual Studio 2022或更高版本

- .NET 10 SDK
- .NET 10 SDK

- Windows App SDK 2.4.0
- Windows App SDK 2.4.0

### Build / 编译

```bash
cd DLSSFrameGenEnabler_WinUI3
dotnet build --configuration Release
```

## Version / 版本

Current version: V1.14.0.0
当前版本：V1.14.0.0

## Author / 作者

- GitHub: [QSXK1314](https://github.com/QSXK1314)
- Bilibili: [哔哩哔哩](https://space.bilibili.com/414911649)
- 小黑盒: [小黑盒](https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=7c7772709af8&new_post_share_style=true)

## Acknowledgments / 致谢

This project uses various community-made patches and tools. Thanks to all the modders and developers who made this possible.
本项目使用了各种社区制作的补丁和工具。感谢所有让这成为可能的modder和开发者。

## License / 许可证

This project is open source and free to use.
本项目开源且免费使用。

## Disclaimer / 免责声明

This software modifies game files. Use at your own risk. We are not responsible for any game bans, data loss, or other issues caused by using this software.
本软件会修改游戏文件，使用风险自负。对于使用本软件导致的任何游戏封号、数据丢失或其他问题，我们概不负责。

Do not use this software in online games with anti-cheat systems.
不建议在带有反作弊系统的网络游戏中使用本软件。
