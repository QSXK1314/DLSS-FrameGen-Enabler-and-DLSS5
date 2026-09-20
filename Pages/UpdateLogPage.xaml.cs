using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using WinRT;
using WinRT.Interop;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace DLSSFrameGenEnabler_WinUI3.Pages;

public sealed partial class UpdateLogPage : Page
{

	public UpdateLogPage()
	{
		InitializeComponent();
		Translator.LanguageChanged += UpdateLanguageTexts;
		UpdateLanguageTexts();
	}

	private void UpdateLanguageTexts()
	{
		bool isEnglish = Translator.IsEnglish;
		TitleText.Text = (isEnglish ? "Update Log" : "更新内容");
		CheckUpdateBtn.Content = (isEnglish ? "Check Update" : "检查更新");
		DirectDownloadBtn.Content = (isEnglish ? "Direct Download" : "直接下载最新版");
		V11520BetaTitle.Text = "V1.15.3.0-Beta";
		V11520Beta_1.Text = (isEnglish ? "\ud83d\udd2c BETA: This is a beta version for testing new features. Stable version remains V1.14.1.0. Beta features will be synced to stable version after testing." : "\ud83d\udd2c 测试版：本版本为测试版，用于测试新功能。稳定版仍为V1.14.1.0。测试版功能经过测试后会同步到稳定版。");
		V11520Beta_2.Text = (isEnglish ? "NEW: Game Remark/Tag feature - Right-click game menu adds 'Edit Remark/Tags', supports multi-line remark and comma-separated tags, displayed in game list below exe path, auto-saved, supports Chinese/English" : "新增：游戏备注/标签功能 - 右键游戏菜单新增「编辑备注/标签」，支持多行备注和逗号分隔标签，显示在游戏列表exe路径下方，自动保存，支持中英文");
		V11520Beta_3.Text = (isEnglish ? "NEW: Game icon cache optimization - Extracted exe icons are cached to IconCache folder, named by exe path MD5 + file modification time, second startup loads directly from cache, auto-refreshes when exe updates, auto-cleans old cache" : "新增：游戏图标缓存优化 - 提取的exe图标缓存到IconCache文件夹，以exe路径MD5+文件修改时间命名，第二次启动直接从缓存加载，exe更新后自动刷新，自动清理旧缓存");
		V11520Beta_4.Text = (isEnglish ? "NEW: Navigation bar collapse/expand - Bottom-left hamburger button toggles nav bar, collapses to show only icons (positions fixed), 200ms width animation, displays GPU vendor logo (NVIDIA/AMD) when collapsed" : "新增：导航栏收起/展开 - 左下角汉堡菜单按钮切换导航栏，收起后只显示图标（位置固定不变），200ms宽度动画，收起时左上角显示显卡厂商logo（NVIDIA/AMD）");
		V11520Beta_5.Text = (isEnglish ? "NEW: Keyboard shortcuts - Ctrl+A select all games, Delete remove selected games, F5 refresh all game patch status, shortcuts don't trigger when focus is in TextBox" : "新增：键盘快捷键 - Ctrl+A全选游戏，Delete移除所选游戏，F5刷新所有游戏补丁状态，焦点在输入框时不触发快捷键");
		V11520Beta_6.Text = (isEnglish ? "NEW: Game launch shortcut - Right-click menu adds 'Launch Game', directly starts game exe with error handling and toast notification" : "新增：游戏启动快捷方式 - 右键菜单新增「启动游戏」，直接启动游戏exe，包含错误处理和Toast提示");
		V11520Beta_7.Text = (isEnglish ? "NEW: Auto-check update on startup configurable - Settings page adds 'Auto-check update on startup' toggle, default enabled, users can choose to disable" : "新增：启动时自动检查更新可配置 - 设置页面新增「启动时自动检查更新」开关，默认开启，用户可选择关闭");
		V11520Beta_8.Text = (isEnglish ? "IMPROVED: Operation success/failure Toast notifications - Changed from ContentDialog (needs click confirm) to centered Toast, auto-disappears after 3 seconds, follows light/dark theme, reduces popup interruption" : "改进：操作成功/失败Toast提示 - 从需要点确认的ContentDialog改成居中显示的Toast，3秒后自动消失，跟随深浅色主题，减少弹窗打断");
		V11400Title.Text = "V1.14.0.0";
		V11400_1.Text = (isEnglish ? "NEW: Manual Add Exe now supports ALL games (DX9/DX11/DX12), no longer forced to DX9 only. Detect Running Games also supports all games" : "新增：手动添加exe现在支持所有游戏（DX9/DX11/DX12），不再强制只支持DX9。检测运行中游戏也支持所有游戏");
		V11400_2.Text = (isEnglish ? "NEW: Manual Add Directory skips hasRealGameExe check for network drives - users explicitly select the folder, so it must be a game folder, avoids Directory.Exists/GetFiles failures on network drives" : "新增：手动添加目录跳过hasRealGameExe检查（针对网络驱动器）- 用户明确选择了文件夹，肯定是游戏目录，避免网络驱动器上Directory.Exists/GetFiles失败");
		V11400_3.Text = (isEnglish ? "CRITICAL FIX: 007 First Light and other games with digit-starting exe names were incorrectly excluded by recursive search logic (char.IsDigit(fileName[0]) && !Contains('shipping')). Removed this exclusion entirely" : "关键修复：007 First Light等以数字开头的exe被递归搜索逻辑错误排除（char.IsDigit(fileName[0]) && !Contains('shipping')）。完全移除此排除逻辑");
		V11400_4.Text = (isEnglish ? "FIX: FindGameExeDirectory did not check Retail folder - games with exe in Retail directory (007 First Light etc.) could not find the real game exe. Added Retail/retail to commonSubDirs" : "修复：FindGameExeDirectory未检查Retail文件夹 - exe在Retail目录下的游戏（007 First Light等）无法找到真正的游戏exe。在commonSubDirs中添加了Retail/retail");
		V11400_5.Text = (isEnglish ? "FIX: Manual Add Exe could not enable Frame Generation - was forced to DX9 mode (isDx9Manual: true), which disabled DX11/12 support and Frame Generation. Changed to isDx9Manual: false for auto DX detection" : "修复：手动添加exe无法开启多帧生成 - 之前强制为DX9模式（isDx9Manual: true），禁用了DX11/12支持和多帧生成。改为isDx9Manual: false自动检测DX支持");
		V11400_6.Text = (isEnglish ? "IMPROVED: Retail folder uses recursive search (AllDirectories) to find exe in any subdirectory. Added 007 First Light special handling (exe in Retail folder). All checks wrapped in try-catch for robustness" : "改进：Retail文件夹使用递归搜索（AllDirectories）在任意子目录中查找exe。添加007 First Light特殊处理（exe在Retail文件夹）。所有检查都包裹在try-catch中以提高健壮性");
		V11400_7.Text = (isEnglish ? "UI: Changed button text from 'Manual Mode (Select game exe, supports DX9 games)' to 'Manual Mode (Select game exe)' since it now supports all games, not just DX9" : "界面：按钮文字从'手动模式（自行选择游戏exe，支持dx9游戏）'改为'手动模式（自行选择游戏exe）'，因为现在支持所有游戏，不只是DX9");
		V11300Title.Text = "V1.13.0.0";
		V11300_1.Text = (isEnglish ? "NEW: Detect Running Games feature - Enumerate system processes via Win32 API (CreateToolhelp32Snapshot + QueryFullProcessImageName), 100% accurate game exe path, just like Task Manager's 'Open file location'" : "新增：检测运行中游戏功能 - 通过纯Win32 API（CreateToolhelp32Snapshot + QueryFullProcessImageName）枚举系统进程，100%准确获取游戏真正的exe路径，模仿任务管理器'打开文件所在位置'");
		V11300_2.Text = (isEnglish ? "Process list with icons: Extract exe file icons via SHGetFileInfo Win32 API, display 24x24 icons next to process name for easy identification" : "进程列表显示图标：通过SHGetFileInfo Win32 API提取exe文件图标，在进程名旁边显示24x24图标，方便用户辨认");
		V11300_3.Text = (isEnglish ? "NEW: Usage Notes dialog on startup - Shows 6 important notes after startup dialog (enable DLSS first, miHoYo not supported, Ubisoft/EA may not be fully compatible, Vulkan not supported, backup game files, anti-cheat risk), with 'Don't show again' checkbox" : "新增：启动时使用前说明弹窗 - 在启动弹窗后显示6条重要注意事项（先开启DLSS、米哈游不支持、育碧EA可能不完全适配、Vulkan不支持、备份游戏文件、反作弊风险），支持'不再显示'复选框");
		V11300_4.Text = (isEnglish ? "Fixed ContentDialog crash: Changed all ContentDialog initialization from InitializeWithWindow (Win32 interop) to XamlRoot (WinUI3 native), resolved 'Specified cast is not valid' error" : "修复ContentDialog崩溃：将所有ContentDialog初始化从InitializeWithWindow（Win32互操作）改为XamlRoot（WinUI3原生），解决'Specified cast is not valid'错误");
		V11300_5.Text = (isEnglish ? "Fixed restore preview inconsistency: PreviewRestoreFrameGen and PreviewRestoreDLSS5 now use EXACTLY the same logic as actual Restore (IsAddedPatchFile function), preview and actual results are 100% consistent" : "修复还原预览不一致：PreviewRestoreFrameGen和PreviewRestoreDLSS5现在使用与实际还原完全一致的逻辑（IsAddedPatchFile函数），预览和实际结果100%一致");
		V11300_6.Text = (isEnglish ? "Improved running game detection prompt: Added warning to enable DLSS/Frame Generation in game settings FIRST, then close game, then install patches" : "优化运行中游戏检测提示：添加提醒，告知用户必须先在游戏设置中开启DLSS/帧生成相关功能，然后关闭游戏，最后再安装补丁");
		V11290Title.Text = "V1.12.9.0";
		V11290_1.Text = (isEnglish ? "Critical fix: Restore function was deleting game native files (plugins folder, nvngx files etc.). Simplified restore logic to stable pattern matching, DLSS5 and Frame Gen restore now use EXACTLY the same logic" : "关键修复：还原功能误删游戏原生文件（plugins文件夹、nvngx文件等）。简化还原逻辑为稳定的模式匹配，DLSS5和多帧生成的还原现在使用完全一致的逻辑");
		V11290_2.Text = (isEnglish ? "Restore safety: Only delete explicitly added patch files (ReShade*, dxgi.dll, d3d9.dll etc.). Files that may be game native (nvngx_dlss.dll, nvngx_dlssg.dll, sl.*) are only restored if backup exists, otherwise left untouched" : "还原安全保障：只删除明确添加的补丁文件（ReShade*、dxgi.dll、d3d9.dll等）。可能是游戏原生的文件（nvngx_dlss.dll、nvngx_dlssg.dll、sl.*等）只有备份存在时才恢复，否则完全不动");
		V11290_3.Text = (isEnglish ? "Folder protection: Never delete plugins, Streamline, Fonts folders that may be game native. Only delete patch-created folders: reshade-shaders, runtime, host64" : "文件夹保护：绝不删除可能是游戏原生的plugins、Streamline、Fonts文件夹。只删除补丁创建的文件夹：reshade-shaders、runtime、host64");
		V11290_4.Text = (isEnglish ? "Removed complex snapshot feature that caused instability: Backup manifest and file pattern matching is sufficient and reliable for restore operations" : "移除导致不稳定的复杂快照功能：备份清单和文件模式匹配对于还原操作已经足够且可靠");
		V11283Title.Text = "V1.12.8.3";
		V11283_1.Text = (isEnglish ? "Fixed crash on Usage page: NullReferenceException in UpdateLanguageTexts when XAML elements not fully initialized" : "修复使用说明页面崩溃：XAML元素未完全初始化时UpdateLanguageTexts出现空引用异常");
		V11283_2.Text = (isEnglish ? "Fixed crash on download feature: ContentDialog conflict when multiple dialogs try to open simultaneously (loading dialog + changelog dialog + error dialog)" : "修复下载功能崩溃：多个对话框同时打开时的ContentDialog冲突（加载对话框+更新内容对话框+错误对话框）");
		V11283_3.Text = (isEnglish ? "Fixed crash on Settings page: InvalidCastException when XAML generated cache out of sync, resolved by clean rebuild" : "修复设置页面崩溃：XAML生成缓存不同步导致的类型转换异常，通过清理并重新编译解决");
		V11283_4.Text = (isEnglish ? "Fixed software won't launch on many computers: Missing Visual C++ Redistributable runtime. Release package now includes ALL required runtimes (.NET, Windows App SDK, VC++), extract and run directly, no installation needed" : "修复很多电脑无法打开软件的问题：缺少Visual C++ Redistributable运行时。发布版现已包含所有所需运行时（.NET、Windows App SDK、VC++），解压即用，无需安装任何组件");
		V11283_5.Text = (isEnglish ? "Added VC++ runtime auto-detection: Checks registry and system DLLs on startup, shows bilingual prompt if missing" : "新增VC++运行时自动检测：启动时检查注册表和系统DLL，如缺少则弹出中英文提示");
		V11283_6.Text = (isEnglish ? "Added bilingual launchers: 启动软件.bat (Chinese) and Launch.bat (English), auto-detects VC++ runtime and Windows version before launching" : "新增中英文启动器：启动软件.bat（中文）和Launch.bat（英文），启动前自动检测VC++运行时和Windows版本");
		V11283_7.Text = (isEnglish ? "Updated usage guide: Added system requirements section (Windows 10 1809+, VC++ runtime, GPU requirements, latest drivers)" : "更新使用说明：添加系统要求部分（Windows 10 1809+、VC++运行时、显卡要求、最新驱动）");
		V11252Title.Text = "V1.12.5.2";
		V11252_1.Text = (isEnglish ? "Added tip for Pure Transparent backdrop: switching themes requires app restart to display correct colors" : "纯透明材质添加提示说明：切换深浅色模式后需要重启软件才能显示正确颜色");
		V11252_2.Text = (isEnglish ? "Added vertical scrolling to Settings page, no need to resize window to see all options" : "设置页面添加上下滚动功能，无需扩大窗口即可查看所有设置项");
		V11252_3.Text = (isEnglish ? "Adjusted dark mode transparency color depth for better visual experience" : "调整深色模式透明材质的颜色深度，优化视觉体验");
		V11252_4.Text = (isEnglish ? "Fixed Pure Transparent backdrop blur effect, restored to non-blurred pure transparency" : "修复纯透明材质带模糊效果的问题，恢复为无模糊的纯透明效果");
		V11252_5.Text = (isEnglish ? "Fixed Settings page not loading after XAML changes, ensured all pages are verified after modifications" : "修复修改XAML后设置页面无法加载的问题，后续修改页面后均会验证页面可正常打开");
		V11231Title.Text = "V1.12.3.1";
		V11231_1.Text = (isEnglish ? "Theme switch wipe animation: PPT-style wipe animation for dark/light mode switching, with blurred edge, bidirectional playback, and color matching for all backdrop materials" : "深浅色切换擦除动画：实现类似PPT擦除动画的深浅色切换效果，边缘模糊处理，双向动画（浅色→深色和深色→浅色），适配各种材质颜色");
		V11231_2.Text = (isEnglish ? "Page transition animation enhanced: SlideNavigationTransitionInfo slide effect, from left to right (matches left navigation bar visual flow)" : "页面切换动画增强：使用SlideNavigationTransitionInfo滑动过渡效果，从左往右滑入（符合左侧导航栏的视觉习惯）");
		V11231_3.Text = (isEnglish ? "Animation master switch: Renamed from 'Page Transition Animation' to 'Animation Effects', controls all animations including page transitions and theme switches to reduce performance usage" : "动画效果总开关：从'页面切换动画'改名为'动画效果'，关闭后禁止页面切换和深浅色切换等所有动画，以减少性能占用");
		V11231_4.Text = (isEnglish ? "Transparent material color alignment: Fixed navigation panel and content area color mismatch in both light and dark modes, wipe animation colors now match actual material colors" : "透明材质颜色对齐：修复浅色和深色模式下导航栏与右边界面颜色不一致的问题，擦除动画颜色与实际材质颜色对齐");
		V11231_5.Text = (isEnglish ? "Fixed Settings page not loading: XAML connection error after adding new control, resolved by cleaning and rebuilding project to regenerate auto-generated code and xbf files" : "修复设置页面无法加载：添加新控件后XAML连接代码与xbf文件不同步导致类型转换错误，通过清理并重新生成项目解决");
		V112Title.Text = "V1.12.0.0";
		V112_1.Text = (isEnglish ? "Completely rewritten UI with WinUI 3 framework" : "使用WinUI 3框架完全重写UI界面");
		V112_2.Text = (isEnglish ? "Completely rewritten core functionality logic" : "核心功能逻辑完全重写");
		V112_3.Text = (isEnglish ? "Support RTX 20/30 series Frame Generation" : "支持RTX20系和30系开启多帧生成");
		V112_4.Text = (isEnglish ? "Support Where Winds Meet (Yan Yun) DLSS5 + Frame Generation" : "支持燕云十六声开启DLSS5+多帧生成");
		V112_5.Text = (isEnglish ? "Support RE Engine Frame Generation" : "支持RE引擎开启多帧生成");
		V112_6.Text = (isEnglish ? "Support DX9 64-bit games DLSS5" : "支持DX9 64位游戏开启DLSS5");
		V112_7.Text = (isEnglish ? "Support Chinese/English language switching" : "支持中英文切换");
		V112_8.Text = (isEnglish ? "Added custom wallpaper feature" : "新增自定义壁纸功能");
		V112_9.Text = (isEnglish ? "Added backdrop material selection (Mica/Acrylic/Gaussian Blur/Custom)" : "新增界面材质选择（云母/亚克力/高斯模糊/自定义壁纸）");
		V112_10.Text = (isEnglish ? "Added icons to About page and startup dialog links" : "关于页面和启动弹窗链接添加图标");
		V112_11.Text = (isEnglish ? "Fixed window title bar icon display" : "修复窗口左上角图标显示");
		V112_12.Text = (isEnglish ? "Optimized dark/light mode switching" : "优化深色模式/浅色模式切换");
		V112_13.Text = (isEnglish ? "Added GPU selection feature (auto-detect + manual, prefer dedicated GPU)" : "新增显卡选择功能（自动检测+手动选择，优先独立显卡）");
		V112_14.Text = (isEnglish ? "Added right-click context menu (open directory, remove from list, quick actions)" : "新增右键菜单功能（打开目录、移除列表、各功能快捷操作）");
		V112_15.Text = (isEnglish ? "Optimized game detection logic (exclude engine directories, RE engine detection, Cyberpunk 2077 special handling)" : "优化游戏识别逻辑（排除引擎目录、RE引擎识别、2077特殊处理）");
		V110Title.Text = "V1.10.3.2";
		V110_1.Text = (isEnglish ? "New GPU Support: RTX 20/30 series Frame Generation, auto-detect GPU model, RTX 40 series continues Classic/Advanced mode" : "新增显卡支持：支持RTX 20系/30系显卡开启多帧生成，软件自动检测显卡型号调用对应补丁，RTX 40系继续使用经典模式和高级模式");
		V110_2.Text = (isEnglish ? "RE Engine Frame Generation: Support RE engine games (RE9, Onimusha, Pragmata etc.), auto-detect via re_chunk_000.pak, mutually exclusive with DLSS5" : "新增RE引擎多帧生成支持：支持RE引擎游戏开启多帧生成（生化危机9、鬼武者、识质存在等），通过检测re_chunk_000.pak自动识别，与DLSS5互斥");
		V110_3.Text = (isEnglish ? "Where Winds Meet special adaptation: Support DLSS5 + Frame Generation simultaneously, special Engine\\Binaries\\Win64r directory detection, Chinese font support" : "燕云十六声特殊适配：支持同时开启DLSS5+多帧生成，特殊识别Engine\\Binaries\\Win64r目录，中文字体支持（MiSans-Bold.ttf）");
		V110_4.Text = (isEnglish ? "DX9 DLSS5 improved: DX9 32-bit (dgVoodoo2 + DLSS5-Feeder cross-process), DX9 64-bit (direct loading), manual exe selection, DLL replacement feature" : "DX9 DLSS5完善：支持DX9 32位（dgVoodoo2+DLSS5-Feeder跨进程方案）和64位（直接加载方案），手动添加可自行选择exe，替换dll功能自动移动dgVoodoo2补丁到运行dll目录");
		V110_5.Text = (isEnglish ? "Auto-scan platforms extended: EA, Ubisoft, GOG platform library auto-scan added, continue support Steam and Epic" : "自动扫描平台扩展：新增EA、育碧、GOG平台游戏库自动扫描，继续支持Steam和Epic平台");
		V110_6.Text = (isEnglish ? "Right-click context menu: Quick actions for frame gen, DLSS5, DX9 DLSS5, troubleshooting, remove from list" : "右键菜单功能：游戏列表右键可快速操作多帧生成、DLSS5、DX9 DLSS5、排错修复、从列表移除");
		V110_7.Text = (isEnglish ? "No-fault restore: For games without backup files, with severe warning before use" : "无责还原功能：无备份文件的游戏可使用无责还原删除补丁，使用前严重警告可能删除游戏源文件");
		V110_8.Text = (isEnglish ? "RE engine mutual exclusion logic fixed: Fixed buttons still clickable after enabling DLSS5/Frame Gen, fixed right-click menu logic, fixed button overlap issue" : "RE引擎互斥逻辑修复：修复开启DLSS5后多帧生成按钮仍可交互的问题，修复右键菜单互斥逻辑不生效，修复按钮重叠导致点击还原按钮的问题");
		V110_9.Text = (isEnglish ? "Other fixes: reshade-shaders naming, version sync, light mode list alternating colors, manual exe frame gen detection, uninstalled game placeholder, engine exe misidentification, Neverness To Everness path, Half-Life 2 bin directory, 2077 restore, render API detection" : "其他修复：reshade-shaders目录命名、版本号不同步、浅色模式列表黑白交替、手动添加exe无法识别多帧生成、卸载游戏占位符误加入、引擎exe误识别、异环特殊路径、半条命2 bin目录识别、2077专用补丁还原、渲染API检测逻辑");
		V197Title.Text = "V1.9.7.2";
		V197_1.Text = (isEnglish ? "Core runtime update: nvngx_dlss.dll updated from 310.8.0.0 to 310.9.1.0, nvngx_dlssg.dll (Advanced) updated to 310.9.1.0, Classic and RTX20 remain unchanged for compatibility" : "核心运行时更新：nvngx_dlss.dll从310.8.0.0更新到310.9.1.0（最新版），nvngx_dlssg.dll（高级模式）更新到310.9.1.0，经典模式和RTX20系专用版本保持不变（兼容性需要）");
		V197_2.Text = (isEnglish ? "About page community credits: Added acknowledgment area listing all community open source projects and authors" : "关于页面添加社区作者致谢：新增致谢区域，列出所有使用的社区开源项目及作者（DLSS-Enabler、RTX40MFG-Unlock、RenoDX、DLSS5-Feeder、ReShade等）");
		V1868Title.Text = "V1.8.6.8";
		V1868_1.Text = (isEnglish ? "DX9 64-bit reshade-shaders missing fix (critical): Fixed GetSharedDirectory method not handling subdirectory extraction correctly" : "DX9 64位reshade-shaders缺失修复（关键修复）：修复GetSharedDirectory方法未正确处理子目录提取的bug，添加缓存完整性检查");
		V1868_2.Text = (isEnglish ? "Source engine dgvoodoo bin directory fix: Fixed Half-Life 2 etc. dgvoodoo patches not recognizing bin directory, added Source engine special handling (highest priority)" : "起源引擎dgvoodoo bin目录识别修复：修复半条命2等Source引擎游戏dgvoodoo补丁无法正确识别bin目录的问题，添加Source引擎特殊处理（最高优先级）");
		V1868_3.Text = (isEnglish ? "DX9 detection logic improved: New DetectRenderAPIType method returns 4 render API types, stricter detection: exe references d3d9.dll and not d3d11/d3d12/dxgi" : "DX9检测逻辑改进：新增DetectRenderAPIType方法返回4种渲染API类型，检测逻辑更严格：exe引用d3d9.dll且不引用d3d11/d3d12/dxgi才判定为DX9");
		V1868_4.Text = (isEnglish ? "Game exe detection logic enhanced: Support Win64r variant directory names (e.g. Where Winds Meet Engine\\Binaries\\Win64r)" : "游戏exe识别逻辑加强：支持Win64r等变体目录名（如燕云十六声的Engine\\Binaries\\Win64r）");
		V1865Title.Text = "V1.8.6.5";
		V1865_1.Text = (isEnglish ? "UI refactor (WinUI3 style): New left navigation bar layout, reference Windows 11 Settings, 5 pages: Home, Settings, Usage, Update Log, About" : "UI重构（模仿WinUI3风格）：全新左侧导航栏布局，参考Windows 11设置界面，主页、设置、使用说明、更新内容、关于五个页面");
		V1865_2.Text = (isEnglish ? "About page: Software introduction, version info, author links: GitHub, Bilibili, Xiaoheihe" : "关于页面：软件介绍、版本信息、作者栏：GitHub、哔哩哔哩、小黑盒链接");
		V1865_3.Text = (isEnglish ? "Light/Dark mode: All pages fully support light and dark mode" : "浅色/深色模式：所有页面完整适配浅色和深色模式");
		HistoryTitle.Text = (isEnglish ? "Historical Versions" : "历史版本");
		History_1.Text = (isEnglish ? "V1.8.6: Size optimization, Shared directory for duplicate large files" : "V1.8.6：体积优化，建立Shared共享目录存放重复大文件");
		History_2.Text = (isEnglish ? "V1.8.5: No-fault restore feature" : "V1.8.5：无责还原功能");
		History_3.Text = (isEnglish ? "V1.8.4: DX9 DLSS5 feature (32-bit)" : "V1.8.4：DX9 DLSS5功能（32位）");
		History_4.Text = (isEnglish ? "V1.8.3: RE engine universal DLSS5 patch" : "V1.8.3：RE引擎通用DLSS5补丁");
		History_5.Text = (isEnglish ? "V1.8.2: GPU detection optimization (prefer dedicated GPU)" : "V1.8.2：显卡检测优化（优先独立显卡）");
		History_6.Text = (isEnglish ? "V1.8.1: Backup restore enhanced (snapshot comparison)" : "V1.8.1：备份还原加强（快照对比）");
		History_7.Text = (isEnglish ? "V1.8.0: DLSS5 feature (NVIDIA/AMD)" : "V1.8.0：DLSS5功能（N卡/A卡）");
		History_8.Text = (isEnglish ? "V1.7.x: Config name modification feature" : "V1.7.x：配置名修改功能");
		History_9.Text = (isEnglish ? "V1.7.0: Classic/Advanced dual mode" : "V1.7.0：经典/高级双模式");
		History_10.Text = (isEnglish ? "V1.6.x: Full disk scan + disclaimer" : "V1.6.x：全盘扫描 + 免责声明");
		History_11.Text = (isEnglish ? "V1.5.x: Light/dark mode" : "V1.5.x：浅色/深色模式");
		History_12.Text = (isEnglish ? "V1.4.x: Troubleshooting feature" : "V1.4.x：排错修复功能");
		History_13.Text = (isEnglish ? "V1.3.x: Cyberpunk 2077 exclusive patch" : "V1.3.x：2077专用补丁");
		History_14.Text = (isEnglish ? "V1.2.x: Auto-scan Epic library" : "V1.2.x：自动扫描Epic库");
		History_15.Text = (isEnglish ? "V1.1.x: Manual game addition" : "V1.1.x：手动添加游戏");
		History_16.Text = (isEnglish ? "V1.0 ~ V1.1: Basic features (Classic mode frame gen, Steam library scan, one-click restore)" : "V1.0 ~ V1.1：基础功能（经典模式多帧生成、Steam库扫描、一键还原）");
		NoticeTitle.Text = (isEnglish ? "Important Notes" : "重要说明");
		Notice_1.Text = (isEnglish ? "Due to strict anti-cheat in miHoYo games, adaptation for miHoYo games is not included. Recommended to use Magpie, HoYoshade, or XXMI." : "由于米哈游系列游戏反作弊较为严苛，暂不加入对米哈游游戏的适配，建议使用【大力喜鹊(Magpie)】或【HoYoshade】【XXMI】开启DLSS5和多帧生成功能。");
		Notice_2.Text = (isEnglish ? "Due to Vulkan not being as widely used as DX, Vulkan adaptation is temporarily not included. Thank you for your understanding." : "由于Vulkan使用率并没有DX广泛，暂时也不加入适配，还请玩家们见谅。个人时间和精力实在有限。");
	}

