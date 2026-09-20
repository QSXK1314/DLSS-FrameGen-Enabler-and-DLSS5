using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DLSSFrameGenEnabler_WinUI3.Pages;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT;
using WinRT.Interop;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;

namespace DLSSFrameGenEnabler_WinUI3;

public sealed partial class MainWindow : Window
{
	private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

	private struct WindowCompositionAttributeData
	{
		public int Attribute;

		public nint Data;

		public int SizeOfData;
	}

	private struct AccentPolicy
	{
		public int AccentState;

		public int AccentFlags;

		public int GradientColor;

		public int AnimationId;
	}

	private const int GWL_EXSTYLE = -20;

	private const int WS_EX_LAYERED = 524288;

	private const int LWA_ALPHA = 2;

	private const uint SWP_NOMOVE = 2u;

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOZORDER = 4u;

	private const uint SWP_FRAMECHANGED = 32u;

	private const int GWLP_WNDPROC = -4;

	private const uint WM_ENTERSIZEMOVE = 561u;

	private const uint WM_EXITSIZEMOVE = 562u;

	private nint _oldWndProc;

	private WndProcDelegate _wndProcDelegate;

	private const int WCA_ACCENT_POLICY = 19;

	private const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;

	private nint _windowHandle;

	private bool _isLayered;

	private bool _startupShown;

	private bool _updateChecked;

	private string _currentTag = "home";

	private bool _isNavigating;

	private bool _backdropApplied;

	private bool _isDragging;

	private bool _isNavCollapsed;

	private bool _isThemeAnimating;

	private SpriteVisual _currentThemeSprite;

	private DispatcherTimer _navAnimTimer;

	private double _navAnimStart;

	private double _navAnimTarget;

	private DateTime _navAnimStartTime;

	private const double NavAnimDuration = 200.0;

	public Frame NavFramePublic => NavFrame;

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll")]
	private static extern bool SetLayeredWindowAttributes(nint hWnd, uint crKey, byte bAlpha, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

	[DllImport("user32.dll")]
	private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint Msg, nint wParam, nint lParam);

	public MainWindow()
	{
		try
		{
			File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MainWindow构造函数开始\n", Encoding.UTF8);
		}
		catch
		{
		}
		InitializeComponent();
		try
		{
			File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] InitializeComponent完成\n", Encoding.UTF8);
		}
		catch
		{
		}
		Translator.CurrentLanguage = (AppLanguage)SettingsService.Instance.Language;
		Translator.LanguageChanged += UpdateLanguageTexts;
		UpdateLanguageTexts();
		try
		{
			AppWindow appWindow = base.AppWindow;
			string text = Path.Combine(AppContext.BaseDirectory, "app.ico");
			if (File.Exists(text))
			{
				appWindow.SetIcon(text);
			}
		}
		catch (Exception)
		{
		}
		try
		{
			GpuInfo gpuInfo = GpuDetector.DetectPrimaryGpu();
			string text2 = "";
			if (gpuInfo.Series.StartsWith("RTX"))
			{
				text2 = Path.Combine(AppContext.BaseDirectory, "Assets", "NVIDIA_logo.png");
			}
			else if (gpuInfo.Series == "AMD")
			{
				text2 = Path.Combine(AppContext.BaseDirectory, "Assets", "AMD_logo.png");
			}
			if (!string.IsNullOrEmpty(text2) && File.Exists(text2))
			{
				BitmapImage source = new BitmapImage
				{
					UriSource = new Uri(text2)
				};
				TitleIcon.Source = source;
			}
		}
		catch
		{
		}
		try
		{
			AppWindow appWindow2 = base.AppWindow;
			appWindow2.Resize(new SizeInt32(900, 650));
			int num = 900;
			int num2 = 650;
			if (SettingsService.Instance.WindowWidth > 900.0 && SettingsService.Instance.WindowHeight > 650.0)
			{
				num = (int)SettingsService.Instance.WindowWidth;
				num2 = (int)SettingsService.Instance.WindowHeight;
				appWindow2.Resize(new SizeInt32(num, num2));
			}
			DisplayArea fromWindowId = DisplayArea.GetFromWindowId(appWindow2.Id, DisplayAreaFallback.Nearest);
			if (fromWindowId != null)
			{
				PointInt32 position = new PointInt32((fromWindowId.WorkArea.Width - num) / 2, (fromWindowId.WorkArea.Height - num2) / 2);
				appWindow2.Move(position);
			}
		}
		catch (Exception)
		{
		}
		NavListView.SelectedIndex = 0;
		NavFrame.Navigate(typeof(HomePage));
		SetNavigationAnimation(SettingsService.Instance.EnableAnimation);
		base.Activated += MainWindow_Activated;
		base.Closed += MainWindow_Closed;
		_windowHandle = WindowNative.GetWindowHandle(this);
		try
		{
			ShowWindow(_windowHandle, 1);
		}
		catch
		{
		}
		_wndProcDelegate = CustomWndProc;
		_oldWndProc = SetWindowLongPtr(_windowHandle, -4, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
		try
		{
			File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MainWindow构造函数完成\n", Encoding.UTF8);
		}
		catch
		{
		}
	}

	private nint CustomWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
	{
		switch (msg)
		{
		case 561u:
			_isDragging = true;
			SetNavigationAnimation(enable: false);
			break;
		case 562u:
			_isDragging = false;
			SetNavigationAnimation(SettingsService.Instance.EnableAnimation);
			break;
		}
		return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
	}

