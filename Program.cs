using System;
using System.Windows.Forms;
using DLSSFrameGenEnabler_WinUI3.Services;

namespace WebUI.Beta;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // 管理员模式下的一次性 Smooth Motion 写入：不启动 WebView、不创建主窗口。
        if (SmoothMotionElevated.TryHandle(args))
        {
            return;
        }

        MainForm.WriteStartupLog("=== WebUI 启动开始 ===");
        MainForm.WriteStartupLog("软件目录: " + AppContext.BaseDirectory);
        MainForm.WriteStartupLog("启动参数: " + (args.Length == 0 ? "(none)" : string.Join(" ", args)));

        ApplicationConfiguration.Initialize();
        bool snap = Array.Exists(args, a => string.Equals(a, "--snap", StringComparison.OrdinalIgnoreCase));
        bool scan = Array.Exists(args, a => string.Equals(a, "--scan", StringComparison.OrdinalIgnoreCase));
        bool demo = Array.Exists(args, a => string.Equals(a, "--demo-dialog", StringComparison.OrdinalIgnoreCase));
        bool demoSm86 = Array.Exists(args, a => string.Equals(a, "--demo-sm86", StringComparison.OrdinalIgnoreCase));
        bool demoDetect = Array.Exists(args, a => string.Equals(a, "--demo-detect", StringComparison.OrdinalIgnoreCase));
        bool demoDark = Array.Exists(args, a => string.Equals(a, "--dark", StringComparison.OrdinalIgnoreCase));
        bool demoEnglish = Array.Exists(args, a => string.Equals(a, "--english", StringComparison.OrdinalIgnoreCase));
        bool demoScrollEnd = Array.Exists(args, a => string.Equals(a, "--scrollend", StringComparison.OrdinalIgnoreCase));
        bool demoStartup = Array.Exists(args, a => string.Equals(a, "--demo-startup", StringComparison.OrdinalIgnoreCase));
        bool demoOn = Array.Exists(args, a => string.Equals(a, "--demo-on", StringComparison.OrdinalIgnoreCase));
        bool demoSelect = Array.Exists(args, a => string.Equals(a, "--demo-select", StringComparison.OrdinalIgnoreCase));
        bool demoBackup = Array.Exists(args, a => string.Equals(a, "--demo-backup", StringComparison.OrdinalIgnoreCase));
        bool demoCheck = Array.Exists(args, a => string.Equals(a, "--demo-check", StringComparison.OrdinalIgnoreCase));
        bool demoBeta = Array.Exists(args, a => string.Equals(a, "--demo-beta", StringComparison.OrdinalIgnoreCase));
        bool demoBig = Array.Exists(args, a => string.Equals(a, "--big", StringComparison.OrdinalIgnoreCase));
        string demoPage = "";
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--page", StringComparison.OrdinalIgnoreCase))
            {
                demoPage = args[i + 1];
            }
        }
        Application.Run(new MainForm(snap, scan, demo, demoSm86, demoPage, demoDetect, demoDark, demoEnglish, demoScrollEnd, demoStartup, demoOn, demoSelect, demoBackup, demoCheck, demoBeta, demoBig));
        MainForm.WriteStartupLog("Application.Run 退出");
    }
}
