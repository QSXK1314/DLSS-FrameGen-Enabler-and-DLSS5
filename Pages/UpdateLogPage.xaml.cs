using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace DLSSFrameGenEnabler_WinUI3.Pages
{
    public sealed partial class UpdateLogPage : Page
    {
        public UpdateLogPage()
        {
            this.InitializeComponent();
            Translator.LanguageChanged += UpdateLanguageTexts;
            UpdateLanguageTexts();
        }

        private void UpdateLanguageTexts()
        {
            bool en = Translator.IsEnglish;
            TitleText.Text = en ? "Update Log" : "更新内容";
            CheckUpdateBtn.Content = en ? "Check Update" : "检查更新";
            DirectDownloadBtn.Content = en ? "Direct Download" : "直接下载最新版";

            // ===== 更新内容（根据版本类型显示不同内容）=====
            if (App.IsBeta)
            {
                // ===== Beta版：V1.15.2.0-Beta =====
                V11520BetaTitle.Text = "V1.15.2.0-Beta";
                V11520Beta_1.Text = en ?
                    "🔬 BETA: This is a beta version for testing new features. Stable version remains V1.14.0.0. Beta features will be synced to stable version after testing." :
                    "🔬 测试版：本版本为测试版，用于测试新功能。稳定版仍为V1.14.0.0。测试版功能经过测试后会同步到稳定版。";
                V11520Beta_2.Text = en ?
                    "NEW: Beta Feedback Guide dialog - Shows on startup after usage notes, guides users to export logs and send to developer when encountering issues, with 'Don't show again' checkbox" :
                    "新增：测试版反馈指引弹窗 - 在使用前说明弹窗后显示，引导用户遇到问题时导出日志并发给开发者，支持'不再显示此提示'复选框";
                V11520Beta_3.Text = en ?
                    "NEW: Export Logs feature - In Settings page, 'Debug & Support' section, click 'Export Logs to Desktop' to collect startup.log, error.log, save_debug.log, settings.json, and auto-generated system_info.txt (OS info, software settings, GPU info), packaged as zip" :
                    "新增：导出日志功能 - 在设置页面的「调试与支持」部分，点击「导出日志到桌面」，收集startup.log、error.log、save_debug.log、settings.json以及自动生成的system_info.txt（系统信息、软件设置、显卡信息），打包为zip";
                V11520Beta_4.Text = en ?
                    "NEW: Beta/Stable version separation - App.IsBeta flag controls version display. Beta shows V1.15.2.0-Beta, Stable shows V1.14.0.0. Easy to switch when publishing stable version." :
                    "新增：测试版/正式版版本号分离 - 通过App.IsBeta标志控制版本号显示。测试版显示V1.15.2.0-Beta，正式版显示V1.14.0.0。发布正式版时只需修改一个标志即可切换。";
                V11520Beta_5.Text = en ?
                    "FIX: Game list duplicate display - Improved deduplication logic, normalized paths (TrimEnd slashes + ToLower) before comparison, double insurance: dedupe inside auto-scan AND when adding to list" :
                    "修复：游戏列表重复显示 - 改进去重逻辑，规范化路径（去除结尾斜杠+转小写）后再比较，双重保险：自动扫描内部先去重，添加到列表时再次检查";
                V11520Beta_6.Text = en ?
                    "FIX: Uninstalled games still detected - Removed Retail folder special handling (was detecting games just because Retail folder exists), unified IsLikelyGameExe function for all 4 checkpoints, strengthened exclusion keywords (unins002, uninst, uninstaller, remove.exe, remover, installer etc.)" :
                    "修复：已卸载游戏仍被识别 - 删除Retail目录特殊处理（之前只要Retail目录存在就认为有游戏），统一使用IsLikelyGameExe函数应用于所有4个检查点，加强排除关键词（unins002、uninst、uninstaller、remove.exe、remover、installer等）";
                V11520Beta_7.Text = en ?
                    "FIX: Normal games missed (like REPO) - Removed all file size thresholds completely, only use IsLikelyGameExe to judge, avoids small game exe being filtered out" :
                    "修复：正常游戏被漏掉（如REPO） - 完全去掉所有文件大小阈值，只靠IsLikelyGameExe判断，避免小体积游戏exe被过滤掉";
                V11520Beta_8.Text = en ?
                    "UPDATED: Xiaoheihe link updated to latest version, with auto-migration for old links (any link not containing latest link_id will be automatically updated)" :
                    "更新：小黑盒链接更新为最新版本，旧链接自动迁移（任何不包含最新link_id的链接都会自动更新）";
            }
            else
            {
                // ===== 正式版：V1.14.1.0（只显示通用功能更新和bug修复）=====
                V11520BetaTitle.Text = "V1.14.1.0";
                V11520Beta_1.Text = en ?
                    "NEW: Export Logs feature - In Settings page, 'Debug & Support' section, click 'Export Logs to Desktop' to collect startup.log, error.log, save_debug.log, settings.json, and auto-generated system_info.txt (OS info, software settings, GPU info), packaged as zip" :
                    "新增：导出日志功能 - 在设置页面的「调试与支持」部分，点击「导出日志到桌面」，收集startup.log、error.log、save_debug.log、settings.json以及自动生成的system_info.txt（系统信息、软件设置、显卡信息），打包为zip";
                V11520Beta_2.Text = en ?
                    "NEW: Game list multi-select - Drag to select multiple games, Ctrl+Click to select/deselect individually, batch remove selected games, clear all games button" :
                    "新增：游戏列表多选功能 - 拖拽框选多个游戏，Ctrl+单击自行选择/取消选择，批量移除所选游戏，新增清除列表按钮";
                V11520Beta_3.Text = en ?
                    "IMPROVED: Auto-scan logic - Removed all file size thresholds, added loose mode for auto-scan, avoids missing normal games (like REPO), improved deduplication logic to prevent duplicate display" :
                    "改进：自动扫描逻辑 - 完全去掉所有文件大小阈值，自动扫描使用宽松模式，避免漏掉正常游戏（如REPO），改进去重逻辑防止重复显示";
                V11520Beta_4.Text = en ?
                    "FIX: Open game folder opened Documents folder - Now prioritizes ExePath directory (more reliable than GamePath), tries 3 methods to open folder, shows detailed error message with both paths" :
                    "修复：打开游戏目录打开的却是文档目录 - 现在优先使用ExePath所在目录（比GamePath更可靠），3种方式尝试打开目录，显示包含两个路径的详细错误提示";
                V11520Beta_5.Text = en ?
                    "FIX: Game list duplicate display - Improved deduplication logic, normalized paths (TrimEnd slashes + ToLower) before comparison, double insurance: dedupe inside auto-scan AND when adding to list" :
                    "修复：游戏列表重复显示 - 改进去重逻辑，规范化路径（去除结尾斜杠+转小写）后再比较，双重保险：自动扫描内部先去重，添加到列表时再次检查";
                V11520Beta_6.Text = en ?
                    "FIX: Uninstalled games still detected - Removed Retail folder special handling, unified IsLikelyGameExe function for all 4 checkpoints, strengthened exclusion keywords (unins002, uninst, uninstaller, remove.exe, remover, installer etc.)" :
                    "修复：已卸载游戏仍被识别 - 删除Retail目录特殊处理，统一使用IsLikelyGameExe函数应用于所有4个检查点，加强排除关键词（unins002、uninst、uninstaller、remove.exe、remover、installer等）";
                V11520Beta_7.Text = en ?
                    "FIX: Normal games missed (like REPO) - Removed all file size thresholds completely, only use IsLikelyGameExe to judge, avoids small game exe being filtered out" :
                    "修复：正常游戏被漏掉（如REPO） - 完全去掉所有文件大小阈值，只靠IsLikelyGameExe判断，避免小体积游戏exe被过滤掉";
                V11520Beta_8.Text = en ?
                    "UPDATED: Xiaoheihe link updated to latest version, with auto-migration for old links (any link not containing latest link_id will be automatically updated)" :
                    "更新：小黑盒链接更新为最新版本，旧链接自动迁移（任何不包含最新link_id的链接都会自动更新）";
            }

            // ===== V1.14.0.0 =====
            V11400Title.Text = "V1.14.0.0";
            V11400_1.Text = en ?
                "NEW: Manual Add Exe now supports ALL games (DX9/DX11/DX12), no longer forced to DX9 only. Detect Running Games also supports all games" :
                "新增：手动添加exe现在支持所有游戏（DX9/DX11/DX12），不再强制只支持DX9。检测运行中游戏也支持所有游戏";
            V11400_2.Text = en ?
                "NEW: Manual Add Directory skips hasRealGameExe check for network drives - users explicitly select the folder, so it must be a game folder, avoids Directory.Exists/GetFiles failures on network drives" :
                "新增：手动添加目录跳过hasRealGameExe检查（针对网络驱动器）- 用户明确选择了文件夹，肯定是游戏目录，避免网络驱动器上Directory.Exists/GetFiles失败";
            V11400_3.Text = en ?
                "CRITICAL FIX: 007 First Light and other games with digit-starting exe names were incorrectly excluded by recursive search logic (char.IsDigit(fileName[0]) && !Contains('shipping')). Removed this exclusion entirely" :
                "关键修复：007 First Light等以数字开头的exe被递归搜索逻辑错误排除（char.IsDigit(fileName[0]) && !Contains('shipping')）。完全移除此排除逻辑";
            V11400_4.Text = en ?
                "FIX: FindGameExeDirectory did not check Retail folder - games with exe in Retail directory (007 First Light etc.) could not find the real game exe. Added Retail/retail to commonSubDirs" :
                "修复：FindGameExeDirectory未检查Retail文件夹 - exe在Retail目录下的游戏（007 First Light等）无法找到真正的游戏exe。在commonSubDirs中添加了Retail/retail";
            V11400_5.Text = en ?
                "FIX: Manual Add Exe could not enable Frame Generation - was forced to DX9 mode (isDx9Manual: true), which disabled DX11/12 support and Frame Generation. Changed to isDx9Manual: false for auto DX detection" :
                "修复：手动添加exe无法开启多帧生成 - 之前强制为DX9模式（isDx9Manual: true），禁用了DX11/12支持和多帧生成。改为isDx9Manual: false自动检测DX支持";
            V11400_6.Text = en ?
                "IMPROVED: Retail folder uses recursive search (AllDirectories) to find exe in any subdirectory. Added 007 First Light special handling (exe in Retail folder). All checks wrapped in try-catch for robustness" :
                "改进：Retail文件夹使用递归搜索（AllDirectories）在任意子目录中查找exe。添加007 First Light特殊处理（exe在Retail文件夹）。所有检查都包裹在try-catch中以提高健壮性";
            V11400_7.Text = en ?
                "UI: Changed button text from 'Manual Mode (Select game exe, supports DX9 games)' to 'Manual Mode (Select game exe)' since it now supports all games, not just DX9" :
                "界面：按钮文字从'手动模式（自行选择游戏exe，支持dx9游戏）'改为'手动模式（自行选择游戏exe）'，因为现在支持所有游戏，不只是DX9";

            // ===== V1.13.0.0 =====
            V11300Title.Text = "V1.13.0.0";
            V11300_1.Text = en ?
                "NEW: Detect Running Games feature - Enumerate system processes via Win32 API (CreateToolhelp32Snapshot + QueryFullProcessImageName), 100% accurate game exe path, just like Task Manager's 'Open file location'" :
                "新增：检测运行中游戏功能 - 通过纯Win32 API（CreateToolhelp32Snapshot + QueryFullProcessImageName）枚举系统进程，100%准确获取游戏真正的exe路径，模仿任务管理器'打开文件所在位置'";
            V11300_2.Text = en ?
                "Process list with icons: Extract exe file icons via SHGetFileInfo Win32 API, display 24x24 icons next to process name for easy identification" :
                "进程列表显示图标：通过SHGetFileInfo Win32 API提取exe文件图标，在进程名旁边显示24x24图标，方便用户辨认";
            V11300_3.Text = en ?
                "NEW: Usage Notes dialog on startup - Shows 6 important notes after startup dialog (enable DLSS first, miHoYo not supported, Ubisoft/EA may not be fully compatible, Vulkan not supported, backup game files, anti-cheat risk), with 'Don't show again' checkbox" :
                "新增：启动时使用前说明弹窗 - 在启动弹窗后显示6条重要注意事项（先开启DLSS、米哈游不支持、育碧EA可能不完全适配、Vulkan不支持、备份游戏文件、反作弊风险），支持'不再显示'复选框";
            V11300_4.Text = en ?
                "Fixed ContentDialog crash: Changed all ContentDialog initialization from InitializeWithWindow (Win32 interop) to XamlRoot (WinUI3 native), resolved 'Specified cast is not valid' error" :
                "修复ContentDialog崩溃：将所有ContentDialog初始化从InitializeWithWindow（Win32互操作）改为XamlRoot（WinUI3原生），解决'Specified cast is not valid'错误";
            V11300_5.Text = en ?
                "Fixed restore preview inconsistency: PreviewRestoreFrameGen and PreviewRestoreDLSS5 now use EXACTLY the same logic as actual Restore (IsAddedPatchFile function), preview and actual results are 100% consistent" :
                "修复还原预览不一致：PreviewRestoreFrameGen和PreviewRestoreDLSS5现在使用与实际还原完全一致的逻辑（IsAddedPatchFile函数），预览和实际结果100%一致";
            V11300_6.Text = en ?
                "Improved running game detection prompt: Added warning to enable DLSS/Frame Generation in game settings FIRST, then close game, then install patches" :
                "优化运行中游戏检测提示：添加提醒，告知用户必须先在游戏设置中开启DLSS/帧生成相关功能，然后关闭游戏，最后再安装补丁";

            // ===== V1.12.9.0 =====
            V11290Title.Text = "V1.12.9.0";
            V11290_1.Text = en ?
                "Critical fix: Restore function was deleting game native files (plugins folder, nvngx files etc.). Simplified restore logic to stable pattern matching, DLSS5 and Frame Gen restore now use EXACTLY the same logic" :
                "关键修复：还原功能误删游戏原生文件（plugins文件夹、nvngx文件等）。简化还原逻辑为稳定的模式匹配，DLSS5和多帧生成的还原现在使用完全一致的逻辑";
            V11290_2.Text = en ?
                "Restore safety: Only delete explicitly added patch files (ReShade*, dxgi.dll, d3d9.dll etc.). Files that may be game native (nvngx_dlss.dll, nvngx_dlssg.dll, sl.*) are only restored if backup exists, otherwise left untouched" :
                "还原安全保障：只删除明确添加的补丁文件（ReShade*、dxgi.dll、d3d9.dll等）。可能是游戏原生的文件（nvngx_dlss.dll、nvngx_dlssg.dll、sl.*等）只有备份存在时才恢复，否则完全不动";
            V11290_3.Text = en ?
                "Folder protection: Never delete plugins, Streamline, Fonts folders that may be game native. Only delete patch-created folders: reshade-shaders, runtime, host64" :
                "文件夹保护：绝不删除可能是游戏原生的plugins、Streamline、Fonts文件夹。只删除补丁创建的文件夹：reshade-shaders、runtime、host64";
            V11290_4.Text = en ?
                "Removed complex snapshot feature that caused instability: Backup manifest and file pattern matching is sufficient and reliable for restore operations" :
                "移除导致不稳定的复杂快照功能：备份清单和文件模式匹配对于还原操作已经足够且可靠";

            // ===== V1.12.8.3 =====
            V11283Title.Text = "V1.12.8.3";
            V11283_1.Text = en ?
                "Fixed crash on Usage page: NullReferenceException in UpdateLanguageTexts when XAML elements not fully initialized" :
                "修复使用说明页面崩溃：XAML元素未完全初始化时UpdateLanguageTexts出现空引用异常";
            V11283_2.Text = en ?
                "Fixed crash on download feature: ContentDialog conflict when multiple dialogs try to open simultaneously (loading dialog + changelog dialog + error dialog)" :
                "修复下载功能崩溃：多个对话框同时打开时的ContentDialog冲突（加载对话框+更新内容对话框+错误对话框）";
            V11283_3.Text = en ?
                "Fixed crash on Settings page: InvalidCastException when XAML generated cache out of sync, resolved by clean rebuild" :
                "修复设置页面崩溃：XAML生成缓存不同步导致的类型转换异常，通过清理并重新编译解决";
            V11283_4.Text = en ?
                "Fixed software won't launch on many computers: Missing Visual C++ Redistributable runtime. Release package now includes ALL required runtimes (.NET, Windows App SDK, VC++), extract and run directly, no installation needed" :
                "修复很多电脑无法打开软件的问题：缺少Visual C++ Redistributable运行时。发布版现已包含所有所需运行时（.NET、Windows App SDK、VC++），解压即用，无需安装任何组件";
            V11283_5.Text = en ?
                "Added VC++ runtime auto-detection: Checks registry and system DLLs on startup, shows bilingual prompt if missing" :
                "新增VC++运行时自动检测：启动时检查注册表和系统DLL，如缺少则弹出中英文提示";
            V11283_6.Text = en ?
                "Added bilingual launchers: 启动软件.bat (Chinese) and Launch.bat (English), auto-detects VC++ runtime and Windows version before launching" :
                "新增中英文启动器：启动软件.bat（中文）和Launch.bat（英文），启动前自动检测VC++运行时和Windows版本";
            V11283_7.Text = en ?
                "Updated usage guide: Added system requirements section (Windows 10 1809+, VC++ runtime, GPU requirements, latest drivers)" :
                "更新使用说明：添加系统要求部分（Windows 10 1809+、VC++运行时、显卡要求、最新驱动）";

            // ===== V1.12.5.2 =====
            V11252Title.Text = "V1.12.5.2";
            V11252_1.Text = en ?
                "Added tip for Pure Transparent backdrop: switching themes requires app restart to display correct colors" :
                "纯透明材质添加提示说明：切换深浅色模式后需要重启软件才能显示正确颜色";
            V11252_2.Text = en ?
                "Added vertical scrolling to Settings page, no need to resize window to see all options" :
                "设置页面添加上下滚动功能，无需扩大窗口即可查看所有设置项";
            V11252_3.Text = en ?
                "Adjusted dark mode transparency color depth for better visual experience" :
                "调整深色模式透明材质的颜色深度，优化视觉体验";
            V11252_4.Text = en ?
                "Fixed Pure Transparent backdrop blur effect, restored to non-blurred pure transparency" :
                "修复纯透明材质带模糊效果的问题，恢复为无模糊的纯透明效果";
            V11252_5.Text = en ?
                "Fixed Settings page not loading after XAML changes, ensured all pages are verified after modifications" :
                "修复修改XAML后设置页面无法加载的问题，后续修改页面后均会验证页面可正常打开";

            // ===== V1.12.3.1 =====
            V11231Title.Text = "V1.12.3.1";
            V11231_1.Text = en ?
                "Theme switch wipe animation: PPT-style wipe animation for dark/light mode switching, with blurred edge, bidirectional playback, and color matching for all backdrop materials" :
                "深浅色切换擦除动画：实现类似PPT擦除动画的深浅色切换效果，边缘模糊处理，双向动画（浅色→深色和深色→浅色），适配各种材质颜色";
            V11231_2.Text = en ?
                "Page transition animation enhanced: SlideNavigationTransitionInfo slide effect, from left to right (matches left navigation bar visual flow)" :
                "页面切换动画增强：使用SlideNavigationTransitionInfo滑动过渡效果，从左往右滑入（符合左侧导航栏的视觉习惯）";
            V11231_3.Text = en ?
                "Animation master switch: Renamed from 'Page Transition Animation' to 'Animation Effects', controls all animations including page transitions and theme switches to reduce performance usage" :
                "动画效果总开关：从'页面切换动画'改名为'动画效果'，关闭后禁止页面切换和深浅色切换等所有动画，以减少性能占用";
            V11231_4.Text = en ?
                "Transparent material color alignment: Fixed navigation panel and content area color mismatch in both light and dark modes, wipe animation colors now match actual material colors" :
                "透明材质颜色对齐：修复浅色和深色模式下导航栏与右边界面颜色不一致的问题，擦除动画颜色与实际材质颜色对齐";
            V11231_5.Text = en ?
                "Fixed Settings page not loading: XAML connection error after adding new control, resolved by cleaning and rebuilding project to regenerate auto-generated code and xbf files" :
                "修复设置页面无法加载：添加新控件后XAML连接代码与xbf文件不同步导致类型转换错误，通过清理并重新生成项目解决";

            // ===== V1.12.0.0 =====
            V112Title.Text = "V1.12.0.0";
            V112_1.Text = en ? "Completely rewritten UI with WinUI 3 framework" : "使用WinUI 3框架完全重写UI界面";
            V112_2.Text = en ? "Completely rewritten core functionality logic" : "核心功能逻辑完全重写";
            V112_3.Text = en ? "Support RTX 20/30 series Frame Generation" : "支持RTX20系和30系开启多帧生成";
            V112_4.Text = en ? "Support Where Winds Meet (Yan Yun) DLSS5 + Frame Generation" : "支持燕云十六声开启DLSS5+多帧生成";
            V112_5.Text = en ? "Support RE Engine Frame Generation" : "支持RE引擎开启多帧生成";
            V112_6.Text = en ? "Support DX9 64-bit games DLSS5" : "支持DX9 64位游戏开启DLSS5";
            V112_7.Text = en ? "Support Chinese/English language switching" : "支持中英文切换";
            V112_8.Text = en ? "Added custom wallpaper feature" : "新增自定义壁纸功能";
            V112_9.Text = en ? "Added backdrop material selection (Mica/Acrylic/Gaussian Blur/Custom)" : "新增界面材质选择（云母/亚克力/高斯模糊/自定义壁纸）";
            V112_10.Text = en ? "Added icons to About page and startup dialog links" : "关于页面和启动弹窗链接添加图标";
            V112_11.Text = en ? "Fixed window title bar icon display" : "修复窗口左上角图标显示";
            V112_12.Text = en ? "Optimized dark/light mode switching" : "优化深色模式/浅色模式切换";
            V112_13.Text = en ? "Added GPU selection feature (auto-detect + manual, prefer dedicated GPU)" : "新增显卡选择功能（自动检测+手动选择，优先独立显卡）";
            V112_14.Text = en ? "Added right-click context menu (open directory, remove from list, quick actions)" : "新增右键菜单功能（打开目录、移除列表、各功能快捷操作）";
            V112_15.Text = en ? "Optimized game detection logic (exclude engine directories, RE engine detection, Cyberpunk 2077 special handling)" : "优化游戏识别逻辑（排除引擎目录、RE引擎识别、2077特殊处理）";

            // ===== V1.10.3.2 =====
            V110Title.Text = "V1.10.3.2";
            V110_1.Text = en ?
                "New GPU Support: RTX 20/30 series Frame Generation, auto-detect GPU model, RTX 40 series continues Classic/Advanced mode" :
                "新增显卡支持：支持RTX 20系/30系显卡开启多帧生成，软件自动检测显卡型号调用对应补丁，RTX 40系继续使用经典模式和高级模式";
            V110_2.Text = en ?
                "RE Engine Frame Generation: Support RE engine games (RE9, Onimusha, Pragmata etc.), auto-detect via re_chunk_000.pak, mutually exclusive with DLSS5" :
                "新增RE引擎多帧生成支持：支持RE引擎游戏开启多帧生成（生化危机9、鬼武者、识质存在等），通过检测re_chunk_000.pak自动识别，与DLSS5互斥";
            V110_3.Text = en ?
                "Where Winds Meet special adaptation: Support DLSS5 + Frame Generation simultaneously, special Engine\\Binaries\\Win64r directory detection, Chinese font support" :
                "燕云十六声特殊适配：支持同时开启DLSS5+多帧生成，特殊识别Engine\\Binaries\\Win64r目录，中文字体支持（MiSans-Bold.ttf）";
            V110_4.Text = en ?
                "DX9 DLSS5 improved: DX9 32-bit (dgVoodoo2 + DLSS5-Feeder cross-process), DX9 64-bit (direct loading), manual exe selection, DLL replacement feature" :
                "DX9 DLSS5完善：支持DX9 32位（dgVoodoo2+DLSS5-Feeder跨进程方案）和64位（直接加载方案），手动添加可自行选择exe，替换dll功能自动移动dgVoodoo2补丁到运行dll目录";
            V110_5.Text = en ?
                "Auto-scan platforms extended: EA, Ubisoft, GOG platform library auto-scan added, continue support Steam and Epic" :
                "自动扫描平台扩展：新增EA、育碧、GOG平台游戏库自动扫描，继续支持Steam和Epic平台";
            V110_6.Text = en ?
                "Right-click context menu: Quick actions for frame gen, DLSS5, DX9 DLSS5, troubleshooting, remove from list" :
                "右键菜单功能：游戏列表右键可快速操作多帧生成、DLSS5、DX9 DLSS5、排错修复、从列表移除";
            V110_7.Text = en ?
                "No-fault restore: For games without backup files, with severe warning before use" :
                "无责还原功能：无备份文件的游戏可使用无责还原删除补丁，使用前严重警告可能删除游戏源文件";
            V110_8.Text = en ?
                "RE engine mutual exclusion logic fixed: Fixed buttons still clickable after enabling DLSS5/Frame Gen, fixed right-click menu logic, fixed button overlap issue" :
                "RE引擎互斥逻辑修复：修复开启DLSS5后多帧生成按钮仍可交互的问题，修复右键菜单互斥逻辑不生效，修复按钮重叠导致点击还原按钮的问题";
            V110_9.Text = en ?
                "Other fixes: reshade-shaders naming, version sync, light mode list alternating colors, manual exe frame gen detection, uninstalled game placeholder, engine exe misidentification, Neverness To Everness path, Half-Life 2 bin directory, 2077 restore, render API detection" :
                "其他修复：reshade-shaders目录命名、版本号不同步、浅色模式列表黑白交替、手动添加exe无法识别多帧生成、卸载游戏占位符误加入、引擎exe误识别、异环特殊路径、半条命2 bin目录识别、2077专用补丁还原、渲染API检测逻辑";

            // ===== V1.9.7.2 =====
            V197Title.Text = "V1.9.7.2";
            V197_1.Text = en ?
                "Core runtime update: nvngx_dlss.dll updated from 310.8.0.0 to 310.9.1.0, nvngx_dlssg.dll (Advanced) updated to 310.9.1.0, Classic and RTX20 remain unchanged for compatibility" :
                "核心运行时更新：nvngx_dlss.dll从310.8.0.0更新到310.9.1.0（最新版），nvngx_dlssg.dll（高级模式）更新到310.9.1.0，经典模式和RTX20系专用版本保持不变（兼容性需要）";
            V197_2.Text = en ?
                "About page community credits: Added acknowledgment area listing all community open source projects and authors" :
                "关于页面添加社区作者致谢：新增致谢区域，列出所有使用的社区开源项目及作者（DLSS-Enabler、RTX40MFG-Unlock、RenoDX、DLSS5-Feeder、ReShade等）";

            // ===== V1.8.6.8 =====
            V1868Title.Text = "V1.8.6.8";
            V1868_1.Text = en ?
                "DX9 64-bit reshade-shaders missing fix (critical): Fixed GetSharedDirectory method not handling subdirectory extraction correctly" :
                "DX9 64位reshade-shaders缺失修复（关键修复）：修复GetSharedDirectory方法未正确处理子目录提取的bug，添加缓存完整性检查";
            V1868_2.Text = en ?
                "Source engine dgvoodoo bin directory fix: Fixed Half-Life 2 etc. dgvoodoo patches not recognizing bin directory, added Source engine special handling (highest priority)" :
                "起源引擎dgvoodoo bin目录识别修复：修复半条命2等Source引擎游戏dgvoodoo补丁无法正确识别bin目录的问题，添加Source引擎特殊处理（最高优先级）";
            V1868_3.Text = en ?
                "DX9 detection logic improved: New DetectRenderAPIType method returns 4 render API types, stricter detection: exe references d3d9.dll and not d3d11/d3d12/dxgi" :
                "DX9检测逻辑改进：新增DetectRenderAPIType方法返回4种渲染API类型，检测逻辑更严格：exe引用d3d9.dll且不引用d3d11/d3d12/dxgi才判定为DX9";
            V1868_4.Text = en ?
                "Game exe detection logic enhanced: Support Win64r variant directory names (e.g. Where Winds Meet Engine\\Binaries\\Win64r)" :
                "游戏exe识别逻辑加强：支持Win64r等变体目录名（如燕云十六声的Engine\\Binaries\\Win64r）";

            // ===== V1.8.6.5 =====
            V1865Title.Text = "V1.8.6.5";
            V1865_1.Text = en ?
                "UI refactor (WinUI3 style): New left navigation bar layout, reference Windows 11 Settings, 5 pages: Home, Settings, Usage, Update Log, About" :
                "UI重构（模仿WinUI3风格）：全新左侧导航栏布局，参考Windows 11设置界面，主页、设置、使用说明、更新内容、关于五个页面";
            V1865_2.Text = en ?
                "About page: Software introduction, version info, author links: GitHub, Bilibili, Xiaoheihe" :
                "关于页面：软件介绍、版本信息、作者栏：GitHub、哔哩哔哩、小黑盒链接";
            V1865_3.Text = en ?
                "Light/Dark mode: All pages fully support light and dark mode" :
                "浅色/深色模式：所有页面完整适配浅色和深色模式";

            // ===== 历史版本 =====
            HistoryTitle.Text = en ? "Historical Versions" : "历史版本";
            History_1.Text = en ? "V1.8.6: Size optimization, Shared directory for duplicate large files" : "V1.8.6：体积优化，建立Shared共享目录存放重复大文件";
            History_2.Text = en ? "V1.8.5: No-fault restore feature" : "V1.8.5：无责还原功能";
            History_3.Text = en ? "V1.8.4: DX9 DLSS5 feature (32-bit)" : "V1.8.4：DX9 DLSS5功能（32位）";
            History_4.Text = en ? "V1.8.3: RE engine universal DLSS5 patch" : "V1.8.3：RE引擎通用DLSS5补丁";
            History_5.Text = en ? "V1.8.2: GPU detection optimization (prefer dedicated GPU)" : "V1.8.2：显卡检测优化（优先独立显卡）";
            History_6.Text = en ? "V1.8.1: Backup restore enhanced (snapshot comparison)" : "V1.8.1：备份还原加强（快照对比）";
            History_7.Text = en ? "V1.8.0: DLSS5 feature (NVIDIA/AMD)" : "V1.8.0：DLSS5功能（N卡/A卡）";
            History_8.Text = en ? "V1.7.x: Config name modification feature" : "V1.7.x：配置名修改功能";
            History_9.Text = en ? "V1.7.0: Classic/Advanced dual mode" : "V1.7.0：经典/高级双模式";
            History_10.Text = en ? "V1.6.x: Full disk scan + disclaimer" : "V1.6.x：全盘扫描 + 免责声明";
            History_11.Text = en ? "V1.5.x: Light/dark mode" : "V1.5.x：浅色/深色模式";
            History_12.Text = en ? "V1.4.x: Troubleshooting feature" : "V1.4.x：排错修复功能";
            History_13.Text = en ? "V1.3.x: Cyberpunk 2077 exclusive patch" : "V1.3.x：2077专用补丁";
            History_14.Text = en ? "V1.2.x: Auto-scan Epic library" : "V1.2.x：自动扫描Epic库";
            History_15.Text = en ? "V1.1.x: Manual game addition" : "V1.1.x：手动添加游戏";
            History_16.Text = en ? "V1.0 ~ V1.1: Basic features (Classic mode frame gen, Steam library scan, one-click restore)" : "V1.0 ~ V1.1：基础功能（经典模式多帧生成、Steam库扫描、一键还原）";

            // ===== 重要说明 =====
            NoticeTitle.Text = en ? "Important Notes" : "重要说明";
            Notice_1.Text = en ?
                "Due to strict anti-cheat in miHoYo games, adaptation for miHoYo games is not included. Recommended to use Magpie, HoYoshade, or XXMI." :
                "由于米哈游系列游戏反作弊较为严苛，暂不加入对米哈游游戏的适配，建议使用【大力喜鹊(Magpie)】或【HoYoshade】【XXMI】开启DLSS5和多帧生成功能。";
            Notice_2.Text = en ?
                "Due to Vulkan not being as widely used as DX, Vulkan adaptation is temporarily not included. Thank you for your understanding." :
                "由于Vulkan使用率并没有DX广泛，暂时也不加入适配，还请玩家们见谅。个人时间和精力实在有限。";
        }

        // 直接下载最新版（不检查版本）
        private async void DirectDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            await DownloadFromGitHubAsync();
        }

        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var json = await client.GetStringAsync(Services.SettingsService.Instance.UpdateCheckUrl);
                // 允许尾随逗号，避免用户的JSON格式不规范导致解析失败
                var jsonOptions = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                using var doc = JsonDocument.Parse(json, jsonOptions);
                var root = doc.RootElement;
                var latestVersion = root.GetProperty("version").GetString();
                var currentVersion = App.CurrentVersion; // 使用App中定义的版本号（支持Beta版本号）

                // 解析下载链接（同时支持平铺格式 downloadUrlXxx 和嵌套格式 downloadLinks.xxx）
                string githubUrl = SettingsService.Instance.GithubUrl;
                string kuakeUrl = root.TryGetProperty("downloadUrlKuake", out var k) ? k.GetString() : "";
                string baiduUrl = root.TryGetProperty("downloadUrlBaidu", out var b) ? b.GetString() : "";
                string pan123Url = root.TryGetProperty("downloadUrl123pan", out var l) ? l.GetString() : "";
                
                // 兼容嵌套格式 downloadLinks
                if (root.TryGetProperty("downloadLinks", out var dl))
                {
                    if (string.IsNullOrEmpty(kuakeUrl) && dl.TryGetProperty("kuake", out var dk)) kuakeUrl = dk.GetString();
                    if (string.IsNullOrEmpty(baiduUrl) && dl.TryGetProperty("baidu", out var db)) baiduUrl = db.GetString();
                    if (string.IsNullOrEmpty(pan123Url) && dl.TryGetProperty("pan123", out var dl123)) pan123Url = dl123.GetString();
                    if (dl.TryGetProperty("github", out var dgh)) githubUrl = dgh.GetString();
                }

                if (IsNewerVersion(latestVersion, currentVersion))
                {
                    var panel = new StackPanel { Spacing = 8 };
                    panel.Children.Add(new TextBlock
                    {
                        Text = string.Format(Translator.IsEnglish ?
                            "Latest version: {0}\nCurrent version: {1}" :
                            "最新版本：{0}\n当前版本：{1}", latestVersion, currentVersion),
                        TextWrapping = TextWrapping.Wrap
                    });
                    panel.Children.Add(new TextBlock
                    {
                        Text = Translator.IsEnglish ?
                            "Click Update to choose a download source." :
                            "点击更新选择下载渠道。",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.7
                    });

                    var dialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "New Version Found" : "发现新版本",
                        Content = panel,
                        PrimaryButtonText = Translator.IsEnglish ? "Update" : "更新",
                        CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                        XamlRoot = this.Content.XamlRoot
                    };
                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        // 弹出下载渠道选择
                        await ShowDownloadSourceDialog(githubUrl, kuakeUrl, baiduUrl, pan123Url);
                    }
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "Up to Date" : "已是最新版本",
                        Content = Translator.IsEnglish ? "You are using the latest version." : "您正在使用最新版本。",
                        CloseButtonText = Translator.IsEnglish ? "OK" : "知道了",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Check Failed" : "检查失败",
                    Content = string.Format(Translator.IsEnglish ? "Failed to check for updates: {0}" : "检查更新失败：{0}", ex.Message),
                    CloseButtonText = Translator.IsEnglish ? "OK" : "知道了",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        // 版本比较：判断remote是否比current新（支持Beta版本号，如 V1.15.2.0-Beta）
        private static bool IsNewerVersion(string remote, string current)
        {
            try
            {
                // 去掉开头的V/v
                remote = remote.TrimStart('V', 'v');
                current = current.TrimStart('V', 'v');
                
                // 去掉后缀（如 -Beta、-alpha、-rc 等），只比较数字部分
                int betaIndex = remote.IndexOf('-');
                if (betaIndex > 0) remote = remote.Substring(0, betaIndex);
                betaIndex = current.IndexOf('-');
                if (betaIndex > 0) current = current.Substring(0, betaIndex);
                
                if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(current, out var currentVer))
                {
                    return remoteVer > currentVer;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // 显示下载渠道选择对话框
        private async System.Threading.Tasks.Task ShowDownloadSourceDialog(string githubUrl, string kuakeUrl, string baiduUrl, string pan123Url)
        {
            ContentDialog dialog = null;
            
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = Translator.IsEnglish ? "Please choose a download source:" : "请选择下载渠道：",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            // GitHub直接下载按钮
            var directDownloadBtn = new Button
            {
                Content = Translator.IsEnglish ? "Direct Download (GitHub)" : "直接下载（GitHub）",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            directDownloadBtn.Click += async (s, e) => 
            {
                dialog?.Hide(); // 先关闭当前对话框
                await System.Threading.Tasks.Task.Delay(300); // 等待对话框完全关闭
                await DownloadFromGitHubAsync();
            };
            panel.Children.Add(directDownloadBtn);

            // GitHub按钮
            var githubBtn = new Button
            {
                Content = "GitHub " + (Translator.IsEnglish ? "(Open in browser)" : "（浏览器打开）"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = githubUrl
            };
            githubBtn.Click += (s, e) => 
            {
                dialog?.Hide();
                OpenUrl((string)((Button)s).Tag);
            };
            panel.Children.Add(githubBtn);

            // 夸克网盘按钮
            if (!string.IsNullOrEmpty(kuakeUrl))
            {
                var kuakeBtn = new Button
                {
                    Content = Translator.IsEnglish ? "Quark Cloud Drive" : "夸克网盘",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = kuakeUrl
                };
                kuakeBtn.Click += (s, e) => 
                {
                    dialog?.Hide();
                    OpenUrl((string)((Button)s).Tag);
                };
                panel.Children.Add(kuakeBtn);
            }

            // 百度网盘按钮
            if (!string.IsNullOrEmpty(baiduUrl))
            {
                var baiduBtn = new Button
                {
                    Content = Translator.IsEnglish ? "Baidu Cloud Drive" : "百度网盘",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = baiduUrl
                };
                baiduBtn.Click += (s, e) => 
                {
                    dialog?.Hide();
                    OpenUrl((string)((Button)s).Tag);
                };
                panel.Children.Add(baiduBtn);
            }

            // 123云盘按钮
            if (!string.IsNullOrEmpty(pan123Url))
            {
                var pan123Btn = new Button
                {
                    Content = Translator.IsEnglish ? "123Pan" : "123云盘",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = pan123Url
                };
                pan123Btn.Click += (s, e) => 
                {
                    dialog?.Hide();
                    OpenUrl((string)((Button)s).Tag);
                };
                panel.Children.Add(pan123Btn);
            }

            dialog = new ContentDialog
            {
                Title = Translator.IsEnglish ? "Choose Download Source" : "选择下载渠道",
                Content = panel,
                CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        // 从GitHub直接下载
        private async System.Threading.Tasks.Task DownloadFromGitHubAsync()
        {
            // 当前主题
            var theme = SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light;
            try
            {
                
                // 先显示正在获取下载信息的提示
                var loadingDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Please wait" : "请稍候",
                    Content = Translator.IsEnglish ? "Getting download information from GitHub..." : "正在从GitHub获取下载信息...",
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };
                var loadingTask = loadingDialog.ShowAsync();

                // 从GitHub API获取最新release的下载链接
                // 正式版使用 /releases/latest（只返回正式版，不包含pre-release）
                // Beta版使用 /releases，然后筛选 prerelease=true 的最新版本
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DLSSFrameGenEnabler");
                
                System.Text.Json.JsonElement releaseElement = default;
                if (App.IsBeta)
                {
                    // Beta版：获取所有releases，筛选最新的pre-release
                    var apiUrlAll = "https://api.github.com/repos/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases?per_page=20";
                    var jsonAll = await client.GetStringAsync(apiUrlAll);
                    using var docAll = System.Text.Json.JsonDocument.Parse(jsonAll);
                    var releases = docAll.RootElement;
                    
                    // 找到第一个 prerelease=true 的版本（按发布时间倒序，第一个就是最新的Beta版）
                    bool foundBeta = false;
                    foreach (var rel in releases.EnumerateArray())
                    {
                        if (rel.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())
                        {
                            releaseElement = rel.Clone();
                            foundBeta = true;
                            break;
                        }
                    }
                    
                    if (!foundBeta)
                    {
                        loadingDialog.Hide();
                        await System.Threading.Tasks.Task.Delay(200);
                        var errDialog = new ContentDialog
                        {
                            Title = Translator.IsEnglish ? "Download Failed" : "下载失败",
                            Content = Translator.IsEnglish ?
                                "No beta version found in GitHub releases.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." :
                                "在GitHub releases中没有找到测试版。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                            CloseButtonText = "OK",
                            RequestedTheme = theme,
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errDialog.ShowAsync();
                        return;
                    }
                }
                else
                {
                    // 正式版：获取latest release（不包含pre-release）
                    var apiUrl = "https://api.github.com/repos/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases/latest";
                    var json = await client.GetStringAsync(apiUrl);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    releaseElement = doc.RootElement.Clone();
                }
                
                var assets = releaseElement.GetProperty("assets");
                if (assets.GetArrayLength() == 0)
                {
                    loadingDialog.Hide();
                    await System.Threading.Tasks.Task.Delay(200);
                    var errDialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "Download Failed" : "下载失败",
                        Content = Translator.IsEnglish ?
                            "No downloadable files found in the latest release.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." :
                            "最新版本中没有找到可下载的文件。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                        CloseButtonText = "OK",
                        RequestedTheme = theme,
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errDialog.ShowAsync();
                    return;
                }

                var asset = assets[0];
                var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                var fileName = asset.GetProperty("name").GetString();
                var fileSize = asset.GetProperty("size").GetInt64();
                
                // 读取更新内容
                var version = releaseElement.TryGetProperty("tag_name", out var tag) ? tag.GetString() : "";
                var changelog = releaseElement.TryGetProperty("body", out var body) ? body.GetString() : "";

                loadingDialog.Hide();
                await System.Threading.Tasks.Task.Delay(200);

                // 显示更新内容对话框，让用户确认是否下载
                var changelogPanel = new StackPanel { Spacing = 12 };
                changelogPanel.Children.Add(new TextBlock
                {
                    Text = string.Format(Translator.IsEnglish ? "Latest Version: {0}" : "最新版本：{0}", version),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 16
                });
                changelogPanel.Children.Add(new TextBlock
                {
                    Text = string.Format(Translator.IsEnglish ? "File Size: {0:0.0} MB" : "文件大小：{0:0.0} MB", fileSize / 1024.0 / 1024.0),
                    Opacity = 0.7
                });
                
                if (!string.IsNullOrEmpty(changelog))
                {
                    changelogPanel.Children.Add(new TextBlock
                    {
                        Text = Translator.IsEnglish ? "Update Notes:" : "更新内容：",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Margin = new Thickness(0, 8, 0, 0)
                    });
                    
                    // 使用ScrollViewer来显示可能很长的更新内容
                    var scrollViewer = new ScrollViewer
                    {
                        MaxHeight = 300,
                        VerticalScrollMode = ScrollMode.Enabled,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };
                    var changelogText = new TextBlock
                    {
                        Text = changelog,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.9
                    };
                    scrollViewer.Content = changelogText;
                    changelogPanel.Children.Add(scrollViewer);
                }

                // 添加加速器提示
                changelogPanel.Children.Add(new TextBlock
                {
                    Text = Translator.IsEnglish ?
                        "Tip: Users in mainland China may need a VPN/accelerator to download from GitHub." :
                        "提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                    FontSize = 12,
                    Opacity = 0.7,
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });

                var changelogDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Update Available" : "发现新版本",
                    Content = changelogPanel,
                    PrimaryButtonText = Translator.IsEnglish ? "Download" : "下载",
                    CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };
                var changelogResult = await changelogDialog.ShowAsync();
                if (changelogResult != ContentDialogResult.Primary) return;

                // 让用户选择下载文件夹
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");
                
                // 获取窗口句柄（通过App.MainWindow）
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null) return;

                // 显示下载进度对话框
                var progressPanel = new StackPanel { Spacing = 12 };
                var progressText = new TextBlock
                {
                    Text = string.Format(Translator.IsEnglish ? "Downloading: {0}\nSize: {1:0.0} MB" : "正在下载：{0}\n大小：{1:0.0} MB", fileName, fileSize / 1024.0 / 1024.0),
                    TextWrapping = TextWrapping.Wrap
                };
                var progressBar = new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Height = 20,
                    IsIndeterminate = false
                };
                var speedText = new TextBlock
                {
                    Text = Translator.IsEnglish ? "Connecting..." : "正在连接...",
                    Opacity = 0.7
                };
                // 暂停和取消按钮
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var pauseBtn = new Button
                {
                    Content = Translator.IsEnglish ? "Pause" : "暂停",
                    Width = 100
                };
                var cancelBtn = new Button
                {
                    Content = Translator.IsEnglish ? "Cancel" : "取消",
                    Width = 100
                };
                buttonPanel.Children.Add(pauseBtn);
                buttonPanel.Children.Add(cancelBtn);
                progressPanel.Children.Add(progressText);
                progressPanel.Children.Add(progressBar);
                progressPanel.Children.Add(speedText);
                progressPanel.Children.Add(buttonPanel);

                var progressDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Downloading" : "下载中",
                    Content = progressPanel,
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };

                // 使用CancellationTokenSource来支持取消
                var cts = new System.Threading.CancellationTokenSource();
                // 暂停控制
                var pauseEvent = new System.Threading.ManualResetEventSlim(true);
                bool isPaused = false;
                // 取消时是否删除文件
                bool deleteOnCancel = false;

                // 暂停按钮事件
                pauseBtn.Click += (s, e) =>
                {
                    isPaused = !isPaused;
                    if (isPaused)
                    {
                        pauseEvent.Reset();
                        pauseBtn.Content = Translator.IsEnglish ? "Resume" : "继续";
                        speedText.Text = Translator.IsEnglish ? "Paused" : "已暂停";
                    }
                    else
                    {
                        pauseEvent.Set();
                        pauseBtn.Content = Translator.IsEnglish ? "Pause" : "暂停";
                    }
                };

                // 取消按钮事件 - 在当前对话框内切换内容，不弹出新对话框
                cancelBtn.Click += (s, e) =>
                {
                    // 构建确认取消的UI
                    var confirmPanel = new StackPanel { Spacing = 16 };
                    confirmPanel.Children.Add(new TextBlock
                    {
                        Text = Translator.IsEnglish ? "Confirm cancel download?" : "确认取消下载？",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 16
                    });
                    confirmPanel.Children.Add(new TextBlock
                    {
                        Text = Translator.IsEnglish ? "Choose how to cancel:" : "请选择取消方式：",
                        Opacity = 0.7
                    });
                    var confirmBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    
                    var deleteBtn = new Button
                    {
                        Content = Translator.IsEnglish ? "Cancel & Delete" : "取消并删除文件",
                        Width = 140
                    };
                    var keepBtn = new Button
                    {
                        Content = Translator.IsEnglish ? "Cancel & Keep" : "取消并保留文件",
                        Width = 140
                    };
                    var backBtn = new Button
                    {
                        Content = Translator.IsEnglish ? "Continue" : "继续下载",
                        Width = 120
                    };
                    confirmBtnPanel.Children.Add(deleteBtn);
                    confirmBtnPanel.Children.Add(keepBtn);
                    confirmBtnPanel.Children.Add(backBtn);
                    confirmPanel.Children.Add(confirmBtnPanel);
                    
                    // 替换对话框内容
                    progressDialog.Content = confirmPanel;
                    progressDialog.Title = Translator.IsEnglish ? "Cancel Download" : "取消下载";
                    
                    // 删除文件并取消
                    deleteBtn.Click += (s2, e2) =>
                    {
                        deleteOnCancel = true;
                        cts.Cancel();
                        pauseEvent.Set();
                        progressDialog.Hide();
                    };
                    
                    // 保留文件并取消
                    keepBtn.Click += (s2, e2) =>
                    {
                        cts.Cancel();
                        pauseEvent.Set();
                        progressDialog.Hide();
                        // 文件保留在磁盘上，用于断点续传
                    };
                    
                    // 继续下载 - 恢复原来的进度UI
                    backBtn.Click += (s2, e2) =>
                    {
                        progressDialog.Content = progressPanel;
                        progressDialog.Title = Translator.IsEnglish ? "Downloading" : "下载中";
                    };
                };

                // 开始下载（支持断点续传）
                var downloadTask = System.Threading.Tasks.Task.Run(async () =>
                {
                    var filePath = System.IO.Path.Combine(folder.Path, fileName);
                    long existingBytes = 0;
                    
                    // 检查是否已有部分下载的文件（断点续传）
                    if (System.IO.File.Exists(filePath))
                    {
                        existingBytes = new System.IO.FileInfo(filePath).Length;
                    }
                    
                    using var downloadClient = new System.Net.Http.HttpClient();
                    downloadClient.Timeout = TimeSpan.FromMinutes(60);
                    
                    // 如果已有部分文件，添加Range请求头实现断点续传
                    if (existingBytes > 0)
                    {
                        downloadClient.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
                    }
                    
                    var response = await downloadClient.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? fileSize;
                    // 如果服务器支持断点续传，总大小需要加上已下载的部分
                    if (existingBytes > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent)
                    {
                        totalBytes += existingBytes;
                    }
                    else
                    {
                        // 服务器不支持断点续传，重新开始
                        existingBytes = 0;
                    }
                    
                    // 文件流：如果有已下载部分，使用追加模式；否则创建新文件
                    var fileMode = existingBytes > 0 ? System.IO.FileMode.Append : System.IO.FileMode.Create;
                    using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
                    using var fileStream = new System.IO.FileStream(filePath, fileMode, System.IO.FileAccess.Write, System.IO.FileShare.None);
                    
                    var buffer = new byte[65536];
                    long totalRead = existingBytes;
                    int bytesRead;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                    {
                        // 检查暂停
                        pauseEvent.Wait(cts.Token);
                        
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                        totalRead += bytesRead;
                        
                        // 更新进度
                        var progress = (int)((double)totalRead / totalBytes * 100);
                        var speed = (totalRead - existingBytes) / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;
                        var remainingSeconds = speed > 0 ? (totalBytes - totalRead) / 1024.0 / 1024.0 / speed : 0;
                        
                        // 在UI线程更新
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!isPaused)
                            {
                                progressBar.Value = progress;
                                var resumeInfo = existingBytes > 0 ? string.Format(Translator.IsEnglish ? " (Resumed from {0:0.0} MB)" : " (已续传 {0:0.0} MB)", existingBytes / 1024.0 / 1024.0) : "";
                                speedText.Text = string.Format(Translator.IsEnglish ?
                                    "{0:0.0} MB/s | {1:0.0}% | {2:0}s remaining{3}" :
                                    "{0:0.0} MB/s | {1:0.0}% | 剩余 {2:0} 秒{3}",
                                    speed, progress, remainingSeconds, resumeInfo);
                            }
                        });
                    }
                    
                    stopwatch.Stop();
                    return filePath;
                });

                // 显示进度对话框
                _ = progressDialog.ShowAsync();

                string savedPath;
                try
                {
                    savedPath = await downloadTask;
                }
                catch (OperationCanceledException)
                {
                    // 用户主动取消下载
                    if (deleteOnCancel)
                    {
                        // 等待文件流释放（下载任务结束后using会自动释放流）
                        await System.Threading.Tasks.Task.Delay(500);
                        try
                        {
                            var filePath = System.IO.Path.Combine(folder.Path, fileName);
                            if (System.IO.File.Exists(filePath))
                            {
                                System.IO.File.Delete(filePath);
                            }
                        }
                        catch { }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    cts.Cancel();
                    progressDialog.Hide();
                    var errDialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "Download Failed" : "下载失败",
                        Content = Translator.IsEnglish ?
                            "Download failed. This may be due to network issues or GitHub access restrictions.\n\nTip: Users in mainland China may need a VPN/accelerator to download from GitHub." :
                            "下载失败，可能是网络问题或GitHub访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                        CloseButtonText = "OK",
                        RequestedTheme = theme,
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errDialog.ShowAsync();
                    return;
                }

                // 关闭进度对话框
                progressDialog.Hide();

                // 下载完成提示 - 添加手动安装和自动安装选项
                var completePanel = new StackPanel { Spacing = 8 };
                completePanel.Children.Add(new TextBlock
                {
                    Text = string.Format(Translator.IsEnglish ?
                        "File saved to:\n{0}" :
                        "文件已保存到：\n{0}", savedPath),
                    TextWrapping = TextWrapping.Wrap
                });
                completePanel.Children.Add(new TextBlock
                {
                    Text = Translator.IsEnglish ?
                        "\nChoose installation method:" :
                        "\n请选择安装方式：",
                    TextWrapping = TextWrapping.Wrap
                });
                completePanel.Children.Add(new TextBlock
                {
                    Text = Translator.IsEnglish ?
                        "• Manual Install: Safe, extract and replace files yourself\n• Auto Install (Beta): Automatically extract and overwrite, may have risks" :
                        "• 手动安装：安全，自行解压替换文件\n• 自动安装（Beta）：自动解压覆盖，可能存在风险",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.7
                });

                var completeDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Download Complete" : "下载完成",
                    Content = completePanel,
                    PrimaryButtonText = Translator.IsEnglish ? "Auto Install (Beta)" : "自动安装（Beta）",
                    SecondaryButtonText = Translator.IsEnglish ? "Manual Install" : "手动安装",
                    CloseButtonText = "OK",
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };
                var result = await completeDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // 自动安装
                    await AutoInstallUpdateAsync(savedPath);
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    // 手动安装 - 打开文件夹
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + savedPath + "\"");
                }
            }
            catch (Exception ex)
            {
                var errDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Download Failed" : "下载失败",
                    Content = Translator.IsEnglish ?
                        "Failed to get download information from GitHub. This may be due to network issues or GitHub access restrictions.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." :
                        "从GitHub获取下载信息失败，可能是网络问题或GitHub访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                    CloseButtonText = "OK",
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }

        // 自动安装更新（Beta）
        private async System.Threading.Tasks.Task AutoInstallUpdateAsync(string zipPath)
        {
            try
            {
                // 确认对话框
                var confirmDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Auto Install (Beta)" : "自动安装（Beta）",
                    Content = Translator.IsEnglish ?
                        "Auto Install is still in testing, please backup your current version before using this feature.\n\nThe software will close automatically, then the update script will replace files and restart.\n\nDo not turn off your computer during the update.\n\nContinue?" :
                        "自动安装功能仍在测试中，使用前请尽量备份好原版本。\n\n软件将自动关闭，随后更新脚本会替换文件并重新启动。\n\n更新过程中请勿关闭电脑。\n\n是否继续？",
                    PrimaryButtonText = Translator.IsEnglish ? "Continue" : "继续",
                    CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                    RequestedTheme = Services.SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light,
                    XamlRoot = this.Content.XamlRoot
                };
                var confirmResult = await confirmDialog.ShowAsync();
                if (confirmResult != ContentDialogResult.Primary) return;

                // 解压到临时目录
                string tempDir = Path.Combine(Path.GetTempPath(), $"DFGE_Update_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(tempDir);
                
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, true);

                // 找到新版本的根目录（可能压缩包内有一层文件夹）
                string newVersionDir = tempDir;
                var subDirs = Directory.GetDirectories(tempDir);
                if (subDirs.Length == 1)
                {
                    var exeInSub = Directory.GetFiles(subDirs[0], "*.exe", SearchOption.TopDirectoryOnly);
                    if (exeInSub.Length > 0)
                    {
                        newVersionDir = subDirs[0];
                    }
                }

                // 软件当前目录
                string appDir = AppContext.BaseDirectory.TrimEnd('\\');

                // 生成更新批处理脚本
                string batPath = Path.Combine(Path.GetTempPath(), "DFGE_Update.bat");
                string exeName = "DLSSFrameGenEnabler_WinUI3.exe";
                string backupId = new Random().Next(100000, 999999).ToString();
                string batContent = $@"@echo off
echo Updating DLSS5+MFG Enable...
echo.

REM 等待软件进程结束
timeout /t 3 /nobreak >nul
taskkill /f /im {exeName} 2>nul
timeout /t 2 /nobreak >nul

REM 备份当前版本
set BACKUP_DIR=%TEMP%\DFGE_Backup_{backupId}
mkdir ""%BACKUP_DIR%"" 2>nul
echo Backing up current version...
xcopy ""{appDir}\*"" ""%BACKUP_DIR%\"" /E /Y /Q /H >nul 2>&1

REM 复制新文件
echo Copying new files...
xcopy ""{newVersionDir}\*"" ""{appDir}\"" /E /Y /R /H >nul 2>&1

REM 重新启动软件
echo Starting software...
start """" ""{appDir}\{exeName}""

REM 清理临时文件
timeout /t 3 /nobreak >nul
rmdir /s /q ""{tempDir}"" 2>nul
del ""%~f0"" 2>nul
";

                File.WriteAllText(batPath, batContent, System.Text.Encoding.Default);

                // 运行批处理脚本并关闭软件
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{batPath}\"\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };
                System.Diagnostics.Process.Start(startInfo);

                // 关闭当前软件
                Microsoft.UI.Xaml.Application.Current.Exit();
            }
            catch (Exception ex)
            {
                var errDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Auto Install Failed" : "自动安装失败",
                    Content = string.Format(Translator.IsEnglish ?
                        "Auto install failed: {0}\n\nPlease use manual install instead." :
                        "自动安装失败：{0}\n\n请改用手动安装。", ex.Message),
                    CloseButtonText = "OK",
                    RequestedTheme = Services.SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light,
                    XamlRoot = this.Content.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }

        // 打开URL
        private void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
    }
}
