using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebUI.Beta;

/// <summary>
/// 测试版增量更新：识别安装位置、校验增量包、启动独立安装器、解压覆盖。
/// 安装根目录是包含 DLSS5+MFG Enable 子目录的父目录，而不是 WebUI_Beta.exe 所在目录。
/// </summary>
internal static class UpdateInstallService
{
    internal const string InstallerArgument = "--update-install";
    internal const string InnerFolderName = "DLSS5+MFG Enable";

    internal static string DetectInstallRoot()
    {
        try
        {
            string baseDir = Path.GetFullPath(AppContext.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            DirectoryInfo info = new DirectoryInfo(baseDir);

            // 当前 exe 就在 DLSS5+MFG Enable 内：只有外层是 WebUI测试版/WebUI稳定版
            // 这类包装目录时，才把父目录作为安装根；单独解压到桌面的情况仍用自身目录。
            if (string.Equals(info.Name, InnerFolderName, StringComparison.OrdinalIgnoreCase))
            {
                if (info.Parent != null && IsWrapperDirectory(info.Parent.FullName))
                {
                    return info.Parent.FullName;
                }
                return baseDir;
            }

            // 当前 exe 已经位于安装根：根目录下直接能看到 DLSS5+MFG Enable。
            if (Directory.Exists(Path.Combine(baseDir, InnerFolderName)))
            {
                return baseDir;
            }

            // 某些启动方式会把工作目录放在子目录里，再向上一层寻找。
            if (info.Parent != null &&
                Directory.Exists(Path.Combine(info.Parent.FullName, InnerFolderName)))
            {
                return info.Parent.FullName;
            }

            return baseDir;
        }
        catch
        {
            return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static bool IsWrapperDirectory(string directory)
    {
        try
        {
            string name = Path.GetFileName(directory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            bool looksLikeWrapperName =
                name.StartsWith("WebUI", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("稳定版", StringComparison.Ordinal) ||
                name.Contains("测试版", StringComparison.Ordinal);
            bool hasLauncher =
                File.Exists(Path.Combine(directory, "DFGE.exe")) ||
                File.Exists(Path.Combine(directory, "Feedback.exe"));
            return looksLikeWrapperName || hasLauncher;
        }
        catch
        {
            return false;
        }
    }

    internal static string GetInnerDirectory(string installRoot)
    {
        // 便携版/测试发布目录本身可能就是程序目录（WebUI_Beta.exe + wwwroot），
        // 这时直接覆盖该目录；正常的 WebUI稳定版/WebUI测试版 包装目录才追加子目录。
        if (IsAppContentDirectory(installRoot) &&
            !Directory.Exists(Path.Combine(installRoot, InnerFolderName)))
        {
            return installRoot;
        }
        return Path.Combine(installRoot, InnerFolderName);
    }

    internal static bool IsAppContentDirectory(string directory)
    {
        return File.Exists(Path.Combine(directory, "WebUI_Beta.exe")) ||
               File.Exists(Path.Combine(directory, "WebUI.exe")) ||
               Directory.Exists(Path.Combine(directory, "wwwroot"));
    }

    internal static string GetPreferredAppExe(string installRoot, bool includeCurrentProcess = true)
    {
        string inner = GetInnerDirectory(installRoot);
        string beta = Path.Combine(inner, "WebUI_Beta.exe");
        if (File.Exists(beta))
        {
            return beta;
        }
        string stable = Path.Combine(inner, "WebUI.exe");
        if (File.Exists(stable))
        {
            return stable;
        }
        return includeCurrentProcess
            ? (Environment.ProcessPath ?? Application.ExecutablePath)
            : "";
    }

    internal static bool IsSupportedPackage(string zipPath, out string reason)
    {
        reason = "";
        try
        {
            if (!File.Exists(zipPath))
            {
                reason = "安装包不存在。";
                return false;
            }
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            List<InstallFileEntry> plan = BuildInstallPlan(archive);
            if (plan.Count == 0)
            {
                reason = "压缩包中没有找到 DLSS5+MFG Enable 文件。";
                return false;
            }
            bool hasPayload = plan.Any(e =>
                string.Equals(e.RelativePath, "WebUI_Beta.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.RelativePath, "WebUI.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.RelativePath.Replace('\\', '/'), "wwwroot/index.html", StringComparison.OrdinalIgnoreCase) ||
                e.RelativePath.Replace('\\', '/').StartsWith("Patches/", StringComparison.OrdinalIgnoreCase));
            if (!hasPayload)
            {
                reason = "压缩包里没有可覆盖的程序文件。";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            reason = "无法读取安装包：" + ex.Message;
            return false;
        }
    }

    internal static List<InstallFileEntry> BuildInstallPlan(ZipArchive archive)
    {
        List<InstallFileEntry> plan = new List<InstallFileEntry>();
        bool hasInner = archive.Entries.Any(e => !IsDirectoryEntry(e) && FindInnerRelativePath(e.FullName) != null);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (IsDirectoryEntry(entry))
            {
                continue;
            }

            string? relative = FindInnerRelativePath(entry.FullName);
            if (hasInner)
            {
                if (relative == null)
                {
                    // 全量包里同时有 DFGE.exe 等外层文件；本次只覆盖程序目录。
                    continue;
                }
            }
            else
            {
                relative = entry.FullName;
                if (!IsAllowedFlatAppFile(relative))
                {
                    continue;
                }
            }

            string? safe = SanitizeRelativePath(relative);
            if (safe == null)
            {
                continue;
            }
            plan.Add(new InstallFileEntry(entry, safe, Math.Max(0, entry.Length)));
        }

        return plan;
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry)
    {
        return string.IsNullOrEmpty(entry.Name) ||
               entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
               entry.FullName.EndsWith("\\", StringComparison.Ordinal);
    }

    private static string? FindInnerRelativePath(string fullName)
    {
        string normalized = fullName.Replace('\\', '/').TrimStart('/');
        string marker = InnerFolderName + "/";
        int index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }
        string relative = normalized.Substring(index + marker.Length);
        return relative.Length == 0 ? null : relative;
    }

    private static bool IsAllowedFlatAppFile(string relative)
    {
        string normalized = relative.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Patches/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("backup/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        string fileName = Path.GetFileName(normalized);
        return string.Equals(fileName, "WebUI_Beta.exe", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "WebUI.exe", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "WebUI_Beta.pdb", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "WebUI.pdb", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "app.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static string? SanitizeRelativePath(string relative)
    {
        string normalized = relative.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Contains(':'))
        {
            return null;
        }
        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(p => p == "." || p == ".."))
        {
            return null;
        }
        return string.Join(Path.DirectorySeparatorChar, parts);
    }

    internal static void ExtractPackage(
        string zipPath,
        string installRoot,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        string targetRoot = GetInnerDirectory(installRoot);
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        List<InstallFileEntry> plan = BuildInstallPlan(archive);
        if (plan.Count == 0)
        {
            throw new InvalidOperationException("压缩包中没有可安装的文件。");
        }

        long totalBytes = plan.Sum(e => e.Length);
        long processedBytes = 0;
        int filesDone = 0;
        Directory.CreateDirectory(targetRoot);

        foreach (InstallFileEntry item in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string destination = Path.GetFullPath(Path.Combine(targetRoot, item.RelativePath));
            if (!IsPathInside(destination, targetRoot))
            {
                throw new InvalidOperationException("安装包包含非法路径：" + item.RelativePath);
            }

            string? parent = Path.GetDirectoryName(destination);
            if (parent != null)
            {
                Directory.CreateDirectory(parent);
            }

            if (File.Exists(destination))
            {
                try
                {
                    File.SetAttributes(destination, FileAttributes.Normal);
                }
                catch
                {
                }
            }

            using FileStream output = OpenDestinationWithRetry(destination);
            using Stream input = item.Entry.Open();
            byte[] buffer = new byte[1024 * 1024];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Write(buffer, 0, read);
                processedBytes += read;
                if (progress != null)
                {
                    progress.Report(new InstallProgress
                    {
                        TotalBytes = totalBytes,
                        ProcessedBytes = processedBytes,
                        FilesDone = filesDone,
                        FilesTotal = plan.Count,
                        CurrentFile = item.RelativePath
                    });
                }
            }

            filesDone++;
            if (progress != null)
            {
                progress.Report(new InstallProgress
                {
                    TotalBytes = totalBytes,
                    ProcessedBytes = processedBytes,
                    FilesDone = filesDone,
                    FilesTotal = plan.Count,
                    CurrentFile = item.RelativePath
                });
            }
        }
    }

    private static FileStream OpenDestinationWithRetry(string destination)
    {
        Exception? lastError = null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                return new FileStream(
                    destination,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 1024,
                    FileOptions.SequentialScan);
            }
            catch (IOException ex)
            {
                lastError = ex;
                Thread.Sleep(400);
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
                Thread.Sleep(400);
            }
        }
        throw new IOException("无法覆盖文件：" + destination, lastError);
    }

    private static bool IsPathInside(string path, string root)
    {
        string fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<bool> WaitForMainProcessAsync(int processId, int timeoutMilliseconds)
    {
        if (processId <= 0)
        {
            return true;
        }
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return true;
            }
            using CancellationTokenSource timeout = new CancellationTokenSource(timeoutMilliseconds);
            await process.WaitForExitAsync(timeout.Token);
            return true;
        }
        catch (ArgumentException)
        {
            // 进程已经退出。
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return true;
        }
    }

    internal static Process LaunchInstaller(
        string zipPath,
        string installRoot,
        string appExePath,
        string version)
    {
        string sourceExe = Environment.ProcessPath ?? Application.ExecutablePath;
        string tempDir = Path.Combine(
            Path.GetTempPath(),
            "DFGE-Update-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        Directory.CreateDirectory(tempDir);
        string updaterExe = Path.Combine(tempDir, "DFGE-Update.exe");
        File.Copy(sourceExe, updaterExe, true);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = updaterExe,
            WorkingDirectory = tempDir,
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(InstallerArgument);
        startInfo.ArgumentList.Add(zipPath);
        startInfo.ArgumentList.Add(installRoot);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add(appExePath);
        startInfo.ArgumentList.Add(version);

        Process? process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("安装器进程没有成功启动。");
        }
        WriteLog("已启动安装器：" + updaterExe + "，安装根：" + installRoot + "，安装包：" + zipPath);
        return process;
    }

    internal static void ScheduleSelfCleanup(string tempDirectory, string exePath)
    {
        try
        {
            string script = Path.Combine(tempDirectory, "cleanup.cmd");
            string contents =
                "@echo off\r\n" +
                "chcp 65001 >nul\r\n" +
                "timeout /t 3 /nobreak >nul\r\n" +
                "del /f /q \"" + exePath + "\" >nul 2>nul\r\n" +
                "rd /s /q \"" + tempDirectory + "\" >nul 2>nul\r\n";
            File.WriteAllText(script, contents, Encoding.Default);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + script + "\"",
                WorkingDirectory = Path.GetPathRoot(tempDirectory) ?? "C:\\",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(startInfo);
        }
        catch
        {
        }
    }

    internal static void WriteLog(string message)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DLSSFrameGenEnabler");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "update-install.log"),
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
        }
    }

    internal sealed class InstallFileEntry
    {
        internal InstallFileEntry(ZipArchiveEntry entry, string relativePath, long length)
        {
            Entry = entry;
            RelativePath = relativePath;
            Length = length;
        }

        internal ZipArchiveEntry Entry { get; }
        internal string RelativePath { get; }
        internal long Length { get; }
    }
}

internal sealed class InstallProgress
{
    internal long TotalBytes { get; set; }
    internal long ProcessedBytes { get; set; }
    internal int FilesDone { get; set; }
    internal int FilesTotal { get; set; }
    internal string CurrentFile { get; set; } = "";

    internal int Percent
    {
        get
        {
            if (TotalBytes <= 0)
            {
                return FilesTotal > 0 ? (int)Math.Round(FilesDone * 100.0 / FilesTotal) : 0;
            }
            return (int)Math.Max(0, Math.Min(100, Math.Round(ProcessedBytes * 100.0 / TotalBytes)));
        }
    }
}
