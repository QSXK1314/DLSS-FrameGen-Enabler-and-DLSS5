using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    /// <summary>
    /// OptiCaptureHost 外置捕获引擎控制层。
    /// 只启动 CaptureHost\OptiCaptureHost.exe，不启动 OptiCaptureLauncher.exe；
    /// 所有设置由本软件写入 OptiCaptureHost.ini / OptiScaler.ini。
    /// </summary>
    public static class CaptureHostService
    {
        public sealed class CaptureConfig
        {
            public int FrameGenMode { get; set; } = 9;   // 0 XeFG, 1 DLSSG, 5 Combo, 6 Nukem, 7 FSR3-4, 8 Nvidia MFG, 9 F5Low, 2 off
            public int MfgCount { get; set; } = 2;       // 0 auto, 1..5 = 2x..6x; 3X keeps the source frame rate usable on a 165 Hz panel
            public int UpscalerMode { get; set; } = 4;   // 0 XeSS, 1 FSR3.1.6, 2 DLSS, 3 DLSS3.7, 4 FSR4.1.2, 5 DittoSR
            public int Upscale { get; set; } = 0;        // 0 off, 1 bilinear, 2 FSR1, 3 NIS, 4 FSR3/4, 5 DLSS
            public int UpscaleLevel { get; set; } = 0;   // 0 Quality, 1 Balanced, 2 Performance, 3 Native
            public int PresentMode { get; set; } = 6;    // 0..6
            public int Boost { get; set; } = 3;          // 0..3；外置捕获默认 Max（性能 level 3）
            public bool VSync { get; set; } = true;
            public int PresentDelay { get; set; } = 0;   // 0..8 ms; >0 can hold the next WGC buffer, so 0 is the default but the user can adjust it
            public int DepthSource { get; set; } = 0;    // 0 flat depth, 1 ReShade shared depth (OptiCaptureHost 2.12+)

            public CaptureConfig Clone()
            {
                return new CaptureConfig
                {
                    FrameGenMode = FrameGenMode,
                    MfgCount = MfgCount,
                    UpscalerMode = UpscalerMode,
                    Upscale = Upscale,
                    UpscaleLevel = UpscaleLevel,
                    PresentMode = PresentMode,
                    Boost = Boost,
                    VSync = VSync,
                    PresentDelay = PresentDelay,
                    DepthSource = DepthSource
                };
            }

            public void Normalize()
            {
                if (FrameGenMode < 0 || FrameGenMode > 10 || FrameGenMode == 3) FrameGenMode = 0;
                if (MfgCount < 0 || MfgCount > 5) MfgCount = 0;
                if (UpscalerMode < 0 || UpscalerMode > 5) UpscalerMode = 4;
                if (Upscale < 0 || Upscale > 5) Upscale = 0;
                if (UpscaleLevel < 0 || UpscaleLevel > 3) UpscaleLevel = 0;
                if (PresentMode < 0 || PresentMode > 6) PresentMode = 6;
                if (Boost < 0 || Boost > 3) Boost = 3;
                // PresentDelay is user-selectable again. 0 ms remains the default because
                // a hold can occupy the next WGC buffer and lower the capture rate; the UI
                // exposes 0/1/2/3/4/6/8 ms for users who need to smooth presentation jitter.
                if (PresentDelay < 0 || PresentDelay > 8) PresentDelay = 0;
                if (DepthSource < 0 || DepthSource > 1) DepthSource = 0;
            }
        }

        public sealed class CaptureWindowInfo
        {
            public string Hwnd { get; set; } = "";
            public uint Pid { get; set; }
            public string ProcessName { get; set; } = "";
            public string Title { get; set; } = "";
            public string ExePath { get; set; } = "";
            public int Width { get; set; }
            public int Height { get; set; }
            public bool Minimized { get; set; }
        }

        public sealed class CaptureSnapshot
        {
            public bool Running { get; set; }
            public bool OverlayHidden { get; set; }
            public bool EngineExists { get; set; }
            public string EngineVersion { get; set; } = "";
            public string EnginePath { get; set; } = "";
            public bool LicenseOk { get; set; }
            public bool SourceOk { get; set; }
            public int Pid { get; set; }
            public string WindowTitle { get; set; } = "";
            public bool StatusFresh { get; set; }
            public double RefreshHz { get; set; }
            public double CaptureFps { get; set; }
            public double PresentFps { get; set; }
            public double DxgiFps { get; set; }
            public double PresentMs { get; set; }
            public double WaitMs { get; set; }
            public int Interval { get; set; }
            public int Focus { get; set; }
            public int CapHint { get; set; }
            public double CapFps { get; set; }
            public int DepthState { get; set; }
            public uint DepthWidth { get; set; }
            public uint DepthHeight { get; set; }
            public string LastError { get; set; } = "";
            public CaptureConfig Config { get; set; } = new CaptureConfig();
            public List<CaptureWindowInfo> Windows { get; set; } = new List<CaptureWindowInfo>();
        }

        public sealed class CaptureActionResult
        {
            public bool Ok { get; set; }
            public string Message { get; set; } = "";
        }

        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int WM_CLOSE = 0x0010;
        private const int WM_HOTKEY = 0x0312;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_RESTORE = 9;

        // The host-side overlay visibility command (WM_APP + 0x51). The patched
        // OptiCaptureHost keeps its own hidden flag so MoveOverlay() will not
        // immediately re-show the window; capture and frame generation keep running.
        private const int WM_APP = 0x8000;
        private const int OverlayVisibilityMessage = WM_APP + 0x51;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "WritePrivateProfileStringW")]
        private static extern long WritePrivateProfileStringW(string section, string? key, string? value, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetPrivateProfileIntW")]
        private static extern uint GetPrivateProfileIntW(string section, string key, int defaultValue, string filePath);

        private static Process? _hostProcess;
        private static IntPtr _targetHwnd = IntPtr.Zero;
        private static string _targetTitle = "";
        private static RECT _originalRect;
        private static bool _originalRectValid;
        private static bool _originalMinimized;
        private static string _lastError = "";
        private static ulong _startedTick;
        private static bool _overlayHidden;

        public static IntPtr TargetWindow => _targetHwnd;
        public static string CaptureRoot => Path.Combine(AppContext.BaseDirectory, "CaptureHost");
        public static string HostExe => Path.Combine(CaptureRoot, "OptiCaptureHost.exe");
        public static string HostIni => Path.Combine(CaptureRoot, "OptiCaptureHost.ini");
        public static string OptiIni => Path.Combine(CaptureRoot, "OptiScaler.ini");
        public static string StatusFile => Path.Combine(CaptureRoot, "OptiCaptureHost.status");
        public static string LogFile => Path.Combine(CaptureRoot, "OptiCaptureHost.log");
        public static string MenuLanguageFile => Path.Combine(CaptureRoot, "OptiScaler.lang");

        /// <summary>把当前软件语言写盘，OptiScaler 菜单会在大约 500 ms 内切到相同语言。</summary>
        public static void SyncMenuLanguage()
        {
            try
            {
                Directory.CreateDirectory(CaptureRoot);
                File.WriteAllText(MenuLanguageFile, Translator.IsEnglish ? "en" : "zh", new UTF8Encoding(false));
            }
            catch
            {
                // 语言文件写失败不应阻止启动或捕获；Host 启动参数仍会带环境变量。
            }
        }

        public static bool IsRunning
        {
            get
            {
                try
                {
                    return _hostProcess != null && !_hostProcess.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>当前是否由本软件主动隐藏了 Host 覆盖层（捕获仍在运行）。</summary>
        public static bool IsOverlayHidden
        {
            get { return _overlayHidden; }
        }

        public static CaptureSnapshot GetSnapshot(bool includeWindows)
        {
            CaptureSnapshot snapshot = new CaptureSnapshot
            {
                Running = IsRunning,
                OverlayHidden = _overlayHidden,
                EngineExists = File.Exists(HostExe) && File.Exists(OptiIni) && File.Exists(HostIni),
                EngineVersion = ReadVersion(HostExe),
                EnginePath = CaptureRoot,
                LicenseOk = File.Exists(Path.Combine(CaptureRoot, "licenses", "GPL-3.0.txt"))
                    && File.Exists(Path.Combine(CaptureRoot, "licenses", "OptiCaptureHost-LICENSE.txt"))
                    && File.Exists(Path.Combine(CaptureRoot, "licenses", "OptiScaler-LICENSE.txt")),
                SourceOk = (File.Exists(Path.Combine(CaptureRoot, "source", "OptiCaptureHost-2.12.00-source.zip"))
                        || File.Exists(Path.Combine(CaptureRoot, "source", "OptiCaptureHost-2.10.80-source.zip")))
                    && File.Exists(Path.Combine(CaptureRoot, "source", "OptiScaler-0.1.39-pre1-956bb719-source.zip")),
                Config = ReadConfig(),
                LastError = _lastError,
                WindowTitle = _targetHwnd != IntPtr.Zero ? WindowTitle(_targetHwnd) : ""
            };

            if (_hostProcess != null)
            {
                try
                {
                    snapshot.Pid = _hostProcess.HasExited ? 0 : _hostProcess.Id;
                    if (_hostProcess.HasExited && _lastError.Length == 0)
                    {
                        _lastError = ReadLogTail();
                        snapshot.LastError = _lastError;
                    }
                }
                catch
                {
                }
            }

            ReadLiveStatus(snapshot);
            if (includeWindows)
            {
                snapshot.Windows = ListWindows();
            }
            return snapshot;
        }

        public static CaptureActionResult Start(IntPtr hwnd, CaptureConfig config)
        {
            if (!IsWindow(hwnd))
            {
                return new CaptureActionResult { Ok = false, Message = "找不到这个窗口，请刷新后重新选择。" };
            }
            if (!File.Exists(HostExe))
            {
                return new CaptureActionResult { Ok = false, Message = "没有找到 CaptureHost\\OptiCaptureHost.exe，请确认 Full Pack 已完整放入软件目录。" };
            }
            if (IsRunning)
            {
                return new CaptureActionResult { Ok = false, Message = "已有捕获正在运行，请先停止。" };
            }

            config.Normalize();
            MigrateLegacyConfig(config);
            _lastError = "";
            _overlayHidden = false;

            try
            {
                EnsureEngineDefaults();
                if (!WriteConfigFiles(config, out string writeError))
                {
                    return new CaptureActionResult { Ok = false, Message = writeError };
                }

                _targetHwnd = hwnd;
                _targetTitle = WindowTitle(hwnd);
                _originalRectValid = GetWindowRect(hwnd, out _originalRect);
                _originalMinimized = IsIconic(hwnd);
                if (_originalMinimized)
                {
                    ShowWindow(hwnd, 9); // SW_RESTORE
                }

                if (config.Upscale != 0)
                {
                    ResizeWindowForOutputScaling(hwnd, config);
                }

                TryDelete(StatusFile);
                SyncMenuLanguage();

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = HostExe,
                    WorkingDirectory = CaptureRoot,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                psi.Environment["OPTISCALER_UI_LANG"] = Translator.IsEnglish ? "en" : "zh";
                psi.ArgumentList.Add("--hwnd");
                psi.ArgumentList.Add(hwnd.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
                psi.ArgumentList.Add("--parent");
                psi.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

                _hostProcess = Process.Start(psi);
                if (_hostProcess == null)
                {
                    RestoreWindowAfterFailure();
                    return new CaptureActionResult { Ok = false, Message = "启动 OptiCaptureHost.exe 失败。" };
                }
                _startedTick = (ulong)Environment.TickCount64;

                System.Threading.Thread.Sleep(280);
                try
                {
                    if (_hostProcess.HasExited)
                    {
                        _lastError = ReadLogTail();
                        RestoreWindowAfterFailure();
                        string detail = _lastError.Length > 0 ? "\n\n" + _lastError : "";
                        return new CaptureActionResult { Ok = false, Message = "捕获进程启动后立即退出。" + detail };
                    }
                }
                catch
                {
                }

                return new CaptureActionResult
                {
                    Ok = true,
                    Message = config.Upscale != 0
                        ? "捕获已启动。输出缩放已按设置调整游戏窗口；停止捕获时会恢复原窗口大小。"
                        : "捕获已启动。游戏请保持无边框或窗口模式，按“打开 OptiScaler 菜单”可进入帧生成设置。"
                };
            }
            catch (Exception ex)
            {
                RestoreWindowAfterFailure();
                _lastError = ex.Message;
                return new CaptureActionResult { Ok = false, Message = "启动捕获失败：" + ex.Message };
            }
        }

        public static CaptureActionResult Stop()
        {
            if (!IsRunning)
            {
                RestoreWindowAfterFailure();
                return new CaptureActionResult { Ok = true, Message = "当前没有正在运行的捕获。" };
            }

            try
            {
                IntPtr overlay = FindOverlayWindow(_hostProcess!.Id);
                if (overlay != IntPtr.Zero)
                {
                    PostMessage(overlay, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                else
                {
                    try { _hostProcess.CloseMainWindow(); } catch { }
                }

                if (!_hostProcess.WaitForExit(4500))
                {
                    try { _hostProcess.Kill(entireProcessTree: true); } catch { }
                    try { _hostProcess.WaitForExit(1500); } catch { }
                }
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                try { _hostProcess?.Kill(entireProcessTree: true); } catch { }
            }
            finally
            {
                try { _hostProcess?.Dispose(); } catch { }
                _hostProcess = null;
                RestoreWindowAfterFailure();
                _targetHwnd = IntPtr.Zero;
                _targetTitle = "";
                _overlayHidden = false;
            }

            return new CaptureActionResult { Ok = true, Message = "捕获已停止。" };
        }

        public static CaptureActionResult ApplyConfig(CaptureConfig config)
        {
            config.Normalize();
            MigrateLegacyConfig(config);
            try
            {
                EnsureEngineDefaults();
                if (!WriteConfigFiles(config, out string error))
                {
                    return new CaptureActionResult { Ok = false, Message = error };
                }
                string suffix = IsRunning
                    ? " 帧生成模式和倍率正在重启捕获后生效。"
                    : "";
                return new CaptureActionResult { Ok = true, Message = "设置已保存。" + suffix };
            }
            catch (Exception ex)
            {
                return new CaptureActionResult { Ok = false, Message = "保存设置失败：" + ex.Message };
            }
        }

        public static CaptureActionResult SendMenu()
        {
            if (!IsRunning) return new CaptureActionResult { Ok = false, Message = "捕获未运行。" };
            return SendHotkey(3, "已请求打开 OptiScaler 菜单。菜单出现后可用鼠标操作，关闭后点“交还鼠标”。");
        }

        public static CaptureActionResult ToggleFocus()
        {
            if (!IsRunning) return new CaptureActionResult { Ok = false, Message = "捕获未运行。" };
            return SendHotkey(2, "已切换游戏 / 覆盖层焦点。若画面卡住，再按一次或点游戏画面恢复。");
        }

        /// <summary>
        /// 只隐藏/恢复 Host 的覆盖层窗口，不停止捕获进程。
        /// 隐藏后游戏窗口本身仍被 WGC 捕获，补帧也不会因为“停止捕获”而中断；
        /// 但 Windows 会认为游戏不是前台，Host/OptiScaler 可能暂停生成帧，恢复前台后继续。
        /// </summary>
        public static CaptureActionResult ToggleOverlay()
        {
            if (!IsRunning)
            {
                return new CaptureActionResult { Ok = false, Message = "捕获未运行。" };
            }

            try
            {
                IntPtr overlay = FindOverlayWindowWithRetry(_hostProcess!.Id);
                if (overlay == IntPtr.Zero)
                {
                    return new CaptureActionResult
                    {
                        Ok = false,
                        Message = "没有找到 Host 覆盖层窗口。请等捕获状态变为运行中后再试。"
                    };
                }

                bool hide = !_overlayHidden;
                if (PostMessage(overlay, OverlayVisibilityMessage, hide ? new IntPtr(1) : IntPtr.Zero, IntPtr.Zero))
                {
                    _overlayHidden = hide;
                    return new CaptureActionResult
                    {
                        Ok = true,
                        Message = hide
                            ? "已隐藏覆盖层（捕获不停止），现在可以 Alt+Tab 切到其他窗口；按 Ctrl+Shift+F9 或点“恢复覆盖层”回来。"
                            : "已恢复覆盖层。按 Ctrl+Shift+F11 或点一下游戏画面切回游戏焦点。"
                    };
                }

                // Fallback for an old/unpatched CaptureHost build. The Host's
                // MoveOverlay() may re-show it on the next pass; the packaged
                // 1.63.2 build uses the patched host above.
                if (hide)
                {
                    ShowWindow(overlay, SW_HIDE);
                    _overlayHidden = true;
                    return new CaptureActionResult
                    {
                        Ok = true,
                        Message = "已隐藏覆盖层，捕获和补帧继续运行。现在可以 Alt+Tab 切到其他窗口；按 Ctrl+Shift+F9 或点“恢复覆盖层”回来。"
                    };
                }

                ShowWindow(overlay, SW_SHOWNOACTIVATE);
                _overlayHidden = false;
                return new CaptureActionResult
                {
                    Ok = true,
                    Message = "已恢复覆盖层。按 Ctrl+Shift+F11 或点一下游戏画面切回游戏焦点。"
                };
            }
            catch (Exception ex)
            {
                return new CaptureActionResult { Ok = false, Message = "切换覆盖层失败：" + ex.Message };
            }
        }

        private static IntPtr FindOverlayWindowWithRetry(int hostPid)
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                IntPtr overlay = FindOverlayWindow(hostPid);
                if (overlay != IntPtr.Zero)
                {
                    return overlay;
                }
                Thread.Sleep(100);
            }
            return IntPtr.Zero;
        }

        private static CaptureActionResult SendHotkey(int hotkeyId, string successMessage)
        {
            try
            {
                IntPtr overlay = FindOverlayWindow(_hostProcess!.Id);
                if (overlay != IntPtr.Zero)
                {
                    // The Host's message pump reads WM_HOTKEY from the overlay's
                    // owning thread queue, not from the overlay window proc. Post
                    // it to the thread, otherwise the message is accepted and
                    // silently dropped by DefWindowProc.
                    uint threadId = GetWindowThreadProcessId(overlay, out _);
                    if (threadId != 0 &&
                        PostThreadMessage(threadId, WM_HOTKEY, new IntPtr(hotkeyId), IntPtr.Zero))
                    {
                        return new CaptureActionResult { Ok = true, Message = successMessage };
                    }
                }

                // 兜底：Host 注册的是全局热键，直接模拟 Ctrl+Shift+F10/F11。
                byte vk = hotkeyId == 3 ? (byte)0x79 : (byte)0x7A; // F10 / F11
                keybd_event(0x11, 0, 0, UIntPtr.Zero);
                keybd_event(0x10, 0, 0, UIntPtr.Zero);
                keybd_event(vk, 0, 0, UIntPtr.Zero);
                keybd_event(vk, 0, 2, UIntPtr.Zero);
                keybd_event(0x10, 0, 2, UIntPtr.Zero);
                keybd_event(0x11, 0, 2, UIntPtr.Zero);
                return new CaptureActionResult { Ok = true, Message = successMessage };
            }
            catch (Exception ex)
            {
                return new CaptureActionResult { Ok = false, Message = "发送热键失败：" + ex.Message };
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostThreadMessage(uint idThread, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public static string ReadLogTail(int maxLines = 18)
        {
            try
            {
                if (!File.Exists(LogFile)) return "";
                string[] lines = File.ReadAllLines(LogFile, Encoding.UTF8);
                if (lines.Length == 0) return "";
                int start = Math.Max(0, lines.Length - maxLines);
                return string.Join("\n", lines.Skip(start));
            }
            catch
            {
                return "";
            }
        }

        public static CaptureConfig ReadConfig()
        {
            CaptureConfig c = new CaptureConfig();
            try
            {
                if (File.Exists(HostIni))
                {
                    c.VSync = GetPrivateProfileIntW("General", "VSync", c.VSync ? 1 : 0, HostIni) != 0;
                    c.FrameGenMode = (int)GetPrivateProfileIntW("Settings", "FrameGenMode", c.FrameGenMode, HostIni);
                    c.MfgCount = (int)GetPrivateProfileIntW("Settings", "MfgCount", c.MfgCount, HostIni);
                    c.UpscalerMode = (int)GetPrivateProfileIntW("Settings", "UpscalerMode", c.UpscalerMode, HostIni);
                    c.PresentMode = (int)GetPrivateProfileIntW("Advanced", "PresentMode", c.PresentMode, HostIni);
                    c.Boost = (int)GetPrivateProfileIntW("Advanced", "Boost", c.Boost, HostIni);
                    c.PresentDelay = (int)GetPrivateProfileIntW("Advanced", "PresentDelayMs", c.PresentDelay, HostIni);
                    c.Upscale = (int)GetPrivateProfileIntW("Upscale", "Method", c.Upscale, HostIni);
                    c.UpscaleLevel = (int)GetPrivateProfileIntW("Upscale", "Level", c.UpscaleLevel, HostIni);
                    c.DepthSource = (int)GetPrivateProfileIntW("Depth", "Source", c.DepthSource, HostIni);
                }
            }
            catch
            {
            }
            c.Normalize();
            MigrateLegacyConfig(c);
            return c;
        }

        private static bool WriteConfigFiles(CaptureConfig c, out string error)
        {
            error = "";
            try
            {
                if (!File.Exists(HostIni))
                {
                    error = "缺少 OptiCaptureHost.ini。";
                    return false;
                }
                if (!File.Exists(OptiIni))
                {
                    error = "缺少 OptiScaler.ini。";
                    return false;
                }

                WriteIni("General", "VSync", c.VSync ? "1" : "0", HostIni);
                WriteIni("Settings", "FrameGenMode", c.FrameGenMode.ToString(), HostIni);
                WriteIni("Settings", "MfgCount", c.MfgCount.ToString(), HostIni);
                WriteIni("Settings", "UpscalerMode", c.UpscalerMode.ToString(), HostIni);
                WriteIni("Advanced", "PresentMode", c.PresentMode.ToString(), HostIni);
                WriteIni("Advanced", "Boost", c.Boost.ToString(), HostIni);
                WriteIni("Advanced", "PresentDelayMs", c.PresentDelay.ToString(), HostIni);
                WriteIni("Upscale", "Method", c.Upscale.ToString(), HostIni);
                WriteIni("Upscale", "Level", c.UpscaleLevel.ToString(), HostIni);
                WriteIni("Depth", "Source", c.DepthSource == 1 ? "1" : "0", HostIni);

                // 关闭 OptiCaptureHost 自带的 RTSS 限帧和 OptiFps 覆盖，否则会出现：
                // Fps=-4 把游戏原始帧压到刷新率的 1/4（165Hz 下约 41fps），
                // OptiFps=1 又把 OptiScaler.ini 的 FramerateLimit 改回刷新率。
                // 这两项都会让用户设置的 0 失效，表现为“先降原始帧再补帧”。
                WriteIni("Limiter", "Target", "0", HostIni);
                WriteIni("Limiter", "Fps", "0", HostIni);
                WriteIni("Limiter", "OptiFps", "0", HostIni);
                WriteIni("Limiter", "Auto", "0", HostIni);

                // 外置捕获推荐的低延迟组合：OptiScaler 接管输入，NVIDIA Reflex 负责输出。
                // 帧率上限必须是 0，否则 OptiScaler 会先压低渲染帧率，再用生成帧补回来，
                // 造成“先降帧再补帧”的卡顿/节奏异常。用户仍可在菜单里临时改，下一次开始捕获会恢复默认。
                WriteIni("Framerate", "LowLatencyInput", "optiscaler", OptiIni);
                WriteIni("XeFG", "LowLatencyOutput", "reflex", OptiIni);
                WriteIni("Framerate", "FramerateLimit", "0", OptiIni);

                // 清除 Full Pack 自带的示例 RTSS 配置，避免影响用户自己的游戏。
                if (ReadIni("Rtss", "Profile", HostIni).Equals("savagewind.exe", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteIniKey("Rtss", "Profile", HostIni);
                    DeleteIniKey("Rtss", "OrigLimit", HostIni);
                    DeleteIniKey("Rtss", "LastInfo", HostIni);
                    DeleteIniKey("Rtss", "LastNote", HostIni);
                }

                string[] output = { "XeFG", "DLSSG", "nofg", "XeFG", "XeFG", "DLSSG", "DLSSG", "DLSSG", "DLSSG", "f5low", "FSRFG" };
                string[] nvngx = { "auto", "Arturs", "auto", "auto", "auto", "Combo", "Nukems", "FFX", "None", "auto", "auto" };
                int mode = c.FrameGenMode;
                WriteIni("FrameGen", "FGOutput", output[mode], OptiIni);
                WriteIni("FrameGen", "FGNvngxReplacement", nvngx[mode], OptiIni);

                bool mfgMode = mode == 0 || mode == 1 || mode == 5 || mode == 8 || mode == 9;
                string interpolation = c.MfgCount == 0 ? "auto" : c.MfgCount.ToString();
                if (mfgMode)
                {
                    WriteIni(mode == 1 || mode == 5 || mode == 8 ? "DLSSG" : "XeFG", "InterpolationCount", interpolation, OptiIni);
                    if (mode == 9)
                    {
                        // F5Low 自带 pacer/presenter，是外置捕获的推荐输出；它使用 [XeFG] 的倍率选项。
                        WriteIni("F5Low", "FrameGenerator", "xefg_tensor", OptiIni);
                        WriteIni("F5Low", "InterpolationCount", interpolation, OptiIni);
                    }
                }

                string[] dx12 = { "xess", "ffx", "dlss", "ffx", "ffx", "ffx" };
                WriteIni("Upscalers", "Dx12Upscaler", dx12[c.UpscalerMode], OptiIni);
                if (c.UpscalerMode == 3)
                {
                    WriteIni("FSR", "UpscalerIndex", "1", OptiIni);
                }
                else if (c.UpscalerMode == 1 || c.UpscalerMode == 5)
                {
                    WriteIni("FSR", "UpscalerIndex", "2", OptiIni);
                }
                else if (c.UpscalerMode == 4)
                {
                    WriteIni("FSR", "UpscalerIndex", "auto", OptiIni);
                    WriteIni("Sharpness", "OverrideSharpness", "true", OptiIni);
                    WriteIni("Sharpness", "Sharpness", "0.600000", OptiIni);
                    WriteIni("DlssNr", "NativeUpscalerPass", "true", OptiIni);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "写入捕获配置失败：" + ex.Message;
                return false;
            }
        }

        private static void MigrateLegacyConfig(CaptureConfig config)
        {
            try
            {
                if (!File.Exists(HostIni)) return;
                string version = ReadIni("General", "ConfigVersion", HostIni);
                if (version == "7")
                {
                    // r7: PresentDelayMs is deliberately user-selectable. Do not rewrite it here.
                    return;
                }
                if (version == "6")
                {
                    // r6 configs already have the F5Low / 3X / Max defaults this build uses.
                    // Keep the user's multiplier and present delay, only advance the version.
                    WriteIni("General", "ConfigVersion", "7", HostIni);
                    return;
                }

                // 旧版升级后可能保留 XeFG + 4X 等测试配置。XeFG 4X 在 165Hz 下
                // 会把原始帧压到约 32fps；F5Low 4X 才是实装前深度测试的默认路径，
                // 同一台机器约 89fps 原始帧、约 356fps 输出（旧 2.12 Host 日志）。
                // 同时旧深度测试日志有 "Performance level 3"（Boost=3）：Max 性能模式
                // 才能保持原始帧。只迁移一次：把旧 XeFG 改成 F5Low，把 r3 误降的
                // 2X/3X 恢复为 4X，Boost 恢复 Max；用户已有 5X/6X 保持，之后不再覆盖。
                string modeText = ReadIni("Settings", "FrameGenMode", HostIni);
                string mfgText = ReadIni("Settings", "MfgCount", HostIni);
                int oldMode = int.TryParse(modeText, out int parsedMode) ? parsedMode : 9;
                int oldMfg = int.TryParse(mfgText, out int parsedMfg) ? parsedMfg : 3;

                if (oldMode == 0)
                    config.FrameGenMode = 9;   // XeFG -> F5Low
                else if (oldMode >= 0 && oldMode <= 10 && oldMode != 3)
                    config.FrameGenMode = oldMode;

                if (config.FrameGenMode == 9)
                    config.MfgCount = oldMfg > 0 ? oldMfg : 2; // keep an explicit 2X..6X choice; default 3X
                else
                    config.MfgCount = oldMfg;

                config.Boost = 3;

                // r7: keep PresentDelayMs as the user left it. The old r6 migration reset it to 0,
                // but 0 did not fix the gameplay case because the base rate is also divided by the
                // frame-generation multiplier (GPU output 122 fps / 3X = 40.5 fps source).
                // CaptureConfig.Normalize only clamps it to 0..8.

                // 立即把迁移后的模式、倍率和性能模式写回宿主 INI，保证界面在点击
                // “开始捕获”前就能显示 F5Low + 3X、Max；完整配置稍后由 Start 写入。
                WriteIni("Settings", "FrameGenMode", config.FrameGenMode.ToString(), HostIni);
                WriteIni("Settings", "MfgCount", config.MfgCount.ToString(), HostIni);
                WriteIni("Advanced", "Boost", "3", HostIni);
                WriteIni("General", "ConfigVersion", "7", HostIni);
            }
            catch
            {
            }
        }

        private static void EnsureEngineDefaults()
        {
            try
            {
                if (!File.Exists(HostIni)) return;
                string profile = ReadIni("Rtss", "Profile", HostIni);
                if (profile.Equals("savagewind.exe", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteIniKey("Rtss", "Profile", HostIni);
                    DeleteIniKey("Rtss", "OrigLimit", HostIni);
                    DeleteIniKey("Rtss", "LastInfo", HostIni);
                    DeleteIniKey("Rtss", "LastNote", HostIni);
                }
            }
            catch
            {
            }
        }

        private static void ResizeWindowForOutputScaling(IntPtr hwnd, CaptureConfig c)
        {
            try
            {
                double factor = c.UpscaleLevel == 0 ? 1.5 : c.UpscaleLevel == 1 ? 1.7 : c.UpscaleLevel == 2 ? 2.0 : 1.0;
                if (factor <= 1.0) return;
                IntPtr monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
                MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(monitor, ref mi)) return;
                int monW = mi.rcMonitor.Right - mi.rcMonitor.Left;
                int monH = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                int w = ((int)(monW / factor)) & ~1;
                int h = ((int)(monH / factor)) & ~1;
                SetWindowPos(hwnd, IntPtr.Zero, mi.rcMonitor.Left, mi.rcMonitor.Top, w, h, 0x0004 | 0x0010); // SWP_NOZORDER | SWP_NOACTIVATE
            }
            catch
            {
            }
        }

        private static void RestoreWindowAfterFailure()
        {
            try
            {
                if (_originalRectValid && _targetHwnd != IntPtr.Zero && IsWindow(_targetHwnd))
                {
                    SetWindowPos(_targetHwnd, IntPtr.Zero, _originalRect.Left, _originalRect.Top,
                        Math.Max(1, _originalRect.Right - _originalRect.Left),
                        Math.Max(1, _originalRect.Bottom - _originalRect.Top),
                        0x0004 | 0x0010);
                    if (_originalMinimized) ShowWindow(_targetHwnd, 6); // SW_MINIMIZE
                }
            }
            catch
            {
            }
            _originalRectValid = false;
            _originalMinimized = false;
        }

        private static IntPtr FindOverlayWindow(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                GetWindowThreadProcessId(h, out uint windowPid);
                if (windowPid != pid) return true;
                StringBuilder cls = new StringBuilder(128);
                GetClassName(h, cls, cls.Capacity);
                if (cls.ToString() == "OptiCaptureHostOverlay")
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static List<CaptureWindowInfo> ListWindows()
        {
            List<CaptureWindowInfo> result = new List<CaptureWindowInfo>();
            int self = Environment.ProcessId;
            EnumWindows((h, l) =>
            {
                try
                {
                    if (!IsWindowVisible(h)) return true;
                    long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
                    if ((ex & WS_EX_TOOLWINDOW) != 0) return true;
                    if (GetWindowTextLength(h) <= 0) return true;
                    GetWindowThreadProcessId(h, out uint pid);
                    if (pid == 0 || pid == (uint)self) return true;
                    if (!GetWindowRect(h, out RECT r)) return true;
                    int w = r.Right - r.Left;
                    int hgt = r.Bottom - r.Top;
                    if (w < 200 || hgt < 120) return true;

                    StringBuilder cls = new StringBuilder(128);
                    GetClassName(h, cls, cls.Capacity);
                    string className = cls.ToString();
                    if (className == "Progman" || className == "Shell_TrayWnd" || className == "WorkerW") return true;

                    string exe = GetProcessExePath(pid);
                    string proc = exe.Length > 0 ? Path.GetFileNameWithoutExtension(exe) : SafeProcessName(pid);
                    string file = Path.GetFileName(exe);
                    if (file.Equals("OptiCaptureHost.exe", StringComparison.OrdinalIgnoreCase)
                        || file.Equals("OptiCaptureLauncher.exe", StringComparison.OrdinalIgnoreCase)
                        || file.Equals("WebUI_CaptureBeta.exe", StringComparison.OrdinalIgnoreCase)
                        || file.Equals("WebUI_Beta.exe", StringComparison.OrdinalIgnoreCase)) return true;
                    if (proc.Equals("explorer", StringComparison.OrdinalIgnoreCase)
                        || proc.Equals("searchhost", StringComparison.OrdinalIgnoreCase)
                        || proc.Equals("shellexperiencehost", StringComparison.OrdinalIgnoreCase)
                        || proc.Equals("startmenuexperiencehost", StringComparison.OrdinalIgnoreCase)
                        || proc.Equals("textinputhost", StringComparison.OrdinalIgnoreCase)) return true;

                    result.Add(new CaptureWindowInfo
                    {
                        Hwnd = h.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Pid = pid,
                        ProcessName = proc,
                        Title = WindowTitle(h),
                        ExePath = exe,
                        Width = w,
                        Height = hgt,
                        Minimized = IsIconic(h)
                    });
                }
                catch
                {
                }
                return true;
            }, IntPtr.Zero);

            // 同一进程只保留面积最大的主窗口，减少浏览器/启动器多窗口带来的噪音。
            return result
                .OrderByDescending(x => x.Width * (long)x.Height)
                .GroupBy(x => x.Pid)
                .Select(g => g.First())
                .OrderBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string SafeProcessName(uint pid)
        {
            try { return Process.GetProcessById((int)pid).ProcessName; } catch { return "unknown"; }
        }

        private static string GetProcessExePath(uint pid)
        {
            IntPtr process = IntPtr.Zero;
            try
            {
                process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (process == IntPtr.Zero) return "";
                StringBuilder sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (QueryFullProcessImageName(process, 0, sb, ref size)) return sb.ToString();
            }
            catch
            {
            }
            finally
            {
                if (process != IntPtr.Zero) CloseHandle(process);
            }
            return "";
        }

        private static string WindowTitle(IntPtr hwnd)
        {
            try
            {
                StringBuilder sb = new StringBuilder(512);
                GetWindowText(hwnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static string ReadVersion(string file)
        {
            try
            {
                return File.Exists(file) ? FileVersionInfo.GetVersionInfo(file).ProductVersion ?? "" : "";
            }
            catch
            {
                return "";
            }
        }

        private static void ReadLiveStatus(CaptureSnapshot s)
        {
            try
            {
                if (!File.Exists(StatusFile)) return;
                Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(StatusFile))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                if (!map.TryGetValue("stamp", out string? stampText) || !ulong.TryParse(stampText, out ulong stamp)) return;
                ulong now = (ulong)Environment.TickCount64;
                if (stamp == 0 || now < stamp || now - stamp > 3000) return;

                s.StatusFresh = true;
                s.Pid = ParseInt(map, "pid", s.Pid);
                s.RefreshHz = ParseDouble(map, "refresh");
                s.CaptureFps = ParseDouble(map, "capture");
                s.PresentFps = ParseDouble(map, "present");
                s.DxgiFps = ParseDouble(map, "dxgi");
                s.PresentMs = ParseDouble(map, "presentms");
                s.WaitMs = ParseDouble(map, "waitms");
                s.Interval = ParseInt(map, "interval");
                s.Focus = ParseInt(map, "focus");
                s.CapHint = ParseInt(map, "caphint");
                s.CapFps = ParseDouble(map, "capfps");
                s.DepthState = ParseInt(map, "depth");
                s.DepthWidth = (uint)ParseInt(map, "depthw");
                s.DepthHeight = (uint)ParseInt(map, "depthh");
            }
            catch
            {
            }
        }

        private static int ParseInt(Dictionary<string, string> map, string key, int fallback = 0)
        {
            return map.TryGetValue(key, out string? value) && int.TryParse(value, out int parsed) ? parsed : fallback;
        }

        private static double ParseDouble(Dictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out string? value)
                && double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsed)
                ? parsed : 0;
        }

        private static string ReadIni(string section, string key, string path)
        {
            try
            {
                string current = "";
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        current = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }
                    if (!current.Equals(section, StringComparison.OrdinalIgnoreCase)) continue;
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring(eq + 1).Trim();
                    }
                }
            }
            catch
            {
            }
            return "";
        }

        private static void WriteIni(string section, string key, string value, string path)
        {
            WritePrivateProfileStringW(section, key, value, path);
        }

        private static void DeleteIniKey(string section, string key, string path)
        {
            WritePrivateProfileStringW(section, key, null, path);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
