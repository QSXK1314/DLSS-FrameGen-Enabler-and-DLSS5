using System.Text.RegularExpressions;
using DLSSFrameGenEnabler.Models;
using Microsoft.Win32;

namespace DLSSFrameGenEnabler.Services;

/// <summary>
/// 游戏库扫描引擎：自动扫描 Steam、Epic、EA、育碧、GOG 已安装游戏
/// </summary>
public class GameScanner
{
    /// <summary>要查找的 DLSS 多帧生成 DLL 文件名</summary>
    public const string DlssgDllName = "nvngx_dlssg.dll";

    /// <summary>
    /// 扫描所有平台的游戏
    /// </summary>
    public List<GameInfo> ScanAllGames(IProgress<string>? progress = null)
    {
        var games = new List<GameInfo>();

        progress?.Report("正在扫描 Steam 游戏库...");
        var steamGames = ScanSteamGames();
        games.AddRange(steamGames);
        progress?.Report($"Steam 找到 {steamGames.Count} 个游戏");

        progress?.Report("正在扫描 Epic 游戏库...");
        var epicGames = ScanEpicGames();
        games.AddRange(epicGames);
        progress?.Report($"Epic 找到 {epicGames.Count} 个游戏");

        progress?.Report("正在扫描 EA 游戏库...");
        var eaGames = ScanEaGames();
        games.AddRange(eaGames);
        progress?.Report($"EA 找到 {eaGames.Count} 个游戏");

        progress?.Report("正在扫描育碧游戏库...");
        var ubisoftGames = ScanUbisoftGames();
        games.AddRange(ubisoftGames);
        progress?.Report($"育碧 找到 {ubisoftGames.Count} 个游戏");

        progress?.Report("正在扫描 GOG 游戏库...");
        var gogGames = ScanGogGames();
        games.AddRange(gogGames);
        progress?.Report($"GOG 找到 {gogGames.Count} 个游戏");

        // 去重（按安装路径）
        var unique = new Dictionary<string, GameInfo>();
        foreach (var g in games)
        {
            var key = g.InstallPath.TrimEnd('\\').ToLowerInvariant();
            if (!unique.ContainsKey(key))
                unique[key] = g;
        }

        progress?.Report($"共找到 {unique.Count} 个游戏，正在检测 DLSS 支持...");

        // 检测每个游戏：所有游戏都保留，标记是否支持帧生成（有 nvngx_dlssg.dll）
        // 但必须找到真正的游戏运行 exe 才能添加（排除卸载后留下的空文件夹）
        var result = new List<GameInfo>();
        foreach (var g in unique.Values)
        {
            try
            {
                var dlssgPath = FindCorrectDlssgDll(g.InstallPath);
                var gameExePath = FindGameExe(g.InstallPath);

                // 如果找不到真正的游戏运行 exe，跳过（可能是卸载后留下的空文件夹，或者不是游戏）
                if (string.IsNullOrEmpty(gameExePath))
                    continue;

                if (dlssgPath != null)
                {
                    // 支持帧生成的游戏
                    g.DlssgDllPath = dlssgPath;
                    g.SupportsFrameGen = true;
                }
                else
                {
                    // 不支持帧生成的游戏，但可能支持 DLSS5
                    g.SupportsFrameGen = false;
                }

                g.GameExePath = gameExePath;
                g.IsPatched = CheckAlreadyPatched(g);
                g.IsAdvancedPatched = CheckAlreadyAdvancedPatched(g);
                g.IsCyberpunkPatched = CheckAlreadyCyberpunkPatched(g);
                g.IsDLSS5Patched = FilePatcher.IsDLSS5Enabled(g);
                g.DLSS5GpuType = FilePatcher.DetectDLSS5GpuType(g);
                result.Add(g);
            }
            catch
            {
                // 忽略无权限访问的目录
            }
        }

        progress?.Report($"检测完成，共 {result.Count} 个游戏（其中 {result.Count(x => x.SupportsFrameGen)} 个支持帧生成）");
        return result.OrderBy(x => x.Name).ToList();
    }

