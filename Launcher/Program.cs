using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Launcher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                // 获取当前exe所在目录
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // 软件路径
                string appPath = Path.Combine(currentDir, "DLSS5+MFG Enable", "DLSSFrameGenEnabler_WinUI3.exe");
                
                if (File.Exists(appPath))
                {
                    // 启动软件
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = appPath,
                        WorkingDirectory = Path.GetDirectoryName(appPath),
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show(
                        $"找不到软件文件：\n{appPath}\n\n请确保软件目录完整。",
                        "启动失败",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"启动失败：{ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
