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

    /// <summary>是否已打补丁（经典模式）</summary>
    public bool IsPatched { get; set; }

    /// <summary>是否已打高级模式补丁</summary>
    public bool IsAdvancedPatched { get; set; }

    /// <summary>是否已打2077专用补丁</summary>
    public bool IsCyberpunkPatched { get; set; }

    /// <summary>是否已打RE引擎多帧生成补丁</summary>
    public bool IsREFrameGenPatched { get; set; }

    /// <summary>是否为RE引擎游戏</summary>
    public bool IsREEngine { get; set; }

    /// <summary>多帧生成补丁模式：Classic / Advanced / RTX20 / RTX30 / REFrameGen / Cyberpunk</summary>
    public string PatchMode { get; set; } = "";

    /// <summary>是否已开启DLSS5</summary>
    public bool IsDLSS5Patched { get; set; }

    /// <summary>是否已开启DX9 DLSS5</summary>
    public bool IsDX9DLSS5Patched { get; set; }

    /// <summary>是否为DX9游戏（检测到DX9相关文件或exe引用d3d9.dll）</summary>
    public bool IsDX9Game { get; set; }

    /// <summary>是否支持DX11及以上（检测exe引用d3d11.dll/d3d12.dll/dxgi.dll，或目录有相关文件）</summary>
    public bool SupportsDX11OrAbove { get; set; }

    /// <summary>游戏exe是否为32位</summary>
    public bool Is32Bit { get; set; }

    /// <summary>DLSS5适用的显卡类型：Nvidia / AMD</summary>
    public string DLSS5GpuType { get; set; } = "";

    /// <summary>游戏真正运行的 exe 完整路径（兼容FilePatcher中的ExePath引用）</summary>
    public string? ExePath
    {
        get => GameExePath;
        set => GameExePath = value;
    }

    /// <summary>是否支持帧生成（即游戏目录中是否有 nvngx_dlssg.dll）</summary>
    public bool SupportsFrameGen { get; set; }

    /// <summary>补丁状态描述</summary>
    public string PatchStatus
    {
        get
        {
            // 判断是否开启了多帧生成（经典/高级/2077专属/RE引擎都算）
            bool hasFrameGen = IsPatched || IsAdvancedPatched || IsCyberpunkPatched || IsREFrameGenPatched;
            bool hasDLSS5 = IsDLSS5Patched || IsDX9DLSS5Patched;

            if (hasFrameGen && hasDLSS5)
                return "已开启（全部）";
            if (hasFrameGen)
            {
                if (IsCyberpunkPatched)
                    return "已开启（专属多帧生成）";
                return "已开启（多帧生成）";
            }
            if (hasDLSS5)
            {
                if (IsDX9DLSS5Patched)
                    return "已开启（DX9 DLSS5）";
                return "已开启（DLSS5）";
            }
            if (IsDX9Game && !SupportsDX11OrAbove && !SupportsFrameGen)
                return "仅支持DX9 DLSS5";
            if (IsDX9Game && SupportsDX11OrAbove && !SupportsFrameGen)
                return "支持DX9/通用DLSS5";
            if (!SupportsFrameGen)
                return "○ 仅支持DLSS5";
            return "未开启";
        }
    }

    public override string ToString() => $"[{Source}] {Name} - {PatchStatus}";
}
