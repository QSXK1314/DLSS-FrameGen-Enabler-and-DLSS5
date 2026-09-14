using DLSSFrameGenEnabler_WinUI3.Pages;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DLSSFrameGenEnabler_WinUI3
{
    public sealed partial class MainWindow : Window
    {
        // Win32 API for transparent window
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int LWA_ALPHA = 0x2;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;
        private const uint WM_ENTERSIZEMOVE = 0x0231;
        private const uint WM_EXITSIZEMOVE = 0x0232;
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private IntPtr _oldWndProc;
        private WndProcDelegate _wndProcDelegate;
        
        private struct WindowCompositionAttributeData {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }
        
        private struct AccentPolicy {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }
        
        private const int WCA_ACCENT_POLICY = 19;
        private const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;
        
        private IntPtr _windowHandle;
        private bool _isLayered = false;
        private bool _startupShown = false;
        private bool _updateChecked = false;
        private string _currentTag = "home";
        private bool _isNavigating = false;
        private bool _backdropApplied = false;
        // 拖动优化
        private bool _isDragging = false;

        // 公共访问NavFrame
        public Frame NavFramePublic => NavFrame;

        public MainWindow()
        {
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MainWindow构造函数开始\n", System.Text.Encoding.UTF8);
            }
            catch { }

            this.InitializeComponent();
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] InitializeComponent完成\n", System.Text.Encoding.UTF8);
            }
            catch { }

            // 应用语言设置
            Translator.CurrentLanguage = (AppLanguage)SettingsService.Instance.Language;
            Translator.LanguageChanged += UpdateLanguageTexts;
            UpdateLanguageTexts();

            // 设置窗口图标
            try
            {
                var appWindow = this.AppWindow;
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置窗口图标失败: {ex.Message}");
            }

            // 设置窗口大小和最小大小（防止窗口太小看不见）
            try
            {
                var appWindow = this.AppWindow;
                // 设置最小窗口大小
                appWindow.Resize(new Windows.Graphics.SizeInt32(900, 650));
                
                // 恢复上次的窗口大小（如果有保存）
                int windowWidth = 900;
                int windowHeight = 650;
                if (SettingsService.Instance.WindowWidth > 900 && SettingsService.Instance.WindowHeight > 650)
                {
                    windowWidth = (int)SettingsService.Instance.WindowWidth;
                    windowHeight = (int)SettingsService.Instance.WindowHeight;
                    appWindow.Resize(new Windows.Graphics.SizeInt32(windowWidth, windowHeight));
                }
                
                // 窗口居中显示
                var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(appWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                if (displayArea != null)
                {
                    var centeredPos = new Windows.Graphics.PointInt32(
                        (displayArea.WorkArea.Width - windowWidth) / 2,
                        (displayArea.WorkArea.Height - windowHeight) / 2);
                    appWindow.Move(centeredPos);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置窗口大小失败: {ex.Message}");
            }

            NavListView.SelectedIndex = 0;
            NavFrame.Navigate(typeof(HomePage));
            SetNavigationAnimation(SettingsService.Instance.EnableAnimation);
            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;
            
            // 获取窗口句柄
            _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // 确保窗口正常显示（防止窗口被最小化或隐藏）
            try
            {
                ShowWindow(_windowHandle, 1); // SW_SHOWNORMAL
            }
            catch { }

            // 拖动性能优化：子类化窗口监听拖动开始/结束
            _wndProcDelegate = new WndProcDelegate(CustomWndProc);
            _oldWndProc = SetWindowLongPtr(_windowHandle, GWLP_WNDPROC,
                System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MainWindow构造函数完成\n", System.Text.Encoding.UTF8);
            }
            catch { }
        }

        // 自定义窗口过程 - 监听拖动事件
        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_ENTERSIZEMOVE)
            {
                // 开始拖动/调整大小，禁用动画提升流畅度
                _isDragging = true;
                SetNavigationAnimation(false);
            }
            else if (msg == WM_EXITSIZEMOVE)
            {
                // 结束拖动/调整大小，恢复动画
                _isDragging = false;
                SetNavigationAnimation(SettingsService.Instance.EnableAnimation);
            }
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                // 关闭时保存游戏列表
                if (NavFrame.Content is HomePage homePage)
                {
                    homePage.SaveGamesNow();
                }
                
                // 关闭时保存窗口大小
                try
                {
                    var appWindow = this.AppWindow;
                    if (appWindow != null)
                    {
                        var size = appWindow.Size;
                        // 只保存大于最小尺寸的窗口大小
                        if (size.Width >= 900 && size.Height >= 650)
                        {
                            SettingsService.Instance.WindowWidth = size.Width;
                            SettingsService.Instance.WindowHeight = size.Height;
                            SettingsService.Instance.Save();
                        }
                    }
                }
                catch { }
            }
            catch { }
        }
        
        // 设置窗口透明度（0=完全透明，255=完全不透明）
        private void SetWindowTransparency(byte alpha)
        {
            try
            {
                if (alpha < 255)
                {
                    // 设置分层窗口
                    int exStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_LAYERED) == 0)
                    {
                        SetWindowLong(_windowHandle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
                    }
                    SetLayeredWindowAttributes(_windowHandle, 0, alpha, LWA_ALPHA);
                    _isLayered = true;
                }
                else
                {
                    // 移除分层窗口
                    int exStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_LAYERED) != 0)
                    {
                        SetWindowLong(_windowHandle, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED);
                    }
                    _isLayered = false;
                }
                // 强制刷新窗口，确保透明度立即生效（避免切换主题时透明度残留）
                SetWindowPos(_windowHandle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }
            catch { }
        }
        
        // 设置窗口纯透明（无模糊，使用SetWindowCompositionAttribute）
        private void SetPureTransparency(byte alpha, Windows.UI.Color color)
        {
            try
            {
                // 确保窗口是分层窗口
                int exStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
                if ((exStyle & WS_EX_LAYERED) == 0)
                {
                    SetWindowLong(_windowHandle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
                }
                
                // ABGR格式
                int abgr = (alpha << 24) | (color.B << 16) | (color.G << 8) | color.R;
                
                var accent = new AccentPolicy {
                    AccentState = ACCENT_ENABLE_TRANSPARENTGRADIENT,
                    AccentFlags = 2, // 透明
                    GradientColor = abgr,
                    AnimationId = 0
                };
                
                int accentSize = System.Runtime.InteropServices.Marshal.SizeOf(accent);
                IntPtr accentPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(accentSize);
                System.Runtime.InteropServices.Marshal.StructureToPtr(accent, accentPtr, false);
                
                var data = new WindowCompositionAttributeData {
                    Attribute = WCA_ACCENT_POLICY,
                    Data = accentPtr,
                    SizeOfData = accentSize
                };
                
                SetWindowCompositionAttribute(_windowHandle, ref data);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(accentPtr);
                
                _isLayered = true;
            }
            catch { }
        }
        
        // 设置标题栏颜色
        private void SetTitleBarColor(Windows.UI.Color bgColor, Windows.UI.Color fgColor)
        {
            try
            {
                var titleBar = this.AppWindow.TitleBar;
                // 标题栏背景色和前景色
                titleBar.BackgroundColor = bgColor;
                titleBar.ForegroundColor = fgColor;
                titleBar.InactiveBackgroundColor = bgColor;
                titleBar.InactiveForegroundColor = fgColor;
                // 按钮背景色和标题栏一致，前景色一致
                // 不设置悬停/按下状态，让系统自动处理
                titleBar.ButtonBackgroundColor = bgColor;
                titleBar.ButtonForegroundColor = fgColor;
                titleBar.ButtonInactiveBackgroundColor = bgColor;
                titleBar.ButtonInactiveForegroundColor = fgColor;
            }
            catch { }
        }

        // 应用材质背景
        public void ApplyBackdrop(int type)
        {
            try
            {
                // 先重置左侧导航栏为默认样式
                NavPanel.ClearValue(Grid.BackgroundProperty);
                NavPanel.ClearValue(Grid.OpacityProperty);
                NavPanel.ClearValue(Grid.BorderBrushProperty);
                // 注意：不在开头统一设置窗口透明度，避免case 5经历"移除分层→重新添加分层"导致透明度更新失败
                // 每个case自己设置需要的窗口透明度
                
                switch (type)
                {
                    case 1: // Mica云母
                        SetWindowTransparency(255);
                        this.SystemBackdrop = new MicaBackdrop();
                        RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                        // 标题栏颜色跟随主题
                        if (SettingsService.Instance.DarkMode)
                        {
                            SetTitleBarColor(Windows.UI.Color.FromArgb(255, 32, 32, 32), Windows.UI.Color.FromArgb(255, 255, 255, 255));
                        }
                        else
                        {
                            SetTitleBarColor(Windows.UI.Color.FromArgb(255, 243, 243, 243), Windows.UI.Color.FromArgb(255, 0, 0, 0));
                        }
                        break;
                    case 2: // Acrylic亚克力 - 半透明磨砂玻璃效果
                        SetWindowTransparency(255);
                        this.SystemBackdrop = new DesktopAcrylicBackdrop();
                        // 通过半透明背景叠加来调整磨砂玻璃效果（更透明）
                        if (SettingsService.Instance.DarkMode)
                        {
                            // 深色模式：深灰黑色半透明叠加（更透明）
                            RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 28, 28, 28));
                            SetTitleBarColor(Windows.UI.Color.FromArgb(200, 32, 32, 32), Windows.UI.Color.FromArgb(255, 255, 255, 255));
                        }
                        else
                        {
                            // 浅色模式：淡灰色半透明叠加（增加磨砂质感）
                            RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(50, 220, 220, 220));
                            SetTitleBarColor(Windows.UI.Color.FromArgb(200, 243, 243, 243), Windows.UI.Color.FromArgb(255, 0, 0, 0));
                        }
                        break;
                    case 5: // 纯透明 - 窗口半透明，能看清桌面
                        this.SystemBackdrop = null;
                        // 内容区域半透明背景
                        NavPanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                        NavPanel.Opacity = 1;
                        NavPanel.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                        if (SettingsService.Instance.DarkMode)
                        {
                            // 深色模式
                            SetWindowTransparency(245);
                            var darkColor = Windows.UI.Color.FromArgb(180, 40, 40, 40);
                            RootGrid.Background = new SolidColorBrush(darkColor);
                            SetTitleBarColor(Windows.UI.Color.FromArgb(200, 40, 40, 40), Windows.UI.Color.FromArgb(255, 255, 255, 255));
                        }
                        else
                        {
                            // 浅色模式
                            SetWindowTransparency(235);
                            var lightColor = Windows.UI.Color.FromArgb(140, 255, 255, 255);
                            RootGrid.Background = new SolidColorBrush(lightColor);
                            SetTitleBarColor(Windows.UI.Color.FromArgb(180, 255, 255, 255), Windows.UI.Color.FromArgb(255, 0, 0, 0));
                        }
                        break;
                    case 4: // 自定义壁纸
                        SetWindowTransparency(255);
                        this.SystemBackdrop = null;
                        // 如果有自定义壁纸路径，应用壁纸
                        if (!string.IsNullOrEmpty(SettingsService.Instance.CustomWallpaperPath) &&
                            System.IO.File.Exists(SettingsService.Instance.CustomWallpaperPath))
                        {
                            ApplyCustomWallpaper(SettingsService.Instance.CustomWallpaperPath);
                        }
                        else
                        {
                            // 没有壁纸时使用默认背景
                            if (SettingsService.Instance.DarkMode)
                            {
                                RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
                            }
                            else
                            {
                                RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                            }
                        }
                        break;
                    default: // 默认 - 纯色背景
                        SetWindowTransparency(255);
                        this.SystemBackdrop = null;
                        // 根据当前主题设置背景颜色
                        if (SettingsService.Instance.DarkMode)
                        {
                            RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
                            SetTitleBarColor(Windows.UI.Color.FromArgb(255, 32, 32, 32), Windows.UI.Color.FromArgb(255, 255, 255, 255));
                        }
                        else
                        {
                            RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                            SetTitleBarColor(Windows.UI.Color.FromArgb(255, 243, 243, 243), Windows.UI.Color.FromArgb(255, 0, 0, 0));
                        }
                        break;
                }
            }
            catch { }
        }

        // 应用自定义壁纸
        public async void ApplyCustomWallpaper(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath)) return;

                // 使用BitmapImage加载图片
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(imagePath);
                using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    await bitmap.SetSourceAsync(stream);
                }

                // 使用ImageBrush设置背景，Stretch="UniformToFill"按比例填满窗口，不留黑边
                var imageBrush = new ImageBrush
                {
                    ImageSource = bitmap,
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                RootGrid.Background = imageBrush;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用壁纸失败: {ex.Message}");
                // 失败时使用默认背景
                if (SettingsService.Instance.DarkMode)
                {
                    RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
                }
                else
                {
                    RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                }
            }
        }

        private bool _isThemeAnimating = false;
        private SpriteVisual _currentThemeSprite = null;

        /// <summary>
        /// 获取当前材质对应的擦除颜色
        /// 注意：擦除动画遮罩必须使用不透明颜色(alpha=255)，否则下面的旧主题界面会透过来导致颜色混合
        /// </summary>
        private Windows.UI.Color GetMaskColor(bool toDark)
        {
            int backdropType = SettingsService.Instance.BackdropType;
            // 擦除动画遮罩始终使用不透明颜色，确保动画期间颜色纯粹
            byte alpha = 255;

            switch (backdropType)
            {
                case 1: // Mica云母
                    return toDark
                        ? Windows.UI.Color.FromArgb(alpha, 26, 26, 26)
                        : Windows.UI.Color.FromArgb(alpha, 250, 250, 250);
                case 2: // Acrylic亚克力
                    return toDark
                        ? Windows.UI.Color.FromArgb(alpha, 40, 40, 40)
                        : Windows.UI.Color.FromArgb(alpha, 240, 240, 240);
                case 5: // 纯透明模糊
                    // 透明材质下也使用不透明颜色做擦除，动画结束后淡出显示真实的透明材质效果
                    // RGB值跟实际透明材质的RootGrid背景RGB对齐
                    return toDark
                        ? Windows.UI.Color.FromArgb(alpha, 0, 0, 0)
                        : Windows.UI.Color.FromArgb(alpha, 255, 255, 255);
                default: // 默认
                    return toDark
                        ? Windows.UI.Color.FromArgb(alpha, 32, 32, 32)
                        : Windows.UI.Color.FromArgb(alpha, 243, 243, 243);
            }
        }

        /// <summary>
        /// 深浅色切换擦除动画（使用Composition API，不依赖XAML元素）
        /// </summary>
        /// <param name="toDark">是否切换到深色模式</param>
        public async void AnimateThemeChange(bool toDark)
        {
            // 如果用户关闭了所有动画，直接切换主题
            if (!SettingsService.Instance.EnableAnimation)
            {
                App.ApplyTheme(toDark ? ElementTheme.Dark : ElementTheme.Light);
                ApplyBackdrop(SettingsService.Instance.BackdropType);
                return;
            }

            // 自定义壁纸不需要动画，直接切换
            if (SettingsService.Instance.BackdropType == 4)
            {
                App.ApplyTheme(toDark ? ElementTheme.Dark : ElementTheme.Light);
                ApplyBackdrop(SettingsService.Instance.BackdropType);
                return;
            }

            // 防止动画重叠
            if (_isThemeAnimating)
            {
                App.ApplyTheme(toDark ? ElementTheme.Dark : ElementTheme.Light);
                ApplyBackdrop(SettingsService.Instance.BackdropType);
                return;
            }
            _isThemeAnimating = true;

            try
            {
                // 强制清理之前的SpriteVisual
                try
                {
                    ElementCompositionPreview.SetElementChildVisual(RootGrid, null);
                    if (_currentThemeSprite != null)
                    {
                        _currentThemeSprite.Dispose();
                        _currentThemeSprite = null;
                    }
                }
                catch { }

                // 等待一帧确保清理完成
                await System.Threading.Tasks.Task.Delay(16);

                // 获取RootGrid的Visual和Compositor
                var rootVisual = ElementCompositionPreview.GetElementVisual(RootGrid);
                var compositor = rootVisual.Compositor;

                // 获取窗口大小（使用窗口Bounds，更可靠）
                var bounds = this.Bounds;
                float width = (float)bounds.Width;
                float height = (float)bounds.Height;
                float edgeWidth = 80f; // 边缘模糊区域宽度（在窗口外面）
                float totalWidth = width + edgeWidth; // 遮罩层总宽度（比窗口宽，让渐变部分在窗口外面）

                // 获取当前材质对应的擦除颜色
                Windows.UI.Color maskColor = GetMaskColor(toDark);
                Windows.UI.Color transparentColor = Windows.UI.Color.FromArgb(0, maskColor.R, maskColor.G, maskColor.B);

                // 创建渐变Brush：前窗口宽度部分是纯色，后edgeWidth部分是渐变透明
                var gradientBrush = compositor.CreateLinearGradientBrush();
                float solidEnd = width / totalWidth; // 纯色部分结束位置（相对比例）

                if (toDark)
                {
                    // 从左到右擦除：左边纯色，右边渐变透明
                    gradientBrush.StartPoint = new Vector2(0f, 0f);
                    gradientBrush.EndPoint = new Vector2(1f, 0f);
                    gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, maskColor));
                    gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop((float)solidEnd, maskColor));
                    gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, transparentColor));
                }
                else
                {
                    // 从右到左擦除：左边渐变透明，右边纯色
                    gradientBrush.StartPoint = new Vector2(0f, 0f);
                    gradientBrush.EndPoint = new Vector2(1f, 0f);
                    gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, transparentColor));
                    gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop((float)(edgeWidth / totalWidth), maskColor));
                    gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, maskColor));
                }

                // 创建一个比窗口宽的SpriteVisual，让渐变部分在动画结束时完全在窗口外面
                var spriteVisual = compositor.CreateSpriteVisual();
                spriteVisual.Size = new Vector2(totalWidth, height);
                spriteVisual.Brush = gradientBrush;
                _currentThemeSprite = spriteVisual;

                if (toDark)
                {
                    // 从左到右擦除：AnchorPoint在左边，Offset在窗口左边(0,0)
                    // 动画结束时：纯色部分覆盖窗口，渐变部分在窗口右边外面
                    spriteVisual.Offset = new Vector3(0, 0, 0);
                    spriteVisual.AnchorPoint = new Vector2(0f, 0f);
                }
                else
                {
                    // 从右到左擦除：AnchorPoint在右边，Offset在窗口右边(width,0)
                    // 动画结束时：纯色部分覆盖窗口，渐变部分在窗口左边外面
                    spriteVisual.Offset = new Vector3(width, 0, 0);
                    spriteVisual.AnchorPoint = new Vector2(1f, 0f);
                }
                spriteVisual.Scale = new Vector3(0f, 1f, 1f);

                // 将SpriteVisual插入到RootGrid的最上层
                ElementCompositionPreview.SetElementChildVisual(RootGrid, spriteVisual);

                // 等待两帧确保UI更新
                await System.Threading.Tasks.Task.Delay(32);

                // 创建ScaleX擦除动画（从0到1，铺满整个窗口）
                var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
                scaleAnimation.InsertKeyFrame(0f, new Vector3(0f, 1f, 1f));
                scaleAnimation.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f));
                scaleAnimation.Duration = TimeSpan.FromMilliseconds(500);

                // 启动Scale擦除动画并等待完成
                spriteVisual.StartAnimation("Scale", scaleAnimation);
                await System.Threading.Tasks.Task.Delay(520);

                // 铺满后，切换实际主题（此时遮罩完全覆盖窗口，用户看不到背景变化）
                App.ApplyTheme(toDark ? ElementTheme.Dark : ElementTheme.Light);
                // 立即更新背景颜色，避免主题默认颜色闪一下
                ApplyBackdrop(SettingsService.Instance.BackdropType);

                // 等待主题切换和背景更新完成
                await System.Threading.Tasks.Task.Delay(60);

                // 创建Opacity淡出动画
                var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
                opacityAnimation.InsertKeyFrame(0f, 1f);
                opacityAnimation.InsertKeyFrame(1f, 0f);
                opacityAnimation.Duration = TimeSpan.FromMilliseconds(250);

                // 启动Opacity动画并等待完成
                spriteVisual.StartAnimation("Opacity", opacityAnimation);
                await System.Threading.Tasks.Task.Delay(270);

                // 移除SpriteVisual
                ElementCompositionPreview.SetElementChildVisual(RootGrid, null);
                spriteVisual.Dispose();
                _currentThemeSprite = null;
                // 两个模式使用相同的窗口透明度，切换时只改变背景颜色，不需要额外刷新
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"主题切换动画失败: {ex.Message}");
                // 动画失败时直接切换主题
                App.ApplyTheme(toDark ? ElementTheme.Dark : ElementTheme.Light);
            }
            finally
            {
                // 强制清理
                try
                {
                    ElementCompositionPreview.SetElementChildVisual(RootGrid, null);
                    if (_currentThemeSprite != null)
                    {
                        _currentThemeSprite.Dispose();
                        _currentThemeSprite = null;
                    }
                }
                catch { }
                _isThemeAnimating = false;
            }
        }

        private void UpdateLanguageTexts()
        {
            Title = Translator.IsEnglish ? "Frame Gen + DLSS5 Enabler V1.14.0.0" : "多帧生成+DLSS5开启工具 V1.14.0.0";
            TitleText.Text = Translator.IsEnglish ? "Frame Gen + DLSS5" : "多帧生成+DLSS5";
            NavHomeText.Text = Translator.T("Nav_Home");
            NavUsageText.Text = Translator.T("Nav_Usage");
            NavUpdateText.Text = Translator.T("Nav_Update");
            NavSettingsText.Text = Translator.T("Nav_Settings");
            NavAboutText.Text = Translator.T("Nav_About");
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // 第一次激活时应用材质背景
            if (!_backdropApplied)
            {
                _backdropApplied = true;
                ApplyBackdrop(SettingsService.Instance.BackdropType);
            }

            if (_startupShown) return;
            _startupShown = true;

            if (SettingsService.Instance.ShowStartupDialog)
            {
                await System.Threading.Tasks.Task.Delay(500);
                await ShowStartupDialog();
            }

            // 显示使用前说明弹窗
            if (SettingsService.Instance.ShowUsageGuide)
            {
                await System.Threading.Tasks.Task.Delay(300);
                await ShowUsageGuideDialog();
            }

            // 自动检查更新
            if (!_updateChecked)
            {
                _updateChecked = true;
                _ = CheckForUpdatesAsync();
            }
        }

        // 版本比较：判断remote是否比current新
        private static bool IsNewerVersion(string remote, string current)
        {
            try
            {
                // 去掉V前缀
                remote = remote.TrimStart('V', 'v');
                current = current.TrimStart('V', 'v');
                if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(current, out var currentVer))
                {
                    return remoteVer > currentVer;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // 自动检查更新
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(2000); // 延迟2秒，避免启动时卡顿

                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var json = await client.GetStringAsync(SettingsService.Instance.UpdateCheckUrl);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var latestVersion = root.GetProperty("version").GetString();
                var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.12.3.1";

                // 解析下载链接
                string githubUrl = SettingsService.Instance.GithubUrl;
                string kuakeUrl = root.TryGetProperty("downloadUrlKuake", out var k) ? k.GetString() : "";
                string baiduUrl = root.TryGetProperty("downloadUrlBaidu", out var b) ? b.GetString() : "";
                string pan123Url = root.TryGetProperty("downloadUrl123pan", out var l) ? l.GetString() : "";

                // 比较版本号 - 只有远程版本更高时才提示更新
                if (!string.IsNullOrEmpty(latestVersion) &&
                    IsNewerVersion(latestVersion, currentVersion) &&
                    latestVersion != SettingsService.Instance.SkipUpdateVersion)
                {
                    var dialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "New Version Available" : "发现新版本",
                        Content = new StackPanel
                        {
                            Spacing = 8,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = string.Format(Translator.IsEnglish ?
                                        "Latest version: {0}\nCurrent version: {1}" :
                                        "最新版本：{0}\n当前版本：{1}", latestVersion, currentVersion),
                                    TextWrapping = TextWrapping.Wrap
                                },
                                new TextBlock
                                {
                                    Text = Translator.IsEnglish ?
                                        "Click Update to choose a download source." :
                                        "点击更新选择下载渠道，点击不再提示以后将不再提醒此版本。",
                                    TextWrapping = TextWrapping.Wrap,
                                    Opacity = 0.7
                                }
                            }
                        },
                        PrimaryButtonText = Translator.IsEnglish ? "Update" : "更新",
                        SecondaryButtonText = Translator.IsEnglish ? "Don't Remind Again" : "不再提示",
                        CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                        XamlRoot = this.Content.XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        // 弹出下载渠道选择
                        await ShowDownloadSourceDialog(githubUrl, kuakeUrl, baiduUrl, pan123Url);
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        // 保存不再提示的版本号
                        SettingsService.Instance.SkipUpdateVersion = latestVersion;
                        SettingsService.Instance.Save();
                    }
                }
            }
            catch
            {
                // 检查更新失败时静默处理，不打扰用户
            }
        }

        // 显示下载渠道选择对话框
        private async System.Threading.Tasks.Task ShowDownloadSourceDialog(string githubUrl, string kuakeUrl, string baiduUrl, string pan123Url)
        {
            ContentDialog dialog = null;
            
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = Translator.IsEnglish ? "Please choose a download source:" : "请选择下载渠道：",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            // GitHub直接下载按钮
            var directDownloadBtn = new Button
            {
                Content = Translator.IsEnglish ? "Direct Download (GitHub)" : "直接下载（GitHub）",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            directDownloadBtn.Click += async (s, e) => 
            {
                dialog?.Hide(); // 先关闭当前对话框
                await System.Threading.Tasks.Task.Delay(300); // 等待对话框完全关闭
                await DownloadFromGitHubAsync();
            };
            panel.Children.Add(directDownloadBtn);

            // GitHub按钮
            var githubBtn = new Button
            {
                Content = "GitHub " + (Translator.IsEnglish ? "(Open in browser)" : "（浏览器打开）"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = githubUrl
            };
            githubBtn.Click += (s, e) => 
            {
                dialog?.Hide();
                OpenUrl((string)((Button)s).Tag);
            };
            panel.Children.Add(githubBtn);

            // 夸克网盘按钮
            if (!string.IsNullOrEmpty(kuakeUrl))
            {
                var kuakeBtn = new Button
                {
                    Content = Translator.IsEnglish ? "Quark Cloud Drive" : "夸克网盘",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = kuakeUrl
                };
                kuakeBtn.Click += (s, e) => 
                {
                    dialog?.Hide();
                    OpenUrl((string)((Button)s).Tag);
                };
                panel.Children.Add(kuakeBtn);
            }

            // 百度网盘按钮
            if (!string.IsNullOrEmpty(baiduUrl))
            {
                var baiduBtn = new Button
                {
                    Content = Translator.IsEnglish ? "Baidu Cloud Drive" : "百度网盘",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = baiduUrl
                };
                baiduBtn.Click += (s, e) => 
                {
                    dialog?.Hide();
                    OpenUrl((string)((Button)s).Tag);
                };
                panel.Children.Add(baiduBtn);
            }

            // 123云盘按钮
            if (!string.IsNullOrEmpty(pan123Url))
            {
                var pan123Btn = new Button
                {
                    Content = Translator.IsEnglish ? "123Pan" : "123云盘",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = pan123Url
                };
                pan123Btn.Click += (s, e) => 
                {
                    dialog?.Hide();
                    OpenUrl((string)((Button)s).Tag);
                };
                panel.Children.Add(pan123Btn);
            }

            dialog = new ContentDialog
            {
                Title = Translator.IsEnglish ? "Choose Download Source" : "选择下载渠道",
                Content = panel,
                CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        // 从GitHub直接下载
        private async System.Threading.Tasks.Task DownloadFromGitHubAsync()
        {
            // 当前主题
            var theme = SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light;
            try
            {
                // 先显示正在获取下载信息的提示
                var loadingDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Please wait" : "请稍候",
                    Content = Translator.IsEnglish ? "Getting download information from GitHub..." : "正在从GitHub获取下载信息...",
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };
                var loadingTask = loadingDialog.ShowAsync();

                // 从GitHub API获取最新release的下载链接
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                var apiUrl = "https://api.github.com/repos/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases/latest";
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DLSSFrameGenEnabler");
                var json = await client.GetStringAsync(apiUrl);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var assets = doc.RootElement.GetProperty("assets");
                if (assets.GetArrayLength() == 0)
                {
                    loadingDialog.Hide();
                    await System.Threading.Tasks.Task.Delay(200);
                    var errDialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "Download Failed" : "下载失败",
                        Content = Translator.IsEnglish ?
                            "No downloadable files found in the latest release.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." :
                            "最新版本中没有找到可下载的文件。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                        CloseButtonText = "OK",
                        RequestedTheme = theme,
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errDialog.ShowAsync();
                    return;
                }

                var asset = assets[0];
                var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                var fileName = asset.GetProperty("name").GetString();
                var fileSize = asset.GetProperty("size").GetInt64();
                
                // 读取更新内容
                var version = doc.RootElement.TryGetProperty("tag_name", out var tag) ? tag.GetString() : "";
                var changelog = doc.RootElement.TryGetProperty("body", out var body) ? body.GetString() : "";

                loadingDialog.Hide();
                await System.Threading.Tasks.Task.Delay(200);

                // 显示更新内容对话框，让用户确认是否下载
                var changelogPanel = new StackPanel { Spacing = 12 };
                changelogPanel.Children.Add(new TextBlock
                {
                    Text = string.Format(Translator.IsEnglish ? "Latest Version: {0}" : "最新版本：{0}", version),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 16
                });
                changelogPanel.Children.Add(new TextBlock
                {
                    Text = string.Format(Translator.IsEnglish ? "File Size: {0:0.0} MB" : "文件大小：{0:0.0} MB", fileSize / 1024.0 / 1024.0),
                    Opacity = 0.7
                });
                
                if (!string.IsNullOrEmpty(changelog))
                {
                    changelogPanel.Children.Add(new TextBlock
                    {
                        Text = Translator.IsEnglish ? "Update Notes:" : "更新内容：",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Margin = new Thickness(0, 8, 0, 0)
                    });
                    
                    // 使用ScrollViewer来显示可能很长的更新内容
                    var scrollViewer = new ScrollViewer
                    {
                        MaxHeight = 300,
                        VerticalScrollMode = ScrollMode.Enabled,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };
                    var changelogText = new TextBlock
                    {
                        Text = changelog,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.9
                    };
                    scrollViewer.Content = changelogText;
                    changelogPanel.Children.Add(scrollViewer);
                }

                // 添加加速器提示
                changelogPanel.Children.Add(new TextBlock
                {
                    Text = Translator.IsEnglish ?
                        "Tip: Users in mainland China may need a VPN/accelerator to download from GitHub." :
                        "提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                    FontSize = 12,
                    Opacity = 0.7,
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });

                var changelogDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Update Available" : "发现新版本",
                    Content = changelogPanel,
                    PrimaryButtonText = Translator.IsEnglish ? "Download" : "下载",
                    CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };
                var changelogResult = await changelogDialog.ShowAsync();
                if (changelogResult != ContentDialogResult.Primary) return;

                // 让用户选择下载文件夹
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");
                
                // 获取窗口句柄
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null) return;

                // 显示下载进度对话框
                var progressPanel = new StackPanel { Spacing = 12 };
                var progressText = new TextBlock
                {
                    Text = string.Format(Translator.IsEnglish ? "Downloading: {0}\nSize: {1:0.0} MB" : "正在下载：{0}\n大小：{1:0.0} MB", fileName, fileSize / 1024.0 / 1024.0),
                    TextWrapping = TextWrapping.Wrap
                };
                var progressBar = new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Height = 20,
                    IsIndeterminate = false
                };
                var speedText = new TextBlock
                {
                    Text = Translator.IsEnglish ? "Connecting..." : "正在连接...",
                    Opacity = 0.7
                };
                // 暂停和取消按钮
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var pauseBtn = new Button
                {
                    Content = Translator.IsEnglish ? "Pause" : "暂停",
                    Width = 100
                };
                var cancelBtn = new Button
                {
                    Content = Translator.IsEnglish ? "Cancel" : "取消",
                    Width = 100
                };
                buttonPanel.Children.Add(pauseBtn);
                buttonPanel.Children.Add(cancelBtn);
                progressPanel.Children.Add(progressText);
                progressPanel.Children.Add(progressBar);
                progressPanel.Children.Add(speedText);
                progressPanel.Children.Add(buttonPanel);

                var progressDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Downloading" : "下载中",
                    Content = progressPanel,
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };

                // 使用CancellationTokenSource来支持取消
                var cts = new System.Threading.CancellationTokenSource();
                // 暂停控制
                var pauseEvent = new System.Threading.ManualResetEventSlim(true);
                bool isPaused = false;
                // 取消时是否删除文件
                bool deleteOnCancel = false;

                // 暂停按钮事件
                pauseBtn.Click += (s, e) =>
                {
                    isPaused = !isPaused;
                    if (isPaused)
                    {
                        pauseEvent.Reset();
                        pauseBtn.Content = Translator.IsEnglish ? "Resume" : "继续";
                        speedText.Text = Translator.IsEnglish ? "Paused" : "已暂停";
                    }
                    else
                    {
                        pauseEvent.Set();
                        pauseBtn.Content = Translator.IsEnglish ? "Pause" : "暂停";
                    }
                };

                // 取消按钮事件 - 在当前对话框内切换内容，不弹出新对话框
                cancelBtn.Click += (s, e) =>
                {
                    // 构建确认取消的UI
                    var confirmPanel = new StackPanel { Spacing = 16 };
                    confirmPanel.Children.Add(new TextBlock
                    {
                        Text = Translator.IsEnglish ? "Confirm cancel download?" : "确认取消下载？",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 16
                    });
                    confirmPanel.Children.Add(new TextBlock
                    {
                        Text = Translator.IsEnglish ? "Choose how to cancel:" : "请选择取消方式：",
                        Opacity = 0.7
                    });
                    var confirmBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    
                    var deleteBtn = new Button
                    {
                        Content = Translator.IsEnglish ? "Cancel & Delete" : "取消并删除文件",
                        Width = 140
                    };
                    var keepBtn = new Button
                    {
                        Content = Translator.IsEnglish ? "Cancel & Keep" : "取消并保留文件",
                        Width = 140
                    };
                    var backBtn = new Button
                    {
                        Content = Translator.IsEnglish ? "Continue" : "继续下载",
                        Width = 120
                    };
                    confirmBtnPanel.Children.Add(deleteBtn);
                    confirmBtnPanel.Children.Add(keepBtn);
                    confirmBtnPanel.Children.Add(backBtn);
                    confirmPanel.Children.Add(confirmBtnPanel);
                    
                    // 替换对话框内容
                    progressDialog.Content = confirmPanel;
                    progressDialog.Title = Translator.IsEnglish ? "Cancel Download" : "取消下载";
                    
                    // 删除文件并取消
                    deleteBtn.Click += (s2, e2) =>
                    {
                        deleteOnCancel = true;
                        cts.Cancel();
                        pauseEvent.Set();
                        progressDialog.Hide();
                    };
                    
                    // 保留文件并取消
                    keepBtn.Click += (s2, e2) =>
                    {
                        cts.Cancel();
                        pauseEvent.Set();
                        progressDialog.Hide();
                        // 文件保留在磁盘上，用于断点续传
                    };
                    
                    // 继续下载 - 恢复原来的进度UI
                    backBtn.Click += (s2, e2) =>
                    {
                        progressDialog.Content = progressPanel;
                        progressDialog.Title = Translator.IsEnglish ? "Downloading" : "下载中";
                    };
                };

                // 开始下载（支持断点续传）
                var downloadTask = System.Threading.Tasks.Task.Run(async () =>
                {
                    var filePath = System.IO.Path.Combine(folder.Path, fileName);
                    long existingBytes = 0;
                    
                    // 检查是否已有部分下载的文件（断点续传）
                    if (System.IO.File.Exists(filePath))
                    {
                        existingBytes = new System.IO.FileInfo(filePath).Length;
                    }
                    
                    using var downloadClient = new System.Net.Http.HttpClient();
                    downloadClient.Timeout = TimeSpan.FromMinutes(60);
                    
                    // 如果已有部分文件，添加Range请求头实现断点续传
                    if (existingBytes > 0)
                    {
                        downloadClient.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
                    }
                    
                    var response = await downloadClient.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? fileSize;
                    // 如果服务器支持断点续传，总大小需要加上已下载的部分
                    if (existingBytes > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent)
                    {
                        totalBytes += existingBytes;
                    }
                    else
                    {
                        // 服务器不支持断点续传，重新开始
                        existingBytes = 0;
                    }
                    
                    // 文件流：如果有已下载部分，使用追加模式；否则创建新文件
                    var fileMode = existingBytes > 0 ? System.IO.FileMode.Append : System.IO.FileMode.Create;
                    using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
                    using var fileStream = new System.IO.FileStream(filePath, fileMode, System.IO.FileAccess.Write, System.IO.FileShare.None);
                    
                    var buffer = new byte[65536];
                    long totalRead = existingBytes;
                    int bytesRead;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                    {
                        // 检查暂停
                        pauseEvent.Wait(cts.Token);
                        
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                        totalRead += bytesRead;
                        
                        // 更新进度
                        var progress = (int)((double)totalRead / totalBytes * 100);
                        var speed = (totalRead - existingBytes) / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;
                        var remainingSeconds = speed > 0 ? (totalBytes - totalRead) / 1024.0 / 1024.0 / speed : 0;
                        
                        // 在UI线程更新
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!isPaused)
                            {
                                progressBar.Value = progress;
                                var resumeInfo = existingBytes > 0 ? string.Format(Translator.IsEnglish ? " (Resumed from {0:0.0} MB)" : " (已续传 {0:0.0} MB)", existingBytes / 1024.0 / 1024.0) : "";
                                speedText.Text = string.Format(Translator.IsEnglish ?
                                    "{0:0.0} MB/s | {1:0.0}% | {2:0}s remaining{3}" :
                                    "{0:0.0} MB/s | {1:0.0}% | 剩余 {2:0} 秒{3}",
                                    speed, progress, remainingSeconds, resumeInfo);
                            }
                        });
                    }
                    
                    stopwatch.Stop();
                    return filePath;
                });

                // 显示进度对话框
                _ = progressDialog.ShowAsync();

                string savedPath;
                try
                {
                    savedPath = await downloadTask;
                }
                catch (OperationCanceledException)
                {
                    // 用户主动取消下载
                    if (deleteOnCancel)
                    {
                        // 等待文件流释放（下载任务结束后using会自动释放流）
                        await System.Threading.Tasks.Task.Delay(500);
                        try
                        {
                            var filePath = System.IO.Path.Combine(folder.Path, fileName);
                            if (System.IO.File.Exists(filePath))
                            {
                                System.IO.File.Delete(filePath);
                            }
                        }
                        catch { }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    cts.Cancel();
                    progressDialog.Hide();
                    var errDialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "Download Failed" : "下载失败",
                        Content = Translator.IsEnglish ?
                            "Download failed. This may be due to network issues or GitHub access restrictions.\n\nTip: Users in mainland China may need a VPN/accelerator to download from GitHub." :
                            "下载失败，可能是网络问题或GitHub访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                        CloseButtonText = "OK",
                        RequestedTheme = theme,
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errDialog.ShowAsync();
                    return;
                }

                // 关闭进度对话框
                progressDialog.Hide();

                // 下载完成提示 - 添加手动安装和自动安装选项
                var completePanel = new StackPanel { Spacing = 8 };
                completePanel.Children.Add(new TextBlock
                {
                    Text = string.Format(Translator.IsEnglish ?
                        "File saved to:\n{0}" :
                        "文件已保存到：\n{0}", savedPath),
                    TextWrapping = TextWrapping.Wrap
                });
                completePanel.Children.Add(new TextBlock
                {
                    Text = Translator.IsEnglish ?
                        "\nChoose installation method:" :
                        "\n请选择安装方式：",
                    TextWrapping = TextWrapping.Wrap
                });
                completePanel.Children.Add(new TextBlock
                {
                    Text = Translator.IsEnglish ?
                        "• Manual Install: Safe, extract and replace files yourself\n• Auto Install (Beta): Automatically extract and overwrite, may have risks" :
                        "• 手动安装：安全，自行解压替换文件\n• 自动安装（Beta）：自动解压覆盖，可能存在风险",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.7
                });

                var completeDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Download Complete" : "下载完成",
                    Content = completePanel,
                    PrimaryButtonText = Translator.IsEnglish ? "Auto Install (Beta)" : "自动安装（Beta）",
                    SecondaryButtonText = Translator.IsEnglish ? "Manual Install" : "手动安装",
                    CloseButtonText = "OK",
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };
                var result = await completeDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // 自动安装
                    await AutoInstallUpdateAsync(savedPath);
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    // 手动安装 - 打开文件夹
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + savedPath + "\"");
                }
            }
            catch (Exception ex)
            {
                var errDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Download Failed" : "下载失败",
                    Content = Translator.IsEnglish ?
                        "Failed to get download information from GitHub. This may be due to network issues or GitHub access restrictions.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." :
                        "从GitHub获取下载信息失败，可能是网络问题或GitHub访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                    CloseButtonText = "OK",
                    RequestedTheme = theme,
                    XamlRoot = this.Content.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }

        // 自动安装更新（Beta）
        private async System.Threading.Tasks.Task AutoInstallUpdateAsync(string zipPath)
        {
            try
            {
                // 确认对话框
                var confirmDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Auto Install (Beta)" : "自动安装（Beta）",
                    Content = Translator.IsEnglish ?
                        "Auto Install is still in testing, please backup your current version before using this feature.\n\nThe software will close automatically, then the update script will replace files and restart.\n\nDo not turn off your computer during the update.\n\nContinue?" :
                        "自动安装功能仍在测试中，使用前请尽量备份好原版本。\n\n软件将自动关闭，随后更新脚本会替换文件并重新启动。\n\n更新过程中请勿关闭电脑。\n\n是否继续？",
                    PrimaryButtonText = Translator.IsEnglish ? "Continue" : "继续",
                    CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                    RequestedTheme = SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light,
                    XamlRoot = this.Content.XamlRoot
                };
                var confirmResult = await confirmDialog.ShowAsync();
                if (confirmResult != ContentDialogResult.Primary) return;

                // 解压到临时目录
                string tempDir = Path.Combine(Path.GetTempPath(), $"DFGE_Update_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(tempDir);
                
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, true);

                // 找到新版本的根目录（可能压缩包内有一层文件夹）
                string newVersionDir = tempDir;
                var subDirs = Directory.GetDirectories(tempDir);
                if (subDirs.Length == 1)
                {
                    // 检查子目录中是否有exe文件
                    var exeInSub = Directory.GetFiles(subDirs[0], "*.exe", SearchOption.TopDirectoryOnly);
                    if (exeInSub.Length > 0)
                    {
                        newVersionDir = subDirs[0];
                    }
                }

                // 软件当前目录
                string appDir = AppContext.BaseDirectory.TrimEnd('\\');

                // 生成更新批处理脚本
                string batPath = Path.Combine(Path.GetTempPath(), "DFGE_Update.bat");
                string exeName = "DLSSFrameGenEnabler_WinUI3.exe";
                // 使用随机数作为备份目录名，避免日期时间截取的编码问题
                string backupId = new Random().Next(100000, 999999).ToString();
                string batContent = $@"@echo off
echo Updating DLSS5+MFG Enable...
echo.

REM 等待软件进程结束
timeout /t 3 /nobreak >nul
taskkill /f /im {exeName} 2>nul
timeout /t 2 /nobreak >nul

REM 备份当前版本
set BACKUP_DIR=%TEMP%\DFGE_Backup_{backupId}
mkdir ""%BACKUP_DIR%"" 2>nul
echo Backing up current version...
xcopy ""{appDir}\*"" ""%BACKUP_DIR%\"" /E /Y /Q /H >nul 2>&1

REM 复制新文件
echo Copying new files...
xcopy ""{newVersionDir}\*"" ""{appDir}\"" /E /Y /R /H >nul 2>&1

REM 重新启动软件
echo Starting software...
start """" ""{appDir}\{exeName}""

REM 清理临时文件
timeout /t 3 /nobreak >nul
rmdir /s /q ""{tempDir}"" 2>nul
del ""%~f0"" 2>nul
";

                File.WriteAllText(batPath, batContent, System.Text.Encoding.Default);

                // 运行批处理脚本并关闭软件
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{batPath}\"\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };
                System.Diagnostics.Process.Start(startInfo);

                // 关闭当前软件
                this.Close();
            }
            catch (Exception ex)
            {
                var errDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Auto Install Failed" : "自动安装失败",
                    Content = string.Format(Translator.IsEnglish ?
                        "Auto install failed: {0}\n\nPlease use manual install instead." :
                        "自动安装失败：{0}\n\n请改用手动安装。", ex.Message),
                    CloseButtonText = "OK",
                    RequestedTheme = SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light,
                    XamlRoot = this.Content.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }

        // 打开URL
        private void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }

        private async System.Threading.Tasks.Task ShowStartupDialog()
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock
            {
                Text = Translator.T("Dlg_Startup_Text1"),
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = Translator.T("Dlg_Startup_Text2"),
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = Translator.T("Dlg_Startup_Text3"),
                TextWrapping = TextWrapping.Wrap
            });

            // GitHub链接（带图标）
            var githubPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            githubPanel.Children.Add(new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/github.jpg")),
                Width = 20,
                Height = 20
            });
            githubPanel.Children.Add(new TextBlock
            {
                Text = Translator.T("Dlg_Startup_Github"),
                VerticalAlignment = VerticalAlignment.Center
            });
            var githubLink = new HyperlinkButton
            {
                Content = githubPanel,
                NavigateUri = new Uri("https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5"),
                Padding = new Thickness(0, 4, 0, 4)
            };

            // 小黑盒链接（带图标）
            var xiaoheihePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            xiaoheihePanel.Children.Add(new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/xiaoheihe.png")),
                Width = 20,
                Height = 20
            });
            xiaoheihePanel.Children.Add(new TextBlock
            {
                Text = Translator.T("Dlg_Startup_Xiaoheihe"),
                VerticalAlignment = VerticalAlignment.Center
            });
            var xiaoheiheLink = new HyperlinkButton
            {
                Content = xiaoheihePanel,
                NavigateUri = new Uri(SettingsService.Instance.XiaoheiheUrl),
                Padding = new Thickness(0, 4, 0, 4)
            };
            panel.Children.Add(githubLink);
            panel.Children.Add(xiaoheiheLink);

            var dialog = new ContentDialog
            {
                Title = Translator.T("Dlg_Startup_Title"),
                Content = panel,
                PrimaryButtonText = Translator.T("Dlg_Startup_Understand"),
                CloseButtonText = Translator.T("Common_Close"),
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                SettingsService.Instance.ShowStartupDialog = false;
                SettingsService.Instance.Save();
            }
        }

        /// <summary>
        /// 显示使用前说明弹窗
        /// </summary>
        private async System.Threading.Tasks.Task ShowUsageGuideDialog()
        {
            var panel = new Microsoft.UI.Xaml.Controls.StackPanel
            {
                Spacing = 12
            };

            // 标题说明
            var introText = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = Translator.IsEnglish ?
                    "Please read the following notes carefully before using this software:" :
                    "使用本软件前，请仔细阅读以下注意事项：",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            panel.Children.Add(introText);

            // 注意事项列表
            var notes = Translator.IsEnglish ? new[]
            {
                "⚠  Must enable DLSS/Frame Generation in game settings FIRST, then close the game, and finally install patches.",
                "⚠  miHoYo games (Genshin Impact, Honkai: Star Rail, Zenless Zone Zero, etc.) are NOT supported due to strict anti-cheat.",
                "⚠  Ubisoft and EA games may not be fully compatible, please test by yourself.",
                "⚠  Vulkan API games are NOT supported yet.",
                "⚠  It is recommended to backup game files or verify game integrity after uninstalling patches.",
                "⚠  Do not use this software in online games with anti-cheat, there may be a ban risk."
            } : new[]
            {
                "⚠  必须先在游戏设置中开启DLSS/帧生成相关功能，然后关闭游戏，最后再安装补丁。",
                "⚠  米哈游系列游戏（原神、崩坏：星穹铁道、绝区零等）因反作弊严苛，暂不支持。",
                "⚠  育碧、EA等平台的游戏可能无法完全适配，是否生效请玩家自行测试。",
                "⚠  Vulkan渲染的游戏暂未适配。",
                "⚠  建议备份游戏文件，或在卸载补丁后验证游戏完整性。",
                "⚠  不建议在有反作弊的网游中使用本软件，可能存在封号风险。"
            };

            foreach (var note in notes)
            {
                var noteText = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = note,
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                    FontSize = 13
                };
                panel.Children.Add(noteText);
            }

            // 不再提示复选框
            var dontShowAgain = new Microsoft.UI.Xaml.Controls.CheckBox
            {
                Content = Translator.IsEnglish ? "Don't show this again" : "不再显示此提示",
                Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 0)
            };
            panel.Children.Add(dontShowAgain);

            var dialog = new ContentDialog
            {
                Title = Translator.IsEnglish ? "Usage Notes" : "使用前说明",
                Content = panel,
                PrimaryButtonText = Translator.IsEnglish ? "I Understand" : "我知道了",
                CloseButtonText = Translator.IsEnglish ? "Close" : "关闭",
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dontShowAgain.IsChecked == true)
            {
                SettingsService.Instance.ShowUsageGuide = false;
                SettingsService.Instance.Save();
            }
        }

        private void NavListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isNavigating) return;
            if (NavListView.SelectedItem is ListViewItem item && item.Tag?.ToString() is string tag)
            {
                NavigateTo(tag);
                // 清除底部列表的选中
                _isNavigating = true;
                FooterListView.SelectedItem = null;
                _isNavigating = false;
            }
        }

        private void FooterListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isNavigating) return;
            if (FooterListView.SelectedItem is ListViewItem item && item.Tag?.ToString() is string tag)
            {
                NavigateTo(tag);
                // 清除顶部列表的选中
                _isNavigating = true;
                NavListView.SelectedItem = null;
                _isNavigating = false;
            }
        }

        private void NavigateTo(string tag)
        {
            if (tag == _currentTag) return;
            _currentTag = tag;

            var pageType = tag switch
            {
                "home" => typeof(HomePage),
                "usage" => typeof(UsagePage),
                "updatelog" => typeof(UpdateLogPage),
                "settings" => typeof(SettingsPage),
                "about" => typeof(AboutPage),
                _ => typeof(HomePage)
            };

            if (SettingsService.Instance.EnableAnimation)
            {
                // 开启动画：使用滑动过渡效果，从左向右滑入（导航栏在左边，更符合视觉习惯）
                NavFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo()
                {
                    Effect = SlideNavigationTransitionEffect.FromLeft
                });
            }
            else
            {
                // 关闭动画：禁止所有过渡效果
                NavFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
            }
        }

        public void SetNavigationAnimation(bool enable)
        {
            // 动画通过Navigate的transitionInfo控制，这里不需要额外设置
            // 实际在NavigateTo里根据设置决定用哪种transition
        }
    }
}