    /// <summary>
    /// 扫描单个用户指定的目录
    /// </summary>
    public GameInfo? ScanSingleDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        var dlssgPath = FindCorrectDlssgDll(directory);
        var gameExePath = FindGameExe(directory);

        // 必须找到真正的游戏运行 exe 才能添加（排除空文件夹和非游戏目录）
        if (string.IsNullOrEmpty(gameExePath))
            return null;

        var game = new GameInfo
        {
            Name = new DirectoryInfo(directory).Name,
            InstallPath = directory,
            DlssgDllPath = dlssgPath,
            SupportsFrameGen = dlssgPath != null,
            GameExePath = gameExePath,
            Source = "手动选择"
        };
        game.IsPatched = CheckAlreadyPatched(game);
        game.IsAdvancedPatched = CheckAlreadyAdvancedPatched(game);
        game.IsCyberpunkPatched = CheckAlreadyCyberpunkPatched(game);
        game.IsDLSS5Patched = FilePatcher.IsDLSS5Enabled(game);
        game.DLSS5GpuType = FilePatcher.DetectDLSS5GpuType(game);
        return game;
    }

    #region Steam 扫描

    private List<GameInfo> ScanSteamGames()
    {
        var games = new List<GameInfo>();
        string? steamPath = null;

        // 从注册表获取 Steam 安装路径
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            steamPath = key?.GetValue("InstallPath") as string;
        }
        catch { }

        if (string.IsNullOrEmpty(steamPath))
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
                steamPath = key?.GetValue("SteamPath") as string;
            }
            catch { }
        }

        if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
            return games;

        // 读取 libraryfolders.vdf 获取所有库目录
        var libraryFolders = new List<string> { Path.Combine(steamPath, "steamapps", "common") };
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

        if (File.Exists(vdfPath))
        {
            try
            {
                var vdfContent = File.ReadAllText(vdfPath);
                // 匹配 "path"		"..."
                var matches = Regex.Matches(vdfContent, @"\""path\""\s+\""([^\""]+)\""");
                foreach (Match m in matches)
                {
                    var libPath = m.Groups[1].Value.Replace("\\\\", "\\");
                    var commonPath = Path.Combine(libPath, "steamapps", "common");
                    if (Directory.Exists(commonPath) && !libraryFolders.Contains(commonPath))
                        libraryFolders.Add(commonPath);
                }
            }
            catch { }
        }

        // 遍历每个库目录下的游戏文件夹
        foreach (var lib in libraryFolders)
        {
            if (!Directory.Exists(lib)) continue;
            try
            {
                foreach (var gameDir in Directory.GetDirectories(lib))
                {
                    var dirName = Path.GetFileName(gameDir);
                    // 跳过备份目录
                    if (IsBackupDirectory(dirName)) continue;
                    games.Add(new GameInfo
                    {
                        Name = dirName,
                        InstallPath = gameDir,
                        Source = "Steam"
                    });
                }
            }
            catch { }
        }

        return games;
    }

    #endregion

    #region Epic 扫描

    private List<GameInfo> ScanEpicGames()
    {
        var games = new List<GameInfo>();

        // Epic Games Launcher 的清单数据目录
        var epicDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (!Directory.Exists(epicDataPath))
            return games;

        try
        {
            foreach (var manifestFile in Directory.GetFiles(epicDataPath, "*.item"))
            {
                try
                {
                    var content = File.ReadAllText(manifestFile);
                    var displayName = Regex.Match(content, @"\""DisplayName\""\s*:\s*\""([^\""]*)\""").Groups[1].Value;
                    var installLocation = Regex.Match(content, @"\""InstallLocation\""\s*:\s*\""([^\""]*)\""").Groups[1].Value;
                    installLocation = installLocation.Replace("\\\\", "\\");

                    if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                    {
                        games.Add(new GameInfo
                        {
                            Name = displayName,
                            InstallPath = installLocation,
                            Source = "Epic"
                        });
                    }
                }
                catch { }
            }
        }
        catch { }

        return games;
    }

    #endregion

    #region EA 扫描

    /// <summary>
    /// 扫描 EA Desktop 已安装游戏
    /// </summary>
    private List<GameInfo> ScanEaGames()
    {
        var games = new List<GameInfo>();
        var gameDirs = new List<string>();

        // 从注册表获取 EA Desktop 安装路径
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EA Desktop");
            var installPath = key?.GetValue("InstallDir") as string;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                var gamesPath = Path.Combine(installPath, "Games");
                if (Directory.Exists(gamesPath))
                    gameDirs.Add(gamesPath);
            }
        }
        catch { }

        // 尝试 WOW6432Node
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\EA Desktop");
            var installPath = key?.GetValue("InstallDir") as string;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                var gamesPath = Path.Combine(installPath, "Games");
                if (Directory.Exists(gamesPath) && !gameDirs.Contains(gamesPath))
                    gameDirs.Add(gamesPath);
            }
        }
        catch { }

        // 默认安装路径
        var defaultPaths = new[]
        {
            @"C:\Program Files\EA Games",
            @"C:\Program Files (x86)\EA Games",
            @"D:\EA Games",
            @"E:\EA Games"
        };
        foreach (var p in defaultPaths)
        {
            if (Directory.Exists(p) && !gameDirs.Contains(p))
                gameDirs.Add(p);
        }

        // 遍历每个游戏目录
        foreach (var gamesDir in gameDirs)
        {
            try
            {
                foreach (var gameDir in Directory.GetDirectories(gamesDir))
                {
                    var dirName = Path.GetFileName(gameDir);
                    if (IsBackupDirectory(dirName)) continue;
                    games.Add(new GameInfo
                    {
                        Name = dirName,
                        InstallPath = gameDir,
                        Source = "EA"
                    });
                }
            }
            catch { }
        }

        return games;
    }

    #endregion

    #region 育碧 扫描

    /// <summary>
    /// 扫描育碧 Uplay/Connect 已安装游戏
    /// </summary>
    private List<GameInfo> ScanUbisoftGames()
    {
        var games = new List<GameInfo>();
        var gameDirs = new List<string>();

        // 从注册表获取育碧安装路径
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher");
            var installPath = key?.GetValue("InstallDir") as string;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                var gamesPath = Path.Combine(installPath, "games");
                if (Directory.Exists(gamesPath))
                    gameDirs.Add(gamesPath);
            }
        }
        catch { }

        // 尝试 CurrentUser
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher");
            var installPath = key?.GetValue("InstallDir") as string;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                var gamesPath = Path.Combine(installPath, "games");
                if (Directory.Exists(gamesPath) && !gameDirs.Contains(gamesPath))
                    gameDirs.Add(gamesPath);
            }
        }
        catch { }

        // 默认安装路径
        var defaultPaths = new[]
        {
            @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games",
            @"C:\Program Files\Ubisoft\Ubisoft Game Launcher\games",
            @"D:\Ubisoft\games",
            @"E:\Ubisoft\games"
        };
        foreach (var p in defaultPaths)
        {
            if (Directory.Exists(p) && !gameDirs.Contains(p))
                gameDirs.Add(p);
        }

        // 遍历每个游戏目录
        foreach (var gamesDir in gameDirs)
        {
            try
            {
                foreach (var gameDir in Directory.GetDirectories(gamesDir))
                {
                    var dirName = Path.GetFileName(gameDir);
                    if (IsBackupDirectory(dirName)) continue;
                    games.Add(new GameInfo
                    {
                        Name = dirName,
                        InstallPath = gameDir,
                        Source = "育碧"
                    });
                }
            }
            catch { }
        }

        return games;
    }

    #endregion

    #region GOG 扫描

    /// <summary>
    /// 扫描 GOG Galaxy 已安装游戏
    /// </summary>
    private List<GameInfo> ScanGogGames()
    {
        var games = new List<GameInfo>();
        var gameDirs = new List<string>();

        // 从注册表获取 GOG Galaxy 安装路径
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Galaxy");
            var installPath = key?.GetValue("installPath") as string;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                var gamesPath = Path.Combine(installPath, "Games");
                if (Directory.Exists(gamesPath))
                    gameDirs.Add(gamesPath);
            }
        }
        catch { }

        // 尝试 CurrentUser
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\GOG.com\Galaxy");
            var installPath = key?.GetValue("installPath") as string;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                var gamesPath = Path.Combine(installPath, "Games");
                if (Directory.Exists(gamesPath) && !gameDirs.Contains(gamesPath))
                    gameDirs.Add(gamesPath);
            }
        }
        catch { }

        // 默认安装路径
        var defaultPaths = new[]
        {
            @"C:\Program Files (x86)\GOG Galaxy\Games",
            @"C:\Program Files\GOG Galaxy\Games",
            @"D:\GOG Galaxy\Games",
            @"E:\GOG Galaxy\Games"
        };
        foreach (var p in defaultPaths)
        {
            if (Directory.Exists(p) && !gameDirs.Contains(p))
                gameDirs.Add(p);
        }

        // 遍历每个游戏目录
        foreach (var gamesDir in gameDirs)
        {
            try
            {
                foreach (var gameDir in Directory.GetDirectories(gamesDir))
                {
                    var dirName = Path.GetFileName(gameDir);
                    if (IsBackupDirectory(dirName)) continue;
                    games.Add(new GameInfo
                    {
                        Name = dirName,
                        InstallPath = gameDir,
                        Source = "GOG"
                    });
                }
            }
            catch { }
        }

        return games;
    }

    #endregion

    #region 通用文件查找

    /// <summary>
    /// 在目录中递归查找指定文件（最多深入 8 层，避免过慢）
    /// </summary>
    public static string? FindFileInDirectory(string rootDir, string fileName)
    {
        if (!Directory.Exists(rootDir)) return null;

        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootDir, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > 8) continue;

            try
            {
                // 检查当前目录
                var targetPath = Path.Combine(current, fileName);
                if (File.Exists(targetPath))
                    return targetPath;

                // 子目录入队
                foreach (var subDir in Directory.GetDirectories(current))
                {
                    var dirName = Path.GetFileName(subDir).ToLowerInvariant();
                    // 跳过明显无关的目录
                    if (dirName is "$recycle.bin" or "system volume information" or "node_modules" or ".git")
                        continue;
                    queue.Enqueue((subDir, depth + 1));
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// 查找正确的 nvngx_dlssg.dll 路径（优先选择路径中包含 Nvidia 的目录）
    /// 有些游戏（如异环）有两个 nvngx_dlssg.dll，只有放在 Nvidia 目录下的才有效
    /// </summary>
    public static string? FindCorrectDlssgDll(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return null;

        var allDlssg = new List<string>();
        FindFilesRecursive(rootDir, DlssgDllName, allDlssg);
        if (allDlssg.Count == 0) return null;
        if (allDlssg.Count == 1) return allDlssg[0];

        // 优先选择路径中包含 "Nvidia" 的
        var nvidiaPath = allDlssg.FirstOrDefault(p =>
            p.Contains("Nvidia", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Streamline", StringComparison.OrdinalIgnoreCase));
        if (nvidiaPath != null) return nvidiaPath;

        // 其次选择路径更深的（通常在 Plugins/Nvidia 下的路径更深）
        return allDlssg.OrderByDescending(p => p.Count(c => c == '\\')).First();
    }

    /// <summary>
    /// 递归查找指定文件（简单版本，用于查找 nvngx_dlssg.dll）
    /// </summary>
    private static void FindFilesRecursive(string rootDir, string fileName, List<string> result)
    {
        try
        {
            var targetPath = Path.Combine(rootDir, fileName);
            if (File.Exists(targetPath))
                result.Add(targetPath);

            foreach (var subDir in Directory.GetDirectories(rootDir))
            {
                FindFilesRecursive(subDir, fileName, result);
            }
        }
        catch { }
    }

    /// <summary>
    /// 判断目录名是否为备份目录（包含 backup、_backup、备份等关键词）
    /// </summary>
    private static bool IsBackupDirectory(string dirName)
    {
        if (string.IsNullOrEmpty(dirName)) return false;
        var lower = dirName.ToLowerInvariant();
        return lower.Contains("backup") ||
               lower.Contains("_backup") ||
               lower.Contains("备份") ||
               lower.Contains("bak") ||
               lower.Contains(".old");
    }

    /// <summary>
    /// 判断一个 exe 是否可能是真正的游戏运行程序（排除卸载程序、安装程序、启动器等）
    /// </summary>
    private static bool IsLikelyGameExe(string exePath)
    {
        var name = Path.GetFileName(exePath).ToLowerInvariant();

        // 明确排除的非游戏程序关键词
        // 注意：
        // 1. 不要添加 "x64"、"x86"、"win64" 等关键词，因为很多游戏exe文件名包含这些（如 XXX-Win64-Shipping.exe）
        // 2. 不要添加太短的关键词（如 "be"、"eac"），会误过滤很多游戏名（如 BETGameSteam 包含 "be"）
        // 3. 不要添加平台相关关键词（如 "steam"、"epic"），很多游戏exe文件名包含这些（如 XXXSteam.exe）
        // 4. 反作弊程序使用完整名称匹配
        string[] excludeKeywords =
        {
            "unins", "uninstall", "setup", "install", "installer",
            "crash", "report", "config", "configuration", "settings",
            "launcher", "update", "updater", "patch", "patcher",
            "dxwebsetup", "vcredist", "vc_redist", "redist", "dotnet",
            "ue4prereq", "prereq", "dxsetup", "directx", "physx",
            "easyanticheat", "battleye", "anticheat",
            "benchmark", "demo", "tool", "editor", "sdk",
            "debug", "release", "test", "helper", "service",
            "webhelper", "overlay", "notification", "tray",
            "nvidia", "amd", "driver",
            "unitycrashhandler", "crashhandler",
            "microsoft", "windows", "system", "runtime",
            "compiler", "captioncompiler", "studiomdl", "vpk",
            "hlmv", "mdlcompiler", "shadercompiler", "resourcecompiler",
            "faceposer", "sourcetv", "hammer", "vrad", "vvis", "vbsp",
            "elementviewer", "sdklauncher", "vconfig", "mksheet",
            "qc_eyes", "dmxconvert", "sfmgen", "height2normal",
            "normal2height", "bumpgenerator", "texturecompile",
            "shadercompile", "vmpi", "vtex", "vfont", "videofunctions"
        };

        foreach (var keyword in excludeKeywords)
        {
            if (name.Contains(keyword))
                return false;
        }

        // 排除太小的 exe（小于 100KB 的通常不是游戏主程序）
        try
        {
            var fileInfo = new FileInfo(exePath);
            if (fileInfo.Length < 100 * 1024) // 小于 100KB
                return false;
        }
        catch
        {
            // 无法获取文件大小，不排除
        }

        return true;
    }

    /// <summary>
    /// 从 exe 列表中筛选出可能是游戏运行程序的 exe
    /// 注意：过滤后为空时返回空数组，不会回退到原数组（避免把卸载程序等非游戏程序当成游戏 exe）
    /// </summary>
    private static string[] FilterGameExes(string[] exes)
    {
        return exes.Where(IsLikelyGameExe).ToArray();
    }

    /// <summary>
    /// 查找游戏真正的运行 exe（优先 Binaries\Win64 目录下的 Shipping exe，排除 Engine 目录）
    /// </summary>
    public static string? FindGameExe(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return null;

        // 策略1：直接找 Binaries\Win64 目录（UE游戏），优先排除 Engine 目录
        // 支持Win64、Win64r、Win64_Shipping等变体目录名
        var win64Dirs = FindDirectoriesByPrefix(rootDir, "Win64");
        // 优先找非 Engine 目录下的 Win64（Engine 目录下的通常是引擎 exe，不是游戏 exe）
        // 但某些游戏（如燕云十六声）的真正 exe 就在 Engine 目录下，所以如果非 Engine 目录找不到，再找 Engine 目录的
        var nonEngineWin64 = win64Dirs.Where(d => !d.Contains("\\Engine\\", StringComparison.OrdinalIgnoreCase)).ToList();
        var engineWin64 = win64Dirs.Where(d => d.Contains("\\Engine\\", StringComparison.OrdinalIgnoreCase)).ToList();

        // 先找非 Engine 目录的
        foreach (var dir in nonEngineWin64)
        {
            var exes = FilterGameExes(Directory.GetFiles(dir, "*.exe"));
            var shipping = exes.FirstOrDefault(e =>
                Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase));
            if (shipping != null) return shipping;
            if (exes.Length > 0) return exes[0];
        }

        // 非 Engine 目录找不到，再找 Engine 目录的（燕云十六声等特殊游戏）
        foreach (var dir in engineWin64)
        {
            var exes = FilterGameExes(Directory.GetFiles(dir, "*.exe"));
            var shipping = exes.FirstOrDefault(e =>
                Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase));
            if (shipping != null) return shipping;
            if (exes.Length > 0) return exes[0];
        }

        // 策略1.5：找 bin\x64 目录（如赛博朋克2077）
        var binX64Dirs = FindDirectoriesByName(rootDir, "x64");
        foreach (var dir in binX64Dirs)
        {
            // 只处理 bin 目录下的 x64
            var parentName = Path.GetFileName(Path.GetDirectoryName(dir))?.ToLowerInvariant();
            if (parentName != "bin") continue;

            var exes = FilterGameExes(Directory.GetFiles(dir, "*.exe"));
            if (exes.Length > 0) return exes[0];
        }

        // 策略2：递归找所有 exe，优先选名字最大/最像游戏主程序的
        var allExes = new List<string>();
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootDir, 0));
        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > 6) continue;
            try
            {
                allExes.AddRange(Directory.GetFiles(current, "*.exe"));
                foreach (var sub in Directory.GetDirectories(current))
                    queue.Enqueue((sub, depth + 1));
            }
            catch { }
        }

        if (allExes.Count == 0) return null;

        // 过滤掉常见的非游戏主程序（使用统一的过滤逻辑）
        var filtered = allExes.Where(IsLikelyGameExe).ToList();

        // 注意：过滤后为空时返回 null，不会回退到原数组（避免把卸载程序等非游戏程序当成游戏 exe）
        if (filtered.Count == 0) return null;

        // 优先选不在 Engine/Extras/Redist 等目录下的 Shipping exe，然后选名字最长的（通常是主程序）
        return filtered
            .OrderBy(e => e.Contains("\\Engine\\", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(e => e.Contains("\\Extras\\", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(e => e.Contains("\\Redist\\", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(e => e.Contains("\\ThirdParty\\", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(e => e.Contains("\\Prereq\\", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(e => Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(e => Path.GetFileName(e).Length)
            .First();
    }

    /// <summary>
    /// 递归查找指定名称的目录
    /// </summary>
    private static List<string> FindDirectoriesByName(string rootDir, string dirName)
    {
        var result = new List<string>();
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootDir, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > 8) continue;

            try
            {
                foreach (var sub in Directory.GetDirectories(current))
                {
                    if (Path.GetFileName(sub).Equals(dirName, StringComparison.OrdinalIgnoreCase))
                        result.Add(sub);
                    queue.Enqueue((sub, depth + 1));
                }
            }
            catch { }
        }

        return result;
    }

    /// <summary>
    /// 递归查找以指定前缀开头的目录（如Win64、Win64r、Win64_Shipping等）
    /// </summary>
    private static List<string> FindDirectoriesByPrefix(string rootDir, string prefix)
    {
        var result = new List<string>();
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootDir, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > 8) continue;

            try
            {
                foreach (var sub in Directory.GetDirectories(current))
                {
                    var dirName = Path.GetFileName(sub);
                    if (dirName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        result.Add(sub);
                    queue.Enqueue((sub, depth + 1));
                }
            }
            catch { }
        }

        return result;
    }

    /// <summary>
    /// 常见的配置名列表（配置名修改功能支持的名称）
    /// </summary>
    private static readonly string[] ConfigNames = new[]
    {
        "version", "dinput8", "d3d11", "winmm", "d3d9", "winhttp",
        "wininet", "dsound", "binkw64", "xinput1_3", "bink2w64",
        "xinput1_4", "xinputuap", "d3d12"
    };

    /// <summary>
    /// 检查游戏是否已经打过补丁（经典模式）
    /// 支持配置名修改后的检测（如 version.dll 改成 dinput8.dll）
    /// </summary>
    public static bool CheckAlreadyPatched(GameInfo game)
    {
        if (string.IsNullOrEmpty(game.GameExeDirectory)) return false;

        // 经典模式的核心特征：RTX40MFG.asi 必须存在
        var asiPath = Path.Combine(game.GameExeDirectory, "RTX40MFG.asi");
        if (!File.Exists(asiPath)) return false;

        // 配置文件存在（经典模式特有）
        var configPath = Path.Combine(game.GameExeDirectory, "RTX40MFG_config.json");
        if (File.Exists(configPath)) return true;

        // 检查是否存在任何常见配置名的 dll（version.dll 或修改后的名称）
        foreach (var name in ConfigNames)
        {
            var dllPath = Path.Combine(game.GameExeDirectory, name + ".dll");
            if (File.Exists(dllPath)) return true;
        }

        return false;
    }

    /// <summary>
    /// 检查游戏是否已经打过高级模式补丁
    /// 支持配置名修改后的检测（如 version.ini 改成 dinput8.ini）
    /// </summary>
    public static bool CheckAlreadyAdvancedPatched(GameInfo game)
    {
        if (string.IsNullOrEmpty(game.GameExeDirectory)) return false;

        // 高级模式的核心特征：RTX40MFGCore.dll 必须存在
        var mfgCore = Path.Combine(game.GameExeDirectory, "RTX40MFGCore.dll");
        if (!File.Exists(mfgCore)) return false;

        // 检查是否存在任何常见配置名的 ini（version.ini 或修改后的名称）
        foreach (var name in ConfigNames)
        {
            var iniPath = Path.Combine(game.GameExeDirectory, name + ".ini");
            if (File.Exists(iniPath)) return true;
        }

        // 也检查 dll（高级模式也有 version.dll 或修改后的 dll）
        foreach (var name in ConfigNames)
        {
            var dllPath = Path.Combine(game.GameExeDirectory, name + ".dll");
            if (File.Exists(dllPath)) return true;
        }

        return false;
    }

    /// <summary>
    /// 检查游戏是否已经打过2077专用补丁
    /// </summary>
    public static bool CheckAlreadyCyberpunkPatched(GameInfo game)
    {
        if (string.IsNullOrEmpty(game.GameExeDirectory)) return false;
        // 2077专用补丁的特征：plugins\cyber_engine_tweaks.asi 存在
        var cetAsi = Path.Combine(game.GameExeDirectory, "plugins", "cyber_engine_tweaks.asi");
        var rtx40mfgAsi = Path.Combine(game.GameExeDirectory, "plugins", "RTX40MFG.asi");
        return File.Exists(cetAsi) && File.Exists(rtx40mfgAsi);
    }

    /// <summary>
    /// 检查游戏是否已经打过RE引擎多帧生成补丁
    /// 特征文件：dlssg_sm86.ini（RE引擎多帧生成补丁特有）
    /// </summary>
    public static bool CheckAlreadyREFrameGenPatched(GameInfo game)
    {
        if (string.IsNullOrEmpty(game.GameExeDirectory)) return false;
        // RE引擎多帧生成补丁的特征：dlssg_sm86.ini 存在
        var iniPath = Path.Combine(game.GameExeDirectory, "dlssg_sm86.ini");
        return File.Exists(iniPath);
    }

    #endregion
}
