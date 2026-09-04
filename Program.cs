namespace DLSSFrameGenEnabler;

static class Program
{
    /// <summary>
    /// 应用程序主入口
    /// </summary>
    [STAThread]
    static void Main()
    {
        // 高 DPI 支持
        ApplicationConfiguration.Initialize();

        // 全局异常处理
        Application.ThreadException += (sender, e) =>
        {
            MessageBox.Show(
                $"程序发生未处理的异常：\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "程序错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"程序发生致命错误：\n\n{ex?.Message ?? "未知错误"}",
                "致命错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        Application.Run(new MainForm());
    }
}
