using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    /// <summary>
    /// 运行中游戏进程检测服务
    /// 完全使用Win32 API实现，模仿任务管理器的"打开文件所在位置"功能
    /// 100%准确获取游戏真正的exe路径
    /// </summary>
    public class ProcessDetector
    {
        #region Win32 API 声明

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS pmc, uint cb);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

        // 窗口相关API
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        #endregion

        /// <summary>
        /// 游戏进程信息
        /// </summary>
        public class GameProcessInfo
        {
            public int ProcessId { get; set; }
            public string ProcessName { get; set; } = "";
            public string WindowTitle { get; set; } = "";
            public string ExePath { get; set; } = "";
            public long MemoryMB { get; set; }
            public string GameName { get; set; } = "";
            public BitmapImage? Icon { get; set; }
        }

        // 需要排除的系统进程/非游戏进程关键词
        private static readonly string[] ExcludeProcessKeywords = new[]
        {
            // 系统核心进程
            "system", "svchost", "csrss", "wininit", "winlogon", "services", "lsass",
            "smss", "dwm", "explorer", "taskhostw", "taskhost", "runtimebroker",
            "sihost", "shellexperiencehost", "startmenuexperiencehost", "searchui",
            "searchhost", "textinputhost", "ctfmon", "conhost", "dllhost",
            "registry", "memory compression", "secure system", "vmmem",
            // 本软件
            "dlssframegenenabler", "dfge",
            // 浏览器
            "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
            "iexplore", "safari", "tor", "chromium", "webkit",
            // 办公软件
            "winword", "excel", "powerpnt", "outlook", "onenote", "visio",
            "wps", "et", "wpp", "wpscenter", "wpscloudsvr",
            "acrobat", "acrord32", "foxitreader", "sumatrapdf",
            // 通讯软件
            "wechat", "wechatapp", "qq", "tim", "dingtalk", "feishu", "lark",
            "skype", "teams", "zoom", "discord", "telegram", "whatsapp",
            "slack", "mattermost", "line", "viber",
            // 开发工具
            "devenv", "msbuild", "dotnet", "code", "vscode", "cursor",
            "idea", "pycharm", "webstorm", "clion", "goland", "rider",
            "androidstudio", "eclipse", "netbeans", "sublime_text", "notepad++",
            "git", "git-bash", "cmd", "powershell", "pwsh", "windowsterminal",
            "wt", "bash", "zsh", "ssh", "scp", "sftp",
            // 下载工具
            "thunder", "xunlei", "idman", "idm", "qbittorrent", "utorrent",
            "bittorrent", "aria2", "motrix", "fdm",
            // 视频播放
            "potplayer", "vlc", "mpc-hc", "mpc-hc64", "kmplayer", "gomplayer",
            "qqplayer", "baiduyunguanjia", "baidunetdisk",
            // 音乐播放
            "cloudmusic", "netease", "qqmusic", "kuwo", "kugou", "spotify",
            "foobar2000", "aimp", "winamp", "itunes",
            // 安全软件
            "360tray", "360sd", "360safe", "huorong", "hipsdaemon", "wsctrl",
            "kav", "kaspersky", "avp", "mcafee", "symantec", "norton",
            "eset", "avast", "avg", "bitdefender",
            // 输入法
            "sogoucloud", "sogouinput", "baiduinput", "qqpinyin", "googlepinyin",
            "microsoftpy", "microsoftpinyin",
            // 其他常见非游戏进程
            "steamwebhelper", "steamerrorreporter", "steamclient", "steamservice",
            "epicwebhelper", "epicgameslauncher", "egstore",
            "origin", "eadesktop", "ubisoftconnect", "uplay", "goggalaxy",
            "battle.net", "battlenet", "blizzard", "agent",
            "rockstargameslauncher", "socialclub", "rgsc",
            "nvcontainer", "nvdisplay.container", "nvidia share",
            "amdrsserver", "amdsoftware", "radeonsoftware",
            "obs64", "obs32", "obs", "xsplit", "bandicam", "fraps",
            "msiafterburner", "rtss", "rivatuner",
            "cpu-z", "gpu-z", "hwinfo", "aida64", "speccy",
            "ccleaner", "revo", "geek", "iobit",
            "everything", "listary", "powertoys", "utools",
            "notion", "obsidian", "typora", "marktext",
            "postman", "insomnia", "fiddler", "wireshark", "charles",
            "photoshop", "illustrator", "premiere", "aftereffects",
            "audition", "bridge", "lightroom", "captureone",
            "3dsmax", "maya", "blender", "cinema4d", "zbrush", "substance",
            "autocad", "solidworks", "catia", "creo", "inventor",
            "matlab", "mathematica", "maple", "originlab",
            "spss", "sas", "stata", "eviews",
            "vmware", "virtualbox", "docker", "wsl",
            "virtualdj", "serato", "traktor", "ableton", "flstudio",
            "cubase", "nuendo", "protools", "studioone", "reaper",
            "handbrake", "formatfactory", "shanaencoder", "megui",
            "desktop", "wallpaper", "wallpaper32", "wallpaper64",
            "rainmeter", "rocketdock", "objectdock", "winstep",
            "f.lux", "redshift", "lightbulb", "careueyes",
            "translucenttb", "taskbarx", "startallback", "starts11",
            "python", "pythonw", "node", "npm", "yarn", "pnpm",
            "java", "javaw", "javaws", "jqs",
            "ruby", "perl", "php", "go", "rustc", "cargo",
            "nginx", "apache", "mysql", "postgres", "mongodb", "redis",
            "docker", "kubectl", "helm", "terraform",
            "vnc", "teamviewer", "anydesk", "向日葵", "todesk",
            "rustdesk", "parsec", "moonlight", "steamlink",
            "prism", "drawio", "xmind", "mindmanager", "mindmaster",
            "evernote", "yinxiang", "notion", "logseq", "joplin",
            "foxmail", "thunderbird", "outlook", "mail",
            "calendar", "calculator", "notepad", "paint", "snippingtool",
            "photos", "camera", "alarms", "clock", "weather", "maps",
            "store", "settings", "actioncenter", "quickassist",
            "magnifier", "narrator", "osk", "easeofaccess",
            "applicationframehost", "backgroundtaskhost", "dashost",
            "fontdrvhost", "mousocoreworker", "remotefx", "sgrmagent",
            "sihost", "spectrum", "spoolsv", "wudfhost", "wmiprvse",
            "unsecapp", "userinit", "w3wp", "wermgr", "werfault",
        };

        // 游戏进程常见关键词（用于优先排序）
        private static readonly string[] GameKeywords = new[]
        {
            "game", "gaming", "play", "player",
            "shipping", "win64", "win32",
            "engine", "unreal", "unity", "cryengine",
            "dx9", "dx11", "dx12", "vulkan", "opengl",
            "dlss", "fsr", "xess", "framegen",
        };

        /// <summary>
        /// 获取exe文件的图标
        /// </summary>
        private static BitmapImage? GetExeIcon(string exePath)
        {
            try
            {
                if (!File.Exists(exePath)) return null;

                var shfi = new SHFILEINFO();
                IntPtr hImg = SHGetFileInfo(exePath, 0, ref shfi,
                    (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                    SHGFI_ICON | SHGFI_SMALLICON);

                if (shfi.hIcon == IntPtr.Zero) return null;

                try
                {
                    using (var icon = Icon.FromHandle(shfi.hIcon))
                    {
                        using (var bitmap = icon.ToBitmap())
                        {
                            using (var ms = new MemoryStream())
                            {
                                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Position = 0;

                                var bitmapImage = new BitmapImage();
                                bitmapImage.SetSource(ms.AsRandomAccessStream());
                                return bitmapImage;
                            }
                        }
                    }
                }
                finally
                {
                    DestroyIcon(shfi.hIcon);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取当前所有可能是游戏的运行进程
        /// 完全使用Win32 API，模仿任务管理器的实现方式
        /// </summary>
        public static List<GameProcessInfo> GetRunningGames()
        {
            var result = new List<GameProcessInfo>();

            // 1. 使用CreateToolhelp32Snapshot枚举所有进程（最稳定的方式）
            IntPtr snapshot = IntPtr.Zero;
            try
            {
                snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
                if (snapshot == IntPtr.Zero) return result;

                var pe32 = new PROCESSENTRY32();
                pe32.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

                if (!Process32First(snapshot, ref pe32)) return result;

                // 获取所有窗口对应的进程ID和标题
                var windowMap = GetWindowMap();

                do
                {
                    try
                    {
                        uint pid = pe32.th32ProcessID;
                        string exeName = pe32.szExeFile ?? "";
                        string procName = Path.GetFileNameWithoutExtension(exeName).ToLowerInvariant();

                        // 排除系统进程和非游戏进程
                        if (IsExcludedProcess(procName)) continue;
                        if (string.IsNullOrEmpty(exeName)) continue;

                        // 获取窗口信息
                        string windowTitle = "";
                        bool hasWindow = false;
                        if (windowMap.TryGetValue(pid, out var winInfo))
                        {
                            windowTitle = winInfo.title;
                            hasWindow = winInfo.visible;
                        }

                        // 使用Win32 API获取进程完整路径
                        string exePath = GetProcessExePath(pid);
                        if (string.IsNullOrEmpty(exePath)) continue;

                        // 排除系统目录下的进程
                        if (exePath.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (exePath.StartsWith(@"C:\Program Files\WindowsApps\", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // 获取内存占用
                        long memoryMB = GetProcessMemoryMB(pid);

                        // 排除没有窗口且内存占用小的后台进程
                        if (!hasWindow && string.IsNullOrEmpty(windowTitle))
                        {
                            if (memoryMB < 100) continue;
                        }

                        // 获取exe图标
                        var icon = GetExeIcon(exePath);

                        var info = new GameProcessInfo
                        {
                            ProcessId = (int)pid,
                            ProcessName = Path.GetFileNameWithoutExtension(exeName),
                            WindowTitle = windowTitle,
                            ExePath = exePath,
                            MemoryMB = memoryMB,
                            GameName = Path.GetFileNameWithoutExtension(exeName),
                            Icon = icon
                        };

                        result.Add(info);
                    }
                    catch
                    {
                        // 忽略任何无法访问的进程
                    }
                } while (Process32Next(snapshot, ref pe32));
            }
            catch
            {
                // 忽略任何错误
            }
            finally
            {
                if (snapshot != IntPtr.Zero)
                {
                    try { CloseHandle(snapshot); } catch { }
                }
            }

            // 排序：优先显示包含游戏关键词的进程，然后按内存占用排序
            return result
                .OrderByDescending(p => GameKeywords.Any(k =>
                    p.ProcessName.ToLowerInvariant().Contains(k) ||
                    p.WindowTitle.ToLowerInvariant().Contains(k)))
                .ThenByDescending(p => p.MemoryMB)
                .ToList();
        }

        /// <summary>
        /// 获取所有可见窗口的进程ID和标题
        /// </summary>
        private static Dictionary<uint, (string title, bool visible)> GetWindowMap()
        {
            var map = new Dictionary<uint, (string, bool)>();
            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        GetWindowThreadProcessId(hWnd, out uint pid);
                        if (pid > 0 && !map.ContainsKey(pid))
                        {
                            var sb = new StringBuilder(512);
                            GetWindowText(hWnd, sb, sb.Capacity);
                            bool visible = IsWindowVisible(hWnd);
                            string title = sb.ToString();
                            map[pid] = (title, visible);
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            return map;
        }

        /// <summary>
        /// 安全获取进程exe路径（使用Win32 API）
        /// </summary>
        private static string? GetProcessExePath(uint processId)
        {
            IntPtr hProcess = IntPtr.Zero;
            try
            {
                hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess == IntPtr.Zero) return null;

                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                {
                    return sb.ToString();
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    try { CloseHandle(hProcess); } catch { }
                }
            }
        }

        /// <summary>
        /// 获取进程内存占用（MB）
        /// </summary>
        private static long GetProcessMemoryMB(uint processId)
        {
            IntPtr hProcess = IntPtr.Zero;
            try
            {
                hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
                if (hProcess == IntPtr.Zero) return 0;

                var pmc = new PROCESS_MEMORY_COUNTERS();
                pmc.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS));
                if (GetProcessMemoryInfo(hProcess, out pmc, pmc.cb))
                {
                    return (long)(pmc.WorkingSetSize.ToUInt64() / (1024 * 1024));
                }
                return 0;
            }
            catch
            {
                return 0;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    try { CloseHandle(hProcess); } catch { }
                }
            }
        }

        /// <summary>
        /// 判断进程是否应该被排除
        /// </summary>
        private static bool IsExcludedProcess(string processName)
        {
            foreach (var keyword in ExcludeProcessKeywords)
            {
                if (processName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查游戏是否正在运行
        /// </summary>
        public static bool IsGameRunning(string exePath)
        {
            try
            {
                var games = GetRunningGames();
                return games.Any(g => g.ExePath.Equals(exePath, StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }
    }
}
