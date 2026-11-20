namespace DLSSFrameGenEnabler_WinUI3;

/// <summary>稳定版的最小 App 占位：服务层读取 IsBeta 决定更新源，稳定版固定走正式通道。</summary>
public static class App
{
    public static bool IsBeta { get; set; } = false;
}
