using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DLSSFrameGenEnabler_WinUI3.Models;

namespace DLSSFrameGenEnabler_WinUI3.Services;

/// <summary>
/// HSF（Hunter's Sloppy Frames）0.9.9 内置安装器。
/// HSF 是 ReShade 的 add-on，不是独立程序：本服务负责把 ReShade 6.8 add-on
/// 版和 HSF 的 addon / Streamline / HSF.fx / HSF.ini 写进游戏目录，并记录清单，
/// 还原时只动自己安装的文件，不覆盖游戏原有文件。
/// </summary>
public static class HsfService
{
    public const string HsfVersion = "0.9.9";
    public const int MenuLanguageFollowApp = 0;
    public const int MenuLanguageChinese = 1;
    public const int MenuLanguageEnglish = 2;
    public const string LaunchPadSourceUrl = "https://github.com/martymcmodding/iMMERSE";
    private const string LaunchPadArchiveUrl = "https://github.com/martymcmodding/iMMERSE/archive/refs/heads/main.zip";
    private const string LaunchPadTechnique = "MartysMods_Launchpad@MartysMods_LAUNCHPAD.fx";

    public static readonly string PatchRoot = Path.Combine(AppContext.BaseDirectory, "Patches", "HSF");
    private static readonly string OriginalAddonRoot = Path.Combine(PatchRoot, "Original");
    private static readonly string BackupBaseDir = Path.Combine(AppContext.BaseDirectory, "backup", "HSF");

    /// <summary>HSF 菜单语言设置：跟随软件 / 简体中文 / English。</summary>
    public static int GetMenuLanguageMode()
    {
        int mode = SettingsService.Instance.HsfMenuLanguage;
        return mode == MenuLanguageChinese || mode == MenuLanguageEnglish
            ? mode
            : MenuLanguageFollowApp;
    }

    /// <summary>根据设置和软件界面语言，决定是否使用原版英文 addon。</summary>
    public static bool UseEnglishMenu()
    {
        int mode = GetMenuLanguageMode();
        if (mode == MenuLanguageChinese) return false;
        if (mode == MenuLanguageEnglish) return true;
        return Translator.IsEnglish;
    }

    public static string DesiredMenuLanguage => UseEnglishMenu() ? "en-US" : "zh-CN";

    private static string T(string zh, string en)
    {
        return Translator.IsEnglish ? en : zh;
    }

    private static readonly string[] ProxyCandidates =
    {
        "dxgi.dll", "d3d9.dll", "d3d11.dll", "d3d12.dll", "opengl32.dll", "vulkan-1.dll"
    };

    private static readonly object PackageLock = new object();
    private static PackageInfo? _cachedPackage;

    public sealed class PackageInfo
    {
        public bool Ready { get; set; }
        public bool Addon64Ready { get; set; }
        public bool Addon32Ready { get; set; }
        public bool OriginalAddon64Ready { get; set; }
        public bool OriginalAddon32Ready { get; set; }
        public bool ReShade64Ready { get; set; }
        public bool ReShade32Ready { get; set; }
        public bool MfgUnlockerReady { get; set; }
        public long Addon64Size { get; set; }
        public long Addon32Size { get; set; }
        public long OriginalAddon64Size { get; set; }
        public long OriginalAddon32Size { get; set; }
        public long MfgUnlockerSize { get; set; }
        public string Addon64Sha256 { get; set; } = "";
        public string Addon32Sha256 { get; set; } = "";
        public string OriginalAddon64Sha256 { get; set; } = "";
        public string OriginalAddon32Sha256 { get; set; } = "";
        public string MfgUnlockerVersion { get; set; } = "";
        public int InstalledCount { get; set; }
        public string Error { get; set; } = "";
        public string Version => HsfVersion;
        public string Root => PatchRoot;
    }

    public sealed class GameStatus
    {
        public bool Installed { get; set; }
        public bool Partial { get; set; }
        public bool Conflict { get; set; }
        public bool CanInstall { get; set; }
        public bool Is64Bit { get; set; } = true;
        public string Arch { get; set; } = "64-bit";
        public string Api { get; set; } = "未知";
        public bool ReShadePresent { get; set; }
        public bool ReShadeReady { get; set; }
        public string ReShadeProxy { get; set; } = "";
        public string ReShadeVersion { get; set; } = "";
        public string ReShadeKind { get; set; } = "";
        public string Message { get; set; } = "";
        public string BackupPath { get; set; } = "";
        public string InstallDir { get; set; } = "";
        public string InstallMode { get; set; } = "auto";
        public bool IsSourceEngine { get; set; }
        public string HsfVersion { get; set; } = "";
        public string InstalledLanguage { get; set; } = "";
        public string DesiredLanguage { get; set; } = "";
        public bool LanguageMismatch { get; set; }
        public bool LaunchPadInstalled { get; set; }
        public bool LumeniteDetected { get; set; }
        public string FlowSourceName { get; set; } = "";
        public bool MfgUnlockerInstalled { get; set; }
        public bool CanInstallLaunchPad { get; set; }
        public bool CanInstallMfgUnlocker { get; set; }
    }

