using System.Text.RegularExpressions;
using DLSSFrameGenEnabler.Models;
using Microsoft.Win32;

namespace DLSSFrameGenEnabler.Services;

/// <summary>
/// 游戏库扫描引擎：自动扫描 Steam 和 Epic 已安装游戏
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

        // 去重（按安装路径）
        var unique = new Dictionary<string, GameInfo>();
        foreach (var g in games)
        {
            var key = g.InstallPath.TrimEnd('\\').ToLowerInvariant();
            if (!unique.ContainsKey(key))
                unique[key] = g;
        }

        progress?.Report($"共找到 {unique.Count} 个游戏，正在检测 DLSS 支持...");

        // 检测每个游戏是否包含 nvngx_dlssg.dll，并查找游戏 exe
        var result = new List<GameInfo>();
        foreach (var g in unique.Values)
        {
            try
            {
                var dlssgPath = FindFileInDirectory(g.InstallPath, DlssgDllName);
                if (dlssgPath != null)
                {
                    g.DlssgDllPath = dlssgPath;
                    g.GameExePath = FindGameExe(g.InstallPath);
                    g.IsPatched = CheckAlreadyPatched(g);
                    result.Add(g);
                }
            }
            catch
            {
                // 忽略无权限访问的目录
            }
        }

        progress?.Report($"检测完成，共 {result.Count} 个游戏支持 DLSS 多帧生成");
        return result.OrderBy(x => x.Name).ToList();
    }

    /// <summary>
    /// 扫描单个用户指定的目录
    /// </summary>
    public GameInfo? ScanSingleDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        var dlssgPath = FindFileInDirectory(directory, DlssgDllName);
        if (dlssgPath == null)
            return null;

        var game = new GameInfo
        {
            Name = new DirectoryInfo(directory).Name,
            InstallPath = directory,
            DlssgDllPath = dlssgPath,
            GameExePath = FindGameExe(directory),
            Source = "手动选择"
        };
        game.IsPatched = CheckAlreadyPatched(game);
        return game;
    }

    #region 全盘/分区扫描

    /// <summary>
    /// 扫描所有可用分区中支持 DLSS 的游戏
    /// </summary>
    public List<GameInfo> ScanAllDrives(IProgress<string>? progress = null)
    {
        var games = new List<GameInfo>();
        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .ToList();

        progress?.Report($"发现 {drives.Count} 个本地分区，开始全盘扫描...");

        foreach (var drive in drives)
        {
            try
            {
                var driveGames = ScanDrive(drive.Name, progress);
                games.AddRange(driveGames);
            }
            catch { }
        }

        // 去重
        var unique = new Dictionary<string, GameInfo>();
        foreach (var g in games)
        {
            var key = g.InstallPath.TrimEnd('\\').ToLowerInvariant();
            if (!unique.ContainsKey(key))
                unique[key] = g;
        }

        progress?.Report($"全盘扫描完成，共找到 {unique.Count} 个支持 DLSS 的游戏");
        return unique.Values.OrderBy(x => x.Name).ToList();
    }

    /// <summary>
    /// 扫描指定分区
    /// </summary>
    public List<GameInfo> ScanDrive(string driveRoot, IProgress<string>? progress = null)
    {
        var games = new List<GameInfo>();
        if (!Directory.Exists(driveRoot)) return games;

        progress?.Report($"正在扫描 {driveRoot} ...");

        // 递归查找所有 nvngx_dlssg.dll
        var dlssgFiles = FindAllFilesInDirectory(driveRoot, DlssgDllName, progress);

        foreach (var dlssgPath in dlssgFiles)
        {
            try
            {
                // 推断游戏根目录
                var gameRoot = InferGameRootDirectory(dlssgPath);
                if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
                    gameRoot = Directory.GetParent(dlssgPath)?.FullName ?? dlssgPath;

                // 去重检查
                var key = gameRoot.TrimEnd('\\').ToLowerInvariant();
                if (games.Any(g => g.InstallPath.TrimEnd('\\').ToLowerInvariant() == key))
                    continue;

                // 验证是否为游戏（排除非游戏软件）
                var (isGame, gameExe) = VerifyIsGame(gameRoot);
                if (!isGame)
                {
                    progress?.Report($"跳过非游戏目录：{gameRoot}");
                    continue;
                }

                var game = new GameInfo
                {
                    Name = new DirectoryInfo(gameRoot).Name,
                    InstallPath = gameRoot,
                    DlssgDllPath = dlssgPath,
                    GameExePath = gameExe ?? FindGameExe(gameRoot),
                    Source = "全盘扫描"
                };
                game.IsPatched = CheckAlreadyPatched(game);
                games.Add(game);
            }
            catch { }
        }

        return games;
    }

    /// <summary>
    /// 验证目录是否为游戏目录（排除包含 nvngx_dlssg.dll 但非游戏的软件）
    /// 验证逻辑：
    /// 1. 优先检查 Binaries\Win64 目录下是否有 *Shipping*.exe（UE游戏典型特征）
    /// 2. 其次检查 Binaries\Win64 下是否有其他游戏exe（排除安装/卸载/配置/启动器）
    /// 3. 最后检查根目录下是否有游戏exe
    /// </summary>
    private static (bool IsGame, string? GameExe) VerifyIsGame(string gameRoot)
    {
        if (!Directory.Exists(gameRoot)) return (false, null);

        // 跳过备份目录
        var dirName = Path.GetFileName(gameRoot);
        if (IsBackupDirectory(dirName)) return (false, null);

        // 非游戏exe关键词（安装程序、卸载程序、配置工具、启动器、更新器等）
        var nonGameKeywords = new[] { "unins", "uninstall", "setup", "install", "config",
            "configuration", "launcher", "update", "updater", "crash", "report", "repair",
            "dxwebsetup", "vcredist", "dotnet", "ue4prereq", "easyanticheat", "battleye",
            "redist", "prereq", "tool", "editor", "cooker", "demo", "benchmark", "test" };

        // 策略1：检查 Binaries\Win64 目录
        var win64Dir = Path.Combine(gameRoot, "Binaries", "Win64");
        if (Directory.Exists(win64Dir))
        {
            try
            {
                var exes = Directory.GetFiles(win64Dir, "*.exe");

                // 优先找 *Shipping*.exe（UE游戏的典型主程序命名）
                var shippingExe = exes.FirstOrDefault(e =>
                    Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase));
                if (shippingExe != null)
                    return (true, shippingExe);

                // 找其他游戏exe（排除非游戏程序）
                var gameExe = exes.FirstOrDefault(e =>
                {
                    var name = Path.GetFileName(e).ToLowerInvariant();
                    return !nonGameKeywords.Any(k => name.Contains(k));
                });
                if (gameExe != null)
                    return (true, gameExe);
            }
            catch { }
        }

        // 策略2：检查 Binaries 下的其他子目录（如 Win32、Win64 之外的）
        var binariesDir = Path.Combine(gameRoot, "Binaries");
        if (Directory.Exists(binariesDir))
        {
            try
            {
                foreach (var subDir in Directory.GetDirectories(binariesDir))
                {
                    var exes = Directory.GetFiles(subDir, "*.exe");
                    var shippingExe = exes.FirstOrDefault(e =>
                        Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase));
                    if (shippingExe != null)
                        return (true, shippingExe);

                    var gameExe = exes.FirstOrDefault(e =>
                    {
                        var name = Path.GetFileName(e).ToLowerInvariant();
                        return !nonGameKeywords.Any(k => name.Contains(k));
                    });
                    if (gameExe != null)
                        return (true, gameExe);
                }
            }
            catch { }
        }

        // 策略3：检查根目录下是否有游戏exe（非UE游戏可能直接放在根目录）
        try
        {
            var rootExes = Directory.GetFiles(gameRoot, "*.exe");
            var rootGameExe = rootExes.FirstOrDefault(e =>
            {
                var name = Path.GetFileName(e).ToLowerInvariant();
                // 排除非游戏程序
                if (nonGameKeywords.Any(k => name.Contains(k)))
                    return false;
                // 根目录的exe通常名字比较短，且不包含版本号等
                return name.Length >= 3;
            });
            if (rootGameExe != null)
                return (true, rootGameExe);
        }
        catch { }

        // 都没找到，判定为非游戏
        return (false, null);
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
    /// 递归查找目录中所有指定文件（全盘扫描用，跳过系统目录）
    /// </summary>
    private static List<string> FindAllFilesInDirectory(string rootDir, string fileName, IProgress<string>? progress = null)
    {
        var result = new List<string>();
        if (!Directory.Exists(rootDir)) return result;

        var queue = new Queue<string>();
        queue.Enqueue(rootDir);
        var scanned = 0;

        // 要跳过的目录名（全盘扫描性能优化）
        var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$recycle.bin", "system volume information", "windows", "winnt",
            "program files\\common files", "program files (x86)\\common files",
            "programdata", "appdata", "$windows.~bt", "$windows.~ws",
            "msocache", "perflogs", "recovery", "boot", "efi",
            "node_modules", ".git", ".svn", "__pycache__",
            "packages", "lib", "logs", "cache", "temp", "tmp",
            "_backup", "backup", "backups"
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            scanned++;

            if (scanned % 500 == 0)
                progress?.Report($"已扫描 {scanned} 个目录，正在查找 {fileName}...");

            try
            {
                // 检查当前目录
                var targetPath = Path.Combine(current, fileName);
                if (File.Exists(targetPath))
                    result.Add(targetPath);

                // 子目录入队（跳过系统目录和明显无关的目录）
                foreach (var subDir in Directory.GetDirectories(current))
                {
                    var dirName = Path.GetFileName(subDir).ToLowerInvariant();
                    var fullPathLower = subDir.ToLowerInvariant();

                    // 跳过指定目录名
                    if (skipDirs.Contains(dirName)) continue;

                    // 跳过备份目录（名字包含 backup、_backup、备份等）
                    if (IsBackupDirectory(dirName)) continue;

                    // 跳过路径中包含系统目录的
                    if (fullPathLower.Contains("\\windows\\") ||
                        fullPathLower.Contains("\\program files\\common files\\") ||
                        fullPathLower.Contains("\\program files (x86)\\common files\\"))
                        continue;

                    // 限制路径深度（最多12层）
                    var depth = subDir.Split(Path.DirectorySeparatorChar).Length;
                    if (depth > 15) continue;

                    queue.Enqueue(subDir);
                }
            }
            catch { }
        }

        return result;
    }

    /// <summary>
    /// 根据 nvngx_dlssg.dll 的路径推断游戏根目录
    /// </summary>
    private static string? InferGameRootDirectory(string dlssgPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(dlssgPath);
            if (string.IsNullOrEmpty(dir)) return null;

            // 向上查找包含 Binaries 目录的最近目录（UE游戏的典型结构）
            var current = dir;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(current); i++)
            {
                // 检查当前目录是否有 Binaries
                if (Directory.Exists(Path.Combine(current, "Binaries")))
                    return current;

                // 检查当前目录是否有游戏exe（直接在根目录的情况）
                var exes = Directory.GetFiles(current, "*.exe");
                if (exes.Length > 0 && exes.Any(e => !Path.GetFileName(e).ToLowerInvariant().Contains("unins") &&
                    !Path.GetFileName(e).ToLowerInvariant().Contains("setup") &&
                    !Path.GetFileName(e).ToLowerInvariant().Contains("install")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            // 默认返回上3级目录
            var defaultDir = Path.GetDirectoryName(dlssgPath);
            for (int i = 0; i < 3 && !string.IsNullOrEmpty(defaultDir); i++)
            {
                defaultDir = Directory.GetParent(defaultDir)?.FullName;
            }
            return defaultDir ?? Path.GetDirectoryName(dlssgPath);
        }
        catch
        {
            return Path.GetDirectoryName(dlssgPath);
        }
    }

    #endregion

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
    /// 查找游戏真正的运行 exe（优先 Binaries\Win64 目录下的 Shipping exe）
    /// </summary>
    public static string? FindGameExe(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return null;

        // 策略1：直接找 Binaries\Win64 目录
        var win64Dirs = FindDirectoriesByName(rootDir, "Win64");
        foreach (var dir in win64Dirs)
        {
            // 优先找 *Shipping*.exe
            var exes = Directory.GetFiles(dir, "*.exe");
            var shipping = exes.FirstOrDefault(e =>
                Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase));
            if (shipping != null) return shipping;
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

        // 过滤掉常见的非游戏主程序
        var filtered = allExes.Where(e =>
        {
            var name = Path.GetFileName(e).ToLowerInvariant();
            return !name.Contains("unins") && !name.Contains("setup") &&
                   !name.Contains("install") && !name.Contains("crash") &&
                   !name.Contains("report") && !name.Contains("config") &&
                   !name.Contains("launcher") && !name.Contains("update") &&
                   !name.Contains("dxwebsetup") && !name.Contains("vcredist") &&
                   !name.Contains("dotnet") && !name.Contains("ue4prereq") &&
                   !name.Contains("easyanticheat") && !name.Contains("battleye");
        }).ToList();

        if (filtered.Count == 0) filtered = allExes;

        // 优先选 Shipping，然后选名字最长的（通常是主程序）
        return filtered
            .OrderByDescending(e => Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase))
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
    /// 检查游戏是否已经打过补丁
    /// </summary>
    public static bool CheckAlreadyPatched(GameInfo game)
    {
        if (string.IsNullOrEmpty(game.GameExeDirectory)) return false;
        var asiPath = Path.Combine(game.GameExeDirectory, "RTX40MFG.asi");
        var versionDllPath = Path.Combine(game.GameExeDirectory, "version.dll");
        return File.Exists(asiPath) && File.Exists(versionDllPath);
    }

    #endregion
}