	private async void DirectDownloadBtn_Click(object sender, RoutedEventArgs e)
	{
		await DownloadFromGitHubAsync();
	}

	private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			using HttpClient client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(10L);
			string json = await client.GetStringAsync(SettingsService.Instance.UpdateCheckUrl);
			JsonDocumentOptions options = new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			};
			using JsonDocument doc = JsonDocument.Parse(json, options);
			JsonElement rootElement = doc.RootElement;
			string text = rootElement.GetProperty("version").GetString();
			string currentVersion = App.CurrentVersion;
			string githubUrl = SettingsService.Instance.GithubUrl;
			string kuakeUrl = (rootElement.TryGetProperty("downloadUrlKuake", out var value) ? value.GetString() : "");
			string baiduUrl = (rootElement.TryGetProperty("downloadUrlBaidu", out var value2) ? value2.GetString() : "");
			string pan123Url = (rootElement.TryGetProperty("downloadUrl123pan", out var value3) ? value3.GetString() : "");
			if (rootElement.TryGetProperty("downloadLinks", out var value4))
			{
				if (string.IsNullOrEmpty(kuakeUrl) && value4.TryGetProperty("kuake", out var value5))
				{
					kuakeUrl = value5.GetString();
				}
				if (string.IsNullOrEmpty(baiduUrl) && value4.TryGetProperty("baidu", out var value6))
				{
					baiduUrl = value6.GetString();
				}
				if (string.IsNullOrEmpty(pan123Url) && value4.TryGetProperty("pan123", out var value7))
				{
					pan123Url = value7.GetString();
				}
				if (value4.TryGetProperty("github", out var value8))
				{
					githubUrl = value8.GetString();
				}
			}
			if (!IsNewerVersion(text, currentVersion))
			{
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Up to Date" : "已是最新版本"),
					Content = (Translator.IsEnglish ? "You are using the latest version." : "您正在使用最新版本。"),
					CloseButtonText = (Translator.IsEnglish ? "OK" : "知道了"),
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				return;
			}
			StackPanel stackPanel = new StackPanel
			{
				Spacing = 8.0
			};
			stackPanel.Children.Add(new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "Latest version: {0}\nCurrent version: {1}" : "最新版本：{0}\n当前版本：{1}", text, currentVersion),
				TextWrapping = TextWrapping.Wrap
			});
			stackPanel.Children.Add(new TextBlock
			{
				Text = (Translator.IsEnglish ? "Click Update to choose a download source." : "点击更新选择下载渠道。"),
				TextWrapping = TextWrapping.Wrap,
				Opacity = 0.7
			});
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "New Version Found" : "发现新版本"),
				Content = stackPanel,
				PrimaryButtonText = (Translator.IsEnglish ? "Update" : "更新"),
				CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() == ContentDialogResult.Primary)
			{
				await ShowDownloadSourceDialog(githubUrl, kuakeUrl, baiduUrl, pan123Url);
			}
		}
		catch (Exception ex)
		{
			await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Check Failed" : "检查失败"),
				Content = string.Format(Translator.IsEnglish ? "Failed to check for updates: {0}" : "检查更新失败：{0}", ex.Message),
				CloseButtonText = (Translator.IsEnglish ? "OK" : "知道了"),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
		}
	}

	private static bool IsNewerVersion(string remote, string current)
	{
		try
		{
			remote = remote.TrimStart(new char[2] { 'V', 'v' });
			current = current.TrimStart(new char[2] { 'V', 'v' });
			int num = remote.IndexOf('-');
			if (num > 0)
			{
				remote = remote.Substring(0, num);
			}
			num = current.IndexOf('-');
			if (num > 0)
			{
				current = current.Substring(0, num);
			}
			if (Version.TryParse(remote, out Version result) && Version.TryParse(current, out Version result2))
			{
				return result > result2;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private async Task ShowDownloadSourceDialog(string githubUrl, string kuakeUrl, string baiduUrl, string pan123Url)
	{
		ContentDialog dialog = null;
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 8.0
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = (Translator.IsEnglish ? "Please choose a download source:" : "请选择下载渠道："),
			FontWeight = FontWeights.SemiBold
		});
		Button button = new Button
		{
			Content = (Translator.IsEnglish ? "Direct Download (GitHub)" : "直接下载（GitHub）"),
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		button.Click += async delegate
		{
			dialog?.Hide();
			await Task.Delay(300);
			await DownloadFromGitHubAsync();
		};
		stackPanel.Children.Add(button);
		Button button2 = new Button
		{
			Content = "GitHub " + (Translator.IsEnglish ? "(Open in browser)" : "（浏览器打开）"),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Tag = githubUrl
		};
		button2.Click += delegate(object s, RoutedEventArgs e)
		{
			dialog?.Hide();
			OpenUrl((string)((Button)s).Tag);
		};
		stackPanel.Children.Add(button2);
		if (!string.IsNullOrEmpty(kuakeUrl))
		{
			Button button3 = new Button
			{
				Content = (Translator.IsEnglish ? "Quark Cloud Drive" : "夸克网盘"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Tag = kuakeUrl
			};
			button3.Click += delegate(object s, RoutedEventArgs e)
			{
				dialog?.Hide();
				OpenUrl((string)((Button)s).Tag);
			};
			stackPanel.Children.Add(button3);
		}
		if (!string.IsNullOrEmpty(baiduUrl))
		{
			Button button4 = new Button
			{
				Content = (Translator.IsEnglish ? "Baidu Cloud Drive" : "百度网盘"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Tag = baiduUrl
			};
			button4.Click += delegate(object s, RoutedEventArgs e)
			{
				dialog?.Hide();
				OpenUrl((string)((Button)s).Tag);
			};
			stackPanel.Children.Add(button4);
		}
		if (!string.IsNullOrEmpty(pan123Url))
		{
			Button button5 = new Button
			{
				Content = (Translator.IsEnglish ? "123Pan" : "123云盘"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Tag = pan123Url
			};
			button5.Click += delegate(object s, RoutedEventArgs e)
			{
				dialog?.Hide();
				OpenUrl((string)((Button)s).Tag);
			};
			stackPanel.Children.Add(button5);
		}
		dialog = new ContentDialog
		{
			Title = (Translator.IsEnglish ? "Choose Download Source" : "选择下载渠道"),
			Content = stackPanel,
			CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
			XamlRoot = base.Content.XamlRoot
		};
		await dialog.ShowAsync();
	}

	private async Task DownloadFromGitHubAsync()
	{
		ElementTheme theme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark);
		try
		{
			ContentDialog loadingDialog = new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Please wait" : "请稍候"),
				Content = (Translator.IsEnglish ? "Getting download information from GitHub..." : "正在从GitHub获取下载信息..."),
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			};
			loadingDialog.ShowAsync();
			using HttpClient client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(30L);
			client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DLSSFrameGenEnabler");
			JsonElement releaseElement = default(JsonElement);
			string requestUri = "https://api.github.com/repos/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases?per_page=20";
			using (JsonDocument docAll = JsonDocument.Parse(await client.GetStringAsync(requestUri)))
			{
				JsonElement rootElement = docAll.RootElement;
				bool flag = false;
				foreach (JsonElement item2 in rootElement.EnumerateArray())
				{
					if (item2.TryGetProperty("prerelease", out var value) && value.GetBoolean())
					{
						releaseElement = item2.Clone();
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					loadingDialog.Hide();
					await Task.Delay(200);
					await new ContentDialog
					{
						Title = (Translator.IsEnglish ? "Download Failed" : "下载失败"),
						Content = (Translator.IsEnglish ? "No beta version found in GitHub releases.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." : "在GitHub releases中没有找到测试版。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
						CloseButtonText = "OK",
						RequestedTheme = theme,
						XamlRoot = base.Content.XamlRoot
					}.ShowAsync();
					return;
				}
			}
			JsonElement property = releaseElement.GetProperty("assets");
			if (property.GetArrayLength() == 0)
			{
				loadingDialog.Hide();
				await Task.Delay(200);
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Download Failed" : "下载失败"),
					Content = (Translator.IsEnglish ? "No downloadable files found in the latest release.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." : "最新版本中没有找到可下载的文件。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
					CloseButtonText = "OK",
					RequestedTheme = theme,
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				return;
			}
			JsonElement jsonElement = property[0];
			string downloadUrl = jsonElement.GetProperty("browser_download_url").GetString();
			string fileName = jsonElement.GetProperty("name").GetString();
			long fileSize = jsonElement.GetProperty("size").GetInt64();
			string version = (releaseElement.TryGetProperty("tag_name", out var value2) ? value2.GetString() : "");
			string changelog = (releaseElement.TryGetProperty("body", out var value3) ? value3.GetString() : "");
			loadingDialog.Hide();
			await Task.Delay(200);
			StackPanel stackPanel = new StackPanel
			{
				Spacing = 12.0
			};
			stackPanel.Children.Add(new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "Latest Version: {0}" : "最新版本：{0}", version),
				FontWeight = FontWeights.SemiBold,
				FontSize = 16.0
			});
			stackPanel.Children.Add(new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "File Size: {0:0.0} MB" : "文件大小：{0:0.0} MB", (double)fileSize / 1024.0 / 1024.0),
				Opacity = 0.7
			});
			if (!string.IsNullOrEmpty(changelog))
			{
				stackPanel.Children.Add(new TextBlock
				{
					Text = (Translator.IsEnglish ? "Update Notes:" : "更新内容："),
					FontWeight = FontWeights.SemiBold,
					Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
				});
				ScrollViewer scrollViewer = new ScrollViewer
				{
					MaxHeight = 300.0,
					VerticalScrollMode = ScrollMode.Enabled,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto
				};
				TextBlock content = new TextBlock
				{
					Text = changelog,
					TextWrapping = TextWrapping.Wrap,
					Opacity = 0.9
				};
				scrollViewer.Content = content;
				stackPanel.Children.Add(scrollViewer);
			}
			stackPanel.Children.Add(new TextBlock
			{
				Text = (Translator.IsEnglish ? "Tip: Users in mainland China may need a VPN/accelerator to download from GitHub." : "提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
				FontSize = 12.0,
				Opacity = 0.7,
				Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
				TextWrapping = TextWrapping.Wrap
			});
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Update Available" : "发现新版本"),
				Content = stackPanel,
				PrimaryButtonText = (Translator.IsEnglish ? "Download" : "下载"),
				CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() != ContentDialogResult.Primary)
			{
				return;
			}
			FolderPicker obj = new FolderPicker
			{
				SuggestedStartLocation = PickerLocationId.Desktop,
				FileTypeFilter = { "*" }
			};
			nint windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
			InitializeWithWindow.Initialize(obj, windowHandle);
			StorageFolder folder = await obj.PickSingleFolderAsync();
			if (folder == null)
			{
				return;
			}
			StackPanel progressPanel = new StackPanel
			{
				Spacing = 12.0
			};
			TextBlock item = new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "Downloading: {0}\nSize: {1:0.0} MB" : "正在下载：{0}\n大小：{1:0.0} MB", fileName, (double)fileSize / 1024.0 / 1024.0),
				TextWrapping = TextWrapping.Wrap
			};
			ProgressBar progressBar = new ProgressBar
			{
				Minimum = 0.0,
				Maximum = 100.0,
				Value = 0.0,
				Height = 20.0,
				IsIndeterminate = false
			};
			TextBlock speedText = new TextBlock
			{
				Text = (Translator.IsEnglish ? "Connecting..." : "正在连接..."),
				Opacity = 0.7
			};
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 8.0
			};
			Button pauseBtn = new Button
			{
				Content = (Translator.IsEnglish ? "Pause" : "暂停"),
				Width = 100.0
			};
			Button button = new Button
			{
				Content = (Translator.IsEnglish ? "Cancel" : "取消"),
				Width = 100.0
			};
			stackPanel2.Children.Add(pauseBtn);
			stackPanel2.Children.Add(button);
			progressPanel.Children.Add(item);
			progressPanel.Children.Add(progressBar);
			progressPanel.Children.Add(speedText);
			progressPanel.Children.Add(stackPanel2);
			ContentDialog progressDialog = new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Downloading" : "下载中"),
				Content = progressPanel,
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			};
			CancellationTokenSource cts = new CancellationTokenSource();
			ManualResetEventSlim pauseEvent = new ManualResetEventSlim(initialState: true);
			bool isPaused = false;
			bool deleteOnCancel = false;
			pauseBtn.Click += delegate
			{
				isPaused = !isPaused;
				if (isPaused)
				{
					pauseEvent.Reset();
					pauseBtn.Content = (Translator.IsEnglish ? "Resume" : "继续");
					speedText.Text = (Translator.IsEnglish ? "Paused" : "已暂停");
				}
				else
				{
					pauseEvent.Set();
					pauseBtn.Content = (Translator.IsEnglish ? "Pause" : "暂停");
				}
			};
			button.Click += delegate
			{
				StackPanel stackPanel4 = new StackPanel
				{
					Spacing = 16.0
				};
				stackPanel4.Children.Add(new TextBlock
				{
					Text = (Translator.IsEnglish ? "Confirm cancel download?" : "确认取消下载？"),
					FontWeight = FontWeights.SemiBold,
					FontSize = 16.0
				});
				stackPanel4.Children.Add(new TextBlock
				{
					Text = (Translator.IsEnglish ? "Choose how to cancel:" : "请选择取消方式："),
					Opacity = 0.7
				});
				StackPanel stackPanel5 = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Spacing = 8.0
				};
				Button button2 = new Button
				{
					Content = (Translator.IsEnglish ? "Cancel & Delete" : "取消并删除文件"),
					Width = 140.0
				};
				Button button3 = new Button
				{
					Content = (Translator.IsEnglish ? "Cancel & Keep" : "取消并保留文件"),
					Width = 140.0
				};
				Button button4 = new Button
				{
					Content = (Translator.IsEnglish ? "Continue" : "继续下载"),
					Width = 120.0
				};
				stackPanel5.Children.Add(button2);
				stackPanel5.Children.Add(button3);
				stackPanel5.Children.Add(button4);
				stackPanel4.Children.Add(stackPanel5);
				progressDialog.Content = stackPanel4;
				progressDialog.Title = (Translator.IsEnglish ? "Cancel Download" : "取消下载");
				button2.Click += delegate
				{
					deleteOnCancel = true;
					cts.Cancel();
					pauseEvent.Set();
					progressDialog.Hide();
				};
				button3.Click += delegate
				{
					cts.Cancel();
					pauseEvent.Set();
					progressDialog.Hide();
				};
				button4.Click += delegate
				{
					progressDialog.Content = progressPanel;
					progressDialog.Title = (Translator.IsEnglish ? "Downloading" : "下载中");
				};
			};
			Task<string> task = Task.Run(async delegate
			{
				string filePath = Path.Combine(folder.Path, fileName);
				long existingBytes = 0L;
				if (File.Exists(filePath))
				{
					existingBytes = new FileInfo(filePath).Length;
				}
				using HttpClient downloadClient = new HttpClient();
				downloadClient.Timeout = TimeSpan.FromMinutes(60L);
				if (existingBytes > 0)
				{
					downloadClient.DefaultRequestHeaders.Range = new RangeHeaderValue(existingBytes, null);
				}
				HttpResponseMessage httpResponseMessage = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
				httpResponseMessage.EnsureSuccessStatusCode();
				long totalBytes = httpResponseMessage.Content.Headers.ContentLength.GetValueOrDefault(fileSize);
				if (existingBytes > 0 && httpResponseMessage.StatusCode == HttpStatusCode.PartialContent)
				{
					totalBytes += existingBytes;
				}
				else
				{
					existingBytes = 0L;
				}
				FileMode fileMode = ((existingBytes > 0) ? FileMode.Append : FileMode.Create);
				using Stream contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(cts.Token);
				using FileStream fileStream = new FileStream(filePath, fileMode, FileAccess.Write, FileShare.None);
				byte[] buffer = new byte[65536];
				long totalRead = existingBytes;
				Stopwatch stopwatch = Stopwatch.StartNew();
				while (true)
				{
					int num;
					int bytesRead = (num = await contentStream.ReadAsync(buffer, 0, buffer.Length, cts.Token));
					if (num <= 0)
					{
						break;
					}
					pauseEvent.Wait(cts.Token);
					await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
					totalRead += bytesRead;
					int progress = (int)((double)totalRead / (double)totalBytes * 100.0);
					double speed = (double)(totalRead - existingBytes) / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;
					double remainingSeconds = ((speed > 0.0) ? ((double)(totalBytes - totalRead) / 1024.0 / 1024.0 / speed) : 0.0);
					base.DispatcherQueue.TryEnqueue(delegate
					{
						if (!isPaused)
						{
							progressBar.Value = progress;
							string text = ((existingBytes > 0) ? string.Format(Translator.IsEnglish ? " (Resumed from {0:0.0} MB)" : " (已续传 {0:0.0} MB)", (double)existingBytes / 1024.0 / 1024.0) : "");
							TextBlock textBlock = speedText;
							string format = (Translator.IsEnglish ? "{0:0.0} MB/s | {1:0.0}% | {2:0}s remaining{3}" : "{0:0.0} MB/s | {1:0.0}% | 剩余 {2:0} 秒{3}");
							InlineArray4<object> buffer2 = default(InlineArray4<object>);
							buffer2[0] = speed;
							buffer2[1] = progress;
							buffer2[2] = remainingSeconds;
							buffer2[3] = text;
							textBlock.Text = string.Format(format, (ReadOnlySpan<object?>)buffer2);
						}
					});
				}
				stopwatch.Stop();
				return filePath;
			});
			progressDialog.ShowAsync();
			string savedPath;
			try
			{
				savedPath = await task;
			}
			catch (OperationCanceledException)
			{
				if (!deleteOnCancel)
				{
					return;
				}
				await Task.Delay(500);
				try
				{
					string path = Path.Combine(folder.Path, fileName);
					if (File.Exists(path))
					{
						File.Delete(path);
					}
					return;
				}
				catch
				{
					return;
				}
			}
			catch (Exception ex2)
			{
				_ = ex2;
				cts.Cancel();
				progressDialog.Hide();
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Download Failed" : "下载失败"),
					Content = (Translator.IsEnglish ? "Download failed. This may be due to network issues or GitHub access restrictions.\n\nTip: Users in mainland China may need a VPN/accelerator to download from GitHub." : "下载失败，可能是网络问题或GitHub访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
					CloseButtonText = "OK",
					RequestedTheme = theme,
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				return;
			}
			progressDialog.Hide();
			StackPanel stackPanel3 = new StackPanel
			{
				Spacing = 8.0
			};
			stackPanel3.Children.Add(new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "File saved to:\n{0}" : "文件已保存到：\n{0}", savedPath),
				TextWrapping = TextWrapping.Wrap
			});
			stackPanel3.Children.Add(new TextBlock
			{
				Text = (Translator.IsEnglish ? "\nChoose installation method:" : "\n请选择安装方式："),
				TextWrapping = TextWrapping.Wrap
			});
			stackPanel3.Children.Add(new TextBlock
			{
				Text = (Translator.IsEnglish ? "• Manual Install: Safe, extract and replace files yourself\n• Auto Install (Beta): Automatically extract and overwrite, may have risks" : "• 手动安装：安全，自行解压替换文件\n• 自动安装（Beta）：自动解压覆盖，可能存在风险"),
				TextWrapping = TextWrapping.Wrap,
				Opacity = 0.7
			});
			switch (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Download Complete" : "下载完成"),
				Content = stackPanel3,
				PrimaryButtonText = (Translator.IsEnglish ? "Auto Install (Beta)" : "自动安装（Beta）"),
				SecondaryButtonText = (Translator.IsEnglish ? "Manual Install" : "手动安装"),
				CloseButtonText = "OK",
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync())
			{
			case ContentDialogResult.Primary:
				await AutoInstallUpdateAsync(savedPath);
				break;
			case ContentDialogResult.Secondary:
				Process.Start("explorer.exe", "/select,\"" + savedPath + "\"");
				break;
			}
		}
		catch (Exception ex3)
		{
			_ = ex3;
			await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Download Failed" : "下载失败"),
				Content = (Translator.IsEnglish ? "Failed to get download information from GitHub. This may be due to network issues or GitHub access restrictions.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." : "从GitHub获取下载信息失败，可能是网络问题或GitHub访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
				CloseButtonText = "OK",
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
		}
	}

	private async Task AutoInstallUpdateAsync(string zipPath)
	{
		try
		{
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Auto Install (Beta)" : "自动安装（Beta）"),
				Content = (Translator.IsEnglish ? "Auto Install is still in testing, please backup your current version before using this feature.\n\nThe software will close automatically, then the update script will replace files and restart.\n\nDo not turn off your computer during the update.\n\nContinue?" : "自动安装功能仍在测试中，使用前请尽量备份好原版本。\n\n软件将自动关闭，随后更新脚本会替换文件并重新启动。\n\n更新过程中请勿关闭电脑。\n\n是否继续？"),
				PrimaryButtonText = (Translator.IsEnglish ? "Continue" : "继续"),
				CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
				RequestedTheme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() != ContentDialogResult.Primary)
			{
				return;
			}
			string text = Path.Combine(Path.GetTempPath(), $"DFGE_Update_{DateTime.Now:yyyyMMdd_HHmmss}");
			Directory.CreateDirectory(text);
			ZipFile.ExtractToDirectory(zipPath, text, overwriteFiles: true);
			string value = text;
			string[] directories = Directory.GetDirectories(text);
			if (directories.Length == 1 && Directory.GetFiles(directories[0], "*.exe", SearchOption.TopDirectoryOnly).Length != 0)
			{
				value = directories[0];
			}
			string value2 = AppContext.BaseDirectory.TrimEnd('\\');
			string text2 = Path.Combine(Path.GetTempPath(), "DFGE_Update.bat");
			string value3 = "DLSSFrameGenEnabler_WinUI3.exe";
			string value4 = new Random().Next(100000, 999999).ToString();
			string contents = $"@echo off\necho Updating DLSS5+MFG Enable...\necho.\n\nREM 等待软件进程结束\ntimeout /t 3 /nobreak >nul\ntaskkill /f /im {value3} 2>nul\ntimeout /t 2 /nobreak >nul\n\nREM 备份当前版本\nset BACKUP_DIR=%TEMP%\\DFGE_Backup_{value4}\nmkdir \"%BACKUP_DIR%\" 2>nul\necho Backing up current version...\nxcopy \"{value2}\\*\" \"%BACKUP_DIR%\\\" /E /Y /Q /H >nul 2>&1\n\nREM 复制新文件\necho Copying new files...\nxcopy \"{value}\\*\" \"{value2}\\\" /E /Y /R /H >nul 2>&1\n\nREM 重新启动软件\necho Starting software...\nstart \"\" \"{value2}\\{value3}\"\n\nREM 清理临时文件\ntimeout /t 3 /nobreak >nul\nrmdir /s /q \"{text}\" 2>nul\ndel \"%~f0\" 2>nul\n";
			File.WriteAllText(text2, contents, Encoding.Default);
			Process.Start(new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = "/c \"\"" + text2 + "\"\"",
				UseShellExecute = true,
				CreateNoWindow = false,
				WindowStyle = ProcessWindowStyle.Normal
			});
			Application.Current.Exit();
		}
		catch (Exception ex)
		{
			await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Auto Install Failed" : "自动安装失败"),
				Content = string.Format(Translator.IsEnglish ? "Auto install failed: {0}\n\nPlease use manual install instead." : "自动安装失败：{0}\n\n请改用手动安装。", ex.Message),
				CloseButtonText = "OK",
				RequestedTheme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
		}
	}

	private void OpenUrl(string url)
	{
		if (!string.IsNullOrEmpty(url))
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
		}
	}

}
