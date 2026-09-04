using System.Reflection;
using DLSSFrameGenEnabler.Models;

namespace DLSSFrameGenEnabler.Services;

/// <summary>
/// 文件补丁服务：负责备份、替换、还原 DLSS 相关文件
/// 支持外部 Patches 文件夹优先，内置嵌入资源兜底（单文件独立运行）
/// </summary>
public class FilePatcher
{
    /// <summary>补丁文件所在目录（外部文件夹或临时提取目录）</summary>
    private readonly string _patchesDir;

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
        _patchesDir = ResolvePatchDirectory();
    }

    /// <summary>
    /// 解析补丁文件目录：优先外部 Patches 文件夹，不完整则从嵌入资源提取到临时目录
    /// </summary>
    private string ResolvePatchDirectory()
    {
        var externalDir = Path.Combine(AppContext.BaseDirectory, "Patches");

        // 检查外部文件夹是否完整
        if (CheckFilesExist(externalDir, AllPatchFiles))
        {
            UsingEmbeddedResources = false;
            return externalDir;
        }

        // 外部文件夹不完整，尝试从嵌入资源提取
        var tempDir = Path.Combine(Path.GetTempPath(), "DLSSFrameGenPatchFiles_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(tempDir);
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "DLSSFrameGenEnabler.Patches.";

            foreach (var fileName in AllPatchFiles)
            {
                var resourceName = resourcePrefix + fileName;
                var outputPath = Path.Combine(tempDir, fileName);

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    // 嵌入资源不存在，清理临时目录并返回外部目录（让 CheckPatchFiles 报告缺失）
                    try { Directory.Delete(tempDir, true); } catch { }
                    UsingEmbeddedResources = false;
                    return externalDir;
                }

                using var fileStream = File.Create(outputPath);
                stream.CopyTo(fileStream);
            }

            UsingEmbeddedResources = true;
            return tempDir;
        }
        catch
        {
            // 提取失败，返回外部目录
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            UsingEmbeddedResources = false;
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
    /// 检查补丁文件是否齐全
    /// </summary>
    public (bool Ok, string MissingFiles, bool UsingEmbedded) CheckPatchFiles()
    {
        var missing = new List<string>();

        foreach (var f in AllPatchFiles)
        {
            var path = Path.Combine(_patchesDir, f);
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
            var replacementDll = Path.Combine(_patchesDir, DlssgDllName);
            File.Copy(replacementDll, game.DlssgDllPath, true);

            // 3. 复制三个补丁文件到游戏 exe 目录
            progress?.Report("正在复制补丁文件到游戏目录...");
            foreach (var f in PatchFiles)
            {
                var src = Path.Combine(_patchesDir, f);
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

            // 1. 还原 nvngx_dlssg.dll
            progress?.Report("正在还原 nvngx_dlssg.dll...");
            var backupDll = Path.Combine(backupDir, DlssgDllName);
            if (File.Exists(backupDll) && !string.IsNullOrEmpty(game.DlssgDllPath))
            {
                File.Copy(backupDll, game.DlssgDllPath, true);
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

            // 3. 删除备份目录
            try
            {
                Directory.Delete(backupDir, true);
            }
            catch { }

            result.Success = true;
            result.Message = $"《{game.Name}》已成功还原到原始状态。";
            game.IsPatched = false;
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
        return Directory.Exists(backupDir) && File.Exists(Path.Combine(backupDir, DlssgDllName));
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

        var queue = new Queue<string>();
        queue.Enqueue(gameRootPath);
        var depth = 0;

        while (queue.Count > 0 && depth < 15)
        {
            var current = queue.Dequeue();
            try
            {
                foreach (var file in Directory.GetFiles(current, "sl.*.dll"))
                {
                    var fileName = Path.GetFileName(file);
                    if (TroubleshootFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    {
                        found.Add(file);
                    }
                }
                foreach (var dir in Directory.GetDirectories(current))
                {
                    var dirName = Path.GetFileName(dir).ToLowerInvariant();
                    if (dirName is not ("$recycle.bin" or "system volume information"))
                        queue.Enqueue(dir);
                }
            }
            catch { }
            depth++;
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
