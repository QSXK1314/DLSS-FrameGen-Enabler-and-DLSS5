using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DLSSFrameGenEnabler_WinUI3.Services;

/// <summary>
/// RTX 20/30 系 Smooth Motion 内置补丁的部署 / 还原。
///
/// 补丁随软件放在 Patches\SmoothMotion 下，不再从 zip 导入，也不在发布包里
/// 重新分发任何二进制。RTX 20 和 RTX 30 是两个不同引擎，按显卡系列选择。
/// </summary>
public static class SmoothMotionModService
{
    public const string EngineRtx20 = "RTX20";
    public const string EngineRtx30 = "RTX30";

    public static string PatchRoot => Path.Combine(AppContext.BaseDirectory, "Patches", "SmoothMotion");
    public static string Rtx20PayloadPath => Path.Combine(PatchRoot, "RTX20", "version.dll");
    public static string Rtx30PayloadPath => Path.Combine(PatchRoot, "RTX30", "version.dll");

    public sealed class ModPackageInfo
    {
        public bool Rtx20Ready;
        public bool Rtx30Ready;
        public string Rtx20Path = "";
        public string Rtx30Path = "";
        public long Rtx20Size;
        public long Rtx30Size;
        public string Rtx20Sha256 = "";
        public string Rtx30Sha256 = "";
        public string Error = "";

        public bool AnyReady => Rtx20Ready || Rtx30Ready;
        public bool Ready => Rtx20Ready && Rtx30Ready;
    }

    public sealed class ModGameStatus
    {
        public bool PackageReady;
        public bool Installed;
        public bool Conflict;
        public bool WrongEngine;
        public string Engine = "";
        public string ExpectedEngine = "";
        public string DllPath = "";
        public string DllSha256 = "";
        public string Message = "";
    }

    public static string EngineForGpu(string gpuSeries)
    {
        return gpuSeries switch
        {
            "RTX20" => EngineRtx20,
            "RTX30" => EngineRtx30,
            _ => ""
        };
    }

    public static string PayloadPathForEngine(string engine)
    {
        return engine == EngineRtx20 ? Rtx20PayloadPath :
               engine == EngineRtx30 ? Rtx30PayloadPath : "";
    }

    public static ModPackageInfo GetPackageInfo()
    {
        ModPackageInfo info = new ModPackageInfo();
        info.Rtx20Path = Rtx20PayloadPath;
        info.Rtx30Path = Rtx30PayloadPath;

        if (File.Exists(info.Rtx20Path))
        {
            info.Rtx20Ready = true;
            info.Rtx20Size = new FileInfo(info.Rtx20Path).Length;
            info.Rtx20Sha256 = ComputeSha256(info.Rtx20Path);
        }

        if (File.Exists(info.Rtx30Path))
        {
            info.Rtx30Ready = true;
            info.Rtx30Size = new FileInfo(info.Rtx30Path).Length;
            info.Rtx30Sha256 = ComputeSha256(info.Rtx30Path);
        }

        if (!info.Rtx20Ready && !info.Rtx30Ready)
        {
            info.Error = "内置补丁缺失：请确认 Patches\\SmoothMotion\\RTX20\\version.dll 和 RTX30\\version.dll 存在。";
        }
        else if (!info.Rtx20Ready)
        {
            info.Error = "内置补丁不完整：缺少 Patches\\SmoothMotion\\RTX20\\version.dll。";
        }
        else if (!info.Rtx30Ready)
        {
            info.Error = "内置补丁不完整：缺少 Patches\\SmoothMotion\\RTX30\\version.dll。";
        }

        return info;
    }

    public static ModGameStatus GetStatus(string exePath, string gpuSeries)
    {
        ModGameStatus status = new ModGameStatus();
        ModPackageInfo package = GetPackageInfo();
        status.PackageReady = package.AnyReady;
        status.ExpectedEngine = EngineForGpu(gpuSeries);

        string gameDir = Path.GetDirectoryName(exePath) ?? "";
        if (gameDir.Length == 0)
        {
            status.Message = "游戏路径无效";
            return status;
        }

        status.DllPath = Path.Combine(gameDir, "version.dll");
        if (!File.Exists(status.DllPath))
        {
            status.Message = package.AnyReady ? "未部署" : package.Error;
            return status;
        }

        status.DllSha256 = ComputeSha256(status.DllPath);
        string engine = IdentifyEngine(status.DllSha256, package);
        if (engine.Length == 0)
        {
            status.Conflict = true;
            status.Message = "游戏目录已有其他 version.dll，未改动";
            return status;
        }

        status.Installed = true;
        status.Engine = engine;
        if (status.ExpectedEngine.Length == 0)
        {
            status.WrongEngine = true;
            status.Message = gpuSeries is "RTX40" or "RTX50"
                ? "已部署 " + engine + " 引擎；当前显卡官方支持，不需要这个补丁，建议还原"
                : "已部署 " + engine + " 引擎；当前显卡不是 RTX 20/30";
        }
        else if (!string.Equals(engine, status.ExpectedEngine, StringComparison.Ordinal))
        {
            status.WrongEngine = true;
            status.Message = "已部署 " + engine + " 引擎，当前显卡需要 " + status.ExpectedEngine + " 引擎；请先还原再部署";
        }
        else
        {
            status.Message = "已部署 " + engine + " 引擎";
        }

        return status;
    }

