using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string root = AppContext.BaseDirectory;
        string[] candidates =
        {
            Path.Combine(root, "WebUI.exe"),
            Path.Combine(root, "DLSS5+MFG Enable", "WebUI.exe")
        };

        foreach (string candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;

            Process.Start(new ProcessStartInfo
            {
                FileName = candidate,
                WorkingDirectory = Path.GetDirectoryName(candidate) ?? root,
                UseShellExecute = true
            });
            return;
        }

        MessageBox.Show(
            "未找到 WebUI 稳定版主程序。请确认 DFGE.exe 与 WebUI.exe 位于同一目录，或 WebUI.exe 位于 DLSS5+MFG Enable 子目录。",
            "DFGE 启动器（WebUI 稳定版）",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
