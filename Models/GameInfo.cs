namespace DLSSFrameGenEnabler.Models;

/// <summary>
/// 游戏信息
/// </summary>
public class GameInfo
{
    /// <summary>游戏名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>游戏安装根目录</summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>nvngx_dlssg.dll 文件完整路径</summary>
    public string? DlssgDllPath { get; set; }

    /// <summary>游戏真正运行的 exe 完整路径（通常在 Binaries\Win64 下）</summary>
    public string? GameExePath { get; set; }

    /// <summary>游戏 exe 所在目录</summary>
    public string? GameExeDirectory => string.IsNullOrEmpty(GameExePath) ? null : Path.GetDirectoryName(GameExePath);

    /// <summary>来源平台：Steam / Epic / 手动选择</summary>
    public string Source { get; set; } = "未知";

    /// <summary>是否已打补丁</summary>
    public bool IsPatched { get; set; }

    /// <summary>补丁状态描述</summary>
    public string PatchStatus => IsPatched ? "已开启" : "未开启";

    public override string ToString() => $"[{Source}] {Name} - {PatchStatus}";
}
