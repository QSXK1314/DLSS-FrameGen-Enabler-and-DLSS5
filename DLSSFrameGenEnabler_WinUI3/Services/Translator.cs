using System;
using System.Collections.Generic;
using System.Globalization;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    public enum AppLanguage { Auto, Chinese, English }

    public static class Translator
    {
        private static AppLanguage _currentLanguage = AppLanguage.Auto;
        public static AppLanguage CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                _currentLanguage = value;
                LanguageChanged?.Invoke();
            }
        }

        public static event Action? LanguageChanged;

        public static bool IsEnglish
        {
            get
            {
                if (_currentLanguage == AppLanguage.English) return true;
                if (_currentLanguage == AppLanguage.Chinese) return false;
                // Auto: 检测系统语言
                var sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return sysLang != "zh";
            }
        }

        // 翻译字典
        private static readonly Dictionary<string, (string zh, string en)> _translations = new()
        {
            // 导航
            ["Nav_Home"] = ("主页", "Home"),
            ["Nav_Usage"] = ("使用说明", "Usage Guide"),
            ["Nav_Update"] = ("更新内容", "Update Log"),
            ["Nav_Settings"] = ("设置", "Settings"),
            ["Nav_About"] = ("关于", "About"),

            // 主页按钮
            ["Btn_AutoScan"] = ("自动扫描", "Auto Scan"),
            ["Btn_ManualAdd"] = ("手动添加", "Manual Add"),
            ["Btn_ManualDx9"] = ("手动模式（自行选择游戏exe）", "Manual Mode (Select game exe)"),
            ["Btn_FrameGen"] = ("一键开启多帧生成", "Enable Frame Gen"),
            ["Btn_Dlss5"] = ("开启DLSS5", "Enable DLSS5"),
            ["Btn_Dx9Dlss5"] = ("开启DX9 DLSS5", "Enable DX9 DLSS5"),
            ["Btn_2077"] = ("2077专用补丁", "2077 Special Patch"),
            ["Btn_Config"] = ("配置多帧生成", "Configure Frame Gen"),
            ["Btn_ConfigName"] = ("配置名修改", "Config Name Modify"),
            ["Btn_Troubleshoot"] = ("排错修复", "Troubleshoot"),
            ["Btn_Restore"] = ("一键还原", "Restore All"),
            ["Btn_RefreshStatus"] = ("刷新状态", "Refresh Status"),

            // 右键菜单
            ["Ctx_OpenFolder"] = ("打开游戏目录", "Open Game Folder"),
            ["Ctx_Remove"] = ("移除列表", "Remove from List"),

            // 状态
            ["Status_NotEnabled"] = ("未开启", "Not Enabled"),
            ["Status_FrameGen"] = ("已开启（多帧生成）", "Enabled (Frame Gen)"),
            ["Status_Dlss5"] = ("已开启（DLSS5）", "Enabled (DLSS5)"),
            ["Status_All"] = ("已开启（全部）", "Enabled (All)"),
            ["Status_2077"] = ("已开启（专属多帧生成）", "Enabled (Special Frame Gen)"),

            // 通用
            ["Common_Confirm"] = ("确定", "OK"),
            ["Common_Cancel"] = ("取消", "Cancel"),
            ["Common_Yes"] = ("是", "Yes"),
            ["Common_No"] = ("否", "No"),
            ["Common_Close"] = ("关闭", "Close"),
            ["Common_Save"] = ("保存", "Save"),

            // 免责声明
            ["Disclaimer"] = ("免责声明：不建议在网游上和带反作弊的游戏使用该软件，本质是修改文件，而修改文件则可能导致游戏封号，如果遇到游戏封号，本软件概不负责。",
                "Disclaimer: Not recommended for online games or games with anti-cheat. This software modifies game files, which may result in account bans. We are not responsible for any bans."),

            // 设置
            ["Settings_DarkMode"] = ("深色模式", "Dark Mode"),
            ["Settings_Animation"] = ("动画效果", "Animation Effects"),
            ["Settings_Animation_Desc"] = ("关闭后将禁止页面切换和深浅色切换等所有动画，以减少性能占用", "Disables all animations including page transitions and theme switches to reduce performance usage"),
            ["Settings_Transparent_Tip"] = ("注意：纯透明材质下切换深浅色模式后，需要重启软件才能显示正确颜色", "Note: After switching themes with Pure Transparent backdrop, please restart the app to display correct colors"),
            ["Settings_StartupDialog"] = ("启动时显示提示弹窗", "Show startup dialog"),
            ["Settings_RememberGames"] = ("记住游戏列表", "Remember game list"),
            ["Settings_Language"] = ("语言", "Language"),
            ["Settings_GpuSelect"] = ("显卡选择", "GPU Selection"),
            ["Settings_AutoDetect"] = ("自动检测", "Auto Detect"),
            ["Settings_Lang_Auto"] = ("跟随系统", "System Default"),
            ["Settings_Lang_Chinese"] = ("简体中文", "简体中文"),
            ["Settings_Lang_English"] = ("English", "English"),
            ["Settings_Backdrop"] = ("界面材质", "Window Material"),
            ["Settings_Backdrop_Default"] = ("默认", "Default"),
            ["Settings_Backdrop_Mica"] = ("云母 Mica", "Mica"),
            ["Settings_Backdrop_Acrylic"] = ("亚克力 Acrylic", "Acrylic"),
            ["Settings_Backdrop_TransparentBlur"] = ("纯透明", "Pure Transparent"),
            ["Settings_Backdrop_Blur"] = ("高斯模糊 Gaussian Blur", "Gaussian Blur"),
            ["Settings_Backdrop_Custom"] = ("自定义壁纸", "Custom Wallpaper"),
            ["Settings_SelectWallpaper"] = ("选择图片", "Select Image"),
            ["Settings_ResetWallpaper"] = ("恢复默认", "Reset to Default"),

            // 关于
            ["About_Title"] = ("关于", "About"),
            ["About_Author"] = ("作者", "Author"),
            ["About_Github"] = ("GitHub", "GitHub"),
            ["About_Bilibili"] = ("哔哩哔哩", "Bilibili"),
            ["About_Xiaoheihe"] = ("小黑盒", "Xiaoheihe"),
            ["About_Thanks"] = ("补丁致谢", "Patch Credits"),
            ["About_Version"] = ("版本", "Version"),
            ["About_AppDesc"] = ("一款为游戏开启多帧生成和DLSS5的工具", "A tool to enable Frame Generation and DLSS5 for games"),
            ["About_DevLang"] = ("开发语言：C# + WinUI 3", "Developed with: C# + WinUI 3"),

            // 弹窗通用
            ["Dlg_Title_Tip"] = ("提示", "Tip"),
            ["Dlg_Title_Warning"] = ("警告", "Warning"),
            ["Dlg_Title_Confirm"] = ("确认", "Confirm"),
            ["Dlg_Title_Success"] = ("成功", "Success"),
            ["Dlg_Title_Error"] = ("错误", "Error"),

            // 多帧生成
            ["Dlg_FrameGen_Title"] = ("选择多帧生成模式", "Select Frame Gen Mode"),
            ["Dlg_FrameGen_Classic"] = ("经典模式", "Classic Mode"),
            ["Dlg_FrameGen_ClassicDesc"] = ("适合老驱动（616.56之前），只对310.8及之前的帧生成版本有效，即装即用", "For old drivers (before 616.56), only works with frame gen version 310.8 and below, plug and play"),
            ["Dlg_FrameGen_Advanced"] = ("高级模式", "Advanced Mode"),
            ["Dlg_FrameGen_AdvancedDesc"] = ("适合新驱动，对310.9及以上的帧生成版本有效，兼容性最强（部分游戏需自行修改配置）", "For new drivers, works with frame gen version 310.9+, best compatibility (some games need manual config)"),
            ["Dlg_FrameGen_RE"] = ("RE引擎多帧生成", "RE Engine Frame Gen"),
            ["Dlg_FrameGen_RT20"] = ("RTX20系多帧生成", "RTX 20 Series Frame Gen"),
            ["Dlg_FrameGen_RT30"] = ("RTX30系多帧生成", "RTX 30 Series Frame Gen"),
            ["Dlg_FrameGen_Installed"] = ("多帧生成安装完成", "Frame Gen installed successfully"),
            ["Dlg_FrameGen_Restored"] = ("多帧生成已还原", "Frame Gen restored"),
            ["Dlg_FrameGen_NotSupported"] = ("当前游戏不支持多帧生成", "This game does not support Frame Gen"),
            ["Dlg_FrameGen_GpuNotSupported"] = ("当前显卡不支持多帧生成", "Current GPU does not support Frame Gen"),

            // DLSS5
            ["Dlg_Dlss5_Title"] = ("选择DLSS5方案", "Select DLSS5 Method"),
            ["Dlg_Dlss5_General"] = ("通用DLSS5补丁", "General DLSS5 Patch"),
            ["Dlg_Dlss5_RE"] = ("RE引擎通用DLSS补丁", "RE Engine DLSS Patch"),
            ["Dlg_Dlss5_Installed"] = ("DLSS5安装完成", "DLSS5 installed successfully"),
            ["Dlg_Dlss5_Restored"] = ("DLSS5已还原", "DLSS5 restored"),
            ["Dlg_Dlss5_GpuNotSupported"] = ("当前显卡不支持DLSS5", "Current GPU does not support DLSS5"),
            ["Dlg_Dlss5_AmdWarn"] = ("AMD显卡较为特殊，需要开启FSR3.0以上才会启动DLSS5，不要选择FSR2.0，否则会导致游戏崩溃", "AMD GPUs are special: need FSR 3.0+ to enable DLSS5. Do NOT select FSR 2.0, it will crash the game"),
            ["Dlg_Dlss5_AmdSetup"] = ("DLSS5安装完成，已自动打开AMD设置程序", "DLSS5 installed, AMD setup program launched automatically"),
            ["Dlg_Dlss5_REWarn"] = ("开启DLSS5后暂时无法开启游戏自带的帧生成，建议使用DLSS5自带的AI插帧", "After enabling DLSS5, built-in frame gen is temporarily unavailable. Recommend using DLSS5's built-in AI frame generation"),

            // DX9 DLSS5
            ["Dlg_Dx9_Installed"] = ("DX9 DLSS5安装完成", "DX9 DLSS5 installed successfully"),
            ["Dlg_Dx9_Restored"] = ("DX9 DLSS5已还原", "DX9 DLSS5 restored"),

            // 2077
            ["Dlg_2077_Installed"] = ("2077专用补丁安装完成", "2077 special patch installed"),
            ["Dlg_2077_Restored"] = ("2077专用补丁已还原", "2077 special patch restored"),
            ["Dlg_2077_Usage"] = ("2077专用补丁使用说明", "2077 Special Patch Usage"),
            ["Dlg_2077_UsageText"] = ("①所有文件已自动复制到【Cyberpunk 2077\\bin\\x64】\n\n②启动游戏，点击Unbound，设置CET控制台快捷键\n\n③游戏设置内，开启DLSS帧生成\n\n④按CET控制台快捷键，左上角出现DLSS MFG面板，自行调节生成倍率\n\n⑤重启游戏生效",
                "①All files have been copied to [Cyberpunk 2077\\bin\\x64]\n\n②Launch game, click Unbound, set CET console hotkey\n\n③In game settings, enable DLSS Frame Generation\n\n④Press CET hotkey, DLSS MFG panel appears top-left, adjust multiplier\n\n⑤Restart game to apply"),

            // 配置多帧生成
            ["Dlg_Config_Title"] = ("配置多帧生成", "Configure Frame Gen"),
            ["Dlg_Config_Multiplier"] = ("帧生成倍率", "Frame Gen Multiplier"),
            ["Dlg_Config_Mode"] = ("模式", "Mode"),
            ["Dlg_Config_Fixed"] = ("固定倍率", "Fixed"),
            ["Dlg_Config_Dynamic"] = ("动态倍率", "Dynamic"),
            ["Dlg_Config_TargetFps"] = ("动态目标帧率", "Dynamic Target FPS"),
            ["Dlg_Config_Exp"] = ("动态模式允许5/6倍（实验性）", "Allow 5x/6x in dynamic mode (experimental)"),
            ["Dlg_Config_Saved"] = ("配置已保存", "Config saved"),
            ["Dlg_Config_SaveFail"] = ("配置保存失败", "Failed to save config"),
            ["Dlg_Config_NotFound"] = ("未找到RTX40MFG_config.json配置文件\n请先开启经典模式多帧生成", "RTX40MFG_config.json not found\nPlease enable Classic Mode Frame Gen first"),
            ["Dlg_Config_ReadFail"] = ("配置文件读取失败", "Failed to read config file"),

            // 配置名修改
            ["Dlg_ConfigName_Title"] = ("配置名修改", "Config Name Modify"),
            ["Dlg_ConfigName_TipTitle"] = ("使用前必看", "Read Before Use"),
            ["Dlg_ConfigName_TipText"] = ("在使用该功能前请确认您用了高级功能后游戏会出现报错、非法模块、无法开启多帧生成这类问题才建议使用。\n\n如果没有问题，一切运行良好请保持默认配置。\n\n鸣潮：使用高级模式后默认不需要更改配置名，但需要绕过启动器启动，直接从游戏目录开启游戏。\n异环：需要修改dxgi.dll和两个version文件，建议将version改成dsound。\n明末：只需要修改version配置名称，需要自行检测哪一个配置名称可以开启多帧生成。",
                "Only use this if you encounter errors, illegal module detection, or frame gen not working after Advanced Mode.\n\nIf everything works fine, keep default config.\n\nWuthering Waves: No config change needed after Advanced Mode, but launch directly from game folder, bypass launcher.\n\nNeverness to Everness: Need to modify dxgi.dll and both version files, recommend changing version to dsound.\n\nMing Dynasty: Only need to modify version config name, test which name works."),
            ["Dlg_ConfigName_IKnow"] = ("我知道了，继续", "I understand, continue"),
            ["Dlg_ConfigName_SelectName"] = ("选择配置名（修改version.dll和version.ini）", "Select config name (modifies version.dll and version.ini)"),
            ["Dlg_ConfigName_ModifyVersion"] = ("修改version配置名", "Modify version config name"),
            ["Dlg_ConfigName_Confirm"] = ("是否将version.dll和version.ini修改为{0}.dll和{0}.ini？", "Rename version.dll and version.ini to {0}.dll and {0}.ini?"),
            ["Dlg_ConfigName_Done"] = ("已修改为{0}.dll和{0}.ini\n请自行测试是否能进入游戏", "Renamed to {0}.dll and {0}.ini\nPlease test if game can launch"),
            ["Dlg_ConfigName_NotFound"] = ("未找到version.dll和version.ini\n请先确认高级模式已正确安装", "version.dll and version.ini not found\nPlease confirm Advanced Mode is installed"),
            ["Dlg_ConfigName_Conflict"] = ("检测到目录中已存在{0}.dll或{0}.ini\n请勿更改配置名称", "{0}.dll or {0}.ini already exists in directory\nDo not change config name"),
            ["Dlg_ConfigName_OnlyAdvanced"] = ("配置名修改仅对高级模式开放\n高级模式已集成所有功能", "Config Name Modify only available for Advanced Mode\nAdvanced Mode has all features integrated"),
            ["Dlg_ConfigName_DxgiNotFound"] = ("未找到dxgi.dll", "dxgi.dll not found"),
            ["Dlg_ConfigName_DxgiConflict"] = ("目标目录已存在d3d12.dll，请勿更改", "d3d12.dll already exists, do not change"),
            ["Dlg_ConfigName_DxgiDone"] = ("dxgi.dll已修改为d3d12.dll\n请自行测试是否能进入游戏", "dxgi.dll renamed to d3d12.dll\nPlease test if game can launch"),

            // 排错
            ["Dlg_Trouble_Title"] = ("排错修复 - 选择文件", "Troubleshoot - Select Files"),
            ["Dlg_Trouble_SelectTip"] = ("选择要替换的文件（灰色为游戏中不存在的文件，不可替换）", "Select files to replace (grey = not in game, cannot replace)"),
            ["Dlg_Trouble_SelectAll"] = ("全选（仅游戏中存在的文件可替换）", "Select All (only existing files)"),
            ["Dlg_Trouble_ReplaceSelected"] = ("替换选中文件", "Replace Selected"),
            ["Dlg_Trouble_Done"] = ("排错修复完成\n已替换{0}个文件", "Troubleshoot complete\n{0} files replaced"),
            ["Dlg_Trouble_NoPatch"] = ("未找到排错补丁文件", "No troubleshoot patch files found"),
            ["Dlg_Trouble_NoSelect"] = ("未选择任何文件", "No files selected"),
            ["Dlg_Trouble_NeedFrameGen"] = ("请先开启多帧生成功能后再使用排错修复", "Please enable Frame Gen first before using troubleshoot"),

            // 还原
            ["Dlg_Restore_Title"] = ("一键还原", "Restore All"),
            ["Dlg_Restore_Confirm"] = ("确定要还原所有已安装的补丁吗？", "Are you sure you want to restore all installed patches?"),
            ["Dlg_Restore_Done"] = ("还原完成", "Restore complete"),
            ["Dlg_Restore_NoBackup"] = ("未找到备份文件，无法还原", "No backup found, cannot restore"),
            ["Dlg_Restore_NoResponsibility_Title"] = ("无责还原", "No-Responsibility Restore"),
            ["Dlg_Restore_NoResponsibility_Warn"] = ("警告：本功能会删除游戏目录中的补丁文件，但不提供恢复游戏源文件的功能！\n\n如果是Steam或Epic平台的游戏，使用后请验证游戏完整性。\n\n如果是无法验证完整性的游戏，建议删除后重新开启多帧生成或DLSS5功能。",
                "WARNING: This function deletes patch files from game directory, but does NOT restore original game files!\n\nFor Steam/Epic games, verify game integrity after use.\n\nFor games that cannot verify integrity, recommend re-enabling Frame Gen or DLSS5 after deletion."),
            ["Dlg_Restore_NoResp_Done"] = ("已删除所有补丁文件", "All patch files deleted"),

            // 扫描和添加
            ["Dlg_Scan_Done"] = ("扫描完成，共找到 {0} 个游戏", "Scan complete, found {0} games"),
            ["Dlg_Add_Success"] = ("添加成功", "Added successfully"),
            ["Dlg_Add_Fail"] = ("无法识别该游戏目录", "Cannot recognize this game directory"),
            ["Dlg_Add_SelectExe"] = ("选择游戏exe文件", "Select game exe file"),
            ["Dlg_Add_SelectFolder"] = ("选择游戏文件夹", "Select game folder"),
            ["Dlg_Refresh_Done"] = ("状态已刷新", "Status refreshed"),

            // 启动弹窗
            ["Dlg_Startup_Title"] = ("温馨提示", "Friendly Reminder"),
            ["Dlg_Startup_Text1"] = ("本软件目前只在小黑盒免费分享，请勿倒卖。", "This software is only shared for free on Xiaoheihe, do not resell."),
            ["Dlg_Startup_Text2"] = ("本软件已免费开源，也可以前往Github上下载本软件。", "This software is open source and free, you can also download it from GitHub."),
            ["Dlg_Startup_Text3"] = ("如果您是在其他渠道购买本软件，请您立刻向渠道方申请退款！", "If you purchased this software from other channels, please request a refund immediately!"),
            ["Dlg_Startup_Github"] = ("GitHub 项目仓库", "GitHub Repository"),
            ["Dlg_Startup_Xiaoheihe"] = ("小黑盒链接", "Xiaoheihe Link"),
            ["Dlg_Startup_Understand"] = ("了解并不再弹出", "Understood, don't show again"),

            // 设置页面
            ["Settings_Title"] = ("设置", "Settings"),
            ["Settings_Appearance"] = ("外观", "Appearance"),
            ["Settings_Startup"] = ("启动", "Startup"),
            ["Settings_Gpu"] = ("显卡", "GPU"),
            ["Settings_GpuTip"] = ("如果自动检测出错，可以手动选择显卡", "If auto-detection fails, you can manually select GPU"),
            ["Settings_GpuHeader"] = ("选择显卡", "Select GPU"),
            ["Settings_About"] = ("关于", "About"),
            ["Settings_LightMode"] = ("浅色模式", "Light Mode"),
            ["Settings_Off"] = ("关闭", "Off"),
            ["Settings_On"] = ("开启", "On"),

            // 使用说明
            ["Usage_Title"] = ("使用说明", "Usage Guide"),

            // 更新内容
            ["Update_Title"] = ("更新内容", "Update Log"),
            ["Update_CheckBtn"] = ("检查更新", "Check for Updates"),
            ["Update_NoUpdate"] = ("当前已是最新版本", "You are on the latest version"),
            ["Update_Found"] = ("发现新版本：{0}", "New version found: {0}"),
            ["Update_CheckFail"] = ("检查更新失败，请检查网络连接", "Failed to check updates, please check your network"),
        };

        public static string T(string key)
        {
            if (_translations.TryGetValue(key, out var pair))
            {
                return IsEnglish ? pair.en : pair.zh;
            }
            return key;
        }
    }
}
