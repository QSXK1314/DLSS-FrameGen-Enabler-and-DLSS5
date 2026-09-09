using System.Reflection;
using DLSSFrameGenEnabler.Models;

namespace DLSSFrameGenEnabler.Services;

/// <summary>
/// 文件补丁服务：负责备份、替换、还原 DLSS 相关文件
/// 支持外部 Patches 文件夹优先，内置嵌入资源兜底（单文件独立运行）
/// </summary>
public class FilePatcher
{
    /// <summary>当前版本号（用于缓存目录命名）</summary>
    private const string CurrentVersion = "1.9.7.1";

    /// <summary>补丁文件所在目录（外部文件夹或临时提取目录）</summary>
    private string? _patchesDir;

    /// <summary>
    /// 补丁目录（延迟初始化，首次访问时提取嵌入资源）
    /// </summary>
    private string PatchesDirectory => EnsurePatchDirectory();

    /// <summary>是否使用了内置嵌入资源（外部文件夹不完整时）</summary>
    public bool UsingEmbeddedResources { get; private set; }

    /// <summary>备份目录名后缀</summary>
    private const string BackupSuffix = "_dlss_backup";

    /// <summary>需要替换的 DLL 文件名</summary>
    private const string DlssgDllName = "nvngx_dlssg.dll";

    /// <summary>需要复制到游戏 exe 目录的补丁文件</summary>
    private static readonly string[] PatchFiles = { "RTX40MFG.asi", "RTX40MFG_config.json", "version.dll" };

    /// <summary>所有需要的补丁文件（含替换用的 DLL）</summary>
    private static readonly string[] AllPatchFiles = { DlssgDllName, "RTX40MFG.asi", "RTX40MFG_config.json", "version.dll" };

    public FilePatcher()
    {
        // 不在构造函数中提取补丁，改为延迟初始化
        // 先检查外部目录和缓存目录，快速返回
        _patchesDir = ResolvePatchDirectoryFast();
    }

    /// <summary>
    /// 快速解析补丁目录：优先外部文件夹，其次缓存目录，都不完整则返回null（延迟提取）
    /// </summary>
    private string? ResolvePatchDirectoryFast()
    {
        var externalDir = Path.Combine(AppContext.BaseDirectory, "Patches");

        // 检查外部文件夹是否完整
        if (CheckFilesExist(externalDir, AllPatchFiles))
        {
            UsingEmbeddedResources = false;
            return externalDir;
        }

        // 检查缓存目录是否完整（使用固定目录名，基于版本号）
        var cacheDir = Path.Combine(Path.GetTempPath(), $"DLSSFrameGenPatchFiles_{CurrentVersion}");
        if (CheckFilesExist(cacheDir, AllPatchFiles))
        {
            UsingEmbeddedResources = true;
            return cacheDir;
        }

        // 都不完整，返回null，延迟到需要时再提取
        return null;
    }

