using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using DLSSFrameGenEnabler_WinUI3.Models;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Navigation;
using WinRT;
using WinRT.Interop;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace DLSSFrameGenEnabler_WinUI3.Pages;

public sealed partial class SettingsPage : Page
{
	private bool _isLoaded;

	private bool _isDisposed;

	public SettingsPage()
	{
		InitializeComponent();
		DarkModeToggle.IsOn = SettingsService.Instance.DarkMode;
		AnimationToggle.IsOn = SettingsService.Instance.EnableAnimation;
		StartupDialogToggle.IsOn = SettingsService.Instance.ShowStartupDialog;
		RememberGamesToggle.IsOn = SettingsService.Instance.RememberGames;
		AutoCheckUpdateToggle.IsOn = SettingsService.Instance.AutoCheckUpdate;
		LanguageCombo.SelectedIndex = SettingsService.Instance.Language;
		int num = SettingsService.Instance.BackdropType;
		if (num == 3)
		{
			num = 0;
		}
		foreach (ComboBoxItem item in BackdropCombo.Items)
		{
			if (item.Tag is string s && int.TryParse(s, out var result) && result == num)
			{
				BackdropCombo.SelectedItem = item;
				break;
			}
		}
		WallpaperButtons.Visibility = ((num != 4) ? Visibility.Collapsed : Visibility.Visible);
		TransparentTipText.Visibility = ((num != 5) ? Visibility.Collapsed : Visibility.Visible);
		UpdateLanguageTexts();
		LoadGpuListAsync();
	}

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);
		Translator.LanguageChanged += UpdateLanguageTexts;
		UpdateLanguageTexts();
		_isLoaded = true;
		_isDisposed = false;
	}

	protected override void OnNavigatedFrom(NavigationEventArgs e)
	{
		base.OnNavigatedFrom(e);
		Translator.LanguageChanged -= UpdateLanguageTexts;
		_isLoaded = false;
		_isDisposed = true;
	}

	private void UpdateLanguageTexts()
	{
		TitleText.Text = Translator.T("Settings_Title");
		AppearanceText.Text = Translator.T("Settings_Appearance");
		DarkModeToggle.Header = Translator.T("Settings_DarkMode");
		DarkModeToggle.OffContent = Translator.T("Settings_LightMode");
		DarkModeToggle.OnContent = Translator.T("Settings_DarkMode");
		AnimationToggle.Header = Translator.T("Settings_Animation");
		AnimationToggle.OffContent = Translator.T("Settings_Off");
		AnimationToggle.OnContent = Translator.T("Settings_On");
		AnimationDescText.Text = Translator.T("Settings_Animation_Desc");
		TransparentTipText.Text = Translator.T("Settings_Transparent_Tip");
		BackdropCombo.Header = Translator.T("Settings_Backdrop");
		LanguageCombo.Header = Translator.T("Settings_Language");
		StartupText.Text = Translator.T("Settings_Startup");
		StartupDialogToggle.Header = Translator.T("Settings_StartupDialog");
		StartupDialogToggle.OffContent = Translator.T("Settings_Off");
		StartupDialogToggle.OnContent = Translator.T("Settings_On");
		RememberGamesToggle.Header = Translator.T("Settings_RememberGames");
		RememberGamesToggle.OffContent = Translator.T("Settings_Off");
		RememberGamesToggle.OnContent = Translator.T("Settings_On");
		GpuText.Text = Translator.T("Settings_Gpu");
		GpuTipText.Text = (Translator.IsEnglish ? "If auto-detection fails, manually select GPU" : "如果自动检测出错，可以手动选择显卡");
		GpuComboBox.Header = Translator.T("Settings_GpuHeader");
		AboutText.Text = Translator.T("Settings_About");
		VersionText.Text = (Translator.IsEnglish ? ("Version: " + App.CurrentVersion) : ("版本：" + App.CurrentVersion));
		DevLangText.Text = Translator.T("About_DevLang");
		DebugText.Text = (Translator.IsEnglish ? "Debug & Support" : "调试与支持");
		ExportLogTipText.Text = (Translator.IsEnglish ? "If the software has issues, you can export log files and send them to the developer to help quickly locate and solve problems" : "如果软件出现问题，可以导出日志文件发给开发者，帮助快速定位和解决问题");
		ExportLogBtn.Content = (Translator.IsEnglish ? "Export Logs to Desktop" : "导出日志到桌面");
		if (LanguageCombo.Items.Count >= 3)
		{
			((ComboBoxItem)LanguageCombo.Items[0]).Content = Translator.T("Settings_Lang_Auto");
			((ComboBoxItem)LanguageCombo.Items[1]).Content = Translator.T("Settings_Lang_Chinese");
			((ComboBoxItem)LanguageCombo.Items[2]).Content = Translator.T("Settings_Lang_English");
		}
		if (BackdropCombo.Items.Count >= 5)
		{
			((ComboBoxItem)BackdropCombo.Items[0]).Content = Translator.T("Settings_Backdrop_Default");
			((ComboBoxItem)BackdropCombo.Items[1]).Content = Translator.T("Settings_Backdrop_Mica");
			((ComboBoxItem)BackdropCombo.Items[2]).Content = Translator.T("Settings_Backdrop_Acrylic");
			((ComboBoxItem)BackdropCombo.Items[3]).Content = Translator.T("Settings_Backdrop_TransparentBlur");
			((ComboBoxItem)BackdropCombo.Items[4]).Content = Translator.T("Settings_Backdrop_Custom");
		}
		SelectWallpaperBtn.Content = Translator.T("Settings_SelectWallpaper");
		ResetWallpaperBtn.Content = Translator.T("Settings_ResetWallpaper");
		if (GpuComboBox.Items.Count > 0)
		{
			GpuComboBox.Items[0] = Translator.T("Settings_AutoDetect");
		}
	}

	private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isLoaded && LanguageCombo.SelectedItem is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out var result))
		{
			SettingsService.Instance.Language = result;
			SettingsService.Instance.Save();
			Translator.CurrentLanguage = (AppLanguage)result;
		}
	}

	private async Task LoadGpuListAsync()
	{
		base.DispatcherQueue.TryEnqueue(delegate
		{
			if (!_isDisposed)
			{
				GpuComboBox.Items.Clear();
				GpuComboBox.Items.Add(Translator.T("Settings_AutoDetect"));
				GpuComboBox.IsEnabled = false;
			}
		});
		GpuInfo[] gpus = await Task.Run(delegate
		{
			try
			{
				return GpuDetector.GetAllGpus();
			}
			catch
			{
				return new GpuInfo[0];
			}
		});
		if (_isDisposed)
		{
			return;
		}
		base.DispatcherQueue.TryEnqueue(delegate
		{
			if (_isDisposed)
			{
				return;
			}
			try
			{
				GpuComboBox.Items.Clear();
				GpuComboBox.Items.Add(Translator.T("Settings_AutoDetect"));
				GpuInfo[] array = gpus;
				foreach (GpuInfo gpuInfo in array)
				{
					GpuComboBox.Items.Add(gpuInfo.Name);
				}
				string selectedGpuName = SettingsService.Instance.SelectedGpuName;
				if (string.IsNullOrEmpty(selectedGpuName))
				{
					GpuComboBox.SelectedIndex = 0;
				}
				else
				{
					GpuComboBox.SelectedItem = selectedGpuName;
					if (GpuComboBox.SelectedItem == null)
					{
						GpuComboBox.SelectedIndex = 0;
					}
				}
				GpuComboBox.IsEnabled = true;
			}
			catch
			{
			}
		});
	}

	private void GpuComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isLoaded)
		{
			if (GpuComboBox.SelectedIndex == 0)
			{
				SettingsService.Instance.SelectedGpuName = "";
			}
			else
			{
				SettingsService.Instance.SelectedGpuName = GpuComboBox.SelectedItem?.ToString() ?? "";
			}
			SettingsService.Instance.Save();
		}
	}

	private void DarkModeToggle_Toggled(object sender, RoutedEventArgs e)
	{
		if (_isLoaded)
		{
			SettingsService.Instance.DarkMode = DarkModeToggle.IsOn;
			SettingsService.Instance.Save();
			if (App.MainWindow is MainWindow mainWindow)
			{
				mainWindow.AnimateThemeChange(DarkModeToggle.IsOn);
			}
			else
			{
				App.ApplyTheme((!DarkModeToggle.IsOn) ? ElementTheme.Light : ElementTheme.Dark);
			}
		}
	}

	private void AnimationToggle_Toggled(object sender, RoutedEventArgs e)
	{
		if (_isLoaded)
		{
			SettingsService.Instance.EnableAnimation = AnimationToggle.IsOn;
			SettingsService.Instance.Save();
			App.ApplyAnimation(AnimationToggle.IsOn);
		}
	}

	private void StartupDialogToggle_Toggled(object sender, RoutedEventArgs e)
	{
		if (_isLoaded)
		{
			SettingsService.Instance.ShowStartupDialog = StartupDialogToggle.IsOn;
			SettingsService.Instance.Save();
		}
	}

	private void RememberGamesToggle_Toggled(object sender, RoutedEventArgs e)
	{
		if (!_isLoaded)
		{
			return;
		}
		SettingsService.Instance.RememberGames = RememberGamesToggle.IsOn;
		SettingsService.Instance.Save();
		if (RememberGamesToggle.IsOn)
		{
			if (App.MainWindow is MainWindow mainWindow && mainWindow.NavFramePublic.Content is HomePage homePage)
			{
				SettingsService.SaveGames(homePage.Games.ToList());
			}
		}
		else
		{
			SettingsService.SaveGames(new List<GameInfo>());
		}
	}

	private void AutoCheckUpdateToggle_Toggled(object sender, RoutedEventArgs e)
	{
		if (_isLoaded)
		{
			SettingsService.Instance.AutoCheckUpdate = AutoCheckUpdateToggle.IsOn;
			SettingsService.Instance.Save();
		}
	}

	private void BackdropCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isLoaded && BackdropCombo.SelectedItem is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out var result))
		{
			SettingsService.Instance.BackdropType = result;
			SettingsService.Instance.Save();
			WallpaperButtons.Visibility = ((result != 4) ? Visibility.Collapsed : Visibility.Visible);
			TransparentTipText.Visibility = ((result != 5) ? Visibility.Collapsed : Visibility.Visible);
			if (App.MainWindow is MainWindow mainWindow)
			{
				mainWindow.ApplyBackdrop(result);
			}
		}
	}

	private async void SelectWallpaperBtn_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			FileOpenPicker obj = new FileOpenPicker
			{
				ViewMode = PickerViewMode.Thumbnail,
				SuggestedStartLocation = PickerLocationId.PicturesLibrary,
				FileTypeFilter = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" }
			};
			nint windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
			InitializeWithWindow.Initialize(obj, windowHandle);
			StorageFile storageFile = await obj.PickSingleFileAsync();
			if (storageFile != null)
			{
				SettingsService.Instance.CustomWallpaperPath = storageFile.Path;
				SettingsService.Instance.Save();
				if (App.MainWindow is MainWindow mainWindow)
				{
					mainWindow.ApplyCustomWallpaper(storageFile.Path);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private void ResetWallpaperBtn_Click(object sender, RoutedEventArgs e)
	{
		SettingsService.Instance.CustomWallpaperPath = "";
		SettingsService.Instance.Save();
		if (App.MainWindow is MainWindow mainWindow)
		{
			mainWindow.ApplyBackdrop(0);
		}
		BackdropCombo.SelectedIndex = 0;
	}

	private async void ExportLogBtn_Click(object sender, RoutedEventArgs e)
	{
		_ = 1;
		try
		{
			ExportLogBtn.IsEnabled = false;
			ExportLogBtn.Content = (Translator.IsEnglish ? "Exporting..." : "正在导出...");
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string text = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			string text2 = Path.Combine(folderPath, "DLSS软件日志_" + text + ".zip");
			string text3 = Path.Combine(Path.GetTempPath(), "DLSS_Logs_" + text);
			Directory.CreateDirectory(text3);
			string baseDirectory = AppContext.BaseDirectory;
			string[] array = new string[2] { "startup.log", "error.log" };
			foreach (string path in array)
			{
				string text4 = Path.Combine(baseDirectory, path);
				if (File.Exists(text4))
				{
					if (new FileInfo(text4).Length > 5242880)
					{
						string[] array2 = File.ReadAllLines(text4);
						string[] contents = array2.Skip(Math.Max(0, array2.Length - 1000)).ToArray();
						File.WriteAllLines(Path.Combine(text3, path), contents);
					}
					else
					{
						File.Copy(text4, Path.Combine(text3, path), overwrite: true);
					}
				}
			}
			try
			{
				string text5 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DLSSFrameGenEnabler");
				if (Directory.Exists(text5))
				{
					string text6 = Path.Combine(text5, "save_debug.log");
					if (File.Exists(text6))
					{
						File.Copy(text6, Path.Combine(text3, "save_debug.log"), overwrite: true);
					}
					string text7 = Path.Combine(text5, "settings.json");
					if (File.Exists(text7))
					{
						File.Copy(text7, Path.Combine(text3, "settings.json"), overwrite: true);
					}
				}
			}
			catch
			{
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("=== 系统信息 / System Information ===");
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
			handler.AppendLiteral("导出时间 / Export Time: ");
			handler.AppendFormatted(DateTime.Now, "yyyy-MM-dd HH:mm:ss");
			stringBuilder3.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("软件版本 / Software Version: ");
			handler.AppendFormatted(App.CurrentVersion);
			stringBuilder4.AppendLine(ref handler);
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("--- 操作系统 / Operating System ---");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder2);
			handler.AppendLiteral("操作系统 / OS: ");
			handler.AppendFormatted(Environment.OSVersion);
			stringBuilder5.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder2);
			handler.AppendLiteral("64位操作系统 / 64-bit OS: ");
			handler.AppendFormatted(Environment.Is64BitOperatingSystem);
			stringBuilder6.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder2);
			handler.AppendLiteral("64位进程 / 64-bit Process: ");
			handler.AppendFormatted(Environment.Is64BitProcess);
			stringBuilder7.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder2);
			handler.AppendLiteral(".NET版本 / .NET Version: ");
			handler.AppendFormatted(Environment.Version);
			stringBuilder8.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder9 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("处理器数量 / Processor Count: ");
			handler.AppendFormatted(Environment.ProcessorCount);
			stringBuilder9.AppendLine(ref handler);
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("--- 软件设置 / Software Settings ---");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder10 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder2);
			handler.AppendLiteral("深色模式 / Dark Mode: ");
			handler.AppendFormatted(SettingsService.Instance.DarkMode);
			stringBuilder10.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder11 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder2);
			handler.AppendLiteral("动画效果 / Animation: ");
			handler.AppendFormatted(SettingsService.Instance.EnableAnimation);
			stringBuilder11.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder12 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(22, 1, stringBuilder2);
			handler.AppendLiteral("界面材质 / Backdrop Type: ");
			handler.AppendFormatted(SettingsService.Instance.BackdropType);
			stringBuilder12.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder13 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder2);
			handler.AppendLiteral("语言 / Language: ");
			handler.AppendFormatted(SettingsService.Instance.Language);
			stringBuilder13.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder14 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("记住游戏列表 / Remember Games: ");
			handler.AppendFormatted(SettingsService.Instance.RememberGames);
			stringBuilder14.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder15 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder2);
			handler.AppendLiteral("启动弹窗 / Startup Dialog: ");
			handler.AppendFormatted(SettingsService.Instance.ShowStartupDialog);
			stringBuilder15.AppendLine(ref handler);
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("--- 显卡信息 / GPU Information ---");
			try
			{
				ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
				try
				{
					foreach (ManagementBaseObject item in managementObjectSearcher.Get())
					{
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder16 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder2);
						handler.AppendLiteral("显卡名称 / GPU Name: ");
						handler.AppendFormatted<object>(item["Name"]);
						stringBuilder16.AppendLine(ref handler);
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder17 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder2);
						handler.AppendLiteral("显存 / VRAM: ");
						handler.AppendFormatted(Math.Round(Convert.ToDouble(item["AdapterRAM"]) / 1024.0 / 1024.0 / 1024.0, 2));
						handler.AppendLiteral(" GB");
						stringBuilder17.AppendLine(ref handler);
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder18 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder2);
						handler.AppendLiteral("驱动版本 / Driver Version: ");
						handler.AppendFormatted<object>(item["DriverVersion"]);
						stringBuilder18.AppendLine(ref handler);
						stringBuilder.AppendLine();
					}
				}
				finally
				{
					((IDisposable)managementObjectSearcher)?.Dispose();
				}
			}
			catch (Exception ex)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder19 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(35, 1, stringBuilder2);
				handler.AppendLiteral("获取显卡信息失败 / Failed to get GPU info: ");
				handler.AppendFormatted(ex.Message);
				stringBuilder19.AppendLine(ref handler);
			}
			File.WriteAllText(Path.Combine(text3, "system_info.txt"), stringBuilder.ToString(), Encoding.UTF8);
			if (File.Exists(text2))
			{
				File.Delete(text2);
			}
			ZipFile.CreateFromDirectory(text3, text2);
			try
			{
				Directory.Delete(text3, recursive: true);
			}
			catch
			{
			}
			await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Export Successful" : "导出成功"),
				Content = (Translator.IsEnglish ? ("Logs have been exported to desktop:\n" + text2 + "\n\nPlease send this file to the developer.") : ("日志已导出到桌面：\n" + text2 + "\n\n请将此文件发送给开发者。")),
				CloseButtonText = "OK",
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
		}
		catch (Exception ex2)
		{
			await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Export Failed" : "导出失败"),
				Content = (Translator.IsEnglish ? ("Failed to export logs: " + ex2.Message + "\n\nPlease check if you have write permission to the desktop.") : ("导出日志失败：" + ex2.Message + "\n\n请检查是否有桌面的写入权限。")),
				CloseButtonText = "OK",
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
		}
		finally
		{
			ExportLogBtn.IsEnabled = true;
			ExportLogBtn.Content = (Translator.IsEnglish ? "Export Logs to Desktop" : "导出日志到桌面");
		}
	}

}
