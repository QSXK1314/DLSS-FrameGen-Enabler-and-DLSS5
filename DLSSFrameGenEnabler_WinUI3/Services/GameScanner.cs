using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DLSSFrameGenEnabler_WinUI3.Models;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    public class GameScanner
    {
        // 限制深度的文件搜索（避免遍历整个大目录导致性能问题）
        private static string[] GetFilesWithDepthLimit(string rootPath, string searchPattern, int maxDepth = 3)
        {
            var result = new List<string>();
            try
            {
                // 第一层
                result.AddRange(Directory.GetFiles(rootPath, searchPattern, SearchOption.TopDirectoryOnly));
                if (maxDepth <= 1) return result.ToArray();
                
                // 递归子目录（限制深度）
                SearchFilesRecursive(rootPath, searchPattern, 1, maxDepth, result);
            }
            catch { }
            return result.ToArray();
        }
        
        private static void SearchFilesRecursive(string dir, string pattern, int currentDepth, int maxDepth, List<string> result)
        {
            if (currentDepth >= maxDepth) return;
            try
            {
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    try
                    {
                        result.AddRange(Directory.GetFiles(subDir, pattern, SearchOption.TopDirectoryOnly));
                        if (currentDepth + 1 < maxDepth)
                        {
                            SearchFilesRecursive(subDir, pattern, currentDepth + 1, maxDepth, result);
                        }
                    }
                    catch { } // 跳过无权限的目录
                }
            }
            catch { }
        }
        
        // 限制深度的目录搜索
        private static string[] GetDirectoriesWithDepthLimit(string rootPath, string searchPattern, int maxDepth = 3)
        {
            var result = new List<string>();
            try
            {
                SearchDirectoriesRecursive(rootPath, searchPattern, 0, maxDepth, result);
            }
            catch { }
            return result.ToArray();
        }
        
        private static void SearchDirectoriesRecursive(string dir, string pattern, int currentDepth, int maxDepth, List<string> result)
        {
            if (currentDepth >= maxDepth) return;
            try
            {
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    try
                    {
                        var dirName = Path.GetFileName(subDir);
                        // "*" 匹配所有目录
                        if (pattern == "*" || dirName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(subDir);
                        }
                        SearchDirectoriesRecursive(subDir, pattern, currentDepth + 1, maxDepth, result);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // 扫描Steam库
        public static List<GameInfo> ScanSteamGames()
        {
            var games = new List<GameInfo>();
            try
            {
                // 获取所有Steam库路径（从libraryfolders.vdf读取）
                var steamLibraryPaths = GetSteamLibraryPaths();

                foreach (var steamPath in steamLibraryPaths.Where(Directory.Exists))
                {
                    var commonPath = Path.Combine(steamPath, "steamapps", "common");
                    if (!Directory.Exists(commonPath)) continue;

                    foreach (var gameDir in Directory.GetDirectories(commonPath))
                    {
                        var game = AnalyzeGameDirectory(gameDir);
                        if (game != null) games.Add(game);
                    }
                }
            }
            catch { }
            return games;
        }

        // 从Steam配置文件读取所有Steam库路径
        private static List<string> GetSteamLibraryPaths()
        {
            var paths = new List<string>();
            try
            {
                // 常见的Steam安装路径
                var possibleSteamPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    @"D:\Steam",
                    @"E:\Steam",
                    @"F:\Steam",
                    @"G:\Steam"
                };

                string? steamInstallPath = null;
                foreach (var p in possibleSteamPaths)
                {
                    if (Directory.Exists(p))
                    {
                        steamInstallPath = p;
                        break;
                    }
                }

                // 从注册表读取Steam安装路径
                if (steamInstallPath == null)
                {
                    try
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                        if (key != null)
                        {
                            var installPath = key.GetValue("InstallPath") as string;
                            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                            {
                                steamInstallPath = installPath;
                            }
                        }
                    }
                    catch { }
                }

                if (steamInstallPath == null) return paths;

                // Steam安装目录本身就是一个库
                paths.Add(steamInstallPath);

                // 读取libraryfolders.vdf获取其他库
                var vdfPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    var lines = File.ReadAllLines(vdfPath);
                    foreach (var line in lines)
                    {
                        // 匹配 "path"		"G:\SteamLibrary" 这种格式
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = trimmed.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                var path = parts[1].Trim('"');
                                if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                                {
                                    paths.Add(path);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return paths;
        }

        // 扫描Epic库
        public static List<GameInfo> ScanEpicGames()
        {
            var games = new List<GameInfo>();
            try
            {
                var epicManifests = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (!Directory.Exists(epicManifests)) return games;

                foreach (var manifest in Directory.GetFiles(epicManifests, "*.item"))
                {
                    try
                    {
                        var json = File.ReadAllText(manifest);
                        var installLocation = GetJsonValue(json, "InstallLocation");
                        var displayName = GetJsonValue(json, "DisplayName");
                        if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                        {
                            var game = AnalyzeGameDirectory(installLocation, displayName);
                            if (game != null) games.Add(game);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return games;
        }

        // 扫描GOG库
        public static List<GameInfo> ScanGOGGames()
        {
            var games = new List<GameInfo>();
            try
            {
                // GOG Galaxy的游戏数据库
                var gogDbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "GOG.com", "Galaxy", "storage", "galaxy-2.0.db");

                // 如果没有GOG Galaxy数据库，尝试常见的GOG安装路径
                if (!File.Exists(gogDbPath))
                {
                    var gogPaths = new[]
                    {
                        @"C:\Program Files (x86)\GOG Galaxy\Games",
                        @"C:\Program Files\GOG Galaxy\Games",
                        @"D:\GOG Games",
                        @"E:\GOG Games",
                        @"F:\GOG Games"
                    };

                    foreach (var gogPath in gogPaths.Where(Directory.Exists))
                    {
                        foreach (var gameDir in Directory.GetDirectories(gogPath))
                        {
                            var game = AnalyzeGameDirectory(gameDir);
                            if (game != null) games.Add(game);
                        }
                    }
                    return games;
                }
            }
            catch { }
            return games;
        }

        // 扫描育碧库
        public static List<GameInfo> ScanUbisoftGames()
        {
            var games = new List<GameInfo>();
            try
            {
                // 育碧Connect的配置文件
                var ubisoftConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Ubisoft Game Launcher", "settings.yml");

                // 常见的育碧游戏安装路径
                var ubisoftPaths = new[]
                {
                    @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games",
                    @"C:\Program Files\Ubisoft\Ubisoft Game Launcher\games",
                    @"D:\Ubisoft Games",
                    @"E:\Ubisoft Games",
                    @"F:\Ubisoft Games"
                };

                foreach (var ubiPath in ubisoftPaths.Where(Directory.Exists))
                {
                    foreach (var gameDir in Directory.GetDirectories(ubiPath))
                    {
                        var game = AnalyzeGameDirectory(gameDir);
                        if (game != null) games.Add(game);
                    }
                }
            }
            catch { }
            return games;
        }

        // 手动添加游戏（目录模式）
        // 手动添加游戏（目录模式）
        // 注意：手动添加目录时，用户已经明确选择了目录，所以跳过hasRealGameExe检查
        // 直接查找游戏真正的exe，避免因为网络驱动器访问问题导致识别失败
        public static GameInfo? AddGameByDirectory(string gamePath)
        {
            return AnalyzeGameDirectory(gamePath, skipExeCheck: true);
        }

        // 手动添加游戏（exe模式，支持DX9）
        public static GameInfo? AddGameByExe(string exePath, bool isDx9Manual = false)
        {
            if (!File.Exists(exePath)) return null;
            var exeDir = Path.GetDirectoryName(exePath)!;
            var gameRoot = FindGameRoot(exeDir);
            var gameName = Path.GetFileNameWithoutExtension(exePath);
            
            // 检测DX支持情况
            var (supportsDX9, supportsDX11) = CheckDXSupport(exeDir, gameRoot);
            
            var game = new GameInfo
            {
                Name = gameName,
                GamePath = gameRoot,
                ExePath = exePath,
                IsDX9 = isDx9Manual || supportsDX9,
                SupportsDX11 = isDx9Manual ? false : supportsDX11,
                IsREEngine = CheckREEngine(gameRoot),
                Is2077 = gameName.Contains("Cyberpunk", StringComparison.OrdinalIgnoreCase),
                IsYanYun = gameName.Contains("燕云", StringComparison.OrdinalIgnoreCase) || gameName.Contains("yysls", StringComparison.OrdinalIgnoreCase),
                SupportsFrameGen = isDx9Manual ? false : supportsDX11
            };
            
            // 燕云十六声特殊处理：已知支持多帧生成和DLSS5
            if (game.IsYanYun)
            {
                game.SupportsFrameGen = true;
                game.IsDX9 = false;
                game.SupportsDX11 = true;
            }

            // 自动检测游戏是否已开启补丁
            if (!game.SupportsDX11)
            {
                // 纯DX9游戏
                game.SupportsFrameGen = false;
                game.Dx9Dlss5Enabled = CheckDX9DLSS5Enabled(gameRoot, exeDir);
            }
            else
            {
                // 支持DX11/12的游戏
                game.FrameGenEnabled = CheckFrameGenEnabled(gameRoot, exeDir);
                game.Dlss5Enabled = CheckDLSS5Enabled(gameRoot, exeDir);
                // 同时也检测是否开启了DX9 DLSS5
                game.Dx9Dlss5Enabled = CheckDX9DLSS5Enabled(gameRoot, exeDir);
            }

            return game;
        }

        // 分析游戏目录
        // skipExeCheck: 是否跳过hasRealGameExe检查（手动添加目录时为true，避免网络驱动器访问问题）
        private static GameInfo? AnalyzeGameDirectory(string gamePath, string? overrideName = null, bool skipExeCheck = false)
        {
            var gameName = overrideName ?? Path.GetFileName(gamePath);

            // 排除明显不是游戏的目录（Steam共享组件等）
            string[] nonGameDirs = new[]
            {
                "Steamworks Shared", "steamworks shared",
                "Steam Controller Configs", "steam controller configs",
                "Common Redist", "common redist",
                "DirectX", "directx",
                "_CommonRedist", "_commonredist"
            };
            if (nonGameDirs.Any(d => gameName.Equals(d, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            // 排除已卸载游戏的残留目录：检查是否有真正的游戏主程序
            try
            {
                // 已卸载游戏的核心特征：游戏主程序exe不在了
                // 检查根目录和常见bin目录下是否有大于1MB的exe（游戏主程序通常较大）
                bool hasRealGameExe = false;
                
                // 如果是手动添加目录（skipExeCheck=true），直接跳过hasRealGameExe检查
                // 因为用户已经明确选择了这个目录，肯定是游戏目录
                // 避免因为网络驱动器访问问题导致识别失败
                if (skipExeCheck)
                {
                    hasRealGameExe = true;
                }
                
                // 特殊处理：如果目录下有Retail子目录，直接认为有真正的游戏exe
                // 因为有些游戏（如007 First Light）的exe在Retail目录下
                // 而且网络驱动器访问可能有问题，导致Directory.GetFiles失败
                if (!hasRealGameExe)
                {
                    try
                    {
                        if (Directory.Exists(Path.Combine(gamePath, "Retail")) || 
                            Directory.Exists(Path.Combine(gamePath, "retail")))
                        {
                            hasRealGameExe = true;
                        }
                    }
                    catch { }
                }
                
                // 检查根目录（加上try-catch，避免因为权限问题导致整个检查失败）
                if (!hasRealGameExe)
                {
                    try
                    {
                        foreach (var f in Directory.GetFiles(gamePath, "*.exe"))
                        {
                            try
                            {
                                if (new FileInfo(f).Length > 1024 * 1024) // 大于1MB
                                {
                                    hasRealGameExe = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                
                // 检查常见bin目录
                if (!hasRealGameExe)
                {
                    string[] binDirs = { "bin", "Binaries", "Bin64", "Bin32", "win64", "Win64", "x64", "game\\bin\\win64", "Retail", "retail" };
                    foreach (var binDir in binDirs)
                    {
                        var fullPath = Path.Combine(gamePath, binDir);
                        if (Directory.Exists(fullPath))
                        {
                            try
                            {
                                // 对于Retail目录，使用递归搜索（包括所有子目录）
                                // 因为有些游戏的exe可能在Retail目录下的更深子目录里
                                SearchOption searchOption = (binDir.Equals("Retail", StringComparison.OrdinalIgnoreCase) || binDir.Equals("retail", StringComparison.OrdinalIgnoreCase)) 
                                    ? SearchOption.AllDirectories 
                                    : SearchOption.TopDirectoryOnly;
                                
                                foreach (var f in Directory.GetFiles(fullPath, "*.exe", searchOption))
                                {
                                    try
                                    {
                                        if (new FileInfo(f).Length > 1024 * 1024) // 大于1MB
                                        {
                                            hasRealGameExe = true;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                            if (hasRealGameExe) break;
                        }
                    }
                }
                
                // 检查UE游戏的Binaries\Win64目录
                if (!hasRealGameExe)
                {
                    try
                    {
                        var binariesDirs = GetDirectoriesWithDepthLimit(gamePath, "Win64", 3)
                            .Where(d => 
                            {
                                var parentDir = Path.GetDirectoryName(d);
                                if (string.IsNullOrEmpty(parentDir)) return false;
                                var parentName = Path.GetFileName(parentDir);
                                return !string.IsNullOrEmpty(parentName) && 
                                       parentName.Equals("Binaries", StringComparison.OrdinalIgnoreCase);
                            });
                        
                        foreach (var dir in binariesDirs)
                        {
                            foreach (var f in Directory.GetFiles(dir, "*.exe"))
                            {
                                try
                                {
                                    if (new FileInfo(f).Length > 1024 * 1024)
                                    {
                                        hasRealGameExe = true;
                                        break;
                                    }
                                }
                                catch { }
                            }
                            if (hasRealGameExe) break;
                        }
                    }
                    catch { }
                }
                
                // 最后手段：递归搜索整个游戏目录（深度8层），查找大于1MB的exe
                // 注意：这里不排除任何关键词，只要大于1MB的exe就认为是游戏主程序
                // 因为有些游戏的exe文件名可能包含launcher、tool等关键词
                if (!hasRealGameExe)
                {
                    try
                    {
                        var allExes = GetFilesWithDepthLimit(gamePath, "*.exe", 8);
                        foreach (var f in allExes)
                        {
                            try
                            {
                                // 只要大于1MB的exe就认为是游戏主程序（不排除任何关键词）
                                if (new FileInfo(f).Length > 1024 * 1024)
                                {
                                    hasRealGameExe = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                
                if (!hasRealGameExe)
                {
                    return null; // 没有真正的游戏主程序，可能是已卸载的残留目录
                }
            }
            catch { }

            // 尝试查找游戏exe目录
            var exeDir = FindGameExeDirectory(gamePath);
            
            // 如果找不到已知模式的目录，就直接用用户选择的目录
            if (exeDir == null)
            {
                exeDir = gamePath;
            }

            var exeFile = FindGameExe(exeDir);
            
            // 如果在当前目录找不到exe，尝试递归搜索
            if (exeFile == null)
            {
                try
                {
                    var allExes = GetFilesWithDepthLimit(gamePath, "*.exe", 4)
                        .Where(e =>
                        {
                            var fileName = Path.GetFileName(e).ToLowerInvariant();
                            string[] excludeKeywords = new[]
                            {
                                "unins000", "unins001", "uninstall", "unins.exe",
                                "setup.exe", "install.exe", "update.exe", "updater",
                                "launcher.exe", "launcher_", "_launcher",
                                "crashreport", "crash_report", "crashhandler", "crash_handler",
                                "createdump", "crashdump", "dump.exe", "_dump",
                                "editor.exe", "unrealeditor", "ue4editor", "ue5editor",
                                "server.exe", "dedicated", "tool.exe", "tools.exe",
                                "source1import", "source2import", "import.exe",
                                "steamworksexample", "example.exe",
                                // Source 2引擎工具
                                "vrad3", "resourcecompiler", "resourcecopy", "resourceinfo",
                                "dmxconvert", "cs_mdl_import", "cs2_build", "csgocfg", "vconsole2",
                                "vpk", "vtex", "vmt", "vbsp", "vvis", "vrad", "vmpi", "vfont",
                                "import_map", "legacy",
                                "vcredist", "dxwebsetup", "dotnetfx", "xnafx", "commonredist",
                                "easyanticheat_setup", "battleye_setup",
                                "configtool", "settingstool", "modmanager", "saveeditor",
                                "debug.exe", "test.exe", "preview.exe",
                                "steam_api", "steamclient", "steamworks",
                                "patch.exe", "patcher.exe", "repair.exe", "verify.exe",
                                "diagnostic", "compiler", "converter", "importer",
                                "viewer", "manager.exe", "monitor.exe", "overlay.exe",
                                "injector.exe", "loader.exe", "plugin", "plugins",
                                "translator", "autotranslator", "ext.protocol", "executor.exe"
                            };
                            foreach (var keyword in excludeKeywords)
                            {
                                if (fileName.Contains(keyword)) return false;
                            }
                            // 注意：不要排除以数字开头的exe！
                            // 因为有些游戏的exe文件名以数字开头，比如007FirstLight.exe
                            // 原来的逻辑：if (char.IsDigit(fileName[0]) && !fileName.Contains("shipping")) return false;
                            // 这会导致007FirstLight.exe被错误排除！
                            return true;
                        })
                        .OrderByDescending(e => 
                        {
                            try { return new FileInfo(e).Length; }
                            catch { return 0; }
                        })
                        .ToList();
                    
                    if (allExes.Count > 0)
                    {
                        // 优先选择包含Shipping或Game的exe
                        var bestExe = allExes.FirstOrDefault(e => 
                            Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(e).Contains("Game", StringComparison.OrdinalIgnoreCase));
                        
                        if (bestExe == null) bestExe = allExes[0];
                        
                        exeFile = bestExe;
                        exeDir = Path.GetDirectoryName(exeFile)!;
                    }
                }
                catch { }
            }
            
            // 如果找不到exe，不要直接返回null
            // 尝试用用户选择的目录作为exeDir，用游戏名称作为默认exe名称
            // 这样可以避免弹出"无法识别该游戏目录"的情况，就算识别错了也没太大问题
            // 用户可以后续用"手动添加exe"或"检测运行中游戏"来修正
            if (exeFile == null)
            {
                try
                {
                    // 尝试用游戏名称.exe作为默认exe
                    var defaultExe = Path.Combine(exeDir, gameName + ".exe");
                    if (File.Exists(defaultExe))
                    {
                        exeFile = defaultExe;
                    }
                    else
                    {
                        // 尝试在exeDir中找任意一个exe（不排除任何关键词，只要大于100KB）
                        var anyExe = Directory.GetFiles(exeDir, "*.exe")
                            .Where(e => {
                                try { return new FileInfo(e).Length > 100 * 1024; }
                                catch { return false; }
                            })
                            .OrderByDescending(e => {
                                try { return new FileInfo(e).Length; }
                                catch { return 0; }
                            })
                            .FirstOrDefault();
                        
                        if (anyExe != null)
                        {
                            exeFile = anyExe;
                        }
                        else
                        {
                            // 如果还是找不到，就用游戏名称.exe作为默认exe（即使文件不存在）
                            // 这样至少能添加到列表中，用户可以后续手动修改
                            exeFile = defaultExe;
                        }
                    }
                }
                catch
                {
                    // 如果出错，就用游戏名称.exe作为默认exe
                    exeFile = Path.Combine(exeDir, gameName + ".exe");
                }
            }

            var isREEngine = CheckREEngine(gamePath);
            var is2077 = gameName.Contains("Cyberpunk", StringComparison.OrdinalIgnoreCase);
            var isYanYun = gameName.Contains("燕云", StringComparison.OrdinalIgnoreCase) ||
                           gameName.Contains("yysls", StringComparison.OrdinalIgnoreCase) ||
                           Directory.Exists(Path.Combine(gamePath, "yysls_medium"));
            var is007FirstLight = gameName.Contains("007", StringComparison.OrdinalIgnoreCase) &&
                                   (gameName.Contains("First Light", StringComparison.OrdinalIgnoreCase) ||
                                    gameName.Contains("初露锋芒", StringComparison.OrdinalIgnoreCase));

            // 燕云十六声特殊处理
            if (isYanYun)
            {
                var yanYunExeDir = Path.Combine(gamePath, "yysls_medium", "Engine", "Binaries", "Win64r");
                if (Directory.Exists(yanYunExeDir))
                {
                    exeDir = yanYunExeDir;
                    exeFile = FindGameExe(exeDir) ?? exeFile;
                }
            }

            // 2077特殊处理
            if (is2077)
            {
                var cp2077ExeDir = Path.Combine(gamePath, "bin", "x64");
                if (Directory.Exists(cp2077ExeDir))
                {
                    exeDir = cp2077ExeDir;
                    exeFile = FindGameExe(exeDir) ?? exeFile;
                }
            }

            // 007 First Light特殊处理（exe在Retail目录下）
            if (is007FirstLight)
            {
                var firstLightExeDir = Path.Combine(gamePath, "Retail");
                if (Directory.Exists(firstLightExeDir))
                {
                    exeDir = firstLightExeDir;
                    exeFile = FindGameExe(exeDir) ?? exeFile;
                }
            }

            // 检测DX支持情况
            var (supportsDX9, supportsDX11) = CheckDXSupport(exeDir, gamePath);
            // 只要支持DX11/12就允许开启多帧生成，不依赖游戏目录中是否已有nvngx_dlssg.dll
            var supportsFrameGen = supportsDX11;
            
            // 燕云十六声特殊处理：已知支持多帧生成和DLSS5
            if (isYanYun)
            {
                supportsFrameGen = true;
                supportsDX9 = false;
                supportsDX11 = true;
            }

            var game = new GameInfo
            {
                Name = gameName,
                GamePath = gamePath,
                ExePath = exeFile,
                IsREEngine = isREEngine,
                IsDX9 = supportsDX9,  // 是否支持DX9
                SupportsDX11 = supportsDX11,  // 是否支持DX11/12
                Is2077 = is2077,
                IsYanYun = isYanYun,
                SupportsFrameGen = supportsFrameGen
            };

            // 自动检测游戏是否已开启补丁
            if (!supportsDX11)
            {
                // 纯DX9游戏
                game.Dx9Dlss5Enabled = CheckDX9DLSS5Enabled(gamePath, exeDir);
            }
            else
            {
                // 支持DX11/12的游戏
                game.FrameGenEnabled = CheckFrameGenEnabled(gamePath, exeDir);
                game.Dlss5Enabled = CheckDLSS5Enabled(gamePath, exeDir);
                // 同时也检测是否开启了DX9 DLSS5
                game.Dx9Dlss5Enabled = CheckDX9DLSS5Enabled(gamePath, exeDir);
            }

            return game;
        }

        // 查找游戏真正运行exe目录
        private static string? FindGameExeDirectory(string gamePath)
        {
            if (!Directory.Exists(gamePath)) return null;

            // ========== 特殊游戏优先处理 ==========
            // 异环特殊处理
            var htDir = Path.Combine(gamePath, "Client", "WindowsNoEditor", "HT", "Binaries", "Win64");
            if (Directory.Exists(htDir) && FindGameExe(htDir) != null) return htDir;

            // 燕云十六声特殊处理
            var yanYunDir = Path.Combine(gamePath, "yysls_medium", "Engine", "Binaries", "Win64r");
            if (Directory.Exists(yanYunDir) && FindGameExe(yanYunDir) != null) return yanYunDir;

            // 赛博朋克2077
            var cp2077Dir = Path.Combine(gamePath, "bin", "x64");
            if (Directory.Exists(cp2077Dir) && FindGameExe(cp2077Dir) != null) return cp2077Dir;

            // ========== RE引擎：根目录 ==========
            if (CheckREEngine(gamePath))
            {
                if (FindGameExe(gamePath) != null) return gamePath;
            }

            // ========== Unity引擎：根目录（检测UnityPlayer.dll） ==========
            if (File.Exists(Path.Combine(gamePath, "UnityPlayer.dll")))
            {
                if (FindGameExe(gamePath) != null) return gamePath;
            }

            // ========== Source引擎：根目录（检测hl2.exe和steam.inf） ==========
            // Source引擎游戏：CSGO、TF2、DOTA2、L4D2等
            // 根目录有hl2.exe（引擎引导）和游戏名exe（如csgo.exe）
            if (File.Exists(Path.Combine(gamePath, "hl2.exe")) || 
                File.Exists(Path.Combine(gamePath, "steam.inf")))
            {
                // Source引擎游戏真正的exe通常是游戏名.exe（如csgo.exe），不是hl2.exe
                var sourceExes = Directory.GetFiles(gamePath, "*.exe")
                    .Where(e => 
                    {
                        var name = Path.GetFileName(e).ToLowerInvariant();
                        // 排除hl2.exe（引擎引导）和其他非游戏exe
                        return name != "hl2.exe" && !name.Contains("unins") && 
                               !name.Contains("setup") && !name.Contains("install") &&
                               !name.Contains("crash") && !name.Contains("tool") &&
                               !name.Contains("editor") && !name.Contains("server");
                    })
                    .ToList();
                if (sourceExes.Count > 0)
                {
                    // 优先选择体积最大的（游戏主程序）
                    var bestSource = sourceExes.OrderByDescending(e =>
                    {
                        try { return new FileInfo(e).Length; }
                        catch { return 0; }
                    }).First();
                    return gamePath;
                }
                // 如果只有hl2.exe，也返回根目录
                if (FindGameExe(gamePath) != null) return gamePath;
            }

            // ========== Source 2引擎：game\bin\win64（CS2、DOTA2等） ==========
            // Source 2引擎游戏真正的exe在 game\bin\win64\ 目录下
            var source2BinDir = Path.Combine(gamePath, "game", "bin", "win64");
            if (Directory.Exists(source2BinDir))
            {
                // Source 2引擎游戏真正的exe通常是游戏名.exe（如cs2.exe、dota2.exe）
                var source2Exes = Directory.GetFiles(source2BinDir, "*.exe")
                    .Where(e => 
                    {
                        var name = Path.GetFileName(e).ToLowerInvariant();
                        // 排除工具exe
                        string[] source2Tools = new[]
                        {
                            "vrad3", "resourcecompiler", "resourcecopy", "resourceinfo",
                            "dmxconvert", "cs_mdl_import", "cs2_build", "csgocfg", "vconsole2",
                            "vpk", "vtex", "vmt", "vbsp", "vvis", "vrad", "vmpi", "vfont",
                            "source1import", "import", "legacy", "dotnet", "createdump"
                        };
                        foreach (var tool in source2Tools)
                        {
                            if (name.Contains(tool)) return false;
                        }
                        return true;
                    })
                    .ToList();
                if (source2Exes.Count > 0)
                {
                    return source2BinDir;
                }
            }

            // ========== 标准UE游戏：Binaries\Win64（排除Engine目录） ==========
            // 注意：必须精确匹配 Binaries\Win64，不能匹配 bin\win64（Source引擎等其他引擎的目录）
            try
            {
                var binariesDirs = GetDirectoriesWithDepthLimit(gamePath, "Win64", 3)
                    .Where(d => 
                    {
                        // 必须包含 Binaries\Win64（精确匹配，区分大小写不敏感但路径结构要对）
                        var parentDir = Path.GetDirectoryName(d);
                        if (string.IsNullOrEmpty(parentDir)) return false;
                        var parentName = Path.GetFileName(parentDir);
                        if (string.IsNullOrEmpty(parentName) || !parentName.Equals("Binaries", StringComparison.OrdinalIgnoreCase))
                            return false;
                        // 排除Engine目录下的
                        if (d.Contains($"{Path.DirectorySeparatorChar}Engine{Path.DirectorySeparatorChar}Binaries", StringComparison.OrdinalIgnoreCase))
                            return false;
                        // 排除game\bin\win64这种Source引擎结构
                        if (d.Contains($"{Path.DirectorySeparatorChar}game{Path.DirectorySeparatorChar}bin", StringComparison.OrdinalIgnoreCase))
                            return false;
                        return FindGameExe(d) != null;
                    })
                    .ToList();

                if (binariesDirs.Count > 0)
                {
                    // 优先选择路径最短的（通常是游戏主目录，不是DLC或其他子目录）
                    return binariesDirs.OrderBy(d => d.Length).First();
                }
            }
            catch { }

            // ========== 根目录有exe（很多老游戏、独立游戏） ==========
            if (FindGameExe(gamePath) != null) return gamePath;

            // ========== bin目录（DX9游戏、一些老游戏） ==========
            var binDir = Path.Combine(gamePath, "bin");
            if (Directory.Exists(binDir))
            {
                var bin64 = Path.Combine(binDir, "x64");
                var bin32 = Path.Combine(binDir, "x86");
                var binBin64 = Path.Combine(binDir, "Bin64");
                var binBin32 = Path.Combine(binDir, "Bin32");
                
                if (Directory.Exists(bin64) && FindGameExe(bin64) != null) return bin64;
                if (Directory.Exists(binBin64) && FindGameExe(binBin64) != null) return binBin64;
                if (Directory.Exists(bin32) && FindGameExe(bin32) != null) return bin32;
                if (Directory.Exists(binBin32) && FindGameExe(binBin32) != null) return binBin32;
                if (FindGameExe(binDir) != null) return binDir;
            }

            // ========== 其他常见目录结构 ==========
            string[] commonSubDirs = { "Binaries", "Win64", "Win32", "x64", "x86", "game", "Game", "Retail", "retail" };
            foreach (var subDir in commonSubDirs)
            {
                var dir = Path.Combine(gamePath, subDir);
                if (Directory.Exists(dir) && FindGameExe(dir) != null) return dir;
            }

            // ========== 兜底：递归搜索整个目录，使用改进后的FindGameExe逻辑 ==========
            try
            {
                var allExes = GetFilesWithDepthLimit(gamePath, "*.exe", 4)
                    .Where(e =>
                    {
                        var fileName = Path.GetFileName(e).ToLowerInvariant();
                        // 排除明显不是游戏主程序的exe
                        string[] excludeKeywords = new[]
                        {
                            "unins000", "unins001", "uninstall", "setup.exe", "install.exe",
                            "launcher.exe", "update.exe", "updater", "crashreport", "crash_handler",
                            "createdump", "crashdump", "dump.exe",
                            "editor.exe", "unrealeditor", "ue4editor", "ue5editor",
                            "server.exe", "dedicated", "vcredist", "dxwebsetup", "dotnetfx",
                            "easyanticheat_setup", "battleye_setup",
                            "patch.exe", "patcher.exe", "repair.exe", "verify.exe",
                            "steam_api", "steamclient", "steamworks", "overlay.exe", "injector.exe",
                            "configtool", "settingstool", "modmanager", "saveeditor",
                            "debug.exe", "test.exe", "tool.exe", "manager.exe", "monitor.exe",
                            "source1import", "source2import", "import.exe", "steamworksexample",
                            // Source 2引擎工具
                            "vrad3", "resourcecompiler", "resourcecopy", "resourceinfo",
                            "dmxconvert", "cs_mdl_import", "cs2_build", "csgocfg", "vconsole2",
                            "vpk", "vtex", "vmt", "vbsp", "vvis", "vrad", "vmpi", "vfont",
                            "import_map", "legacy", "plugin", "translator", "executor"
                        };
                        foreach (var keyword in excludeKeywords)
                        {
                            if (fileName.Contains(keyword)) return false;
                        }
                        if (char.IsDigit(fileName[0]) && !fileName.Contains("shipping")) return false;
                        return true;
                    })
                    .ToList();

                if (allExes.Count > 0)
                {
                    // 使用FindGameExe的优先级逻辑来选择最佳exe
                    // 1. Win64-Shipping
                    var best = allExes.FirstOrDefault(e => 
                        Path.GetFileName(e).Contains("Win64-Shipping", StringComparison.OrdinalIgnoreCase));
                    // 2. Shipping
                    if (best == null) best = allExes.FirstOrDefault(e => 
                        Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase));
                    // 3. Game
                    if (best == null) best = allExes.FirstOrDefault(e => 
                        Path.GetFileName(e).Contains("Game", StringComparison.OrdinalIgnoreCase));
                    // 4. 大于1MB的最大exe
                    if (best == null)
                    {
                        var largeExes = allExes.Where(e =>
                        {
                            try { return new FileInfo(e).Length > 1024 * 1024; }
                            catch { return false; }
                        }).ToList();
                        if (largeExes.Count > 0)
                        {
                            best = largeExes.OrderByDescending(e =>
                            {
                                try { return new FileInfo(e).Length; }
                                catch { return 0; }
                            }).First();
                        }
                    }
                    // 5. 任意exe
                    if (best == null) best = allExes[0];

                    var exeDir = Path.GetDirectoryName(best);
                    if (exeDir != null) return exeDir;
                }
            }
            catch { }

            return null;
        }

        // 查找游戏真正运行exe
        private static string? FindGameExe(string dir)
        {
            if (!Directory.Exists(dir)) return null;

            // 需要排除的关键词（这些通常不是游戏主程序）
            // 注意：关键词要精确，避免误排除正常游戏exe
            string[] excludeKeywords = new[]
            {
                // 卸载程序
                "unins000", "unins001", "uninstall", "unins.exe",
                // 安装/更新程序
                "setup.exe", "install.exe", "update.exe", "updater",
                // 启动器（但不排除游戏名中包含launcher的情况）
                "launcher.exe", "launcher_", "_launcher",
                // 崩溃报告/转储
                "crashreport", "crash_report", "crashhandler", "crash_handler", "crash report",
                "createdump", "crashdump", "dump.exe", "_dump",
                // 引擎编辑器
                "editor.exe", "unrealeditor", "ue4editor", "ue5editor",
                // 服务器/专用服务器
                "server.exe", "dedicated", "listen server",
                // 工具类
                "tool.exe", "tools.exe", "utility", "utilities", "benchmark",
                "source1import", "source2import", "import.exe", "exporter",
                "steamworksexample", "steamworks_example", "example.exe",
                // Source 2引擎工具
                "vrad3", "resourcecompiler", "resourcecopy", "resourceinfo",
                "dmxconvert", "cs_mdl_import", "cs2_build", "csgocfg", "vconsole2",
                "vpk.exe", "vtex.exe", "vmt.exe", "vbsp.exe", "vvis.exe",
                "vrad.exe", "vmpi", "vfont", "import_map", "legacy",
                // 运行库安装
                "vcredist", "dxwebsetup", "dotnetfx", "xnafx", "commonredist",
                "ndp48", "dotnet", "runtime",
                // 反作弊（通常不是主程序）
                "easyanticheat_setup", "battleye_setup", "anticheat_setup",
                // 其他
                "configtool", "configurationtool", "settingstool", "optionstool",
                "modmanager", "mod manager", "modloader", "mod_loader",
                "saveeditor", "save_editor", "savegame",
                "readme", "license", "eula", "documentation",
                "debug.exe", "test.exe", "preview.exe", "alpha.exe", "beta.exe",
                "steam_api", "steamclient", "steamworks",
                "report.exe", "feedback.exe", "support.exe", "help.exe",
                "patch.exe", "patcher.exe", "repair.exe", "verify.exe",
                "diagnostic", "check.exe", "compiler", "converter",
                "importer", "viewer", "manager.exe",
                "monitor.exe", "overlay.exe", "injector.exe", "loader.exe",
                "plugin", "plugins", "translator", "autotranslator",
                "ext.protocol", "executor.exe", "protocol.exe"
            };

            var exes = Directory.GetFiles(dir, "*.exe")
                .Where(e =>
                {
                    var fileName = Path.GetFileName(e).ToLowerInvariant();
                    // 排除包含关键词的exe
                    foreach (var keyword in excludeKeywords)
                    {
                        if (fileName.Contains(keyword)) return false;
                    }
                    // 排除以数字开头的exe（通常是补丁、更新程序，如 unins000 已被上面排除）
                    if (char.IsDigit(fileName[0]) && !fileName.Contains("shipping")) return false;
                    return true;
                })
                .ToList();

            if (exes.Count == 0) return null;

            // 如果只有一个exe，直接返回
            if (exes.Count == 1) return exes[0];

            // ========== 优先级1：UE游戏标准命名 ==========
            // 最精确：Win64-Shipping（UE4/UE5游戏真正的运行程序）
            var win64Shipping = exes.FirstOrDefault(e =>
                Path.GetFileName(e).Contains("Win64-Shipping", StringComparison.OrdinalIgnoreCase));
            if (win64Shipping != null) return win64Shipping;

            // 次精确：Shipping（可能是Win32-Shipping或其他平台）
            var shipping = exes.FirstOrDefault(e =>
                Path.GetFileName(e).Contains("Shipping", StringComparison.OrdinalIgnoreCase));
            if (shipping != null) return shipping;

            // ========== 优先级2：Unity引擎检测 ==========
            // Unity游戏：exe旁边有UnityPlayer.dll和同名_Data目录
            var hasUnityPlayer = File.Exists(Path.Combine(dir, "UnityPlayer.dll"));
            if (hasUnityPlayer)
            {
                // 查找与_Data目录同名的exe
                var dataDirs = Directory.GetDirectories(dir, "*_Data");
                foreach (var dataDir in dataDirs)
                {
                    var dataName = Path.GetFileNameWithoutExtension(dataDir).Replace("_Data", "");
                    var unityExe = exes.FirstOrDefault(e =>
                        Path.GetFileNameWithoutExtension(e).Equals(dataName, StringComparison.OrdinalIgnoreCase));
                    if (unityExe != null) return unityExe;
                }
                // 如果没找到完全匹配，选择包含目录名的exe
                var dirName = Path.GetFileName(dir).ToLowerInvariant();
                var unityMatch = exes.FirstOrDefault(e =>
                    Path.GetFileNameWithoutExtension(e).ToLowerInvariant().Contains(dirName));
                if (unityMatch != null) return unityMatch;
            }

            // ========== 优先级3：包含Game关键词 ==========
            var game = exes.FirstOrDefault(e =>
                Path.GetFileName(e).Contains("Game", StringComparison.OrdinalIgnoreCase));
            if (game != null) return game;

            // ========== 优先级4：名称匹配游戏目录名 ==========
            var currentDirName = Path.GetFileName(dir).ToLowerInvariant();
            var parentDirName = Directory.GetParent(dir)?.Name.ToLowerInvariant() ?? "";
            var grandParentDirName = Directory.GetParent(dir)?.Parent?.Name.ToLowerInvariant() ?? "";
            
            var nameMatch = exes.FirstOrDefault(e =>
            {
                var fileName = Path.GetFileNameWithoutExtension(e).ToLowerInvariant();
                // 精确匹配优先
                if (fileName == currentDirName || fileName == parentDirName || fileName == grandParentDirName)
                    return true;
                // 包含匹配
                return fileName.Contains(currentDirName) || fileName.Contains(parentDirName);
            });
            if (nameMatch != null) return nameMatch;

            // ========== 优先级5：排除bootstrap/启动器后选最大的 ==========
            // UE游戏根目录通常有一个小的bootstrap exe（几十KB），真正的游戏exe在Binaries\Win64
            // 如果当前目录是根目录且有Binaries\Win64子目录，优先选择子目录中的exe
            var binariesWin64 = Path.Combine(dir, "Binaries", "Win64");
            if (Directory.Exists(binariesWin64))
            {
                var subExe = FindGameExe(binariesWin64);
                if (subExe != null) return subExe;
            }

            // 过滤掉明显过小的exe（小于1MB的通常是启动器/引导程序）
            var largeEnough = exes.Where(e =>
            {
                try { return new FileInfo(e).Length > 1024 * 1024; } // > 1MB
                catch { return false; }
            }).ToList();

            if (largeEnough.Count > 0)
            {
                // 选择体积最大的
                return largeEnough.OrderByDescending(e =>
                {
                    try { return new FileInfo(e).Length; }
                    catch { return 0; }
                }).First();
            }

            // 最后兜底：返回第一个
            return exes.FirstOrDefault();
        }

        // 检测是否RE引擎
        public static bool CheckREEngine(string gamePath)
        {
            return DirectoryContainsFile(gamePath, "re_chunk_000.pak");
        }

        // 检测游戏的DX支持情况
        public static (bool supportsDX9, bool supportsDX11) CheckDXSupport(string exeDir, string gamePath)
        {
            bool supportsDX9 = false;
            bool supportsDX11 = false;
            
            // 检查目录结构
            bool hasBin32 = Directory.Exists(Path.Combine(exeDir, "Bin32")) || 
                           Directory.Exists(Path.Combine(gamePath, "Bin32")) ||
                           Directory.Exists(Path.Combine(exeDir, "bin")) ||
                           Directory.Exists(Path.Combine(gamePath, "bin"));
            bool hasBin64 = Directory.Exists(Path.Combine(exeDir, "Bin64")) || 
                           Directory.Exists(Path.Combine(gamePath, "Bin64")) ||
                           Directory.Exists(Path.Combine(exeDir, "bin64")) ||
                           Directory.Exists(Path.Combine(gamePath, "bin64")) ||
                           Directory.Exists(Path.Combine(exeDir, "Win64")) ||
                           Directory.Exists(Path.Combine(gamePath, "Win64")) ||
                           Directory.Exists(Path.Combine(exeDir, "x64")) ||
                           Directory.Exists(Path.Combine(gamePath, "x64"));
            
            if (hasBin64) supportsDX11 = true;
            if (hasBin32 && !hasBin64) supportsDX9 = true;
            
            // 检查exe位数（主要判断依据）
            try
            {
                var exeFile = FindGameExe(exeDir);
                if (exeFile != null)
                {
                    bool is32Bit = Is32BitExe(exeFile);
                    
                    if (is32Bit)
                    {
                        // 32位exe：很可能支持DX9
                        supportsDX9 = true;
                        // 32位exe也可能支持DX11（通过兼容层），但默认认为不支持
                        // 除非有明确的DX11特征文件
                    }
                    else
                    {
                        // 64位exe：通常支持DX11/12，不支持纯DX9
                        supportsDX11 = true;
                        supportsDX9 = false;
                    }
                }
            }
            catch { }
            
            // 检查是否有d3d9.dll（DX9游戏特征）
            if (File.Exists(Path.Combine(exeDir, "d3d9.dll")))
            {
                supportsDX9 = true;
            }
            
            // 检查是否有DX11/12特征文件（这些通常是mod注入的，不是游戏自带的）
            var dx11Files = new[] { "d3d11.dll", "d3d12.dll", "dxgi.dll", "D3D12Core.dll" };
            foreach (var file in dx11Files)
            {
                if (File.Exists(Path.Combine(exeDir, file)))
                {
                    supportsDX11 = true;
                    break;
                }
            }

            // 检查是否有D3D12文件夹（游戏原生支持DX12的特征）
            if (Directory.Exists(Path.Combine(exeDir, "D3D12")) ||
                Directory.Exists(Path.Combine(gamePath, "D3D12")))
            {
                supportsDX11 = true;
            }
            
            // 如果两个都不支持，默认认为支持DX11（现代游戏居多）
            if (!supportsDX9 && !supportsDX11)
            {
                supportsDX11 = true;
            }
            
            // 如果是64位exe，强制不支持纯DX9（64位DX9游戏极少）
            try
            {
                var exeFile = FindGameExe(exeDir);
                if (exeFile != null && !Is32BitExe(exeFile))
                {
                    supportsDX9 = false;
                    supportsDX11 = true;
                }
            }
            catch { }
            
            return (supportsDX9, supportsDX11);
        }
        
        // 检测是否DX9游戏（保留兼容）
        public static bool CheckIsDX9(string exeDir)
        {
            var (supportsDX9, supportsDX11) = CheckDXSupport(exeDir, exeDir);
            // 只支持DX9，不支持DX11，才认为是纯DX9游戏
            return supportsDX9 && !supportsDX11;
        }
        
        // 检测exe是否是32位
        private static bool Is32BitExe(string exePath)
        {
            try
            {
                using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fs);
                
                // 读取PE签名位置
                fs.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                
                // 移动到PE签名
                fs.Position = peOffset;
                uint peSignature = reader.ReadUInt32();
                if (peSignature != 0x00004550) return false; // "PE\0\0"
                
                // 读取Machine字段
                ushort machine = reader.ReadUInt16();
                
                // 0x14c = i386 (32位), 0x8664 = x64 (64位)
                return machine == 0x14c;
            }
            catch
            {
                return false;
            }
        }

        // 检测游戏是否已开启多帧生成（通过检测特征文件）
        // 检查备份目录是否真的属于当前游戏（通过game_path.txt比对路径）
        private static bool CheckBackupBelongsToGame(string backupDir, string gamePath)
        {
            try
            {
                var pathFile = Path.Combine(backupDir, "game_path.txt");
                if (File.Exists(pathFile))
                {
                    var savedPath = File.ReadAllText(pathFile).Trim();
                    // 路径一致才判定（忽略末尾的斜杠差异）
                    return string.Equals(
                        savedPath.TrimEnd('\\', '/'), 
                        gamePath.TrimEnd('\\', '/'), 
                        StringComparison.OrdinalIgnoreCase);
                }
                // 旧版本备份没有game_path.txt，为了避免同名游戏误判，不判定
                // 用户重新开启一次补丁后会生成新的带路径标记的备份
                return false;
            }
            catch { return false; }
        }

        public static bool CheckFrameGenEnabled(string gamePath, string exeDir)
        {
            try
            {
                // 1. 最可靠：检查备份目录是否存在，并且路径匹配
                var backupBase = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DLSSFrameGenEnabler", "Backups");
                
                // 只用游戏根目录名匹配（exe目录名可能是Win64/Bin等通用名称，容易误判）
                var gameName = Path.GetFileName(gamePath);
                
                string[] frameGenFeatures = new[] 
                { 
                    "FrameGen_Classic", "FrameGen_Advanced", "FrameGen_RTX20", 
                    "FrameGen_RTX30", "FrameGen_RE", "FrameGen_2077" 
                };
                
                if (!string.IsNullOrEmpty(gameName))
                {
                    foreach (var feature in frameGenFeatures)
                    {
                        var backupDir = Path.Combine(backupBase, gameName, feature);
                        if (Directory.Exists(backupDir) && CheckBackupBelongsToGame(backupDir, gamePath))
                        {
                            return true;
                        }
                    }
                }
                
                // 2. 检查exe目录下的特征文件组合（单独一个文件不能判定）
                // RTX40系多帧生成：RTX40MFG.asi + RTX40MFG_config.json
                if (File.Exists(Path.Combine(exeDir, "RTX40MFG.asi")) && 
                    File.Exists(Path.Combine(exeDir, "RTX40MFG_config.json")))
                {
                    return true;
                }
                
                // RTX40MFGCore.dll + RTX40MFG-UI.addon64
                if (File.Exists(Path.Combine(exeDir, "RTX40MFGCore.dll")) && 
                    File.Exists(Path.Combine(exeDir, "RTX40MFG-UI.addon64")))
                {
                    return true;
                }
                
                // 3. RE引擎多帧生成：dinput8.dll + version.dll
                // 单独的dinput8.dll不能判定，因为很多游戏本身就有
                if (File.Exists(Path.Combine(exeDir, "dinput8.dll")) && 
                    File.Exists(Path.Combine(exeDir, "version.dll")))
                {
                    return true;
                }
                
                return false;
            }
            catch { return false; }
        }

        // 检测游戏是否已开启DLSS5（通过检测特征文件）
        public static bool CheckDLSS5Enabled(string gamePath, string exeDir)
        {
            try
            {
                // 1. 最可靠：检查备份目录是否存在，并且路径匹配
                var backupBase = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DLSSFrameGenEnabler", "Backups");
                
                // 只用游戏根目录名匹配
                var gameName = Path.GetFileName(gamePath);
                
                string[] dlss5Features = new[] 
                { 
                    "DLSS5_Common", "DLSS5_AMD", "DLSS5_RE", "DLSS5_YanYun"
                };
                
                if (!string.IsNullOrEmpty(gameName))
                {
                    foreach (var feature in dlss5Features)
                    {
                        var backupDir = Path.Combine(backupBase, gameName, feature);
                        if (Directory.Exists(backupDir) && CheckBackupBelongsToGame(backupDir, gamePath))
                        {
                            return true;
                        }
                    }
                }
                
                // 2. 检查exe目录下的特征文件组合（单独一个文件不能判定，必须是组合）
                // renodx-dlss5.addon64 + nvngx_dlssnr.dll（通用DLSS5的核心组合）
                if (File.Exists(Path.Combine(exeDir, "renodx-dlss5.addon64")) && 
                    File.Exists(Path.Combine(exeDir, "nvngx_dlssnr.dll")))
                {
                    return true;
                }
                
                // RE_DLSS5_Core.dll + dinput8.dll（RE引擎DLSS5的核心组合）
                if (File.Exists(Path.Combine(exeDir, "RE_DLSS5_Core.dll")) && 
                    File.Exists(Path.Combine(exeDir, "dinput8.dll")))
                {
                    return true;
                }
                
                // 3. dxgi.dll + ReShade.ini + renodx插件/nvngx_dlssnr 三个同时存在才判定
                if (File.Exists(Path.Combine(exeDir, "dxgi.dll")) && 
                    File.Exists(Path.Combine(exeDir, "ReShade.ini")) &&
                    (File.Exists(Path.Combine(exeDir, "renodx-dlss5.addon64")) ||
                     File.Exists(Path.Combine(exeDir, "nvngx_dlssnr.dll"))))
                {
                    return true;
                }
                
                return false;
            }
            catch { return false; }
        }

        // 检测游戏是否已开启DX9 DLSS5（通过检测特征文件）
        public static bool CheckDX9DLSS5Enabled(string gamePath, string exeDir)
        {
            try
            {
                // 1. 最可靠：检查备份目录是否存在，并且路径匹配
                var backupBase = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DLSSFrameGenEnabler", "Backups");
                
                // 只用游戏根目录名匹配
                var gameName = Path.GetFileName(gamePath);
                
                string[] dx9Features = new[] { "DX9DLSS5_32", "DX9DLSS5_64" };
                
                if (!string.IsNullOrEmpty(gameName))
                {
                    foreach (var feature in dx9Features)
                    {
                        var backupDir = Path.Combine(backupBase, gameName, feature);
                        if (Directory.Exists(backupDir) && CheckBackupBelongsToGame(backupDir, gamePath))
                        {
                            return true;
                        }
                    }
                }
                
                // 2. DX9 DLSS5特征文件（dgVoodoo + DLSS5 Feed组合，游戏本身不会有这种组合）
                var hasDgVoodoo = File.Exists(Path.Combine(exeDir, "D3D9.dll")) &&
                                  File.Exists(Path.Combine(exeDir, "dgVoodoo.conf"));
                var hasFeed = File.Exists(Path.Combine(exeDir, "dlss5-feed.addon32")) ||
                              File.Exists(Path.Combine(exeDir, "dlss5-feed.addon64"));
                
                // 需要同时有dgVoodoo和feed才能确定是DX9 DLSS5
                if (hasDgVoodoo && hasFeed) return true;
                
                // 检测host64目录（32位DX9 DLSS5的特征）
                if (Directory.Exists(Path.Combine(exeDir, "host64")) && hasDgVoodoo)
                {
                    return true;
                }
                
                return false;
            }
            catch { return false; }
        }

        // 辅助方法
        private static bool DirectoryContainsFile(string root, string fileName)
        {
            try
            {
                return GetFilesWithDepthLimit(root, fileName, 4).Length > 0;
            }
            catch { return false; }
        }

        private static string FindGameRoot(string exeDir)
        {
            // 向上查找游戏根目录（增加查找深度到10级，覆盖目录结构深的游戏）
            var dir = exeDir;
            string bestRoot = exeDir; // 默认返回exe目录
            
            for (int i = 0; i < 10; i++)
            {
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
                
                // 如果找到steamapps/common或Epic Games，这就是游戏根目录
                if (dir.Contains("steamapps\\common", StringComparison.OrdinalIgnoreCase) || 
                    dir.Contains("Epic Games", StringComparison.OrdinalIgnoreCase))
                {
                    return dir;
                }
                
                // 检查是否有游戏特征文件/目录（这些通常在游戏根目录）
                bool hasGameFeatures = false;
                try
                {
                    // 检查常见的游戏根目录特征
                    string[] gameRootMarkers = { 
                        "game.ico", "Game.ico", "steam_api.dll", "steam_api64.dll",
                        "steam_appid.txt", "EOSSDK-Win64-Shipping.dll", "eossdk-win64-shipping.dll",
                        "GOG.com", "goggame-", "uplay_r1_loader64.dll", "ubiorbitapi_r2_loader64.dll",
                        "Engine", "engine", "Content", "content", "Data", "data",
                        "Retail", "retail", "Binaries", "binaries"
                    };
                    
                    foreach (var marker in gameRootMarkers)
                    {
                        if (File.Exists(Path.Combine(dir, marker)) || Directory.Exists(Path.Combine(dir, marker)))
                        {
                            hasGameFeatures = true;
                            break;
                        }
                    }
                    
                    // 检查是否有大于5MB的exe在当前目录（游戏根目录通常有启动器exe）
                    if (!hasGameFeatures)
                    {
                        foreach (var f in Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                if (new FileInfo(f).Length > 5 * 1024 * 1024)
                                {
                                    hasGameFeatures = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
                
                if (hasGameFeatures)
                {
                    bestRoot = dir;
                }
            }
            
            return bestRoot;
        }

        private static string GetJsonValue(string json, string key)
        {
            try
            {
                var pattern = $"\"{key}\"";
                var idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return "";
                idx = json.IndexOf(':', idx);
                if (idx < 0) return "";
                var start = json.IndexOf('"', idx) + 1;
                var end = json.IndexOf('"', start);
                return json.Substring(start, end - start);
            }
            catch { return ""; }
        }
    }
}
