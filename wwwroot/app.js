(function () {
  var wv = (window.chrome && window.chrome.webview) ? window.chrome.webview : null;
  var games = [];
  var listEl = document.getElementById('gameList');
  var countEl = document.getElementById('listCount');
  var toastEl = document.getElementById('toast');
  var toastTimer = null;
  var viewBtn = document.getElementById('btnViewMode');
  var viewGlyph = document.getElementById('viewGlyph');
  var viewLabel = document.getElementById('viewLabel');

  var modalMask = document.getElementById('modalMask');
  var modalBox = document.getElementById('modalBox');
  var modalTitle = document.getElementById('modalTitle');
  var modalText = document.getElementById('modalText');
  var modalOptions = document.getElementById('modalOptions');
  var modalConfirm = document.getElementById('modalConfirm');
  var modalCancel = document.getElementById('modalCancel');
  var confirmCallback = null;
  var progressMask = document.getElementById('progressMask');
  var progressText = document.getElementById('progressText');
  var currentDialogId = null;
  var ctxMenu = document.getElementById('ctxMenu');
  var ctxExe = '';
  var ctxGame = null;
  var toolMask = document.getElementById('toolMask');
  var toolBox = document.getElementById('toolBox');
  var toolTitle = document.getElementById('toolTitle');
  var toolText = document.getElementById('toolText');
  var toolBody = document.getElementById('toolBody');
  var toolConfirm = document.getElementById('toolConfirm');
  var toolCancel = document.getElementById('toolCancel');
  var toolCallback = null;
  var groupMask = document.getElementById('groupMask');

  var state = {
    english: false,
    dark: true,
    anim: false,
    backdrop: 0,
    wallpaper: '',
    gpuSelected: '',
    backup: { sizeMb: 0, dirs: [] },
    installPath: '',
    lang: 0,
    fontSize: 0,
    currentPage: 'home',
    smooth: null,
    smoothTab: 'official',
    hsf: null,
    downloads: {},
    version: '1.75',
    isBeta: false,
    bigView: true
  };
  var captureData = null;
  var captureWindowOptions = [];
  var captureFormInitialized = false;
  try {
    state.bigView = localStorage.getItem('dfge.viewMode') !== 'list';
  } catch (e) { }
  var homeIntroShown = false;

  var EN = {
    'app.brand': 'Frame Gen + DLSS5 Enabler',
    'app.sub': 'V1.75-Beta · External Capture Integrated',
    'app.brandSub': 'Frame Gen + DLSS5',
    'nav.home': 'Home',
    'nav.usage': 'Usage Guide',
    'nav.update': 'Update Log',
    'nav.smooth': 'Smooth Motion',
    'nav.hsf': 'HSF Frame Gen',
    'nav.framegen': 'Frame Gen Mods',
    'fg.title': 'Frame Gen Mods',
    'fg.sub': 'Choose a frame generation path. All three stay inside this app; no second tool needed.',
    'fg.smoothTitle': 'NVIDIA Smooth Motion',
    'fg.smoothDesc': 'Driver-level AI frame interpolation written straight into the NVIDIA profile. RTX 40/50 use the official driver; RTX 20/30 use the built-in patch. Best for games without native frame generation.',
    'fg.smoothTag1': 'RTX 40/50 official',
    'fg.smoothTag2': 'RTX 20/30 patch',
    'fg.smoothTag3': '2X',
    'fg.hsfTitle': 'HSF Frame Generation',
    'fg.hsfDesc': 'ReShade capture frame interpolation feeding the game’s frames, depth and motion data to NVIDIA DLSS-G. Supports DX9 / DX11 / DX12 / Vulkan, up to 6X; LaunchPad is recommended.',
    'fg.hsfTag1': 'LaunchPad recommended',
    'fg.hsfTag2': 'Up to 6X',
    'fg.hsfTag3': 'All APIs',
    'fg.enter': 'Open settings',
    'fg.back': 'Back to Frame Gen Mods',
    'fg.tip': 'Tip: external capture is experimental. It does not modify game files, but some anti-cheat systems may still flag it. Check the risk before using it in online games. HSF with LaunchPad has better optical flow than the default NVOF and significantly less ghosting. Vulkan games should use windowed mode; borderless/fullscreen may hide generated frames.',
    'fg.captureTitle': 'External Capture (OptiCaptureHost)',
    'fg.captureDesc': 'Writes nothing into the game folder: Windows Graphics Capture grabs the game window and feeds it to the built-in OptiScaler. Good for games where mods are inconvenient; windowed / borderless only, up to 6X.',
    'fg.captureTag1': 'No injection',
    'fg.captureTag2': 'Any window',
    'fg.captureTag3': 'Up to 6X',
    'capture.experimentalBadge': 'Experimental',
    'capture.title': 'External Capture (Experimental)',
    'capture.sub': 'No files are written into the game folder. Windows Graphics Capture grabs the game window, and the built-in OptiCaptureHost + OptiScaler handle upscaling and multi frame generation. Use windowed or borderless mode.',
    'capture.notRunning': 'Not running',
    'capture.engine': 'Capture engine',
    'capture.window': 'Current window',
    'capture.fpsIn': 'Capture FPS',
    'capture.fpsOut': 'Presented FPS',
    'capture.idleNote': 'Pick a running game window, then press Start. This page never opens OptiCaptureLauncher and never writes into the game folder.',
    'capture.windowTitle': 'Select game window',
    'capture.windowDesc': 'Lists visible windows and keeps the largest main window per process. The game must be windowed or borderless; exclusive fullscreen cannot be captured.',
    'capture.refresh': 'Refresh windows',
    'capture.openLog': 'Open capture log',
    'capture.windowSelect': 'Game window',
    'capture.windowSelectDesc': 'If the game is missing, start it in windowed mode and refresh again.',
    'capture.chooseWindow': 'Refresh and choose a game window',
    'capture.start': 'Start capture',
    'capture.stop': 'Stop capture',
    'capture.menu': 'Open OptiScaler menu',
    'capture.focus': 'Return mouse / focus',
    'capture.overlay': 'Hide overlay / switch out',
    'capture.hotkeyNote': 'In-game hotkeys: Ctrl+Shift+F9 hides/restores the overlay without stopping capture, Ctrl+Shift+F10 opens the menu, Ctrl+Shift+F11 switches game / overlay focus, Ctrl+Shift+F12 stops capture. With the overlay hidden you can Alt+Tab to other windows; frame generation may pause while the game is out of focus and resume when you return.',
    'capture.settingsTitle': 'Frame generation and image settings',
    'capture.settingsDesc': 'These settings are written to the bundled OptiCaptureHost.ini and OptiScaler.ini, never into the game folder. VSync applies live; changing frame-gen mode or multiplier while running restarts capture; upscaling and output scaling need a stop/restart.',
    'capture.fgMode': 'Frame generation mode',
    'capture.fgModeDesc': 'Select the frame-generation method used by external capture. F5Low is the default; other modes depend on the GPU and installed runtimes.',
    'capture.fgF5low': 'F5Low (capture recommended)',
    'capture.fgXefg': 'XeFG (original compatibility)',
    'capture.fgDlssg': 'DLSSG (Artur / older GPUs)',
    'capture.fgCombo': 'Combo MFG (DLSSG + XeFG)',
    'capture.fgNukem': 'Nukem FG',
    'capture.fgFsr': 'FSR3-4 FG',
    'capture.fgNvidia': 'NVIDIA MFG (RTX 50)',
    'capture.fgOff': 'Frame generation off',
    'capture.mfg': 'Multi-frame multiplier',
    'capture.mfgDesc': 'Sets how many frames are generated per source frame. Higher multipliers produce more output; support depends on the frame-generation mode.',
    'capture.mfgAuto': 'Auto',
    'capture.upscaler': 'Upscaler',
    'capture.upscalerDesc': 'Select the OptiScaler upscaling backend. Choose by GPU model and game compatibility.',
    'capture.upFsr4': 'FSR 4.1.2 (recommended)',
    'capture.upXess': 'Intel XeSS',
    'capture.upFsr3': 'AMD FSR 3.1.6',
    'capture.upDlss': 'NVIDIA DLSS',
    'capture.upDlss37': 'DLSS 3.7 (AMD / GTX 16)',
    'capture.upDitto': 'DittoSR',
    'capture.upscale': 'Output scaling',
    'capture.upscaleDesc': 'When enabled, Start shrinks the game window and the host scales it to fullscreen; stopping restores the original window.',
    'capture.upOff': 'Off',
    'capture.upFsr': 'FSR3/4',
    'capture.upDlssOut': 'DLSS',
    'capture.upFsr1': 'FSR 1',
    'capture.upNis': 'NIS',
    'capture.upBilinear': 'Bilinear',
    'capture.upscaleLevel': 'Output scaling level',
    'capture.upscaleLevelDesc': 'Sets the output-scaling ratio. Native keeps the window size and only applies super-resolution anti-aliasing.',
    'capture.lvQuality': 'Quality (1.5X)',
    'capture.lvBalanced': 'Balanced (1.7X)',
    'capture.lvPerformance': 'Performance (2X)',
    'capture.lvNative': 'Native (1X)',
    'capture.depth': 'ReShade true depth (experimental)',
    'capture.depthDesc': 'When enabled, the host reads the game’s real depth during output scaling to improve edges and occlusion. Output scaling must be on and the ReShade depth add-on installed in the game folder.',
    'capture.statDepth': 'Depth received',
    'capture.vsync': 'VSync',
    'capture.vsyncDesc': 'Controls whether the host uses VSync. Changes apply live.',
    'capture.presentMode': 'Present mode',
    'capture.presentModeDesc': 'Selects the host presentation path. Classic has better compatibility; Lossless uses an alternative presentation path.',
    'capture.pmClassicUnlimited': 'Classic + unlimited (recommended)',
    'capture.pmClassic': 'Classic',
    'capture.pmLossless': 'Lossless low latency',
    'capture.pmAuto': 'Auto',
    'capture.pmNoWait': 'Never wait',
    'capture.pmWait': 'Always wait for vblank',
    'capture.pmHalf': 'Classic + half refresh',
    'capture.boost': 'Performance mode',
    'capture.boostDesc': 'Sets the host process priority. Max is the default; weaker GPUs can use a lower priority.',
    'capture.boostBalanced': 'Balanced (weak GPUs)',
    'capture.boostLight': 'Light (CPU only)',
    'capture.boostMax': 'Max (strong GPUs, recommended)',
    'capture.boostOff': 'Off',
    'capture.presentDelay': 'Present delay',
    'capture.presentDelayDesc': 'Milliseconds to wait before each Present; 0 is off. Used to fine-tune presentation pacing.',
    'capture.basePriority': 'Source-frame priority',
    'capture.basePriorityDesc': 'Switches to F5Low + 2X + 0 ms to reduce the multiplier’s load on the source frame; it does not turn frame generation off.',
    'capture.basePriorityBtn': 'Switch to source-frame priority (2X)',
    'capture.apply': 'Save settings',
    'capture.openEngine': 'Open capture engine folder',
    'capture.statsTitle': 'Live status',
    'capture.statCapture': 'Capture FPS',
    'capture.statPresent': 'Host base FPS',
    'capture.statDxgi': 'Presented FPS',
    'capture.statPresentMs': 'Present time',
    'capture.statFocus': 'Focus',
    'capture.statRefresh': 'Display refresh',
    'capture.notesTitle': 'Notes and limits',
    'capture.note1': '· This is external capture: by default it does not modify game files; OptiScaler only attaches to the OptiCaptureHost process. Only when you enable ReShade depth and manually install the add-on does it write ReShade depth helper files into the game folder.',
    'capture.note2': '· Captured frames have no true motion vectors; the host estimates motion on the GPU. ReShade depth only adds depth and does not remove jelly-like motion on fast movement.',
    'capture.note3': '· The game must be windowed or borderless; exclusive fullscreen cannot be captured. HDR is not a strength here; test with HDR off first.',
    'capture.note4': '· Online games and anti-cheat games may still treat a captured / resized window as abnormal. ReShade and depth add-ons carry a ban risk in online/anti-cheat games; single-player testing only.',
    'capture.note5': '· Bundles OptiCaptureHost 2.12.0 (QSXK custom build keeping Ctrl+Shift+F9 overlay hide) and OptiScaler 0.1.39-pre1, both GPL-3.0. License texts and corresponding source archives are under CaptureHost\\licenses and CaptureHost\\source.',
    'capture.note6': '· ReShade depth: install ReShade 6+ with add-on support in the game → close the game → copy the files under CaptureHost\\ReShade depth into the game exe folder, keeping the folder structure → start the game, enable OptiCaptureDepth in ReShade → enable “ReShade true depth” and output scaling on this page, then start capture.',
    'capture.note7': '· Low-latency defaults: Input = OptiScaler, Output = NVIDIA Reflex, frame limit = 0. 0 disables the fixed cap so the render rate is not lowered just to be refilled by generated frames. The 516 in the screenshot is a personal setting, not a default.',
    'capture.note8': '· This build disables OptiCaptureHost’s own RTSS limiter and OptiFps override; every capture start forces Target=0 / Auto=0 / Fps=0 / OptiFps=0, so frame limit 0 is no longer rewritten to the refresh rate or 1/4 refresh rate.',
    'hsf.apiTitle': 'API compatibility',
    'hsf.apiDx9': 'For Source 1 engine DX9 games, ReShade/HSF must be installed in the game root’s bin folder; installing to the root will not load. This app detects it from the PE import table and shaderapidx9.dll, and lets you override the install folder on the game card.',
    'hsf.apiDx9Tag': 'Auto-detect bin',
    'hsf.apiDx11': 'HSF helper route; fullscreen and windowed both work. Install LaunchPad for steadier flow and less ghosting.',
    'hsf.apiDx11Tag': 'LaunchPad recommended',
    'hsf.apiDx12Native': 'HSF reads the game’s own DLSS data; usually native 2X only. 3X-6X needs MFG-Unlocker and is limited by the game provider.',
    'hsf.apiDx12NativeTag': '2X native / MFG unlock',
    'hsf.apiDx12NoDlss': 'HSF helper route; needs LaunchPad or LumeniteFX for flow. 3X-6X needs MFG-Unlocker.',
    'hsf.apiDx12NoDlssTag': 'Flow source required',
    'hsf.apiVulkan': 'HSF helper route; windowed mode recommended. Borderless/fullscreen may run generation but hide the generated frames; some games only show them in windowed mode.',
    'hsf.apiVulkanTag': 'Windowed recommended',
    'hsf.apiFoot': 'Compatibility notes describe how HSF works, not a guarantee of the same multiplier in every game. The game’s DLSS-G / driver limits still apply.',
    'hsf.qualityLabel': 'Image quality',
    'hsf.launchpadQuality': 'LaunchPad’s optical flow is better than the default NVOF and significantly reduces ghosting. Prioritize it for DX9 / DX11 / Vulkan and DX12 games without native DLSS data.',
    'nav.download': 'Downloads',
    'nav.settings': 'Settings',
    'nav.about': 'About',
    'hsf.title': 'HSF Frame Generation',
    'hsf.sub': "Hunter's Sloppy Frames: feed the game's frames to NVIDIA DLSS Frame Generation without opening a second app.",
    'hsf.checking': 'Checking…',
    'hsf.package': 'Built-in pack',
    'hsf.gpu': 'GPU',
    'hsf.addon': 'Add-on',
    'hsf.reshade': 'ReShade',
    'hsf.gamesTitle': 'Install per game',
    'hsf.gamesDesc': 'Install writes ReShade 6.8 add-on support and HSF into the game folder; Source 1 DX9 games are automatically installed into the bin subfolder, and you can switch Auto / Game root / bin on the game card. Restore removes only files this app added. Fully exit the game first.',
    'hsf.auxHint': 'Vulkan / DX11 without a flow source: click “Install LaunchPad” on the game card. LaunchPad’s optical flow is better than the default NVOF and significantly reduces ghosting, so install it first. RTX 20/30/40 that need 3X-6X: click “Install MFG-Unlocker”. Everything stays inside this app.',
    'hsf.refresh': 'Refresh',
    'hsf.openPatch': 'Open patch folder',
    'hsf.openDocs': 'Official guide',
    'hsf.noGames': 'No games in the list yet. Add games on Home, then come back to install HSF.',
    'hsf.notesTitle': 'Notes',
    'hsf.note1': "· HSF is not a standalone app but a ReShade add-on: this page installs or reuses ReShade 6.8 add-on support, then adds hsf.addon64/32, HSF_streamline, HSF.fx and HSF.ini.",
    'hsf.note2': '· DX12 games with DLSS enabled can feed HSF with the game’s own DLSS data. DX9 / DX11 / Vulkan and DX12 games without DLSS can click “Install LaunchPad” to add the iMMERSE LaunchPad flow source (downloaded from the official iMMERSE GitHub; not redistributed with this app). LaunchPad’s optical flow is better than the default NVOF and significantly reduces ghosting, so install it first.',
    'hsf.note3': '· RTX 50 supports high multipliers natively; RTX 20/30/40 are normally limited to 2X. Click “Install MFG-Unlocker” to put it into the HSF helper mods folder and unlock 3X-6X. For native DX12, still follow the official MFG-Unlocker guide and rename the DLL next to the game exe; if the game provider reports max frames 1, 3X+ still cannot be enabled.',
    'hsf.note4': '· Single-player games only. Never use with online anti-cheat. HSF 0.9.9 is a preview; some games may crash or show HUD ghosting.',
    'hsf.note5': '· After installing, press Home in game to open ReShade and enable HSF under Add-ons. Restart after changing API, resolution or game updates.',
    'hsf.ready': 'Ready',
    'hsf.missing': 'Missing',
    'hsf.installed': 'HSF installed',
    'hsf.notInstalled': 'Not installed',
    'hsf.partial': 'Incomplete files',
    'hsf.conflict': 'Cannot install',
    'hsf.install': 'Install HSF',
    'hsf.launchpadInstall': 'Install LaunchPad',
    'hsf.mfgInstall': 'Install MFG-Unlocker',
    'hsf.flowLaunchPad': 'Flow: LaunchPad',
    'hsf.flowLumenite': 'Flow: LumeniteFX',
    'hsf.flowMissing': 'Flow: not installed',
    'hsf.mfgInstalled': 'Multiplier: MFG-Unlocker',
    'hsf.mfgNative': 'Multiplier: 2X native',
    'hsf.launchpadDownloading': 'Downloading LaunchPad from the official iMMERSE GitHub…',
    'hsf.mfgInstalling': 'Installing MFG-Unlocker into the HSF helper mods folder…',
    'hsf.restore': 'Restore',
    'hsf.openGame': 'Open game folder',
    'hsf.openLog': 'Open ReShade log',
    'hsf.reshadeOk': 'ReShade 6.8 add-on ready',
    'hsf.reshadeReuse': 'Existing ReShade 6.8 add-on',
    'hsf.reshadeNone': 'Will install built-in ReShade 6.8',
    'hsf.vulkanWarn': 'Vulkan: install ReShade 6.8 add-on first',
    'hsf.gpuOfficial': 'RTX 50: native high multipliers',
    'hsf.gpuUnlocker': 'RTX 20/30/40: MFG-Unlocker needed for 3X+',
    'hsf.gpuUnsupported': 'HSF needs an NVIDIA RTX 20/30/40/50',
    'hsf.packMissing': 'Built-in HSF pack is incomplete',
    'hsf.languageTitle': 'HSF menu language',
    'hsf.languageDesc': 'Choose which HSF add-on gets installed. Chinese uses the translated build; English uses the original HSF 0.9.9 add-on. Installed games can switch directly from their card.',
    'hsf.languageFollow': 'Follow app language',
    'hsf.languageChinese': '简体中文（汉化版）',
    'hsf.languageEnglish': 'English (original HSF)',
    'hsf.languageTip': 'Changing this only affects new installs. For a game that already has HSF, click “Switch to English” or “Switch to Chinese” on its card; the app restores the old install first, then installs the target language.',
    'update.v175Title': 'V1.75 Update Notes',
    'update.v1751': '· Beta republished as V1.75.0.0; external capture, OptiScaler defaults and the patch set keep the current configuration. The notes below keep the V1.65 update details and the V1.63.2 history.',
    'update.v1752': '· The OptiScaler menu now follows the app language: switch the app between Chinese and English and the menu updates within about 0.5 seconds, even while capture is running; no capture restart is needed.',
    'update.v1753': '· Game-card action buttons now have a clearer hover highlight: Frame Gen, DLSS5 and Restore get a stronger border, background and glow, especially in the dark theme.',
    'update.v1651': '· External capture is now part of the beta: a third card under Frame Gen Mods opens the capture page. It writes nothing into the game folder, captures the game window with Windows Graphics Capture, then feeds the bundled OptiCaptureHost 2.12.00 + OptiScaler for upscaling and frame generation.',
    'update.v1652': '· 2.12 real depth: adds an experimental “ReShade real depth” toggle and live depth status. D3D11/D3D12 use GPU shared textures; D3D9/D3D10/OpenGL/Vulkan use CPU shared buffers. Depth only affects the Host’s own FSR/DLSS output scaling and does not fix F5Low/MFG jelly.',
    'update.v1653': '· Capture defaults: F5Low + 3X, with 2X-6X, FSR4.1.2 and Present Classic available; the OptiScaler menu is fully translated into Chinese. Ctrl+Shift+F9 hides/restores the overlay, and the page button still works if the hotkey is taken.',
    'update.v1654': '· Low-latency defaults: OptiScaler input, NVIDIA Reflex output, frame limit 0. 0 disables the fixed cap and avoids lowering FPS before generating frames; the 516 in the menu is a personal setting, not a recommendation.',
    'update.v1655': '· Bundled custom 2.12 capture engine without NGX to avoid hanging at NVSDK_NGX_D3D12_Init with nvngx_dlss.dll 310.9.1; the Host’s own DLSS output scaling falls back to FSR1, while F5Low/MFG/OptiScaler still work. For ReShade depth tests, set output scaling to FSR3/4.',
    'update.v1656': '· All 1.63.x external-capture pages, status, config write-back and Full Pack assets are merged into the 1.65 main beta; the standalone external-capture test package is discontinued from this version.',
    'update.v1621': '· Render API detection now reads the PE import table instead of guessing from the exe bitness or a d3d9.dll file; DX9 no longer gets confused by ReShade’s own proxy DLL.',
    'update.v1622': '· Source 1 DX9 games are detected automatically and ReShade/HSF are installed into the game’s bin subfolder; the game card shows the actual install folder and lets you override Auto / Game root / bin before installing.',
    'update.v1623': '· HSF install, restore, LaunchPad, MFG-Unlocker and ReShade proxy checks now all use the recorded install folder; older root-folder installs still restore correctly and show a migration hint.',
    'update.v1624': '· API compatibility now includes a DX9 / Source engine entry; Vulkan, DX12 and DX11 behavior is unchanged.',
    'update.v1625': '· Settings now detect and show the app install folder on every launch; downloaded incremental packages can be installed immediately.',
    'update.v1611': '· HSF menu language: Chinese installs the translated HSF add-on; English installs the original HSF 0.9.9 add-on. “Follow app language” keeps the HSF menu consistent with the UI.',
    'update.v1612': '· ReShade shell language follows the selected HSF language: Chinese writes zh-CN + MiSans, English writes en-US without forcing the Chinese font.',
    'update.v1613': '· Existing HSF installs can switch menu language from the game card: the app restores the old files first, then reinstalls with the selected language.',
    'hsf.busy': 'HSF is busy, please wait',
    'hsf.installFail': 'HSF install failed',
    'hsf.restoreFail': 'HSF restore failed',
    'hsf.done': 'HSF installed',
    'hsf.restored': 'HSF restored',
    'download.title': 'Downloads',
    'download.sub': 'GitHub releases · HSF components · background downloads',
    'download.openFolder': 'Open download folder',
    'download.emptyTitle': 'No downloads yet',
    'download.emptyDesc': 'Start from “Check for updates”, “Download latest version”, or “Install LaunchPad” on the HSF page. Active tasks appear here.',
    'download.saveTo': 'Save to: ',
    'download.installTo': 'Install to: ',
    'download.downloading': 'Downloading',
    'download.paused': 'Paused',
    'download.completed': 'Completed',
    'download.failed': 'Failed',
    'download.pause': 'Pause',
    'download.resume': 'Resume',
    'download.cancel': 'Cancel',
    'download.open': 'Open location',
    'download.remove': 'Remove',
    'download.done': 'Downloaded',
    'download.cancelTitle': 'Cancel download?',
    'download.cancelText': 'The partial .part file will be deleted. You can start again later.',
    'download.cancelOk': 'Cancel download',
    'download.completeToast': 'Download complete',
    'download.pausedToast': 'Download paused',
    'download.failedToast': 'Download failed',
    'sm.title': 'Smooth Motion',
    'sm.sub': 'No NVIDIA App, no Profile Inspector — write the driver profile directly here.',
    'sm.checking': 'Checking…',
    'sm.gpu': 'GPU',
    'sm.driver': 'Driver',
    'sm.base': 'Driver default',
    'sm.profile': 'Profile',
    'sm.tabOfficial': 'Official driver',
    'sm.tabMod': 'RTX 20/30 Patch',
    'sm.gamesTitle': 'Per-game toggle',
    'sm.gamesDesc': 'The switch writes to this game’s own NVIDIA driver profile, not the global profile. If no profile exists, one is created on first enable.',
    'sm.refresh': 'Refresh',
    'sm.openLog': 'Open log folder',
    'sm.noGames': 'No games in the list yet. Add games on Home, then come back to enable Smooth Motion.',
    'sm.notesTitle': 'Notes',
    'sm.note1': '· RTX 40/50: official driver support — use this page directly. Smooth Motion inserts 1 frame (2x) and supports DX11 / DX12 / Vulkan.',
    'sm.note2': '· RTX 20/30: the patch is built in under Patches\\SmoothMotion; no zip import is needed. Switch to the RTX 20/30 Patch tab and deploy directly.',
    'sm.note3': '· Enabled APIs is a bit mask: DX12 = 1, DX11 = 2, Vulkan = 4; default 7 means all three.',
    'sm.note4': '· Enable DLSS / frame generation in the game first, then Smooth Motion can take effect. Fully restart the game after changes.',
    'sm.modTitle': 'Built-in RTX 20/30 patch',
    'sm.modDesc': 'The patch ships with the app under Patches\\SmoothMotion. No zip import is needed; the RTX20 or RTX30 engine is selected automatically by GPU.',
    'sm.openMod': 'Open patch folder',
    'sm.openModLog': 'Open Mod log',
    'sm.modWarn': 'Fully exit the game before deploying. If the game folder already has its own version.dll, it will not be overwritten; the RTX30 engine requires msvcp140.dll 14.40 or newer.',
    'sm.modGamesTitle': 'Deploy to game',
    'sm.modGamesDesc': 'Only one engine per game; restore before switching GPUs or engines. RTX 40/50 do not need this patch — use the Official driver tab.',
    'sm.on': 'On',
    'sm.off': 'Off',
    'sm.noProfile': 'No profile',
    'sm.modInstalled': 'Mod installed',
    'sm.modConflict': 'version.dll conflict',
    'sm.modNotInstalled': 'Not installed',
    'sm.deploy': 'Deploy',
    'sm.restore': 'Restore',
    'sm.imported': 'Imported',
    'sm.notImported': 'Not imported',
    'sm.supportOfficial': 'Official support',
    'sm.supportMod': 'Needs 20/30 Mod',
    'sm.supportNone': 'Not supported',
    'sm.supportNeedMod': 'Driver profile only — Mod still required',
    'sm.apiLabel': 'APIs',
    'sm.loading': 'Reading driver profiles…',
    'sm.writeFailed': 'Write failed',
    'sm.busy': 'Smooth Motion is busy, please wait',
    'sm.applyOk': 'Smooth Motion updated',
    'sm.applyFail': 'Smooth Motion write failed',
    'sm.resetOk': 'Smooth Motion restored to default',
    'sm.resetFail': 'Restore failed',
    'sm.importOk': 'Mod imported',
    'sm.importFail': 'Mod import failed',
    'sm.deployOk': 'Smooth Motion Mod deployed',
    'sm.restoreOk': 'Smooth Motion Mod restored',
    'sm.logMissing': 'The log folder does not exist yet. Enable Smooth Motion and run the game once; the driver will create C:\\NvPresent.',
    'head.group': 'QQ Group',
    'group.title': 'Join the QQ Group',
    'group.sub': 'Scan the QR code to join the group, share tips, or report issues.',
    'group.id': 'Group ID: 857934769',
    'group.close': 'Close',
    'home.notice': 'Notice: for single-player games only. Do not use with online games or anti-cheat. Modifying game files carries a ban risk — proceed at your own discretion.',
    'home.noticeOk': 'Got it',
    'home.listHint': 'Right-click a game card for quick actions',
    'home.foot': 'Disclaimer: not recommended for online games or games with anti-cheat. This tool modifies files, which may lead to a ban. The author takes no responsibility.',
    'home.bigView': 'Poster view',
    'home.listView': 'List view',
    'home.viewToBig': 'Switch to poster view',
    'home.viewToList': 'Switch to list view',
    'tool.scan': 'Auto Scan',
    'tool.add': 'Manual Add',
    'tool.addExe': 'Select Game exe',
    'tool.detect': 'Detect Running',
    'tool.refresh': 'Refresh Status',
    'tool.clear': 'Clear List',
    'usage.noticeTitle': '⚠️ Important Notice',
    'usage.notice1': '· For single-player games only. Do not use with online games or anti-cheat; modifying game files carries a ban risk.',
    'usage.notice2': '· miHoYo games are not supported due to strict anti-cheat. Use Magpie, HoYoshade or XXMI instead.',
    'usage.notice3': '· Ubisoft and EA games are not widely adapted yet; test each game yourself.',
    'usage.notice4': '· Vulkan support is limited. AMD GPUs are no longer patched by this tool; AMD users please follow the NeuralHub guide.',
    'usage.introTitle': '1. What the app does',
    'usage.intro1': '· Enable Frame Generation and DLSS5 for games with one click. Auto Scan detects Steam / Epic / GOG / Ubisoft libraries; you can also add a folder or exe manually. This version shares the same scanning and patch logic as the full version, so install and restore results match.',
    'usage.intro2': '· Supports NVIDIA RTX 20/30/40/50 series. Patches are matched per game; when installing DLSS5 you also confirm the rendering API, game architecture and GPU model.',
    'usage.fgTitle': '2. Frame Generation',
    'usage.fgClassicTitle': 'Classic mode',
    'usage.fgClassic': '· Replaces nvngx_dlssg.dll. For drivers before 616.56 and frame generation 310.8 or older. Plug-and-play with the best compatibility, ideal for games that cannot update drivers.',
    'usage.fgAdvancedTitle': 'Advanced MFG',
    'usage.fgAdvanced': '· For newer drivers and frame generation 310.9+. Includes ReShade, Universal RTX 40 MFG Unlock and the version.dll proxy. Highest compatibility, but some games need manual config edits to launch.',
    'usage.fgSm86Title': 'SM86 scheme',
    'usage.fgSm86': '· Does not replace original game files: place version.dll, dlssg_sm86.ini and the dlssg_sm86 folder next to the game exe. Includes kernel optimizations and can pair DLSS upscaling with XeSS frame generation.',
    'usage.fgRule': '· MFG and SM86 are mutually exclusive. Restore before switching schemes on the same game.',
    'usage.dlssTitle': '3. DLSS5',
    'usage.dlssVersions': '· DLSS5 offers 1.7.0 or 2.0.0. 1.7.0 has the widest compatibility and works alongside MFG 1.7.0 / 1.7.1; 2.0.0 has more features but conflicts with MFG 1.7.1 and crashes, while it works with the SM86 scheme.',
    'usage.dlssInstall': '· The installer asks for DX12 / DX9 / Vulkan; DX9 also asks for standard/Source-engine install and 32/64-bit. In some games, press Home after launch to open the GITC menu and enable NR manually (off by default).',
    'usage.dlssConfig': '· Games with SM86 get a Configure button for proxy DLL, frame generation engine, multiplier, levels, preset, super resolution, optical flow, HUD and log level. Fully restart the game after changes.',
    'usage.scanTitle': '4. Game management',
    'usage.scanAuto': '· Auto Scan: detects Steam, Epic, GOG and Ubisoft libraries; only adds games with a real executable, skipping empty and engine folders.',
    'usage.scanManual': '· Manual Add: pick a game folder to auto-detect the exe; for DX9 or older games you can select the exe directly.',
    'usage.scanDetect': '· Detect Running: pick from running processes; games already in the list are hidden; the dialog shows icons to identify them.',
    'usage.scanRefresh': '· Refresh Status: re-checks enabled states after external file changes or restores.',
    'usage.scanClear': '· Clear List: clears only the test-build game list, not the full version data.',
    'usage.scanNoMenu': '· Right-click a game card to enable/restore frame generation, toggle DLSS5, modify the config name, run troubleshoot repair, or edit remark/tags.',
    'usage.restoreTitle': '5. Restore and status',
    'usage.restore1': '· Restore removes installed Frame Generation and DLSS5 patches, restores backups, then re-checks the state.',
    'usage.restore2': '· If nothing is enabled, the app reports that there is nothing to restore.',
    'usage.restore3': '· Fully exit the game before installing or restoring to avoid file locks; if patches stop working after a game update, install them again.',
    'usage.faqTitle': '6. FAQ',
    'usage.faq1': '· Enabled but no effect: enable DLSS / Frame Generation in game settings first, close the game, then install patches; for SM86 try another proxy DLL.',
    'usage.faq2': "· Game won't launch or crashes: check whether MFG 1.7.1 and GITC DLSS5 2.0.0 are both enabled; restore first, then use a compatible combination. Verify game files on Steam if needed.",
    'usage.faq3': '· Frame Gen button is grey: no DLSS / frame generation signatures were found, so the game does not support it. Not every game can enable it.',
    'usage.faq4': '· Game not found by scan: use Manual Add to pick the exe, or launch the game first and use Detect Running.',
    'usage.sysTitle': '7. System requirements and notes',
    'usage.sysReq': '· Windows 10 1809 or later (Windows 11 recommended); WebView2 Runtime required; NVIDIA RTX 20/30/40/50 series with the latest driver recommended.',
    'usage.sysNotes': '· Back up saves before use; fully close the game when patching or restoring; not recommended for online games or anti-cheat; RE Engine games can use GITC DLSS5 + Frame Generation together; DX9 uses the d3d9.dll branch — mind 32/64-bit.',
    'usage.betaTitle': 'Beta feedback guide',
    'usage.beta1': '· This is a beta build; features and UI are still changing. When reporting a problem, include the game name, patch scheme (Classic / MFG / SM86), DLSS5 version, rendering API, symptoms or screenshots.',
    'usage.beta2': '· Settings → Runtime log opens error.log directly. Send the log with the problem description to help locate the issue faster.',
    'usage.beta3': '· Feedback channels: GitHub Issues, Bilibili and Xiaoheihe; links are on the About page.',
    'update.v1451': '· Unified UI: white selected nav item with a bottom shadow; toolbar, settings, About, dialogs and dropdowns now share the same WebUI style; the Restore button is red.',
    'update.v1454': '· Home: removed the search box and filtering; game cards now have a right-click menu for one-click frame-gen on/restore, DLSS5 on/off, SM86 config, launch, open folder and remove from list.',
    'update.v1455': '· Poster view: added a 9:16 game cover view that reads covers from the game library; fixed incomplete cover display.',
    'update.v1457': '· Fixes and cleanup: removed 8-Zip false detection; improved auto scan and running-game detection; fixed some game list state display issues.',
    'update.v1458': '· Downloads: moved downloads to a separate Downloads page with pause/resume, cancel and open-location actions; the main UI stays usable while downloading.',
    'update.v1601': '· Added HSF frame generation: a ReShade add-on that feeds the game image, depth and motion data to NVIDIA DLSS-G. Supports DX9 / DX11 / DX12 / Vulkan, up to 6X; MFG-Unlocker unlocks 3X-6X through the HSF helper mods folder.',
    'update.v1602': '· Frame Gen Mods: Smooth Motion and HSF are merged into one navigation entry. Choose “NVIDIA Smooth Motion” or “HSF Frame Generation”; each sub-page can return to the selector.',
    'update.v1603': '· The HSF page now includes API compatibility notes for DX11, native-DLSS DX12, DX12 without DLSS, and Vulkan. Vulkan games should use windowed mode.',
    'update.v1604': '· Stronger LaunchPad guidance: LaunchPad optical flow is better than the default NVOF and significantly reduces ghosting; the game-card button is now “Install LaunchPad (recommended)”.',
    'update.v1605': '· The Downloads page now shows real LaunchPad download progress; the “Downloads” navigation item shows the number of active download tasks.',
    'update.v1501': '· Added Smooth Motion: AI frame interpolation for some games without native frame generation. Currently only 2X, and only DX11+ and Vulkan are supported. This feature is in testing!',
    'update.v1501sub': '(If an error pops up when selecting a single render mode, ignore it — the app simply did not get administrator rights, and it will automatically fall back to the default adjustment.)',
    'update.v1502': '· Reduced source-directory files and overall app size (the app itself is only 70 MB, while related patches take about 2.6 GB).',
    'update.b1': '· New SM86 scheme for frame generation: after choosing Advanced, a dialog lets you pick MFG or SM86.',
    'update.b2': '· MFG is balanced between image quality and frame rate; SM86 does not replace original game files, includes kernel optimizations and allows DLSS upscaling + XeSS frame generation.',
    'update.b3': '· SM86 config panel: after enabling, click "Configure" on the card to change proxy DLL, frame generation engine and levels, super resolution, XeSS-FG, and log level.',
    'update.b4': '· Note: SM86 and MFG can only be enabled one at a time.',
    'update.b5': '· GITC DLSS5 is upgraded to 2.0.0, but 2.0.0 conflicts with MFG 1.7.1 and crashes the game, so 1.7.0 is kept; enabling DLSS5 lets you choose 1.7.0 or 2.0.0.',
    'update.b6': '· Compatibility check warns against enabling MFG 1.7.1 and GITC 2.0.0 together, but still allows installation.',
    'update.b7': '· Fixed some games still showing Chinese names in the English UI.',
    'update.p1': '· MFG updated to 1.7.1.',
    'update.p2': '· Since GITC DLSS5 1.8.0 conflicts with MFG 1.7.1 and crashes, GITC DLSS5 was not updated.',
    'set.appearance': 'Appearance',
    'set.dark': 'Dark mode',
    'set.darkDesc': 'Switch between light and dark with a smooth transition',
    'set.anim': 'Animations',
    'set.animDesc': 'When off, page transitions, theme changes and all other animations are disabled to save resources',
    'set.backdrop': 'Window material',
    'set.backdropDesc': 'Mica / Acrylic / Pure transparent / Custom wallpaper',
    'set.bd0': 'Default',
    'set.bd1': 'Mica',
    'set.bd2': 'Acrylic',
    'set.bd5': 'Pure Transparent',
    'set.bd4': 'Custom wallpaper',
    'set.wallpaper': 'Wallpaper image',
    'set.pick': 'Choose image',
    'set.reset': 'Reset to default',
    'set.transparentTip': 'Note: after switching light/dark mode with Pure Transparent, restart the app for correct colors.',
    'set.language': 'Language',
    'set.languageDesc': 'Applies immediately, no restart needed',
    'set.lang0': 'Follow system',
    'set.lang1': 'Simplified Chinese',
    'set.lang2': 'English',
    'set.fontSize': 'Interface font size',
    'set.fontSizeDesc': 'Uses 13px Small as the base; Medium is about 14px and Large about 16px. Changes apply immediately.',
    'set.font0': 'Small (default)',
    'set.font1': 'Medium',
    'set.font2': 'Large',
    'set.startup': 'Startup',
    'set.startupDialog': 'Show notice dialog on startup',
    'set.startupDialogDesc': 'Show the usage notice when opening; turn off to go straight to the main window',
    'set.rememberGames': 'Remember game list',
    'set.rememberGamesDesc': 'Turning off clears the saved game list',
    'set.autoUpdate': 'Check for updates on startup',
    'set.autoUpdateDesc': 'Reads the online version on startup and reports new versions',
    'set.software': 'Software',
    'set.installPath': 'Install location',
    'set.openInstallDir': 'Open folder',
    'set.installPathTip': 'Detected automatically on every launch; updates are extracted over this folder.',
    'set.gpu': 'GPU',
    'set.gpuSelect': 'Select GPU',
    'set.gpuTip': 'Usually auto-detected; if detection is wrong, pick the GPU manually',
    'set.backup': 'Backup management',
    'set.backupSize': 'Backup usage',
    'set.openBackup': 'Open backup folder',
    'set.clearBackup': 'Delete backups',
    'set.data': 'Data & logs',
    'set.dataPath': 'Game list data',
    'set.openDir': 'Open folder',
    'set.logPath': 'Runtime log',
    'set.openLog': 'Open log',
    'set.clearList': 'Clear list',
    'set.clearListDesc': 'Only clears the test-build list, the full version data is untouched.',
    'set.clearBtn': 'Clear',
    'set.exportLog': 'Export logs to Desktop',
    'set.exportLogDesc': 'Package runtime logs, settings and system information for support.',
    'set.exportLogBtn': 'Export logs',
    'set.version': 'Current version',
    'set.versionName': 'Version',
    'set.checkUpdate': 'Check for updates',
    'set.downloadLatest': 'Download latest version',
    'win.min': 'Minimize',
    'win.max': 'Maximize',
    'win.close': 'Close',
    'about.name': 'Frame Gen + DLSS5 Enabler',
    'about.devlang': 'Built with C# + WebView2 + hand-drawn HTML',
    'about.openSource': 'This software is open source and free. Welcome to Star and Fork!',
    'about.links': 'Author Links',
    'about.ghProfile': 'GitHub Profile',
    'about.ghRepo': 'GitHub Repository',
    'about.bilibili': 'Bilibili',
    'about.xiaoheihe': 'Xiaoheihe',
    'about.patchLinks': 'Patch Sources',
    'about.patchLinksIntro': 'Patches integrated into this software and their upstream repositories. Click to open:',
    'about.patchSmooth': 'Smooth Motion frame generation',
    'about.patchHsf': 'HSF frame generation',
    'about.patchSm86': 'SM86 frame generation',
    'about.patchMfg': 'MFG frame generation',
    'about.patchDlss5': 'DLSS5',
    'about.patchCapture': 'OptiCaptureHost external capture',
    'about.patchOptiScaler': 'OptiScaler upscaling / frame generation',
    'about.thanks': 'Patch Credits',
    'about.thanksIntro': 'The following community projects made this software possible:',
    'about.thanks1': '• Universal RTX 40 MFG Unlock (Community)',
    'about.thanks2': '• ReShade (Crosire)',
    'about.thanks3': '• RE Framework / DLSS5 on RE (Community)',
    'about.thanks4': '• GITC DLSS5 (ghostinthecamera)',
    'about.thanks5': '• DLSSG for SM86 / SM86 frame-gen patch (sdli1995, pipotoufikxyz-lgtm and community branches)',
    'about.thanks6': '• Smooth Motion Mod (ItsAdeline / NVSmooth30; RTXMFG SmoothMotion-next community build)',
    'about.thanks7': '• HSF 0.9.9 (Capt-Hunter)',
    'about.thanks8': '• MFG-Unlocker (Michael Robles / dashdogy, MIT License)',
    'about.thanks9': '• iMMERSE LaunchPad (Pascal Gilcher / Marty McFly; downloaded from the official GitHub under the author’s licence, not redistributed with this app)',
    'about.thanks10': '• OptiCaptureHost (MK-JH / tegraultra, GPL-3.0; external capture host)',
    'about.thanks11': '• OptiScaler (janblade / OptiScaler-F5-DLSSNR-Multipass, GPL-3.0; upscaling and frame generation)',
    'about.disclaimerTitle': 'Disclaimer',
    'about.disclaimer': 'This software is for learning and communication only, not for commercial use. Not recommended for online games or games with anti-cheat. This software modifies game files, which may result in account bans. We are not responsible for any bans.',
    'common.cancel': 'Cancel',
    'common.save': 'Save',
    'backup.title': 'Select backups to delete',
    'backup.desc': 'Deleted backups cannot be recovered. Make sure no game still relies on them.',
    'backup.all': 'Select all',
    'backup.delete': 'Delete selected',
    'sm86.reset': 'Restore defaults'
  };

  var PAGE_META = {
    zh: {
      home: ['主页', '本地库 · 一键开关 · 状态总览'],
      usage: ['使用说明', '完整指引 · 功能说明'],
      update: ['更新内容', '版本更新记录'],
      framegen: ['帧生成mod', 'Smooth Motion / HSF / 外置捕获 · 三选一进入'],
      smooth: ['Smooth Motion', '驱动 profile · 按游戏开关 · 20/30 Mod'],
      hsf: ['HSF 帧生成', 'ReShade 插件 · 内置安装 · 按游戏还原'],
      capture: ['外置捕获（实验性）', '不写游戏目录 · 图形捕获 · OptiScaler'],
      download: ['下载', 'GitHub 发行包 · HSF 组件 · 后台下载'],
      settings: ['设置', '外观、启动、显卡与备份'],
      about: ['关于', '版本与说明']
    },
    en: {
      home: ['Home', 'Library · one-click toggles · status overview'],
      usage: ['Usage Guide', 'Full guide · feature overview'],
      update: ['Update Log', 'Version history'],
      framegen: ['Frame Gen Mods', 'Smooth Motion / HSF / external capture · choose one'],
      smooth: ['Smooth Motion', 'Driver profile · per-game toggle · 20/30 Mod'],
      hsf: ['HSF Frame Gen', 'ReShade add-on · built-in install · per-game restore'],
      capture: ['External Capture (Experimental)', 'No game-folder writes · graphics capture · OptiScaler'],
      download: ['Downloads', 'GitHub releases · HSF components · background downloads'],
      settings: ['Settings', 'Appearance, startup, GPU and backups'],
      about: ['About', 'Version and notes']
    }
  };

  var SM86_TR = {
    '代理 DLL': 'Proxy DLL',
    '帧生成': 'Frame generation',
    '超分（仅 RTX 20/30 有效）': 'Super resolution (RTX 20/30 only)',
    'XeSS-FG（仅引擎为 XeSS 时）': 'XeSS-FG (only when engine is XeSS)',
    '日志': 'Logging',
    '帧生成总开关': 'Frame generation master switch',
    '插帧引擎': 'Frame generation engine',
    '生成帧数': 'Generated frames',
    '加速档位': 'Optimization level',
    '渲染预设': 'Render preset',
    '超分模式': 'Super resolution mode',
    '光流精度': 'Optical flow resolution',
    '无 HUD 画面要求': 'Require HUD-less frame',
    '日志级别': 'Log level',
    '记录每帧耗时（frame_timing.txt）': 'Log per-frame timing (frame_timing.txt)',
    '恢复出厂 dlssg_sm86.ini？当前配置会被覆盖。': 'Restore the factory dlssg_sm86.ini? The current config will be overwritten.',
    'version.dll - 默认，先试这个': 'version.dll - default, try this first',
    'winmm.dll - 第 1 备用': 'winmm.dll - fallback #1',
    'dbghelp.dll - 第 2 备用': 'dbghelp.dll - fallback #2',
    'dinput8.dll - 第 3（与 RE 框架冲突）': 'dinput8.dll - fallback #3 (conflicts with RE Framework)',
    'dxgi.dll - 第 4（与 ReShade 冲突）': 'dxgi.dll - fallback #4 (conflicts with ReShade)',
    'd3d12.dll - 最后（在渲染路径上）': 'd3d12.dll - last resort (in the render path)',
    '按 winmm → dbghelp → dinput8 → dxgi → d3d12 的顺序逐个试，一次只留一个。每次换完进游戏跑一会儿，看 dlssg_sm86\\logs 里有没有新行（日志级别要先设为 2）。': 'Try them in order winmm → dbghelp → dinput8 → dxgi → d3d12, keeping only one at a time. After each swap, run the game for a while and check dlssg_sm86\\logs for new lines (set log level to 2 first).',
    'RTX 40/50：本项目不改超分，改了也不起作用。': 'RTX 40/50: this project does not modify upscaling; changes will have no effect.',
    'XeSS-FG：此时 DLSSG 的加速档位与预设不起作用；后缓冲为 RGBA16F 的游戏会拒绝插帧。': 'XeSS-FG: DLSSG optimization level and preset are ignored; games with RGBA16F backbuffers will refuse frame generation.',
    'DLSS - NVIDIA 模型（默认）': 'DLSS - NVIDIA model (default)',
    'XeSS-FG（Intel model 15）': 'XeSS-FG (Intel model 15)',
    '2 倍': '2x',
    '3 倍': '3x',
    '4 倍（出厂默认）': '4x (factory default)',
    '6 倍（需游戏支持 6X）': '6x (game must support 6X)',
    '0 - 不加速（最慢，基准）': '0 - no optimization (slowest, baseline)',
    '1 - 与官方逐位一致（出厂）': '1 - bit-exact with official (factory)',
    '2 - 略快，不再逐位一致': '2 - slightly faster, no longer bit-exact',
    '3 - 最快，有损': '3 - fastest, lossy',
    'A - 默认': 'A - default',
    'B - 强制 UI 重组（多数游戏无效）': 'B - force UI recomposition (no effect in most games)',
    'C - 轻量帧生成（省 30-50% GPU 时间，画质略低）': 'C - lightweight frame gen (saves 30-50% GPU time, slightly lower quality)',
    '关闭（出厂默认）': 'Off (factory default)',
    'Coarse - 最粗最快': 'Coarse - coarsest and fastest',
    'Middle - 出厂默认': 'Middle - factory default',
    'Fine - 最细最慢': 'Fine - finest and slowest',
    'Auto - 游戏给不给都行': 'Auto - accept whatever the game provides',
    '1 - 一律要求（没有就报错退出）': '1 - always required (exit with error if missing)',
    '0 - 不记录': '0 - off',
    '1 - 只记错误（出厂默认）': '1 - errors only (factory default)',
    '2 - 详细（反馈问题时用）': '2 - verbose (for bug reports)'
  };

  function post(msg) {
    if (wv) {
      try { wv.postMessage(msg); } catch (e) { }
    }
  }

  function esc(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  function L(zh, en) {
    return state.english ? en : zh;
  }

  function tr(zh) {
    if (!state.english) return zh;
    return SM86_TR[zh] || zh;
  }

  function toast(text) {
    toastEl.textContent = text;
    toastEl.classList.add('show');
    if (toastTimer) clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove('show'); }, 2400);
  }

  // ---------------- 标题栏拖动 ----------------
  var titlebar = document.getElementById('titlebar');

  function isWindowButton(node) {
    while (node && node !== titlebar) {
      if (node.getAttribute && (node.getAttribute('data-win') || (node.classList && node.classList.contains('win-btn')))) {
        return true;
      }
      node = node.parentNode;
    }
    return false;
  }

  titlebar.addEventListener('mousedown', function (e) {
    if (e.button !== 0) return;
    if (isWindowButton(e.target)) return;
    post({ type: 'drag' });
  });

  titlebar.addEventListener('dblclick', function (e) {
    if (isWindowButton(e.target)) return;
    post({ type: 'maximize' });
  });

  Array.prototype.forEach.call(document.querySelectorAll('[data-win]'), function (btn) {
    btn.addEventListener('click', function () {
      post({ type: btn.getAttribute('data-win') });
    });
  });

  // ---------------- 语言 ----------------
  function updateAboutVer() {
    var v = state.version || '1.75';
    document.getElementById('aboutVer').textContent =
      L('版本：', 'Version: ') + 'V' + v + (state.isBeta ? ' · Beta' : '');
  }

  function applyLang(english) {
    state.english = !!english;
    document.documentElement.setAttribute('lang', state.english ? 'en' : 'zh-CN');

    Array.prototype.forEach.call(document.querySelectorAll('[data-i18n]'), function (el) {
      var key = el.getAttribute('data-i18n');
      if (el.getAttribute('data-zh') === null) {
        el.setAttribute('data-zh', el.textContent);
      }
      var zh = el.getAttribute('data-zh');
      el.textContent = state.english ? (EN[key] || zh) : zh;
    });

    Array.prototype.forEach.call(document.querySelectorAll('[data-i18n-ph]'), function (el) {
      var key = el.getAttribute('data-i18n-ph');
      if (el.getAttribute('data-zh-ph') === null) {
        el.setAttribute('data-zh-ph', el.getAttribute('placeholder') || '');
      }
      var zh = el.getAttribute('data-zh-ph');
      el.setAttribute('placeholder', state.english ? (EN[key] || zh) : zh);
    });

    Array.prototype.forEach.call(document.querySelectorAll('[data-i18n-title]'), function (el) {
      var key = el.getAttribute('data-i18n-title');
      var zh = el.getAttribute('data-title-zh') || el.getAttribute('title') || '';
      el.setAttribute('title', state.english ? (EN[key] || zh) : zh);
    });

    document.title = state.english ? 'Frame Gen + DLSS5 Enabler' : '多帧生成+DLSS5开启工具';

    var meta = PAGE_META[state.english ? 'en' : 'zh'][state.currentPage] || PAGE_META.zh.home;
    document.getElementById('pageTitle').textContent = meta[0];
    document.getElementById('pageSub').textContent = meta[1];
    updateAboutVer();
    render();
    renderDownload();
    renderSmooth();
    renderHsf();
    renderCapture();
    updateViewControl();
    renderSettingsMeta();
    refreshSelects();
  }

  // ---------------- 主题 / 材质 ----------------
  function applyFontSize() {
    document.documentElement.setAttribute('data-font-size', String(state.fontSize || 0));
  }

  function applyThemeAttributes() {
    var root = document.documentElement;
    root.setAttribute('data-theme', state.dark ? 'dark' : 'light');
    root.setAttribute('data-backdrop', String(state.backdrop));
    root.classList.toggle('theme-anim', !!state.anim);
    root.classList.toggle('no-anim', !state.anim);
    applyFontSize();
    if (state.wallpaper) {
      root.style.setProperty('--wallpaper', 'url("' + state.wallpaper + '")');
    } else {
      root.style.removeProperty('--wallpaper');
    }
  }

  function setDarkMode(on) {
    if (!state.anim || !document.startViewTransition) {
      state.dark = !!on;
      applyThemeAttributes();
      return;
    }

    var root = document.documentElement;
    root.classList.remove('theme-anim');
    try {
      var transition = document.startViewTransition(function () {
        state.dark = !!on;
        applyThemeAttributes();
        root.classList.remove('theme-anim');
      });
      var restore = function () {
        root.classList.toggle('theme-anim', !!state.anim && !root.classList.contains('no-anim'));
      };
      if (transition && transition.finished) {
        transition.finished.then(restore, restore);
      } else {
        setTimeout(restore, 320);
      }
    } catch (e) {
      state.dark = !!on;
      applyThemeAttributes();
    }
  }

  // ---------------- 列表 ----------------
  function updateViewControl() {
    if (!viewBtn) return;
    if (listEl) listEl.classList.toggle('big', !!state.bigView);
    if (viewGlyph) viewGlyph.textContent = state.bigView ? '☰' : '▦';
    if (viewLabel) viewLabel.textContent = state.bigView ? L('列表模式', 'List view') : L('大图模式', 'Poster view');
    viewBtn.title = state.bigView ? L('切换为列表模式', 'Switch to list view') : L('切换为大图模式', 'Switch to poster view');
  }

  function cardHtml(g) {
    var icon = g.icon ? '<img src="' + esc(g.icon) + '" alt="">' : '<div class="icon-fallback"></div>';
    var tags = g.tags ? '<div class="tags">' + esc(g.tags) + '</div>' : '';
    var frameLabel = g.frameOn ? L('关闭多帧', 'Turn off Frame Gen') : L('多帧生成', 'Frame Gen');
    var dlssLabel = g.dlssOn ? L('关闭DLSS5', 'Turn off DLSS5') : 'DLSS5';
    var canFrame = g.supportsFrameGen || g.frameOn;
    var anyOn = !!(g.frameOn || g.dlssOn || g.sm86);
    var statusClass = g.kind === 'on' ? 'on' : 'off';
    var statusText = esc(g.status);
    var frameAttrs = canFrame ? '' : ' disabled title="' + L('该游戏不支持多帧生成', 'This game does not support Frame Gen') + '"';
    var posterClass = g.poster ? 'poster' : 'poster no-poster';
    var posterImg = g.poster ? '<img class="poster-img" src="' + esc(g.poster) + '" alt="" loading="lazy" draggable="false">' : '';
    var posterFallback = g.icon
      ? '<img src="' + esc(g.icon) + '" alt="">'
      : '<div class="poster-initial">' + esc((g.name || '?').slice(0, 1)) + '</div>';
    return '<div class="game-card" data-exe="' + esc(g.exe) + '">' +
      '<div class="' + posterClass + '">' + posterImg +
      '<div class="poster-fallback">' + posterFallback + '</div>' +
      '<div class="poster-shade"></div>' +
      '<div class="poster-top"><span class="status ' + statusClass + '">' + statusText + '</span></div>' +
      '</div>' +
      '<div class="icon-box">' + icon + '</div>' +
      '<div class="meta"><div class="name">' + esc(g.name) + '</div>' +
      '<div class="path">' + esc(g.exe) + '</div>' + tags + '</div>' +
      '<div class="card-right">' +
      '<div class="status ' + statusClass + '">' + statusText + '</div>' +
      '<div class="ops">' +
      '<button class="mini' + (g.frameOn ? ' on' : '') + '"' + frameAttrs + ' data-op="frame" data-exe="' + esc(g.exe) + '">' + frameLabel + '</button>' +
      '<button class="mini' + (g.dlssOn ? ' dlss on' : ' dlss') + '" data-op="dlss5" data-exe="' + esc(g.exe) + '">' + dlssLabel + '</button>' +
      (g.sm86 ? '<button class="mini" data-op="sm86cfg" data-exe="' + esc(g.exe) + '">' + L('配置', 'Configure') + '</button>' : '') +
      '<button class="mini ghost' + (anyOn ? ' danger' : '') + '" data-op="restore" data-exe="' + esc(g.exe) + '">' + L('还原', 'Restore') + '</button>' +
      '</div></div>' +
      '</div>';
  }

  function render() {
    updateViewControl();
    var total = games.length;
    countEl.textContent = state.english
      ? (total + ' games')
      : ('共 ' + total + ' 个游戏');
    if (!total) {
      listEl.innerHTML = '<div class="empty">' + L('还没有添加游戏', 'No games added yet') + '</div>';
      return;
    }
    var html = '';
    for (var i = 0; i < total; i++) html += cardHtml(games[i]);
    listEl.innerHTML = html;
    Array.prototype.forEach.call(listEl.querySelectorAll('.poster-img'), function (img) {
      img.addEventListener('error', function () {
        var poster = img.parentNode;
        if (poster) poster.classList.add('no-poster');
        img.remove();
      });
    });
  }

  if (viewBtn) {
    viewBtn.addEventListener('click', function () {
      state.bigView = !state.bigView;
      try { localStorage.setItem('dfge.viewMode', state.bigView ? 'big' : 'list'); } catch (e) { }
      updateViewControl();
      render();
    });
  }

  // ---------------- 游戏卡片右键菜单 ----------------
  function hideContextMenu() {
    if (ctxMenu) ctxMenu.classList.add('hidden');
    ctxExe = '';
    ctxGame = null;
  }

  function ctxItem(label, op, disabled, danger) {
    return '<button type="button" class="ctx-item' + (danger ? ' danger' : '') +
      '" data-ctx-op="' + esc(op) + '"' + (disabled ? ' disabled' : '') + '>' +
      esc(label) + '</button>';
  }

  function showContextMenu(game, x, y) {
    if (!ctxMenu) return;
    ctxExe = game.exe || '';
    ctxGame = game;
    var anyOn = !!(game.frameOn || game.dlssOn || game.sm86);
    var items = [];
    items.push(ctxItem(
      game.frameOn ? L('一键还原多帧生成', 'Restore Frame Gen') : L('一键开启多帧生成', 'Enable Frame Gen'),
      'frame',
      !(game.supportsFrameGen || game.frameOn)
    ));
    items.push(ctxItem(
      game.dlssOn ? L('关闭DLSS5', 'Turn off DLSS5') : L('开启DLSS5', 'Enable DLSS5'),
      'dlss5'
    ));
    if (game.sm86) {
      items.push(ctxItem(L('配置多帧生成', 'Configure Frame Gen'), 'sm86cfg'));
    }
    items.push('<div class="ctx-sep"></div>');
    items.push(ctxItem(L('配置名修改', 'Config Name Modify'), 'configName', game.frameMode !== '高级模式'));
    items.push(ctxItem(L('排错修复', 'Troubleshoot Repair'), 'troubleshoot', !game.frameOn));
    items.push(ctxItem(L('编辑备注/标签', 'Edit Remark/Tags'), 'remark'));
    items.push('<div class="ctx-sep"></div>');
    items.push(ctxItem(L('一键还原', 'Restore All'), 'restore', !anyOn, anyOn));
    items.push('<div class="ctx-sep"></div>');
    items.push(ctxItem(L('启动游戏', 'Launch Game'), 'launch'));
    items.push(ctxItem(L('打开游戏目录', 'Open Game Folder'), 'openFolder'));
    items.push('<div class="ctx-sep"></div>');
    items.push(ctxItem(L('移除列表', 'Remove from List'), 'remove', false, true));
    ctxMenu.innerHTML = items.join('');
    ctxMenu.classList.remove('hidden');

    var rect = ctxMenu.getBoundingClientRect();
    var left = Math.min(Math.max(8, x), window.innerWidth - rect.width - 8);
    var top = Math.min(Math.max(8, y), window.innerHeight - rect.height - 8);
    ctxMenu.style.left = left + 'px';
    ctxMenu.style.top = top + 'px';
  }

  listEl.addEventListener('contextmenu', function (e) {
    var card = e.target && e.target.closest ? e.target.closest('.game-card') : null;
    if (!card) return;
    e.preventDefault();
    var exe = card.getAttribute('data-exe') || '';
    var game = null;
    for (var i = 0; i < games.length; i++) {
      if ((games[i].exe || '') === exe) {
        game = games[i];
        break;
      }
    }
    if (game) showContextMenu(game, e.clientX, e.clientY);
  });

  if (ctxMenu) {
    ctxMenu.addEventListener('click', function (e) {
      var item = e.target && e.target.closest ? e.target.closest('.ctx-item') : null;
      if (!item || item.disabled) return;
      var op = item.getAttribute('data-ctx-op');
      var exe = ctxExe;
      var game = ctxGame;
      hideContextMenu();
      if (!op || !exe) return;
      if (op === 'configName') {
        openConfigNameDialog(game, exe);
        return;
      }
      if (op === 'troubleshoot') {
        post({ type: 'troubleshootFiles', exe: exe });
        return;
      }
      if (op === 'remark') {
        openRemarkDialog(game, exe);
        return;
      }
      post({ type: 'action', op: op, exe: exe });
    });
  }

  document.addEventListener('mousedown', function (e) {
    if (ctxMenu && !ctxMenu.classList.contains('hidden') && !ctxMenu.contains(e.target)) {
      hideContextMenu();
    }
  });
  listEl.addEventListener('scroll', hideContextMenu);
  window.addEventListener('resize', hideContextMenu);
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') hideContextMenu();
  });

  listEl.addEventListener('click', function (e) {
    var target = e.target;
    while (target && target !== listEl && !(target.getAttribute && target.getAttribute('data-op'))) {
      target = target.parentNode;
    }
    if (!target || target === listEl || !target.getAttribute) return;
    post({
      type: 'action',
      op: target.getAttribute('data-op'),
      exe: target.getAttribute('data-exe')
    });
  });

  Array.prototype.forEach.call(document.querySelectorAll('[data-act]'), function (btn) {
    btn.addEventListener('click', function () {
      var act = btn.getAttribute('data-act');
      if (act === 'scan') { post({ type: 'scan' }); return; }
      if (act === 'add') { post({ type: 'addFolder' }); return; }
      if (act === 'addExe') { post({ type: 'addExe' }); return; }
      if (act === 'refresh') { post({ type: 'refresh' }); return; }
      if (act === 'detect') { post({ type: 'detect' }); return; }
      if (act === 'clear') {
        confirmDialog(
          L('清空游戏列表', 'Clear Game List'),
          L('确定清空游戏列表吗？只清空测试版列表，不影响正式版。', 'Clear the game list? Only the test-build list is cleared; the full version data is untouched.'),
          L('清空', 'Clear'),
          function () { post({ type: 'clear' }); }
        );
        return;
      }
      toast(L('该功能在正式版接入', 'This feature comes with the full version'));
    });
  });

  // ---------------- 导航 ----------------
  function animatePageEnter(pane) {
    if (!pane || !state.anim) return;
    pane.classList.remove('page-enter');
    void pane.offsetWidth;
    pane.classList.add('page-enter');
    setTimeout(function () { pane.classList.remove('page-enter'); }, 320);
  }

  function playHomeIntro() {
    if (!state.anim || state.currentPage !== 'home') return;
    var pane = document.getElementById('homePane');
    if (!pane || pane.classList.contains('hidden')) return;
    pane.classList.remove('home-intro');
    void pane.offsetWidth;
    pane.classList.add('home-intro');
    clearTimeout(playHomeIntro._timer);
    playHomeIntro._timer = setTimeout(function () {
      pane.classList.remove('home-intro');
    }, 760);
  }

  function fmtBytes(n) {
    n = Number(n) || 0;
    if (n <= 0) return '—';
    if (n >= 1073741824) return (n / 1073741824).toFixed(2) + ' GB';
    if (n >= 1048576) return (n / 1048576).toFixed(1) + ' MB';
    if (n >= 1024) return (n / 1024).toFixed(0) + ' KB';
    return n + ' B';
  }

  function downloadStatusText(status) {
    if (status === 'paused') return L('已暂停', 'Paused');
    if (status === 'completed') return L('下载完成', 'Completed');
    if (status === 'failed') return L('下载失败', 'Failed');
    if (status === 'cancelled') return L('已取消', 'Cancelled');
    return L('正在下载', 'Downloading');
  }

  function downloadFinished(status) {
    return status === 'completed' || status === 'failed' || status === 'cancelled';
  }

  function downloadItems() {
    var items = [];
    Object.keys(state.downloads || {}).forEach(function (id) {
      var d = state.downloads[id];
      if (!d) return;
      items.push(d);
    });
    return items;
  }

  function downloadProgressText(d) {
    var status = d.status || 'downloading';
    var total = Number(d.totalBytes) || 0;
    var done = Number(d.downloadedBytes) || 0;
    var pct = Math.max(0, Math.min(100, Number(d.percent) || 0));
    if (status !== 'downloading') return downloadStatusText(status);
    var speedText = Number(d.speed) > 0 ? fmtBytes(d.speed) + '/s · ' : '';
    if (total > 0) return speedText + pct.toFixed(1) + '%';
    return speedText + fmtBytes(done);
  }

  function downloadCardHtml(d) {
    var status = d.status || 'downloading';
    var id = d.id || 'release';
    var isHsf = d.kind === 'hsfLaunchPad';
    var total = Number(d.totalBytes) || 0;
    var done = Number(d.downloadedBytes) || 0;
    var pct = Math.max(0, Math.min(100, Number(d.percent) || 0));
    var name = d.fileName || 'release.zip';
    var path = d.targetPath || '';
    var pathLabel = isHsf ? L('安装到：', 'Install to: ') : L('下载到：', 'Downloaded to: ');
    var sizeText = total > 0 ? (fmtBytes(done) + ' / ' + fmtBytes(total)) : fmtBytes(done);
    var rightText = downloadProgressText(d);
    var barClass = total > 0 ? 'download-bar' : 'download-bar indeterminate';
    var toggleLabel = status === 'paused' ? L('继续下载', 'Resume') : L('暂停下载', 'Pause');
    var toggleBtn = (!isHsf && status !== 'completed' && status !== 'failed' && status !== 'cancelled')
      ? '<button type="button" class="mini" data-download-op="toggle">' + esc(toggleLabel) + '</button>'
      : '';
    var installBtn = (!isHsf && d.canInstall)
      ? '<button type="button" class="mini install" data-download-op="install">' + esc(L('安装更新', 'Install update')) + '</button>'
      : '';
    var secondBtn = downloadFinished(status)
      ? '<button type="button" class="mini ghost" data-download-op="clear">' + esc(L('移除记录', 'Remove')) + '</button>'
      : '<button type="button" class="mini danger" data-download-op="cancel">' + esc(L('取消下载', 'Cancel')) + '</button>';
    var openBtn = (!isHsf && path && status !== 'failed' && status !== 'cancelled')
      ? '<button type="button" class="mini" data-download-op="open">' + esc(L('打开下载位置', 'Open location')) + '</button>'
      : '';
    return '<div class="download-card ' + esc(status) + '" data-id="' + esc(id) + '" data-status="' + esc(status) + '">' +
      '<div class="download-icon">' + (isHsf ? 'HSF' : 'ZIP') + '</div>' +
      '<div class="download-main">' +
        '<div class="download-name">' + esc(name) + '</div>' +
        '<div class="download-path">' + esc(pathLabel) + esc(path) + '</div>' +
        '<div class="download-progress"><div class="' + barClass + '" style="width:' + (total > 0 ? pct.toFixed(1) : 0) + '%"></div></div>' +
        '<div class="download-meta"><span>' + esc(sizeText) + '</span><span>' + esc(rightText) + '</span></div>' +
      '</div>' +
      '<div class="download-actions">' + installBtn + toggleBtn + secondBtn + openBtn + '</div>' +
    '</div>';
  }

  function updateDownloadCard(card, d) {
    var total = Number(d.totalBytes) || 0;
    var done = Number(d.downloadedBytes) || 0;
    var pct = Math.max(0, Math.min(100, Number(d.percent) || 0));
    var isHsf = d.kind === 'hsfLaunchPad';
    var sizeText = total > 0 ? (fmtBytes(done) + ' / ' + fmtBytes(total)) : fmtBytes(done);
    var rightText = downloadProgressText(d);
    var bar = card.querySelector('.download-bar');
    var name = card.querySelector('.download-name');
    var path = card.querySelector('.download-path');
    var meta = card.querySelector('.download-meta');
    if (bar) {
      bar.classList.toggle('indeterminate', total <= 0);
      bar.style.width = total > 0 ? pct.toFixed(1) + '%' : '0%';
    }
    if (name) name.textContent = d.fileName || 'release.zip';
    if (path) path.textContent = (isHsf ? L('安装到：', 'Install to: ') : L('下载到：', 'Downloaded to: ')) + (d.targetPath || '');
    if (meta && meta.children.length >= 2) {
      meta.children[0].textContent = sizeText;
      meta.children[1].textContent = rightText;
    }
  }

  function renderDownload() {
    var items = downloadItems();
    var empty = document.getElementById('downloadEmpty');
    var list = document.getElementById('downloadList');
    var dot = document.getElementById('downloadNavDot');
    if (empty) empty.classList.toggle('hidden', items.length > 0);
    if (list) {
      var cards = list.querySelectorAll('.download-card');
      var rebuild = cards.length !== items.length;
      if (!rebuild) {
        for (var i = 0; i < items.length; i++) {
          var card = cards[i];
          var d = items[i];
          if (!card || card.getAttribute('data-id') !== (d.id || 'release') ||
              card.getAttribute('data-status') !== (d.status || 'downloading')) {
            rebuild = true;
            break;
          }
        }
      }
      if (rebuild) {
        list.innerHTML = items.map(downloadCardHtml).join('');
      } else {
        for (var j = 0; j < items.length; j++) {
          updateDownloadCard(cards[j], items[j]);
        }
      }
    }
    var activeCount = items.filter(function (d) {
      return d.status === 'downloading' || d.status === 'paused';
    }).length;
    if (dot) {
      dot.classList.toggle('hidden', activeCount === 0);
      dot.textContent = String(activeCount);
    }
  }

  // ---------------- Smooth Motion ----------------

  var smoothData = null;

  function formatBytes(n) {
    n = Number(n) || 0;
    if (n >= 1024 * 1024) return (n / (1024 * 1024)).toFixed(1) + ' MB';
    if (n >= 1024) return (n / 1024).toFixed(1) + ' KB';
    return n + ' B';
  }

  function smApiText(mask) {
    var parts = [];
    if (mask & 1) parts.push('DX12');
    if (mask & 2) parts.push('DX11');
    if (mask & 4) parts.push('Vulkan');
    return parts.length ? parts.join(' + ') : '—';
  }

  function findSmoothGame(exe) {
    if (!smoothData || !smoothData.games) return null;
    for (var i = 0; i < smoothData.games.length; i++) {
      if (smoothData.games[i].exe === exe) return smoothData.games[i];
    }
    return null;
  }

  function renderSmoothGames(games) {
    var list = document.getElementById('smGameList');
    var empty = document.getElementById('smGameEmpty');
    if (!list) return;
    if (!games || !games.length) {
      list.innerHTML = '';
      if (empty) empty.classList.remove('hidden');
      return;
    }
    if (empty) empty.classList.add('hidden');

    var html = '';
    games.forEach(function (g) {
      var enabled = !!g.enabled;
      var found = !!g.profileFound;
      var api = Number(g.apiMask) || 7;
      var apiOptions = [[7, 'DX12 + DX11 + Vulkan'], [1, 'DX12'], [2, 'DX11'], [4, 'Vulkan']];
      var opts = '';
      apiOptions.forEach(function (o) {
        opts += '<option value="' + o[0] + '"' + (api === o[0] ? ' selected' : '') + '>' + esc(o[1]) + '</option>';
      });
      var statusClass = enabled ? 'on' : 'off';
      var statusText = enabled ? L('已开启', 'On') : (found ? L('未开启', 'Off') : L('无 profile', 'No profile'));
      var modText = '';
      if (g.modInstalled) modText = '<span class="sm-mini ok">' + L('Mod 已部署', 'Mod installed') + '</span>';
      else if (g.modConflict) modText = '<span class="sm-mini warn">' + L('version.dll 冲突', 'version.dll conflict') + '</span>';

      html += '<div class="sm-game" data-exe="' + esc(g.exe) + '">';
      html += '<div class="sm-game-main">';
      html += '<div class="sm-game-name">' + esc(g.name || g.exe) + (g.profile ? '<span class="sm-profile">' + esc(g.profile) + '</span>' : '') + '</div>';
      html += '<div class="sm-game-sub">' + esc(g.exe) + '</div>';
      if (g.error) html += '<div class="sm-error">' + esc(g.error) + '</div>';
      html += modText;
      html += '</div>';
      html += '<div class="sm-game-right">';
      html += '<span class="sm-status ' + statusClass + '">' + statusText + '</span>';
      html += '<span class="sm-api-label">' + L('APIs', 'APIs') + '</span>';
      html += '<select class="set-select sm-api" data-sm-api="' + esc(g.exe) + '">' + opts + '</select>';
      html += '<button class="sm-switch' + (enabled ? ' on' : '') + '" data-sm-toggle="' + esc(g.exe) + '" data-on="' + (enabled ? '0' : '1') + '" title="' + esc(enabled ? L('关闭', 'Turn off') : L('开启', 'Turn on')) + '"><i></i></button>';
      html += '<button class="sm-reset" data-sm-reset="' + esc(g.exe) + '">' + L('恢复默认', 'Reset') + '</button>';
      html += '</div></div>';
    });
    list.innerHTML = html;

    Array.prototype.forEach.call(list.querySelectorAll('select.sm-api'), function (sel) {
      if (sel.__customized) return;
      setupSelect(sel);
    });
  }

  function renderSmoothMod(data) {
    var mod = (data && data.mod) || {};
    var gpu = (data && data.gpu) || {};
    var statusEl = document.getElementById('smModStatus');
    if (statusEl) {
      if (mod.rtx20Ready && mod.rtx30Ready) {
        statusEl.className = 'sm-mod-status ok';
        statusEl.innerHTML = '<b>' + L('内置补丁已就绪', 'Built-in patches ready') + '</b> · RTX20 ' +
          esc(formatBytes(mod.rtx20Size)) + ' · SHA256 ' + esc(String(mod.rtx20Sha256 || '').slice(0, 10)) + '…' +
          ' · RTX30 ' + esc(formatBytes(mod.rtx30Size)) + ' · SHA256 ' + esc(String(mod.rtx30Sha256 || '').slice(0, 10)) + '…' +
          (mod.installedCount ? ' · ' + esc(String(mod.installedCount)) + L(' 个游戏已部署', ' game(s) deployed') : '');
      } else if (mod.rtx20Ready || mod.rtx30Ready) {
        statusEl.className = 'sm-mod-status warn';
        statusEl.innerHTML = '<b>' + L('内置补丁不完整', 'Built-in patches incomplete') + '</b> · ' +
          esc(mod.error || L('缺少一个引擎的 DLL', 'One engine DLL is missing'));
      } else {
        statusEl.className = 'sm-mod-status bad';
        statusEl.textContent = mod.error || L('内置补丁缺失', 'Built-in patches missing');
      }
    }

    var supportEl = document.getElementById('smModSupport');
    if (supportEl) {
      supportEl.className = 'sm-mod-warn';
      if (gpu.official) {
        supportEl.textContent = L('当前显卡是 ' + (gpu.series || 'RTX 40/50') + '：官方驱动已支持，不需要安装 RTX20/30 补丁，部署按钮已禁用。',
          'This GPU is ' + (gpu.series || 'RTX 40/50') + ': official driver support, the RTX20/30 patch is not needed. Deploy is disabled.');
      } else if (gpu.needsMod) {
        supportEl.textContent = L('当前显卡是 ' + (gpu.series || '') + '：部署时会自动使用 ' + (mod.engine || '') + ' 引擎。',
          'This GPU is ' + (gpu.series || '') + ': the ' + (mod.engine || '') + ' engine will be used automatically.');
      } else {
        supportEl.textContent = L('当前显卡不是 NVIDIA RTX 20/30，RTX20/30 补丁不可用。',
          'This GPU is not an NVIDIA RTX 20/30; the RTX20/30 patch is unavailable.');
      }
    }

    var list = document.getElementById('smModList');
    var empty = document.getElementById('smModEmpty');
    if (!list) return;
    var games = (data && data.games) || [];
    if (!games.length) {
      list.innerHTML = '';
      if (empty) empty.classList.remove('hidden');
      return;
    }
    if (empty) empty.classList.add('hidden');

    var supported = !!gpu.needsMod;
    var html = '';
    games.forEach(function (g) {
      var installed = !!g.modInstalled;
      var conflict = !!g.modConflict;
      var wrong = !!g.modWrongEngine;
      var engine = String(g.modEngine || '');
      var expected = String(g.modExpectedEngine || '');
      var label;
      var cls = '';
      if (installed && wrong) {
        label = L('已装 ' + engine + '，当前需要 ' + expected, 'Installed ' + engine + ', needs ' + expected);
        cls = 'warn';
      } else if (installed) {
        label = L('已部署 ' + engine, 'Installed ' + engine);
        cls = 'ok';
      } else if (conflict) {
        label = L('version.dll 冲突', 'version.dll conflict');
        cls = 'warn';
      } else if (gpu.official) {
        label = L('官方支持，不需要补丁', 'Official support, no patch needed');
      } else if (!supported) {
        label = L('当前显卡不支持', 'Unsupported GPU');
      } else {
        label = L('未部署', 'Not installed');
      }

      html += '<div class="sm-game" data-exe="' + esc(g.exe) + '">';
      html += '<div class="sm-game-main"><div class="sm-game-name">' + esc(g.name || g.exe) + '</div>';
      html += '<div class="sm-game-sub">' + esc(g.exe) + '</div>';
      var detail = conflict && g.modMessage ? ' · ' + esc(g.modMessage) : '';
      html += '<div class="sm-mod-msg ' + cls + '">' + esc(label) + detail + '</div></div>';
      html += '<div class="sm-game-right">';
      if (installed) {
        html += '<button class="pill danger" data-sm-restore="' + esc(g.exe) + '">' + L('还原', 'Restore') + '</button>';
      } else if (supported) {
        html += '<button class="pill primary" data-sm-deploy="' + esc(g.exe) + '"' + (conflict ? ' disabled' : '') + '>' + L('部署', 'Deploy') + '</button>';
      } else {
        html += '<button class="pill" disabled>' + (gpu.official ? L('不需要', 'Not needed') : L('不支持', 'Unsupported')) + '</button>';
      }
      html += '</div></div>';
    });
    list.innerHTML = html;
  }

  function syncSmoothTab() {
    var tab = state.smoothTab === 'mod' ? 'mod' : 'official';
    Array.prototype.forEach.call(document.querySelectorAll('.sm-tab'), function (btn) {
      btn.classList.toggle('active', btn.getAttribute('data-sm-tab') === tab);
    });
    var official = document.getElementById('smOfficialPanel');
    var mod = document.getElementById('smModPanel');
    if (official) official.classList.toggle('hidden', tab !== 'official');
    if (mod) mod.classList.toggle('hidden', tab !== 'mod');
  }

  function renderSmooth() {
    var badge = document.getElementById('smSupportBadge');
    if (!smoothData) {
      if (badge) {
        badge.className = 'sm-badge';
        badge.textContent = L('检测中…', 'Checking…');
      }
      return;
    }

    var gpu = smoothData.gpu || {};
    var driver = smoothData.driver || {};
    var games = smoothData.games || [];

    var gpuEl = document.getElementById('smGpu');
    if (gpuEl) gpuEl.textContent = gpu.name || '—';
    var drvEl = document.getElementById('smDriver');
    if (drvEl) {
      drvEl.textContent = driver.available
        ? (driver.version || '—') + (driver.branch ? ' · ' + driver.branch : '')
        : L('NVAPI 不可用', 'NVAPI unavailable');
    }
    var baseEl = document.getElementById('smBase');
    if (baseEl) {
      baseEl.textContent = driver.supported
        ? (driver.baseEnabled ? L('已开启', 'On') : L('已关闭', 'Off'))
        : L('不支持', 'Unsupported');
    }
    var profileEl = document.getElementById('smProfileHint');
    if (profileEl) {
      var foundCount = games.filter(function (g) { return g.profileFound; }).length;
      profileEl.textContent = foundCount + ' / ' + games.length;
    }

    if (badge) {
      badge.className = 'sm-badge';
      if (gpu.official && driver.available) {
        badge.classList.add('ok');
        badge.textContent = L('官方支持', 'Official support');
      } else if (gpu.needsMod) {
        badge.classList.add('warn');
        badge.textContent = L('需要 20/30 Mod', 'Needs 20/30 Mod');
      } else if (!driver.available) {
        badge.classList.add('bad');
        badge.textContent = L('NVAPI 不可用', 'NVAPI unavailable');
      } else {
        badge.classList.add('bad');
        badge.textContent = L('不支持', 'Unsupported');
      }
    }

    var note = document.getElementById('smSupportNote');
    if (note) {
      if (!driver.available) {
        note.textContent = driver.error || L('没有检测到可用的 NVIDIA 驱动接口。', 'No usable NVIDIA driver API detected.');
      } else if (gpu.official) {
        note.textContent = L('RTX 40/50 官方已支持 Smooth Motion：直接在本页写入 per-game profile 即可，只插 1 帧（2x）。',
          'RTX 40/50 officially support Smooth Motion: write the per-game profile on this page directly. It inserts 1 frame (2x).');
      } else if (gpu.needsMod) {
        note.textContent = L('RTX 20/30 只写驱动 profile 还不够：补丁已内置在 Patches\\SmoothMotion，切到「RTX 20/30 补丁」标签直接部署即可。RTX20 支持 DX11/DX12，RTX30 支持 DX11/DX12/Vulkan。',
          'RTX 20/30: the driver profile alone is not enough. The patch is built in under Patches\\SmoothMotion; switch to the RTX 20/30 Patch tab and deploy directly. RTX20 supports DX11/DX12; RTX30 supports DX11/DX12/Vulkan.');
      } else {
        note.textContent = L('当前显卡不是 NVIDIA RTX 20/30/40/50，Smooth Motion 不可用。',
          'This GPU is not an NVIDIA RTX 20/30/40/50; Smooth Motion is unavailable.');
      }
    }

    renderSmoothGames(games);
    renderSmoothMod(smoothData);
    syncSmoothTab();
  }

  function setupSmoothEvents() {
    var refreshBtn = document.getElementById('smRefreshBtn');
    if (refreshBtn) refreshBtn.addEventListener('click', function () { post({ type: 'smoothRefresh' }); });
    var openLogBtn = document.getElementById('smOpenLogBtn');
    if (openLogBtn) openLogBtn.addEventListener('click', function () { post({ type: 'smoothOpenLog' }); });
    var openModBtn = document.getElementById('smOpenModBtn');
    if (openModBtn) openModBtn.addEventListener('click', function () { post({ type: 'smoothOpenMod' }); });
    var openModLogBtn = document.getElementById('smOpenModLogBtn');
    if (openModLogBtn) openModLogBtn.addEventListener('click', function () { post({ type: 'smoothOpenModLog' }); });

    Array.prototype.forEach.call(document.querySelectorAll('.sm-tab'), function (btn) {
      btn.addEventListener('click', function () {
        state.smoothTab = btn.getAttribute('data-sm-tab') === 'mod' ? 'mod' : 'official';
        syncSmoothTab();
      });
    });

    var gameList = document.getElementById('smGameList');
    if (gameList) {
      gameList.addEventListener('click', function (e) {
        var toggle = e.target.closest ? e.target.closest('[data-sm-toggle]') : null;
        if (toggle) {
          var exe = toggle.getAttribute('data-sm-toggle');
          var on = toggle.getAttribute('data-on') === '1';
          var row = toggle.closest('.sm-game');
          var sel = row ? row.querySelector('select.sm-api') : null;
          var mask = sel ? Number(sel.value) : 7;
          if (!mask) mask = 7;
          toggle.disabled = true;
          post({ type: 'smoothApply', exe: exe, enabled: on, apis: mask });
          return;
        }
        var reset = e.target.closest ? e.target.closest('[data-sm-reset]') : null;
        if (reset) {
          post({ type: 'smoothReset', exe: reset.getAttribute('data-sm-reset') });
        }
      });
      gameList.addEventListener('change', function (e) {
        var sel = e.target.closest ? e.target.closest('select.sm-api') : null;
        if (!sel) return;
        var exe = sel.getAttribute('data-sm-api');
        var g = findSmoothGame(exe);
        if (!g) return;
        var mask = Number(sel.value) || 7;
        if (g.enabled) {
          post({ type: 'smoothApply', exe: exe, enabled: true, apis: mask });
        } else {
          g.apiMask = mask;
        }
      });
    }

    var modList = document.getElementById('smModList');
    if (modList) {
      modList.addEventListener('click', function (e) {
        var deploy = e.target.closest ? e.target.closest('[data-sm-deploy]') : null;
        if (deploy) {
          deploy.disabled = true;
          post({ type: 'smoothDeployMod', exe: deploy.getAttribute('data-sm-deploy') });
          return;
        }
        var restore = e.target.closest ? e.target.closest('[data-sm-restore]') : null;
        if (restore) {
          restore.disabled = true;
          post({ type: 'smoothRestoreMod', exe: restore.getAttribute('data-sm-restore') });
        }
      });
    }
  }

  // ---------------- HSF ----------------

  var hsfData = null;

  function findHsfGame(exe) {
    if (!hsfData || !hsfData.games) return null;
    for (var i = 0; i < hsfData.games.length; i++) {
      if (hsfData.games[i].exe === exe) return hsfData.games[i];
    }
    return null;
  }

  function renderHsf() {
    var badge = document.getElementById('hsfBadge');
    if (!badge) return;

    if (!hsfData) {
      badge.className = 'sm-badge';
      badge.textContent = L('检测中…', 'Checking…');
      var loadingIds = ['hsfPackage', 'hsfGpu', 'hsfAddon', 'hsfReShade'];
      loadingIds.forEach(function (id) {
        var el = document.getElementById(id);
        if (el) el.textContent = '—';
      });
      var loadingNote = document.getElementById('hsfSupportNote');
      if (loadingNote) loadingNote.textContent = '';
      return;
    }

    var pack = (hsfData && hsfData.package) || {};
    var gpu = (hsfData && hsfData.gpu) || {};
    var games = (hsfData && hsfData.games) || [];

    var packEl = document.getElementById('hsfPackage');
    if (packEl) {
      packEl.textContent = pack.ready
        ? ('HSF ' + (pack.version || '0.9.9') + ' · ' + L('就绪', 'Ready'))
        : L('缺失', 'Missing');
    }
    var gpuEl = document.getElementById('hsfGpu');
    if (gpuEl) gpuEl.textContent = gpu.name || '—';
    var addonEl = document.getElementById('hsfAddon');
    if (addonEl) {
      if (pack.addon64Ready || pack.addon32Ready) {
        var parts = [];
        if (pack.addon64Ready) parts.push('64 ' + formatBytes(pack.addon64Size));
        if (pack.addon32Ready) parts.push('32 ' + formatBytes(pack.addon32Size));
        addonEl.textContent = parts.join(' / ');
      } else {
        addonEl.textContent = L('未内置', 'Not bundled');
      }
    }
    var reshadeEl = document.getElementById('hsfReShade');
    if (reshadeEl) {
      reshadeEl.textContent = (pack.reshade64Ready && pack.reshade32Ready)
        ? L('6.8 add-on 已内置', '6.8 add-on bundled')
        : L('缺少基础 DLL', 'Base DLL missing');
    }

    if (badge) {
      badge.className = 'sm-badge';
      if (!pack.ready) {
        badge.classList.add('bad');
        badge.textContent = L('内置包缺失', 'Pack missing');
      } else if (!gpu.supported) {
        badge.classList.add('bad');
        badge.textContent = L('显卡不支持', 'Unsupported GPU');
      } else if (gpu.official) {
        badge.classList.add('ok');
        badge.textContent = L('可直接使用', 'Ready');
      } else if (gpu.needsUnlocker) {
        badge.classList.add('warn');
        badge.textContent = L('3X+ 需解锁', 'Unlock 3X+');
      } else {
        badge.classList.add('warn');
        badge.textContent = L('需要解锁', 'Needs unlock');
      }
    }

    var note = document.getElementById('hsfSupportNote');
    if (note) {
      if (!pack.ready) {
        note.textContent = pack.error || L('内置 HSF 文件不完整，请重新安装完整软件包。', 'The built-in HSF pack is incomplete. Reinstall the full package.');
      } else if (!gpu.supported) {
        note.textContent = L('HSF 需要 NVIDIA RTX 20/30/40/50；当前显卡是 ' + (gpu.name || '未知') + '。',
          'HSF needs an NVIDIA RTX 20/30/40/50; current GPU: ' + (gpu.name || 'unknown') + '.');
      } else if (gpu.official) {
        note.textContent = L('RTX 50 原生支持高倍率；安装后进游戏按 Home，在 Add-ons 里启用。',
          'RTX 50 supports high multipliers natively. After installing, press Home in game and enable it under Add-ons.');
      } else {
        note.textContent = L('RTX 20/30/40 原生通常只有 2X；点游戏卡片里的「安装 MFG-Unlocker」即可解锁 3X-6X（Vulkan / DX11 走 helper，DX12 原生路线按官方说明放到 exe 旁）。',
          'RTX 20/30/40 are normally limited to 2X. Click “Install MFG-Unlocker” on a game card to unlock 3X-6X (Vulkan / DX11 via the helper; for native DX12, place it next to the exe as the official guide says).');
      }
    }

    var langSelect = document.getElementById('hsfMenuLanguage');
    if (langSelect) {
      var menuMode = typeof hsfData.menuLanguage === 'number' ? hsfData.menuLanguage : 0;
      langSelect.value = String(menuMode);
      if (langSelect.__sync) langSelect.__sync();
    }

    renderHsfGames(games, pack);
  }

  function renderHsfGames(games, pack) {
    var list = document.getElementById('hsfGameList');
    var empty = document.getElementById('hsfGameEmpty');
    if (!list) return;
    if (!games || !games.length) {
      list.innerHTML = '';
      if (empty) empty.classList.remove('hidden');
      return;
    }
    if (empty) empty.classList.add('hidden');

    var html = '';
    games.forEach(function (g) {
      var installed = !!g.installed;
      var partial = !!g.partial;
      var conflict = !!g.conflict;
      var canInstall = !!g.canInstall && !!pack.ready;
      var cls = 'off';
      var label = L('未安装', 'Not installed');
      if (installed) {
        cls = 'ok';
        label = L('已安装 HSF ' + (g.version || '0.9.9'), 'HSF installed');
      } else if (partial) {
        cls = 'warn';
        label = L('文件不完整', 'Incomplete files');
      } else if (conflict) {
        cls = 'bad';
        label = L('无法安装', 'Cannot install');
      }

      html += '<div class="sm-game" data-exe="' + esc(g.exe) + '">';
      html += '<div class="sm-game-main">';
      html += '<div class="sm-game-name">' + esc(g.name || g.exe) + '</div>';
      html += '<div class="sm-game-sub">' + esc(g.exe) + '</div>';
      html += '<div class="hsf-chips">';
      html += '<span class="sm-mini">' + esc(g.arch || '—') + '</span>';
      html += '<span class="sm-mini">' + esc(g.api || '—') + '</span>';
      var installDir = g.installDir || '';
      var installDirIsBin = /[\\\/]bin[\\\/]?$/i.test(installDir);
      var installDirLabel = installDirIsBin ? 'bin' : L('根目录', 'Game root');
      html += '<span class="sm-mini' + (g.sourceEngine ? ' ok' : '') + '" title="' + esc(installDir) + '">' +
        esc(L('安装目录：', 'Install dir: ') + (installDirLabel || '—')) + '</span>';
      if (g.reshadePresent) {
        html += '<span class="sm-mini ' + (g.reshadeReady ? 'ok' : 'warn') + '">' +
          L('ReShade ', 'ReShade ') + esc(g.reshadeVersion || g.reshadeProxy || '') + '</span>';
      } else {
        html += '<span class="sm-mini">' + L('将装内置 ReShade', 'Built-in ReShade will be installed') + '</span>';
      }
      if (installed) {
        var flowName = g.flowSource || '';
        html += '<span class="sm-mini ' + (flowName ? 'ok' : 'warn') + '">' +
          (flowName ? esc(L('光流：', 'Flow: ') + flowName) : L('光流：未安装', 'Flow: not installed')) + '</span>';
        html += '<span class="sm-mini ' + (g.mfgUnlockerInstalled ? 'ok' : '') + '">' +
          (g.mfgUnlockerInstalled ? L('倍率：MFG-Unlocker', 'Multiplier: MFG-Unlocker') : L('倍率：2X 原生', 'Multiplier: 2X native')) + '</span>';
        var installedLang = g.installedLanguage === 'en-US'
          ? L('英文原版菜单', 'English menu')
          : (g.installedLanguage === 'zh-CN' ? L('中文菜单', 'Chinese menu') : L('菜单语言未知', 'Menu language unknown'));
        html += '<span class="sm-mini ' + (g.languageMismatch ? 'warn' : '') + '">' + esc(installedLang) + '</span>';
      } else {
        var mode = g.installMode || 'auto';
        html += '<select class="hsf-dir-select" data-hsf-dir="' + esc(g.exe) + '" title="' +
          esc(L('选择 HSF 安装目录', 'Choose HSF install directory')) + '">' +
          '<option value="auto"' + (mode === 'auto' ? ' selected' : '') + '>' + esc(L('自动识别', 'Auto')) + '</option>' +
          '<option value="root"' + (mode === 'root' ? ' selected' : '') + '>' + esc(L('游戏根目录', 'Game root')) + '</option>' +
          '<option value="bin"' + (mode === 'bin' ? ' selected' : '') + '>' + esc(L('bin 子目录', 'bin folder')) + '</option>' +
          '</select>';
      }
      html += '</div>';
      if (g.message) {
        html += '<div class="sm-mod-msg ' + cls + '">' + esc(g.message) + '</div>';
      }
      html += '</div>';
      html += '<div class="sm-game-right">';
      html += '<span class="sm-status ' + cls + '">' + esc(label) + '</span>';
      if (installed) {
        var targetLang = g.installedLanguage === 'en-US' ? 'zh-CN' : 'en-US';
        var switchLabel = targetLang === 'en-US'
          ? L('切换为英文原版', 'Switch to English')
          : L('切换为中文版', 'Switch to Chinese');
        html += '<button class="pill primary" data-hsf-switch="' + esc(g.exe) + '" data-hsf-switch-lang="' + targetLang + '">' +
          esc(switchLabel) + '</button>';
        if (!g.flowSource && g.canInstallLaunchPad) {
          html += '<button class="pill primary" data-hsf-launchpad="' + esc(g.exe) + '">' +
            L('安装 LaunchPad（推荐）', 'Install LaunchPad (recommended)') + '</button>';
        }
        if (!g.mfgUnlockerInstalled && g.canInstallMfg) {
          html += '<button class="pill primary" data-hsf-mfg="' + esc(g.exe) + '">' +
            L('安装 MFG-Unlocker', 'Install MFG-Unlocker') + '</button>';
        }
        html += '<button class="pill danger" data-hsf-restore="' + esc(g.exe) + '">' + L('还原', 'Restore') + '</button>';
      } else {
        html += '<button class="pill primary" data-hsf-install="' + esc(g.exe) + '"' + (canInstall ? '' : ' disabled') + '>' +
          L('安装 HSF', 'Install HSF') + '</button>';
      }
      html += '<button class="pill" data-hsf-open-game="' + esc(g.exe) + '">' + L('游戏目录', 'Game folder') + '</button>';
      if (installed || partial) {
        html += '<button class="pill" data-hsf-open-log="' + esc(g.exe) + '">' + L('ReShade 日志', 'ReShade log') + '</button>';
      }
      html += '</div></div>';
    });
    list.innerHTML = html;
  }

  function setupHsfEvents() {
    var refreshBtn = document.getElementById('hsfRefreshBtn');
    if (refreshBtn) refreshBtn.addEventListener('click', function () { post({ type: 'hsfRefresh' }); });
    var patchBtn = document.getElementById('hsfOpenPatchBtn');
    if (patchBtn) patchBtn.addEventListener('click', function () { post({ type: 'hsfOpenPatch' }); });
    var docsBtn = document.getElementById('hsfOpenDocsBtn');
    if (docsBtn) docsBtn.addEventListener('click', function () { post({ type: 'hsfOpenDocs' }); });

    var langSelect = document.getElementById('hsfMenuLanguage');
    if (langSelect) {
      langSelect.addEventListener('change', function () {
        var mode = parseInt(langSelect.value, 10);
        post({ type: 'setSetting', key: 'hsfLanguage', value: isNaN(mode) ? 0 : mode });
      });
    }

    var list = document.getElementById('hsfGameList');
    if (!list) return;
    list.addEventListener('change', function (e) {
      var dirSelect = e.target.closest ? e.target.closest('[data-hsf-dir]') : null;
      if (!dirSelect) return;
      post({
        type: 'hsfSetInstallMode',
        exe: dirSelect.getAttribute('data-hsf-dir'),
        mode: dirSelect.value
      });
    });
    list.addEventListener('click', function (e) {
      var switchLang = e.target.closest ? e.target.closest('[data-hsf-switch]') : null;
      if (switchLang) {
        switchLang.disabled = true;
        post({
          type: 'hsfSwitchLanguage',
          exe: switchLang.getAttribute('data-hsf-switch'),
          lang: switchLang.getAttribute('data-hsf-switch-lang')
        });
        return;
      }
      var install = e.target.closest ? e.target.closest('[data-hsf-install]') : null;
      if (install) {
        install.disabled = true;
        post({ type: 'hsfInstall', exe: install.getAttribute('data-hsf-install') });
        return;
      }
      var launchPad = e.target.closest ? e.target.closest('[data-hsf-launchpad]') : null;
      if (launchPad) {
        launchPad.disabled = true;
        post({ type: 'hsfInstallLaunchPad', exe: launchPad.getAttribute('data-hsf-launchpad') });
        return;
      }
      var mfg = e.target.closest ? e.target.closest('[data-hsf-mfg]') : null;
      if (mfg) {
        mfg.disabled = true;
        post({ type: 'hsfInstallMfg', exe: mfg.getAttribute('data-hsf-mfg') });
        return;
      }
      var restore = e.target.closest ? e.target.closest('[data-hsf-restore]') : null;
      if (restore) {
        restore.disabled = true;
        post({ type: 'hsfRestore', exe: restore.getAttribute('data-hsf-restore') });
        return;
      }
      var openGame = e.target.closest ? e.target.closest('[data-hsf-open-game]') : null;
      if (openGame) {
        post({ type: 'hsfOpenGame', exe: openGame.getAttribute('data-hsf-open-game') });
        return;
      }
      var openLog = e.target.closest ? e.target.closest('[data-hsf-open-log]') : null;
      if (openLog) {
        post({ type: 'hsfOpenLog', exe: openLog.getAttribute('data-hsf-open-log') });
      }
    });
  }

  // ---------------- 页面切换 ----------------

  var PAGE_PANES = {
    home: 'homePane',
    usage: 'pageUsage',
    update: 'pageUpdate',
    framegen: 'pageFrameGen',
    smooth: 'pageSmooth',
    hsf: 'pageHsf',
    capture: 'pageCapture',
    download: 'pageDownload',
    settings: 'pageSettings',
    about: 'pageAbout'
  };
  var navItems = document.querySelectorAll('.nav-item');

  function activatePage(page) {
    if (!PAGE_PANES[page]) page = 'home';
    state.currentPage = page;
    var navPage = (page === 'smooth' || page === 'hsf' || page === 'capture') ? 'framegen' : page;
    Array.prototype.forEach.call(document.querySelectorAll('.nav-item'), function (i) {
      i.classList.toggle('active', i.getAttribute('data-page') === navPage);
    });
    Object.keys(PAGE_PANES).forEach(function (key) {
      var pane = document.getElementById(PAGE_PANES[key]);
      if (pane) pane.classList.toggle('hidden', key !== page);
    });
    if (page === 'home') {
      playHomeIntro();
    } else {
      var pane = document.getElementById(PAGE_PANES[page]);
      if (pane) animatePageEnter(pane);
    }
    var meta = PAGE_META[state.english ? 'en' : 'zh'][page] || PAGE_META.zh.home;
    document.getElementById('pageTitle').textContent = meta[0];
    document.getElementById('pageSub').textContent = meta[1];
    renderDownload();
    if (page === 'smooth') {
      renderSmooth();
      post({ type: 'smoothRefresh' });
    }
    if (page === 'hsf') {
      renderHsf();
      post({ type: 'hsfRefresh' });
    }
    if (page === 'capture') {
      captureFormInitialized = false;
      renderCapture();
      post({ type: 'captureRefresh', windows: true });
    }
  }

  Array.prototype.forEach.call(navItems, function (item) {
    item.addEventListener('click', function () {
      activatePage(item.getAttribute('data-page') || 'home');
    });
  });

  Array.prototype.forEach.call(document.querySelectorAll('[data-fg-open]'), function (item) {
    item.addEventListener('click', function () {
      activatePage(item.getAttribute('data-fg-open') || 'framegen');
    });
  });

  Array.prototype.forEach.call(document.querySelectorAll('[data-fg-back]'), function (item) {
    item.addEventListener('click', function () {
      activatePage('framegen');
    });
  });

  Array.prototype.forEach.call(document.querySelectorAll('[data-set]'), function (btn) {
    btn.addEventListener('click', function () {
      post({ type: 'openPath', target: btn.getAttribute('data-set') });
    });
  });

  (function () {
    var noticeOk = document.getElementById('btnNoticeOk');
    if (!noticeOk) return;
    noticeOk.addEventListener('click', function () {
      var bar = document.getElementById('noticeBar');
      if (bar) bar.style.display = 'none';
    });
  })();

  Array.prototype.forEach.call(document.querySelectorAll('.about-link'), function (btn) {
    btn.addEventListener('click', function () {
      post({ type: 'openUrl', url: btn.getAttribute('data-url') });
    });
  });

  setupSmoothEvents();
  setupHsfEvents();
  setupCaptureEvents();

  function captureNumber(value, digits) {
    return (typeof value === 'number' && isFinite(value)) ? value.toFixed(digits) : '—';
  }

  function setCaptureSelect(id, value) {
    var el = document.getElementById(id);
    if (!el) return;
    el.value = String(value);
    if (typeof el.__sync === 'function') el.__sync();
  }

  function captureConfigFromForm() {
    var vsync = document.getElementById('captureVsyncToggle');
    var depth = document.getElementById('captureDepthToggle');
    return {
      frameGenMode: parseInt(document.getElementById('captureFgMode').value, 10) || 0,
      mfgCount: parseInt(document.getElementById('captureMfg').value, 10) || 0,
      upscalerMode: parseInt(document.getElementById('captureUpscaler').value, 10) || 0,
      upscale: parseInt(document.getElementById('captureUpscale').value, 10) || 0,
      upscaleLevel: parseInt(document.getElementById('captureUpscaleLevel').value, 10) || 0,
      presentMode: parseInt(document.getElementById('capturePresent').value, 10) || 0,
      boost: parseInt(document.getElementById('captureBoost').value, 10) || 0,
      presentDelay: parseInt(document.getElementById('capturePresentDelay').value, 10) || 0,
      depthSource: (depth && depth.classList.contains('on')) ? 1 : 0,
      vsync: !!(vsync && vsync.classList.contains('on'))
    };
  }

  function captureFocusLabel(value) {
    if (value === 0) return L('游戏焦点', 'Game focus');
    if (value === 1) return L('覆盖层焦点', 'Overlay focus');
    if (value === 2) return L('其他窗口', 'Other window');
    return '—';
  }

  function captureWindowDesc(w) {
    if (!w) return '';
    var exe = String(w.exe || '').split(/[\\/]/).pop();
    var parts = [];
    if (w.process) parts.push(w.process);
    if (exe) parts.push(exe);
    if (w.width && w.height) parts.push(w.width + '×' + w.height);
    if (w.minimized) parts.push(L('已最小化', 'Minimized'));
    return parts.join(' · ');
  }

  function updateCaptureWindowPickerUI(selected) {
    var icon = document.getElementById('captureWindowPickIcon');
    var title = document.getElementById('captureWindowPickTitle');
    var desc = document.getElementById('captureWindowPickDesc');
    if (!title || !desc) return;
    if (icon) {
      icon.innerHTML = '';
      if (selected && selected.icon) {
        var img = document.createElement('img');
        img.src = selected.icon;
        img.alt = '';
        icon.appendChild(img);
      } else {
        icon.textContent = '▣';
      }
    }
    if (!selected) {
      title.textContent = L('选择游戏窗口', 'Choose a game window');
      title.setAttribute('data-i18n', 'capture.chooseWindow');
      desc.textContent = L('点击打开窗口列表', 'Click to open the window list');
      return;
    }
    title.removeAttribute('data-i18n');
    title.textContent = selected.title || selected.process || L('游戏窗口', 'Game window');
    desc.textContent = captureWindowDesc(selected);
  }

  function syncCaptureWindowPicker(input, options) {
    if (!input) return;
    var keep = input.value;
    var selected = null;
    if (keep) {
      selected = options.find(function (w) { return String(w.hwnd) === String(keep); }) || null;
      if (!selected) input.value = '';
    }
    updateCaptureWindowPickerUI(selected);
  }

  function updateCaptureButtons() {
    var d = captureData || {};
    var start = document.getElementById('captureStartBtn');
    var stop = document.getElementById('captureStopBtn');
    var menu = document.getElementById('captureMenuBtn');
    var focus = document.getElementById('captureFocusBtn');
    var overlay = document.getElementById('captureOverlayBtn');
    var apply = document.getElementById('captureApplyBtn');
    var win = document.getElementById('captureWindowSelect');
    var pick = document.getElementById('captureWindowPickBtn');
    var running = !!d.running;
    if (start) start.disabled = running || !d.engineExists || !win || !win.value;
    if (pick) pick.disabled = running;
    if (stop) stop.disabled = !running;
    if (menu) menu.disabled = !running;
    if (focus) focus.disabled = !running;
    if (overlay) {
      overlay.disabled = !running;
      overlay.textContent = d.overlayHidden
        ? L('恢复覆盖层', 'Show overlay')
        : L('隐藏覆盖层 / 切出窗口', 'Hide overlay / switch out');
    }
    if (apply) apply.disabled = !d.engineExists;
  }

  function renderCapture() {
    var d = captureData;
    if (!d) return;
    var badge = document.getElementById('captureBadge');
    var engine = document.getElementById('captureEngine');
    var winTitle = document.getElementById('captureWindowTitle');
    var fpsIn = document.getElementById('captureFpsIn');
    var fpsOut = document.getElementById('captureFpsOut');
    var note = document.getElementById('captureNote');
    var hint = document.getElementById('captureCapHint');
    var lastError = document.getElementById('captureLastError');

    if (badge) {
      badge.textContent = d.running ? L('运行中', 'Running') : L('未启动', 'Not running');
      badge.className = 'sm-badge' + (d.running ? ' ok' : '');
    }
    if (engine) {
      engine.textContent = d.engineExists
        ? (d.engineVersion ? 'v' + d.engineVersion : L('已内置', 'Bundled'))
        : L('未内置', 'Not installed');
    }
    if (winTitle) winTitle.textContent = d.windowTitle || '—';
    if (fpsIn) fpsIn.textContent = d.statusFresh ? captureNumber(d.captureFps, 1) : '—';
    var presentedFps = d.statusFresh ? (Number(d.dxgiFps) || Number(d.presentFps) || 0) : 0;
    if (fpsOut) fpsOut.textContent = d.statusFresh ? captureNumber(presentedFps, 1) : '—';

    var statCapture = document.getElementById('captureStatCapture');
    var statPresent = document.getElementById('captureStatPresent');
    var statDxgi = document.getElementById('captureStatDxgi');
    var statPresentMs = document.getElementById('captureStatPresentMs');
    var statFocus = document.getElementById('captureStatFocus');
    var statDepth = document.getElementById('captureStatDepth');
    var statRefresh = document.getElementById('captureStatRefresh');
    if (statCapture) statCapture.textContent = d.statusFresh ? captureNumber(d.captureFps, 1) : '—';
    if (statPresent) statPresent.textContent = d.statusFresh ? captureNumber(d.presentFps, 1) : '—';
    if (statDxgi) statDxgi.textContent = d.statusFresh ? captureNumber(d.dxgiFps, 1) : '—';
    if (statPresentMs) statPresentMs.textContent = d.statusFresh ? captureNumber(d.presentMs, 2) + ' ms' : '—';
    if (statFocus) statFocus.textContent = d.statusFresh ? captureFocusLabel(d.focus) : '—';
    if (statDepth) {
      var depthState = d.statusFresh ? (Number(d.depthState) || 0) : -1;
      var depthSize = (Number(d.depthWidth) || 0) > 0 ? (' ' + d.depthWidth + '×' + d.depthHeight) : '';
      var depthText = '—';
      if (depthState === 0) depthText = L('未使用（平面深度）', 'Off (flat depth)');
      else if (depthState === 1) depthText = L('等待 ReShade 插件', 'Waiting for add-on');
      else if (depthState === 2) depthText = L('接收中', 'Receiving') + depthSize;
      else if (depthState === 3) depthText = L('尺寸不匹配', 'Size mismatch') + depthSize;
      else if (depthState === 4) depthText = L('无法打开共享深度', 'Cannot open shared depth');
      else if (depthState === 5) depthText = L('插件没有新画面', 'No new frames from add-on');
      statDepth.textContent = depthText;
    }
    if (statRefresh) statRefresh.textContent = d.statusFresh ? captureNumber(d.refreshHz, 0) + ' Hz' : '—';

    if (note) {
      if (!d.engineExists) {
        note.textContent = L('没有找到内置 CaptureHost。请确认 CaptureHost\\OptiCaptureHost.exe 和 OptiScaler.ini 在软件目录中。',
          'Bundled CaptureHost was not found. Make sure CaptureHost\\OptiCaptureHost.exe and OptiScaler.ini exist in the app folder.');
      } else if (d.running && d.statusFresh) {
        var baseFps = Number(d.presentFps) || 0;
        var submitFps = Number(d.dxgiFps) || baseFps;
        var ratio = baseFps > 0 ? submitFps / baseFps : 0;
        var ratioText = ratio >= 1.15 ? '（约 ' + ratio.toFixed(1) + 'X）' : '';
        var ratioTextEn = ratio >= 1.15 ? ' (~' + ratio.toFixed(1) + 'X)' : '';
        note.textContent = L('正在外置捕获：游戏画面 ' + captureNumber(d.captureFps, 1) + ' FPS → 补帧提交 ' + captureNumber(submitFps, 1) + ' FPS' + ratioText + '。游戏内按 Ctrl+Shift+F10 打开菜单，按 Ctrl+Shift+F12 停止。',
          'External capture running: ' + captureNumber(d.captureFps, 1) + ' FPS in → ' + captureNumber(submitFps, 1) + ' FPS presented' + ratioTextEn + '. Ctrl+Shift+F10 opens the menu; Ctrl+Shift+F12 stops.');
      } else if (d.running) {
        note.textContent = L('Host 已启动，等待 OptiCaptureHost.status 出现第一帧数据…',
          'Host is running, waiting for the first frame in OptiCaptureHost.status…');
      } else {
        note.textContent = L('先选择一个正在运行的游戏窗口，再点“开始捕获”。本页不会启动 OptiCaptureLauncher，也不会往游戏目录写任何文件。',
          'Pick a running game window, then press Start. This page never opens OptiCaptureLauncher and never writes into the game folder.');
      }
    }

    if (hint) {
      if (d.statusFresh && d.capHint === 1) {
        hint.style.display = '';
        hint.textContent = L('检测到游戏帧率被外部限制器限制到约 ' + captureNumber(d.capFps, 0) + ' FPS。请关闭游戏内垂直同步或 RTSS 限帧后再试。',
          'The game appears limited to about ' + captureNumber(d.capFps, 0) + ' FPS. Disable in-game VSync or the RTSS frame limiter and try again.');
      } else {
        hint.style.display = 'none';
        hint.textContent = '';
      }
    }

    if (lastError) {
      if (d.lastError) {
        lastError.style.display = '';
        lastError.textContent = L('最近日志：', 'Recent log: ') + String(d.lastError).slice(-1400);
        lastError.style.whiteSpace = 'pre-wrap';
      } else {
        lastError.style.display = 'none';
        lastError.textContent = '';
      }
    }

    var windowsSelect = document.getElementById('captureWindowSelect');
    if (windowsSelect && d.windowsIncluded) {
      var options = d.windows || [];
      captureWindowOptions = options;
      var keep = windowsSelect.value;
      if (keep && !options.some(function (w) { return String(w.hwnd) === String(keep); })) {
        windowsSelect.value = '';
      }
      syncCaptureWindowPicker(windowsSelect, options);
    }

    if (!captureFormInitialized && d.config) {
      setCaptureSelect('captureFgMode', d.config.frameGenMode);
      setCaptureSelect('captureMfg', d.config.mfgCount);
      setCaptureSelect('captureUpscaler', d.config.upscalerMode);
      setCaptureSelect('captureUpscale', d.config.upscale);
      setCaptureSelect('captureUpscaleLevel', d.config.upscaleLevel);
      setCaptureSelect('capturePresent', d.config.presentMode);
      setCaptureSelect('captureBoost', d.config.boost);
      setCaptureSelect('capturePresentDelay', d.config.presentDelay);
      var vsync = document.getElementById('captureVsyncToggle');
      if (vsync) vsync.classList.toggle('on', !!d.config.vsync);
      var depth = document.getElementById('captureDepthToggle');
      if (depth) depth.classList.toggle('on', !!d.config.depthSource);
      captureFormInitialized = true;
    }

    updateCaptureButtons();
  }

  function setupCaptureEvents() {
    var refresh = document.getElementById('captureRefreshBtn');
    if (refresh) refresh.addEventListener('click', function () {
      post({ type: 'captureRefresh', windows: true });
    });

    var start = document.getElementById('captureStartBtn');
    if (start) start.addEventListener('click', function () {
      var win = document.getElementById('captureWindowSelect');
      if (!win || !win.value) {
        toast(L('请先选择一个游戏窗口', 'Choose a game window first'));
        return;
      }
      var msg = captureConfigFromForm();
      msg.type = 'captureStart';
      msg.hwnd = win.value;
      captureFormInitialized = false;
      post(msg);
    });

    var stop = document.getElementById('captureStopBtn');
    if (stop) stop.addEventListener('click', function () {
      post({ type: 'captureStop' });
    });

    var menu = document.getElementById('captureMenuBtn');
    if (menu) menu.addEventListener('click', function () {
      post({ type: 'captureMenu' });
    });

    var focus = document.getElementById('captureFocusBtn');
    if (focus) focus.addEventListener('click', function () {
      post({ type: 'captureFocus' });
    });

    var overlay = document.getElementById('captureOverlayBtn');
    if (overlay) overlay.addEventListener('click', function () {
      post({ type: 'captureOverlay' });
    });

    var apply = document.getElementById('captureApplyBtn');
    if (apply) apply.addEventListener('click', function () {
      var msg = captureConfigFromForm();
      msg.type = 'captureApply';
      captureFormInitialized = true;
      post(msg);
    });

    var basePriority = document.getElementById('captureBasePriorityBtn');
    if (basePriority) basePriority.addEventListener('click', function () {
      setCaptureSelect('captureFgMode', 9);
      setCaptureSelect('captureMfg', 1); // 2X
      setCaptureSelect('capturePresent', 6);
      setCaptureSelect('captureBoost', 3);
      setCaptureSelect('capturePresentDelay', 0);
      captureFormInitialized = true;
      var msg = captureConfigFromForm();
      msg.type = 'captureApply';
      post(msg);
      toast(L('已切到原始帧优先：F5Low + 2X + 0 ms。',
        'Source-frame priority applied: F5Low + 2X + 0 ms.'));
    });

    // 帧生成模式和倍率不是热切换项。运行中改下拉框时自动重启捕获，
    // 否则用户会以为“换哪个模式都一样”，实际一直跑的是旧模式。
    // 不再强制把倍率改回 3X：旧深度测试的真实配置就是 F5Low + 4X。
    ['captureFgMode', 'captureMfg'].forEach(function (id) {
      var el = document.getElementById(id);
      if (!el) return;
      el.addEventListener('change', function () {
        var mode = document.getElementById('captureFgMode');
        var mfg = document.getElementById('captureMfg');
        var value = parseInt(el.value, 10);
        var modeValue = mode ? parseInt(mode.value, 10) : 9;
        var mfgValue = mfg ? parseInt(mfg.value, 10) : 3;
        if (id === 'captureFgMode') {
          if (value === 9) {
            toast(L('F5Low 的基础帧 ≈ 补帧输出 ÷ 倍率。3X 输出约 122 fps 时基础帧约 40.5；换模式不会改倍率。要更高基础帧请选 2X 或“原始帧优先”。',
              'F5Low base FPS is roughly presented FPS ÷ multiplier. At about 122 fps presented, 3X means about 40.5 base; switching mode does not change the multiplier. Choose 2X or Source-frame priority for a higher base.'));
          } else if (value === 0) {
            toast(L('XeFG 同样受倍率影响：倍率越高，基础帧越低；所有外置模式共用同一个输出上限。',
              'XeFG is also divided by the multiplier: a higher multiplier means a lower base rate. Every external mode shares the same GPU output ceiling.'));
          }
        } else if (id === 'captureMfg' && value >= 3) {
          if (modeValue === 9) {
            toast(L('3X/4X 会明显降低基础帧：基础帧 ≈ 输出 ÷ 倍率。165Hz 屏幕上优先试 2X；4X 只适合很轻的游戏。',
              '3X/4X visibly lowers the base rate: base ≈ presented ÷ multiplier. On a 165 Hz panel try 2X first; 4X is only for very light games.'));
          } else {
            toast(L('当前不是 F5Low：4X/5X/6X 在这条路径上会明显压低原始帧；所有外置模式都受输出上限和倍率影响。',
              'The current mode is not F5Low: 4X/5X/6X lowers the base rate heavily here; every external mode is limited by the output ceiling and multiplier.'));
          }
        }
        captureFormInitialized = true;
        if (captureData && captureData.running) {
          var msg = captureConfigFromForm();
          msg.type = 'captureApply';
          post(msg);
          toast(L('设置已保存，正在按新模式重启捕获…', 'Saved. Restarting capture with the new mode…'));
        }
      });
    });

    var openLog = document.getElementById('captureOpenLogBtn');
    if (openLog) openLog.addEventListener('click', function () {
      post({ type: 'captureOpenLog' });
    });

    var openEngine = document.getElementById('captureOpenEngineBtn');
    if (openEngine) openEngine.addEventListener('click', function () {
      post({ type: 'openPath', target: 'capture' });
    });

    var vsync = document.getElementById('captureVsyncToggle');
    if (vsync) vsync.addEventListener('click', function () {
      vsync.classList.toggle('on');
      captureFormInitialized = true;
    });

    var depthToggle = document.getElementById('captureDepthToggle');
    if (depthToggle) depthToggle.addEventListener('click', function () {
      depthToggle.classList.toggle('on');
      if (depthToggle.classList.contains('on')) {
        var upscale = document.getElementById('captureUpscale');
        var level = document.getElementById('captureUpscaleLevel');
        if (upscale && upscale.value === '0') upscale.value = '4';
        if (level && level.value === '3') level.value = '0';
        toast(L('已开启 ReShade 深度：请先在游戏目录安装 ReShade 深度插件，并开启输出缩放；保存后需停止再开始捕获。',
          'ReShade depth enabled: install the add-on in the game folder and turn on output scaling first. Save, then stop and restart capture.'));
      }
      captureFormInitialized = true;
    });

    var pick = document.getElementById('captureWindowPickBtn');
    if (pick) pick.addEventListener('click', function () {
      if (captureData && captureData.running) return;
      post({ type: 'capturePickWindow' });
    });

    var winSelect = document.getElementById('captureWindowSelect');
    if (winSelect) winSelect.addEventListener('change', function () {
      captureFormInitialized = true;
      updateCaptureButtons();
    });

    ['captureFgMode', 'captureMfg', 'captureUpscaler', 'captureUpscale', 'captureUpscaleLevel',
      'capturePresent', 'captureBoost', 'capturePresentDelay'].forEach(function (id) {
      var el = document.getElementById(id);
      if (el) el.addEventListener('change', function () { captureFormInitialized = true; });
    });

    setInterval(function () {
      if (state.currentPage === 'capture' && !document.hidden) {
        post({ type: 'captureRefresh', windows: false });
      }
    }, 1400);
  }

  // ---------------- 设置 ----------------
  function setSwitch(key, on) {
    var el = document.querySelector('.sw[data-setkey="' + key + '"]');
    if (el) el.classList.toggle('on', !!on);
  }

  Array.prototype.forEach.call(document.querySelectorAll('.sw[data-setkey]'), function (btn) {
    btn.addEventListener('click', function () {
      var key = btn.getAttribute('data-setkey');
      var on = !btn.classList.contains('on');
      btn.classList.toggle('on', on);
      if (key === 'dark') { setDarkMode(on); }
      if (key === 'anim') { state.anim = on; applyThemeAttributes(); }
      post({ type: 'setSetting', key: key, value: on });
    });
  });

  var setBackdrop = document.getElementById('setBackdrop');
  var rowWallpaper = document.getElementById('rowWallpaper');
  var tipTransparent = document.getElementById('tipTransparent');
  var setWallpaperPath = document.getElementById('setWallpaperPath');
  var setLang = document.getElementById('setLang');
  var setFontSize = document.getElementById('setFontSize');
  var setGpu = document.getElementById('setGpu');
  var setBackupSize = document.getElementById('setBackupSize');

  // ---------------- 自定义下拉（统一风格） ----------------
  function closeAllSelects(except) {
    Array.prototype.forEach.call(document.querySelectorAll('.sel.open'), function (w) {
      if (w !== except) {
        w.classList.remove('open');
        if (w.__menu) w.__menu.classList.remove('show');
      }
    });
  }

  function setupSelect(sel) {
    if (sel.__customized) return;
    sel.__customized = true;

    var wrap = document.createElement('div');
    wrap.className = 'sel';
    if (sel.classList.contains('cfg-select')) wrap.classList.add('sel-cfg');
    if (sel.classList.contains('tool-select')) wrap.classList.add('sel-tool');
    sel.parentNode.insertBefore(wrap, sel);
    wrap.appendChild(sel);
    sel.classList.add('sel-native');

    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'sel-btn';
    btn.innerHTML = '<span class="sel-label"></span><span class="sel-caret"></span>';
    var menu = document.createElement('div');
    menu.className = 'sel-menu';
    document.body.appendChild(menu);
    wrap.appendChild(btn);
    wrap.__menu = menu;

    function sync() {
      var opt = sel.options[sel.selectedIndex];
      btn.querySelector('.sel-label').textContent = opt ? opt.textContent : '—';
      var cur = sel.value;
      Array.prototype.forEach.call(menu.children, function (b) {
        b.classList.toggle('active', b.getAttribute('data-value') === cur);
      });
      btn.disabled = !!sel.disabled;
    }

    function rebuild() {
      menu.innerHTML = '';
      for (var i = 0; i < sel.options.length; i++) {
        (function (o) {
          var item = document.createElement('button');
          item.type = 'button';
          item.className = 'sel-opt';
          item.setAttribute('data-value', o.value);
          item.textContent = o.textContent;
          item.addEventListener('click', function (e) {
            e.stopPropagation();
            sel.value = o.value;
            sel.dispatchEvent(new Event('change', { bubbles: true }));
            sync();
            closeMenu();
          });
          menu.appendChild(item);
        })(sel.options[i]);
      }
      sync();
    }

    function closeMenu() {
      wrap.classList.remove('open');
      menu.classList.remove('show');
    }

    function openMenu() {
      closeAllSelects(wrap);
      wrap.classList.add('open');
      menu.classList.add('show');

      var rect = btn.getBoundingClientRect();
      menu.style.minWidth = rect.width + 'px';
      menu.style.left = 'auto';
      menu.style.right = (window.innerWidth - rect.right) + 'px';
      menu.style.top = '0px';
      menu.style.bottom = 'auto';
      var h = menu.offsetHeight;
      if (window.innerHeight - rect.bottom - 10 >= h) {
        menu.style.top = (rect.bottom + 6) + 'px';
      } else {
        menu.style.top = 'auto';
        menu.style.bottom = (window.innerHeight - rect.top + 6) + 'px';
      }
    }

    btn.addEventListener('click', function (e) {
      e.stopPropagation();
      if (wrap.classList.contains('open')) {
        closeMenu();
      } else {
        openMenu();
      }
    });

    // 菜单挂在 body 上，点击滚动条/菜单空白处不能触发全局“点击外部关闭”。
    menu.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    menu.addEventListener('click', function (e) { e.stopPropagation(); });
    // 手动滚动菜单，避免滚轮在到达边界后把滚动传给弹窗，触发全局 scroll 关闭。
    menu.addEventListener('wheel', function (e) {
      e.stopPropagation();
      if (e.deltaY === 0) return;
      e.preventDefault();
      var unit = e.deltaMode === 1 ? 18 : (e.deltaMode === 2 ? menu.clientHeight : 1);
      menu.scrollTop += e.deltaY * unit;
    }, { passive: false });

    sel.addEventListener('change', sync);

    if (window.MutationObserver) {
      new MutationObserver(function () { rebuild(); }).observe(sel, {
        childList: true, subtree: true, characterData: true
      });
    }

    window.addEventListener('resize', function () { closeAllSelects(null); });
    window.addEventListener('scroll', function (e) {
      // 任意下拉菜单内部的滚动都不能让其它下拉的 scroll 监听误判为“页面滚动”。
      if (e.target && e.target.closest && e.target.closest('.sel-menu')) return;
      closeAllSelects(null);
    }, true);

    sel.__rebuild = rebuild;
    sel.__sync = sync;
    sel.__closeMenu = closeMenu;
    sel.__openMenu = openMenu;
    rebuild();
  }

  function setupAllSelects(root) {
    Array.prototype.forEach.call(
      (root || document).querySelectorAll('.set-select, .cfg-select, .tool-select'),
      setupSelect
    );
  }

  function teardownSelects(root) {
    Array.prototype.forEach.call(root.querySelectorAll('select.sel-native'), function (sel) {
      if (sel.__menu && sel.__menu.parentNode) sel.__menu.parentNode.removeChild(sel.__menu);
      sel.__menu = null;
      sel.__customized = false;
    });
  }

  function refreshSelects() {
    Array.prototype.forEach.call(document.querySelectorAll('select.sel-native'), function (sel) {
      if (sel.__rebuild) sel.__rebuild();
    });
  }

  setupAllSelects(document);
  document.addEventListener('click', function (e) {
    // 菜单内部（含滚动条）的点击已在 setupSelect 中 stopPropagation；这里再兜底一次，
    // 避免 body 级菜单被误判为“点击外部”而关闭。
    if (e.target && e.target.closest && e.target.closest('.sel-menu')) return;
    closeAllSelects(null);
  });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') closeAllSelects(null);
  });

  function syncBackdropRows() {
    var v = parseInt(setBackdrop.value, 10);
    rowWallpaper.classList.toggle('hidden', v !== 4);
    tipTransparent.classList.toggle('hidden', v !== 5);
  }

  setBackdrop.addEventListener('change', function () {
    var v = parseInt(setBackdrop.value, 10);
    state.backdrop = v;
    applyThemeAttributes();
    syncBackdropRows();
    post({ type: 'setSetting', key: 'backdrop', value: v });
  });

  document.getElementById('btnPickWallpaper').addEventListener('click', function () {
    post({ type: 'wallpaperPick' });
  });

  document.getElementById('btnResetWallpaper').addEventListener('click', function () {
    post({ type: 'wallpaperReset' });
  });

  setLang.addEventListener('change', function () {
    var v = parseInt(setLang.value, 10);
    state.lang = v;
    var english = v === 2 || (v === 0 && !/^zh/i.test(navigator.language || 'en'));
    applyLang(english);
    post({ type: 'setSetting', key: 'language', value: v });
  });

  setFontSize.addEventListener('change', function () {
    var v = parseInt(setFontSize.value, 10);
    state.fontSize = v === 1 || v === 2 ? v : 0;
    applyFontSize();
    post({ type: 'setSetting', key: 'fontSize', value: state.fontSize });
  });

  setGpu.addEventListener('change', function () {
    var name = setGpu.selectedIndex <= 0 ? '' : setGpu.value;
    state.gpuSelected = name;
    post({ type: 'setSetting', key: 'gpu', value: name });
  });

  function renderSettingsMeta() {
    setBackupSize.textContent = state.english
      ? ('(' + state.backup.sizeMb + ' MB / 1024 MB)')
      : ('(' + state.backup.sizeMb + 'MB/1024MB)');
    setWallpaperPath.textContent = state.wallpaper
      ? L('已设置（点「选择图片」可更换）', 'Set (choose an image to replace)')
      : L('尚未设置', 'Not set');
    setFontSize.value = String(state.fontSize || 0);
    setSwitch('dark', state.dark);
    setSwitch('anim', state.anim);
    setSwitch('startupDialog', state.startupDialog);
    setSwitch('rememberGames', state.rememberGames);
    setSwitch('autoUpdate', state.autoUpdate);
    var installPathEl = document.getElementById('setInstallPath');
    if (installPathEl) installPathEl.textContent = state.installPath || '—';
  }

  function renderSettings(s) {
    state.dark = !!s.dark;
    state.anim = !!s.anim;
    state.backdrop = parseInt(s.backdrop, 10) || 0;
    state.wallpaper = s.wallpaper || '';
    state.lang = parseInt(s.language, 10) || 0;
    state.fontSize = parseInt(s.fontSize, 10) || 0;
    state.startupDialog = !!s.startupDialog;
    state.rememberGames = !!s.rememberGames;
    state.autoUpdate = !!s.autoUpdate;
    state.gpuSelected = s.gpuSelected || '';
    state.backup = s.backup || { sizeMb: 0, dirs: [] };

    applyThemeAttributes();
    syncBackdropRows();
    setBackdrop.value = String(state.backdrop === 3 ? 0 : state.backdrop);
    setLang.value = String(state.lang);

    // GPU 列表
    var gpus = s.gpus || [];
    var html = '<option value="">' + L('自动检测', 'Auto detect') + '</option>';
    for (var i = 0; i < gpus.length; i++) {
      var g = gpus[i];
      html += '<option value="' + esc(g.name) + '">' + esc(g.name) + (g.integrated ? ' (' + L('核显', 'integrated') + ')' : '') + '</option>';
    }
    setGpu.innerHTML = html;
    if (state.gpuSelected) {
      setGpu.value = state.gpuSelected;
      if (setGpu.selectedIndex < 0) setGpu.selectedIndex = 0;
    } else {
      setGpu.selectedIndex = 0;
    }

    renderSettingsMeta();
    refreshSelects();
  }

  // 备份删除弹窗
  var backupMask = document.getElementById('backupMask');
  var bkList = document.getElementById('bkList');
  var bkAll = document.getElementById('bkAll');

  function openBackupDialog(dirs) {
    var html = '';
    if (!dirs || !dirs.length) {
      html = '<div class="bk-empty">' + L('当前没有任何备份', 'No backups found') + '</div>';
    } else {
      for (var i = 0; i < dirs.length; i++) {
        html += '<label class="bk-item"><input type="checkbox" value="' + esc(dirs[i]) + '" /><span class="bk-check"></span><span class="bk-name">' + esc(dirs[i]) + '</span></label>';
      }
    }
    bkList.innerHTML = html;
    bkAll.checked = false;
    backupMask.classList.remove('hidden');
  }

  document.getElementById('btnOpenBackup').addEventListener('click', function () {
    post({ type: 'backupOpen' });
  });

  document.getElementById('btnCheckUpdate').addEventListener('click', function () {
    toast(L('正在检查更新…', 'Checking for updates…'));
    post({ type: 'checkUpdate' });
  });

  document.getElementById('btnDownloadLatest').addEventListener('click', function () {
    toast(L('正在准备最新版本下载…', 'Preparing the latest version download…'));
    post({ type: 'downloadLatest' });
  });

  var downloadList = document.getElementById('downloadList');
  if (downloadList) {
    downloadList.addEventListener('click', function (e) {
      var btn = e.target && e.target.closest ? e.target.closest('[data-download-op]') : null;
      if (!btn) return;
      var card = btn.closest ? btn.closest('.download-card') : null;
      var id = card ? (card.getAttribute('data-id') || 'release') : 'release';
      var op = btn.getAttribute('data-download-op');
      var item = state.downloads[id];
      if (op === 'toggle') {
        if (!item) return;
        if (item.status === 'paused') {
          post({ type: 'downloadResume', id: id });
          toast(L('正在继续下载…', 'Resuming download…'));
        } else {
          post({ type: 'downloadPause', id: id });
          toast(L('正在暂停下载…', 'Pausing download…'));
        }
      } else if (op === 'cancel') {
        var isHsf = item && item.kind === 'hsfLaunchPad';
        confirmDialog(
          L('取消下载？', 'Cancel download?'),
          isHsf
            ? L('将停止下载 LaunchPad 并清理临时文件，之后可以重新安装。', 'The LaunchPad download will stop and temporary files will be cleaned up. You can install again later.')
            : L('未完成的 .part 临时文件会被删除，之后可以重新下载。', 'The unfinished .part file will be deleted. You can download again later.'),
          L('取消下载', 'Cancel download'),
          function () { post({ type: 'downloadCancel', id: id }); }
        );
      } else if (op === 'open') {
        post({ type: 'downloadOpenFolder', id: id });
      } else if (op === 'install') {
        post({ type: 'installDownloaded', id: id });
        toast(L('正在准备安装更新…', 'Preparing to install the update…'));
      } else if (op === 'clear') {
        post({ type: 'downloadClear', id: id });
      }
    });
  }

  var btnExportLog = document.getElementById('btnExportLog');
  if (btnExportLog) {
    btnExportLog.addEventListener('click', function () {
      toast(L('正在导出日志到桌面…', 'Exporting logs to Desktop…'));
      post({ type: 'exportLog' });
    });
  }

  document.getElementById('btnClearBackup').addEventListener('click', function () {
    openBackupDialog(state.backup.dirs);
  });

  bkAll.addEventListener('change', function () {
    Array.prototype.forEach.call(bkList.querySelectorAll('input[type=checkbox]'), function (cb) {
      cb.checked = bkAll.checked;
      var item = cb.closest('.bk-item');
      if (item) item.classList.toggle('on', cb.checked);
    });
  });

  bkList.addEventListener('change', function (e) {
    if (e.target && e.target.type === 'checkbox') {
      var item = e.target.closest('.bk-item');
      if (item) item.classList.toggle('on', e.target.checked);
    }
  });

  document.getElementById('bkCancel').addEventListener('click', function () {
    backupMask.classList.add('hidden');
  });

  document.getElementById('bkDelete').addEventListener('click', function () {
    var selected = [];
    Array.prototype.forEach.call(bkList.querySelectorAll('input[type=checkbox]'), function (cb) {
      if (cb.checked) selected.push(cb.value);
    });
    backupMask.classList.add('hidden');
    if (!selected.length) {
      toast(L('没有选择任何备份', 'No backups selected'));
      return;
    }
    post({ type: 'backupDelete', dirs: selected });
    toast(L('正在删除…', 'Deleting…'));
  });

  // ---------------- 弹窗 ----------------
  function answerDialog(value) {
    if (currentDialogId) {
      post({ type: 'dialogResult', id: currentDialogId, value: value });
      currentDialogId = null;
    }
    confirmCallback = null;
    modalMask.classList.add('hidden');
  }

  modalCancel.addEventListener('click', function () { answerDialog(null); });

  modalConfirm.addEventListener('click', function () {
    var cb = confirmCallback;
    confirmCallback = null;
    modalMask.classList.add('hidden');
    if (cb) cb();
  });

  // 本地确认框：复用自绘 modal，保证居中且与软件 UI 一致
  function confirmDialog(title, text, okLabel, onConfirm) {
    currentDialogId = null;
    confirmCallback = onConfirm || null;
    modalBox.classList.add('confirm-solid');
    modalTitle.textContent = title || '';
    modalText.textContent = text || '';
    modalText.classList.remove('long');
    modalText.classList.toggle('hidden', !text);
    modalOptions.innerHTML = '';
    modalConfirm.textContent = okLabel || L('确定', 'OK');
    modalConfirm.classList.remove('hidden');
    modalMask.classList.remove('hidden');
    modalBox.scrollTop = 0;
  }

  function showDialog(d) {
    currentDialogId = d.id;
    confirmCallback = null;
    modalBox.classList.remove('confirm-solid');
    modalConfirm.classList.add('hidden');
    modalTitle.textContent = d.title || '';
    modalText.textContent = d.text || '';
    modalText.classList.toggle('hidden', !d.text);
    modalText.classList.toggle('long', !!(d.text && d.text.length > 600));
    modalOptions.innerHTML = '';
    var options = d.options || [];
    options.forEach(function (o) {
      var btn = document.createElement('button');
      btn.className = 'dlg-option';
      var inner = '';
      if (o.icon) inner += '<img class="opt-icon" src="' + esc(o.icon) + '" alt="">';
      inner += '<span class="opt-body"><span class="opt-label">' + esc(o.label) + '</span>' +
        (o.desc ? '<span class="opt-desc">' + esc(o.desc) + '</span>' : '') + '</span>';
      btn.innerHTML = inner;
      btn.addEventListener('click', function () { answerDialog(o.value); });
      modalOptions.appendChild(btn);
    });
    // 更新说明等长文本弹窗：每次打开都从顶部开始，避免标题/版本信息被滚动到可视区外。
    modalMask.classList.remove('hidden');
    modalBox.scrollTop = 0;
  }

  // ---------------- 右键工具弹窗 ----------------
  function closeToolDialog() {
    teardownSelects(toolBody);
    toolMask.classList.add('hidden');
    toolCallback = null;
  }

  function openToolDialog(cfg) {
    toolTitle.textContent = cfg.title || '';
    toolText.textContent = cfg.text || '';
    toolText.classList.toggle('hidden', !cfg.text);
    toolBody.innerHTML = cfg.body || '';
    setupAllSelects(toolBody);
    toolConfirm.textContent = cfg.ok || L('确定', 'OK');
    toolCallback = cfg.onConfirm || null;
    toolMask.classList.remove('hidden');
    if (cfg.afterOpen) cfg.afterOpen();
  }

  toolCancel.addEventListener('click', closeToolDialog);

  toolConfirm.addEventListener('click', function () {
    var cb = toolCallback;
    if (!cb) { closeToolDialog(); return; }
    if (cb() !== false) closeToolDialog();
  });

  // ---------------- 交流群二维码 ----------------
  function closeGroupDialog() {
    if (groupMask) groupMask.classList.add('hidden');
  }

  function openGroupDialog() {
    if (!groupMask) return;
    groupMask.classList.remove('hidden');
  }

  var btnGroup = document.getElementById('btnGroup');
  if (btnGroup) btnGroup.addEventListener('click', openGroupDialog);
  if (groupMask) {
    groupMask.addEventListener('click', function (e) {
      if (e.target === groupMask) closeGroupDialog();
    });
    var groupClose = document.getElementById('groupClose');
    var groupOk = document.getElementById('groupOk');
    if (groupClose) groupClose.addEventListener('click', closeGroupDialog);
    if (groupOk) groupOk.addEventListener('click', closeGroupDialog);
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && !groupMask.classList.contains('hidden')) closeGroupDialog();
    });
  }

  var CONFIG_NAMES = [
    'dinput8', 'winmm', 'd3d9', 'd3d10', 'd3d11', 'd3d12',
    'dxgi', 'dsound', 'wininet', 'winhttp', 'binkw64', 'bink2w64',
    'xinput1_1', 'xinput1_2', 'xinput1_3', 'xinput1_4', 'xinput9_1_0', 'xinputuap'
  ];

  function openConfigNameDialog(game, exe) {
    if (!game || game.frameMode !== '高级模式') {
      toast(L('配置名修改仅对高级模式开放，高级模式已集成所有功能', 'Config Name Modify is only available for Advanced Mode'));
      return;
    }
    var options = '';
    for (var i = 0; i < CONFIG_NAMES.length; i++) {
      options += '<option value="' + esc(CONFIG_NAMES[i]) + '">' + esc(CONFIG_NAMES[i]) + '.dll</option>';
    }
    openToolDialog({
      title: L('配置名修改', 'Config Name Modify'),
      text: L(
        '只在高级模式出现报错、非法模块或无法开启多帧生成时使用；一切正常请保持默认配置。\n\n鸣潮：无需改配置名，直接从游戏目录启动；异环：建议改为 dsound；明末：逐个测试可用配置名。',
        'Use this only if Advanced Mode causes errors, illegal module detection, or frame generation cannot start. Keep the default if everything works.\n\nWuthering Waves: no rename needed, launch directly from the game folder. Neverness to Everness: try dsound. Ming Dynasty: test each config name.'
      ),
      body: '<div class="tool-field"><label class="tool-label">' +
        esc(L('选择配置名（修改 version.dll）', 'Select config name (modifies version.dll)')) +
        '</label><select class="tool-select" id="toolConfigName">' + options + '</select></div>',
      ok: L('修改 version 配置名', 'Modify version config name'),
      onConfirm: function () {
        var sel = document.getElementById('toolConfigName');
        if (!sel) return false;
        post({ type: 'configName', exe: exe, name: sel.value });
        return true;
      }
    });
  }

  function openRemarkDialog(game, exe) {
    openToolDialog({
      title: L('编辑备注/标签', 'Edit Remark/Tags'),
      text: '',
      body: '<div class="tool-field"><label class="tool-label">' +
        esc(L('备注', 'Remark')) + '</label><textarea class="tool-area" id="toolRemark" rows="3" placeholder="' +
        esc(L('输入备注...', 'Enter remark...')) + '">' + esc((game && game.remark) || '') + '</textarea></div>' +
        '<div class="tool-field"><label class="tool-label">' +
        esc(L('标签（用逗号分隔）', 'Tags (comma separated)')) + '</label><input class="tool-input" id="toolTags" type="text" placeholder="' +
        esc(L('例如：已通关, 多人, 收藏', 'e.g. finished, multiplayer, favorite')) + '" value="' +
        esc((game && game.tagList) || '') + '" /></div>',
      ok: L('保存', 'Save'),
      onConfirm: function () {
        var remark = document.getElementById('toolRemark');
        var tags = document.getElementById('toolTags');
        post({
          type: 'saveRemark',
          exe: exe,
          remark: remark ? remark.value : '',
          tags: tags ? tags.value : ''
        });
        return true;
      }
    });
  }

  function openTroubleshootDialog(exe, items) {
    var html = '<div class="tool-hint">' +
      esc(L('勾选要替换的 sl.*.dll 文件；游戏中不存在的文件仅展示，不可勾选。', 'Select the sl.*.dll files to replace. Files missing from the game are shown but cannot be selected.')) +
      '</div><label class="tool-check-row tool-check-all"><input type="checkbox" id="toolAll" /><span class="bk-check"></span><span>' +
      esc(L('全选', 'Select all')) + '</span></label><div class="tool-list">';
    for (var i = 0; i < (items || []).length; i++) {
      var it = items[i];
      html += '<label class="tool-check-row' + (it.exists ? '' : ' off') + '"><input type="checkbox" data-file="' +
        esc(it.name) + '"' + (it.exists ? ' checked' : ' disabled') + ' /><span class="bk-check"></span><span>' + esc(it.name) +
        (it.exists ? '' : esc(L('（游戏中不存在，仅展示）', ' (not in game, display only)'))) + '</span></label>';
    }
    html += '</div>';
    openToolDialog({
      title: L('排错修复', 'Troubleshoot Repair'),
      text: '',
      body: html,
      ok: L('替换选中文件', 'Replace selected files'),
      afterOpen: function () {
        var all = document.getElementById('toolAll');
        if (all) {
          all.addEventListener('change', function () {
            Array.prototype.forEach.call(toolBody.querySelectorAll('input[data-file]'), function (cb) {
              if (!cb.disabled) cb.checked = all.checked;
            });
          });
        }
      },
      onConfirm: function () {
        var selected = [];
        Array.prototype.forEach.call(toolBody.querySelectorAll('input[data-file]'), function (cb) {
          if (!cb.disabled && cb.checked) selected.push(cb.getAttribute('data-file'));
        });
        if (!selected.length) {
          toast(L('没有选择任何文件', 'No files selected'));
          return false;
        }
        post({ type: 'troubleshootRun', exe: exe, files: selected });
        return true;
      }
    });
  }

  // ---------------- SM86 配置面板 ----------------
  var cfgMask = document.getElementById('cfgMask');
  var cfgTitle = document.getElementById('cfgTitle');
  var cfgSub = document.getElementById('cfgSub');
  var cfgBody = document.getElementById('cfgBody');
  var cfgSave = document.getElementById('cfgSave');
  var cfgReset = document.getElementById('cfgReset');
  var cfgCancel = document.getElementById('cfgCancel');
  var cfgX = document.getElementById('cfgX');
  var cfgState = null;

  var PROXY_OPTIONS = [
    ['version.dll', 'version.dll - 默认，先试这个'],
    ['winmm.dll', 'winmm.dll - 第 1 备用'],
    ['dbghelp.dll', 'dbghelp.dll - 第 2 备用'],
    ['dinput8.dll', 'dinput8.dll - 第 3（与 RE 框架冲突）'],
    ['dxgi.dll', 'dxgi.dll - 第 4（与 ReShade 冲突）'],
    ['d3d12.dll', 'd3d12.dll - 最后（在渲染路径上）']
  ];

  var SM86_SCHEMA = [
    {
      group: '代理 DLL',
      hint: '按 winmm → dbghelp → dinput8 → dxgi → d3d12 的顺序逐个试，一次只留一个。每次换完进游戏跑一会儿，看 dlssg_sm86\\logs 里有没有新行（日志级别要先设为 2）。',
      rows: [
        { key: 'proxy', label: '代理 DLL', options: PROXY_OPTIONS }
      ]
    },
    {
      group: '帧生成',
      rows: [
        { key: 'General.Enabled', label: '帧生成总开关', type: 'switch' },
        { key: 'FrameGeneration.Engine', label: '插帧引擎', options: [['DLSS', 'DLSS - NVIDIA 模型（默认）'], ['XeSS', 'XeSS-FG（Intel model 15）']] },
        { key: 'FrameGeneration.MaxGeneratedFrames', label: '生成帧数', options: [['1', '2 倍'], ['2', '3 倍'], ['3', '4 倍（出厂默认）'], ['5', '6 倍（需游戏支持 6X）']] },
        { key: 'DLSSG.Optimized', label: '加速档位', engineDlss: true, options: [['0', '0 - 不加速（最慢，基准）'], ['1', '1 - 与官方逐位一致（出厂）'], ['2', '2 - 略快，不再逐位一致'], ['3', '3 - 最快，有损']] },
        { key: 'DLSSG.Preset', label: '渲染预设', engineDlss: true, options: [['A', 'A - 默认'], ['B', 'B - 强制 UI 重组（多数游戏无效）'], ['C', 'C - 轻量帧生成（省 30-50% GPU 时间，画质略低）']] }
      ]
    },
    {
      group: '超分（仅 RTX 20/30 有效）',
      hint: 'RTX 40/50：本项目不改超分，改了也不起作用。',
      rows: [
        { key: 'SuperResolution.Mode', label: '超分模式', options: [['Off', '关闭（出厂默认）'], ['M-INT8', 'M-INT8'], ['L-INT8', 'L-INT8'], ['M-FP16', 'M-FP16'], ['L-FP16', 'L-FP16']] }
      ]
    },
    {
      group: 'XeSS-FG（仅引擎为 XeSS 时）',
      hint: 'XeSS-FG：此时 DLSSG 的加速档位与预设不起作用；后缓冲为 RGBA16F 的游戏会拒绝插帧。',
      rows: [
        { key: 'XeSSFG.FlowResolution', label: '光流精度', engineXess: true, options: [['Coarse', 'Coarse - 最粗最快'], ['Middle', 'Middle - 出厂默认'], ['Fine', 'Fine - 最细最慢'], ['CoarseAt4K', 'CoarseAt4K']] },
        { key: 'XeSSFG.UseHudless', label: '无 HUD 画面要求', engineXess: true, options: [['Auto', 'Auto - 游戏给不给都行'], ['1', '1 - 一律要求（没有就报错退出）'], ['0', '0']] }
      ]
    },
    {
      group: '日志',
      rows: [
        { key: 'Logging.Level', label: '日志级别', options: [['0', '0 - 不记录'], ['1', '1 - 只记错误（出厂默认）'], ['2', '2 - 详细（反馈问题时用）'], ['3', '3']] },
        { key: 'Logging.FrameTiming', label: '记录每帧耗时（frame_timing.txt）', type: 'switch' }
      ]
    }
  ];

  function syncEngineState() {
    var engineSelect = cfgBody.querySelector('[data-k="FrameGeneration.Engine"]');
    var isXess = engineSelect && engineSelect.value === 'XeSS';
    Array.prototype.forEach.call(cfgBody.querySelectorAll('.cfg-row[data-engine]'), function (row) {
      var need = row.getAttribute('data-engine');
      var disable = (need === 'xess' && !isXess) || (need === 'dlss' && isXess);
      row.classList.toggle('disabled', disable);
    });
  }

  function renderCfg() {
    teardownSelects(cfgBody);
    var html = '';
    SM86_SCHEMA.forEach(function (g) {
      html += '<div class="cfg-group"><div class="cfg-group-title">' + esc(tr(g.group)) + '</div>';
      g.rows.forEach(function (r) {
        var val = r.key === 'proxy' ? cfgState.proxy : cfgState.values[r.key];
        var extra = r.engineDlss ? ' data-engine="dlss"' : (r.engineXess ? ' data-engine="xess"' : '');
        html += '<div class="cfg-row"' + extra + '><span class="cfg-label">' + esc(tr(r.label)) + '</span>';
        if (r.type === 'switch') {
          var on = (val === true || val === '1' || val === 1 || String(val) === 'true');
          html += '<button class="cfg-switch' + (on ? ' on' : '') + '" data-k="' + r.key + '"><i></i></button>';
        } else {
          var opts = '';
          r.options.forEach(function (o) {
            opts += '<option value="' + esc(o[0]) + '"' + (String(val) === String(o[0]) ? ' selected' : '') + '>' + esc(tr(o[1])) + '</option>';
          });
          html += '<select class="cfg-select" data-k="' + r.key + '">' + opts + '</select>';
        }
        html += '</div>';
      });
      if (g.hint) html += '<div class="cfg-hint">' + esc(tr(g.hint)) + '</div>';
      html += '</div>';
    });
    cfgBody.innerHTML = html;
    setupAllSelects(cfgBody);

    Array.prototype.forEach.call(cfgBody.querySelectorAll('.cfg-switch'), function (btn) {
      btn.addEventListener('click', function () {
        btn.classList.toggle('on');
        syncEngineState();
      });
    });
    var engineSelect = cfgBody.querySelector('[data-k="FrameGeneration.Engine"]');
    if (engineSelect) engineSelect.addEventListener('change', syncEngineState);
    syncEngineState();
  }

  function openCfg(d) {
    cfgState = { exe: d.exe, name: d.name || '', proxy: d.proxy || 'version.dll', values: d.values || {} };
    cfgTitle.textContent = L('SM86 多帧生成设置', 'SM86 Frame Generation Settings');
    cfgSub.textContent = (d.name || '') + L('  ·  修改后需完全退出游戏再启动', '  ·  Restart the game after changes');
    renderCfg();
    cfgMask.classList.remove('hidden');
  }

  function closeCfg() {
    teardownSelects(cfgBody);
    cfgMask.classList.add('hidden');
    cfgState = null;
  }

  function collectCfg() {
    var values = {};
    Array.prototype.forEach.call(cfgBody.querySelectorAll('[data-k]'), function (el) {
      var k = el.getAttribute('data-k');
      if (k === 'proxy') return;
      if (el.classList.contains('cfg-switch')) values[k] = el.classList.contains('on') ? '1' : '0';
      else values[k] = el.value;
    });
    var proxyEl = cfgBody.querySelector('[data-k="proxy"]');
    return { proxy: proxyEl ? proxyEl.value : 'version.dll', values: values };
  }

  cfgSave.addEventListener('click', function () {
    if (!cfgState) return;
    var data = collectCfg();
    post({ type: 'sm86save', exe: cfgState.exe, proxy: data.proxy, values: data.values });
    closeCfg();
    toast(L('正在保存配置…', 'Saving configuration…'));
  });

  cfgReset.addEventListener('click', function () {
    if (!cfgState) return;
    if (!confirm(tr('恢复出厂 dlssg_sm86.ini？当前配置会被覆盖。'))) return;
    post({ type: 'sm86reset', exe: cfgState.exe });
    closeCfg();
    toast(L('正在恢复出厂配置…', 'Restoring defaults…'));
  });

  cfgCancel.addEventListener('click', closeCfg);
  cfgX.addEventListener('click', closeCfg);

  // 截图/调试用钩子：只改当前渲染，不写设置
  window.__beta = {
    page: function (page) { if (typeof activatePage === 'function') activatePage(page); },
    lang: function (english) { applyLang(!!english); },
    big: function (on) {
      state.bigView = !!on;
      updateViewControl();
      render();
    },
    menu: function () {
      setTimeout(function () {
        var card = document.querySelector('.game-card');
        if (!card) return;
        var rect = card.getBoundingClientRect();
        card.dispatchEvent(new MouseEvent('contextmenu', {
          bubbles: true,
          cancelable: true,
          clientX: rect.left + rect.width - 12,
          clientY: rect.top + 28
        }));
      }, 800);
    },
    theme: function (dark, backdrop) {
      state.dark = !!dark;
      if (typeof backdrop === 'number') state.backdrop = backdrop;
      applyThemeAttributes();
      setSwitch('dark', state.dark);
    },
    demoOn: function () {
      setTimeout(function () {
        var card = document.querySelector('.game-card');
        if (!card) return;
        var frame = card.querySelector('[data-op="frame"]');
        if (frame) {
          frame.classList.add('on');
          frame.disabled = false;
          frame.textContent = L('关闭多帧', 'Turn off Frame Gen');
        }
        var dlss = card.querySelector('[data-op="dlss5"]');
        if (dlss) {
          dlss.classList.add('on', 'dlss');
          dlss.textContent = L('关闭DLSS5', 'Turn off DLSS5');
        }
        var restore = card.querySelector('[data-op="restore"]');
        if (restore) restore.classList.add('danger');
      }, 1200);
    },
    demoSelect: function () {
      setTimeout(function () {
        var sel = document.getElementById('setLang');
        if (sel && sel.__openMenu) sel.__openMenu();
      }, 1000);
    },
    demoBackup: function () {
      setTimeout(function () {
        openBackupDialog(state.english
          ? ['Phantom Blade Zero', 'Phantom Blade Zero DLSS5', 'Cyberpunk 2077']
          : ['影之刃零', '影之刃零 DLSS5', '赛博朋克2077']);
        var first = bkList.querySelector('.bk-item');
        if (first) {
          var cb = first.querySelector('input');
          if (cb) { cb.checked = true; first.classList.add('on'); }
        }
      }, 1000);
    }
  };

  // ---------------- 消息 ----------------
  if (wv) {
    wv.addEventListener('message', function (e) {
      var d = e.data;
      if (!d) return;
      if (d.type === 'games') {
        games = d.items || [];
        render();
        if (!homeIntroShown && state.anim) {
          homeIntroShown = true;
          playHomeIntro();
        }
      } else if (d.type === 'status') {
        toast(d.text || '');
      } else if (d.type === 'dialog') {
        showDialog(d);
      } else if (d.type === 'progress') {
        progressText.textContent = d.text || L('正在处理…', 'Working…');
        progressMask.classList.remove('hidden');
      } else if (d.type === 'progressHide') {
        progressMask.classList.add('hidden');
      } else if (d.type === 'navigate') {
        if (d.page) activatePage(d.page);
      } else if (d.type === 'downloadState') {
        var downloadId = d.id || 'release';
        var prevDownload = state.downloads[downloadId];
        var prevDownloadStatus = prevDownload ? prevDownload.status : '';
        d.id = downloadId;
        state.downloads[downloadId] = d;
        renderDownload();
        if (d.navigate) activatePage('download');
        if (d.status === 'completed' && prevDownloadStatus !== 'completed') {
          toast(L('下载完成', 'Download complete'));
        } else if (d.status === 'paused' && prevDownloadStatus !== 'paused') {
          toast(L('下载已暂停', 'Download paused'));
        } else if (d.status === 'failed' && prevDownloadStatus !== 'failed') {
          if (d.error) toast(d.error);
          else toast(L('下载失败', 'Download failed'));
        } else if (d.status === 'cancelled' && prevDownloadStatus !== 'cancelled') {
          toast(L('下载已取消', 'Download cancelled'));
        }
      } else if (d.type === 'downloadClear') {
        delete state.downloads[d.id || 'release'];
        renderDownload();
      } else if (d.type === 'appinfo') {
        state.version = d.version || '1.75';
        state.isBeta = !!d.isBeta;
        state.installPath = d.installPath || '';
        document.getElementById('setDataPath').textContent = d.dataPath || '—';
        document.getElementById('setLogPath').textContent = d.logPath || '—';
        document.getElementById('setInstallPath').textContent = d.installPath || '—';
        document.getElementById('setVersion').textContent = d.version || '—';
        if (d.isBeta) {
          var verEl = document.getElementById('setVersion');
          var badge = document.createElement('span');
          badge.className = 'ver-badge';
          badge.textContent = 'Beta';
          verEl.appendChild(badge);
        }
        updateAboutVer();
      } else if (d.type === 'settings') {
        state.english = !!d.english;
        renderSettings(d);
        applyLang(!!d.english);
        if (state.anim && state.currentPage === 'home' && !homeIntroShown) {
          homeIntroShown = true;
          playHomeIntro();
        }
      } else if (d.type === 'sm86data') {
        openCfg(d);
      } else if (d.type === 'smoothLoading') {
        var badge = document.getElementById('smSupportBadge');
        if (badge) {
          badge.className = 'sm-badge';
          badge.textContent = L('读取中…', 'Reading…');
        }
      } else if (d.type === 'smoothState') {
        smoothData = d;
        renderSmooth();
      } else if (d.type === 'smoothError') {
        toast(d.message || L('Smooth Motion 操作失败', 'Smooth Motion failed'));
      } else if (d.type === 'hsfState') {
        hsfData = d;
        renderHsf();
      } else if (d.type === 'hsfError') {
        toast(d.message || L('HSF 操作失败', 'HSF failed'));
      } else if (d.type === 'captureState') {
        captureData = d;
        renderCapture();
      } else if (d.type === 'captureWindowSelected') {
        var selectedInput = document.getElementById('captureWindowSelect');
        if (selectedInput && d.hwnd) {
          selectedInput.value = String(d.hwnd);
          selectedInput.dispatchEvent(new Event('change'));
        }
        var selectedWindow = (captureWindowOptions || []).find(function (w) {
          return String(w.hwnd) === String(d.hwnd);
        }) || null;
        if (!selectedWindow && d.hwnd) {
          selectedWindow = {
            hwnd: d.hwnd,
            process: d.process || '',
            title: d.title || '',
            exe: d.exe || '',
            width: d.width || 0,
            height: d.height || 0,
            minimized: !!d.minimized,
            icon: d.icon || ''
          };
        }
        updateCaptureWindowPickerUI(selectedWindow);
      } else if (d.type === 'troubleshootFiles') {
        if (d.items && d.items.length) {
          openTroubleshootDialog(d.exe || '', d.items);
        }
      }
    });
    post({ type: 'ready' });
  } else {
    listEl.innerHTML = '<div class="empty">未检测到 WebView2 宿主</div>';
  }
})();