	private void MainWindow_Closed(object sender, WindowEventArgs args)
	{
		try
		{
			if (NavFrame.Content is HomePage homePage)
			{
				homePage.SaveGamesNow();
			}
			try
			{
				AppWindow appWindow = base.AppWindow;
				if (appWindow != null)
				{
					SizeInt32 size = appWindow.Size;
					if (size.Width >= 900 && size.Height >= 650)
					{
						SettingsService.Instance.WindowWidth = size.Width;
						SettingsService.Instance.WindowHeight = size.Height;
						SettingsService.Instance.Save();
					}
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private void SetWindowTransparency(byte alpha)
	{
		try
		{
			if (alpha < byte.MaxValue)
			{
				int windowLong = GetWindowLong(_windowHandle, -20);
				if ((windowLong & 0x80000) == 0)
				{
					SetWindowLong(_windowHandle, -20, windowLong | 0x80000);
				}
				SetLayeredWindowAttributes(_windowHandle, 0u, alpha, 2u);
				_isLayered = true;
			}
			else
			{
				int windowLong2 = GetWindowLong(_windowHandle, -20);
				if ((windowLong2 & 0x80000) != 0)
				{
					SetWindowLong(_windowHandle, -20, windowLong2 & -524289);
				}
				_isLayered = false;
			}
			SetWindowPos(_windowHandle, IntPtr.Zero, 0, 0, 0, 0, 39u);
		}
		catch
		{
		}
	}

	private void SetPureTransparency(byte alpha, Color color)
	{
		try
		{
			int windowLong = GetWindowLong(_windowHandle, -20);
			if ((windowLong & 0x80000) == 0)
			{
				SetWindowLong(_windowHandle, -20, windowLong | 0x80000);
			}
			int gradientColor = (alpha << 24) | (color.B << 16) | (color.G << 8) | color.R;
			AccentPolicy structure = new AccentPolicy
			{
				AccentState = 2,
				AccentFlags = 2,
				GradientColor = gradientColor,
				AnimationId = 0
			};
			int num = Marshal.SizeOf(structure);
			nint num2 = Marshal.AllocHGlobal(num);
			Marshal.StructureToPtr(structure, num2, fDeleteOld: false);
			WindowCompositionAttributeData data = new WindowCompositionAttributeData
			{
				Attribute = 19,
				Data = num2,
				SizeOfData = num
			};
			SetWindowCompositionAttribute(_windowHandle, ref data);
			Marshal.FreeHGlobal(num2);
			_isLayered = true;
		}
		catch
		{
		}
	}

	private void SetTitleBarColor(Color bgColor, Color fgColor)
	{
		try
		{
			AppWindowTitleBar titleBar = base.AppWindow.TitleBar;
			titleBar.BackgroundColor = bgColor;
			titleBar.ForegroundColor = fgColor;
			titleBar.InactiveBackgroundColor = bgColor;
			titleBar.InactiveForegroundColor = fgColor;
			titleBar.ButtonBackgroundColor = bgColor;
			titleBar.ButtonForegroundColor = fgColor;
			titleBar.ButtonInactiveBackgroundColor = bgColor;
			titleBar.ButtonInactiveForegroundColor = fgColor;
		}
		catch
		{
		}
	}

	public void ApplyBackdrop(int type)
	{
		try
		{
			NavPanel.ClearValue(Panel.BackgroundProperty);
			NavPanel.ClearValue(UIElement.OpacityProperty);
			NavPanel.ClearValue(Grid.BorderBrushProperty);
			switch (type)
			{
			case 1:
				SetWindowTransparency(byte.MaxValue);
				base.SystemBackdrop = new MicaBackdrop();
				RootGrid.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
				if (SettingsService.Instance.DarkMode)
				{
					SetTitleBarColor(Color.FromArgb(byte.MaxValue, 32, 32, 32), Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				}
				else
				{
					SetTitleBarColor(Color.FromArgb(byte.MaxValue, 243, 243, 243), Color.FromArgb(byte.MaxValue, 0, 0, 0));
				}
				break;
			case 2:
				SetWindowTransparency(byte.MaxValue);
				base.SystemBackdrop = new DesktopAcrylicBackdrop();
				if (SettingsService.Instance.DarkMode)
				{
					RootGrid.Background = new SolidColorBrush(Color.FromArgb(40, 28, 28, 28));
					SetTitleBarColor(Color.FromArgb(200, 32, 32, 32), Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				}
				else
				{
					RootGrid.Background = new SolidColorBrush(Color.FromArgb(50, 220, 220, 220));
					SetTitleBarColor(Color.FromArgb(200, 243, 243, 243), Color.FromArgb(byte.MaxValue, 0, 0, 0));
				}
				break;
			case 5:
				base.SystemBackdrop = null;
				NavPanel.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
				NavPanel.Opacity = 1.0;
				NavPanel.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
				if (SettingsService.Instance.DarkMode)
				{
					SetWindowTransparency(245);
					Color color = Color.FromArgb(180, 40, 40, 40);
					RootGrid.Background = new SolidColorBrush(color);
					SetTitleBarColor(Color.FromArgb(200, 40, 40, 40), Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				}
				else
				{
					SetWindowTransparency(235);
					Color color2 = Color.FromArgb(140, byte.MaxValue, byte.MaxValue, byte.MaxValue);
					RootGrid.Background = new SolidColorBrush(color2);
					SetTitleBarColor(Color.FromArgb(180, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(byte.MaxValue, 0, 0, 0));
				}
				break;
			case 4:
				SetWindowTransparency(byte.MaxValue);
				base.SystemBackdrop = null;
				if (!string.IsNullOrEmpty(SettingsService.Instance.CustomWallpaperPath) && File.Exists(SettingsService.Instance.CustomWallpaperPath))
				{
					ApplyCustomWallpaper(SettingsService.Instance.CustomWallpaperPath);
				}
				else if (SettingsService.Instance.DarkMode)
				{
					RootGrid.Background = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 32, 32, 32));
				}
				else
				{
					RootGrid.Background = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 243, 243, 243));
				}
				break;
			default:
				SetWindowTransparency(byte.MaxValue);
				base.SystemBackdrop = null;
				if (SettingsService.Instance.DarkMode)
				{
					RootGrid.Background = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 32, 32, 32));
					SetTitleBarColor(Color.FromArgb(byte.MaxValue, 32, 32, 32), Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				}
				else
				{
					RootGrid.Background = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 243, 243, 243));
					SetTitleBarColor(Color.FromArgb(byte.MaxValue, 243, 243, 243), Color.FromArgb(byte.MaxValue, 0, 0, 0));
				}
				break;
			}
		}
		catch
		{
		}
	}

	public async void ApplyCustomWallpaper(string imagePath)
	{
		_ = 2;
		try
		{
			if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
			{
				BitmapImage bitmap = new BitmapImage();
				using (IRandomAccessStream stream = await (await StorageFile.GetFileFromPathAsync(imagePath)).OpenAsync(FileAccessMode.Read))
				{
					await bitmap.SetSourceAsync(stream);
				}
				ImageBrush background = new ImageBrush
				{
					ImageSource = bitmap,
					Stretch = Stretch.UniformToFill,
					AlignmentX = AlignmentX.Center,
					AlignmentY = AlignmentY.Center
				};
				RootGrid.Background = background;
			}
		}
		catch (Exception)
		{
			if (SettingsService.Instance.DarkMode)
			{
				RootGrid.Background = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 32, 32, 32));
			}
			else
			{
				RootGrid.Background = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 243, 243, 243));
			}
		}
	}

	private Color GetMaskColor(bool toDark)
	{
		int backdropType = SettingsService.Instance.BackdropType;
		byte a = byte.MaxValue;
		switch (backdropType)
		{
		case 1:
			if (!toDark)
			{
				return Color.FromArgb(a, 250, 250, 250);
			}
			return Color.FromArgb(a, 26, 26, 26);
		case 2:
			if (!toDark)
			{
				return Color.FromArgb(a, 240, 240, 240);
			}
			return Color.FromArgb(a, 40, 40, 40);
		case 5:
			if (!toDark)
			{
				return Color.FromArgb(a, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			}
			return Color.FromArgb(a, 0, 0, 0);
		default:
			if (!toDark)
			{
				return Color.FromArgb(a, 243, 243, 243);
			}
			return Color.FromArgb(a, 32, 32, 32);
		}
	}

	public async void AnimateThemeChange(bool toDark)
	{
		if (!SettingsService.Instance.EnableAnimation)
		{
			App.ApplyTheme((!toDark) ? ElementTheme.Light : ElementTheme.Dark);
			ApplyBackdrop(SettingsService.Instance.BackdropType);
			return;
		}
		if (SettingsService.Instance.BackdropType == 4)
		{
			App.ApplyTheme((!toDark) ? ElementTheme.Light : ElementTheme.Dark);
			ApplyBackdrop(SettingsService.Instance.BackdropType);
			return;
		}
		if (_isThemeAnimating)
		{
			App.ApplyTheme((!toDark) ? ElementTheme.Light : ElementTheme.Dark);
			ApplyBackdrop(SettingsService.Instance.BackdropType);
			return;
		}
		_isThemeAnimating = true;
		try
		{
			try
			{
				ElementCompositionPreview.SetElementChildVisual(RootGrid, null);
				if (_currentThemeSprite != null)
				{
					_currentThemeSprite.Dispose();
					_currentThemeSprite = null;
				}
			}
			catch
			{
			}
			await Task.Delay(16);
			Visual elementVisual = ElementCompositionPreview.GetElementVisual(RootGrid);
			Compositor compositor = elementVisual.Compositor;
			Rect bounds = base.Bounds;
			float num = (float)bounds.Width;
			float y = (float)bounds.Height;
			float num2 = 80f;
			float num3 = num + num2;
			Color maskColor = GetMaskColor(toDark);
			Color color = Color.FromArgb(0, maskColor.R, maskColor.G, maskColor.B);
			CompositionLinearGradientBrush compositionLinearGradientBrush = compositor.CreateLinearGradientBrush();
			float num4 = num / num3;
			if (toDark)
			{
				compositionLinearGradientBrush.StartPoint = new Vector2(0f, 0f);
				compositionLinearGradientBrush.EndPoint = new Vector2(1f, 0f);
				compositionLinearGradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, maskColor));
				compositionLinearGradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(num4, maskColor));
				compositionLinearGradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, color));
			}
			else
			{
				compositionLinearGradientBrush.StartPoint = new Vector2(0f, 0f);
				compositionLinearGradientBrush.EndPoint = new Vector2(1f, 0f);
				compositionLinearGradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, color));
				compositionLinearGradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(num2 / num3, maskColor));
				compositionLinearGradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, maskColor));
			}
			SpriteVisual spriteVisual = compositor.CreateSpriteVisual();
			spriteVisual.Size = new Vector2(num3, y);
			spriteVisual.Brush = compositionLinearGradientBrush;
			_currentThemeSprite = spriteVisual;
			if (toDark)
			{
				spriteVisual.Offset = new Vector3(0f, 0f, 0f);
				spriteVisual.AnchorPoint = new Vector2(0f, 0f);
			}
			else
			{
				spriteVisual.Offset = new Vector3(num, 0f, 0f);
				spriteVisual.AnchorPoint = new Vector2(1f, 0f);
			}
			spriteVisual.Scale = new Vector3(0f, 1f, 1f);
			ElementCompositionPreview.SetElementChildVisual(RootGrid, spriteVisual);
			await Task.Delay(32);
			Vector3KeyFrameAnimation vector3KeyFrameAnimation = compositor.CreateVector3KeyFrameAnimation();
			vector3KeyFrameAnimation.InsertKeyFrame(0f, new Vector3(0f, 1f, 1f));
			vector3KeyFrameAnimation.InsertKeyFrame(1f, new Vector3(1f, 1f, 1f));
			vector3KeyFrameAnimation.Duration = TimeSpan.FromMilliseconds(500L);
			spriteVisual.StartAnimation("Scale", vector3KeyFrameAnimation);
			await Task.Delay(520);
			App.ApplyTheme((!toDark) ? ElementTheme.Light : ElementTheme.Dark);
			ApplyBackdrop(SettingsService.Instance.BackdropType);
			await Task.Delay(60);
			ScalarKeyFrameAnimation scalarKeyFrameAnimation = compositor.CreateScalarKeyFrameAnimation();
			scalarKeyFrameAnimation.InsertKeyFrame(0f, 1f);
			scalarKeyFrameAnimation.InsertKeyFrame(1f, 0f);
			scalarKeyFrameAnimation.Duration = TimeSpan.FromMilliseconds(250L);
			spriteVisual.StartAnimation("Opacity", scalarKeyFrameAnimation);
			await Task.Delay(270);
			ElementCompositionPreview.SetElementChildVisual(RootGrid, null);
			spriteVisual.Dispose();
			_currentThemeSprite = null;
		}
		catch (Exception)
		{
			App.ApplyTheme((!toDark) ? ElementTheme.Light : ElementTheme.Dark);
		}
		finally
		{
			try
			{
				ElementCompositionPreview.SetElementChildVisual(RootGrid, null);
				if (_currentThemeSprite != null)
				{
					_currentThemeSprite.Dispose();
					_currentThemeSprite = null;
				}
			}
			catch
			{
			}
			_isThemeAnimating = false;
		}
	}

	private void UpdateLanguageTexts()
	{
		base.Title = (Translator.IsEnglish ? ("Frame Gen + DLSS5 Enabler " + App.CurrentVersion) : ("多帧生成+DLSS5开启工具 " + App.CurrentVersion));
		TitleText.Text = (Translator.IsEnglish ? "Frame Gen + DLSS5" : "多帧生成+DLSS5");
		NavHomeText.Text = Translator.T("Nav_Home");
		NavUsageText.Text = Translator.T("Nav_Usage");
		NavUpdateText.Text = Translator.T("Nav_Update");
		NavSettingsText.Text = Translator.T("Nav_Settings");
		NavAboutText.Text = Translator.T("Nav_About");
		ToggleNavText.Text = ((!_isNavCollapsed) ? (Translator.IsEnglish ? "Collapse" : "收起导航") : (Translator.IsEnglish ? "Expand" : "展开导航"));
	}

	private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
	{
		if (!_backdropApplied)
		{
			_backdropApplied = true;
			ApplyBackdrop(SettingsService.Instance.BackdropType);
		}
		if (!_startupShown)
		{
			_startupShown = true;
			if (SettingsService.Instance.ShowStartupDialog)
			{
				await Task.Delay(500);
				await ShowStartupDialog();
			}
			if (SettingsService.Instance.ShowUsageGuide)
			{
				await Task.Delay(300);
				await ShowUsageGuideDialog();
			}
			if (SettingsService.Instance.ShowBetaFeedback)
			{
				await Task.Delay(300);
				await ShowBetaFeedbackDialog();
			}
			if (!_updateChecked)
			{
				_updateChecked = true;
				CheckForUpdatesAsync();
			}
		}
	}

	private static bool IsNewerVersion(string remote, string current)
	{
		try
		{
			remote = remote.TrimStart(new char[2] { 'V', 'v' });
			current = current.TrimStart(new char[2] { 'V', 'v' });
			int num = remote.IndexOf('-');
			if (num > 0)
			{
				remote = remote.Substring(0, num);
			}
			num = current.IndexOf('-');
			if (num > 0)
			{
				current = current.Substring(0, num);
			}
			if (Version.TryParse(remote, out Version result) && Version.TryParse(current, out Version result2))
			{
				return result > result2;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private async Task CheckForUpdatesAsync()
	{
		_ = 3;
		try
		{
			await Task.Delay(2000);
			using HttpClient client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(10L);
			string json = await client.GetStringAsync(SettingsService.Instance.UpdateCheckUrl);
			JsonDocumentOptions options = new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			};
			using JsonDocument doc = JsonDocument.Parse(json, options);
			JsonElement rootElement = doc.RootElement;
			string latestVersion = rootElement.GetProperty("version").GetString();
			string currentVersion = App.CurrentVersion;
			string githubUrl = SettingsService.Instance.GithubUrl;
			string kuakeUrl = (rootElement.TryGetProperty("downloadUrlKuake", out var value) ? value.GetString() : "");
			string baiduUrl = (rootElement.TryGetProperty("downloadUrlBaidu", out var value2) ? value2.GetString() : "");
			string pan123Url = (rootElement.TryGetProperty("downloadUrl123pan", out var value3) ? value3.GetString() : "");
			if (rootElement.TryGetProperty("downloadLinks", out var value4))
			{
				if (string.IsNullOrEmpty(kuakeUrl) && value4.TryGetProperty("kuake", out var value5))
				{
					kuakeUrl = value5.GetString();
				}
				if (string.IsNullOrEmpty(baiduUrl) && value4.TryGetProperty("baidu", out var value6))
				{
					baiduUrl = value6.GetString();
				}
				if (string.IsNullOrEmpty(pan123Url) && value4.TryGetProperty("pan123", out var value7))
				{
					pan123Url = value7.GetString();
				}
				if (value4.TryGetProperty("github", out var value8))
				{
					githubUrl = value8.GetString();
				}
			}
			if (!string.IsNullOrEmpty(latestVersion) && IsNewerVersion(latestVersion, currentVersion) && latestVersion != SettingsService.Instance.SkipUpdateVersion)
			{
				switch (await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "New Version Available" : "发现新版本"),
					Content = new StackPanel
					{
						Spacing = 8.0,
						Children = 
						{
							(UIElement)new TextBlock
							{
								Text = string.Format(Translator.IsEnglish ? "Latest version: {0}\nCurrent version: {1}" : "最新版本：{0}\n当前版本：{1}", latestVersion, currentVersion),
								TextWrapping = TextWrapping.Wrap
							},
							(UIElement)new TextBlock
							{
								Text = (Translator.IsEnglish ? "Click Update to choose a download source." : "点击更新选择下载渠道，点击不再提示以后将不再提醒此版本。"),
								TextWrapping = TextWrapping.Wrap,
								Opacity = 0.7
							}
						}
					},
					PrimaryButtonText = (Translator.IsEnglish ? "Update" : "更新"),
					SecondaryButtonText = (Translator.IsEnglish ? "Don't Remind Again" : "不再提示"),
					CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync())
				{
				case ContentDialogResult.Primary:
					await ShowDownloadSourceDialog(githubUrl, kuakeUrl, baiduUrl, pan123Url);
					break;
				case ContentDialogResult.Secondary:
					SettingsService.Instance.SkipUpdateVersion = latestVersion;
					SettingsService.Instance.Save();
					break;
				}
			}
		}
		catch
		{
		}
	}

	private async Task ShowDownloadSourceDialog(string githubUrl, string kuakeUrl, string baiduUrl, string pan123Url)
	{
		ContentDialog dialog = null;
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 8.0
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = (Translator.IsEnglish ? "Please choose a download source:" : "请选择下载渠道："),
			FontWeight = FontWeights.SemiBold
		});
		Button button = new Button
		{
			Content = (Translator.IsEnglish ? "Direct Download (GitHub)" : "直接下载（GitHub）"),
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		button.Click += async delegate
		{
			dialog?.Hide();
			await Task.Delay(300);
			await DownloadFromGitHubAsync();
		};
		stackPanel.Children.Add(button);
		Button button2 = new Button
		{
			Content = "GitHub " + (Translator.IsEnglish ? "(Open in browser)" : "（浏览器打开）"),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Tag = githubUrl
		};
		button2.Click += delegate(object s, RoutedEventArgs e)
		{
			dialog?.Hide();
			OpenUrl((string)((Button)s).Tag);
		};
		stackPanel.Children.Add(button2);
		if (!string.IsNullOrEmpty(kuakeUrl))
		{
			Button button3 = new Button
			{
				Content = (Translator.IsEnglish ? "Quark Cloud Drive" : "夸克网盘"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Tag = kuakeUrl
			};
			button3.Click += delegate(object s, RoutedEventArgs e)
			{
				dialog?.Hide();
				OpenUrl((string)((Button)s).Tag);
			};
			stackPanel.Children.Add(button3);
		}
		if (!string.IsNullOrEmpty(baiduUrl))
		{
			Button button4 = new Button
			{
				Content = (Translator.IsEnglish ? "Baidu Cloud Drive" : "百度网盘"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Tag = baiduUrl
			};
			button4.Click += delegate(object s, RoutedEventArgs e)
			{
				dialog?.Hide();
				OpenUrl((string)((Button)s).Tag);
			};
			stackPanel.Children.Add(button4);
		}
		if (!string.IsNullOrEmpty(pan123Url))
		{
			Button button5 = new Button
			{
				Content = (Translator.IsEnglish ? "123Pan" : "123云盘"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Tag = pan123Url
			};
			button5.Click += delegate(object s, RoutedEventArgs e)
			{
				dialog?.Hide();
				OpenUrl((string)((Button)s).Tag);
			};
			stackPanel.Children.Add(button5);
		}
		dialog = new ContentDialog
		{
			Title = (Translator.IsEnglish ? "Choose Download Source" : "选择下载渠道"),
			Content = stackPanel,
			CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
			XamlRoot = base.Content.XamlRoot
		};
		await dialog.ShowAsync();
	}

	private async Task DownloadFromGitHubAsync()
	{
		ElementTheme theme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark);
		try
		{
			ContentDialog loadingDialog = new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Please wait" : "请稍候"),
				Content = (Translator.IsEnglish ? "Getting download information from GitHub..." : "正在从GitHub获取下载信息..."),
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			};
			loadingDialog.ShowAsync();
			using HttpClient client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(30L);
			client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DLSSFrameGenEnabler");
			JsonElement releaseElement = default(JsonElement);
			string requestUri = "https://api.github.com/repos/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases?per_page=20";
			using (JsonDocument docAll = JsonDocument.Parse(await client.GetStringAsync(requestUri)))
			{
				JsonElement rootElement = docAll.RootElement;
				bool flag = false;
				foreach (JsonElement item2 in rootElement.EnumerateArray())
				{
					if (item2.TryGetProperty("prerelease", out var value) && value.GetBoolean())
					{
						releaseElement = item2.Clone();
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					string requestUri2 = "https://api.github.com/repos/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases/latest";
					using JsonDocument jsonDocument = JsonDocument.Parse(await client.GetStringAsync(requestUri2));
					releaseElement = jsonDocument.RootElement.Clone();
				}
			}
			JsonElement property = releaseElement.GetProperty("assets");
			if (property.GetArrayLength() == 0)
			{
				loadingDialog.Hide();
				await Task.Delay(200);
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Download Failed" : "下载失败"),
					Content = (Translator.IsEnglish ? "No downloadable files found in the latest release.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." : "最新版本中没有找到可下载的文件。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
					CloseButtonText = "OK",
					RequestedTheme = theme,
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				return;
			}
			JsonElement jsonElement = property[0];
			string downloadUrl = jsonElement.GetProperty("browser_download_url").GetString();
			string fileName = jsonElement.GetProperty("name").GetString();
			long fileSize = jsonElement.GetProperty("size").GetInt64();
			string version = (releaseElement.TryGetProperty("tag_name", out var value2) ? value2.GetString() : "");
			string changelog = (releaseElement.TryGetProperty("body", out var value3) ? value3.GetString() : "");
			loadingDialog.Hide();
			await Task.Delay(200);
			StackPanel stackPanel = new StackPanel
			{
				Spacing = 12.0
			};
			stackPanel.Children.Add(new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "Latest Version: {0}" : "最新版本：{0}", version),
				FontWeight = FontWeights.SemiBold,
				FontSize = 16.0
			});
			stackPanel.Children.Add(new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "File Size: {0:0.0} MB" : "文件大小：{0:0.0} MB", (double)fileSize / 1024.0 / 1024.0),
				Opacity = 0.7
			});
			if (!string.IsNullOrEmpty(changelog))
			{
				stackPanel.Children.Add(new TextBlock
				{
					Text = (Translator.IsEnglish ? "Update Notes:" : "更新内容："),
					FontWeight = FontWeights.SemiBold,
					Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
				});
				ScrollViewer scrollViewer = new ScrollViewer
				{
					MaxHeight = 300.0,
					VerticalScrollMode = ScrollMode.Enabled,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto
				};
				TextBlock content = new TextBlock
				{
					Text = changelog,
					TextWrapping = TextWrapping.Wrap,
					Opacity = 0.9
				};
				scrollViewer.Content = content;
				stackPanel.Children.Add(scrollViewer);
			}
			stackPanel.Children.Add(new TextBlock
			{
				Text = (Translator.IsEnglish ? "Tip: Users in mainland China may need a VPN/accelerator to download from GitHub." : "提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
				FontSize = 12.0,
				Opacity = 0.7,
				Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
				TextWrapping = TextWrapping.Wrap
			});
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Update Available" : "发现新版本"),
				Content = stackPanel,
				PrimaryButtonText = (Translator.IsEnglish ? "Download" : "下载"),
				CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() != ContentDialogResult.Primary)
			{
				return;
			}
			FolderPicker obj = new FolderPicker
			{
				SuggestedStartLocation = PickerLocationId.Desktop,
				FileTypeFilter = { "*" }
			};
			nint windowHandle = WindowNative.GetWindowHandle(this);
			InitializeWithWindow.Initialize(obj, windowHandle);
			StorageFolder folder = await obj.PickSingleFolderAsync();
			if (folder == null)
			{
				return;
			}
			StackPanel progressPanel = new StackPanel
			{
				Spacing = 12.0
			};
			TextBlock item = new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "Downloading: {0}\nSize: {1:0.0} MB" : "正在下载：{0}\n大小：{1:0.0} MB", fileName, (double)fileSize / 1024.0 / 1024.0),
				TextWrapping = TextWrapping.Wrap
			};
			ProgressBar progressBar = new ProgressBar
			{
				Minimum = 0.0,
				Maximum = 100.0,
				Value = 0.0,
				Height = 20.0,
				IsIndeterminate = false
			};
			TextBlock speedText = new TextBlock
			{
				Text = (Translator.IsEnglish ? "Connecting..." : "正在连接..."),
				Opacity = 0.7
			};
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 8.0
			};
			Button pauseBtn = new Button
			{
				Content = (Translator.IsEnglish ? "Pause" : "暂停"),
				Width = 100.0
			};
			Button button = new Button
			{
				Content = (Translator.IsEnglish ? "Cancel" : "取消"),
				Width = 100.0
			};
			stackPanel2.Children.Add(pauseBtn);
			stackPanel2.Children.Add(button);
			progressPanel.Children.Add(item);
			progressPanel.Children.Add(progressBar);
			progressPanel.Children.Add(speedText);
			progressPanel.Children.Add(stackPanel2);
			ContentDialog progressDialog = new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Downloading" : "下载中"),
				Content = progressPanel,
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			};
			CancellationTokenSource cts = new CancellationTokenSource();
			ManualResetEventSlim pauseEvent = new ManualResetEventSlim(initialState: true);
			bool isPaused = false;
			bool deleteOnCancel = false;
			pauseBtn.Click += delegate
			{
				isPaused = !isPaused;
				if (isPaused)
				{
					pauseEvent.Reset();
					pauseBtn.Content = (Translator.IsEnglish ? "Resume" : "继续");
					speedText.Text = (Translator.IsEnglish ? "Paused" : "已暂停");
				}
				else
				{
					pauseEvent.Set();
					pauseBtn.Content = (Translator.IsEnglish ? "Pause" : "暂停");
				}
			};
			button.Click += delegate
			{
				StackPanel stackPanel4 = new StackPanel
				{
					Spacing = 16.0
				};
				stackPanel4.Children.Add(new TextBlock
				{
					Text = (Translator.IsEnglish ? "Confirm cancel download?" : "确认取消下载？"),
					FontWeight = FontWeights.SemiBold,
					FontSize = 16.0
				});
				stackPanel4.Children.Add(new TextBlock
				{
					Text = (Translator.IsEnglish ? "Choose how to cancel:" : "请选择取消方式："),
					Opacity = 0.7
				});
				StackPanel stackPanel5 = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Spacing = 8.0
				};
				Button button2 = new Button
				{
					Content = (Translator.IsEnglish ? "Cancel & Delete" : "取消并删除文件"),
					Width = 140.0
				};
				Button button3 = new Button
				{
					Content = (Translator.IsEnglish ? "Cancel & Keep" : "取消并保留文件"),
					Width = 140.0
				};
				Button button4 = new Button
				{
					Content = (Translator.IsEnglish ? "Continue" : "继续下载"),
					Width = 120.0
				};
				stackPanel5.Children.Add(button2);
				stackPanel5.Children.Add(button3);
				stackPanel5.Children.Add(button4);
				stackPanel4.Children.Add(stackPanel5);
				progressDialog.Content = stackPanel4;
				progressDialog.Title = (Translator.IsEnglish ? "Cancel Download" : "取消下载");
				button2.Click += delegate
				{
					deleteOnCancel = true;
					cts.Cancel();
					pauseEvent.Set();
					progressDialog.Hide();
				};
				button3.Click += delegate
				{
					cts.Cancel();
					pauseEvent.Set();
					progressDialog.Hide();
				};
				button4.Click += delegate
				{
					progressDialog.Content = progressPanel;
					progressDialog.Title = (Translator.IsEnglish ? "Downloading" : "下载中");
				};
			};
			Task<string> task = Task.Run(async delegate
			{
				string filePath = Path.Combine(folder.Path, fileName);
				long existingBytes = 0L;
				if (File.Exists(filePath))
				{
					existingBytes = new FileInfo(filePath).Length;
				}
				using HttpClient downloadClient = new HttpClient();
				downloadClient.Timeout = TimeSpan.FromMinutes(60L);
				if (existingBytes > 0)
				{
					downloadClient.DefaultRequestHeaders.Range = new RangeHeaderValue(existingBytes, null);
				}
				HttpResponseMessage httpResponseMessage = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
				httpResponseMessage.EnsureSuccessStatusCode();
				long totalBytes = httpResponseMessage.Content.Headers.ContentLength.GetValueOrDefault(fileSize);
				if (existingBytes > 0 && httpResponseMessage.StatusCode == HttpStatusCode.PartialContent)
				{
					totalBytes += existingBytes;
				}
				else
				{
					existingBytes = 0L;
				}
				FileMode fileMode = ((existingBytes > 0) ? FileMode.Append : FileMode.Create);
				using Stream contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(cts.Token);
				using FileStream fileStream = new FileStream(filePath, fileMode, FileAccess.Write, FileShare.None);
				byte[] buffer = new byte[65536];
				long totalRead = existingBytes;
				Stopwatch stopwatch = Stopwatch.StartNew();
				while (true)
				{
					int num;
					int bytesRead = (num = await contentStream.ReadAsync(buffer, 0, buffer.Length, cts.Token));
					if (num <= 0)
					{
						break;
					}
					pauseEvent.Wait(cts.Token);
					await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
					totalRead += bytesRead;
					int progress = (int)((double)totalRead / (double)totalBytes * 100.0);
					double speed = (double)(totalRead - existingBytes) / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;
					double remainingSeconds = ((speed > 0.0) ? ((double)(totalBytes - totalRead) / 1024.0 / 1024.0 / speed) : 0.0);
					base.DispatcherQueue.TryEnqueue(delegate
					{
						if (!isPaused)
						{
							progressBar.Value = progress;
							string text = ((existingBytes > 0) ? string.Format(Translator.IsEnglish ? " (Resumed from {0:0.0} MB)" : " (已续传 {0:0.0} MB)", (double)existingBytes / 1024.0 / 1024.0) : "");
							TextBlock textBlock = speedText;
							string format = (Translator.IsEnglish ? "{0:0.0} MB/s | {1:0.0}% | {2:0}s remaining{3}" : "{0:0.0} MB/s | {1:0.0}% | 剩余 {2:0} 秒{3}");
							InlineArray4<object> buffer2 = default(InlineArray4<object>);
							buffer2[0] = speed;
							buffer2[1] = progress;
							buffer2[2] = remainingSeconds;
							buffer2[3] = text;
							textBlock.Text = string.Format(format, (ReadOnlySpan<object?>)buffer2);
						}
					});
				}
				stopwatch.Stop();
				return filePath;
			});
			progressDialog.ShowAsync();
			string savedPath;
			try
			{
				savedPath = await task;
			}
			catch (OperationCanceledException)
			{
				if (!deleteOnCancel)
				{
					return;
				}
				await Task.Delay(500);
				try
				{
					string path = Path.Combine(folder.Path, fileName);
					if (File.Exists(path))
					{
						File.Delete(path);
					}
					return;
				}
				catch
				{
					return;
				}
			}
			catch (Exception ex2)
			{
				_ = ex2;
				cts.Cancel();
				progressDialog.Hide();
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Download Failed" : "下载失败"),
					Content = (Translator.IsEnglish ? "Download failed. This may be due to network issues or GitHub access restrictions.\n\nTip: Users in mainland China may need a VPN/accelerator to download from GitHub." : "下载失败，可能是网络问题或GitHub访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
					CloseButtonText = "OK",
					RequestedTheme = theme,
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				return;
			}
			progressDialog.Hide();
			StackPanel stackPanel3 = new StackPanel
			{
				Spacing = 8.0
			};
			stackPanel3.Children.Add(new TextBlock
			{
				Text = string.Format(Translator.IsEnglish ? "File saved to:\n{0}" : "文件已保存到：\n{0}", savedPath),
				TextWrapping = TextWrapping.Wrap
			});
			stackPanel3.Children.Add(new TextBlock
			{
				Text = (Translator.IsEnglish ? "\nChoose installation method:" : "\n请选择安装方式："),
				TextWrapping = TextWrapping.Wrap
			});
			stackPanel3.Children.Add(new TextBlock
			{
				Text = (Translator.IsEnglish ? "• Manual Install: Safe, extract and replace files yourself\n• Auto Install (Beta): Automatically extract and overwrite, may have risks" : "• 手动安装：安全，自行解压替换文件\n• 自动安装（Beta）：自动解压覆盖，可能存在风险"),
				TextWrapping = TextWrapping.Wrap,
				Opacity = 0.7
			});
			switch (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Download Complete" : "下载完成"),
				Content = stackPanel3,
				PrimaryButtonText = (Translator.IsEnglish ? "Auto Install (Beta)" : "自动安装（Beta）"),
				SecondaryButtonText = (Translator.IsEnglish ? "Manual Install" : "手动安装"),
				CloseButtonText = "OK",
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync())
			{
			case ContentDialogResult.Primary:
				await AutoInstallUpdateAsync(savedPath);
				break;
			case ContentDialogResult.Secondary:
				Process.Start("explorer.exe", "/select,\"" + savedPath + "\"");
				break;
			}
		}
		catch (Exception ex3)
		{
			_ = ex3;
			await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Download Failed" : "下载失败"),
				Content = (Translator.IsEnglish ? "Failed to get download information from GitHub. This may be due to network issues or GitHub access restrictions.\n\nTip: Users in mainland China may need a VPN/accelerator to access GitHub." : "从GitHub获取下载信息失败，可能是网络问题或GitHub访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。"),
				CloseButtonText = "OK",
				RequestedTheme = theme,
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
		}
	}

	private async Task AutoInstallUpdateAsync(string zipPath)
	{
		try
		{
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Auto Install (Beta)" : "自动安装（Beta）"),
				Content = (Translator.IsEnglish ? "Auto Install is still in testing, please backup your current version before using this feature.\n\nThe software will close automatically, then the update script will replace files and restart.\n\nDo not turn off your computer during the update.\n\nContinue?" : "自动安装功能仍在测试中，使用前请尽量备份好原版本。\n\n软件将自动关闭，随后更新脚本会替换文件并重新启动。\n\n更新过程中请勿关闭电脑。\n\n是否继续？"),
				PrimaryButtonText = (Translator.IsEnglish ? "Continue" : "继续"),
				CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
				RequestedTheme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() != ContentDialogResult.Primary)
			{
				return;
			}
			string text = Path.Combine(Path.GetTempPath(), $"DFGE_Update_{DateTime.Now:yyyyMMdd_HHmmss}");
			Directory.CreateDirectory(text);
			ZipFile.ExtractToDirectory(zipPath, text, overwriteFiles: true);
			string value = text;
			string[] directories = Directory.GetDirectories(text);
			if (directories.Length == 1 && Directory.GetFiles(directories[0], "*.exe", SearchOption.TopDirectoryOnly).Length != 0)
			{
				value = directories[0];
			}
			string value2 = AppContext.BaseDirectory.TrimEnd('\\');
			string text2 = Path.Combine(Path.GetTempPath(), "DFGE_Update.bat");
			string value3 = "DLSSFrameGenEnabler_WinUI3.exe";
			string value4 = new Random().Next(100000, 999999).ToString();
			string contents = $"@echo off\necho Updating DLSS5+MFG Enable...\necho.\n\nREM 等待软件进程结束\ntimeout /t 3 /nobreak >nul\ntaskkill /f /im {value3} 2>nul\ntimeout /t 2 /nobreak >nul\n\nREM 备份当前版本\nset BACKUP_DIR=%TEMP%\\DFGE_Backup_{value4}\nmkdir \"%BACKUP_DIR%\" 2>nul\necho Backing up current version...\nxcopy \"{value2}\\*\" \"%BACKUP_DIR%\\\" /E /Y /Q /H >nul 2>&1\n\nREM 复制新文件\necho Copying new files...\nxcopy \"{value}\\*\" \"{value2}\\\" /E /Y /R /H >nul 2>&1\n\nREM 重新启动软件\necho Starting software...\nstart \"\" \"{value2}\\{value3}\"\n\nREM 清理临时文件\ntimeout /t 3 /nobreak >nul\nrmdir /s /q \"{text}\" 2>nul\ndel \"%~f0\" 2>nul\n";
			File.WriteAllText(text2, contents, Encoding.Default);
			Process.Start(new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = "/c \"\"" + text2 + "\"\"",
				UseShellExecute = true,
				CreateNoWindow = false,
				WindowStyle = ProcessWindowStyle.Normal
			});
			Close();
		}
		catch (Exception ex)
		{
			await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Auto Install Failed" : "自动安装失败"),
				Content = string.Format(Translator.IsEnglish ? "Auto install failed: {0}\n\nPlease use manual install instead." : "自动安装失败：{0}\n\n请改用手动安装。", ex.Message),
				CloseButtonText = "OK",
				RequestedTheme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
		}
	}

	private void OpenUrl(string url)
	{
		if (!string.IsNullOrEmpty(url))
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
		}
	}

	private async Task ShowStartupDialog()
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 12.0
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = Translator.T("Dlg_Startup_Text1"),
			TextWrapping = TextWrapping.Wrap
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = Translator.T("Dlg_Startup_Text2"),
			TextWrapping = TextWrapping.Wrap
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = Translator.T("Dlg_Startup_Text3"),
			TextWrapping = TextWrapping.Wrap
		});
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8.0
		};
		stackPanel2.Children.Add(new Image
		{
			Source = new BitmapImage(new Uri("ms-appx:///Assets/github.jpg")),
			Width = 20.0,
			Height = 20.0
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = Translator.T("Dlg_Startup_Github"),
			VerticalAlignment = VerticalAlignment.Center
		});
		HyperlinkButton item = new HyperlinkButton
		{
			Content = stackPanel2,
			NavigateUri = new Uri("https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5"),
			Padding = new Thickness(0.0, 4.0, 0.0, 4.0)
		};
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8.0
		};
		stackPanel3.Children.Add(new Image
		{
			Source = new BitmapImage(new Uri("ms-appx:///Assets/xiaoheihe.png")),
			Width = 20.0,
			Height = 20.0
		});
		stackPanel3.Children.Add(new TextBlock
		{
			Text = Translator.T("Dlg_Startup_Xiaoheihe"),
			VerticalAlignment = VerticalAlignment.Center
		});
		HyperlinkButton item2 = new HyperlinkButton
		{
			Content = stackPanel3,
			NavigateUri = new Uri(SettingsService.Instance.XiaoheiheUrl),
			Padding = new Thickness(0.0, 4.0, 0.0, 4.0)
		};
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(item2);
		if (await new ContentDialog
		{
			Title = Translator.T("Dlg_Startup_Title"),
			Content = stackPanel,
			PrimaryButtonText = Translator.T("Dlg_Startup_Understand"),
			CloseButtonText = Translator.T("Common_Close"),
			XamlRoot = base.Content.XamlRoot,
			RequestedTheme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark)
		}.ShowAsync() == ContentDialogResult.Primary)
		{
			SettingsService.Instance.ShowStartupDialog = false;
			SettingsService.Instance.Save();
		}
	}

	private async Task ShowUsageGuideDialog()
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 12.0
		};
		TextBlock item = new TextBlock
		{
			Text = (Translator.IsEnglish ? "Please read the following notes carefully before using this software:" : "使用本软件前，请仔细阅读以下注意事项："),
			TextWrapping = TextWrapping.Wrap,
			FontWeight = FontWeights.SemiBold
		};
		stackPanel.Children.Add(item);
		string[] array = ((!Translator.IsEnglish) ? new string[6] { "⚠  必须先在游戏设置中开启DLSS/帧生成相关功能，然后关闭游戏，最后再安装补丁。", "⚠  米哈游系列游戏（原神、崩坏：星穹铁道、绝区零等）因反作弊严苛，暂不支持。", "⚠  育碧、EA等平台的游戏可能无法完全适配，是否生效请玩家自行测试。", "⚠  Vulkan渲染的游戏暂未适配。", "⚠  建议备份游戏文件，或在卸载补丁后验证游戏完整性。", "⚠  不建议在有反作弊的网游中使用本软件，可能存在封号风险。" } : new string[6] { "⚠  Must enable DLSS/Frame Generation in game settings FIRST, then close the game, and finally install patches.", "⚠  miHoYo games (Genshin Impact, Honkai: Star Rail, Zenless Zone Zero, etc.) are NOT supported due to strict anti-cheat.", "⚠  Ubisoft and EA games may not be fully compatible, please test by yourself.", "⚠  Vulkan API games are NOT supported yet.", "⚠  It is recommended to backup game files or verify game integrity after uninstalling patches.", "⚠  Do not use this software in online games with anti-cheat, there may be a ban risk." });
		foreach (string text in array)
		{
			TextBlock item2 = new TextBlock
			{
				Text = text,
				TextWrapping = TextWrapping.Wrap,
				FontSize = 13.0
			};
			stackPanel.Children.Add(item2);
		}
		CheckBox dontShowAgain = new CheckBox
		{
			Content = (Translator.IsEnglish ? "Don't show this again" : "不再显示此提示"),
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(dontShowAgain);
		if (await new ContentDialog
		{
			Title = (Translator.IsEnglish ? "Usage Notes" : "使用前说明"),
			Content = stackPanel,
			PrimaryButtonText = (Translator.IsEnglish ? "I Understand" : "我知道了"),
			CloseButtonText = (Translator.IsEnglish ? "Close" : "关闭"),
			XamlRoot = base.Content.XamlRoot,
			RequestedTheme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark)
		}.ShowAsync() == ContentDialogResult.Primary && dontShowAgain.IsChecked == true)
		{
			SettingsService.Instance.ShowUsageGuide = false;
			SettingsService.Instance.Save();
		}
	}

	private async Task ShowBetaFeedbackDialog()
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 12.0
		};
		TextBlock item = new TextBlock
		{
			Text = (Translator.IsEnglish ? "\ud83d\udd2c BETA VERSION" : "\ud83d\udd2c 测试版本"),
			FontSize = 16.0,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, 140, 0))
		};
		stackPanel.Children.Add(item);
		TextBlock item2 = new TextBlock
		{
			Text = (Translator.IsEnglish ? "When you encounter any problems while using this software, please click \"Export Logs to Desktop\" in Settings, then describe the specific cause of the problem through various channels and send the exported log file to the developer, to help better fix the issues!" : "当你在使用软件过程中遇到任何问题，请在设置中点击「导出日志到桌面」，然后通过各种渠道说明问题出现的具体原因并将导出的日志文件发给开发者，以助于更好的修复问题！"),
			TextWrapping = TextWrapping.Wrap,
			FontSize = 14.0
		};
		stackPanel.Children.Add(item2);
		TextBlock item3 = new TextBlock
		{
			Text = (Translator.IsEnglish ? "Steps:" : "操作步骤："),
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(item3);
		string[] array = ((!Translator.IsEnglish) ? new string[4] { "1. 打开设置页面", "2. 拉到最下面，找到「调试与支持」部分", "3. 点击「导出日志到桌面」按钮", "4. 将生成的zip文件连同问题描述一起发给开发者" } : new string[4] { "1. Open Settings page", "2. Scroll to bottom, find \"Debug & Support\" section", "3. Click \"Export Logs to Desktop\" button", "4. Send the generated zip file to developer with problem description" });
		foreach (string text in array)
		{
			TextBlock item4 = new TextBlock
			{
				Text = text,
				TextWrapping = TextWrapping.Wrap,
				FontSize = 13.0,
				Margin = new Thickness(12.0, 2.0, 0.0, 2.0)
			};
			stackPanel.Children.Add(item4);
		}
		TextBlock item5 = new TextBlock
		{
			Text = (Translator.IsEnglish ? "\nYou can contact the developer through GitHub Issues, Bilibili, or Xiaoheihe." : "\n你可以通过GitHub Issues、哔哩哔哩或小黑盒联系开发者。"),
			TextWrapping = TextWrapping.Wrap,
			FontSize = 12.0,
			Foreground = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 128, 128, 128))
		};
		stackPanel.Children.Add(item5);
		CheckBox dontShowAgain = new CheckBox
		{
			Content = (Translator.IsEnglish ? "Don't show this again" : "不再显示此提示"),
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(dontShowAgain);
		if (await new ContentDialog
		{
			Title = (Translator.IsEnglish ? "Beta Version - Feedback Guide" : "测试版 - 反馈指引"),
			Content = stackPanel,
			PrimaryButtonText = (Translator.IsEnglish ? "I Understand" : "我知道了"),
			CloseButtonText = (Translator.IsEnglish ? "Close" : "关闭"),
			XamlRoot = base.Content.XamlRoot,
			RequestedTheme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark)
		}.ShowAsync() == ContentDialogResult.Primary && dontShowAgain.IsChecked == true)
		{
			SettingsService.Instance.ShowBetaFeedback = false;
			SettingsService.Instance.Save();
		}
	}

	private void NavListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isNavigating && NavListView.SelectedItem is ListViewItem { Tag: var tag })
		{
			string text = tag?.ToString();
			if (text != null)
			{
				NavigateTo(text);
				_isNavigating = true;
				FooterListView.SelectedItem = null;
				_isNavigating = false;
			}
		}
	}

	private void FooterListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isNavigating && FooterListView.SelectedItem is ListViewItem { Tag: var tag })
		{
			string text = tag?.ToString();
			if (text != null)
			{
				NavigateTo(text);
				_isNavigating = true;
				NavListView.SelectedItem = null;
				_isNavigating = false;
			}
		}
	}

	private void ToggleNavBtn_Click(object sender, RoutedEventArgs e)
	{
		_isNavCollapsed = !_isNavCollapsed;
		if (_isNavCollapsed)
		{
			TitleText.Opacity = 0.0;
			TitleIcon.Opacity = 1.0;
			ToggleNavText.Visibility = Visibility.Collapsed;
			NavHomeText.Visibility = Visibility.Collapsed;
			NavUsageText.Visibility = Visibility.Collapsed;
			NavUpdateText.Visibility = Visibility.Collapsed;
			NavSettingsText.Visibility = Visibility.Collapsed;
			NavAboutText.Visibility = Visibility.Collapsed;
			_navAnimTarget = 60.0;
		}
		else
		{
			TitleText.Opacity = 1.0;
			TitleIcon.Opacity = 0.0;
			ToggleNavText.Visibility = Visibility.Visible;
			NavHomeText.Visibility = Visibility.Visible;
			NavUsageText.Visibility = Visibility.Visible;
			NavUpdateText.Visibility = Visibility.Visible;
			NavSettingsText.Visibility = Visibility.Visible;
			NavAboutText.Visibility = Visibility.Visible;
			_navAnimTarget = 180.0;
		}
		_navAnimStart = RootGrid.ColumnDefinitions[0].Width.Value;
		_navAnimStartTime = DateTime.Now;
		if (_navAnimTimer == null)
		{
			_navAnimTimer = new DispatcherTimer();
			_navAnimTimer.Interval = TimeSpan.FromMilliseconds(16L);
			_navAnimTimer.Tick += NavAnimTimer_Tick;
		}
		_navAnimTimer.Start();
	}

	private void NavAnimTimer_Tick(object sender, object e)
	{
		double num = Math.Min((DateTime.Now - _navAnimStartTime).TotalMilliseconds / 200.0, 1.0);
		double num2 = ((num < 0.5) ? (2.0 * num * num) : (1.0 - Math.Pow(-2.0 * num + 2.0, 2.0) / 2.0));
		double pixels = _navAnimStart + (_navAnimTarget - _navAnimStart) * num2;
		RootGrid.ColumnDefinitions[0].Width = new GridLength(pixels);
		if (num >= 1.0)
		{
			_navAnimTimer.Stop();
			RootGrid.ColumnDefinitions[0].Width = new GridLength(_navAnimTarget);
		}
	}

	private void NavigateTo(string tag)
	{
		if (!(tag == _currentTag))
		{
			_currentTag = tag;
			Type sourcePageType = tag switch
			{
				"home" => typeof(HomePage), 
				"usage" => typeof(UsagePage), 
				"updatelog" => typeof(UpdateLogPage), 
				"settings" => typeof(SettingsPage), 
				"about" => typeof(AboutPage), 
				_ => typeof(HomePage), 
			};
			if (SettingsService.Instance.EnableAnimation)
			{
				NavFrame.Navigate(sourcePageType, null, new SlideNavigationTransitionInfo
				{
					Effect = SlideNavigationTransitionEffect.FromLeft
				});
			}
			else
			{
				NavFrame.Navigate(sourcePageType, null, new SuppressNavigationTransitionInfo());
			}
		}
	}

	public void SetNavigationAnimation(bool enable)
	{
	}

}