    /// <summary>
    /// 确保补丁目录可用（延迟提取）
    /// </summary>
    private string EnsurePatchDirectory()
    {
        if (_patchesDir != null && Directory.Exists(_patchesDir) && CheckFilesExist(_patchesDir, AllPatchFiles))
        {
            return _patchesDir;
        }

        // 需要提取嵌入资源到缓存目录
        var cacheDir = Path.Combine(Path.GetTempPath(), $"DLSSFrameGenPatchFiles_{CurrentVersion}");
        try
        {
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.";

            foreach (var fileName in AllPatchFiles)
            {
                var outputPath = Path.Combine(cacheDir, fileName);
                if (File.Exists(outputPath)) continue; // 已存在的文件跳过

                var resourceName = resourcePrefix + fileName;
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) continue;

                using var fileStream = File.Create(outputPath);
                stream.CopyTo(fileStream);
            }

            UsingEmbeddedResources = true;
            _patchesDir = cacheDir;
            return cacheDir;
        }
        catch
        {
            // 提取失败，返回外部目录
            UsingEmbeddedResources = false;
            var externalDir = Path.Combine(AppContext.BaseDirectory, "Patches");
            _patchesDir = externalDir;
            return externalDir;
        }
    }

    /// <summary>
    /// 检查指定目录下是否存在所有指定文件
    /// </summary>
    private static bool CheckFilesExist(string directory, string[] files)
    {
        if (!Directory.Exists(directory)) return false;
        foreach (var f in files)
        {
            if (!File.Exists(Path.Combine(directory, f)))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 快速检查补丁文件是否齐全（不触发补丁提取，用于启动时快速检测）
    /// </summary>
    public (bool Ok, bool UsingEmbedded, bool NeedExtract) QuickCheckPatchFiles()
    {
        // 检查外部文件夹
        var externalDir = Path.Combine(AppContext.BaseDirectory, "Patches");
        if (CheckFilesExist(externalDir, AllPatchFiles))
        {
            return (true, false, false);
        }

        // 检查缓存目录
        var cacheDir = Path.Combine(Path.GetTempPath(), $"DLSSFrameGenPatchFiles_{CurrentVersion}");
        if (CheckFilesExist(cacheDir, AllPatchFiles))
        {
            return (true, true, false);
        }

        // 都不完整，需要提取
        return (false, false, true);
    }

    /// <summary>
    /// 检查补丁文件是否齐全
    /// </summary>
    public (bool Ok, string MissingFiles, bool UsingEmbedded) CheckPatchFiles()
    {
        var dir = EnsurePatchDirectory();
        var missing = new List<string>();

        foreach (var f in AllPatchFiles)
        {
            var path = Path.Combine(dir, f);
            if (!File.Exists(path))
                missing.Add(f);
        }

        return (missing.Count == 0, string.Join(", ", missing), UsingEmbeddedResources);
    }

    /// <summary>
    /// 为游戏开启 DLSS 多帧生成（一键补丁）
    /// </summary>
    public PatchResult ApplyPatch(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new PatchResult { GameName = game.Name };

        try
        {
            // 0. 前置检查
            progress?.Report("检查补丁文件...");
            var (ok, missing, _) = CheckPatchFiles();
            if (!ok)
            {
                result.Success = false;
                result.Message = $"补丁文件缺失：{missing}\n请将补丁文件放入程序目录下的 Patches 文件夹中。";
                return result;
            }

            if (string.IsNullOrEmpty(game.DlssgDllPath) || !File.Exists(game.DlssgDllPath))
            {
                result.Success = false;
                result.Message = "未找到 nvngx_dlssg.dll 文件，无法打补丁。";
                return result;
            }

            if (string.IsNullOrEmpty(game.GameExeDirectory) || !Directory.Exists(game.GameExeDirectory))
            {
                result.Success = false;
                result.Message = "未找到游戏 exe 所在目录，无法打补丁。";
                return result;
            }

            // 1. 备份原文件
            progress?.Report("正在备份原文件...");
            var backupDir = GetBackupDirectory(game);
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            // 记录游戏目录文件快照（用于还原时精确删除新增文件，不误删游戏文件）
            SnapshotGameFiles(game.GameExeDirectory, backupDir);

            // 备份原 nvngx_dlssg.dll
            var backupDllPath = Path.Combine(backupDir, DlssgDllName);
            if (!File.Exists(backupDllPath))
            {
                File.Copy(game.DlssgDllPath, backupDllPath, true);
            }

            // 备份游戏 exe 目录下可能已存在的补丁文件（还原时需要）
            foreach (var f in PatchFiles)
            {
                var existingPath = Path.Combine(game.GameExeDirectory, f);
                var backupPath = Path.Combine(backupDir, f);
                if (File.Exists(existingPath) && !File.Exists(backupPath))
                {
                    File.Copy(existingPath, backupPath, true);
                }
            }

            // 写入备份元数据
            var metaPath = Path.Combine(backupDir, "backup_info.txt");
            File.WriteAllText(metaPath,
                $"游戏名称: {game.Name}\r\n" +
                $"游戏路径: {game.InstallPath}\r\n" +
                $"原DLL路径: {game.DlssgDllPath}\r\n" +
                $"游戏EXE目录: {game.GameExeDirectory}\r\n" +
                $"备份时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");

            // 2. 替换 nvngx_dlssg.dll
            progress?.Report("正在替换 nvngx_dlssg.dll...");
            var replacementDll = Path.Combine(PatchesDirectory, DlssgDllName);
            File.Copy(replacementDll, game.DlssgDllPath, true);

            // 3. 复制三个补丁文件到游戏 exe 目录
            progress?.Report("正在复制补丁文件到游戏目录...");
            foreach (var f in PatchFiles)
            {
                var src = Path.Combine(PatchesDirectory, f);
                var dst = Path.Combine(game.GameExeDirectory, f);
                File.Copy(src, dst, true);
            }

            var sourceInfo = UsingEmbeddedResources ? "内置补丁" : "外部 Patches 文件夹";
            result.Success = true;
            result.Message = $"补丁已成功应用到《{game.Name}》！\n" +
                           $"补丁来源：{sourceInfo}\n" +
                           $"替换文件：{DlssgDllName}\n" +
                           $"补丁文件：{string.Join("、", PatchFiles)}\n" +
                           $"备份位置：{backupDir}\n" +
                           $"现在可以启动游戏体验多帧生成了。";
            game.IsPatched = true;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。";
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行，且相关文件未被占用。";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"打补丁时发生错误：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 应用RTX20系多帧生成补丁
    /// </summary>
    public PatchResult ApplyRTX20Patch(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new PatchResult { GameName = game.Name };
        try
        {
            progress?.Report("检查RTX20系补丁文件...");
            var patchDir = GetRTX20Directory();
            if (string.IsNullOrEmpty(game.DlssgDllPath) || !File.Exists(game.DlssgDllPath))
            { result.Success = false; result.Message = "未找到 nvngx_dlssg.dll 文件，无法打补丁。"; return result; }
            if (string.IsNullOrEmpty(game.GameExeDirectory) || !Directory.Exists(game.GameExeDirectory))
            { result.Success = false; result.Message = "未找到游戏 exe 所在目录，无法打补丁。"; return result; }

            progress?.Report("正在备份原文件...");
            var backupDir = GetBackupDirectory(game);
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
            SnapshotGameFiles(game.GameExeDirectory, backupDir);
            var backupDllPath = Path.Combine(backupDir, DlssgDllName);
            if (!File.Exists(backupDllPath)) File.Copy(game.DlssgDllPath, backupDllPath, true);
            foreach (var f in new[] { "dinput8.dll", "dlssg_sm86.ini", "version.dll", "sm75_backend.dll" })
            {
                var existingPath = Path.Combine(game.GameExeDirectory, f);
                var backupPath = Path.Combine(backupDir, f);
                if (File.Exists(existingPath) && !File.Exists(backupPath)) File.Copy(existingPath, backupPath, true);
            }
            File.WriteAllText(Path.Combine(backupDir, "backup_info.txt"), $"游戏名称: {game.Name}\r\n补丁类型: RTX20系多帧生成\r\n备份时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");

            progress?.Report("正在替换 nvngx_dlssg.dll...");
            var rtDll = Path.Combine(patchDir, "runtime", "nvngx_dlssg.dll");
            if (File.Exists(rtDll)) File.Copy(rtDll, game.DlssgDllPath, true);
            progress?.Report("正在复制RTX20系补丁文件...");
            foreach (var f in new[] { "dinput8.dll", "dlssg_sm86.ini", "version.dll" })
            {
                var src = Path.Combine(patchDir, f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(game.GameExeDirectory, f), true);
            }
            var runtimeDir = Path.Combine(patchDir, "runtime");
            if (Directory.Exists(runtimeDir))
                foreach (var f in Directory.GetFiles(runtimeDir))
                    File.Copy(f, Path.Combine(game.GameExeDirectory, Path.GetFileName(f)), true);

            result.Success = true;
            result.Message = $"RTX20系多帧生成补丁已成功应用到《{game.Name}》！\n现在可以启动游戏体验多帧生成了。";
            game.IsPatched = true;
            game.PatchMode = "RTX20";
        }
        catch (UnauthorizedAccessException ex) { result.Success = false; result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。"; }
        catch (IOException ex) { result.Success = false; result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。"; }
        catch (Exception ex) { result.Success = false; result.Message = $"打补丁时发生错误：{ex.Message}"; }
        return result;
    }

    /// <summary>
    /// 应用RTX30系多帧生成补丁
    /// </summary>
    public PatchResult ApplyRTX30Patch(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new PatchResult { GameName = game.Name };
        try
        {
            progress?.Report("检查RTX30系补丁文件...");
            var patchDir = GetRTX30Directory();
            if (string.IsNullOrEmpty(game.GameExeDirectory) || !Directory.Exists(game.GameExeDirectory))
            { result.Success = false; result.Message = "未找到游戏 exe 所在目录，无法打补丁。"; return result; }

            progress?.Report("正在备份原文件...");
            var backupDir = GetBackupDirectory(game);
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
            SnapshotGameFiles(game.GameExeDirectory, backupDir);
            foreach (var f in new[] { "dlssg_sm86.ini", "version.dll" })
            {
                var existingPath = Path.Combine(game.GameExeDirectory, f);
                var backupPath = Path.Combine(backupDir, f);
                if (File.Exists(existingPath) && !File.Exists(backupPath)) File.Copy(existingPath, backupPath, true);
            }
            File.WriteAllText(Path.Combine(backupDir, "backup_info.txt"), $"游戏名称: {game.Name}\r\n补丁类型: RTX30系多帧生成\r\n备份时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");

            progress?.Report("正在复制RTX30系补丁文件...");
            foreach (var f in new[] { "dlssg_sm86.ini", "version.dll" })
            {
                var src = Path.Combine(patchDir, f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(game.GameExeDirectory, f), true);
            }

            result.Success = true;
            result.Message = $"RTX30系多帧生成补丁已成功应用到《{game.Name}》！\n现在可以启动游戏体验多帧生成了。";
            game.IsPatched = true;
            game.PatchMode = "RTX30";
        }
        catch (UnauthorizedAccessException ex) { result.Success = false; result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。"; }
        catch (IOException ex) { result.Success = false; result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。"; }
        catch (Exception ex) { result.Success = false; result.Message = $"打补丁时发生错误：{ex.Message}"; }
        return result;
    }

    /// <summary>
    /// 应用RE引擎多帧生成补丁
    /// </summary>
    public PatchResult ApplyREFrameGenPatch(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new PatchResult { GameName = game.Name };
        try
        {
            progress?.Report("检查RE引擎多帧生成补丁文件...");
            var patchDir = GetREFrameGenDirectory();
            if (string.IsNullOrEmpty(game.DlssgDllPath) || !File.Exists(game.DlssgDllPath))
            { result.Success = false; result.Message = "未找到 nvngx_dlssg.dll 文件，无法打补丁。"; return result; }
            if (string.IsNullOrEmpty(game.GameExeDirectory) || !Directory.Exists(game.GameExeDirectory))
            { result.Success = false; result.Message = "未找到游戏 exe 所在目录，无法打补丁。"; return result; }

            progress?.Report("正在备份原文件...");
            var backupDir = GetBackupDirectory(game);
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
            SnapshotGameFiles(game.GameExeDirectory, backupDir);
            var backupDllPath = Path.Combine(backupDir, DlssgDllName);
            if (!File.Exists(backupDllPath)) File.Copy(game.DlssgDllPath, backupDllPath, true);
            foreach (var f in new[] { "dlssg_sm86.ini", "nvngx_dlss.dll", "version.dll", "dinput8.dll" })
            {
                var existingPath = Path.Combine(game.GameExeDirectory, f);
                var backupPath = Path.Combine(backupDir, f);
                if (File.Exists(existingPath) && !File.Exists(backupPath)) File.Copy(existingPath, backupPath, true);
            }
            File.WriteAllText(Path.Combine(backupDir, "backup_info.txt"), $"游戏名称: {game.Name}\r\n补丁类型: RE引擎多帧生成\r\n备份时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");

            progress?.Report("正在替换 nvngx_dlssg.dll...");
            var reDll = Path.Combine(patchDir, "nvngx_dlssg.dll");
            if (File.Exists(reDll)) File.Copy(reDll, game.DlssgDllPath, true);
            progress?.Report("正在复制RE引擎多帧生成补丁文件...");
            foreach (var f in new[] { "dlssg_sm86.ini", "version.dll" })
            {
                var src = Path.Combine(patchDir, f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(game.GameExeDirectory, f), true);
            }
            // nvngx_dlss.dll 从 Shared 共享目录复制（避免重复存储）
            var sharedDlss = Path.Combine(GetSharedDirectory(), "nvngx_dlss.dll");
            if (File.Exists(sharedDlss)) File.Copy(sharedDlss, Path.Combine(game.GameExeDirectory, "nvngx_dlss.dll"), true);
            // RE引擎需要dinput8.dll（RE框架）
            var reEngineDir = GetDlss5REEngineDirectory();
            var dinput8 = Path.Combine(reEngineDir, "dinput8.dll");
            if (File.Exists(dinput8)) File.Copy(dinput8, Path.Combine(game.GameExeDirectory, "dinput8.dll"), true);

            result.Success = true;
            result.Message = $"RE引擎多帧生成补丁已成功应用到《{game.Name}》！\n现在可以启动游戏体验多帧生成了。";
            game.IsPatched = true;
            game.PatchMode = "REFrameGen";
        }
        catch (UnauthorizedAccessException ex) { result.Success = false; result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。"; }
        catch (IOException ex) { result.Success = false; result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。"; }
        catch (Exception ex) { result.Success = false; result.Message = $"打补丁时发生错误：{ex.Message}"; }
        return result;
    }

    /// <summary>
    /// 还原游戏到打补丁前的状态
    /// </summary>
    public PatchResult RestorePatch(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new PatchResult { GameName = game.Name };

        try
        {
            var backupDir = GetBackupDirectory(game);
            if (!Directory.Exists(backupDir))
            {
                result.Success = false;
                result.Message = "未找到备份文件，无法还原。可能从未打过补丁，或备份已被删除。";
                return result;
            }

            // 1. 还原 nvngx_dlssg.dll（优先经典模式备份，其次高级模式备份，最后2077专用备份）
            progress?.Report("正在还原 nvngx_dlssg.dll...");
            var backupDll = Path.Combine(backupDir, DlssgDllName);
            var advancedBackupDll = Path.Combine(backupDir, "Advanced", DlssgDllName);
            var cyberpunkBackupDll = Path.Combine(backupDir, "Cyberpunk2077", DlssgDllName);
            string? sourceDll = null;
            if (File.Exists(backupDll))
                sourceDll = backupDll;
            else if (File.Exists(advancedBackupDll))
                sourceDll = advancedBackupDll;
            else if (File.Exists(cyberpunkBackupDll))
                sourceDll = cyberpunkBackupDll;

            if (sourceDll != null && !string.IsNullOrEmpty(game.DlssgDllPath))
            {
                File.Copy(sourceDll, game.DlssgDllPath, true);
            }

            // 2. 处理游戏 exe 目录下的补丁文件
            progress?.Report("正在清理补丁文件...");
            if (!string.IsNullOrEmpty(game.GameExeDirectory))
            {
                foreach (var f in PatchFiles)
                {
                    var gameFilePath = Path.Combine(game.GameExeDirectory, f);
                    var backupFilePath = Path.Combine(backupDir, f);

                    if (File.Exists(backupFilePath))
                    {
                        // 备份里有原文件，还原回去
                        File.Copy(backupFilePath, gameFilePath, true);
                    }
                    else if (File.Exists(gameFilePath))
                    {
                        // 备份里没有，说明是补丁新增的，删除
                        File.Delete(gameFilePath);
                    }
                }
            }

            // 3. 还原高级模式备份（如果有）
            var advancedBackupDir = Path.Combine(backupDir, "Advanced");
            if (Directory.Exists(advancedBackupDir) && !string.IsNullOrEmpty(game.GameExeDirectory))
            {
                progress?.Report("正在还原高级模式文件...");

                // 3.1 还原高级模式的 nvngx_dlssg.dll
                var advDlssgBackup = Path.Combine(advancedBackupDir, "nvngx_dlssg.dll");
                if (File.Exists(advDlssgBackup) && !string.IsNullOrEmpty(game.DlssgDllPath))
                {
                    File.Copy(advDlssgBackup, game.DlssgDllPath, true);
                }

                // 3.2 高级模式安装的所有文件（用于清理）
                var allAdvancedFiles = new List<string> { "version.dll", "version.ini", "nvngx_dlssg.dll" };
                allAdvancedFiles.AddRange(AdvancedReshadeFiles);
                allAdvancedFiles.AddRange(AdvancedUnlockFiles);

                foreach (var fileName in allAdvancedFiles)
                {
                    var gameFilePath = Path.Combine(game.GameExeDirectory, fileName);
                    var backupFilePath = Path.Combine(advancedBackupDir, fileName);

                    if (File.Exists(backupFilePath))
                    {
                        File.Copy(backupFilePath, gameFilePath, true);
                    }
                    else if (File.Exists(gameFilePath))
                    {
                        File.Delete(gameFilePath);
                    }
                }

                // 3.3 清理配置名修改后的文件（version.dll 被改成其他名字）
                var configNames = new[] { "dinput8", "d3d11", "winmm", "d3d9", "winhttp", "wininet",
                                          "dsound", "binkw64", "xinput1_3", "bink2w64", "xinput1_4", "xinputuap" };
                foreach (var cfgName in configNames)
                {
                    var cfgDll = Path.Combine(game.GameExeDirectory, cfgName + ".dll");
                    var cfgIni = Path.Combine(game.GameExeDirectory, cfgName + ".ini");
                    // 只有当备份里没有这个文件时才删除（说明是改名产生的，不是游戏原有的）
                    var cfgDllBackup = Path.Combine(advancedBackupDir, cfgName + ".dll");
                    var cfgIniBackup = Path.Combine(advancedBackupDir, cfgName + ".ini");
                    if (File.Exists(cfgDll) && !File.Exists(cfgDllBackup))
                        File.Delete(cfgDll);
                    if (File.Exists(cfgIni) && !File.Exists(cfgIniBackup))
                        File.Delete(cfgIni);
                }

                // 3.4 清理 dxgi.dll 改成 d3d12.dll 的情况
                var d3d12Dll = Path.Combine(game.GameExeDirectory, "d3d12.dll");
                var d3d12Backup = Path.Combine(advancedBackupDir, "d3d12.dll");
                if (File.Exists(d3d12Dll) && !File.Exists(d3d12Backup))
                    File.Delete(d3d12Dll);
                else if (File.Exists(d3d12Backup))
                    File.Copy(d3d12Backup, d3d12Dll, true);

                // 3.5 如果 global.ini 在备份里存在，说明原来就有 global.ini，需要还原
                var globalIniBackup = Path.Combine(advancedBackupDir, "global.ini");
                if (File.Exists(globalIniBackup))
                {
                    var globalIniPath = Path.Combine(game.GameExeDirectory, "global.ini");
                    File.Copy(globalIniBackup, globalIniPath, true);
                }

                // 删除高级模式备份目录
                try { Directory.Delete(advancedBackupDir, true); } catch { }
            }

            // 3.5 还原经典排错备份（如果有）
            var troubleshootBackupDir = Path.Combine(backupDir, "Troubleshoot");
            if (Directory.Exists(troubleshootBackupDir))
            {
                progress?.Report("正在还原经典排错备份文件...");
                var slFiles = ScanExistingSlDlls(game.InstallPath);
                foreach (var gameFilePath in slFiles)
                {
                    var fileName = Path.GetFileName(gameFilePath);
                    var backupFilePath = Path.Combine(troubleshootBackupDir, fileName);
                    if (File.Exists(backupFilePath))
                    {
                        File.Copy(backupFilePath, gameFilePath, true);
                    }
                }
                try { Directory.Delete(troubleshootBackupDir, true); } catch { }
            }

            // 3.6 还原2077专用补丁备份（如果有）
            var cyberpunkBackupDir = Path.Combine(backupDir, "Cyberpunk2077");
            if (Directory.Exists(cyberpunkBackupDir))
            {
                progress?.Report("正在还原2077专用补丁文件...");

                // 2077的exe目录可能是 bin\x64，需要重新获取
                var exeDir = game.GameExeDirectory;
                if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
                {
                    var binX64 = GameScanner.FindFileInDirectory(game.InstallPath, "Cyberpunk2077.exe");
                    if (!string.IsNullOrEmpty(binX64))
                        exeDir = Path.GetDirectoryName(binX64);
                    else
                    {
                        var potentialDir = Path.Combine(game.InstallPath, "bin", "x64");
                        if (Directory.Exists(potentialDir))
                            exeDir = potentialDir;
                    }
                }

                if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
                {
                    // 还原所有2077文件
                    foreach (var fileName in Cyberpunk2077Files)
                    {
                        var gameFilePath = Path.Combine(exeDir, fileName);
                        var backupFilePath = Path.Combine(cyberpunkBackupDir, fileName);

                        if (File.Exists(backupFilePath))
                        {
                            File.Copy(backupFilePath, gameFilePath, true);
                        }
                        else if (File.Exists(gameFilePath))
                        {
                            File.Delete(gameFilePath);
                        }
                    }

                    // 还原 plugins 目录
                    var pluginsBackupDir = Path.Combine(cyberpunkBackupDir, "plugins");
                    var pluginsGameDir = Path.Combine(exeDir, "plugins");
                    if (Directory.Exists(pluginsBackupDir) && Directory.Exists(pluginsGameDir))
                    {
                        try { Directory.Delete(pluginsGameDir, true); } catch { }
                    }
                }

                try { Directory.Delete(cyberpunkBackupDir, true); } catch { }
            }

            // 3.6 还原高级排错备份（如果有）
            var advTsBackupDir = Path.Combine(backupDir, "AdvancedTroubleshoot");
            if (Directory.Exists(advTsBackupDir))
            {
                progress?.Report("正在还原高级排错备份文件...");
                // 高级排错的文件在游戏各个目录中，需要扫描游戏目录中的sl.*.dll并还原
                var slFiles = ScanExistingSlDlls(game.InstallPath);
                foreach (var gameFilePath in slFiles)
                {
                    var fileName = Path.GetFileName(gameFilePath);
                    var backupFilePath = Path.Combine(advTsBackupDir, fileName);
                    if (File.Exists(backupFilePath))
                    {
                        File.Copy(backupFilePath, gameFilePath, true);
                    }
                }
                try { Directory.Delete(advTsBackupDir, true); } catch { }
            }

            // 4. 快照对比：删除所有安装后新增的文件（确保不误删游戏本身文件）
            if (!string.IsNullOrEmpty(game.GameExeDirectory) && Directory.Exists(game.GameExeDirectory))
            {
                progress?.Report("正在清理新增文件...");
                var (_, deleted) = RestoreFromSnapshot(game.GameExeDirectory, backupDir);
            }

            // 5. 删除备份目录
            try
            {
                Directory.Delete(backupDir, true);
            }
            catch { }

            result.Success = true;
            result.Message = $"《{game.Name}》已成功还原到原始状态。所有补丁文件已清理，游戏原有文件已恢复。";
            game.IsPatched = false;
            game.IsAdvancedPatched = false;
            game.IsCyberpunkPatched = false;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。";
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"还原时发生错误：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 获取游戏的备份目录路径
    /// </summary>
    private static string GetBackupDirectory(GameInfo game)
    {
        // 备份放在游戏安装目录同级，带游戏名标识
        var parentDir = Path.GetDirectoryName(game.InstallPath.TrimEnd('\\')) ?? game.InstallPath;
        var safeName = string.Join("_", game.Name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(parentDir, $"{safeName}{BackupSuffix}");
    }

    /// <summary>
    /// 检查游戏是否有备份（可还原）
    /// </summary>
    public static bool HasBackup(GameInfo game)
    {
        var backupDir = GetBackupDirectory(game);
        if (!Directory.Exists(backupDir)) return false;

        // 检查经典模式备份
        if (File.Exists(Path.Combine(backupDir, DlssgDllName)))
            return true;

        // 检查高级模式备份
        if (Directory.Exists(Path.Combine(backupDir, "Advanced")))
            return true;

        // 检查2077专用补丁备份
        if (Directory.Exists(Path.Combine(backupDir, "Cyberpunk2077")))
            return true;

        // 检查经典排错备份
        if (Directory.Exists(Path.Combine(backupDir, "Troubleshoot")))
            return true;

        // 检查高级排错备份
        if (Directory.Exists(Path.Combine(backupDir, "AdvancedTroubleshoot")))
            return true;

        return false;
    }

    /// <summary>
    /// 检测游戏目录是否有补丁特征文件（即使没有备份，用于无责还原）
    /// </summary>
    public static bool HasPatchFiles(GameInfo game)
    {
        var exeDir = game.GameExeDirectory;
        if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
            return false;

        // 多帧生成特征文件
        string[] patchIndicators = {
            "nvngx_dlssg.dll",      // 帧生成
            "nvngx_dlssnr.dll",     // DLSS5
            "nvngx_dlss.dll",       // DLSS超分
            "RTX40MFG.asi",         // 经典模式解锁
            "RTX40MFG_config.json", // 经典模式配置
            "version.dll",          // 高级模式
            "version.ini",          // 高级模式配置
            "dxgi.dll",             // ReShade框架
            "ReShade.ini",          // ReShade配置
            "d3d9.dll",             // DX9 dgVoodoo
            "dgVoodoo.conf",        // dgVoodoo配置
        };

        foreach (var file in patchIndicators)
        {
            if (File.Exists(Path.Combine(exeDir, file)))
                return true;
        }

        // 检查reshade-shaders目录
        if (Directory.Exists(Path.Combine(exeDir, "reshade-shaders")))
            return true;

        // 检查host64目录（DX9 DLSS5）
        if (Directory.Exists(Path.Combine(exeDir, "host64")))
            return true;

        // 检查sl.开头的排错文件
        var slFiles = Directory.GetFiles(exeDir, "sl.*.dll");
        if (slFiles.Length > 0)
            return true;

        // 检查addon文件
        var addonFiles = Directory.GetFiles(exeDir, "*.addon64");
        if (addonFiles.Length > 0)
            return true;

        return false;
    }

    /// <summary>
    /// 检测游戏本身是否有原生DLSS代码（游戏目录中有nvngx_dlss.dll或nvngx_dlssnr.dll）
    /// 有原生DLSS的游戏应该用普通DLSS5功能，不应该用DX9 DLSS5
    /// </summary>
    public static bool HasNativeDLSS(GameInfo game)
    {
        var exeDir = game.GameExeDirectory;
        if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
            return false;

        // 游戏原生DLSS的标志文件
        string[] nativeDlssFiles = {
            "nvngx_dlss.dll",       // DLSS超分运行时（游戏原生）
            "nvngx_dlssnr.dll",     // DLSS5神经渲染（游戏原生）
            "nvngx_dlssd.dll",      // DLSS光线追踪降噪（游戏原生）
        };

        foreach (var file in nativeDlssFiles)
        {
            if (File.Exists(Path.Combine(exeDir, file)))
                return true;
        }

        // 检查子目录（最多两层），有些游戏把DLSS文件放在子目录
        try
        {
            var subDirs = Directory.GetDirectories(exeDir);
            foreach (var subDir in subDirs)
            {
                foreach (var file in nativeDlssFiles)
                {
                    if (File.Exists(Path.Combine(subDir, file)))
                        return true;
                }
                // 检查第二层子目录
                var subSubDirs = Directory.GetDirectories(subDir);
                foreach (var subSubDir in subSubDirs)
                {
                    foreach (var file in nativeDlssFiles)
                    {
                        if (File.Exists(Path.Combine(subSubDir, file)))
                            return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// 无责还原：强制删除游戏目录中的补丁文件（不恢复原文件，有风险）
    /// 返回删除的文件列表
    /// </summary>
    public static List<string> ForceRemovePatchFiles(GameInfo game)
    {
        var removedFiles = new List<string>();
        var exeDir = game.GameExeDirectory;
        if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
            return removedFiles;

        // 要删除的补丁特征文件列表
        string[] patchFiles = {
            "nvngx_dlssg.dll", "nvngx_dlssnr.dll", "nvngx_dlss.dll", "nvngx_dlssd.dll",
            "RTX40MFG.asi", "RTX40MFG_config.json", "RTX40MFG-UI.addon64", "RTX40MFGCore.dll",
            "version.dll", "version.ini", "global.ini",
            "dxgi.dll", "d3d9.dll", "d3d11.dll", "dinput8.dll",
            "ReShade.ini", "ReShade.log", "ReShadePreset.ini",
            "dgVoodoo.conf", "dgVoodooCpl.exe", "dgVoodoo2.dll",
            "dlss5-feed.addon32", "dlss5-feed-host64.exe", "DLSS5_Feed.fx",
            "renodx-dlss5.addon64", "renodx-mfgunlock.addon64",
            "LICENSE", "MINHOOK-LICENSE.txt", "README.md", "SHA256SUMS.txt",
        };

        // 删除根目录的补丁文件
        foreach (var file in patchFiles)
        {
            var filePath = Path.Combine(exeDir, file);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    removedFiles.Add(file);
                }
                catch { }
            }
        }

        // 删除sl.开头的排错文件
        var slFiles = Directory.GetFiles(exeDir, "sl.*.dll");
        foreach (var slFile in slFiles)
        {
            try
            {
                File.Delete(slFile);
                removedFiles.Add(Path.GetFileName(slFile));
            }
            catch { }
        }

        // 删除addon文件
        var addonFiles = Directory.GetFiles(exeDir, "*.addon64");
        foreach (var addonFile in addonFiles)
        {
            try
            {
                File.Delete(addonFile);
                removedFiles.Add(Path.GetFileName(addonFile));
            }
            catch { }
        }

        // 删除reshade-shaders目录
        var reshadeDir = Path.Combine(exeDir, "reshade-shaders");
        if (Directory.Exists(reshadeDir))
        {
            try
            {
                var shaderFiles = Directory.GetFiles(reshadeDir, "*", SearchOption.AllDirectories);
                foreach (var f in shaderFiles)
                {
                    removedFiles.Add("reshade-shaders\\" + f.Substring(reshadeDir.Length + 1));
                }
                Directory.Delete(reshadeDir, true);
            }
            catch { }
        }

        // 删除host64目录（DX9 DLSS5）
        var host64Dir = Path.Combine(exeDir, "host64");
        if (Directory.Exists(host64Dir))
        {
            try
            {
                var hostFiles = Directory.GetFiles(host64Dir, "*", SearchOption.AllDirectories);
                foreach (var f in hostFiles)
                {
                    removedFiles.Add("host64\\" + f.Substring(host64Dir.Length + 1));
                }
                Directory.Delete(host64Dir, true);
            }
            catch { }
        }

        // 删除plugins目录（2077专用）
        var pluginsDir = Path.Combine(exeDir, "plugins");
        if (Directory.Exists(pluginsDir))
        {
            try
            {
                var pluginFiles = Directory.GetFiles(pluginsDir, "*", SearchOption.AllDirectories);
                foreach (var f in pluginFiles)
                {
                    removedFiles.Add("plugins\\" + f.Substring(pluginsDir.Length + 1));
                }
                Directory.Delete(pluginsDir, true);
            }
            catch { }
        }

        // 重置游戏状态
        game.IsPatched = false;
        game.IsAdvancedPatched = false;
        game.IsCyberpunkPatched = false;
        game.IsDLSS5Patched = false;
        game.IsDX9DLSS5Patched = false;
        game.DLSS5GpuType = "";

        return removedFiles;
    }

    #region 排错修复功能

    /// <summary>排错补丁文件列表（sl.开头的dll）</summary>
    private static readonly string[] TroubleshootFiles = {
        "sl.common.dll", "sl.deepdvc.dll", "sl.dlss.dll", "sl.dlss_d.dll",
        "sl.dlss_g.dll", "sl.dlss_nr.dll", "sl.interposer.dll", "sl.nis.dll",
        "sl.pcl.dll", "sl.reflex.dll"
    };

    /// <summary>排错补丁目录（延迟解析）</summary>
    private string? _troubleshootDir;

    /// <summary>
    /// 解析排错补丁目录：优先外部 Patches\Troubleshoot，不完整则从嵌入资源提取
    /// </summary>
    private string GetTroubleshootDirectory()
    {
        if (!string.IsNullOrEmpty(_troubleshootDir) && Directory.Exists(_troubleshootDir))
            return _troubleshootDir;

        var externalDir = Path.Combine(AppContext.BaseDirectory, "Patches", "Troubleshoot");

        // 检查外部文件夹是否有至少一个排错补丁
        var hasExternal = Directory.Exists(externalDir) &&
                          TroubleshootFiles.Any(f => File.Exists(Path.Combine(externalDir, f)));
        if (hasExternal)
        {
            _troubleshootDir = externalDir;
            return _troubleshootDir;
        }

        // 从嵌入资源提取到临时目录
        var tempDir = Path.Combine(Path.GetTempPath(), "DLSSFrameGenTroubleshoot_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(tempDir);
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.Troubleshoot.";
            var extracted = 0;

            foreach (var fileName in TroubleshootFiles)
            {
                var resourceName = resourcePrefix + fileName;
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var outputPath = Path.Combine(tempDir, fileName);
                    using var fileStream = File.Create(outputPath);
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            if (extracted > 0)
            {
                _troubleshootDir = tempDir;
                return _troubleshootDir;
            }

            try { Directory.Delete(tempDir, true); } catch { }
        }
        catch
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }

        _troubleshootDir = externalDir;
        return _troubleshootDir;
    }

    /// <summary>
    /// 扫描游戏目录中已存在的 sl.*.dll 文件
    /// </summary>
    public List<string> ScanExistingSlDlls(string gameRootPath)
    {
        var found = new List<string>();
        if (!Directory.Exists(gameRootPath)) return found;

        // 使用元组跟踪每个目录的真正深度，修复之前depth变量是处理目录数量而非目录深度的bug
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((gameRootPath, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > 20) continue;  // 真正的目录深度限制，20层足够覆盖任何游戏目录结构

            try
            {
                foreach (var file in Directory.GetFiles(current, "sl.*.dll"))
                {
                    var fileName = Path.GetFileName(file);
                    if (TroubleshootFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)
                        || AdvancedTroubleshootFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    {
                        found.Add(file);
                    }
                }
                foreach (var dir in Directory.GetDirectories(current))
                {
                    var dirName = Path.GetFileName(dir).ToLowerInvariant();
                    if (dirName is not ("$recycle.bin" or "system volume information"))
                        queue.Enqueue((dir, depth + 1));
                }
            }
            catch { }
        }

        return found;
    }

    /// <summary>
    /// 排错修复：替换游戏目录中已存在的 sl.*.dll 文件（仅替换已有的，不新增）
    /// </summary>
    public TroubleshootResult ApplyTroubleshoot(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new TroubleshootResult { GameName = game.Name };

        try
        {
            progress?.Report("正在加载排错补丁...");
            var patchDir = GetTroubleshootDirectory();

            progress?.Report("正在扫描游戏目录中的 sl.*.dll 文件...");
            var existingFiles = ScanExistingSlDlls(game.InstallPath);

            if (existingFiles.Count == 0)
            {
                result.Success = true;
                result.Message = $"在《{game.Name}》的游戏目录中未找到任何 sl.*.dll 文件，无需替换。\n\n（排错修复仅替换游戏中已存在的同名文件，不会新增文件）";
                result.ReplacedFiles = new List<string>();
                return result;
            }

            // 备份目录
            var backupDir = Path.Combine(GetBackupDirectory(game), "Troubleshoot");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            var replaced = new List<string>();
            var skipped = new List<string>();

            foreach (var gameFilePath in existingFiles)
            {
                var fileName = Path.GetFileName(gameFilePath);
                var patchFilePath = Path.Combine(patchDir, fileName);

                if (!File.Exists(patchFilePath))
                {
                    skipped.Add(fileName);
                    continue;
                }

                try
                {
                    progress?.Report($"正在替换 {fileName}...");

                    // 备份原文件
                    var backupPath = Path.Combine(backupDir, fileName);
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(gameFilePath, backupPath, true);
                    }

                    // 替换
                    File.Copy(patchFilePath, gameFilePath, true);
                    replaced.Add(fileName);
                }
                catch (Exception ex)
                {
                    skipped.Add($"{fileName}（失败：{ex.Message}）");
                }
            }

            result.ReplacedFiles = replaced;
            result.SkippedFiles = skipped;

            if (replaced.Count > 0)
            {
                result.Success = true;
                result.Message = $"排错修复完成！\n\n" +
                               $"游戏：《{game.Name}》\n" +
                               $"已替换 {replaced.Count} 个文件：\n" +
                               string.Join("\n", replaced.Select(f => "  • " + f)) +
                               (skipped.Count > 0 ? $"\n\n跳过 {skipped.Count} 个：\n" + string.Join("\n", skipped.Select(f => "  • " + f)) : "") +
                               "\n\n原文件已备份，可通过「一键还原」恢复。\n重启游戏后生效。";
            }
            else
            {
                result.Success = false;
                result.Message = $"未能替换任何文件。\n\n找到 {existingFiles.Count} 个 sl.*.dll 文件，但补丁文件均缺失或替换失败。";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。";
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"排错修复时发生错误：{ex.Message}";
        }

        return result;
    }

    #endregion

    #region 高级模式功能

    /// <summary>高级模式 reshade 文件列表</summary>
    private static readonly string[] AdvancedReshadeFiles = {
        "dxgi.dll", "ReShade.ini", "ReShade.log", "ReShadePreset.ini"
    };

    /// <summary>高级模式 unlock 文件列表</summary>
    private static readonly string[] AdvancedUnlockFiles = {
        "global.ini", "LICENSE", "MINHOOK-LICENSE.txt", "README.md",
        "RTX40MFG-UI.addon64", "RTX40MFG.asi", "RTX40MFGCore.dll", "SHA256SUMS.txt"
    };

    /// <summary>高级模式补丁目录（延迟解析）</summary>
    private string? _advancedDir;

    /// <summary>
    /// 解析高级模式补丁目录：优先外部 Patches\Advanced，不完整则从嵌入资源提取
    /// </summary>
    private string GetAdvancedDirectory()
    {
        if (!string.IsNullOrEmpty(_advancedDir) && Directory.Exists(_advancedDir))
            return _advancedDir;

        var externalDir = Path.Combine(AppContext.BaseDirectory, "Patches", "Advanced");

        // 检查外部文件夹是否有至少一个高级模式补丁
        var hasExternal = Directory.Exists(externalDir) &&
                          File.Exists(Path.Combine(externalDir, "nvngx_dlssg.dll")) &&
                          File.Exists(Path.Combine(externalDir, "version.dll"));
        if (hasExternal)
        {
            _advancedDir = externalDir;
            return _advancedDir;
        }

        // 从嵌入资源提取到临时目录
        var tempDir = Path.Combine(Path.GetTempPath(), "DLSSFrameGenAdvanced_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.Combine(tempDir, "reshade"));
            Directory.CreateDirectory(Path.Combine(tempDir, "unlock"));
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.Advanced.";
            var extracted = 0;

            // 提取根目录文件
            foreach (var fileName in new[] { "nvngx_dlssg.dll", "version.dll" })
            {
                var resourceName = resourcePrefix + fileName;
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(Path.Combine(tempDir, fileName));
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            // 提取 reshade 文件（dxgi.dll从Shared目录复制，避免重复嵌入）
            foreach (var fileName in AdvancedReshadeFiles)
            {
                if (fileName == "dxgi.dll") continue; // dxgi.dll从Shared目录复制
                var resourceName = resourcePrefix + "reshade." + fileName;
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(Path.Combine(tempDir, "reshade", fileName));
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            // 从Shared目录复制dxgi.dll到reshade目录（避免重复嵌入，减小体积）
            try
            {
                var sharedDir = GetSharedDirectory();
                var sharedDxgi = Path.Combine(sharedDir, "dxgi.dll");
                if (File.Exists(sharedDxgi))
                {
                    File.Copy(sharedDxgi, Path.Combine(tempDir, "reshade", "dxgi.dll"), true);
                    extracted++;
                }
            }
            catch { }

            // 提取 unlock 文件
            foreach (var fileName in AdvancedUnlockFiles)
            {
                var resourceName = resourcePrefix + "unlock." + fileName;
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(Path.Combine(tempDir, "unlock", fileName));
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            if (extracted > 0)
            {
                _advancedDir = tempDir;
                return _advancedDir;
            }

            try { Directory.Delete(tempDir, true); } catch { }
        }
        catch
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }

        _advancedDir = externalDir;
        return _advancedDir;
    }

    /// <summary>
    /// 高级模式打补丁：替换 nvngx_dlssg.dll + 复制 reshade + version.dll + unlock 文件 + 重命名 global.ini 为 version.ini
    /// </summary>
    public PatchResult ApplyAdvancedPatch(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new PatchResult { GameName = game.Name };

        try
        {
            if (string.IsNullOrEmpty(game.DlssgDllPath) || !File.Exists(game.DlssgDllPath))
            {
                result.Success = false;
                result.Message = $"未找到 nvngx_dlssg.dll 文件，无法打补丁。";
                return result;
            }

            if (string.IsNullOrEmpty(game.GameExeDirectory) || !Directory.Exists(game.GameExeDirectory))
            {
                result.Success = false;
                result.Message = $"未找到游戏 exe 目录（Binaries\\Win64），无法打补丁。";
                return result;
            }

            progress?.Report("正在加载高级模式补丁...");
            var patchDir = GetAdvancedDirectory();
            var exeDir = game.GameExeDirectory;

            // 备份目录（高级模式单独备份）
            var backupDir = Path.Combine(GetBackupDirectory(game), "Advanced");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            // 记录游戏目录文件快照
            SnapshotGameFiles(exeDir, backupDir);

            var installedFiles = new List<string>();

            // 步骤1：替换 nvngx_dlssg.dll
            progress?.Report("正在替换 nvngx_dlssg.dll...");
            var newDlssgPath = Path.Combine(patchDir, "nvngx_dlssg.dll");
            if (!File.Exists(newDlssgPath))
            {
                result.Success = false;
                result.Message = $"高级模式补丁缺失：nvngx_dlssg.dll";
                return result;
            }
            // 备份原文件
            var dlssgBackup = Path.Combine(backupDir, "nvngx_dlssg.dll");
            if (!File.Exists(dlssgBackup))
                File.Copy(game.DlssgDllPath, dlssgBackup, true);
            // 替换
            File.Copy(newDlssgPath, game.DlssgDllPath, true);
            installedFiles.Add("nvngx_dlssg.dll");

            // 步骤2：复制 version.dll（高级模式的新文件）
            progress?.Report("正在复制 version.dll...");
            var newVersionDll = Path.Combine(patchDir, "version.dll");
            if (File.Exists(newVersionDll))
            {
                var targetVersionDll = Path.Combine(exeDir, "version.dll");
                // 备份已存在的 version.dll
                if (File.Exists(targetVersionDll))
                {
                    var versionBackup = Path.Combine(backupDir, "version.dll");
                    if (!File.Exists(versionBackup))
                        File.Copy(targetVersionDll, versionBackup, true);
                }
                File.Copy(newVersionDll, targetVersionDll, true);
                installedFiles.Add("version.dll");
            }

            // 步骤3：复制 reshade 的4个文件到游戏exe目录
            progress?.Report("正在复制 ReShade 文件...");
            var reshadeDir = Path.Combine(patchDir, "reshade");
            if (Directory.Exists(reshadeDir))
            {
                foreach (var fileName in AdvancedReshadeFiles)
                {
                    var srcFile = Path.Combine(reshadeDir, fileName);
                    if (File.Exists(srcFile))
                    {
                        var dstFile = Path.Combine(exeDir, fileName);
                        // 备份已存在的文件
                        if (File.Exists(dstFile))
                        {
                            var fileBackup = Path.Combine(backupDir, fileName);
                            if (!File.Exists(fileBackup))
                                File.Copy(dstFile, fileBackup, true);
                        }
                        File.Copy(srcFile, dstFile, true);
                        installedFiles.Add(fileName);
                    }
                }

                // 复制 Fonts 目录（中文字体，从Shared共享目录复制，避免重复嵌入减小体积）
                var sharedFontsDir = Path.Combine(GetSharedDirectory(), "Fonts");
                if (Directory.Exists(sharedFontsDir))
                {
                    var fontsDstDir = Path.Combine(exeDir, "Fonts");
                    if (!Directory.Exists(fontsDstDir))
                        Directory.CreateDirectory(fontsDstDir);

                    foreach (var fontFile in Directory.GetFiles(sharedFontsDir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = fontFile.Substring(sharedFontsDir.Length + 1);
                        var dstFontFile = Path.Combine(fontsDstDir, relativePath);
                        var dstFontDir = Path.GetDirectoryName(dstFontFile);
                        if (!string.IsNullOrEmpty(dstFontDir) && !Directory.Exists(dstFontDir))
                            Directory.CreateDirectory(dstFontDir);

                        File.Copy(fontFile, dstFontFile, true);
                        installedFiles.Add($"Fonts/{relativePath.Replace("\\", "/")}");
                    }
                }
            }

            // 步骤4：复制 unlock 文件夹里的所有文件到游戏exe目录
            progress?.Report("正在复制 RTX40MFG Unlock 文件...");
            var unlockDir = Path.Combine(patchDir, "unlock");
            if (Directory.Exists(unlockDir))
            {
                foreach (var fileName in AdvancedUnlockFiles)
                {
                    var srcFile = Path.Combine(unlockDir, fileName);
                    if (File.Exists(srcFile))
                    {
                        var dstFile = Path.Combine(exeDir, fileName);
                        // 备份已存在的文件
                        if (File.Exists(dstFile))
                        {
                            var fileBackup = Path.Combine(backupDir, fileName);
                            if (!File.Exists(fileBackup))
                                File.Copy(dstFile, fileBackup, true);
                        }
                        File.Copy(srcFile, dstFile, true);
                        installedFiles.Add(fileName);
                    }
                }
            }

            // 步骤5：把 global.ini 改名为 version.ini（和 version.dll 同名，只改名字不改后缀）
            progress?.Report("正在重命名 global.ini 为 version.ini...");
            var globalIniPath = Path.Combine(exeDir, "global.ini");
            var versionIniPath = Path.Combine(exeDir, "version.ini");
            if (File.Exists(globalIniPath))
            {
                // 如果 version.ini 已存在，先备份
                if (File.Exists(versionIniPath))
                {
                    var versionIniBackup = Path.Combine(backupDir, "version.ini");
                    if (!File.Exists(versionIniBackup))
                        File.Copy(versionIniPath, versionIniBackup, true);
                }
                // 重命名：先删除已存在的 version.ini，再把 global.ini 改名
                if (File.Exists(versionIniPath))
                    File.Delete(versionIniPath);
                File.Move(globalIniPath, versionIniPath);
                installedFiles.Add("version.ini（由 global.ini 重命名）");
            }

            // 记录已安装的文件列表（用于还原）
            var manifestPath = Path.Combine(backupDir, "installed_files.txt");
            File.WriteAllLines(manifestPath, installedFiles);

            result.Success = true;
            result.Message = $"高级模式开启成功！\n\n" +
                           $"游戏：《{game.Name}》\n" +
                           $"已安装 {installedFiles.Count} 个文件：\n" +
                           string.Join("\n", installedFiles.Select(f => "  • " + f)) +
                           "\n\n原文件已备份，可通过「一键还原」恢复。\n" +
                           "启动游戏后，按 Home 键可打开 ReShade 配置界面。";
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。";
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"高级模式打补丁时发生错误：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 检查高级模式是否已安装
    /// </summary>
    public static bool CheckAdvancedPatched(GameInfo game)
    {
        if (string.IsNullOrEmpty(game.GameExeDirectory)) return false;
        // 高级模式的特征：version.ini 存在（由 global.ini 重命名而来）+ RTX40MFGCore.dll 存在
        var versionIni = Path.Combine(game.GameExeDirectory, "version.ini");
        var mfgCore = Path.Combine(game.GameExeDirectory, "RTX40MFGCore.dll");
        return File.Exists(versionIni) && File.Exists(mfgCore);
    }

    #endregion

    #region 高级模式排错功能

    /// <summary>高级模式排错补丁文件列表（sl.开头）</summary>
    private static readonly string[] AdvancedTroubleshootFiles = {
        "sl.common.dll", "sl.deepdvc.dll", "sl.dlss.dll", "sl.dlss_d.dll",
        "sl.dlss_g.dll", "sl.dlss_nr.dll", "sl.interposer.dll", "sl.nis.dll",
        "sl.nvperf.dll", "sl.pcl.dll", "sl.reflex.dll"
    };

    /// <summary>高级模式排错补丁目录（延迟解析）</summary>
    private string? _advancedTroubleshootDir;

    /// <summary>
    /// 解析高级模式排错补丁目录：优先外部 Patches\TroubleshootAdvanced，不完整则从嵌入资源提取
    /// </summary>
    private string GetAdvancedTroubleshootDirectory()
    {
        if (!string.IsNullOrEmpty(_advancedTroubleshootDir) && Directory.Exists(_advancedTroubleshootDir))
            return _advancedTroubleshootDir;

        var externalDir = Path.Combine(AppContext.BaseDirectory, "Patches", "TroubleshootAdvanced");

        var hasExternal = Directory.Exists(externalDir) &&
                          AdvancedTroubleshootFiles.Any(f => File.Exists(Path.Combine(externalDir, f)));
        if (hasExternal)
        {
            _advancedTroubleshootDir = externalDir;
            return _advancedTroubleshootDir;
        }

        // 从嵌入资源提取
        var tempDir = Path.Combine(Path.GetTempPath(), "DLSSFrameGenAdvTS_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(tempDir);
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.TroubleshootAdvanced.";
            var extracted = 0;

            foreach (var fileName in AdvancedTroubleshootFiles)
            {
                var resourceName = resourcePrefix + fileName;
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(Path.Combine(tempDir, fileName));
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            if (extracted > 0)
            {
                _advancedTroubleshootDir = tempDir;
                return _advancedTroubleshootDir;
            }

            try { Directory.Delete(tempDir, true); } catch { }
        }
        catch
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }

        _advancedTroubleshootDir = externalDir;
        return _advancedTroubleshootDir;
    }

    /// <summary>
    /// 高级模式排错修复：替换游戏目录中已存在的 sl.*.dll 文件（仅替换已有的，不新增）
    /// 使用高级模式专用的排错补丁
    /// </summary>
    public TroubleshootResult ApplyAdvancedTroubleshoot(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new TroubleshootResult { GameName = game.Name };

        try
        {
            progress?.Report("正在加载高级模式排错补丁...");
            var patchDir = GetAdvancedTroubleshootDirectory();

            progress?.Report("正在扫描游戏目录中的 sl.*.dll 文件...");
            var existingFiles = ScanExistingSlDlls(game.InstallPath);

            if (existingFiles.Count == 0)
            {
                result.Success = true;
                result.Message = $"在《{game.Name}》的游戏目录中未找到任何 sl.*.dll 文件，无需替换。\n\n（排错修复仅替换游戏中已存在的同名文件，不会新增文件）";
                result.ReplacedFiles = new List<string>();
                return result;
            }

            var backupDir = Path.Combine(GetBackupDirectory(game), "AdvancedTroubleshoot");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            var replaced = new List<string>();
            var skipped = new List<string>();

            foreach (var gameFilePath in existingFiles)
            {
                var fileName = Path.GetFileName(gameFilePath);
                var patchFilePath = Path.Combine(patchDir, fileName);

                if (!File.Exists(patchFilePath))
                {
                    skipped.Add(fileName);
                    continue;
                }

                try
                {
                    progress?.Report($"正在替换 {fileName}...");

                    var backupPath = Path.Combine(backupDir, fileName);
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(gameFilePath, backupPath, true);
                    }

                    File.Copy(patchFilePath, gameFilePath, true);
                    replaced.Add(fileName);
                }
                catch (Exception ex)
                {
                    skipped.Add($"{fileName}（失败：{ex.Message}）");
                }
            }

            result.ReplacedFiles = replaced;
            result.SkippedFiles = skipped;

            if (replaced.Count > 0)
            {
                result.Success = true;
                result.Message = $"高级模式排错修复完成！\n\n" +
                               $"游戏：《{game.Name}》\n" +
                               $"已替换 {replaced.Count} 个文件：\n" +
                               string.Join("\n", replaced.Select(f => "  • " + f)) +
                               (skipped.Count > 0 ? $"\n\n跳过 {skipped.Count} 个：\n" + string.Join("\n", skipped.Select(f => "  • " + f)) : "") +
                               "\n\n原文件已备份，可通过「一键还原」恢复。\n重启游戏后生效。";
            }
            else
            {
                result.Success = false;
                result.Message = $"未能替换任何文件。\n\n找到 {existingFiles.Count} 个 sl.*.dll 文件，但补丁文件均缺失或替换失败。";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。";
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"高级模式排错修复时发生错误：{ex.Message}";
        }

        return result;
    }

    #endregion

    #region 赛博朋克2077专用功能

    /// <summary>2077专用补丁文件列表</summary>
    private static readonly string[] Cyberpunk2077Files = {
        "global.ini", "LICENSE", "nvngx_dlss.dll", "nvngx_dlssd.dll",
        "nvngx_dlssg.dll", "sl.common.dll", "sl.dlss.dll", "sl.dlss_d.dll",
        "sl.dlss_g.dll", "sl.interposer.dll", "sl.nis.dll", "sl.pcl.dll",
        "sl.reflex.dll", "version.dll"
    };

    /// <summary>2077专用补丁目录（延迟解析）</summary>
    private string? _cyberpunkDir;

    /// <summary>
    /// 解析2077专用补丁目录
    /// </summary>
    private string GetCyberpunk2077Directory()
    {
        if (!string.IsNullOrEmpty(_cyberpunkDir) && Directory.Exists(_cyberpunkDir))
            return _cyberpunkDir;

        var externalDir = Path.Combine(AppContext.BaseDirectory, "Patches", "Cyberpunk2077");

        var hasExternal = Directory.Exists(externalDir) &&
                          File.Exists(Path.Combine(externalDir, "nvngx_dlssg.dll")) &&
                          File.Exists(Path.Combine(externalDir, "version.dll"));
        if (hasExternal)
        {
            _cyberpunkDir = externalDir;
            return _cyberpunkDir;
        }

        // 从嵌入资源提取（包含 plugins 子目录的所有文件）
        var tempDir = Path.Combine(Path.GetTempPath(), "DLSSFrameGenCyber2077_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(tempDir);
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.Cyberpunk2077.";
            var extracted = 0;

            // 遍历所有 Cyberpunk2077 相关的嵌入资源
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix)) continue;

                // 去掉前缀，得到相对路径（点号分隔）
                var relativePath = resourceName.Substring(resourcePrefix.Length);

                string outputPath;
                if (relativePath.StartsWith("plugins.", StringComparison.OrdinalIgnoreCase))
                {
                    // plugins 子目录中的文件：需要把点号转换为目录分隔符
                    // 从右往左找最后一个点号，作为文件名和扩展名的分隔
                    var lastDot = relativePath.LastIndexOf('.');
                    if (lastDot <= 0) continue;

                    var ext = relativePath.Substring(lastDot);
                    var pathPart = relativePath.Substring(0, lastDot).Replace('.', Path.DirectorySeparatorChar);
                    var fullRelativePath = pathPart + ext;
                    outputPath = Path.Combine(tempDir, fullRelativePath);
                }
                else
                {
                    // 根目录中的文件（如 sl.common.dll）：直接使用文件名，不转换点号
                    outputPath = Path.Combine(tempDir, relativePath);
                }

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(outputPath);
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            if (extracted > 0)
            {
                _cyberpunkDir = tempDir;
                return _cyberpunkDir;
            }

            try { Directory.Delete(tempDir, true); } catch { }
        }
        catch
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }

        _cyberpunkDir = externalDir;
        return _cyberpunkDir;
    }

    /// <summary>
    /// 赛博朋克2077专用打补丁：把所有文件复制到 Cyberpunk 2077\bin\x64 目录
    /// </summary>
    public PatchResult ApplyCyberpunk2077Patch(GameInfo game, IProgress<string>? progress = null)
    {
        var result = new PatchResult { GameName = game.Name };

        try
        {
            // 2077的exe目录是 bin\x64，不是 Binaries\Win64
            // 先尝试从game.GameExePath获取，如果没有则手动查找
            var exeDir = game.GameExeDirectory;
            if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
            {
                // 手动查找 bin\x64 目录
                var binX64 = GameScanner.FindFileInDirectory(game.InstallPath, "Cyberpunk2077.exe");
                if (!string.IsNullOrEmpty(binX64))
                {
                    exeDir = Path.GetDirectoryName(binX64);
                }
                else
                {
                    // 尝试直接找 bin\x64
                    var potentialDir = Path.Combine(game.InstallPath, "bin", "x64");
                    if (Directory.Exists(potentialDir))
                        exeDir = potentialDir;
                }
            }

            if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
            {
                result.Success = false;
                result.Message = $"未找到赛博朋克2077的 bin\\x64 目录，无法打补丁。";
                return result;
            }

            progress?.Report("正在加载2077专用补丁...");
            var patchDir = GetCyberpunk2077Directory();

            // 备份目录
            var backupDir = Path.Combine(GetBackupDirectory(game), "Cyberpunk2077");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            // 记录游戏目录文件快照
            SnapshotGameFiles(exeDir, backupDir);

            var installedFiles = new List<string>();

            // 替换 nvngx_dlssg.dll（从Shared共享目录复制经典模式版本，避免重复嵌入减小体积）
            if (!string.IsNullOrEmpty(game.DlssgDllPath) && File.Exists(game.DlssgDllPath))
            {
                progress?.Report("正在替换 nvngx_dlssg.dll...");
                var sharedDlssg = Path.Combine(GetSharedDirectory(), "nvngx_dlssg_classic.dll");
                if (File.Exists(sharedDlssg))
                {
                    var dlssgBackup = Path.Combine(backupDir, "nvngx_dlssg.dll");
                    if (!File.Exists(dlssgBackup))
                        File.Copy(game.DlssgDllPath, dlssgBackup, true);
                    File.Copy(sharedDlssg, game.DlssgDllPath, true);
                    installedFiles.Add("nvngx_dlssg.dll");
                }
            }

            // 复制所有2077专用文件到 bin\x64
            foreach (var fileName in Cyberpunk2077Files)
            {
                progress?.Report($"正在复制 {fileName}...");
                var srcFile = Path.Combine(patchDir, fileName);
                if (!File.Exists(srcFile)) continue;

                var dstFile = Path.Combine(exeDir, fileName);

                // 备份已存在的文件
                if (File.Exists(dstFile))
                {
                    var backupPath = Path.Combine(backupDir, fileName);
                    if (!File.Exists(backupPath))
                        File.Copy(dstFile, backupPath, true);
                }

                File.Copy(srcFile, dstFile, true);
                installedFiles.Add(fileName);
            }

            // 从Troubleshoot目录复制sl.开头文件（2077专用和经典排错共用，避免重复嵌入减小体积）
            var troubleshootDir = GetTroubleshootDirectory();
            if (Directory.Exists(troubleshootDir))
            {
                foreach (var slFile in Directory.GetFiles(troubleshootDir, "sl.*.dll", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(slFile);
                    progress?.Report($"正在复制 {fileName}...");
                    var dstFile = Path.Combine(exeDir, fileName);

                    // 备份已存在的文件
                    if (File.Exists(dstFile))
                    {
                        var backupPath = Path.Combine(backupDir, fileName);
                        if (!File.Exists(backupPath))
                            File.Copy(dstFile, backupPath, true);
                    }

                    File.Copy(slFile, dstFile, true);
                    installedFiles.Add(fileName);
                }
            }

            // 复制 plugins 子目录
            var pluginsSrcDir = Path.Combine(patchDir, "plugins");
            if (Directory.Exists(pluginsSrcDir))
            {
                var pluginsDstDir = Path.Combine(exeDir, "plugins");
                if (!Directory.Exists(pluginsDstDir))
                    Directory.CreateDirectory(pluginsDstDir);

                foreach (var pluginFile in Directory.GetFiles(pluginsSrcDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = pluginFile.Substring(pluginsSrcDir.Length).TrimStart('\\');
                    var dstFile = Path.Combine(pluginsDstDir, relativePath);
                    var dstFileDir = Path.GetDirectoryName(dstFile);
                    if (!string.IsNullOrEmpty(dstFileDir) && !Directory.Exists(dstFileDir))
                        Directory.CreateDirectory(dstFileDir);

                    if (File.Exists(dstFile))
                    {
                        var backupPath = Path.Combine(backupDir, "plugins", relativePath);
                        var backupDir2 = Path.GetDirectoryName(backupPath);
                        if (!string.IsNullOrEmpty(backupDir2) && !Directory.Exists(backupDir2))
                            Directory.CreateDirectory(backupDir2);
                        if (!File.Exists(backupPath))
                            File.Copy(dstFile, backupPath, true);
                    }

                    File.Copy(pluginFile, dstFile, true);
                    installedFiles.Add($"plugins\\{relativePath}");
                }
            }

            // 从Shared目录复制共享大文件nvngx_dlss.dll（避免重复嵌入，减小体积）
            var sharedDir = GetSharedDirectory();
            var sharedDlssSrc = Path.Combine(sharedDir, "nvngx_dlss.dll");
            var sharedDlssDst = Path.Combine(exeDir, "nvngx_dlss.dll");
            if (File.Exists(sharedDlssSrc))
            {
                // 备份已存在的文件
                if (File.Exists(sharedDlssDst))
                {
                    var backupPath = Path.Combine(backupDir, "nvngx_dlss.dll");
                    if (!File.Exists(backupPath))
                        File.Copy(sharedDlssDst, backupPath, true);
                }
                File.Copy(sharedDlssSrc, sharedDlssDst, true);
                installedFiles.Add("nvngx_dlss.dll");
            }

            // 记录已安装文件
            var manifestPath = Path.Combine(backupDir, "installed_files.txt");
            File.WriteAllLines(manifestPath, installedFiles);

            result.Success = true;
            result.Message = $"赛博朋克2077专用补丁安装成功！\n\n" +
                           $"已安装 {installedFiles.Count} 个文件到：\n{exeDir}\n\n" +
                           "使用方法：\n" +
                           "1. 启动游戏，点击 Unbound，设置 CET 控制台快捷键\n" +
                           "2. 游戏设置内，开启 DLSS 帧生成\n" +
                           "3. 按 CET 控制台快捷键，左上角出现 DLSS MFG 面板\n" +
                           "4. 按住4角可以拖拽放大，自行调节生成倍率\n" +
                           "5. 重启游戏生效\n\n" +
                           "原文件已备份，可通过「一键还原」恢复。";
            game.IsCyberpunkPatched = true;
            // 设置游戏exe路径，确保状态检测正常
            if (string.IsNullOrEmpty(game.GameExePath) && Directory.Exists(exeDir))
            {
                var exePath = Path.Combine(exeDir, "Cyberpunk2077.exe");
                if (File.Exists(exePath))
                    game.GameExePath = exePath;
                else
                {
                    var exes = Directory.GetFiles(exeDir, "*.exe");
                    if (exes.Length > 0)
                        game.GameExePath = exes[0];
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。";
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"2077专用补丁安装时发生错误：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 判断是否为赛博朋克2077
    /// </summary>
    public static bool IsCyberpunk2077(GameInfo game)
    {
        var name = game.Name.ToLowerInvariant();
        return name.Contains("cyberpunk") || name.Contains("2077") || name.Contains("赛博朋克");
    }

    #endregion

    #region DLSS5 补丁功能

    /// <summary>DLSS5通用补丁目录（延迟解析）</summary>
    private string? _dlss5CommonDir;
    /// <summary>DLSS5 N卡补丁目录（延迟解析）</summary>
    private string? _dlss5NvidiaDir;
    /// <summary>DLSS5 A卡补丁目录（延迟解析）</summary>
    private string? _dlss5AmdDir;
    /// <summary>共享大文件目录（延迟解析，避免重复嵌入）</summary>
    private string? _sharedDir;

    /// <summary>
    /// 判断是否为核显（集成显卡）
    /// </summary>
    private static bool IsIntegratedGpu(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        // AMD核显关键词
        string[] amdIntegrated = {
            "Radeon Graphics", "Radeon RX Vega", "Radeon Vega",
            "Radeon 600M", "Radeon 700M", "Radeon 800M", "Radeon 900M",
            "Radeon 610M", "Radeon 660M", "Radeon 680M",
            "Radeon 740M", "Radeon 760M", "Radeon 780M",
            "Radeon 860M", "Radeon 880M",
            "Radeon R5 Graphics", "Radeon R7 Graphics", "Radeon R9 Graphics",
            "Radeon HD", "Radeon R5", "Radeon R7", "Radeon R9"
        };

        // Intel核显关键词
        string[] intelIntegrated = {
            "Intel(R) HD Graphics", "Intel HD Graphics",
            "Intel(R) UHD Graphics", "Intel UHD Graphics",
            "Intel(R) Iris(R) Plus Graphics", "Intel Iris Plus Graphics",
            "Intel(R) Iris(R) Xe Graphics", "Intel Iris Xe Graphics",
            "Intel(R) Iris(R) Graphics", "Intel Iris Graphics",
            "Intel(R) Graphics", "Intel Graphics"
        };

        foreach (var kw in amdIntegrated)
        {
            if (name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var kw in intelIntegrated)
        {
            if (name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 获取所有显卡名称，优先返回独立显卡
    /// </summary>
    private static List<string> GetAllGpuNames()
    {
        var gpus = new List<string>();
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(name))
                    gpus.Add(name);
            }
        }
        catch { }

        // 优先排序：独立显卡在前，核显在后
        return gpus.OrderBy(g => IsIntegratedGpu(g) ? 1 : 0).ToList();
    }

    /// <summary>
    /// 检测显卡类型：Nvidia / AMD / Unknown
    /// 优先检测独立显卡，忽略核显
    /// </summary>
    public static string DetectGpuType()
    {
        var gpus = GetAllGpuNames();

        // 第一轮：只检测独立显卡
        foreach (var name in gpus)
        {
            if (IsIntegratedGpu(name)) continue;

            if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                return "NVIDIA";
            if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ATI", StringComparison.OrdinalIgnoreCase))
                return "AMD";
        }

        // 第二轮：如果没有独立显卡，再检测核显（兜底）
        foreach (var name in gpus)
        {
            if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                return "NVIDIA";
            if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ATI", StringComparison.OrdinalIgnoreCase))
                return "AMD";
        }

        return "Unknown";
    }

    /// <summary>
    /// 检测NVIDIA显卡系列：RTX20 / RTX30 / RTX40 / RTX50 / Unknown
    /// 优先检测独立显卡
    /// </summary>
    public static string DetectNvidiaSeries()
    {
        var gpus = GetAllGpuNames();

        // 只检测独立显卡
        foreach (var name in gpus)
        {
            if (IsIntegratedGpu(name)) continue;
            if (!name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("GeForce", StringComparison.OrdinalIgnoreCase))
                continue;

            // RTX 50系：RTX 50xx
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"RTX\s*5\d{2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return "RTX50";
            // RTX 40系：RTX 40xx
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"RTX\s*4\d{2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return "RTX40";
            // RTX 30系：RTX 30xx
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"RTX\s*3\d{2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return "RTX30";
            // RTX 20系：RTX 20xx
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"RTX\s*2\d{2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return "RTX20";
        }

        return "Unknown";
    }

    /// <summary>
    /// 检测AMD显卡是否支持DLSS5（目前支持7000系和9000系）
    /// 优先检测独立显卡，只要有一个独立显卡是7000/9000系就返回true
    /// </summary>
    public static bool IsAmd9000Series()
    {
        var gpus = GetAllGpuNames();

        // 第一轮：只检测独立显卡
        foreach (var name in gpus)
        {
            if (IsIntegratedGpu(name)) continue;

            if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
            {
                // 9000系显卡：RX 9000系列
                if (name.Contains("RX 9", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("9000", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("9060", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("9070", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("9080", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("9090", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                // 7000系显卡：RX 7000系列
                if (name.Contains("RX 7", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("7000", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("7600", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("7700", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("7800", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("7900", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // 第二轮：如果没有独立AMD显卡，再检测核显（兜底，但核显通常不支持）
        foreach (var name in gpus)
        {
            if (!IsIntegratedGpu(name)) continue;

            if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
            {
                // 9000系显卡
                if (name.Contains("RX 9", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("9000", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                // 7000系显卡
                if (name.Contains("RX 7", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("7000", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #region 通用快照/备份/还原辅助方法

    /// <summary>
    /// 记录游戏目录文件快照（安装前调用）
    /// 保存所有文件的相对路径到 snapshot.txt，用于还原时对比
    /// </summary>
    public static void SnapshotGameFiles(string exeDir, string backupDir)
    {
        try
        {
            Directory.CreateDirectory(backupDir);
            var snapshotFile = Path.Combine(backupDir, "snapshot.txt");
            var files = Directory.GetFiles(exeDir, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(backupDir, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Substring(exeDir.Length + 1).Replace("\\", "/"))
                .ToList();
            File.WriteAllLines(snapshotFile, files);
        }
        catch { }
    }

    /// <summary>
    /// 递归删除空目录
    /// </summary>
    private static void DeleteEmptyDirectories(string rootDir)
    {
        try
        {
            if (!Directory.Exists(rootDir)) return;

            foreach (var dir in Directory.GetDirectories(rootDir))
            {
                DeleteEmptyDirectories(dir);
                if (Directory.GetFiles(dir).Length == 0 &&
                    Directory.GetDirectories(dir).Length == 0)
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 从快照还原：恢复备份的文件，删除安装时新增的文件
    /// 确保不会误删游戏本身的文件
    /// </summary>
    public static (int restored, int deleted) RestoreFromSnapshot(string exeDir, string backupDir)
    {
        var restored = 0;
        var deleted = 0;
        try
        {
            if (!Directory.Exists(backupDir))
                return (0, 0);

            var snapshotFile = Path.Combine(backupDir, "snapshot.txt");
            var originalFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 读取快照
            if (File.Exists(snapshotFile))
            {
                foreach (var line in File.ReadAllLines(snapshotFile))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        originalFiles.Add(line.Trim());
                }
            }

            // 删除不在快照中的新增文件（排除备份目录本身）
            if (originalFiles.Count > 0)
            {
                var allFiles = Directory.GetFiles(exeDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.StartsWith(backupDir, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var file in allFiles)
                {
                    var relPath = file.Substring(exeDir.Length + 1).Replace("\\", "/");
                    if (!originalFiles.Contains(relPath))
                    {
                        try
                        {
                            File.Delete(file);
                            deleted++;
                        }
                        catch { }
                    }
                }

                // 删除空目录（排除备份目录）
                var dirs = Directory.GetDirectories(exeDir, "*", SearchOption.AllDirectories)
                    .Where(d => !d.StartsWith(backupDir, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(d => d.Length)
                    .ToList();
                foreach (var dir in dirs)
                {
                    try
                    {
                        if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Length == 0)
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
        return (restored, deleted);
    }

    #endregion

    /// <summary>
    /// 检测游戏是否为生化危机系列，并返回版本（RE2/RE3/RE4/RE7/RE8/Unknown）
    /// </summary>
    public static string DetectResidentEvilVersion(GameInfo game)
    {
        var name = game.Name ?? "";
        var path = game.InstallPath ?? "";
        var combined = name + " " + path;

        // 生化危机2重置版
        if (combined.Contains("resident evil 2", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("biohazard 2", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("re2", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("生化危机2") || name.Contains("生化2"))
            return "RE2";

        // 生化危机3重置版
        if (combined.Contains("resident evil 3", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("biohazard 3", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("re3", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("生化危机3") || name.Contains("生化3"))
            return "RE3";

        // 生化危机4重置版
        if (combined.Contains("resident evil 4", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("biohazard 4", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("re4", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("生化危机4") || name.Contains("生化4"))
            return "RE4";

        // 生化危机7
        if (combined.Contains("resident evil 7", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("biohazard 7", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("re7", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("生化危机7") || name.Contains("生化7"))
            return "RE7";

        // 生化危机8（村庄）
        if (combined.Contains("resident evil 8", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("biohazard 8", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("re8", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("village", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("生化危机8") || name.Contains("生化8") || name.Contains("村庄"))
            return "RE8";

        // 生化危机9（安魂曲）
        if (combined.Contains("resident evil 9", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("biohazard 9", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("re9", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("requiem", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("生化危机9") || name.Contains("生化9") || name.Contains("安魂曲"))
            return "RE9";

        return "Unknown";
    }

    /// <summary>
    /// 检测是否为RE引擎游戏
    /// 检测方式：1. 游戏名称关键词匹配；2. 检测 re_chunk_000.pak 文件（RE引擎标志性文件，通常体积很大）
    /// </summary>
    public static bool DetectREEngineGame(GameInfo game)
    {
        var name = game.Name ?? "";
        var path = game.InstallPath ?? "";
        var combined = name + " " + path;

        // 方式一：通过 re_chunk_000.pak 文件检测（RE引擎标志性文件）
        // RE引擎游戏通常在游戏exe所在目录有 re_chunk_000.pak 文件，且体积通常很大（几百MB以上）
        try
        {
            // 优先检查游戏exe所在目录
            string? exeDir = null;
            if (!string.IsNullOrEmpty(game.GameExePath) && File.Exists(game.GameExePath))
            {
                exeDir = Path.GetDirectoryName(game.GameExePath);
            }

            // 检查的目录列表：游戏exe目录、游戏根目录
            var dirsToCheck = new List<string>();
            if (!string.IsNullOrEmpty(exeDir))
                dirsToCheck.Add(exeDir);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                dirsToCheck.Add(path);

            foreach (var dir in dirsToCheck)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                var pakFile = Path.Combine(dir, "re_chunk_000.pak");
                if (File.Exists(pakFile))
                {
                    // 检查文件大小，RE引擎的 re_chunk_000.pak 通常很大（大于50MB）
                    var fileInfo = new FileInfo(pakFile);
                    if (fileInfo.Length > 50 * 1024 * 1024) // 大于50MB
                    {
                        return true;
                    }
                }
            }
        }
        catch { }

        // 方式二：游戏名称关键词匹配
        // 生化危机系列（RE引擎）
        if (DetectResidentEvilVersion(game) != "Unknown")
            return true;

        // 鬼泣5（RE引擎）
        if (combined.Contains("devil may cry 5", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("dmc5", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("鬼泣5") || name.Contains("鬼泣 5"))
            return true;

        // 怪物猎人：世界（RE引擎）
        if (combined.Contains("monster hunter world", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("mhw", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("怪物猎人世界") || name.Contains("怪物猎人：世界"))
            return true;

        // 怪物猎人：崛起（RE引擎）
        if (combined.Contains("monster hunter rise", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("mhr", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("怪物猎人崛起") || name.Contains("怪物猎人：崛起"))
            return true;

        // 怪物猎人：荒野（RE引擎）
        if (combined.Contains("monster hunter wilds", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("mh wilds", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("怪物猎人荒野") || name.Contains("怪物猎人：荒野"))
            return true;

        // 怪物猎人物语3（RE引擎）
        if (combined.Contains("monster hunter stories 3", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("monster hunter stories", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("怪物猎人物语3") || name.Contains("怪物猎人物语"))
            return true;

        // 原始袭变（RE引擎）
        if (combined.Contains("exoprimal", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("原始袭变"))
            return true;

        // 龙之信条2（RE引擎）
        if (combined.Contains("dragon's dogma 2", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("dragons dogma 2", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("龙之信条2") || name.Contains("龙之信条 2"))
            return true;

        // 街头霸王6（RE引擎）
        if (combined.Contains("street fighter 6", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("sf6", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("街头霸王6") || name.Contains("街霸6"))
            return true;

        // 祇：女神之路（RE引擎）
        if (combined.Contains("kunitsu-gami", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("kunitsu gami", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("path of the goddess", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("祇：女神之路") || name.Contains("祇女神之路") || name.Contains("国津神"))
            return true;

        // 识质存在（Pragmata，RE引擎）
        if (combined.Contains("pragmata", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Pragmata") || name.Contains("识质存在"))
            return true;

        // 鬼武者：剑之道（RE引擎）
        if (combined.Contains("onimusha", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("way of the sword", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("鬼武者"))
            return true;

        return false;
    }

    /// <summary>DLSS5 RE引擎通用补丁目录（延迟解析）</summary>
    private string? _dlss5REEngineDir;
    /// <summary>
    /// 获取DLSS5 RE引擎通用补丁目录（从嵌入资源提取）
    /// </summary>
    private string GetDlss5REEngineDirectory()
    {
        if (_dlss5REEngineDir != null && Directory.Exists(_dlss5REEngineDir))
            return _dlss5REEngineDir;
        _dlss5REEngineDir = ExtractDlss5Patch("REEngine", "dlss5_reengine_");
        // 复制共享的 nvngx_dlss.dll（从 Common 目录，避免重复嵌入）
        var commonDir = GetDlss5CommonDirectory();
        var sharedDlss = Path.Combine(commonDir, "nvngx_dlss.dll");
        if (File.Exists(sharedDlss))
            File.Copy(sharedDlss, Path.Combine(_dlss5REEngineDir, "nvngx_dlss.dll"), true);
        return _dlss5REEngineDir;
    }

    /// <summary>
    /// 从嵌入资源提取DLSS5补丁到临时目录
    /// </summary>
    private string ExtractDlss5Patch(string subFolder, string tempPrefix)
    {
        // 使用固定的缓存目录（基于版本号），避免每次启动都重新提取
        var cacheDir = Path.Combine(Path.GetTempPath(), $"{tempPrefix}{CurrentVersion}");
        try
        {
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = $"DLSSFrameGenEnabler.Patches.DLSS5.{subFolder}.";
            var extracted = 0;

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix)) continue;

                var relativePath = resourceName.Substring(resourcePrefix.Length);

                // 处理子目录：把点号转换为目录分隔符（从右往左找最后一个点号作为扩展名分隔）
                var lastDot = relativePath.LastIndexOf('.');
                string outputPath;
                if (lastDot <= 0)
                {
                    outputPath = Path.Combine(cacheDir, relativePath);
                }
                else
                {
                    var ext = relativePath.Substring(lastDot);
                    var pathPart = relativePath.Substring(0, lastDot).Replace('.', Path.DirectorySeparatorChar);
                    outputPath = Path.Combine(cacheDir, pathPart + ext);
                }

                // 已存在的文件跳过，避免重复提取
                if (File.Exists(outputPath))
                {
                    extracted++;
                    continue;
                }

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(outputPath);
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            if (extracted > 0)
                return cacheDir;

            try { Directory.Delete(cacheDir, true); } catch { }
            throw new Exception($"未找到DLSS5 {subFolder} 补丁资源");
        }
        catch
        {
            try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { }
            throw;
        }
    }

    /// <summary>
    /// 获取DLSS5通用补丁目录
    /// </summary>
    private string GetDlss5CommonDirectory()
    {
        if (!string.IsNullOrEmpty(_dlss5CommonDir) && Directory.Exists(_dlss5CommonDir))
            return _dlss5CommonDir;
        _dlss5CommonDir = ExtractDlss5Patch("Common", "DLSS5_Common_");
        return _dlss5CommonDir;
    }

    /// <summary>
    /// 获取DLSS5 N卡补丁目录
    /// 注意：N卡补丁的renodx-dlss5.addon64已移到Shared目录，Nvidia目录可能为空
    /// </summary>
    private string GetDlss5NvidiaDirectory()
    {
        if (!string.IsNullOrEmpty(_dlss5NvidiaDir) && Directory.Exists(_dlss5NvidiaDir))
            return _dlss5NvidiaDir;

        try
        {
            _dlss5NvidiaDir = ExtractDlss5Patch("Nvidia", "DLSS5_Nvidia_");
        }
        catch
        {
            // Nvidia目录可能为空（renodx-dlss5.addon64已移到Shared），创建一个空目录
            var cacheDir = Path.Combine(Path.GetTempPath(), $"DLSS5_Nvidia_Empty_{CurrentVersion}");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
            _dlss5NvidiaDir = cacheDir;
        }
        return _dlss5NvidiaDir;
    }

    /// <summary>
    /// 获取DLSS5 A卡补丁目录
    /// </summary>
    private string GetDlss5AmdDirectory()
    {
        if (!string.IsNullOrEmpty(_dlss5AmdDir) && Directory.Exists(_dlss5AmdDir))
            return _dlss5AmdDir;
        _dlss5AmdDir = ExtractDlss5Patch("AMD", "DLSS5_AMD_");
        return _dlss5AmdDir;
    }

    /// <summary>
    /// 获取共享大文件目录（避免重复嵌入，减小软件体积）
    /// </summary>
    private string GetSharedDirectory()
    {
        if (!string.IsNullOrEmpty(_sharedDir) && Directory.Exists(_sharedDir))
            return _sharedDir;

        var cacheDir = Path.Combine(Path.GetTempPath(), $"Shared_Patch_{CurrentVersion}");

        // 完整性检查：如果缓存目录存在但是不包含reshade_shaders_common目录，说明是之前错误提取的，删除重新提取
        // 注意：嵌入式资源里连字符会被替换成下划线，所以实际目录名是reshade_shaders_common
        if (Directory.Exists(cacheDir))
        {
            var reshadeCommonDir = Path.Combine(cacheDir, "reshade_shaders_common");
            if (!Directory.Exists(reshadeCommonDir))
            {
                try { Directory.Delete(cacheDir, true); } catch { }
            }
        }

        try
        {
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.Shared.";
            var extracted = 0;

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix)) continue;

                var relativePath = resourceName.Substring(resourcePrefix.Length);

                // 处理子目录：把点号转换为目录分隔符（与GetDx9PatchDirectory一致）
                var lastDot = relativePath.LastIndexOf('.');
                string outputPath;
                if (lastDot <= 0)
                {
                    outputPath = Path.Combine(cacheDir, relativePath);
                }
                else
                {
                    var ext = relativePath.Substring(lastDot);
                    var pathPart = relativePath.Substring(0, lastDot).Replace('.', Path.DirectorySeparatorChar);
                    outputPath = Path.Combine(cacheDir, pathPart + ext);
                }

                if (File.Exists(outputPath))
                {
                    extracted++;
                    continue;
                }

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(outputPath);
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            if (extracted > 0)
            {
                _sharedDir = cacheDir;
                return cacheDir;
            }

            try { Directory.Delete(cacheDir, true); } catch { }
            throw new Exception("未找到共享补丁资源");
        }
        catch
        {
            try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { }
            throw;
        }
    }

    /// <summary>
    /// 检测游戏是否已开启DLSS5
    /// </summary>
    public static bool IsDLSS5Enabled(GameInfo game)
    {
        var exeDir = game.GameExeDirectory;
        if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
            return false;
        // DLSS5的特征文件：nvngx_dlssnr.dll
        return File.Exists(Path.Combine(exeDir, "nvngx_dlssnr.dll"));
    }

    /// <summary>
    /// 检测DLSS5适用的显卡类型（根据游戏目录中的文件判断）
    /// </summary>
    public static string DetectDLSS5GpuType(GameInfo game)
    {
        var exeDir = game.GameExeDirectory;
        if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
            return "";
        if (File.Exists(Path.Combine(exeDir, "renodx-dlss5.addon64")))
            return "NVIDIA";
        if (File.Exists(Path.Combine(exeDir, "dlssnr_on_amd_setup.exe")))
            return "AMD";
        return "Unknown";
    }

    /// <summary>
    /// 安装DLSS5补丁
    /// </summary>
    public PatchResult InstallDLSS5(GameInfo game, bool useREEngineMode = false)
    {
        var result = new PatchResult { GameName = game.Name };
        try
        {
            // 确定游戏真正运行exe的目录
            var exeDir = game.GameExeDirectory;
            if (string.IsNullOrEmpty(exeDir))
            {
                result.Success = false;
                result.Message = "无法确定游戏运行目录，请先选择游戏。";
                return result;
            }
            if (!Directory.Exists(exeDir))
            {
                result.Success = false;
                result.Message = $"游戏目录不存在：{exeDir}";
                return result;
            }

            var installedFiles = new List<string>();

            // DLSS5备份目录（用于保存快照，还原时精确删除新增文件）
            var dlss5BackupDir = Path.Combine(GetBackupDirectory(game), "DLSS5");
            if (!Directory.Exists(dlss5BackupDir))
                Directory.CreateDirectory(dlss5BackupDir);

            // 记录游戏目录文件快照
            SnapshotGameFiles(exeDir, dlss5BackupDir);

            // ========== RE引擎通用模式 ==========
            if (useREEngineMode)
            {
                // 1. 先安装ReShade框架文件（从Common目录复制，包括子目录）
                var reEngineCommonDir = GetDlss5CommonDirectory();
                foreach (var file in Directory.GetFiles(reEngineCommonDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = file.Substring(reEngineCommonDir.Length + 1);
                    var destPath = Path.Combine(exeDir, relativePath);
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);
                    File.Copy(file, destPath, true);
                    installedFiles.Add(relativePath);
                }

                // 1.0.1 从Shared\reshade_shaders_common复制共享的reshade着色器文件（避免重复嵌入减小体积）
                // 注意：嵌入式资源里连字符会被替换成下划线，所以实际目录名是reshade_shaders_common
                var reSharedReshadeCommonDir = Path.Combine(GetSharedDirectory(), "reshade_shaders_common");
                if (Directory.Exists(reSharedReshadeCommonDir))
                {
                    foreach (var shaderFile in Directory.GetFiles(reSharedReshadeCommonDir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = "reshade-shaders\\" + shaderFile.Substring(reSharedReshadeCommonDir.Length + 1);
                        var destPath = Path.Combine(exeDir, relativePath);
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        File.Copy(shaderFile, destPath, true);
                        installedFiles.Add(relativePath.Replace("\\", "/"));
                    }
                }

                // 1.1 从Shared目录复制共享大文件（nvngx_dlss.dll、dxgi.dll，nvngx_dlssnr.dll用RE引擎专属版本）
                var reSharedDir = GetSharedDirectory();
                var reSharedFiles = new[] { "nvngx_dlss.dll", "dxgi.dll" };
                foreach (var sharedFile in reSharedFiles)
                {
                    var srcPath = Path.Combine(reSharedDir, sharedFile);
                    var dstPath = Path.Combine(exeDir, sharedFile);
                    if (File.Exists(srcPath))
                    {
                        File.Copy(srcPath, dstPath, true);
                        installedFiles.Add(sharedFile);
                    }
                }

                // 1.2 从Shared\Fonts复制中文字体文件（避免重复嵌入，减小体积）
                var reSharedFontsDir = Path.Combine(reSharedDir, "Fonts");
                if (Directory.Exists(reSharedFontsDir))
                {
                    var reFontsDstDir = Path.Combine(exeDir, "Fonts");
                    if (!Directory.Exists(reFontsDstDir))
                        Directory.CreateDirectory(reFontsDstDir);

                    foreach (var fontFile in Directory.GetFiles(reSharedFontsDir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = fontFile.Substring(reSharedFontsDir.Length + 1);
                        var dstFontFile = Path.Combine(reFontsDstDir, relativePath);
                        var dstFontDir = Path.GetDirectoryName(dstFontFile);
                        if (!string.IsNullOrEmpty(dstFontDir) && !Directory.Exists(dstFontDir))
                            Directory.CreateDirectory(dstFontDir);

                        File.Copy(fontFile, dstFontFile, true);
                        installedFiles.Add($"Fonts/{relativePath.Replace("\\", "/")}");
                    }
                }

                // 2. 安装RE引擎通用补丁（所有文件直接放到游戏根目录）
                var reEngineDir = GetDlss5REEngineDirectory();
                foreach (var file in Directory.GetFiles(reEngineDir, "*", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(exeDir, fileName);
                    File.Copy(file, destPath, true);
                    installedFiles.Add(fileName);
                }

                // 更新游戏状态
                game.IsDLSS5Patched = true;
                game.DLSS5GpuType = "REEngine";

                result.Success = true;
                result.Message = $"DLSS5（RE引擎通用）补丁安装成功！\n\n" +
                               $"已安装 {installedFiles.Count} 个文件到：\n{exeDir}\n\n" +
                               "包含ReShade框架 + RE引擎专属补丁。\n\n" +
                               "启动游戏即可体验DLSS5。";
                return result;
            }

            // ========== 通用模式（默认） ==========
            // 检测显卡类型
            var gpuType = DetectGpuType();
            if (gpuType == "Unknown")
            {
                result.Success = false;
                result.Message = "无法检测到显卡类型，请确认已安装NVIDIA或AMD显卡驱动。";
                return result;
            }

            // 1. 安装通用文件（reshade配置 + nvngx_dlssnr.dll）
            var commonDir = GetDlss5CommonDirectory();
            foreach (var file in Directory.GetFiles(commonDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(commonDir.Length + 1);
                var destPath = Path.Combine(exeDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, true);
                installedFiles.Add(relativePath);
            }

            // 1.0.1 从Shared\reshade_shaders_common复制共享的reshade着色器文件（避免重复嵌入减小体积）
            // 注意：嵌入式资源里连字符会被替换成下划线，所以实际目录名是reshade_shaders_common
            var sharedReshadeCommonDir = Path.Combine(GetSharedDirectory(), "reshade_shaders_common");
            if (Directory.Exists(sharedReshadeCommonDir))
            {
                foreach (var shaderFile in Directory.GetFiles(sharedReshadeCommonDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = "reshade-shaders\\" + shaderFile.Substring(sharedReshadeCommonDir.Length + 1);
                    var destPath = Path.Combine(exeDir, relativePath);
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    File.Copy(shaderFile, destPath, true);
                    installedFiles.Add(relativePath.Replace("\\", "/"));
                }
            }

            // 1.1 从Shared目录复制共享大文件（避免重复嵌入，减小体积）
            var sharedDir = GetSharedDirectory();
            var sharedFiles = new[] { "nvngx_dlssnr.dll", "nvngx_dlss.dll", "dxgi.dll" };
            foreach (var sharedFile in sharedFiles)
            {
                var srcPath = Path.Combine(sharedDir, sharedFile);
                var dstPath = Path.Combine(exeDir, sharedFile);
                if (File.Exists(srcPath))
                {
                    File.Copy(srcPath, dstPath, true);
                    installedFiles.Add(sharedFile);
                }
            }

            // 1.2 从Shared\Fonts复制中文字体文件（避免重复嵌入，减小体积）
            var sharedFontsDir = Path.Combine(sharedDir, "Fonts");
            if (Directory.Exists(sharedFontsDir))
            {
                var fontsDstDir = Path.Combine(exeDir, "Fonts");
                if (!Directory.Exists(fontsDstDir))
                    Directory.CreateDirectory(fontsDstDir);

                foreach (var fontFile in Directory.GetFiles(sharedFontsDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = fontFile.Substring(sharedFontsDir.Length + 1);
                    var dstFontFile = Path.Combine(fontsDstDir, relativePath);
                    var dstFontDir = Path.GetDirectoryName(dstFontFile);
                    if (!string.IsNullOrEmpty(dstFontDir) && !Directory.Exists(dstFontDir))
                        Directory.CreateDirectory(dstFontDir);

                    File.Copy(fontFile, dstFontFile, true);
                    installedFiles.Add($"Fonts/{relativePath.Replace("\\", "/")}");
                }
            }

            // 2. 安装对应显卡的补丁
            if (gpuType == "NVIDIA")
            {
                var nvidiaDir = GetDlss5NvidiaDirectory();
                foreach (var file in Directory.GetFiles(nvidiaDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = file.Substring(nvidiaDir.Length + 1);
                    var destPath = Path.Combine(exeDir, relativePath);
                    File.Copy(file, destPath, true);
                    installedFiles.Add(relativePath);
                }
                // 从Shared目录复制renodx-dlss5.addon64
                var addonSrc = Path.Combine(sharedDir, "renodx-dlss5.addon64");
                var addonDst = Path.Combine(exeDir, "renodx-dlss5.addon64");
                if (File.Exists(addonSrc))
                {
                    File.Copy(addonSrc, addonDst, true);
                    installedFiles.Add("renodx-dlss5.addon64");
                }
            }
            else if (gpuType == "AMD")
            {
                var amdDir = GetDlss5AmdDirectory();
                foreach (var file in Directory.GetFiles(amdDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = file.Substring(amdDir.Length + 1);
                    var destPath = Path.Combine(exeDir, relativePath);
                    File.Copy(file, destPath, true);
                    installedFiles.Add(relativePath);
                }
            }

            // 更新游戏状态
            game.IsDLSS5Patched = true;
            game.DLSS5GpuType = gpuType;

            // 3. 如果是A卡，自动打开 dlssnr_on_amd_setup.exe
            var amdSetupPath = Path.Combine(exeDir, "dlssnr_on_amd_setup.exe");
            if (gpuType == "AMD" && File.Exists(amdSetupPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(amdSetupPath);
                    result.Success = true;
                    result.Message = $"DLSS5（A卡）补丁安装成功！\n\n" +
                                   $"已安装 {installedFiles.Count} 个文件到：\n{exeDir}\n\n" +
                                   "已自动打开 dlssnr_on_amd_setup.exe，请按照程序提示完成设置。\n\n" +
                                   "设置完成后启动游戏即可体验DLSS5。";
                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = true;
                    result.Message = $"DLSS5（A卡）补丁安装成功！\n\n" +
                                   $"已安装 {installedFiles.Count} 个文件到：\n{exeDir}\n\n" +
                                   $"自动打开设置程序失败：{ex.Message}\n" +
                                   $"请手动运行：{amdSetupPath}";
                    return result;
                }
            }

            result.Success = true;
            result.Message = $"DLSS5（{gpuType}）补丁安装成功！\n\n" +
                           $"已安装 {installedFiles.Count} 个文件到：\n{exeDir}\n\n" +
                           "启动游戏即可体验DLSS5。";
            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。";
            return result;
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"DLSS5补丁安装时发生错误：{ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// 还原DLSS5补丁（删除所有DLSS5新增的文件）
    /// </summary>
    public PatchResult RestoreDLSS5(GameInfo game)
    {
        var result = new PatchResult { GameName = game.Name };
        try
        {
            var exeDir = game.GameExeDirectory;
            if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(exeDir))
            {
                result.Success = false;
                result.Message = "无法确定游戏运行目录。";
                return result;
            }

            var deletedFiles = new List<string>();

            // ========== RE引擎通用模式还原 ==========
            if (game.DLSS5GpuType == "REEngine")
            {
                // RE引擎通用方案的所有文件
                var reEngineFiles = new[]
                {
                    "nvngx_dlss.dll",
                    "nvngx_dlssnr.dll",
                    "PDPerfPlugin.dll",
                    "renodx-dlss5.addon64",
                    "dinput8.dll",
                    "RE_DLSS5_Core.dll",
                    "RE_DLSS5_Core_settings.json",
                    "version.dll"
                };

                foreach (var fileName in reEngineFiles)
                {
                    var filePath = Path.Combine(exeDir, fileName);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        deletedFiles.Add(fileName);
                    }
                }

                // 删除ReShade框架文件（如果没有开启高级模式，高级模式也共用这些文件）
                if (!game.IsAdvancedPatched)
                {
                    var reshadeFiles = new[]
                    {
                        "dxgi.dll",
                        "ReShade.ini",
                        "ReShade.log",
                        "ReShadePreset.ini"
                    };
                    foreach (var fileName in reshadeFiles)
                    {
                        var filePath = Path.Combine(exeDir, fileName);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            deletedFiles.Add(fileName);
                        }
                    }

                    // 删除 reshade-shaders 目录
                    var reEngineReshadeShadersDir = Path.Combine(exeDir, "reshade-shaders");
                    if (Directory.Exists(reEngineReshadeShadersDir))
                    {
                        Directory.Delete(reEngineReshadeShadersDir, true);
                        deletedFiles.Add("reshade-shaders\\");
                    }
                }

                // 快照对比：删除所有新增文件
                var reEngineBackupDir = Path.Combine(GetBackupDirectory(game), "DLSS5");
                if (Directory.Exists(reEngineBackupDir))
                {
                    RestoreFromSnapshot(exeDir, reEngineBackupDir);
                    try { Directory.Delete(reEngineBackupDir, true); } catch { }
                }

                // 更新游戏状态
                game.IsDLSS5Patched = false;
                game.DLSS5GpuType = "";

                result.Success = true;
                result.Message = $"DLSS5（RE引擎通用）补丁还原成功！\n\n" +
                               $"已删除 {deletedFiles.Count} 个DLSS5补丁文件：\n" +
                               string.Join("\n", deletedFiles.Select(f => "  - " + f)) +
                               "\n\n所有DLSS5补丁文件已全部删除，游戏已恢复到未开启DLSS5的状态。";
                return result;
            }

            // ========== 通用模式还原 ==========
            // DLSS5独有的文件列表（这些文件是DLSS5新增的，还原时删除）
            var dlss5ExclusiveFiles = new[]
            {
                "nvngx_dlssnr.dll",
                "renodx-dlss5.addon64",
                "dlssnr_on_amd_setup.exe"
            };

            // 删除DLSS5独有文件
            foreach (var fileName in dlss5ExclusiveFiles)
            {
                var filePath = Path.Combine(exeDir, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    deletedFiles.Add(fileName);
                }
            }

            // 删除 reshade-shaders 目录（DLSS5独有，高级模式没有这个目录）
            var reshadeShadersDir = Path.Combine(exeDir, "reshade-shaders");
            if (Directory.Exists(reshadeShadersDir))
            {
                Directory.Delete(reshadeShadersDir, true);
                deletedFiles.Add("reshade-shaders\\");
            }

            // 如果没有开启高级模式（高级模式也用dxgi.dll和ReShade.*），则删除这些共用文件
            if (!game.IsAdvancedPatched)
            {
                var sharedFiles = new[]
                {
                    "dxgi.dll",
                    "ReShade.ini",
                    "ReShade.log",
                    "ReShadePreset.ini"
                };
                foreach (var fileName in sharedFiles)
                {
                    var filePath = Path.Combine(exeDir, fileName);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        deletedFiles.Add(fileName);
                    }
                }
            }

            // 快照对比：删除所有DLSS5安装后新增的文件（确保不遗漏，不误删游戏文件）
            var dlss5BackupDir = Path.Combine(GetBackupDirectory(game), "DLSS5");
            if (Directory.Exists(dlss5BackupDir))
            {
                var (_, snapshotDeleted) = RestoreFromSnapshot(exeDir, dlss5BackupDir);
                // 删除DLSS5备份目录
                try { Directory.Delete(dlss5BackupDir, true); } catch { }
            }

            // 更新游戏状态
            game.IsDLSS5Patched = false;
            game.DLSS5GpuType = "";

            result.Success = true;
            result.Message = $"DLSS5补丁还原成功！\n\n" +
                           $"已删除 {deletedFiles.Count} 个DLSS5补丁文件：\n" +
                           string.Join("\n", deletedFiles.Select(f => "  - " + f)) +
                           "\n\n所有DLSS5新增文件已清理，游戏已恢复到未开启DLSS5的状态。";
            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.Message = $"权限不足：{ex.Message}\n请尝试以管理员身份运行本程序。";
            return result;
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.Message = $"文件操作失败：{ex.Message}\n请确认游戏未在运行。";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"DLSS5补丁还原时发生错误：{ex.Message}";
            return result;
        }
    }

    #endregion

    #region DX9 DLSS5 补丁功能

    /// <summary>DX9补丁目录（延迟解析）</summary>
    private string? _dx9PatchDir;
    private string? _rtx20PatchDir;
    private string? _rtx30PatchDir;
    private string? _reFrameGenDir;

    /// <summary>
    /// 获取DX9补丁目录（从嵌入资源提取）
    /// </summary>
    private string GetDx9PatchDirectory()
    {
        if (!string.IsNullOrEmpty(_dx9PatchDir) && Directory.Exists(_dx9PatchDir))
            return _dx9PatchDir;

        // 使用固定的缓存目录（基于版本号）
        var cacheDir = Path.Combine(Path.GetTempPath(), $"DX9_Patch_{CurrentVersion}");
        try
        {
            // 完整性检查：如果缓存目录存在但是不包含必要文件，删除重新提取
            if (Directory.Exists(cacheDir))
            {
                bool cacheValid = true;
                // 检查必要文件是否存在
                var requiredFiles = new[] { "dxgi.dll", "D3D9.dll", "ReShade.ini", "dlss5-feed.addon32" };
                foreach (var reqFile in requiredFiles)
                {
                    if (!File.Exists(Path.Combine(cacheDir, reqFile)))
                    {
                        cacheValid = false;
                        break;
                    }
                }
                // 检查reshade-shaders目录是否存在且有文件
                var reshadeDir = Path.Combine(cacheDir, "reshade-shaders");
                if (!Directory.Exists(reshadeDir) || (Directory.GetFiles(reshadeDir, "*", SearchOption.AllDirectories).Length == 0))
                {
                    cacheValid = false;
                }
                // 检查host64目录是否存在且有文件
                var host64Dir = Path.Combine(cacheDir, "host64");
                if (!Directory.Exists(host64Dir) || (Directory.GetFiles(host64Dir, "*", SearchOption.AllDirectories).Length == 0))
                {
                    cacheValid = false;
                }

                if (!cacheValid)
                {
                    try { Directory.Delete(cacheDir, true); } catch { }
                }
            }

            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.DX9.";
            var extracted = 0;

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix)) continue;

                var relativePath = resourceName.Substring(resourcePrefix.Length);

                // 处理子目录：把点号转换为目录分隔符
                var lastDot = relativePath.LastIndexOf('.');
                string outputPath;
                if (lastDot <= 0)
                {
                    outputPath = Path.Combine(cacheDir, relativePath);
                }
                else
                {
                    var ext = relativePath.Substring(lastDot);
                    var pathPart = relativePath.Substring(0, lastDot).Replace('.', Path.DirectorySeparatorChar);
                    outputPath = Path.Combine(cacheDir, pathPart + ext);
                }

                if (File.Exists(outputPath))
                {
                    extracted++;
                    continue;
                }

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(outputPath);
                    stream.CopyTo(fileStream);
                    extracted++;
                }
            }

            if (extracted > 0)
            {
                _dx9PatchDir = cacheDir;
                return cacheDir;
            }

            try { Directory.Delete(cacheDir, true); } catch { }
            throw new Exception("未找到DX9补丁资源");
        }
        catch
        {
            try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { }
            throw;
        }
    }

    /// <summary>
    /// 获取RTX20系多帧生成补丁目录（从嵌入资源提取）
    /// </summary>
    private string GetRTX20Directory()
    {
        if (!string.IsNullOrEmpty(_rtx20PatchDir) && Directory.Exists(_rtx20PatchDir))
            return _rtx20PatchDir;

        var cacheDir = Path.Combine(Path.GetTempPath(), $"RTX20_Patch_{CurrentVersion}");
        try
        {
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.RTX20.";
            var extracted = 0;

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix)) continue;
                var relativePath = resourceName.Substring(resourcePrefix.Length);
                var lastDot = relativePath.LastIndexOf('.');
                string outputPath;
                if (lastDot <= 0) { outputPath = Path.Combine(cacheDir, relativePath); }
                else { var ext = relativePath.Substring(lastDot); var pathPart = relativePath.Substring(0, lastDot).Replace('.', Path.DirectorySeparatorChar); outputPath = Path.Combine(cacheDir, pathPart + ext); }
                if (File.Exists(outputPath)) { extracted++; continue; }
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null) { using var fileStream = File.Create(outputPath); stream.CopyTo(fileStream); extracted++; }
            }

            if (extracted > 0) { _rtx20PatchDir = cacheDir; return cacheDir; }
            try { Directory.Delete(cacheDir, true); } catch { }
            throw new Exception("未找到RTX20补丁资源");
        }
        catch { try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { } throw; }
    }

    /// <summary>
    /// 获取RTX30系多帧生成补丁目录（从嵌入资源提取）
    /// </summary>
    private string GetRTX30Directory()
    {
        if (!string.IsNullOrEmpty(_rtx30PatchDir) && Directory.Exists(_rtx30PatchDir))
            return _rtx30PatchDir;

        var cacheDir = Path.Combine(Path.GetTempPath(), $"RTX30_Patch_{CurrentVersion}");
        try
        {
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.RTX30.";
            var extracted = 0;
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix)) continue;
                var relativePath = resourceName.Substring(resourcePrefix.Length);
                var lastDot = relativePath.LastIndexOf('.');
                string outputPath;
                if (lastDot <= 0) { outputPath = Path.Combine(cacheDir, relativePath); }
                else { var ext = relativePath.Substring(lastDot); var pathPart = relativePath.Substring(0, lastDot).Replace('.', Path.DirectorySeparatorChar); outputPath = Path.Combine(cacheDir, pathPart + ext); }
                if (File.Exists(outputPath)) { extracted++; continue; }
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null) { using var fileStream = File.Create(outputPath); stream.CopyTo(fileStream); extracted++; }
            }
            if (extracted > 0) { _rtx30PatchDir = cacheDir; return cacheDir; }
            try { Directory.Delete(cacheDir, true); } catch { }
            throw new Exception("未找到RTX30补丁资源");
        }
        catch { try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { } throw; }
    }

    /// <summary>
    /// 获取RE引擎多帧生成补丁目录（从嵌入资源提取）
    /// </summary>
    private string GetREFrameGenDirectory()
    {
        if (!string.IsNullOrEmpty(_reFrameGenDir) && Directory.Exists(_reFrameGenDir))
            return _reFrameGenDir;

        var cacheDir = Path.Combine(Path.GetTempPath(), $"REFrameGen_Patch_{CurrentVersion}");
        try
        {
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.REFrameGen.";
            var extracted = 0;
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(resourcePrefix)) continue;
                var relativePath = resourceName.Substring(resourcePrefix.Length);
                var lastDot = relativePath.LastIndexOf('.');
                string outputPath;
                if (lastDot <= 0) { outputPath = Path.Combine(cacheDir, relativePath); }
                else { var ext = relativePath.Substring(lastDot); var pathPart = relativePath.Substring(0, lastDot).Replace('.', Path.DirectorySeparatorChar); outputPath = Path.Combine(cacheDir, pathPart + ext); }
                if (File.Exists(outputPath)) { extracted++; continue; }
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null) { using var fileStream = File.Create(outputPath); stream.CopyTo(fileStream); extracted++; }
            }
            if (extracted > 0) { _reFrameGenDir = cacheDir; return cacheDir; }
            try { Directory.Delete(cacheDir, true); } catch { }
            throw new Exception("未找到RE引擎多帧生成补丁资源");
        }
        catch { try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { } throw; }
    }

    /// <summary>
    /// 检测游戏exe是32位还是64位
    /// </summary>
    public static bool Is32BitExecutable(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return false;

            var bytes = new byte[4096];
            using (var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read))
            {
                var read = fs.Read(bytes, 0, 4096);
                if (read < 64) return false;
            }

            // PE头偏移在0x3C处
            var peOffset = BitConverter.ToInt32(bytes, 0x3C);
            if (peOffset + 4 >= bytes.Length) return false;

            // Machine字段在PE签名后4字节
            var machine = BitConverter.ToUInt16(bytes, peOffset + 4);
            return machine == 0x14c; // 0x14c = x86 (32位), 0x8664 = x64 (64位)
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检测游戏是否为DX9游戏
    /// 检测方式：1. exe本身是否引用d3d9.dll 2. 游戏目录中是否有典型DX9文件
    /// </summary>
    /// <summary>
    /// 渲染API类型枚举
    /// </summary>
    public enum RenderAPIType
    {
        Unknown,        // 未知
        DX9Only,        // 仅DX9
        DX11AndAbove,   // DX11及以上（可能也支持DX9）
        MultiAPI        // 同时支持DX9和DX11+
    }

    /// <summary>
    /// 解析PE文件导入表，获取exe实际导入的DLL列表（最准确的检测方式）
    /// </summary>
    private static string[] GetPEImportDlls(string exePath)
    {
        try
        {
            var dlls = new System.Collections.Generic.List<string>();
            var bytes = File.ReadAllBytes(exePath);
            if (bytes.Length < 0x40) return dlls.ToArray();
            int peOffset = BitConverter.ToInt32(bytes, 0x3C);
            if (peOffset <= 0 || peOffset + 24 > bytes.Length) return dlls.ToArray();
            if (bytes[peOffset] != 'P' || bytes[peOffset + 1] != 'E') return dlls.ToArray();
            int coffOffset = peOffset + 4;
            ushort optHeaderSize = BitConverter.ToUInt16(bytes, coffOffset + 16);
            int optOffset = coffOffset + 20;
            if (optOffset + 2 > bytes.Length) return dlls.ToArray();
            ushort magic = BitConverter.ToUInt16(bytes, optOffset);
            int dataDirOffset = (magic == 0x20b) ? optOffset + 112 : optOffset + 96;
            int importDirOffset = dataDirOffset + 8;
            if (importDirOffset + 8 > bytes.Length) return dlls.ToArray();
            uint importRva = BitConverter.ToUInt32(bytes, importDirOffset);
            if (importRva == 0) return dlls.ToArray();
            ushort numSections = BitConverter.ToUInt16(bytes, coffOffset + 2);
            int sectionOffset = optOffset + optHeaderSize;
            var sections = new (uint VA, uint Size, uint Ptr)[numSections];
            for (int i = 0; i < numSections; i++)
            {
                int so = sectionOffset + i * 40;
                if (so + 40 > bytes.Length) break;
                sections[i].VA = BitConverter.ToUInt32(bytes, so + 12);
                sections[i].Size = BitConverter.ToUInt32(bytes, so + 16);
                sections[i].Ptr = BitConverter.ToUInt32(bytes, so + 20);
            }
            uint RvaToOff(uint rva)
            {
                foreach (var s in sections)
                    if (rva >= s.VA && rva < s.VA + s.Size)
                        return rva - s.VA + s.Ptr;
                return 0;
            }
            uint importOff = RvaToOff(importRva);
            if (importOff == 0) return dlls.ToArray();
            for (int i = 0; ; i++)
            {
                int d = (int)importOff + i * 20;
                if (d + 20 > bytes.Length) break;
                uint nrva = BitConverter.ToUInt32(bytes, d + 12);
                if (nrva == 0) break;
                uint noff = RvaToOff(nrva);
                if (noff == 0 || noff >= bytes.Length) continue;
                int nl = 0;
                while (noff + nl < bytes.Length && bytes[noff + nl] != 0 && nl < 256) nl++;
                if (nl > 0) dlls.Add(System.Text.Encoding.ASCII.GetString(bytes, (int)noff, nl).ToLowerInvariant());
            }
            return dlls.ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>
    /// 检测游戏的渲染API类型（基于PE导入表，最准确）
    /// </summary>
    public static RenderAPIType DetectRenderAPIType(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return RenderAPIType.Unknown;
            var exeDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(exeDir)) return RenderAPIType.Unknown;

            // 第一层：PE导入表检测（最准确）
            var importDlls = GetPEImportDlls(exePath);
            bool impD3D9 = importDlls.Contains("d3d9.dll");
            bool impDX11 = importDlls.Contains("d3d11.dll") || importDlls.Contains("d3d12.dll") || importDlls.Contains("dxgi.dll");
            if (impD3D9 && impDX11) return RenderAPIType.MultiAPI;
            if (impD3D9 && !impDX11) return RenderAPIType.DX9Only;
            if (!impD3D9 && impDX11) return RenderAPIType.DX11AndAbove;

            // 第二层：exe目录游戏自带DLL检测（排除我们的补丁）
            string gameRootDir = FindGameRootDirectory(exeDir);
            bool hasGameD3D9 = false;
            var exeD3D9 = Path.Combine(exeDir, "d3d9.dll");
            if (File.Exists(exeD3D9) && !File.Exists(Path.Combine(exeDir, "dgVoodoo.conf")))
            {
                if (new FileInfo(exeD3D9).Length < 200 * 1024) hasGameD3D9 = true;
            }
            bool hasGameDXGI = false;
            var exeDXGI = Path.Combine(exeDir, "dxgi.dll");
            if (File.Exists(exeDXGI) && !File.Exists(Path.Combine(exeDir, "ReShade.ini")))
                hasGameDXGI = true;
            if (hasGameD3D9 && !hasGameDXGI) return RenderAPIType.DX9Only;
            if (hasGameDXGI && !hasGameD3D9) return RenderAPIType.DX11AndAbove;

            // 第三层：游戏根目录特征文件
            bool hasD3DX9 = false;
            try { hasD3DX9 = Directory.GetFiles(gameRootDir, "d3dx9_*.dll", SearchOption.AllDirectories).Any(f => !f.ToLower().Contains("redist") && !f.ToLower().Contains("directx")); } catch { }
            bool hasD3DC47 = false;
            try { hasD3DC47 = Directory.GetFiles(gameRootDir, "D3DCompiler_47.dll", SearchOption.AllDirectories).Length > 0; } catch { }
            if (hasD3DX9 && !hasD3DC47) return RenderAPIType.DX9Only;
            if (hasD3DC47 && !hasD3DX9) return RenderAPIType.DX11AndAbove;

            // 第四层：exe字符串检测（最后手段）
            var fb = File.ReadAllBytes(exePath);
            bool sD3D9 = ContainsBytes(fb, System.Text.Encoding.ASCII.GetBytes("d3d9.dll"));
            bool sDX11 = ContainsBytes(fb, System.Text.Encoding.ASCII.GetBytes("d3d11.dll")) || ContainsBytes(fb, System.Text.Encoding.ASCII.GetBytes("dxgi.dll"));
            if (sD3D9 && !sDX11) return RenderAPIType.DX9Only;
            if (sDX11 && !sD3D9) return RenderAPIType.DX11AndAbove;
            return RenderAPIType.Unknown;
        }
        catch { return RenderAPIType.Unknown; }
    }

    /// <summary>
    /// 查找游戏根目录（从exe目录往上找，直到找到包含游戏特征的目录）
    /// </summary>
    private static string FindGameRootDirectory(string exeDir)
    {
        string current = exeDir;
        for (int i = 0; i < 4; i++)
        {
            // 检查当前目录是否是游戏根目录（有游戏特征文件或目录）
            if (IsGameRootDirectory(current))
                return current;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return exeDir; // 默认返回exe目录
    }

    /// <summary>
    /// 判断目录是否是游戏根目录
    /// </summary>
    private static bool IsGameRootDirectory(string dir)
    {
        try
        {
            // 游戏根目录通常有这些特征
            var indicators = new[] { "bin", "Binaries", "data", "Data", "content", "Content", ".exe" };
            int matchCount = 0;
            foreach (var indicator in indicators)
            {
                if (indicator == ".exe")
                {
                    if (Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly).Length > 0)
                        matchCount++;
                }
                else if (Directory.Exists(Path.Combine(dir, indicator)))
                {
                    matchCount++;
                }
            }
            return matchCount >= 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Source引擎检测：bin目录 + shaderapidx9.dll等特征文件
    /// </summary>
    private static bool DetectSourceEngine(string gameRootDir, string exeDir)
    {
        try
        {
            // Source引擎特征文件
            var sourceFeatures = new[]
            {
                "shaderapidx9.dll", "materialsystem.dll", "engine.dll",
                "vstdlib.dll", "tier0.dll", "vphysics.dll", "datacache.dll",
                "studiorender.dll", "vguimatsurface.dll", "vgui2.dll"
            };

            // 检查游戏根目录下的bin目录
            var binDirs = new[]
            {
                Path.Combine(gameRootDir, "bin"),
                Path.Combine(exeDir, "bin"),
                Path.Combine(exeDir, "..", "bin")
            };

            foreach (var binDir in binDirs)
            {
                if (!Directory.Exists(binDir)) continue;

                int matchCount = 0;
                foreach (var feature in sourceFeatures)
                {
                    if (File.Exists(Path.Combine(binDir, feature)))
                        matchCount++;
                }

                // 至少3个Source引擎特征文件，判定为Source引擎
                if (matchCount >= 3)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 虚幻引擎3检测：Binaries\Win32目录 + UE3特征文件
    /// </summary>
    private static bool DetectUE3Engine(string gameRootDir, string exeDir)
    {
        try
        {
            // UE3特征文件
            var ue3Features = new[]
            {
                "Core.dll", "Engine.dll", "UnrealScript.dll",
                "WinDrv.dll", "XAudio2.dll", "UE3ShaderCompileWorker.exe"
            };

            // 检查Binaries\Win32或Binaries目录
            var binariesDirs = new[]
            {
                Path.Combine(gameRootDir, "Binaries", "Win32"),
                Path.Combine(gameRootDir, "Binaries"),
                Path.Combine(exeDir, ".."),
                exeDir
            };

            foreach (var dir in binariesDirs)
            {
                if (!Directory.Exists(dir)) continue;

                int matchCount = 0;
                foreach (var feature in ue3Features)
                {
                    if (File.Exists(Path.Combine(dir, feature)))
                        matchCount++;
                }

                if (matchCount >= 2)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 递归查找子目录
    /// </summary>
    private static string FindSubDirectory(string rootDir, string dirName)
    {
        try
        {
            if (!Directory.Exists(rootDir)) return null;

            foreach (var dir in Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(dir).Equals(dirName, StringComparison.OrdinalIgnoreCase))
                    return dir;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 递归检测目录中是否有DX11特征文件（指定深度）
    /// </summary>
    private static bool DirectoryHasDx11FilesRecursive(string dir, int maxDepth)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;

            var dx11Indicators = new[]
            {
                "d3d11.dll", "d3d12.dll", "dxgi.dll", "D3DCompiler_47.dll",
                "D3DCompiler_46.dll", "nvngx_dlss.dll", "nvngx_dlssg.dll",
                "nvngx_dlssnr.dll", "sl.dlss.dll", "sl.interposer.dll",
                "d3d11on12.dll", "D3DCompiler_43.dll"
            };

            // 跳过的目录（运行库、引擎等）
            var skipDirs = new[] { "engine", "runtime", "redist", "directx", "dotnet",
                "vcredist", "physx", "easyanticheat", "battleye", "_commonredist",
                "support", "tools", "sdk", "debug", "nvidia", "amd", "driver" };

            return SearchFilesRecursive(dir, dx11Indicators, maxDepth, 0, skipDirs, true);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 递归检测目录中是否有DX9特征文件（指定深度）
    /// </summary>
    private static bool DirectoryHasDx9FilesRecursive(string dir, int maxDepth)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;

            // 扩展的DX9特征文件列表（20+个）
            var dx9Indicators = new[]
            {
                // Source引擎
                "shaderapidx9.dll",
                // DirectX 9运行库
                "d3dx9_24.dll", "d3dx9_25.dll", "d3dx9_26.dll", "d3dx9_27.dll",
                "d3dx9_28.dll", "d3dx9_29.dll", "d3dx9_30.dll", "d3dx9_31.dll",
                "d3dx9_32.dll", "d3dx9_33.dll", "d3dx9_34.dll", "d3dx9_35.dll",
                "d3dx9_36.dll", "d3dx9_37.dll", "d3dx9_38.dll", "d3dx9_39.dll",
                "d3dx9_40.dll", "d3dx9_41.dll", "d3dx9_42.dll", "d3dx9_43.dll",
                // 视频解码
                "binkw32.dll", "bink2w32.dll",
                // 老版本编译器
                "D3DCompiler_42.dll", "D3DCompiler_43.dll",
                // 其他DX9特征
                "d3d9.dll", "d3d9d.dll"
            };

            // 跳过的目录
            var skipDirs = new[] { "engine", "runtime", "redist", "directx", "dotnet",
                "vcredist", "physx", "easyanticheat", "battleye", "_commonredist",
                "support", "tools", "sdk", "debug", "nvidia", "amd", "driver" };

            return SearchFilesRecursive(dir, dx9Indicators, maxDepth, 0, skipDirs, false);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 递归搜索文件（辅助方法）
    /// </summary>
    private static bool SearchFilesRecursive(string dir, string[] indicators, int maxDepth, int currentDepth, string[] skipDirs, bool useExactMatch)
    {
        try
        {
            if (currentDepth > maxDepth) return false;
            if (!Directory.Exists(dir)) return false;

            // 检查当前目录的文件
            foreach (var indicator in indicators)
            {
                if (useExactMatch)
                {
                    // 精确匹配
                    if (File.Exists(Path.Combine(dir, indicator)))
                        return true;
                }
                else
                {
                    // 通配符匹配（支持d3dx9_*.dll）
                    var files = Directory.GetFiles(dir, indicator, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                        return true;
                }
            }

            // 递归搜索子目录
            if (currentDepth < maxDepth)
            {
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    var dirName = Path.GetFileName(subDir).ToLowerInvariant();

                    // 跳过指定目录
                    bool shouldSkip = false;
                    foreach (var skip in skipDirs)
                    {
                        if (dirName.Contains(skip))
                        {
                            shouldSkip = true;
                            break;
                        }
                    }
                    if (shouldSkip) continue;

                    if (SearchFilesRecursive(subDir, indicators, maxDepth, currentDepth + 1, skipDirs, useExactMatch))
                        return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 辅助函数：检查字节数组中是否包含指定字节序列
    /// </summary>
    private static bool ContainsBytes(byte[] source, byte[] pattern)
    {
        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }

    public static bool IsDX9Game(string exePath)
    {
        // 使用新的渲染API检测逻辑
        // 只有纯DX9游戏才标记为DX9游戏，MultiAPI优先使用DX11+通用DLSS5
        var apiType = DetectRenderAPIType(exePath);
        return apiType == RenderAPIType.DX9Only;
    }

    /// <summary>
    /// 检测exe是否引用d3d9.dll
    /// </summary>
    private static bool ExeReferencesD3D9(string exePath)
    {
        try
        {
            var fileBytes = File.ReadAllBytes(exePath);
            var searchString = System.Text.Encoding.ASCII.GetBytes("d3d9.dll");
            var searchStringUpper = System.Text.Encoding.ASCII.GetBytes("D3D9.DLL");

            for (int i = 0; i < fileBytes.Length - searchString.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < searchString.Length; j++)
                {
                    if (fileBytes[i + j] != searchString[j] && fileBytes[i + j] != searchStringUpper[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检测目录中是否有DX9相关文件
    /// </summary>
    private static bool DirectoryHasDx9Files(string dir, string[] patterns)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;

            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
                if (files.Length > 0)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检测游戏是否支持DX11及以上渲染API
    /// 检测方式：1. exe是否引用d3d11.dll/d3d12.dll/dxgi.dll 2. 游戏目录中是否有典型DX11/DX12文件
    /// </summary>
    public static bool SupportsDX11OrAbove(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return false;

            // 方式1：检测exe本身是否引用d3d11.dll/d3d12.dll/dxgi.dll
            if (ExeReferencesD3D11OrAbove(exePath))
                return true;

            // 方式2：检测exe所在目录及上级目录中是否有典型的DX11/DX12相关文件
            var exeDir = Path.GetDirectoryName(exePath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                // 典型的DX11/DX12游戏dll文件（排除DX9专用的d3d9.dll）
                var dx11Indicators = new[]
                {
                    "d3d11.dll", "d3d12.dll", "dxgi.dll",
                    "d3dcompiler_47.dll", "d3dcompiler_46.dll",
                    "xinput1_4.dll", "xinput9_1_0.dll",
                    "amd_ags_x64.dll", "nvapi64.dll",
                    "d3d11on12.dll"
                };

                // 检查exe所在目录
                if (DirectoryHasDx11Files(exeDir, dx11Indicators))
                    return true;

                // 检查上级目录（游戏根目录）
                var parentDir = Directory.GetParent(exeDir);
                if (parentDir != null && DirectoryHasDx11Files(parentDir.FullName, dx11Indicators))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检测exe是否引用d3d11.dll/d3d12.dll/dxgi.dll
    /// </summary>
    private static bool ExeReferencesD3D11OrAbove(string exePath)
    {
        try
        {
            var fileBytes = File.ReadAllBytes(exePath);
            var searchStrings = new[]
            {
                System.Text.Encoding.ASCII.GetBytes("d3d11.dll"),
                System.Text.Encoding.ASCII.GetBytes("D3D11.DLL"),
                System.Text.Encoding.ASCII.GetBytes("d3d12.dll"),
                System.Text.Encoding.ASCII.GetBytes("D3D12.DLL"),
                System.Text.Encoding.ASCII.GetBytes("dxgi.dll"),
                System.Text.Encoding.ASCII.GetBytes("DXGI.DLL")
            };

            for (int i = 0; i < fileBytes.Length - 10; i++)
            {
                foreach (var searchString in searchStrings)
                {
                    if (i + searchString.Length > fileBytes.Length) continue;
                    bool match = true;
                    for (int j = 0; j < searchString.Length; j++)
                    {
                        if (fileBytes[i + j] != searchString[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检测目录中是否有DX11/DX12相关文件
    /// </summary>
    private static bool DirectoryHasDx11Files(string dir, string[] patterns)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;

            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
                if (files.Length > 0)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检测DX9 DLSS5是否已开启
    /// </summary>
    public static bool IsDX9DLSS5Enabled(GameInfo game)
    {
        if (game == null || string.IsNullOrEmpty(game.ExePath)) return false;
        var exeDir = Path.GetDirectoryName(game.ExePath);
        if (string.IsNullOrEmpty(exeDir)) return false;

        // DX9 DLSS5的特征文件：dgVoodoo2的d3d9.dll + dlss5-feed.addon
        var d3d9Path = Path.Combine(exeDir, "d3d9.dll");
        var addon32Path = Path.Combine(exeDir, "dlss5-feed.addon32");
        var addon64Path = Path.Combine(exeDir, "dlss5-feed.addon64");
        var dgVoodooConf = Path.Combine(exeDir, "dgVoodoo.conf");

        // 必须同时有dgVoodoo2的d3d9.dll和配置文件，以及feeder插件
        return File.Exists(d3d9Path) && File.Exists(dgVoodooConf) &&
               (File.Exists(addon32Path) || File.Exists(addon64Path));
    }

    /// <summary>
    /// 安装DX9 DLSS5补丁（新扁平化补丁结构，直接复制所有文件到游戏exe目录）
    /// </summary>
    public PatchResult InstallDX9DLSS5(GameInfo game)
    {
        var result = new PatchResult { GameName = game.Name };
        try
        {
            if (string.IsNullOrEmpty(game.ExePath) || !File.Exists(game.ExePath))
            {
                result.Success = false;
                result.Message = "未找到游戏exe文件，无法安装DX9 DLSS5补丁。";
                return result;
            }

            var exeDir = Path.GetDirectoryName(game.ExePath)!;
            var is32Bit = Is32BitExecutable(game.ExePath);
            var dx9Dir = GetDx9PatchDirectory();

            // 根据位数选择补丁目录：32位用根目录，64位用x64子目录
            var patchSourceDir = is32Bit ? dx9Dir : Path.Combine(dx9Dir, "x64");

            // 检查显卡类型（DX9 DLSS5只支持N卡，因为需要NGX运行时）
            var gpuType = DetectGpuType();
            if (!gpuType.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase))
            {
                result.Success = false;
                result.Message = $"DX9 DLSS5目前仅支持NVIDIA显卡，您的显卡是{gpuType}，无法使用。";
                return result;
            }

            // 备份目录（用于保存快照，还原时精确删除新增文件）
            var dx9BackupDir = Path.Combine(GetBackupDirectory(game), "DX9DLSS5");
            if (!Directory.Exists(dx9BackupDir))
                Directory.CreateDirectory(dx9BackupDir);

            // 记录安装前的文件快照
            var beforeSnapshotFile = Path.Combine(dx9BackupDir, "before_snapshot.txt");
            var beforeFiles = Directory.GetFiles(exeDir, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(exeDir.Length + 1).Replace("\\", "/"))
                .ToList();
            File.WriteAllLines(beforeSnapshotFile, beforeFiles);

            var installedFiles = new List<string>();

            // ========== 复制对应位数的DX9补丁文件到游戏exe目录 ==========
            // 32位：包含dgVoodoo2、ReShade、DLSS5-Feeder、LumeniteFX、host64等
            // 64位：包含64位dgVoodoo2、ReShade、DLSS5-Feeder(addon64)、LumeniteFX等，不需要host64
            var allFiles = Directory.GetFiles(patchSourceDir, "*", SearchOption.AllDirectories);
            foreach (var srcFile in allFiles)
            {
                var relativePath = srcFile.Substring(patchSourceDir.Length + 1);
                // 32位时排除x64子目录（避免把64位补丁也复制过去）
                if (is32Bit && relativePath.StartsWith("x64" + Path.DirectorySeparatorChar))
                    continue;

                var dstFile = Path.Combine(exeDir, relativePath);
                var dstDir = Path.GetDirectoryName(dstFile);
                if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                    Directory.CreateDirectory(dstDir);

                File.Copy(srcFile, dstFile, true);
                installedFiles.Add(relativePath.Replace("\\", "/"));
            }

            // 64位时：从DX9目录复制完整的reshade-shaders文件（包含DLSS5_Feed、lumenite系列和所有通用着色器）
            if (!is32Bit)
            {
                var dx9ReshadeDir = Path.Combine(dx9Dir, "reshade-shaders");
                if (Directory.Exists(dx9ReshadeDir))
                {
                    foreach (var shaderFile in Directory.GetFiles(dx9ReshadeDir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = "reshade-shaders\\" + shaderFile.Substring(dx9ReshadeDir.Length + 1);
                        var dstFile = Path.Combine(exeDir, relativePath);
                        var dstDir = Path.GetDirectoryName(dstFile);
                        if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                            Directory.CreateDirectory(dstDir);

                        File.Copy(shaderFile, dstFile, true);
                        installedFiles.Add(relativePath.Replace("\\", "/"));
                    }
                }
            }

            // 注意：32位时完整的reshade-shaders已经随patchSourceDir一起复制，64位时上面已单独复制
            // 不再需要从Shared\reshade-shaders-common额外复制，避免重复和路径错误

            // ========== 从Shared目录复制共享大文件 ==========
            var sharedDir = GetSharedDirectory();
            var sharedLargeFiles = new[] { "nvngx_dlssnr.dll", "nvngx_dlss.dll", "dxgi.dll", "renodx-dlss5.addon64" };

            if (is32Bit)
            {
                // 32位：共享大文件放到host64目录（跨进程方案）
                var host64Dir = Path.Combine(exeDir, "host64");
                if (!Directory.Exists(host64Dir))
                    Directory.CreateDirectory(host64Dir);

                foreach (var sharedFile in sharedLargeFiles)
                {
                    var srcPath = Path.Combine(sharedDir, sharedFile);
                    var dstPath = Path.Combine(host64Dir, sharedFile);
                    if (File.Exists(srcPath))
                    {
                        File.Copy(srcPath, dstPath, true);
                        installedFiles.Add($"host64/{sharedFile}");
                    }
                }
            }
            else
            {
                // 64位：共享大文件直接放到游戏exe目录（64位游戏可直接加载64位NGX运行时）
                foreach (var sharedFile in sharedLargeFiles)
                {
                    var srcPath = Path.Combine(sharedDir, sharedFile);
                    var dstPath = Path.Combine(exeDir, sharedFile);
                    if (File.Exists(srcPath))
                    {
                        File.Copy(srcPath, dstPath, true);
                        installedFiles.Add(sharedFile);
                    }
                }
            }

            // ========== 从Shared\Fonts复制中文字体文件（避免重复嵌入，减小体积） ==========
            var dx9SharedFontsDir = Path.Combine(sharedDir, "Fonts");
            if (Directory.Exists(dx9SharedFontsDir))
            {
                var dx9FontsDstDir = Path.Combine(exeDir, "Fonts");
                if (!Directory.Exists(dx9FontsDstDir))
                    Directory.CreateDirectory(dx9FontsDstDir);

                foreach (var fontFile in Directory.GetFiles(dx9SharedFontsDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = fontFile.Substring(dx9SharedFontsDir.Length + 1);
                    var dstFontFile = Path.Combine(dx9FontsDstDir, relativePath);
                    var dstFontDir = Path.GetDirectoryName(dstFontFile);
                    if (!string.IsNullOrEmpty(dstFontDir) && !Directory.Exists(dstFontDir))
                        Directory.CreateDirectory(dstFontDir);

                    File.Copy(fontFile, dstFontFile, true);
                    installedFiles.Add($"Fonts/{relativePath.Replace("\\", "/")}");
                }
            }

            // ========== 自动修复reshade-shaders目录名（如果有下划线版本则重命名为连字符版本） ==========
            var underscoreReshadeDir = Path.Combine(exeDir, "reshade_shaders");
            var hyphenReshadeDir = Path.Combine(exeDir, "reshade-shaders");
            if (Directory.Exists(underscoreReshadeDir))
            {
                try
                {
                    // 如果已经有连字符版本的目录，先合并文件
                    if (Directory.Exists(hyphenReshadeDir))
                    {
                        foreach (var file in Directory.GetFiles(underscoreReshadeDir, "*", SearchOption.AllDirectories))
                        {
                            var relativePath = file.Substring(underscoreReshadeDir.Length + 1);
                            var dstFile = Path.Combine(hyphenReshadeDir, relativePath);
                            var dstDir = Path.GetDirectoryName(dstFile);
                            if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                                Directory.CreateDirectory(dstDir);
                            File.Copy(file, dstFile, true);
                        }
                        Directory.Delete(underscoreReshadeDir, true);
                    }
                    else
                    {
                        // 直接重命名
                        Directory.Move(underscoreReshadeDir, hyphenReshadeDir);
                    }
                    // 更新installedFiles中的路径
                    for (int i = 0; i < installedFiles.Count; i++)
                    {
                        if (installedFiles[i].StartsWith("reshade_shaders/"))
                        {
                            installedFiles[i] = installedFiles[i].Replace("reshade_shaders/", "reshade-shaders/");
                        }
                    }
                }
                catch { }
            }

            // 记录安装后的文件快照
            var afterSnapshotFile = Path.Combine(dx9BackupDir, "after_snapshot.txt");
            var afterFiles = Directory.GetFiles(exeDir, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(exeDir.Length + 1).Replace("\\", "/"))
                .ToList();
            File.WriteAllLines(afterSnapshotFile, afterFiles);

            // 保存安装的文件列表
            File.WriteAllLines(Path.Combine(dx9BackupDir, "installed_files.txt"), installedFiles);

            game.IsDX9DLSS5Patched = true;

            result.Success = true;
            var bitness = is32Bit ? "32位" : "64位";
            result.Message = $"DX9 DLSS5补丁安装成功！\n\n" +
                           $"游戏类型：{bitness} DX9游戏\n" +
                           $"已安装 {installedFiles.Count} 个文件\n\n" +
                           $"游戏外配置已全部完成，启动游戏后：\n" +
                           $"1. 按Home键打开ReShade界面\n" +
                           $"2. 确认\"LUMENITE: Kernel 2.0\"已启用\n" +
                           $"3. 确认\"DLSS 5 Feed\"已启用\n" +
                           $"4. 在插件(Add-ons)标签页中开启神经渲染\n\n" +
                           $"⚠️ 如果游戏不生效：\n" +
                           $"部分DX9游戏（如半条命2）的渲染dll在bin目录，\n" +
                           $"请回到软件点击\"还原DX9 DLSS5\"按钮，\n" +
                           $"选择\"替换dll位置再测试\"，软件会自动把dgvoodoo\n" +
                           $"的三个文件移动到真正运行dll的目录。\n\n" +
                           $"- DX9游戏建议使用DLAA模式，可在插件设置中调整Work resolution";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"DX9 DLSS5补丁安装时发生错误：{ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// 还原DX9 DLSS5补丁（删除所有新增的文件）
    /// </summary>
    public PatchResult RestoreDX9DLSS5(GameInfo game)
    {
        var result = new PatchResult { GameName = game.Name };
        try
        {
            if (string.IsNullOrEmpty(game.ExePath))
            {
                result.Success = false;
                result.Message = "未找到游戏exe路径。";
                return result;
            }

            var exeDir = Path.GetDirectoryName(game.ExePath)!;
            var dx9BackupDir = Path.Combine(GetBackupDirectory(game), "DX9DLSS5");
            var deletedFiles = new List<string>();
            bool usedSnapshot = false;

            // 方法1：通过快照对比删除新增文件（最准确）
            if (Directory.Exists(dx9BackupDir))
            {
                var beforeSnapshot = Path.Combine(dx9BackupDir, "before_snapshot.txt");
                if (File.Exists(beforeSnapshot))
                {
                    var beforeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var line in File.ReadAllLines(beforeSnapshot))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            // 统一路径分隔符为反斜杠
                            var normalizedPath = line.Trim().Replace("/", "\\");
                            beforeFiles.Add(normalizedPath);
                        }
                    }

                    // 扫描当前文件，删除快照中不存在的
                    var currentFiles = Directory.GetFiles(exeDir, "*", SearchOption.AllDirectories);
                    foreach (var file in currentFiles)
                    {
                        var relativePath = file.Substring(exeDir.Length).TrimStart('\\');
                        if (!beforeFiles.Contains(relativePath))
                        {
                            try
                            {
                                File.Delete(file);
                                deletedFiles.Add(relativePath);
                            }
                            catch { }
                        }
                    }

                    // 删除空目录
                    DeleteEmptyDirectories(exeDir);
                    usedSnapshot = true;
                }
            }

            // 方法2：通过已知的文件列表删除（后备方案，快照不存在或补充删除）
            if (!usedSnapshot || deletedFiles.Count == 0)
            {
                var dx9Files = new[]
                {
                    // dgVoodoo2
                    "D3D9.dll", "d3d9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe",
                    // ReShade
                    "dxgi.dll", "ReShade.ini", "ReShade.log", "ReShadePreset.ini",
                    // DLSS5-Feeder
                    "dlss5-feed.addon32", "dlss5-feed.addon64", "dlss5-feed-host64.exe",
                    // NGX运行时
                    "nvngx_dlssnr.dll", "nvngx_dlss.dll",
                    // RenoDX插件
                    "renodx-dlss5.addon64",
                    // 字体
                    "Fonts"
                };

                foreach (var file in dx9Files)
                {
                    var path = Path.Combine(exeDir, file);
                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                            deletedFiles.Add(file);
                        }
                        catch { }
                    }
                    else if (Directory.Exists(path))
                    {
                        try
                        {
                            Directory.Delete(path, true);
                            deletedFiles.Add(file + "\\");
                        }
                        catch { }
                    }
                }

                // 删除host64目录
                var host64Dir = Path.Combine(exeDir, "host64");
                if (Directory.Exists(host64Dir))
                {
                    try
                    {
                        Directory.Delete(host64Dir, true);
                        deletedFiles.Add("host64\\");
                    }
                    catch { }
                }

                // 删除reshade-shaders目录
                var reshadeDir = Path.Combine(exeDir, "reshade-shaders");
                if (Directory.Exists(reshadeDir))
                {
                    try
                    {
                        Directory.Delete(reshadeDir, true);
                        deletedFiles.Add("reshade-shaders\\");
                    }
                    catch { }
                }

                // 删除reshade_shaders目录（旧命名）
                var underscoreReshadeDir = Path.Combine(exeDir, "reshade_shaders");
                if (Directory.Exists(underscoreReshadeDir))
                {
                    try
                    {
                        Directory.Delete(underscoreReshadeDir, true);
                        deletedFiles.Add("reshade_shaders\\");
                    }
                    catch { }
                }

                // 删除空目录
                DeleteEmptyDirectories(exeDir);
            }

            // 删除备份目录
            if (Directory.Exists(dx9BackupDir))
            {
                try { Directory.Delete(dx9BackupDir, true); } catch { }
            }

            game.IsDX9DLSS5Patched = false;

            result.Success = true;
            result.Message = $"DX9 DLSS5补丁还原成功！\n\n" +
                           $"已删除 {deletedFiles.Count} 个文件：\n" +
                           string.Join("\n", deletedFiles.Take(20)) +
                           (deletedFiles.Count > 20 ? "\n..." : "") +
                           "\n\n所有DX9 DLSS5补丁文件已清理，游戏已恢复原状。";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"DX9 DLSS5补丁还原时发生错误：{ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// 查找游戏真正运行dll的目录
    /// 比如半条命2的bin目录，Source引擎游戏的渲染dll都在bin目录
    /// 支持Source引擎、虚幻引擎3、Unity引擎、以及其他常见引擎
    /// </summary>
    public static string? FindDllRuntimeDirectory(GameInfo game)
    {
        if (game == null || string.IsNullOrEmpty(game.ExePath)) return null;

        var exeDir = Path.GetDirectoryName(game.ExePath);
        if (string.IsNullOrEmpty(exeDir)) return null;

        // ========== Source引擎特殊处理（最高优先级） ==========
        // Source引擎游戏（如半条命2、CS等）的exe通常在游戏根目录，dll运行目录是bin目录
        // 特征：bin目录下有shaderapidx9.dll、materialsystem.dll、engine.dll等
        var sourceBinIndicators = new[]
        {
            "shaderapidx9.dll", "materialsystem.dll", "engine.dll", "vstdlib.dll",
            "tier0.dll", "vphysics.dll", "datacache.dll", "studiorender.dll",
            "vguimatsurface.dll", "vgui2.dll", "soundemittersystem.dll"
        };

        // 检查exe目录下的bin目录
        var sourceBinDir = Path.Combine(exeDir, "bin");
        if (Directory.Exists(sourceBinDir))
        {
            int sourceCount = 0;
            foreach (var indicator in sourceBinIndicators)
            {
                if (File.Exists(Path.Combine(sourceBinDir, indicator)))
                    sourceCount++;
            }
            // 如果bin目录下有至少3个Source引擎特征文件，判定为Source引擎，直接返回bin目录
            if (sourceCount >= 3)
            {
                return sourceBinDir;
            }
        }

        // 检查上级目录下的bin目录（exe可能在子目录中）
        var sourceParentDir = Directory.GetParent(exeDir);
        if (sourceParentDir != null)
        {
            var parentBinDir = Path.Combine(sourceParentDir.FullName, "bin");
            if (Directory.Exists(parentBinDir))
            {
                int sourceCount = 0;
                foreach (var indicator in sourceBinIndicators)
                {
                    if (File.Exists(Path.Combine(parentBinDir, indicator)))
                        sourceCount++;
                }
                if (sourceCount >= 3)
                {
                    return parentBinDir;
                }
            }
        }

        // ========== 扩展的dll运行目录特征文件 ==========
        // Source引擎特征
        var sourceIndicators = new[]
        {
            "shaderapidx9.dll", "materialsystem.dll", "engine.dll", "vstdlib.dll",
            "tier0.dll", "vphysics.dll", "datacache.dll", "server.dll", "client.dll",
            "studiorender.dll", "vguimatsurface.dll", "vgui2.dll", "soundemittersystem.dll"
        };

        // 虚幻引擎3特征
        var ue3Indicators = new[]
        {
            "d3d9.dll", "d3d9_*.dll", "d3dx9_*.dll", "D3DCompiler_*.dll",
            "binkw32.dll", "binkw64.dll", "bink2w32.dll", "bink2w64.dll",
            "nvcuda.dll", "nvtt.dll", "PhysXLoader.dll", "PhysXDevice.dll"
        };

        // Unity引擎及其他通用特征
        var unityIndicators = new[]
        {
            "d3d9.dll", "d3d11.dll", "dxgi.dll", "opengl32.dll",
            "UnityPlayer.dll", "mono.dll", "mono-2.0-bdwgc.dll"
        };

        // 合并所有特征文件（去重）
        var allIndicators = sourceIndicators
            .Concat(ue3Indicators)
            .Concat(unityIndicators)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // 需要跳过的目录（引擎目录、运行库目录、安装程序目录等）
        var skipDirs = new[] { "engine", "runtime", "redist", "_commonredist",
            "directx", "dotnet", "vcredist", "physx", "easyanticheat", "battleye",
            "support", "tools", "tool", "sdk", "debug", "release", "test",
            "nvidia", "amd", "driver", "drivers", "microsoft", "windows", "system",
            "installer", "installers", "patch", "updater", "update", "crashhandler",
            "unitycrashhandler", "crash", "report", "config", "configuration", "settings",
            "save", "saves", "savegames", "logs", "log", "cache", "caches", "temp",
            "backup", "backups", "download", "downloads", "movies", "movie", "cinematic",
            "videos", "video", "audio", "sound", "music", "sfx", "voice", "speech",
            "textures", "texture", "models", "model", "maps", "map", "levels", "level",
            "scripts", "script", "shaders", "shader", "fonts", "font", "ui", "gui",
            "localization", "language", "languages", "dlc", "mods", "mod", "plugins",
            "plugin", "extensions", "extension", "addons", "addon", "modules", "module"
        };

        // 辅助函数：检查目录是否包含特征文件
        bool HasIndicatorFiles(string dir, string[] indicators)
        {
            try
            {
                if (!Directory.Exists(dir)) return false;
                foreach (var pattern in indicators)
                {
                    if (Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly).Length > 0)
                        return true;
                }
                return false;
            }
            catch { return false; }
        }

        // 辅助函数：检查目录名是否应该跳过
        bool ShouldSkipDir(string dirPath)
        {
            var dirName = Path.GetFileName(dirPath).ToLower();
            return skipDirs.Any(s => dirName.Contains(s));
        }

        // ========== 检测优先级 ==========

        // 1. 先检查exe目录本身是否有dll运行特征
        if (HasIndicatorFiles(exeDir, allIndicators))
        {
            return exeDir;
        }

        // 2. 检查常见的dll运行目录（按优先级排序）
        var commonDirNames = new[]
        {
            // Source引擎
            "bin",
            // 虚幻引擎3/4
            "Binaries\\Win32", "Binaries\\Win64", "Binaries\\Win32\\", "Binaries\\Win64\\",
            "Binaries", "binary", "binaries",
            // 其他常见命名
            "binx64", "bin_x64", "binx86", "bin_x86",
            "bin32", "bin_32", "bin64", "bin_64",
            "win32", "win64", "Win32", "Win64",
            "x86", "x64", "X86", "X64",
            "release", "Release", "debug", "Debug",
            "game", "Game", "games", "Games",
            "exe", "Exe", "EXE",
            "app", "App", "APP",
            "program", "Program", "Programs", "programs",
            "run", "Run", "runner", "Runner",
            "launch", "Launch", "launcher", "Launcher",
            "client", "Client", "server", "Server",
            "main", "Main", "root", "Root",
            "data", "Data", "content", "Content",
            "system", "System", "core", "Core",
            "engine", "Engine", "framework", "Framework",
            "runtime", "Runtime", "redist", "Redist",
            "directx", "DirectX", "dx", "DX",
            "opengl", "OpenGL", "gl", "GL",
            "vulkan", "Vulkan", "vk", "VK",
            "metal", "Metal",
            "d3d", "D3D", "d3d9", "D3D9", "d3d11", "D3D11", "d3d12", "D3D12",
            "dxgi", "DXGI",
            "physx", "PhysX", "physics", "Physics",
            "audio", "Audio", "sound", "Sound", "music", "Music",
            "video", "Video", "movie", "Movie", "cinematic", "Cinematic",
            "texture", "Texture", "textures", "Textures", "model", "Model", "models", "Models",
            "map", "Map", "maps", "Maps", "level", "Level", "levels", "Levels",
            "script", "Script", "scripts", "Scripts", "shader", "Shader", "shaders", "Shaders",
            "font", "Font", "fonts", "Fonts", "ui", "UI", "gui", "GUI",
            "localization", "Localization", "language", "Language", "languages", "Languages",
            "dlc", "DLC", "mod", "Mod", "mods", "Mods", "plugin", "Plugin", "plugins", "Plugins",
            "extension", "Extension", "extensions", "Extensions", "addon", "Addon", "addons", "Addons",
            "module", "Module", "modules", "Modules",
            "save", "Save", "saves", "Saves", "savegames", "SaveGames",
            "log", "Log", "logs", "Logs", "cache", "Cache", "caches", "Caches", "temp", "Temp",
            "backup", "Backup", "backups", "Backups", "download", "Download", "downloads", "Downloads",
            "support", "Support", "tool", "Tool", "tools", "Tools", "sdk", "SDK",
            "debug", "Debug", "release", "Release", "test", "Test",
            "nvidia", "NVIDIA", "amd", "AMD", "driver", "Driver", "drivers", "Drivers",
            "microsoft", "Microsoft", "windows", "Windows", "system", "System",
            "installer", "Installer", "installers", "Installers", "patch", "Patch",
            "updater", "Updater", "update", "Update", "crashhandler", "CrashHandler",
            "unitycrashhandler", "UnityCrashHandler", "crash", "Crash", "report", "Report",
            "config", "Config", "configuration", "Configuration", "settings", "Settings"
        };

        // 2a. 检查exe目录下的常见子目录
        foreach (var dirName in commonDirNames)
        {
            var candidateDir = Path.Combine(exeDir, dirName);
            if (Directory.Exists(candidateDir) && !ShouldSkipDir(candidateDir))
            {
                if (HasIndicatorFiles(candidateDir, allIndicators))
                {
                    return candidateDir;
                }
            }
        }

        // 2b. 检查上级目录（游戏根目录）下的常见子目录
        var parentDir = Directory.GetParent(exeDir);
        if (parentDir != null)
        {
            foreach (var dirName in commonDirNames)
            {
                var candidateDir = Path.Combine(parentDir.FullName, dirName);
                if (Directory.Exists(candidateDir) && !ShouldSkipDir(candidateDir))
                {
                    if (HasIndicatorFiles(candidateDir, allIndicators))
                    {
                        return candidateDir;
                    }
                }
            }
        }

        // 2c. 检查上上级目录下的常见子目录
        var grandParentDir = parentDir?.Parent;
        if (grandParentDir != null)
        {
            foreach (var dirName in commonDirNames)
            {
                var candidateDir = Path.Combine(grandParentDir.FullName, dirName);
                if (Directory.Exists(candidateDir) && !ShouldSkipDir(candidateDir))
                {
                    if (HasIndicatorFiles(candidateDir, allIndicators))
                    {
                        return candidateDir;
                    }
                }
            }
        }

        // 3. 递归搜索exe目录下3层深度的所有目录，找包含特征文件的目录
        try
        {
            var allSubDirs = Directory.GetDirectories(exeDir, "*", SearchOption.AllDirectories)
                .Where(d => d.Count(c => c == Path.DirectorySeparatorChar) -
                           exeDir.Count(c => c == Path.DirectorySeparatorChar) <= 3)
                .Where(d => !ShouldSkipDir(d))
                .ToList();

            // 按目录深度排序（浅层优先）
            allSubDirs.Sort((a, b) =>
                a.Count(c => c == Path.DirectorySeparatorChar)
                    .CompareTo(b.Count(c => c == Path.DirectorySeparatorChar)));

            foreach (var dir in allSubDirs)
            {
                if (HasIndicatorFiles(dir, allIndicators))
                {
                    return dir;
                }
            }
        }
        catch { }

        // 4. 递归搜索上级目录下3层深度的所有目录
        if (parentDir != null)
        {
            try
            {
                var allSubDirs = Directory.GetDirectories(parentDir.FullName, "*", SearchOption.AllDirectories)
                    .Where(d => d.Count(c => c == Path.DirectorySeparatorChar) -
                               parentDir.FullName.Count(c => c == Path.DirectorySeparatorChar) <= 3)
                    .Where(d => !ShouldSkipDir(d))
                    .ToList();

                allSubDirs.Sort((a, b) =>
                    a.Count(c => c == Path.DirectorySeparatorChar)
                        .CompareTo(b.Count(c => c == Path.DirectorySeparatorChar)));

                foreach (var dir in allSubDirs)
                {
                    if (HasIndicatorFiles(dir, allIndicators))
                    {
                        return dir;
                    }
                }
            }
            catch { }
        }

        // 5. 如果都没找到，返回exe目录作为默认
        return exeDir;
    }

    /// <summary>
    /// 把dgvoodoo的三个文件从exe目录移动到dll运行目录
    /// 用于DX9游戏不生效时，让用户尝试把dgvoodoo放到真正运行dll的目录
    /// </summary>
    public PatchResult MoveDgVoodooToDllDirectory(GameInfo game)
    {
        var result = new PatchResult { GameName = game.Name };
        try
        {
            if (string.IsNullOrEmpty(game.ExePath) || !File.Exists(game.ExePath))
            {
                result.Success = false;
                result.Message = "未找到游戏exe文件。";
                return result;
            }

            var exeDir = Path.GetDirectoryName(game.ExePath)!;
            var dllDir = FindDllRuntimeDirectory(game);

            if (string.IsNullOrEmpty(dllDir) || dllDir.Equals(exeDir, StringComparison.OrdinalIgnoreCase))
            {
                result.Success = false;
                result.Message = "未找到独立的dll运行目录，dgvoodoo已在exe目录，无需移动。\n\n" +
                               "如果游戏仍然不生效，可能是其他原因导致的。";
                return result;
            }

            // dgvoodoo的三个文件
            var dgVoodooFiles = new[] { "D3D9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe" };
            var movedFiles = new List<string>();

            foreach (var fileName in dgVoodooFiles)
            {
                var srcPath = Path.Combine(exeDir, fileName);
                var dstPath = Path.Combine(dllDir, fileName);

                if (File.Exists(srcPath))
                {
                    // 如果目标目录已有同名文件，先备份
                    if (File.Exists(dstPath))
                    {
                        var backupPath = dstPath + ".bak";
                        File.Copy(dstPath, backupPath, true);
                    }

                    File.Move(srcPath, dstPath, true);
                    movedFiles.Add(fileName);
                }
            }

            if (movedFiles.Count == 0)
            {
                result.Success = false;
                result.Message = "在exe目录中未找到dgvoodoo的文件，可能已经移动过了或补丁未正确安装。";
                return result;
            }

            // 更新备份目录中的快照记录
            var dx9BackupDir = Path.Combine(GetBackupDirectory(game), "DX9DLSS5");
            if (Directory.Exists(dx9BackupDir))
            {
                // 记录移动操作
                var moveLog = Path.Combine(dx9BackupDir, "dgvoodoo_moved.txt");
                File.WriteAllText(moveLog, $"DgVoodoo files moved from:\n{exeDir}\nto:\n{dllDir}\n\nFiles moved:\n{string.Join("\n", movedFiles)}");
            }

            result.Success = true;
            result.Message = $"✅ dgvoodoo文件已成功移动到dll运行目录！\n\n" +
                           $"源目录：{exeDir}\n" +
                           $"目标目录：{dllDir}\n\n" +
                           $"已移动 {movedFiles.Count} 个文件：\n" +
                           string.Join("\n", movedFiles.Select(f => "  - " + f)) +
                           "\n\n现在请重新启动游戏测试一下，如果仍然不生效，请检查：\n" +
                           "1. 游戏是否以管理员身份运行\n" +
                           "2. dgVoodoo.conf配置是否正确\n" +
                           "3. 按Scroll Lock键是否显示dgVoodoo水印";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"移动dgvoodoo文件时发生错误：{ex.Message}";
            return result;
        }
    }

    #endregion
}

/// <summary>
/// 排错修复结果
/// </summary>
public class TroubleshootResult
{
    public string GameName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ReplacedFiles { get; set; } = new();
    public List<string> SkippedFiles { get; set; } = new();
}

/// <summary>
/// 补丁操作结果
/// </summary>
public class PatchResult
{
    public string GameName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
