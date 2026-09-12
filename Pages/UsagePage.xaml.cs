using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Xaml.Controls;

namespace DLSSFrameGenEnabler_WinUI3.Pages
{
    public sealed partial class UsagePage : Page
    {
        public UsagePage()
        {
            this.InitializeComponent();
            Translator.LanguageChanged += UpdateLanguageTexts;
            UpdateLanguageTexts();
        }

        private void UpdateLanguageTexts()
        {
            bool en = Translator.IsEnglish;
            TitleText.Text = en ? "Usage Guide" : "使用说明";

            ImportantTitle.Text = en ? "⚠️ Important Notice" : "⚠️ 重要声明";
            Important1.Text = en ?
                "Due to the strict anti-cheat of miHoYo games, we do not support miHoYo games for now. We recommend using Magpie, HoYoshade, or XXMI to enable DLSS5 and Frame Generation." :
                "由于米哈游系列游戏反作弊较为严苛，所以暂不加入对米哈游游戏的适配，建议玩家们可以去使用【Magpie（大力喜鹊）】或者下载【HoYoshade】和【XXMI】开启dlss5和多帧生成功能。";
            Important2.Text = en ?
                "Also, since Vulkan is not as widely used as DirectX, we do not support Vulkan for now. Sorry for the inconvenience, as my personal time and energy are limited." :
                "另外由于Vulkan使用率并没有dx广泛，所以暂时也不加入适配，还请玩家们见谅我没有对这两个方向去适配，毕竟个人时间和精力实在有限。";
            Important3.Text = en ?
                "Not recommended for online games or games with anti-cheat. This software modifies game files, which may result in account bans. We are not responsible for any bans." :
                "不建议在网游上和带反作弊的游戏使用该软件，本质是修改文件，而修改文件则可能导致游戏封号，如果遇到游戏封号，本软件概不负责。";
            Important4.Text = en ?
                "Ubisoft and EA games are not yet widely adapted. Some games may not be able to use the features. Whether it works needs to be tested by the player." :
                "育碧与EA游戏暂未广泛适配，可能有些游戏无法使用其中的功能，至于是否生效需要玩家自行检测。";

            Section1Title.Text = en ? "1. Introduction" : "一、软件简介";
            Section1Content.Text = en ?
                "This software can enable Frame Generation and DLSS5 for games with one click. Supports RTX 20/30/40/50 series GPUs, supports NVIDIA and AMD (AMD only supports 7000 and 9000 series). Supports auto-scan Steam/Epic/EA/Ubisoft/GOG game libraries, and manual game addition." :
                "本软件可以一键为游戏开启多帧生成和DLSS5功能，支持RTX20/30/40/50系显卡，支持N卡和A卡（A卡仅支持7000系和9000系）。支持自动扫描Steam/Epic/EA/育碧/GOG游戏库，也支持手动添加游戏。";

            Section2Title.Text = en ? "2. Frame Generation" : "二、多帧生成";
            FgClassicTitle.Text = en ? "Classic Mode (RTX 40 only)" : "经典模式（仅RTX40系）";
            FgClassicContent.Text = en ?
                "Suitable for old drivers (before 616.56), only effective for frame generation version 310.8 and below. Generally used for games that don't need tinkering, plug-and-play, and games that can't or won't update drivers. Replaces nvngx_dlssg.dll and copies RTX40MFG.asi, RTX40MFG_config.json, version.dll to the game exe directory." :
                "适合老驱动（616.56之前的驱动），而且只对310.8及之前的帧生成版本有效，普遍用于一些不需要折腾，即装即用和不再/无法更新或者616.56驱动之前的游戏。替换nvngx_dlssg.dll，并将RTX40MFG.asi、RTX40MFG_config.json、version.dll复制到游戏运行exe目录。";
            FgAdvancedTitle.Text = en ? "Advanced Mode (RTX 40 only)" : "高级模式（仅RTX40系）";
            FgAdvancedContent.Text = en ?
                "Suitable for new drivers, effective for frame generation version 310.9 and above. Currently the most compatible (some games require users to modify some configurations to enter the game). Includes ReShade, Universal RTX 40 MFG Unlock, and version.dll proxy. Users with latest drivers should use Advanced Mode." :
                "适合新驱动，而且适合310.9及以上的帧生成版本有效，目前兼容性最强（部分游戏需要用户自行修改一些配置才能进入游戏）。包含ReShade、Universal RTX 40 MFG Unlock和version.dll代理。已经更新到最新驱动的建议使用高级模式。";
            Fg2030Title.Text = en ? "RTX 20/30 Series" : "RTX20系/30系";
            Fg2030Content.Text = en ?
                "The software automatically detects GPU model. RTX 20 and 30 series users use dedicated frame generation patches. Just copy all patch files to the game exe directory, no ReShade needed." :
                "软件会自动检测显卡型号，RTX20系和30系用户使用专属的多帧生成补丁。直接将所有补丁文件复制到游戏运行exe目录即可，不需要ReShade。";
            FgRETitle.Text = en ? "RE Engine Frame Gen" : "RE引擎多帧生成";
            FgREContent.Text = en ?
                "RE Engine games (such as Resident Evil 9, Onimusha, Pragmata, etc.) use dedicated RE Engine frame generation patches. Requires dinput8.dll (RE Framework). Game exe is usually in the game root directory." :
                "RE引擎游戏（如生化危机9、鬼武者、识质存在等）使用专属的RE引擎多帧生成补丁。需要dinput8.dll（RE框架）。游戏运行exe通常在游戏根目录。";
            Fg2077Title.Text = en ? "Cyberpunk 2077 Exclusive" : "2077专用";
            Fg2077Content.Text = en ?
                "Cyberpunk 2077 is special, uses exclusive patches. Install to bin\\x64 directory. Usage instructions will pop up after installation. After enabling, only Restore is available." :
                "赛博朋克2077比较特殊，使用专用补丁，安装到bin\\x64目录。安装后会弹出使用说明。启用后只能使用一键还原。";

            Section3Title.Text = en ? "3. DLSS5" : "三、DLSS5";
            Dlss5GeneralTitle.Text = en ? "General DLSS5" : "通用DLSS5";
            Dlss5GeneralContent.Text = en ?
                "Suitable for most DX11/12 games. The software automatically detects NVIDIA or AMD GPU and installs corresponding patches. AMD users need to enable FSR 3.0 or above to start DLSS5, do NOT select FSR 2.0 (may cause game crash)." :
                "适用于大部分DX11/12游戏，软件会自动检测N卡还是A卡，安装对应的补丁。A卡用户需要开启FSR3.0以上才会启动DLSS5，不要选择FSR2.0（可能导致游戏崩溃）。";
            Dlss5RETitle.Text = en ? "RE Engine DLSS5" : "RE引擎DLSS5";
            Dlss5REContent.Text = en ?
                "RE Engine games use dedicated DLSS5 patches. After enabling, the game's built-in frame generation cannot be used temporarily. It is recommended to use DLSS5's built-in AI frame interpolation or NVIDIA AI frame interpolation." :
                "RE引擎游戏使用专属的DLSS5补丁，开启后暂时无法开启游戏自带的帧生成，建议使用DLSS5自带的AI插帧或者NVIDIA的AI插帧。";
            Dlss5Dx9Title.Text = en ? "DX9 DLSS5" : "DX9 DLSS5";
            Dlss5Dx9Content.Text = en ?
                "Supports DX9 32-bit and 64-bit games. Requires manual selection of game exe. Uses dgVoodoo2 to translate DX9 to DX11, then DLSS5 Feeder to enable DLSS5. Some games may need to move dgVoodoo files to the DLL loading directory (use the 'Replace DLL' option in restore menu)." :
                "支持DX9 32位和64位游戏，需要手动选择游戏exe。使用dgVoodoo2将DX9转译到DX11，再通过DLSS5 Feeder开启DLSS5。部分游戏可能需要将dgVoodoo文件移动到DLL加载目录（在还原菜单中使用'替换DLL再测试'功能）。";

            Section4Title.Text = en ? "4. Other Features" : "四、其他功能";
            OtherConfigNameTitle.Text = en ? "Config Name Modification (Advanced mode only)" : "配置名修改（仅高级模式）";
            OtherConfigNameContent.Text = en ?
                "Used to modify the names of version.dll and version.ini to solve the problem of some games not being able to enter (anti-cheat detection). 12 optional config names available. dxgi.dll can only be changed to d3d12.dll. Conflict detection included." :
                "用于修改version.dll和version.ini的名称，解决部分游戏无法进入的问题（反作弊检测）。提供12个可选配置名。dxgi.dll只能改成d3d12.dll。包含文件冲突检测。";
            OtherTroubleTitle.Text = en ? "Troubleshooting" : "排错修复";
            OtherTroubleContent.Text = en ?
                "If frame generation doesn't work after enabling, you can use the troubleshooting feature to replace patches starting with sl. (NVIDIA Streamline files). Only replaces files that exist in the game, won't add new files. You can select which files to replace." :
                "开启多帧生成后如果不生效，可以使用排错功能替换sl.开头的补丁文件（NVIDIA Streamline相关文件）。只会替换游戏中已有的文件，不会新增文件。可以自定义选择要替换的文件。";
            OtherRestoreTitle.Text = en ? "One-Click Restore" : "一键还原";
            OtherRestoreContent.Text = en ?
                "Restore all added and replaced files, recover the game to its original state. If no backup exists, 'No-responsibility Restore' is available (directly deletes all patch files without restoring original files, verify game integrity after use)." :
                "还原所有添加和替换的文件，恢复游戏原始状态。如果没有备份，会提供'无责还原'（直接删除所有补丁文件，不恢复原文件，使用后请验证游戏完整性）。";

            Section5Title.Text = en ? "5. Game Scanning" : "五、游戏扫描";
            Section5Content.Text = en ?
                "Auto Scan: Automatically detects Steam, Epic, EA, Ubisoft, GOG game libraries. Only adds games with a real executable file (excludes uninstalled game folders and engine directories).\nManual Add: Select game folder, auto-detect game exe. Supports DX9 manual mode (select exe directly).\nRight-click menu: Enable/disable features, open game folder, remove from list." :
                "自动扫描：自动检测Steam、Epic、EA、育碧、GOG游戏库。只添加有真正运行exe的游戏（排除已卸载的空文件夹和引擎目录）。\n手动添加：选择游戏文件夹，自动识别游戏exe。支持DX9手动模式（直接选择exe）。\n右键菜单：开启/关闭功能、打开游戏目录、移除列表。";

            Section6Title.Text = en ? "6. Notes" : "六、注意事项";
            Section6Content.Text = en ?
                "1. Backup game saves before use.\n2. Make sure the game is fully closed when patching or restoring.\n3. Not recommended for online games or games with anti-cheat.\n4. If patches fail after game update, re-apply them.\n5. RE Engine games: DLSS5 and built-in frame generation conflict, do not enable both.\n6. AMD DLSS5 only supports 7000 and 9000 series GPUs.\n7. DX9 games must use DX9 DLSS5, not general DLSS5 (DLSS5 requires DX11+)." :
                "1. 使用前请备份游戏存档。\n2. 打补丁和还原时，请确保游戏已完全关闭。\n3. 不建议在网游和带反作弊的游戏上使用。\n4. 游戏更新后补丁失效，重新打补丁即可。\n5. RE引擎游戏：DLSS5和游戏自带帧生成冲突，不要同时开启。\n6. A卡DLSS5仅支持7000系和9000系显卡。\n7. DX9游戏必须使用DX9 DLSS5，不能使用通用DLSS5（DLSS5最低要求DX11）。";
        }
    }
}