    public static string Deploy(string exePath, string gpuSeries)
    {
        string engine = EngineForGpu(gpuSeries);
        if (engine.Length == 0)
        {
            if (gpuSeries is "RTX40" or "RTX50")
            {
                return "当前显卡是 " + gpuSeries + "，官方 Smooth Motion 已支持，不需要安装 RTX 20/30 补丁。";
            }
            return "当前显卡是 " + (string.IsNullOrWhiteSpace(gpuSeries) ? "未知" : gpuSeries) + "，不支持 RTX 20/30 补丁。";
        }

        string payloadPath = PayloadPathForEngine(engine);
        if (!File.Exists(payloadPath))
        {
            return "内置补丁缺失：" + payloadPath + "。请重新安装完整软件包。";
        }

        string gameDir = Path.GetDirectoryName(exePath) ?? "";
        if (gameDir.Length == 0 || !Directory.Exists(gameDir))
        {
            return "游戏目录不存在";
        }

        string dllPath = Path.Combine(gameDir, "version.dll");
        if (File.Exists(dllPath))
        {
            string existingHash = ComputeSha256(dllPath);
            string existingEngine = IdentifyEngine(existingHash, GetPackageInfo());
            if (existingEngine == engine)
            {
                return "这个游戏已经部署过 " + engine + " 引擎";
            }
            if (existingEngine.Length > 0)
            {
                return "这个游戏已经部署了 " + existingEngine + " 引擎；请先还原，再部署 " + engine + " 引擎。";
            }
            return "游戏目录已经有 version.dll（不是本补丁的文件）。为避免覆盖游戏自带的代理，已停止部署；请先手动备份或移除它。";
        }

        if (engine == EngineRtx30)
        {
            string msvcp = Path.Combine(gameDir, "msvcp140.dll");
            if (File.Exists(msvcp) && IsOldMsvcp(msvcp))
            {
                return "游戏目录里的 msvcp140.dll 低于 14.40，RTX30 引擎会拒绝加载。请先安装 Visual C++ 2015-2022 x64 运行库，或移除游戏目录里的旧 msvcp140.dll。";
            }
        }

        try
        {
            File.Copy(payloadPath, dllPath, false);
            string installedHash = ComputeSha256(dllPath);
            object meta = new
            {
                exe = exePath,
                engine = engine,
                gpuSeries = gpuSeries,
                installedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sha256 = installedHash,
                sourcePayload = payloadPath
            };
            File.WriteAllText(Path.Combine(gameDir, "SmoothMotionMod.install.json"),
                System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            return "Smooth Motion " + engine + " 补丁已部署到游戏目录";
        }
        catch (Exception ex)
        {
            return "部署失败：" + ex.Message;
        }
    }

    public static string Restore(string exePath)
    {
        string gameDir = Path.GetDirectoryName(exePath) ?? "";
        if (gameDir.Length == 0)
        {
            return "游戏路径无效";
        }

        string dllPath = Path.Combine(gameDir, "version.dll");
        if (!File.Exists(dllPath))
        {
            return "游戏目录没有 version.dll，已经是默认状态";
        }

        string existingHash = ComputeSha256(dllPath);
        string engine = IdentifyEngine(existingHash, GetPackageInfo());
        if (engine.Length == 0)
        {
            return "游戏目录的 version.dll 不是本补丁的文件，已停止还原，避免删掉游戏自带的代理。";
        }

        try
        {
            File.Delete(dllPath);
            string installMeta = Path.Combine(gameDir, "SmoothMotionMod.install.json");
            if (File.Exists(installMeta))
            {
                File.Delete(installMeta);
            }
            return "Smooth Motion " + engine + " 补丁已还原";
        }
        catch (Exception ex)
        {
            return "还原失败：" + ex.Message;
        }
    }

    private static string IdentifyEngine(string sha256, ModPackageInfo package)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            return "";
        }
        if (package.Rtx20Ready && string.Equals(sha256, package.Rtx20Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return EngineRtx20;
        }
        if (package.Rtx30Ready && string.Equals(sha256, package.Rtx30Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return EngineRtx30;
        }

        // 包信息可能尚未缓存；按文件再核对一次，避免误判为冲突。
        if (File.Exists(package.Rtx20Path) &&
            string.Equals(sha256, ComputeSha256(package.Rtx20Path), StringComparison.OrdinalIgnoreCase))
        {
            return EngineRtx20;
        }
        if (File.Exists(package.Rtx30Path) &&
            string.Equals(sha256, ComputeSha256(package.Rtx30Path), StringComparison.OrdinalIgnoreCase))
        {
            return EngineRtx30;
        }
        return "";
    }

    private static bool IsOldMsvcp(string path)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            string version = (info.FileVersion ?? "").Trim();
            if (version.Length == 0)
            {
                return false;
            }
            string[] parts = version.Split('.');
            if (parts.Length < 2)
            {
                return false;
            }
            if (!int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor))
            {
                return false;
            }
            return major < 14 || (major == 14 && minor < 40);
        }
        catch
        {
            return false;
        }
    }

    public static string ComputeSha256(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }
}
