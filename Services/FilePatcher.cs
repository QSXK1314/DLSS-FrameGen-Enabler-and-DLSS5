using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using DLSSFrameGenEnabler_WinUI3.Models;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    public class FilePatcher
    {
        private static readonly string PatchesBaseDir = Path.Combine(
            AppContext.BaseDirectory, "Patches");

        private static readonly string SharedPatchDir = Path.Combine(
            PatchesBaseDir, "_Shared");

        private static readonly string BackupBaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DLSSFrameGenEnabler", "Backups");

        // 限制深度的文件搜索（避免遍历整个大目录导致性能问题）
        private static string[] GetFilesWithDepthLimit(string rootPath, string searchPattern, int maxDepth = 4)
        {
            var result = new List<string>();
            try
            {
                result.AddRange(Directory.GetFiles(rootPath, searchPattern, SearchOption.TopDirectoryOnly));
                if (maxDepth <= 1) return result.ToArray();
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
                    catch { }
                }
            }
            catch { }
        }

        private static string[] GetDirectoriesWithDepthLimit(string rootPath, string searchPattern, int maxDepth = 4)
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

        // 共享的大文件及对应的目标目录
        private static readonly Dictionary<string, string[]> SharedFiles = new()
        {
            // nvngx_dlssnr.dll: N卡/A卡/dx9/燕云十六声通用版本（排错目录是旧版本，不共享）
            ["nvngx_dlssnr.dll"] = new[]
            {
                "N卡DLSS5补丁",
                "A卡DLSS5补丁（仅支持7000系和9000系）",
                @"dx9 32位dlss5补丁\host64",
                "dx9 64位dlss5补丁",
                "燕云十六声DLSS5补丁",
                @"燕云十六声DLSS5补丁\Streamline"
            },
            // nvngx_dlss.dll: 通用版本（RE引擎多帧生成是单独版本，不共享）
            ["nvngx_dlss.dll"] = new[]
            {
                "2077专用多帧生成补丁",
                @"40系原生多帧生成\排错",
                @"dx9 32位dlss5补丁\host64",
                "dx9 64位dlss5补丁",
                @"RE引擎通用dlss5补丁\DLSS5 on RE",
                @"燕云十六声DLSS5补丁\Streamline",
                @"老驱动多帧生成\排错"
            }
        };

        // 补丁文件直接在输出目录的Patches文件夹中
        public static void EnsurePatchesExtracted()
        {
            // 不再需要预复制共享文件，各个功能会直接从_Shared目录读取
        }

        // 解析补丁文件的实际源路径（优先从_Shared目录读取共享大文件）
        private static string ResolvePatchFile(string patchDir, string fileName)
        {
            // 先检查补丁目录里是否有这个文件
            var patchFile = Path.Combine(patchDir, fileName);
            if (File.Exists(patchFile))
            {
                return patchFile;
            }

            // 如果补丁目录里没有，检查_Shared目录里是否有同名文件
            var sharedFile = Path.Combine(SharedPatchDir, fileName);
            if (File.Exists(sharedFile))
            {
                return sharedFile;
            }

            // 都没有，返回补丁目录的路径（调用方会处理文件不存在的情况）
            return patchFile;
        }

        // 复制补丁目录里的所有文件到目标目录（包括从_Shared目录补充的共享大文件）
        private static void CopyPatchFiles(string patchDir, string destDir, string backupDir, bool backupExisting = true, string[]? skipExtensions = null)
        {
            // 获取补丁目录里的所有文件名
            var fileNames = new List<string>();
            if (Directory.Exists(patchDir))
            {
                foreach (var file in Directory.GetFiles(patchDir))
                {
                    var fileName = Path.GetFileName(file);
                    // 检查是否需要跳过
                    if (skipExtensions != null)
                    {
                        var ext = Path.GetExtension(fileName).ToLower();
                        if (skipExtensions.Any(e => ext == e.ToLower())) continue;
                    }
                    fileNames.Add(fileName);
                }
            }

            // 补充_Shared目录里的共享大文件（如果补丁目录里没有）
            if (Directory.Exists(SharedPatchDir))
            {
                foreach (var sharedFile in Directory.GetFiles(SharedPatchDir))
                {
                    var fileName = Path.GetFileName(sharedFile);
                    if (!fileNames.Contains(fileName))
                    {
                        fileNames.Add(fileName);
                    }
                }
            }

            // 复制所有文件
            foreach (var fileName in fileNames)
            {
                var sourceFile = ResolvePatchFile(patchDir, fileName);
                if (!File.Exists(sourceFile)) continue;

                var destFile = Path.Combine(destDir, fileName);
                if (backupExisting && File.Exists(destFile))
                {
                    BackupFile(destFile, backupDir);
                }
                File.Copy(sourceFile, destFile, true);
            }
        }

        // 获取补丁目录
        private static string GetPatchDir(string subDir) => Path.Combine(PatchesBaseDir, subDir);

        // 获取游戏exe目录
        private static string GetExeDir(GameInfo game) => Path.GetDirectoryName(game.ExePath)!;

        // 获取备份目录
        private static string GetBackupDir(GameInfo game, string feature)
        {
            var dir = Path.Combine(BackupBaseDir, game.Name, feature);
            Directory.CreateDirectory(dir);
            // 在备份目录中记录游戏完整路径，防止同名游戏误判
            try
            {
                File.WriteAllText(Path.Combine(dir, "game_path.txt"), game.GamePath);
            }
            catch { }
            return dir;
        }

        // 复制目录（保持结构）
        private static void CopyDirectory(string source, string dest, bool overwrite = true, bool includeSharedFiles = false)
        {
            Directory.CreateDirectory(dest);

            // 获取源目录里的所有文件
            var filesToCopy = new List<string>(Directory.GetFiles(source));

            // 只有在明确要求时才补充_Shared目录里的共享大文件
            // 避免把nvngx_dlssnr.dll等大文件错误复制到reshade-shaders等子目录
            if (includeSharedFiles && Directory.Exists(SharedPatchDir))
            {
                foreach (var sharedFile in Directory.GetFiles(SharedPatchDir))
                {
                    var fileName = Path.GetFileName(sharedFile);
                    var sourceFile = Path.Combine(source, fileName);
                    // 如果源目录里没有这个文件，但是_Shared目录里有，就添加到要复制的文件列表里
                    if (!File.Exists(sourceFile) && !filesToCopy.Contains(sourceFile))
                    {
                        filesToCopy.Add(sharedFile);
                    }
                }
            }

            foreach (var file in filesToCopy)
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(dest, fileName);
                // 使用ResolvePatchFile获取实际源路径
                var actualSource = ResolvePatchFile(source, fileName);
                if (File.Exists(actualSource))
                {
                    File.Copy(actualSource, destFile, overwrite);
                }
            }
            foreach (var dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), overwrite, includeSharedFiles: false);
            }
        }

        // 复制中文字体文件到游戏目录的Fonts子目录
        private static void CopyFontsFiles(string exeDir)
        {
            try
            {
                var sharedFontsDir = Path.Combine(SharedPatchDir, "Fonts");
                if (!Directory.Exists(sharedFontsDir)) return;

                var fontsDstDir = Path.Combine(exeDir, "Fonts");
                if (!Directory.Exists(fontsDstDir))
                {
                    Directory.CreateDirectory(fontsDstDir);
                }

                foreach (var fontFile in Directory.GetFiles(sharedFontsDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = fontFile.Substring(sharedFontsDir.Length + 1);
                    var dstFontFile = Path.Combine(fontsDstDir, relativePath);
                    var dstFontDir = Path.GetDirectoryName(dstFontFile);
                    if (!string.IsNullOrEmpty(dstFontDir) && !Directory.Exists(dstFontDir))
                    {
                        Directory.CreateDirectory(dstFontDir);
                    }
                    File.Copy(fontFile, dstFontFile, true);
                }
            }
            catch { }
        }

        // 备份文件
        private static void BackupFile(string sourceFile, string backupDir)
        {
            if (!File.Exists(sourceFile)) return;
            var fileName = Path.GetFileName(sourceFile);
            var backupFile = Path.Combine(backupDir, fileName);
            if (!File.Exists(backupFile))
            {
                File.Copy(sourceFile, backupFile, true);
            }
        }

        // 查找nvngx_dlssg.dll位置
        private static string? FindNvngxDlssg(string gamePath)
        {
            try
            {
                var files = GetFilesWithDepthLimit(gamePath, "nvngx_dlssg.dll", 4);
                // 优先选择带NVIDIA路径的
                var nvidiaPath = files.FirstOrDefault(f =>
                    f.Contains("Nvidia", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("Streamline", StringComparison.OrdinalIgnoreCase));
                return nvidiaPath ?? files.FirstOrDefault();
            }
            catch { return null; }
        }

        // ========== 多帧生成 ==========

        // 经典模式（RTX40系老驱动）
        public static bool InstallClassicFrameGen(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "FrameGen_Classic");

                // 替换nvngx_dlssg.dll
                var dlssgPath = FindNvngxDlssg(game.GamePath);
                if (dlssgPath != null)
                {
                    BackupFile(dlssgPath, backupDir);
                    File.Copy(Path.Combine(GetPatchDir(@"老驱动多帧生成\核心"), "nvngx_dlssg.dll"), dlssgPath, true);
                }

                // 复制3个文件到exe目录
                var coreDir = GetPatchDir(@"老驱动多帧生成\核心");
                foreach (var file in new[] { "RTX40MFG.asi", "RTX40MFG_config.json", "version.dll" })
                {
                    var src = Path.Combine(coreDir, file);
                    var dst = Path.Combine(exeDir, file);
                    if (File.Exists(dst)) BackupFile(dst, backupDir);
                    File.Copy(src, dst, true);
                }

                game.FrameGenEnabled = true;
                game.FrameGenMode = "经典模式";
                return true;
            }
            catch { return false; }
        }

        // 高级模式（RTX40系新驱动）
        public static bool InstallAdvancedFrameGen(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "FrameGen_Advanced");

                // 替换nvngx_dlssg.dll
                var dlssgPath = FindNvngxDlssg(game.GamePath);
                if (dlssgPath != null)
                {
                    BackupFile(dlssgPath, backupDir);
                    File.Copy(Path.Combine(GetPatchDir("40系原生多帧生成"), "nvngx_dlssg.dll"), dlssgPath, true);
                }

                // 复制nvngx_dlss.dll
                var nvngxDlss = Path.Combine(exeDir, "nvngx_dlss.dll");
                if (File.Exists(nvngxDlss)) BackupFile(nvngxDlss, backupDir);
                var nvngxDlssSrc = ResolvePatchFile(GetPatchDir(@"40系原生多帧生成\排错"), "nvngx_dlss.dll");
                File.Copy(nvngxDlssSrc, nvngxDlss, true);

                // 复制version.dll
                var versionDll = Path.Combine(exeDir, "version.dll");
                if (File.Exists(versionDll)) BackupFile(versionDll, backupDir);
                File.Copy(Path.Combine(GetPatchDir("40系原生多帧生成"), "version.dll"), versionDll, true);

                // 复制reshade文件
                var reshadeDir = GetPatchDir(@"40系原生多帧生成\reshade");
                foreach (var file in Directory.GetFiles(reshadeDir))
                {
                    var dst = Path.Combine(exeDir, Path.GetFileName(file));
                    if (File.Exists(dst)) BackupFile(dst, backupDir);
                    File.Copy(file, dst, true);
                }

                // 复制reshade-shaders
                var shadersSrc = GetPatchDir(@"dlss5 reshade配置页面\reshade-shaders");
                var shadersDst = Path.Combine(exeDir, "reshade-shaders");
                if (Directory.Exists(shadersSrc))
                    CopyDirectory(shadersSrc, shadersDst);

                // 复制中文字体文件
                CopyFontsFiles(exeDir);

                // 复制unlock文件
                var unlockDir = GetPatchDir(@"40系原生多帧生成\Universal-RTX-40-MFG-Unlock-v1.2.1");
                foreach (var file in Directory.GetFiles(unlockDir))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName == "global.ini")
                    {
                        // global.ini改名为version.ini
                        var dst = Path.Combine(exeDir, "version.ini");
                        if (File.Exists(dst)) BackupFile(dst, backupDir);
                        File.Copy(file, dst, true);
                    }
                    else if (fileName.EndsWith(".dll") || fileName.EndsWith(".asi") || fileName.EndsWith(".addon64"))
                    {
                        var dst = Path.Combine(exeDir, fileName);
                        if (File.Exists(dst)) BackupFile(dst, backupDir);
                        File.Copy(file, dst, true);
                    }
                }

                game.FrameGenEnabled = true;
                game.FrameGenMode = "高级模式";
                return true;
            }
            catch { return false; }
        }

        // RTX20系多帧生成
        public static bool InstallRTX20FrameGen(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "FrameGen_RTX20");
                var patchDir = GetPatchDir("20系多帧生成");

                // 根目录文件（跳过.md）
                CopyPatchFiles(patchDir, exeDir, backupDir, skipExtensions: new[] { ".md" });

                // runtime子目录（保持结构）
                var runtimeSrc = Path.Combine(patchDir, "runtime");
                var runtimeDst = Path.Combine(exeDir, "runtime");
                if (Directory.Exists(runtimeSrc))
                    CopyDirectory(runtimeSrc, runtimeDst);

                game.FrameGenEnabled = true;
                game.FrameGenMode = "RTX20系";
                return true;
            }
            catch { return false; }
        }

        // RTX30系多帧生成
        public static bool InstallRTX30FrameGen(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "FrameGen_RTX30");
                var patchDir = GetPatchDir("30系多帧生成");

                CopyPatchFiles(patchDir, exeDir, backupDir);

                game.FrameGenEnabled = true;
                game.FrameGenMode = "RTX30系";
                return true;
            }
            catch { return false; }
        }

        // RE引擎多帧生成
        public static bool InstallREEngineFrameGen(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game); // RE引擎exe在根目录
                var backupDir = GetBackupDir(game, "FrameGen_RE");

                // RE引擎多帧生成补丁
                var patchDir = GetPatchDir("RE引擎多帧生成");
                CopyPatchFiles(patchDir, exeDir, backupDir);

                // RE框架dinput8.dll
                var dinput8 = Path.Combine(exeDir, "dinput8.dll");
                if (File.Exists(dinput8)) BackupFile(dinput8, backupDir);
                File.Copy(Path.Combine(GetPatchDir("RE框架"), "dinput8.dll"), dinput8, true);

                game.FrameGenEnabled = true;
                game.FrameGenMode = "RE引擎";
                return true;
            }
            catch { return false; }
        }

        // 2077专用多帧生成
        public static bool Install2077FrameGen(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game); // bin\x64
                var backupDir = GetBackupDir(game, "FrameGen_2077");
                var patchDir = GetPatchDir("2077专用多帧生成补丁");

                // 根目录文件（跳过.txt和.png）
                CopyPatchFiles(patchDir, exeDir, backupDir, skipExtensions: new[] { ".txt", ".png" });

                // plugins目录
                var pluginsSrc = Path.Combine(patchDir, "plugins");
                var pluginsDst = Path.Combine(exeDir, "plugins");
                if (Directory.Exists(pluginsSrc))
                    CopyDirectory(pluginsSrc, pluginsDst);

                game.FrameGenEnabled = true;
                game.FrameGenMode = "2077专用";
                return true;
            }
            catch { return false; }
        }

        // ========== DLSS5 ==========

        // 通用DLSS5（N卡）
        public static bool InstallCommonDLSS5(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "DLSS5_Common");

                // reshade基础
                var reshadeDir = GetPatchDir("dlss5 reshade配置页面");
                CopyDirectory(reshadeDir, exeDir, includeSharedFiles: true);

                // 复制中文字体文件
                CopyFontsFiles(exeDir);

                // N卡补丁
                var nCardDir = GetPatchDir("N卡DLSS5补丁");
                CopyPatchFiles(nCardDir, exeDir, backupDir);

                game.Dlss5Enabled = true;
                return true;
            }
            catch { return false; }
        }

        // 通用DLSS5（A卡）
        public static bool InstallAmdDLSS5(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "DLSS5_AMD");

                // reshade基础
                var reshadeDir = GetPatchDir("dlss5 reshade配置页面");
                CopyDirectory(reshadeDir, exeDir, includeSharedFiles: true);

                // 复制中文字体文件
                CopyFontsFiles(exeDir);

                // A卡补丁
                var amdDir = GetPatchDir("A卡DLSS5补丁（仅支持7000系和9000系）");
                CopyPatchFiles(amdDir, exeDir, backupDir);

                // 自动运行dlssnr_on_amd_setup.exe
                var setupExe = Path.Combine(exeDir, "dlssnr_on_amd_setup.exe");
                if (File.Exists(setupExe))
                {
                    System.Diagnostics.Process.Start(setupExe);
                }

                game.Dlss5Enabled = true;
                return true;
            }
            catch { return false; }
        }

        // RE引擎DLSS5
        public static bool InstallREEngineDLSS5(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "DLSS5_RE");

                // reshade基础
                var reshadeDir = GetPatchDir("dlss5 reshade配置页面");
                CopyDirectory(reshadeDir, exeDir, includeSharedFiles: true);

                // 复制中文字体文件
                CopyFontsFiles(exeDir);

                // RE框架dinput8.dll
                var dinput8 = Path.Combine(exeDir, "dinput8.dll");
                if (File.Exists(dinput8)) BackupFile(dinput8, backupDir);
                File.Copy(Path.Combine(GetPatchDir("RE框架"), "dinput8.dll"), dinput8, true);

                // DLSS5 on RE
                var reDlss5Dir = GetPatchDir(@"RE引擎通用dlss5补丁\DLSS5 on RE");
                CopyPatchFiles(reDlss5Dir, exeDir, backupDir, skipExtensions: new[] { ".jpg", ".md" });

                game.Dlss5Enabled = true;
                return true;
            }
            catch { return false; }
        }

        // 燕云十六声DLSS5
        public static bool InstallYanYunDLSS5(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "DLSS5_YanYun");
                var patchDir = GetPatchDir("燕云十六声DLSS5补丁");

                // exe目录文件
                CopyPatchFiles(patchDir, exeDir, backupDir);

                // 复制中文字体文件
                CopyFontsFiles(exeDir);

                // reshade-shaders
                var shadersSrc = Path.Combine(patchDir, "reshade-shaders");
                if (Directory.Exists(shadersSrc))
                    CopyDirectory(shadersSrc, Path.Combine(exeDir, "reshade-shaders"));

                // Streamline目录
                var streamlineSrc = Path.Combine(patchDir, "Streamline");
                var streamlineDst = Path.Combine(game.GamePath, "Streamline");
                if (Directory.Exists(streamlineSrc))
                {
                    // 查找游戏中的Streamline目录
                    var gameStreamline = GetDirectoriesWithDepthLimit(game.GamePath, "Streamline", 4).FirstOrDefault();
                    if (gameStreamline != null)
                        CopyDirectory(streamlineSrc, gameStreamline, includeSharedFiles: true);
                    else
                        CopyDirectory(streamlineSrc, streamlineDst, includeSharedFiles: true);
                }

                game.Dlss5Enabled = true;
                return true;
            }
            catch { return false; }
        }

        // DX9 DLSS5（32位）
        public static bool InstallDX9DLSS5_32(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "DX9DLSS5_32");
                var patchDir = GetPatchDir("dx9 32位dlss5补丁");

                // 根目录文件
                CopyPatchFiles(patchDir, exeDir, backupDir);

                // 复制中文字体文件
                CopyFontsFiles(exeDir);

                // reshade-shaders
                var shadersSrc = Path.Combine(patchDir, "reshade-shaders");
                if (Directory.Exists(shadersSrc))
                    CopyDirectory(shadersSrc, Path.Combine(exeDir, "reshade-shaders"));

                // host64目录
                var host64Src = Path.Combine(patchDir, "host64");
                if (Directory.Exists(host64Src))
                    CopyDirectory(host64Src, Path.Combine(exeDir, "host64"), includeSharedFiles: true);

                game.Dx9Dlss5Enabled = true;
                return true;
            }
            catch { return false; }
        }

        // DX9 DLSS5（64位）
        public static bool InstallDX9DLSS5_64(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "DX9DLSS5_64");
                var patchDir = GetPatchDir("dx9 64位dlss5补丁");

                CopyPatchFiles(patchDir, exeDir, backupDir);

                // 复制中文字体文件
                CopyFontsFiles(exeDir);

                var shadersSrc = Path.Combine(patchDir, "reshade-shaders");
                if (Directory.Exists(shadersSrc))
                    CopyDirectory(shadersSrc, Path.Combine(exeDir, "reshade-shaders"));

                game.Dx9Dlss5Enabled = true;
                return true;
            }
            catch { return false; }
        }

        // 替换dgvoodoo dll位置（移到游戏真正运行dll的目录）
        public static bool ReplaceDgvoodooDll(GameInfo game)
        {
            try
            {
                var exeDir = GetExeDir(game);
                var gameRoot = game.GamePath;
                
                // dgvoodoo三个补丁
                var dgvoodooFiles = new[] { "D3D9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe" };
                
                // 查找游戏真正运行dll的目录
                string? dllDir = null;
                
                // 1. 检查exe目录下的bin目录
                var binInExe = Path.Combine(exeDir, "bin");
                if (Directory.Exists(binInExe)) dllDir = binInExe;
                
                // 2. 检查游戏根目录下的bin目录
                if (dllDir == null)
                {
                    var binInRoot = Path.Combine(gameRoot, "bin");
                    if (Directory.Exists(binInRoot)) dllDir = binInRoot;
                }
                
                // 3. 检查游戏根目录下的Bin32目录
                if (dllDir == null)
                {
                    var bin32InRoot = Path.Combine(gameRoot, "Bin32");
                    if (Directory.Exists(bin32InRoot)) dllDir = bin32InRoot;
                }
                
                // 4. 递归搜索包含d3d9.dll或其他dll的目录（排除exe目录本身）
                if (dllDir == null)
                {
                    try
                    {
                        var dllDirs = GetDirectoriesWithDepthLimit(gameRoot, "*", 4)
                            .Where(d => d != exeDir)
                            .Where(d => Directory.GetFiles(d, "*.dll").Any())
                            .OrderBy(d => d.Length)
                            .ToList();
                        if (dllDirs.Count > 0) dllDir = dllDirs[0];
                    }
                    catch { }
                }
                
                if (dllDir == null)
                {
                    return false; // 找不到dll目录
                }
                
                // 移动dgvoodoo三个补丁到dll目录
                foreach (var file in dgvoodooFiles)
                {
                    var src = Path.Combine(exeDir, file);
                    var dest = Path.Combine(dllDir, file);
                    if (File.Exists(src))
                    {
                        // 如果目标目录已有同名文件，先删除
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(src, dest);
                    }
                }
                
                return true;
            }
            catch { return false; }
        }

        // ========== 还原 ==========

        // 还原多帧生成
        public static bool RestoreFrameGen(GameInfo game)
        {
            try
            {
                var exeDir = GetExeDir(game);
                var backupDirs = Directory.GetDirectories(Path.Combine(BackupBaseDir, game.Name))
                    .Where(d => d.Contains("FrameGen"))
                    .ToList();

                foreach (var backupDir in backupDirs)
                {
                    // 恢复备份文件
                    foreach (var file in Directory.GetFiles(backupDir))
                    {
                        var fileName = Path.GetFileName(file);
                        var target = Path.Combine(exeDir, fileName);
                        if (File.Exists(target))
                        {
                            File.Copy(file, target, true);
                        }
                    }
                }

                // 删除添加的文件（备份里有的说明是替换原文件，已经恢复了，不删除）
                var addedFiles = new[] { "RTX40MFG.asi", "RTX40MFG_config.json", "version.dll", "version.ini",
                    "nvngx_dlss.dll", "dxgi.dll", "ReShade.ini", "ReShade.log", "ReShadePreset.ini",
                    "dinput8.dll", "dlssg_sm86.ini", "RTX40MFGCore.dll", "RTX40MFG-UI.addon64" };
                foreach (var file in addedFiles)
                {
                    // 检查备份目录里是否有这个文件（有说明是替换原文件，已经恢复了）
                    bool hasBackup = false;
                    foreach (var backupDir in backupDirs)
                    {
                        if (File.Exists(Path.Combine(backupDir, file)))
                        {
                            hasBackup = true;
                            break;
                        }
                    }
                    if (hasBackup) continue; // 跳过，已经恢复了

                    var target = Path.Combine(exeDir, file);
                    if (File.Exists(target)) File.Delete(target);
                }

                // 删除配置名修改后的文件（version.dll/version.ini被重命名的情况）
                var configNames = new[] { "dinput8", "d3d11", "winmm", "d3d9", "winhttp", "wininet",
                    "dsound", "binkw64", "xinput1_3", "bink2w64", "xinput1_4", "xinputuap" };
                foreach (var name in configNames)
                {
                    // 跳过dinput8，因为上面已经删了（高级模式本身就有dinput8.dll）
                    if (name == "dinput8") continue;
                    var dllPath = Path.Combine(exeDir, name + ".dll");
                    var iniPath = Path.Combine(exeDir, name + ".ini");
                    // 只有当备份目录里没有这个文件时，才说明是我们重命名的，删除
                    bool isOriginal = false;
                    foreach (var backupDir in backupDirs)
                    {
                        if (File.Exists(Path.Combine(backupDir, name + ".dll")))
                        {
                            isOriginal = true;
                            break;
                        }
                    }
                    if (!isOriginal)
                    {
                        if (File.Exists(dllPath)) File.Delete(dllPath);
                        if (File.Exists(iniPath)) File.Delete(iniPath);
                    }
                }

                // dxgi.dll被改成d3d12.dll的情况
                var d3d12Path = Path.Combine(exeDir, "d3d12.dll");
                bool d3d12IsOriginal = false;
                foreach (var backupDir in backupDirs)
                {
                    if (File.Exists(Path.Combine(backupDir, "d3d12.dll")))
                    {
                        d3d12IsOriginal = true;
                        break;
                    }
                }
                if (!d3d12IsOriginal && File.Exists(d3d12Path))
                {
                    File.Delete(d3d12Path);
                }

                // 删除reshade-shaders目录
                var shadersDir = Path.Combine(exeDir, "reshade-shaders");
                if (Directory.Exists(shadersDir)) Directory.Delete(shadersDir, true);

                // 删除runtime目录
                var runtimeDir = Path.Combine(exeDir, "runtime");
                if (Directory.Exists(runtimeDir)) Directory.Delete(runtimeDir, true);

                // 恢复nvngx_dlssg.dll
                var dlssgPath = FindNvngxDlssg(game.GamePath);
                if (dlssgPath != null)
                {
                    foreach (var backupDir in backupDirs)
                    {
                        var backupDlssg = Path.Combine(backupDir, "nvngx_dlssg.dll");
                        if (File.Exists(backupDlssg))
                        {
                            File.Copy(backupDlssg, dlssgPath, true);
                            break;
                        }
                    }
                }

                game.FrameGenEnabled = false;
                game.FrameGenMode = "";
                return true;
            }
            catch { return false; }
        }

        // 还原DLSS5
        public static bool RestoreDLSS5(GameInfo game)
        {
            try
            {
                var exeDir = GetExeDir(game);
                var backupDirs = Directory.GetDirectories(Path.Combine(BackupBaseDir, game.Name))
                    .Where(d => d.Contains("DLSS5"))
                    .ToList();

                // 恢复备份文件
                foreach (var backupDir in backupDirs)
                {
                    foreach (var file in Directory.GetFiles(backupDir))
                    {
                        var fileName = Path.GetFileName(file);
                        var target = Path.Combine(exeDir, fileName);
                        if (File.Exists(target)) File.Copy(file, target, true);
                    }
                }

                // 删除DLSS5添加的文件
                var dlss5Files = new[] { "dxgi.dll", "ReShade.ini", "ReShade.log", "ReShadePreset.ini",
                    "nvngx_dlssnr.dll", "renodx-dlss5.addon64", "dinput8.dll", "D3DCompiler_47.dll",
                    "RE_DLSS5_Core.dll", "RE_DLSS5_Core_settings.json", "dlssnr_on_amd_setup.exe",
                    "D3D9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe", "dlss5-feed.addon32",
                    "dlss5-feed.addon64", "nvngx_dlss.dll" };
                foreach (var file in dlss5Files)
                {
                    var target = Path.Combine(exeDir, file);
                    if (File.Exists(target)) File.Delete(target);
                }
                
                // 递归搜索游戏目录中的dgvoodoo文件（替换dll后可能在其他目录）
                var dgvoodooFiles = new[] { "D3D9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe" };
                try
                {
                    foreach (var file in dgvoodooFiles)
                    {
                        var foundFiles = GetFilesWithDepthLimit(game.GamePath, file, 5);
                        foreach (var foundFile in foundFiles)
                        {
                            // 确保不是备份目录中的文件
                            if (!foundFile.Contains(BackupBaseDir))
                            {
                                File.Delete(foundFile);
                            }
                        }
                    }
                }
                catch { }

                // 删除目录
                var dirsToDelete = new[] { "reshade-shaders", "host64", "plugins", "Fonts" };
                foreach (var dirName in dirsToDelete)
                {
                    var dir = Path.Combine(exeDir, dirName);
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
                
                // 递归搜索游戏目录中的reshade-shaders和host64目录
                try
                {
                    var dirsToSearch = new[] { "reshade-shaders", "host64" };
                    foreach (var dirName in dirsToSearch)
                    {
                        var foundDirs = GetDirectoriesWithDepthLimit(game.GamePath, dirName, 5);
                        foreach (var foundDir in foundDirs)
                        {
                            if (!foundDir.Contains(BackupBaseDir))
                            {
                                Directory.Delete(foundDir, true);
                            }
                        }
                    }
                }
                catch { }

                game.Dlss5Enabled = false;
                game.Dx9Dlss5Enabled = false;
                return true;
            }
            catch { return false; }
        }

        // 检查是否有备份
        public static bool HasBackup(GameInfo game)
        {
            var gameBackupDir = Path.Combine(BackupBaseDir, game.Name);
            return Directory.Exists(gameBackupDir) && Directory.GetDirectories(gameBackupDir).Length > 0;
        }

        // 无责还原（删除所有补丁文件，不恢复）
        public static bool ForceClean(GameInfo game)
        {
            try
            {
                var exeDir = GetExeDir(game);
                var allPatchFiles = new[] { "RTX40MFG.asi", "RTX40MFG_config.json", "version.dll", "version.ini",
                    "nvngx_dlss.dll", "nvngx_dlssg.dll", "nvngx_dlssnr.dll", "nvngx_dlssd.dll",
                    "dxgi.dll", "ReShade.ini", "ReShade.log", "ReShadePreset.ini",
                    "dinput8.dll", "dlssg_sm86.ini", "RTX40MFGCore.dll", "RTX40MFG-UI.addon64",
                    "renodx-dlss5.addon64", "D3DCompiler_47.dll", "RE_DLSS5_Core.dll",
                    "RE_DLSS5_Core_settings.json", "dlssnr_on_amd_setup.exe",
                    "D3D9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe", "dlss5-feed.addon32",
                    "dlss5-feed.addon64", "sl.common.dll", "sl.dlss.dll", "sl.dlss_g.dll",
                    "sl.interposer.dll", "sl.nis.dll", "sl.reflex.dll", "sl.pcl.dll",
                    "sl.deepdvc.dll", "sl.dlss_d.dll", "sl.dlss_nr.dll", "sl.nvperf.dll",
                    "sl.directsr.dll", "NvLowLatencyVk.dll", "nvngx_deepdvc.dll" };

                foreach (var file in allPatchFiles)
                {
                    var target = Path.Combine(exeDir, file);
                    if (File.Exists(target)) File.Delete(target);
                }

                var dirsToDelete = new[] { "reshade-shaders", "host64", "plugins", "runtime", "Streamline", "Fonts" };
                foreach (var dirName in dirsToDelete)
                {
                    var dir = Path.Combine(exeDir, dirName);
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }

                game.FrameGenEnabled = false;
                game.Dlss5Enabled = false;
                game.Dx9Dlss5Enabled = false;
                return true;
            }
            catch { return false; }
        }

        // 查找游戏中sl.文件所在的目录
        private static string? FindSlFilesDir(string gameRoot)
        {
            try
            {
                // 先在exe目录找
                // 然后递归搜索sl.common.dll（最多搜索5层深度，避免太慢）
                var found = GetFilesWithDepthLimit(gameRoot, "sl.common.dll", 5);
                if (found.Length > 0)
                {
                    return Path.GetDirectoryName(found[0]);
                }
            }
            catch { }
            return null;
        }

        // 获取排错文件列表（返回：文件名，游戏中是否存在）
        public static List<(string fileName, bool existsInGame)> GetTroubleshootFiles(GameInfo game)
        {
            var result = new List<(string, bool)>();
            try
            {
                EnsurePatchesExtracted();
                var slDir = FindSlFilesDir(game.GamePath);
                if (slDir == null) return result;

                string patchDir;
                if (game.FrameGenMode == "经典模式")
                {
                    patchDir = GetPatchDir(@"老驱动多帧生成\排错");
                }
                else
                {
                    patchDir = GetPatchDir(@"40系原生多帧生成\排错");
                }

                if (!Directory.Exists(patchDir)) return result;

                var patchFiles = Directory.GetFiles(patchDir, "sl.*.dll");
                foreach (var patchFile in patchFiles)
                {
                    var fileName = Path.GetFileName(patchFile);
                    var existsInGame = File.Exists(Path.Combine(slDir, fileName));
                    result.Add((fileName, existsInGame));
                }
            }
            catch { }
            return result;
        }

        // 排错修复：替换指定的sl.开头文件
        public static int TroubleshootSelected(GameInfo game, List<string> filesToReplace)
        {
            try
            {
                EnsurePatchesExtracted();
                var slDir = FindSlFilesDir(game.GamePath);
                if (slDir == null) return 0;

                var backupDir = GetBackupDir(game, "Troubleshoot");

                string patchDir;
                if (game.FrameGenMode == "经典模式")
                {
                    patchDir = GetPatchDir(@"老驱动多帧生成\排错");
                }
                else
                {
                    patchDir = GetPatchDir(@"40系原生多帧生成\排错");
                }

                int replaced = 0;
                foreach (var fileName in filesToReplace)
                {
                    var patchFile = Path.Combine(patchDir, fileName);
                    var target = Path.Combine(slDir, fileName);
                    if (File.Exists(patchFile) && File.Exists(target))
                    {
                        BackupFile(target, backupDir);
                        File.Copy(patchFile, target, true);
                        replaced++;
                    }
                }
                return replaced;
            }
            catch { return 0; }
        }

        // 排错修复：替换游戏里已有的sl.开头文件
        public static (int replaced, int skipped) Troubleshoot(GameInfo game)
        {
            try
            {
                EnsurePatchesExtracted();
                var exeDir = GetExeDir(game);
                var backupDir = GetBackupDir(game, "Troubleshoot");

                // 根据模式选择排错补丁目录
                string patchDir;
                if (game.FrameGenMode == "经典模式")
                {
                    patchDir = GetPatchDir(@"老驱动多帧生成\排错");
                }
                else
                {
                    patchDir = GetPatchDir(@"40系原生多帧生成\排错");
                }

                if (!Directory.Exists(patchDir)) return (0, 0);

                int replaced = 0;
                int skipped = 0;

                // 只替换游戏里已存在的sl.开头文件
                var patchFiles = Directory.GetFiles(patchDir, "sl.*.dll");
                foreach (var patchFile in patchFiles)
                {
                    var fileName = Path.GetFileName(patchFile);
                    var target = Path.Combine(exeDir, fileName);

                    if (File.Exists(target))
                    {
                        // 备份原文件
                        BackupFile(target, backupDir);
                        // 替换
                        File.Copy(patchFile, target, true);
                        replaced++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                return (replaced, skipped);
            }
            catch { return (0, 0); }
        }
    }
}
