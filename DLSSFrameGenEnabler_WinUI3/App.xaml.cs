using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using DLSSFrameGenEnabler_WinUI3.Services;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DLSSFrameGenEnabler_WinUI3;

public partial class App : Application
{
    public static Window MainWindow { get; private set; } = null!;

    // 版本号配置
    public const bool IsBeta = false; // 测试版标志，发布正式版时改为 false
    public const string StableVersion = "V1.14.1.0"; // 正式版版本号
    public const string BetaVersion = "V1.15.2.0-Beta"; // 测试版版本号
    public static string CurrentVersion => IsBeta ? BetaVersion : StableVersion;

    // Win32 MessageBox API
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;
    private const uint MB_ICONWARNING = 0x00000030;
    private const uint MB_ICONINFORMATION = 0x00000040;

    private static void LogError(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n");
        }
        catch { }
    }

    private static void LogStartup(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "startup.log");
            // 使用UTF-8编码，避免中文乱码
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n", System.Text.Encoding.UTF8);
        }
        catch { }
    }

    public App()
    {
        try
        {
            LogStartup("=== 软件启动开始 ===");
            LogStartup($"当前目录: {AppContext.BaseDirectory}");
            LogStartup($"操作系统: {Environment.OSVersion}");
            LogStartup($"64位操作系统: {Environment.Is64BitOperatingSystem}");
            LogStartup($"64位进程: {Environment.Is64BitProcess}");
            LogStartup($".NET版本: {Environment.Version}");
            LogStartup($"处理器数量: {Environment.ProcessorCount}");

            // 检查VC++运行时是否已安装
            LogStartup("检查VC++运行时...");
            if (!CheckVCRedist())
            {
                LogStartup("VC++运行时未安装，弹出提示");
                var msg = "本软件需要 Visual C++ Redistributable (x64) 才能运行。\n\n请运行软件目录下的 vc_redist.x64.exe 进行安装，或运行「启动软件.bat」自动检测并安装。\n\nThis software requires Visual C++ Redistributable (x64).\nPlease run vc_redist.x64.exe in the software folder, or run Launch.bat.";
                MessageBox(IntPtr.Zero, msg, "缺少运行时 / Missing Runtime", MB_OK | MB_ICONERROR);
                Environment.Exit(1);
                return;
            }
            LogStartup("VC++运行时检查通过");

            // 检查关键DLL是否存在
            LogStartup("检查关键DLL...");
            var criticalDlls = new[] { "coreclr.dll", "System.Private.CoreLib.dll", "Microsoft.ui.xaml.dll", "Microsoft.WindowsAppRuntime.dll", "hostpolicy.dll", "hostfxr.dll" };
            foreach (var dll in criticalDlls)
            {
                var dllPath = Path.Combine(AppContext.BaseDirectory, dll);
                if (File.Exists(dllPath))
                {
                    LogStartup($"  [OK] {dll}");
                }
                else
                {
                    LogStartup($"  [缺失] {dll}");
                }
            }

            // 注册全局异常处理，防止软件崩溃
            LogStartup("注册全局异常处理...");
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogError($"Unhandled Exception: {e.ExceptionObject}");
                LogStartup($"未处理异常: {e.ExceptionObject}");
            };
            
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogError($"Unobserved Task Exception: {e.Exception}");
                e.SetObserved(); // 标记为已观察，防止进程崩溃
            };
            LogStartup("全局异常处理注册完成");

            // 先设置全局主题，确保启动弹窗也能继承
            LogStartup("设置全局主题...");
            if (SettingsService.Instance.DarkMode)
            {
                Current.RequestedTheme = ApplicationTheme.Dark;
                LogStartup("主题: 深色");
            }
            else
            {
                Current.RequestedTheme = ApplicationTheme.Light;
                LogStartup("主题: 浅色");
            }

            LogStartup("调用InitializeComponent...");
            InitializeComponent();
            LogStartup("InitializeComponent OK");

            // 自包含模式下不需要手动初始化Bootstrap
            LogStartup("App constructor OK (self-contained, no bootstrap)");
        }
        catch (Exception ex)
        {
            LogError($"App constructor FAILED: {ex.Message}");
            LogStartup($"App构造函数失败: {ex.Message}");
            LogStartup($"堆栈跟踪: {ex.StackTrace}");
            try
            {
                MessageBox(IntPtr.Zero, $"软件启动失败：{ex.Message}\n\n请查看软件目录下的 startup.log 和 error.log 获取详细信息。\n\nStartup failed: {ex.Message}\nPlease check startup.log and error.log in the software folder.", "启动失败 / Startup Failed", MB_OK | MB_ICONERROR);
            }
            catch { }
        }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            LogStartup("OnLaunched start");
            
            // XAML层面的全局异常处理
            this.UnhandledException += (s, e) =>
            {
                LogError($"XAML Unhandled Exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
                LogStartup($"XAML未处理异常: {e.Exception.Message}");
                e.Handled = true; // 标记为已处理，防止进程崩溃
            };
            
            LogStartup("创建MainWindow...");
            MainWindow = new MainWindow();
            LogStartup("MainWindow created");
            LogStartup("激活MainWindow...");
            MainWindow.Activate();
            LogStartup("MainWindow activated");

            // 应用保存的主题
            LogStartup("应用主题...");
            ApplyTheme(SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light);
            // 应用材质背景
            LogStartup("应用材质背景...");
            if (MainWindow is MainWindow mw)
            {
                mw.ApplyBackdrop(SettingsService.Instance.BackdropType);
            }
            LogStartup("OnLaunched complete - 软件启动成功！");
        }
        catch (Exception ex)
        {
            LogError($"OnLaunched FAILED: {ex.Message}\n{ex.StackTrace}");
            LogStartup($"OnLaunched失败: {ex.Message}");
            LogStartup($"堆栈跟踪: {ex.StackTrace}");
            try
            {
                MessageBox(IntPtr.Zero, $"软件启动失败：{ex.Message}\n\n请查看软件目录下的 startup.log 和 error.log 获取详细信息。\n\nStartup failed: {ex.Message}\nPlease check startup.log and error.log in the software folder.", "启动失败 / Startup Failed", MB_OK | MB_ICONERROR);
            }
            catch { }
        }
    }

    public static void ApplyTheme(ElementTheme theme)
    {
        try
        {
            // 设置全局主题，确保所有弹窗都能继承
            if (theme == ElementTheme.Dark)
            {
                Current.RequestedTheme = ApplicationTheme.Dark;
            }
            else if (theme == ElementTheme.Light)
            {
                Current.RequestedTheme = ApplicationTheme.Light;
            }
        }
        catch { }

        try
        {
            // 同时设置窗口内容主题
            if (MainWindow?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }
        }
        catch { }
        // 注意：不再在这里调用ApplyBackdrop，避免和动画结束后的调用重复
        // 需要调用ApplyBackdrop的地方请显式调用
    }

    public static void ApplyAnimation(bool enable)
    {
        if (MainWindow is MainWindow mw)
        {
            mw.SetNavigationAnimation(enable);
        }
    }

    /// <summary>
    /// 检查Visual C++ Redistributable (x64) 是否已安装
    /// </summary>
    private static bool CheckVCRedist()
    {
        try
        {
            // 方法1：检查注册表
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
            if (key != null)
            {
                var installed = key.GetValue("Installed");
                if (installed != null && Convert.ToInt32(installed) == 1)
                {
                    return true;
                }
            }

            // 方法2：检查系统目录下的VC++运行时DLL
            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var vcDlls = new[] { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "msvcp140_1.dll", "msvcp140_2.dll" };
            foreach (var dll in vcDlls)
            {
                if (File.Exists(Path.Combine(systemDir, dll)))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // 如果检查出错，假设已安装（避免误报）
            return true;
        }
    }
}