    public sealed class InstallResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Error { get; set; } = "";
    }

    public sealed class LaunchPadProgress
    {
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
    }

    private sealed class Manifest
    {
        public string ExePath { get; set; } = "";
        public string InstallDir { get; set; } = "";
        public string GameName { get; set; } = "";
        public string PackageVersion { get; set; } = HsfVersion;
        public string Arch { get; set; } = "";
        public string AddonLanguage { get; set; } = "";
        public bool ReShadeProxyAdded { get; set; }
        public bool ReShadeIniAdded { get; set; }
        public string ReShadeProxy { get; set; } = "";
        public List<ManifestEntry> Entries { get; set; } = new();
    }

    private sealed class ManifestEntry
    {
        public string RelativePath { get; set; } = "";
        public bool Directory { get; set; }
        public bool Added { get; set; }
        public string BackupRelativePath { get; set; } = "";
    }

    public static PackageInfo GetPackageInfo()
    {
        lock (PackageLock)
        {
            if (_cachedPackage != null)
            {
                _cachedPackage.InstalledCount = CountInstalledManifests();
                return _cachedPackage;
            }

            PackageInfo info = new PackageInfo();
            try
            {
                string addon64 = Path.Combine(PatchRoot, "hsf.addon64");
                string addon32 = Path.Combine(PatchRoot, "hsf.addon32");
                string originalAddon64 = Path.Combine(OriginalAddonRoot, "hsf.addon64");
                string originalAddon32 = Path.Combine(OriginalAddonRoot, "hsf.addon32");
                string streamline = Path.Combine(PatchRoot, "HSF_streamline", "nvngx_dlssg.dll");
                string fx = Path.Combine(PatchRoot, "reshade-shaders", "Shaders", "HSF.fx");
                string ini = Path.Combine(PatchRoot, "HSF.ini");
                string reshade64 = Path.Combine(PatchRoot, "ReShade", "dxgi_x64.dll");
                string reshade32 = Path.Combine(PatchRoot, "ReShade", "dxgi_x86.dll");
                string cjkFont = Path.Combine(PatchRoot, "Fonts", "MiSans-Bold.ttf");
                string mfgUnlocker = Path.Combine(PatchRoot, "MFG", "MFGUnlocker.dll");

                info.Addon64Ready = File.Exists(addon64);
                info.Addon32Ready = File.Exists(addon32);
                info.OriginalAddon64Ready = File.Exists(originalAddon64);
                info.OriginalAddon32Ready = File.Exists(originalAddon32);
                info.ReShade64Ready = File.Exists(reshade64);
                info.ReShade32Ready = File.Exists(reshade32);
                info.MfgUnlockerReady = File.Exists(mfgUnlocker);
                if (info.MfgUnlockerReady)
                {
                    FileInfo f = new FileInfo(mfgUnlocker);
                    info.MfgUnlockerSize = f.Length;
                    info.MfgUnlockerVersion = GetFileProductVersion(mfgUnlocker);
                }

                if (info.Addon64Ready)
                {
                    FileInfo f = new FileInfo(addon64);
                    info.Addon64Size = f.Length;
                    info.Addon64Sha256 = HashFile(addon64);
                }
                if (info.Addon32Ready)
                {
                    FileInfo f = new FileInfo(addon32);
                    info.Addon32Size = f.Length;
                    info.Addon32Sha256 = HashFile(addon32);
                }
                if (info.OriginalAddon64Ready)
                {
                    FileInfo f = new FileInfo(originalAddon64);
                    info.OriginalAddon64Size = f.Length;
                    info.OriginalAddon64Sha256 = HashFile(originalAddon64);
                }
                if (info.OriginalAddon32Ready)
                {
                    FileInfo f = new FileInfo(originalAddon32);
                    info.OriginalAddon32Size = f.Length;
                    info.OriginalAddon32Sha256 = HashFile(originalAddon32);
                }

                bool common = File.Exists(streamline) && File.Exists(fx) && File.Exists(ini)
                    && Directory.Exists(Path.Combine(PatchRoot, "HSF_streamline"))
                    && Directory.Exists(Path.Combine(PatchRoot, "licenses"));
                info.Ready = info.Addon64Ready && info.Addon32Ready
                    && info.OriginalAddon64Ready && info.OriginalAddon32Ready && common
                    && info.ReShade64Ready && info.ReShade32Ready && File.Exists(cjkFont);

                if (!info.Ready)
                {
                    List<string> missing = new List<string>();
                    if (!info.Addon64Ready) missing.Add("hsf.addon64");
                    if (!info.Addon32Ready) missing.Add("hsf.addon32");
                    if (!info.OriginalAddon64Ready) missing.Add("Original\\hsf.addon64");
                    if (!info.OriginalAddon32Ready) missing.Add("Original\\hsf.addon32");
                    if (!File.Exists(streamline)) missing.Add("HSF_streamline\\nvngx_dlssg.dll");
                    if (!File.Exists(fx)) missing.Add("reshade-shaders\\Shaders\\HSF.fx");
                    if (!File.Exists(ini)) missing.Add("HSF.ini");
                    if (!info.ReShade64Ready) missing.Add("ReShade\\dxgi_x64.dll");
                    if (!info.ReShade32Ready) missing.Add("ReShade\\dxgi_x86.dll");
                    if (!File.Exists(cjkFont)) missing.Add("Fonts\\MiSans-Bold.ttf");
                    info.Error = T("内置 HSF 文件不完整，缺少：", "The built-in HSF pack is incomplete; missing: ")
                        + string.Join(T("、", ", "), missing);
                }
            }
            catch (Exception ex)
            {
                info.Ready = false;
                info.Error = ex.Message;
            }

            info.InstalledCount = CountInstalledManifests();
            _cachedPackage = info;
            return info;
        }
    }

    private static string ResolveInstallDir(GameInfo game, string exeDir)
    {
        string root = string.IsNullOrWhiteSpace(game.GamePath) ? exeDir : game.GamePath;
        string mode = game.HsfInstallMode;
        string binDir = Path.Combine(root, "bin");
        if (!Directory.Exists(binDir))
        {
            string exeBin = Path.Combine(exeDir, "bin");
            if (Directory.Exists(exeBin)) binDir = exeBin;
        }

        if (mode == "bin")
            return Directory.Exists(binDir) ? binDir : exeDir;

        if (mode == "auto" && game.IsSourceEngine && Directory.Exists(binDir))
            return binDir;

        return exeDir;
    }

    public static GameStatus GetStatus(string exePath, GameInfo game)
    {
        GameStatus status = new GameStatus();
        PackageInfo package = GetPackageInfo();
        status.DesiredLanguage = DesiredMenuLanguage;
        if (!package.Ready)
        {
            status.Message = package.Error;
            return status;
        }
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            status.Message = T("找不到游戏可执行文件", "Game executable not found");
            return status;
        }

        string exeDir = Path.GetDirectoryName(exePath) ?? "";
        string installDir = ResolveInstallDir(game, exeDir);
        status.Is64Bit = Is64BitExe(exePath);
        status.Arch = status.Is64Bit ? "64-bit" : "32-bit";
        status.Api = DescribeApi(game);
        status.BackupPath = GetBackupDir(exePath);
        // 旧记录可能装在根目录。已有 install.json 时以记录里的目录为准，
        // 这样自动识别改成 bin 之后，旧安装仍能正常显示并还原。
        Manifest? existingManifest = ReadManifest(status.BackupPath);
        if (existingManifest != null && !string.IsNullOrWhiteSpace(existingManifest.InstallDir)
            && Directory.Exists(existingManifest.InstallDir))
        {
            installDir = existingManifest.InstallDir;
        }
        status.InstallDir = installDir;
        status.InstallMode = game.HsfInstallMode;
        status.IsSourceEngine = game.IsSourceEngine;

        string addonName = status.Is64Bit ? "hsf.addon64" : "hsf.addon32";
        string chineseHash = status.Is64Bit ? package.Addon64Sha256 : package.Addon32Sha256;
        string englishHash = status.Is64Bit ? package.OriginalAddon64Sha256 : package.OriginalAddon32Sha256;
        string addonPath = Path.Combine(installDir, addonName);
        string actualHash = File.Exists(addonPath) ? HashFile(addonPath) : "";
        bool ownAddon = !string.IsNullOrWhiteSpace(actualHash) &&
            (string.Equals(actualHash, chineseHash, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(actualHash, englishHash, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(actualHash))
        {
            if (string.Equals(actualHash, englishHash, StringComparison.OrdinalIgnoreCase))
                status.InstalledLanguage = "en-US";
            else if (string.Equals(actualHash, chineseHash, StringComparison.OrdinalIgnoreCase))
                status.InstalledLanguage = "zh-CN";
        }
        bool anyHsfAddon = File.Exists(Path.Combine(installDir, "hsf.addon64")) || File.Exists(Path.Combine(installDir, "hsf.addon32"));
        bool streamline = File.Exists(Path.Combine(installDir, "HSF_streamline", "nvngx_dlssg.dll"));
        bool fx = File.Exists(Path.Combine(installDir, "reshade-shaders", "Shaders", "HSF.fx"));
        bool ini = File.Exists(Path.Combine(installDir, "HSF.ini"));
        bool manifest = File.Exists(Path.Combine(status.BackupPath, "install.json"));

        status.Installed = ownAddon && streamline && fx && ini && manifest;
        status.Partial = (anyHsfAddon || manifest) && !status.Installed;
        status.HsfVersion = status.Installed ? HsfVersion : "";
        status.LanguageMismatch = status.Installed &&
            !string.Equals(status.InstalledLanguage, status.DesiredLanguage, StringComparison.OrdinalIgnoreCase);
        PopulateAuxStatus(status, installDir, package);

        ResolveReShade(status, installDir, status.Is64Bit);

        if (status.Conflict)
        {
            status.CanInstall = false;
            return status;
        }

        if (status.ReShadePresent && !status.ReShadeReady)
        {
            status.Conflict = true;
            status.CanInstall = false;
            status.Message = T("游戏目录已有 ReShade，但不是 6.8 add-on 版：" + status.ReShadeVersion,
                "The game folder already has ReShade, but it is not the 6.8 add-on build: " + status.ReShadeVersion);
            return status;
        }

        if (anyHsfAddon && !ownAddon)
        {
            status.Conflict = true;
            status.CanInstall = false;
            status.Message = T("游戏目录里已有另一版本的 HSF，请先手动清理再安装",
                "The game folder already contains a different HSF build. Remove it manually before installing.");
            return status;
        }

        if (status.Installed)
        {
            if (status.LanguageMismatch)
            {
                status.CanInstall = true;
                status.Message = status.DesiredLanguage == "en-US"
                    ? T("已安装中文版 HSF；重新安装会切换为英文原版。",
                        "The Chinese HSF menu is installed. Reinstall to switch to the original English build.")
                    : T("已安装英文原版 HSF；重新安装会切换为简体中文版。",
                        "The original English HSF menu is installed. Reinstall to switch to Simplified Chinese.");
            }
            else
            {
                status.CanInstall = false;
                if (game.IsSourceEngine && !Path.GetFileName(installDir).Equals("bin", StringComparison.OrdinalIgnoreCase))
                {
                    status.Message = T("当前是旧版根目录安装；还原后重新安装会自动装到 bin 子目录。",
                        "This is an older root-folder install. Restore it, then reinstall; the app will automatically use the bin subfolder.");
                }
                else
                {
                    status.Message = T("HSF " + HsfVersion + " 已安装",
                        "HSF " + HsfVersion + " is installed");
                }
            }
            return status;
        }

        if (status.Api.Contains("Vulkan") && !status.ReShadePresent)
        {
            status.CanInstall = false;
            status.Message = T("Vulkan 游戏需要先用 ReShade 安装器装 6.8 add-on 版；本软件不自动改 Vulkan 全局层",
                "Vulkan games need ReShade 6.8 add-on installed first; this app does not modify the global Vulkan layer.");
            return status;
        }

        status.CanInstall = true;
        if (status.Partial)
        {
            status.Message = T("检测到未完成的 HSF 文件，可重新安装补齐",
                "Incomplete HSF files detected; reinstall to fill in the missing files.");
        }
        else if (status.ReShadePresent)
        {
            status.Message = T("将复用游戏目录已有的 ReShade 6.8 add-on",
                "The existing ReShade 6.8 add-on in the game folder will be reused.");
        }
        else
        {
            status.Message = T("将安装内置 ReShade 6.8 add-on + HSF",
                "The built-in ReShade 6.8 add-on and HSF will be installed.");
        }
        return status;
    }

    private static void PopulateAuxStatus(GameStatus status, string exeDir, PackageInfo package)
    {
        string launchPadFx = Path.Combine(exeDir, "reshade-shaders", "Shaders", "MartysMods_LAUNCHPAD.fx");
        string launchPadDir = Path.Combine(exeDir, "reshade-shaders", "Shaders", "MartysMods");
        string launchPadTexOpt = Path.Combine(exeDir, "reshade-shaders", "Textures", "iMMERSE_bluenoise_opt.png");
        string launchPadTexTemporal = Path.Combine(exeDir, "reshade-shaders", "Textures", "iMMERSE_bluenoise_temporal.png");
        status.LaunchPadInstalled = File.Exists(launchPadFx) && Directory.Exists(launchPadDir)
            && File.Exists(launchPadTexOpt) && File.Exists(launchPadTexTemporal);

        string lumeniteFx = Path.Combine(exeDir, "reshade-shaders", "Shaders", "lumenite_Kernel.fx");
        string lumeniteInclude = Path.Combine(exeDir, "reshade-shaders", "Shaders", "include");
        status.LumeniteDetected = File.Exists(lumeniteFx) && Directory.Exists(lumeniteInclude)
            && Directory.GetFiles(lumeniteInclude, "lumenite_*.fxh").Length > 0;

        if (status.LaunchPadInstalled) status.FlowSourceName = "LaunchPad";
        else if (status.LumeniteDetected) status.FlowSourceName = "LumeniteFX";

        status.MfgUnlockerInstalled = File.Exists(Path.Combine(exeDir, "HSF_streamline", "mods", "MFGUnlocker.dll"))
            || File.Exists(Path.Combine(exeDir, "MFGUnlocker.dll"));

        status.CanInstallLaunchPad = status.Installed && !status.LaunchPadInstalled;
        status.CanInstallMfgUnlocker = status.Installed && package.MfgUnlockerReady && !status.MfgUnlockerInstalled;
    }

    public static InstallResult Install(GameInfo game)
    {
        PackageInfo package = GetPackageInfo();
        if (!package.Ready)
        {
            return new InstallResult { Success = false, Error = package.Error };
        }
        if (string.IsNullOrWhiteSpace(game.ExePath) || !File.Exists(game.ExePath))
        {
            return new InstallResult { Success = false, Error = T("找不到游戏可执行文件", "Game executable not found") };
        }

        GameStatus status = GetStatus(game.ExePath, game);
        if (status.Installed && !status.LanguageMismatch)
        {
            return new InstallResult
            {
                Success = true,
                Message = T("HSF " + HsfVersion + " 已经是安装状态",
                    "HSF " + HsfVersion + " is already installed")
            };
        }
        if (status.Conflict)
        {
            return new InstallResult { Success = false, Error = status.Message };
        }
        if (!status.CanInstall)
        {
            return new InstallResult { Success = false, Error = status.Message };
        }

        string exeDir = Path.GetDirectoryName(game.ExePath) ?? "";
        string installDir = ResolveInstallDir(game, exeDir);
        string backupDir = status.BackupPath;
        bool englishMenu = UseEnglishMenu();

        // 已安装但语言不一致：先按清单还原，再按当前语言重新安装。
        if (status.Installed && status.LanguageMismatch)
        {
            if (!RestoreInternal(game.ExePath, out string restoreMessage))
            {
                return new InstallResult { Success = false, Error = restoreMessage };
            }
            status = GetStatus(game.ExePath, game);
            installDir = ResolveInstallDir(game, exeDir);
            backupDir = status.BackupPath;
            if (status.Conflict || (!status.CanInstall && !status.Installed))
            {
                return new InstallResult { Success = false, Error = status.Message };
            }
        }

        // 上次安装留下半套文件：先按记录还原，再重新安装。
        if (status.Partial && File.Exists(Path.Combine(backupDir, "install.json")))
        {
            RestoreInternal(game.ExePath, out _);
        }

        Manifest manifest = new Manifest
        {
            ExePath = game.ExePath,
            InstallDir = installDir,
            GameName = game.Name ?? "",
            PackageVersion = HsfVersion,
            Arch = status.Arch,
            AddonLanguage = englishMenu ? "en-US" : "zh-CN"
        };

        try
        {
            Directory.CreateDirectory(backupDir);
            string backupFilesDir = Path.Combine(backupDir, "backup");

            // 1) ReShade：已有就复用，没有就装内置 6.8 add-on 版。
            if (!status.ReShadePresent)
            {
                string proxyName = status.Api.Contains("DX9") ? "d3d9.dll" : "dxgi.dll";
                string sourceDll = status.Is64Bit
                    ? Path.Combine(PatchRoot, "ReShade", "dxgi_x64.dll")
                    : Path.Combine(PatchRoot, "ReShade", "dxgi_x86.dll");
                string targetDll = Path.Combine(installDir, proxyName);

                BackupIfExists(targetDll, backupFilesDir, installDir, manifest, false);
                File.Copy(sourceDll, targetDll, true);
                manifest.ReShadeProxyAdded = true;
                manifest.ReShadeProxy = proxyName;
                manifest.Entries.Add(new ManifestEntry
                {
                    RelativePath = proxyName,
                    Added = true,
                    BackupRelativePath = ""
                });

                string iniPath = Path.Combine(installDir, "ReShade.ini");
                if (!File.Exists(iniPath))
                {
                    WriteMinimalReShadeIni(iniPath, englishMenu);
                    manifest.ReShadeIniAdded = true;
                }
            }

            // 复用已有 ReShade 时也要同步外壳语言；中文界面才需要 MiSans 字体。
            string reshadeIniPath = Path.Combine(installDir, "ReShade.ini");
            if (File.Exists(reshadeIniPath))
            {
                EnsureReShadeMenuLanguage(reshadeIniPath, installDir, backupFilesDir, manifest, englishMenu);
            }
            if (!englishMenu)
            {
                CopyDirectoryWithBackup(
                    Path.Combine(PatchRoot, "Fonts"),
                    Path.Combine(installDir, "Fonts"),
                    installDir, backupFilesDir, manifest);
            }

            // 2) HSF addon（按游戏位数和菜单语言只放一个）
            string addonName = status.Is64Bit ? "hsf.addon64" : "hsf.addon32";
            string addonSource = Path.Combine(englishMenu ? OriginalAddonRoot : PatchRoot, addonName);
            CopyFileWithBackup(
                addonSource,
                Path.Combine(installDir, addonName),
                installDir, backupFilesDir, manifest);

            // 3) HSF_streamline
            CopyDirectoryWithBackup(
                Path.Combine(PatchRoot, "HSF_streamline"),
                Path.Combine(installDir, "HSF_streamline"),
                installDir, backupFilesDir, manifest);

            // 4) HSF.fx
            CopyFileWithBackup(
                Path.Combine(PatchRoot, "reshade-shaders", "Shaders", "HSF.fx"),
                Path.Combine(installDir, "reshade-shaders", "Shaders", "HSF.fx"),
                installDir, backupFilesDir, manifest);

            // 5) HSF.ini（官方说明要求覆盖旧文件，给一份干净配置）
            CopyFileWithBackup(
                Path.Combine(PatchRoot, "HSF.ini"),
                Path.Combine(installDir, "HSF.ini"),
                installDir, backupFilesDir, manifest);

            // 6) 许可证和说明：HSF 的安装条款要求保留这些文件。
            CopyFileWithBackup(
                Path.Combine(PatchRoot, "LICENSE.txt"),
                Path.Combine(installDir, "HSF-LICENSE.txt"),
                installDir, backupFilesDir, manifest);
            CopyDirectoryWithBackup(
                Path.Combine(PatchRoot, "licenses"),
                Path.Combine(installDir, "HSF-licenses"),
                installDir, backupFilesDir, manifest);
            CopyFileWithBackup(
                Path.Combine(PatchRoot, "README.txt"),
                Path.Combine(installDir, "HSF-README.txt"),
                installDir, backupFilesDir, manifest);
            if (!englishMenu)
            {
                CopyFileWithBackup(
                    Path.Combine(PatchRoot, "README-汉化说明.txt"),
                    Path.Combine(installDir, "HSF-汉化说明.txt"),
                    installDir, backupFilesDir, manifest);
            }
            if (File.Exists(Path.Combine(PatchRoot, "README-第三方组件.txt")))
            {
                CopyFileWithBackup(
                    Path.Combine(PatchRoot, "README-第三方组件.txt"),
                    Path.Combine(installDir, "HSF-第三方组件.txt"),
                    installDir, backupFilesDir, manifest);
            }

            File.WriteAllText(
                Path.Combine(backupDir, "install.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);

            return new InstallResult
            {
                Success = true,
                Message = T(
                    "HSF " + HsfVersion + " 安装完成（" + (englishMenu ? "英文原版菜单" : "简体中文菜单") +
                    "）。进游戏按 Home 打开 ReShade 菜单，在 Add-ons 里启用 HSF；完全退出游戏后再启动。",
                    "HSF " + HsfVersion + " installed (" + (englishMenu ? "original English menu" : "Simplified Chinese menu") +
                    "). Press Home in game to open ReShade and enable HSF under Add-ons; fully restart the game afterward.")
            };
        }
        catch (Exception ex)
        {
            // 安装中途失败：把已经写下的清单落盘，再按清单回滚，避免留下半套文件。
            try
            {
                string manifestPath = Path.Combine(backupDir, "install.json");
                Directory.CreateDirectory(backupDir);
                File.WriteAllText(
                    manifestPath,
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                    Encoding.UTF8);
                RestoreInternal(game.ExePath, out _);
            }
            catch
            {
            }
            return new InstallResult
            {
                Success = false,
                Error = T("HSF 安装失败：" + ex.Message, "HSF installation failed: " + ex.Message)
            };
        }
    }

    /// <summary>
    /// 从 iMMERSE 官方 GitHub 仓库下载 LaunchPad（不随本软件分发），
    /// 再按 HSF 的目录约定放入游戏的 reshade-shaders，供 Vulkan / DX11 / 无 DLSS 的 DX12 游戏作为光流源。
    /// </summary>
    public static async Task<InstallResult> InstallLaunchPadAsync(
        GameInfo game,
        IProgress<LaunchPadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        GameStatus status = GetStatus(game.ExePath, game);
        if (!status.Installed)
        {
            return new InstallResult
            {
                Success = false,
                Error = T("请先安装 HSF，再安装 LaunchPad 光流源。",
                    "Install HSF first, then install the LaunchPad optical-flow source.")
            };
        }
        if (status.LaunchPadInstalled)
        {
            return new InstallResult
            {
                Success = true,
                Message = T("LaunchPad 光流源已经安装。", "The LaunchPad optical-flow source is already installed.")
            };
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "DFGE-LaunchPad-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            string zipPath = Path.Combine(tempRoot, "iMMERSE.zip");
            using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DFGE-HSF/1.0");
                using HttpResponseMessage response = await client.GetAsync(
                    LaunchPadArchiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                long totalBytes = response.Content.Headers.ContentLength ?? 0;
                if (response.Content.Headers.ContentRange?.Length is long rangeLength && rangeLength > 0)
                {
                    totalBytes = rangeLength;
                }

                progress?.Report(new LaunchPadProgress { DownloadedBytes = 0, TotalBytes = totalBytes });

                using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using FileStream output = File.Create(zipPath);
                byte[] buffer = new byte[81920];
                long downloadedBytes = 0;
                DateTime lastReport = DateTime.MinValue;
                while (true)
                {
                    int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read <= 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    downloadedBytes += read;
                    DateTime now = DateTime.UtcNow;
                    if ((now - lastReport).TotalMilliseconds >= 250)
                    {
                        lastReport = now;
                        progress?.Report(new LaunchPadProgress
                        {
                            DownloadedBytes = downloadedBytes,
                            TotalBytes = totalBytes
                        });
                    }
                }
                progress?.Report(new LaunchPadProgress
                {
                    DownloadedBytes = downloadedBytes,
                    TotalBytes = totalBytes > 0 ? totalBytes : downloadedBytes
                });
            }

            string staging = Path.Combine(tempRoot, "stage");
            Directory.CreateDirectory(staging);
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!TryGetLaunchPadRelativePath(entry.FullName, out string relative)) continue;
                    string target = Path.Combine(staging, relative.Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(target) ?? staging);
                    entry.ExtractToFile(target, true);
                }
            }

            string stagedFx = Path.Combine(staging, "Shaders", "MartysMods_LAUNCHPAD.fx");
            if (!File.Exists(stagedFx) || !File.Exists(Path.Combine(staging, "Textures", "iMMERSE_bluenoise_opt.png"))
                || !File.Exists(Path.Combine(staging, "Textures", "iMMERSE_bluenoise_temporal.png")))
            {
                return new InstallResult
                {
                    Success = false,
                    Error = T("下载到的 iMMERSE 压缩包缺少 LaunchPad 必要文件，请稍后重试。",
                        "The downloaded iMMERSE archive is missing required LaunchPad files. Try again later.")
                };
            }

            Manifest? manifest = ReadManifest(status.BackupPath);
            if (manifest == null)
            {
                return new InstallResult
                {
                    Success = false,
                    Error = T("读取 HSF 安装记录失败，无法记录 LaunchPad 文件。",
                        "Could not read the HSF install record; LaunchPad files cannot be tracked.")
                };
            }
            manifest.Entries ??= new List<ManifestEntry>();
            int startIndex = manifest.Entries.Count;
            string exeDir = Path.GetDirectoryName(game.ExePath) ?? "";
            string installDir = string.IsNullOrWhiteSpace(status.InstallDir)
                ? ResolveInstallDir(game, exeDir)
                : status.InstallDir;
            string backupFilesDir = Path.Combine(status.BackupPath, "backup");

            try
            {
                foreach (string file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(staging, file).Replace('\\', '/');
                    string target;
                    if (relative.Equals("LICENSE", StringComparison.OrdinalIgnoreCase))
                        target = Path.Combine(installDir, "HSF-iMMERSE-LICENSE.txt");
                    else if (relative.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                        target = Path.Combine(installDir, "HSF-iMMERSE-README.txt");
                    else if (relative.StartsWith("Shaders/", StringComparison.OrdinalIgnoreCase))
                        target = Path.Combine(installDir, "reshade-shaders", "Shaders",
                            relative.Substring("Shaders/".Length).Replace('/', '\\'));
                    else if (relative.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase))
                        target = Path.Combine(installDir, "reshade-shaders", "Textures",
                            relative.Substring("Textures/".Length).Replace('/', '\\'));
                    else
                        continue;

                    CopyFileWithBackup(file, target, installDir, backupFilesDir, manifest);
                }

                // HSF 要求 LaunchPad 排在效果链最前面：HSF_Motion 必须读到本帧的
                // MotionVectorsTex，排在 HSF 后面会让补帧使用上一帧的光流。
                bool orderApplied = EnsureLaunchPadPresetOrder(installDir, backupFilesDir, manifest);

                SaveManifest(status.BackupPath, manifest);

                return new InstallResult
                {
                    Success = true,
                    Message = orderApplied
                        ? T("LaunchPad 光流源安装完成，已把 iMMERSE LAUNCHPAD 放到 ReShade 效果链首位。请完全退出游戏后再启动；HSF 里把 MOTION 选为“光流”。",
                            "LaunchPad installed and moved to the top of the ReShade effect chain. Fully exit and restart the game; set MOTION to FLOW in HSF.")
                        : T("LaunchPad 光流源安装完成。没有找到 ReShadePreset.ini，请进游戏按 Home 打开 ReShade，把 iMMERSE LAUNCHPAD 拖到效果列表最顶部并启用，再完全退出游戏后启动。",
                            "LaunchPad installed, but ReShadePreset.ini was not found. In game, press Home, drag iMMERSE LAUNCHPAD to the top of the effect list, enable it, then fully restart the game.")
                };
            }
            catch
            {
                RollbackAddedEntries(manifest, startIndex, installDir, status.BackupPath);
                throw;
            }
        }
        catch (Exception ex)
        {
            return new InstallResult
            {
                Success = false,
                Error = T("LaunchPad 安装失败：" + ex.Message, "LaunchPad installation failed: " + ex.Message)
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// 把 MIT 许可的 MFG-Unlocker 放进 HSF helper 的 mods 目录。
    /// Vulkan / DX11 / 32 位游戏由 helper 加载；DX12 游戏的原生路线仍按 MFG-Unlocker 官方说明放置。
    /// </summary>
    public static InstallResult InstallMfgUnlocker(GameInfo game)
    {
        GameStatus status = GetStatus(game.ExePath, game);
        if (!status.Installed)
        {
            return new InstallResult
            {
                Success = false,
                Error = T("请先安装 HSF，再安装 MFG-Unlocker。",
                    "Install HSF first, then install MFG-Unlocker.")
            };
        }
        if (status.MfgUnlockerInstalled)
        {
            return new InstallResult
            {
                Success = true,
                Message = T("MFG-Unlocker 已经安装。", "MFG-Unlocker is already installed.")
            };
        }

        PackageInfo package = GetPackageInfo();
        if (!package.MfgUnlockerReady)
        {
            return new InstallResult
            {
                Success = false,
                Error = T("内置 MFG-Unlocker 文件缺失，请重新安装完整软件包。",
                    "The bundled MFG-Unlocker file is missing. Reinstall the full package.")
            };
        }

        string sourceDll = Path.Combine(PatchRoot, "MFG", "MFGUnlocker.dll");
        string sourceLicense = Path.Combine(PatchRoot, "MFG", "LICENSE");
        string sourceNotice = Path.Combine(PatchRoot, "MFG", "NOTICE.md");
        string exeDir = Path.GetDirectoryName(game.ExePath) ?? "";
        string installDir = string.IsNullOrWhiteSpace(status.InstallDir)
            ? ResolveInstallDir(game, exeDir)
            : status.InstallDir;
        string targetDll = Path.Combine(installDir, "HSF_streamline", "mods", "MFGUnlocker.dll");
        string backupFilesDir = Path.Combine(status.BackupPath, "backup");

        Manifest? manifest = ReadManifest(status.BackupPath);
        if (manifest == null)
        {
            return new InstallResult
            {
                Success = false,
                Error = T("读取 HSF 安装记录失败，无法记录 MFG-Unlocker 文件。",
                    "Could not read the HSF install record; MFG-Unlocker files cannot be tracked.")
            };
        }
        manifest.Entries ??= new List<ManifestEntry>();
        int startIndex = manifest.Entries.Count;

        try
        {
            CopyFileWithBackup(sourceDll, targetDll, installDir, backupFilesDir, manifest);
            if (File.Exists(sourceLicense))
                CopyFileWithBackup(sourceLicense, Path.Combine(installDir, "HSF-MFG-Unlocker-LICENSE.txt"),
                    installDir, backupFilesDir, manifest);
            if (File.Exists(sourceNotice))
                CopyFileWithBackup(sourceNotice, Path.Combine(installDir, "HSF-MFG-Unlocker-NOTICE.txt"),
                    installDir, backupFilesDir, manifest);
            SaveManifest(status.BackupPath, manifest);
        }
        catch (Exception ex)
        {
            RollbackAddedEntries(manifest, startIndex, installDir, status.BackupPath);
            return new InstallResult
            {
                Success = false,
                Error = T("MFG-Unlocker 安装失败：" + ex.Message, "MFG-Unlocker installation failed: " + ex.Message)
            };
        }

        return new InstallResult
        {
            Success = true,
            Message = T("MFG-Unlocker 已放入 HSF helper 的 mods 目录。Vulkan / DX11 游戏重启后生效；DX12 游戏仍需按官方说明改名放到游戏 exe 旁。",
                "MFG-Unlocker was placed in the HSF helper mods folder. Vulkan / DX11 games take effect after a restart; for DX12, still rename and place it next to the game exe as the official guide says.")
        };
    }

    /// <summary>
    /// 把 LaunchPad 放到 ReShade 预设的效果链最前面。
    /// HSF_Motion 通过 Deferred::MotionVectorsTex 读取本帧光流；如果 LaunchPad 排在
    /// HSF_Motion 后面，读到的会是上一帧的光流，2X 也会出现重复帧/不生效的观感。
    /// 返回 false 表示找不到 ReShadePreset.ini，或里面没有 Techniques 行。
    /// </summary>
    private static bool EnsureLaunchPadPresetOrder(string exeDir, string backupFilesDir, Manifest manifest)
    {
        string preset = Path.Combine(exeDir, "ReShadePreset.ini");
        if (!File.Exists(preset)) return false;

        string[] lines = File.ReadAllLines(preset);
        bool changed = false;
        bool foundTechniques = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (!line.StartsWith("Techniques=", StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith("TechniqueSorting=", StringComparison.OrdinalIgnoreCase))
                continue;

            int equals = line.IndexOf('=');
            if (equals < 0) continue;
            string prefix = line.Substring(0, equals + 1);
            string[] items = line.Substring(equals + 1)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            List<string> ordered = new List<string> { LaunchPadTechnique };
            ordered.AddRange(items.Where(item => !item.Equals(LaunchPadTechnique, StringComparison.OrdinalIgnoreCase)));
            string updated = prefix + string.Join(',', ordered);

            if (!updated.Equals(line, StringComparison.Ordinal))
            {
                lines[i] = updated;
                changed = true;
            }

            if (line.StartsWith("Techniques=", StringComparison.OrdinalIgnoreCase))
                foundTechniques = true;
        }

        if (!foundTechniques) return false;
        if (!changed) return true;

        string relative = Path.GetRelativePath(exeDir, preset);
        string backupRelative = Path.Combine("backup", relative);
        string backupPath = Path.Combine(Path.GetDirectoryName(backupFilesDir) ?? backupFilesDir, backupRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? backupFilesDir);
        File.Copy(preset, backupPath, true);
        File.WriteAllLines(preset, lines, new UTF8Encoding(false));

        manifest.Entries.Add(new ManifestEntry
        {
            RelativePath = relative,
            Added = false,
            BackupRelativePath = backupRelative
        });

        return true;
    }

    private static bool TryGetLaunchPadRelativePath(string entryName, out string relative)
    {
        relative = "";
        string name = entryName.Replace('\\', '/');
        int shadersAt = name.IndexOf("/Shaders/", StringComparison.Ordinal);
        if (shadersAt >= 0) name = name.Substring(shadersAt + 1);
        else if (name.StartsWith("Shaders/", StringComparison.Ordinal)) { }
        else
        {
            int texturesAt = name.IndexOf("/Textures/", StringComparison.Ordinal);
            if (texturesAt >= 0) name = name.Substring(texturesAt + 1);
        }

        if (name.Equals("Shaders/MartysMods_LAUNCHPAD.fx", StringComparison.OrdinalIgnoreCase)
            || (name.StartsWith("Shaders/MartysMods/", StringComparison.OrdinalIgnoreCase) && !name.EndsWith("/", StringComparison.Ordinal))
            || (name.StartsWith("Textures/iMMERSE_bluenoise", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            || name.EndsWith("/LICENSE", StringComparison.OrdinalIgnoreCase)
            || name.Equals("LICENSE", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase)
            || name.Equals("README.md", StringComparison.OrdinalIgnoreCase))
        {
            if (name.EndsWith("/LICENSE", StringComparison.OrdinalIgnoreCase)) name = "LICENSE";
            else if (name.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase)) name = "README.md";
            relative = name;
            return true;
        }

        return false;
    }

    private static Manifest? ReadManifest(string backupDir)
    {
        try
        {
            string path = Path.Combine(backupDir, "install.json");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<Manifest>(
                File.ReadAllText(path, Encoding.UTF8),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static void SaveManifest(string backupDir, Manifest manifest)
    {
        Directory.CreateDirectory(backupDir);
        File.WriteAllText(
            Path.Combine(backupDir, "install.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
    }

    private static void RollbackAddedEntries(Manifest manifest, int startIndex, string exeDir, string backupDir)
    {
        for (int i = manifest.Entries.Count - 1; i >= startIndex; i--)
        {
            ManifestEntry entry = manifest.Entries[i];
            string target = Path.Combine(exeDir, entry.RelativePath);
            string backup = string.IsNullOrWhiteSpace(entry.BackupRelativePath)
                ? ""
                : Path.Combine(backupDir, entry.BackupRelativePath);

            try
            {
                if (entry.Directory)
                {
                    if (Directory.Exists(backup))
                    {
                        if (Directory.Exists(target)) Directory.Delete(target, true);
                        CopyDirectory(backup, target);
                    }
                    else if (entry.Added && Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }
                }
                else
                {
                    if (File.Exists(backup))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? exeDir);
                        File.Copy(backup, target, true);
                    }
                    else if (entry.Added && File.Exists(target))
                    {
                        File.Delete(target);
                    }
                }
            }
            catch
            {
                // 回滚失败不掩盖原始错误；下一次还原仍会按清单继续处理。
            }
        }
        manifest.Entries.RemoveRange(startIndex, manifest.Entries.Count - startIndex);
    }

    public static InstallResult Restore(GameInfo game)
    {
        if (string.IsNullOrWhiteSpace(game.ExePath) || !File.Exists(game.ExePath))
        {
            return new InstallResult
            {
                Success = false,
                Error = T("找不到游戏可执行文件", "Game executable not found")
            };
        }
        return RestoreInternal(game.ExePath, out string message)
            ? new InstallResult { Success = true, Message = message }
            : new InstallResult { Success = false, Error = message };
    }

    private static bool RestoreInternal(string exePath, out string message)
    {
        string exeDir = Path.GetDirectoryName(exePath) ?? "";
        string backupDir = GetBackupDir(exePath);
        string manifestPath = Path.Combine(backupDir, "install.json");
        if (!File.Exists(manifestPath))
        {
            message = T("找不到 HSF 安装记录，未做任何改动。若文件是手动放入的，请自行删除。",
                "No HSF install record was found; nothing was changed. If the files were added manually, remove them yourself.");
            return false;
        }

        Manifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            message = T("读取 HSF 安装记录失败：" + ex.Message, "Could not read the HSF install record: " + ex.Message);
            return false;
        }
        if (manifest == null)
        {
            message = T("HSF 安装记录为空", "The HSF install record is empty");
            return false;
        }

        try
        {
            string installDir = !string.IsNullOrWhiteSpace(manifest.InstallDir)
                && Directory.Exists(manifest.InstallDir)
                ? manifest.InstallDir
                : exeDir;
            List<ManifestEntry> entries = manifest.Entries ?? new List<ManifestEntry>();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                ManifestEntry entry = entries[i];
                string target = Path.Combine(installDir, entry.RelativePath);
                string backup = string.IsNullOrWhiteSpace(entry.BackupRelativePath)
                    ? ""
                    : Path.Combine(backupDir, entry.BackupRelativePath);

                if (entry.Directory)
                {
                    if (Directory.Exists(backup))
                    {
                        if (Directory.Exists(target)) Directory.Delete(target, true);
                        CopyDirectory(backup, target);
                    }
                    else if (entry.Added && Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }
                }
                else
                {
                    if (File.Exists(backup))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? installDir);
                        File.Copy(backup, target, true);
                    }
                    else if (entry.Added && File.Exists(target))
                    {
                        File.Delete(target);
                    }
                }
            }

            // ReShade 是共享组件：只有确认目录里没有别的 add-on 时才移除 HSF 自己装的那一份。
            if (manifest.ReShadeProxyAdded && !string.IsNullOrWhiteSpace(manifest.ReShadeProxy))
            {
                bool otherAddons = Directory.GetFiles(installDir, "*.addon64", SearchOption.TopDirectoryOnly).Length > 0
                    || Directory.GetFiles(installDir, "*.addon32", SearchOption.TopDirectoryOnly).Length > 0;
                if (!otherAddons)
                {
                    string proxy = Path.Combine(installDir, manifest.ReShadeProxy);
                    if (File.Exists(proxy)) File.Delete(proxy);
                    if (manifest.ReShadeIniAdded)
                    {
                        string ini = Path.Combine(installDir, "ReShade.ini");
                        if (File.Exists(ini)) File.Delete(ini);
                    }
                }
            }

            try
            {
                Directory.Delete(backupDir, true);
            }
            catch
            {
                // 备份目录删不掉不影响功能，下次安装会覆盖清单。
            }

            message = T("HSF 已还原。" + (manifest.ReShadeProxyAdded ? " ReShade 共享组件按需保留。" : ""),
                "HSF restored." + (manifest.ReShadeProxyAdded ? " Shared ReShade components are kept when needed." : ""));
            return true;
        }
        catch (Exception ex)
        {
            message = T("还原失败：" + ex.Message, "Restore failed: " + ex.Message);
            return false;
        }
    }

    private static void CopyFileWithBackup(string source, string target, string exeDir, string backupFilesDir, Manifest manifest)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("缺少内置文件", source);
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? exeDir);
        string relative = Path.GetRelativePath(exeDir, target);
        bool existed = File.Exists(target);
        string backupRelative = "";
        if (existed)
        {
            backupRelative = Path.Combine("backup", relative);
            string backupPath = Path.Combine(Path.GetDirectoryName(backupFilesDir) ?? backupFilesDir, backupRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? backupFilesDir);
            File.Copy(target, backupPath, true);
        }
        File.Copy(source, target, true);
        manifest.Entries.Add(new ManifestEntry
        {
            RelativePath = relative,
            Added = !existed,
            BackupRelativePath = backupRelative
        });
    }

    private static void CopyDirectoryWithBackup(string sourceDir, string targetDir, string exeDir, string backupFilesDir, Manifest manifest)
    {
        if (!Directory.Exists(sourceDir)) throw new DirectoryNotFoundException("缺少内置目录：" + sourceDir);
        string relative = Path.GetRelativePath(exeDir, targetDir);
        bool existed = Directory.Exists(targetDir);
        string backupRelative = "";
        if (existed)
        {
            backupRelative = Path.Combine("backup", relative);
            string backupPath = Path.Combine(Path.GetDirectoryName(backupFilesDir) ?? backupFilesDir, backupRelative);
            if (Directory.Exists(backupPath)) Directory.Delete(backupPath, true);
            CopyDirectory(targetDir, backupPath);
        }
        Directory.CreateDirectory(targetDir);
        CopyDirectory(sourceDir, targetDir, overwrite: true);
        manifest.Entries.Add(new ManifestEntry
        {
            RelativePath = relative,
            Directory = true,
            Added = !existed,
            BackupRelativePath = backupRelative
        });
    }

    private static void BackupIfExists(string target, string backupFilesDir, string exeDir, Manifest manifest, bool directory)
    {
        if (!File.Exists(target)) return;
        string relative = Path.GetRelativePath(exeDir, target);
        string backupRelative = Path.Combine("backup", relative);
        string backupPath = Path.Combine(Path.GetDirectoryName(backupFilesDir) ?? backupFilesDir, backupRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? backupFilesDir);
        File.Copy(target, backupPath, true);
    }

    private static void CopyDirectory(string sourceDir, string targetDir, bool overwrite = true)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string dest = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? targetDir);
            File.Copy(file, dest, overwrite);
        }
    }

    private static void ResolveReShade(GameStatus status, string exeDir, bool is64Bit)
    {
        foreach (string name in ProxyCandidates)
        {
            string path = Path.Combine(exeDir, name);
            if (!File.Exists(path)) continue;

            string version = GetFileProductVersion(path);
            if (IsReShadeFile(path))
            {
                status.ReShadePresent = true;
                status.ReShadeProxy = name;
                status.ReShadeVersion = version;
                status.ReShadeKind = name + " · " + version;
                bool archMatches = GetPeArchitecture(path) == (is64Bit ? "x64" : "x86");
                bool versionOk = IsAtLeast68(version);
                bool addonFiles = Directory.GetFiles(exeDir, "*.addon64", SearchOption.TopDirectoryOnly).Length > 0
                    || Directory.GetFiles(exeDir, "*.addon32", SearchOption.TopDirectoryOnly).Length > 0;
                status.ReShadeReady = archMatches && (versionOk || addonFiles);
                if (!archMatches)
                {
                    status.Message = T("现有 ReShade 位数与游戏不一致：" + name,
                        "The existing ReShade architecture does not match the game: " + name);
                }
                return;
            }

            // 只把真正不是 ReShade 的渲染代理当成冲突；version.dll/winmm 等 SM86 代理可以共存。
            if (name == "dxgi.dll" || name == "d3d9.dll" || name == "d3d11.dll" || name == "d3d12.dll" || name == "vulkan-1.dll")
            {
                status.Conflict = true;
                status.Message = T(name + " 已被其他 MOD 占用，无法安全安装 ReShade/HSF",
                    name + " is already used by another mod; ReShade/HSF cannot be installed safely");
                return;
            }
        }
    }

    private static bool IsReShadeFile(string path)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            string product = info.ProductName ?? "";
            string desc = info.FileDescription ?? "";
            string original = info.OriginalFilename ?? "";
            return product.IndexOf("ReShade", StringComparison.OrdinalIgnoreCase) >= 0
                || desc.IndexOf("ReShade", StringComparison.OrdinalIgnoreCase) >= 0
                || original.IndexOf("ReShade", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetFileProductVersion(string path)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            return string.IsNullOrWhiteSpace(info.ProductVersion) ? (info.FileVersion ?? "") : info.ProductVersion;
        }
        catch
        {
            return "";
        }
    }

    private static bool IsAtLeast68(string version)
    {
        string[] parts = version.Split('.');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor)) return false;
        return major > 6 || (major == 6 && minor >= 8);
    }

    private static string DescribeApi(GameInfo game)
    {
        if (!string.IsNullOrWhiteSpace(game.RenderApi)) return game.RenderApi;
        if (game.IsDX9 && !game.SupportsDX11) return "DX9";
        if (game.SupportsDX11) return "DX11 / DX12";
        return "未知";
    }

    public static bool Is64BitExe(string exePath)
    {
        try
        {
            using FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using BinaryReader reader = new BinaryReader(fs);
            if (fs.Length < 0x40) return true;
            fs.Position = 0x3C;
            int peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset + 6 > fs.Length) return true;
            fs.Position = peOffset + 4;
            ushort machine = reader.ReadUInt16();
            return machine == 0x8664 || machine == 0xAA64;
        }
        catch
        {
            return true;
        }
    }

    private static string GetPeArchitecture(string path)
    {
        try
        {
            using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using BinaryReader reader = new BinaryReader(fs);
            if (fs.Length < 0x40) return "";
            fs.Position = 0x3C;
            int peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset + 6 > fs.Length) return "";
            fs.Position = peOffset + 4;
            ushort machine = reader.ReadUInt16();
            if (machine == 0x8664 || machine == 0xAA64) return "x64";
            if (machine == 0x14C) return "x86";
            return "";
        }
        catch
        {
            return "";
        }
    }

    private static string HashFile(string path)
    {
        try
        {
            using FileStream fs = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(fs));
        }
        catch
        {
            return "";
        }
    }

    private static string GetBackupDir(string exePath)
    {
        string key = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(exePath.Trim().ToLowerInvariant()))).Substring(0, 12);
        return Path.Combine(BackupBaseDir, key);
    }

    private static int CountInstalledManifests()
    {
        try
        {
            if (!Directory.Exists(BackupBaseDir)) return 0;
            return Directory.GetFiles(BackupBaseDir, "install.json", SearchOption.AllDirectories).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static void EnsureReShadeMenuLanguage(
        string iniPath, string exeDir, string backupFilesDir, Manifest manifest, bool englishMenu)
    {
        string targetLanguage = englishMenu ? "en-US" : "zh-CN";
        string text = File.ReadAllText(iniPath);
        bool languageOk = text.Contains("Language=" + targetLanguage, StringComparison.OrdinalIgnoreCase);
        bool fontOk = englishMenu ||
            (text.Contains("Font=.\\Fonts\\MiSans-Bold.ttf", StringComparison.OrdinalIgnoreCase)
             && text.Contains("EditorFont=.\\Fonts\\MiSans-Bold.ttf", StringComparison.OrdinalIgnoreCase)
             && text.Contains("LatinFont=.\\Fonts\\MiSans-Bold.ttf", StringComparison.OrdinalIgnoreCase));
        // 已经由本补丁设置过就不再改动，避免每次安装都新增一条备份记录。
        if (languageOk && fontOk)
        {
            return;
        }

        string relative = Path.GetRelativePath(exeDir, iniPath);
        string backupRelative = Path.Combine("backup", relative);
        string backupPath = Path.Combine(Path.GetDirectoryName(backupFilesDir) ?? backupFilesDir, backupRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? backupFilesDir);
        File.Copy(iniPath, backupPath, true);
        manifest.Entries.Add(new ManifestEntry
        {
            RelativePath = relative,
            Added = false,
            BackupRelativePath = backupRelative
        });

        text = SetIniValue(text, "OVERLAY", "Language", targetLanguage);
        if (!englishMenu)
        {
            text = SetIniValue(text, "STYLE", "Font", ".\\Fonts\\MiSans-Bold.ttf");
            text = SetIniValue(text, "STYLE", "EditorFont", ".\\Fonts\\MiSans-Bold.ttf");
            text = SetIniValue(text, "STYLE", "LatinFont", ".\\Fonts\\MiSans-Bold.ttf");
        }
        File.WriteAllText(iniPath, text, new UTF8Encoding(false));
    }

    private static string SetIniValue(string text, string section, string key, string value)
    {
        List<string> lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        int sectionAt = lines.FindIndex(line => line.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
        if (sectionAt < 0)
        {
            lines.Add("[" + section + "]");
            lines.Add(key + "=" + value);
            return string.Join("\r\n", lines);
        }

        int sectionEnd = lines.Count;
        for (int i = sectionAt + 1; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                sectionEnd = i;
                break;
            }
        }

        int keyAt = -1;
        for (int i = sectionAt + 1; i < sectionEnd; i++)
        {
            if (lines[i].TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                keyAt = i;
                break;
            }
        }

        if (keyAt >= 0) lines[keyAt] = key + "=" + value;
        else lines.Insert(sectionEnd, key + "=" + value);
        return string.Join("\r\n", lines);
    }

    private static void WriteMinimalReShadeIni(string path, bool englishMenu)
    {
        string style = englishMenu
            ? "FontSize=13.000000\r\n" +
              "EditorFontSize=13.000000\r\n" +
              "FontScale=1.000000\r\n"
            : "Font=.\\Fonts\\MiSans-Bold.ttf\r\n" +
              "FontSize=13.000000\r\n" +
              "EditorFont=.\\Fonts\\MiSans-Bold.ttf\r\n" +
              "EditorFontSize=13.000000\r\n" +
              "LatinFont=.\\Fonts\\MiSans-Bold.ttf\r\n" +
              "FontScale=1.000000\r\n";
        string text =
            "[GENERAL]\r\n" +
            "EffectSearchPaths=.\\reshade-shaders\\Shaders\\**\r\n" +
            "TextureSearchPaths=.\\reshade-shaders\\Textures\\**\r\n" +
            "PreprocessorDefinitions=RESHADE_DEPTH_LINEARIZATION_FAR_PLANE=1000.0,RESHADE_DEPTH_INPUT_IS_UPSIDE_DOWN=0,RESHADE_DEPTH_INPUT_IS_REVERSED=0,RESHADE_DEPTH_INPUT_IS_LOGARITHMIC=0\r\n" +
            "\r\n" +
            "[OVERLAY]\r\n" +
            "Language=" + (englishMenu ? "en-US" : "zh-CN") + "\r\n" +
            "\r\n" +
            "[STYLE]\r\n" +
            style;
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }
}
