using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using DLSSFrameGenEnabler_WinUI3.Services;
using System;
using System.IO;

namespace DLSSFrameGenEnabler_WinUI3;

public partial class App : Application
{
    public static Window MainWindow { get; private set; } = null!;

    private static void LogError(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n");
        }
        catch { }
    }

    public App()
    {
        try
        {
            // 注册全局异常处理，防止软件崩溃
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogError($"Unhandled Exception: {e.ExceptionObject}");
            };
            
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogError($"Unobserved Task Exception: {e.Exception}");
                e.SetObserved(); // 标记为已观察，防止进程崩溃
            };

            // 先设置全局主题，确保启动弹窗也能继承
            if (SettingsService.Instance.DarkMode)
            {
                Current.RequestedTheme = ApplicationTheme.Dark;
            }
            else
            {
                Current.RequestedTheme = ApplicationTheme.Light;
            }

            InitializeComponent();
            LogError("InitializeComponent OK");

            // 自包含模式下不需要手动初始化Bootstrap
            LogError("App constructor OK (self-contained, no bootstrap)");
        }
        catch (Exception ex)
        {
            LogError($"App constructor FAILED: {ex.Message}");
        }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            LogError("OnLaunched start");
            
            // XAML层面的全局异常处理
            this.UnhandledException += (s, e) =>
            {
                LogError($"XAML Unhandled Exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
                e.Handled = true; // 标记为已处理，防止进程崩溃
            };
            
            MainWindow = new MainWindow();
            LogError("MainWindow created");
            MainWindow.Activate();
            LogError("MainWindow activated");

            // 应用保存的主题
            ApplyTheme(SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light);
            // 应用材质背景
            if (MainWindow is MainWindow mw)
            {
                mw.ApplyBackdrop(SettingsService.Instance.BackdropType);
            }
            LogError("OnLaunched complete");
        }
        catch (Exception ex)
        {
            LogError($"OnLaunched FAILED: {ex.Message}\n{ex.StackTrace}");
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
}
