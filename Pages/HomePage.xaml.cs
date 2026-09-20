using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DLSSFrameGenEnabler_WinUI3.Models;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using WinRT;
using WinRT.Interop;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;

namespace DLSSFrameGenEnabler_WinUI3.Pages;

public sealed partial class HomePage : Page
{
	private class MfgConfig
	{
		public int version { get; set; } = 7;

		public int dynamicTargetFrameRate { get; set; }

		public int multiplier { get; set; } = 4;

		public string mode { get; set; } = "fixed";

		public bool generatedOnlyDebug { get; set; }

		public bool dynamicExperimental56 { get; set; }
	}

	private GpuInfo? _gpuInfo;

	private GameInfo? _contextMenuGame;

	private bool _isDragSelecting;

	private Windows.Foundation.Point _dragStartPoint;

	private List<GameInfo> _preSelectedGames = new List<GameInfo>();

	private List<GameInfo> _rightClickSelectedGames = new List<GameInfo>();

	private bool _isRightClicking;

	private DispatcherTimer? _toastTimer;

	private bool _isPageLoaded;

	public ObservableCollection<GameInfo> Games { get; } = new ObservableCollection<GameInfo>();

	private GameInfo? SelectedGame => GameListView.SelectedItem as GameInfo;

	private void SetGameIcon(GameInfo game)
	{
		if (game == null || string.IsNullOrEmpty(game.ExePath) || !File.Exists(game.ExePath))
		{
			return;
		}
		string exePath = game.ExePath;
		Microsoft.UI.Dispatching.DispatcherQueue dispatcher = base.DispatcherQueue;
		Task.Run(delegate
		{
			byte[] iconBytes = null;
			try
			{
				if (File.Exists(exePath))
				{
					using Icon icon = Icon.ExtractAssociatedIcon(exePath);
					if (icon != null)
					{
						using Bitmap bitmap = icon.ToBitmap();
						using MemoryStream memoryStream = new MemoryStream();
						bitmap.Save(memoryStream, ImageFormat.Png);
						iconBytes = memoryStream.ToArray();
					}
				}
			}
			catch
			{
			}
			if (iconBytes != null)
			{
				dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, delegate
				{
					try
					{
						using MemoryStream stream = new MemoryStream(iconBytes);
						BitmapImage bitmapImage = new BitmapImage();
						bitmapImage.SetSource(stream.AsRandomAccessStream());
						game.Icon = bitmapImage;
					}
					catch
					{
					}
				});
			}
		});
	}

	private async Task<GpuInfo> GetGpuInfoAsync()
	{
		return await Task.Run(delegate
		{
			try
			{
				return GpuDetector.DetectPrimaryGpu();
			}
			catch
			{
				return new GpuInfo
				{
					Name = "未知显卡",
					Series = "Other"
				};
			}
		});
	}

	public HomePage()
	{
		InitializeComponent();
		base.Loaded += delegate
		{
			_isPageLoaded = true;
		};
		GameListView.ItemsSource = Games;
		Translator.LanguageChanged += UpdateLanguageTexts;
		UpdateLanguageTexts();
		if (SettingsService.Instance.RememberGames)
		{
			foreach (GameInfo item in SettingsService.LoadGames())
			{
				if (item.IsREEngine && item.FrameGenEnabled && item.Dlss5Enabled)
				{
					item.FrameGenEnabled = false;
				}
				item.PropertyChanged += Game_PropertyChanged;
				Games.Add(item);
				SetGameIcon(item);
			}
		}
		Games.CollectionChanged += delegate(object? s, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null)
			{
				foreach (GameInfo newItem in e.NewItems)
				{
					newItem.PropertyChanged += Game_PropertyChanged;
				}
			}
			if (SettingsService.Instance.RememberGames)
			{
				SettingsService.SaveGames(Games.ToList());
			}
		};
		Task.Run(delegate
		{
			_gpuInfo = GpuDetector.DetectPrimaryGpu();
		});
	}

	public void SaveGamesNow()
	{
		try
		{
			if (SettingsService.Instance.RememberGames)
			{
				SettingsService.SaveGames(Games.ToList());
			}
		}
		catch (Exception)
		{
		}
	}

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);
		Translator.LanguageChanged += UpdateLanguageTexts;
		UpdateLanguageTexts();
	}

	protected override void OnNavigatedFrom(NavigationEventArgs e)
	{
		base.OnNavigatedFrom(e);
		Translator.LanguageChanged -= UpdateLanguageTexts;
	}

	private void UpdateLanguageTexts()
	{
		bool isEnglish = Translator.IsEnglish;
		AutoScanBtn.Content = Translator.T("Btn_AutoScan");
		ManualAddBtn.Content = Translator.T("Btn_ManualAdd");
		ManualAddDx9Btn.Content = Translator.T("Btn_ManualDx9");
		FrameGenBtn.Content = Translator.T("Btn_FrameGen");
		Dlss5Btn.Content = Translator.T("Btn_Dlss5");
		Dx9Dlss5Btn.Content = Translator.T("Btn_Dx9Dlss5");
		Cp2077Btn.Content = Translator.T("Btn_2077");
		ConfigBtn.Content = Translator.T("Btn_Config");
		ConfigNameBtn.Content = Translator.T("Btn_ConfigName");
		TroubleshootBtn.Content = Translator.T("Btn_Troubleshoot");
		RestoreBtn.Content = Translator.T("Btn_Restore");
		RefreshStatusBtn.Content = Translator.T("Btn_RefreshStatus");
		ClearAllBtn.Content = (isEnglish ? "Clear All" : "清除列表");
		DetectRunningBtn.Content = (isEnglish ? "Detect Running Games" : "检测运行中游戏");
		int selectedIndex = SortComboBox.SelectedIndex;
		SortComboBox.Items.Clear();
		if (isEnglish)
		{
			SortComboBox.Items.Add(new ComboBoxItem
			{
				Content = "Default"
			});
			SortComboBox.Items.Add(new ComboBoxItem
			{
				Content = "Name A-Z"
			});
			SortComboBox.Items.Add(new ComboBoxItem
			{
				Content = "Name Z-A"
			});
		}
		else
		{
			SortComboBox.Items.Add(new ComboBoxItem
			{
				Content = "默认排序"
			});
			SortComboBox.Items.Add(new ComboBoxItem
			{
				Content = "名称 A-Z"
			});
			SortComboBox.Items.Add(new ComboBoxItem
			{
				Content = "名称 Z-A"
			});
		}
		SortComboBox.SelectedIndex = ((selectedIndex >= 0) ? selectedIndex : 0);
		DisclaimerText.Text = Translator.T("Disclaimer");
		ToolTipService.SetToolTip(AutoScanBtn, isEnglish ? "Auto scan games in Steam and Epic libraries" : "自动扫描Steam和Epic游戏库中的游戏");
		ToolTipService.SetToolTip(ManualAddBtn, isEnglish ? "Select game folder, auto-detect game exe" : "选择游戏文件夹，自动识别游戏运行exe");
		ToolTipService.SetToolTip(ManualAddDx9Btn, isEnglish ? "Manually select game exe, for DX9 games or auto-detection failures" : "手动选择游戏exe文件，适用于DX9游戏或自动识别失败的游戏");
		ToolTipService.SetToolTip(FrameGenBtn, isEnglish ? "Enable Frame Generation for selected game" : "为选中游戏开启多帧生成功能");
		ToolTipService.SetToolTip(Dlss5Btn, isEnglish ? "Enable DLSS5 Super Resolution for selected game" : "为选中游戏开启DLSS5超分辨率");
		ToolTipService.SetToolTip(Dx9Dlss5Btn, isEnglish ? "Enable DLSS5 for DX9 games (requires translation)" : "为DX9游戏开启DLSS5（需转译）");
		ToolTipService.SetToolTip(Cp2077Btn, isEnglish ? "Cyberpunk 2077 exclusive frame gen patch" : "赛博朋克2077专用多帧生成补丁");
		ToolTipService.SetToolTip(ConfigBtn, isEnglish ? "Edit frame gen config (Classic mode only)" : "编辑多帧生成配置文件（仅经典模式）");
		ToolTipService.SetToolTip(ConfigNameBtn, isEnglish ? "Modify patch DLL config name (Advanced mode only)" : "修改补丁DLL配置名（仅高级模式）");
		ToolTipService.SetToolTip(TroubleshootBtn, isEnglish ? "Replace sl. prefixed patch files for troubleshooting" : "替换sl.开头的补丁文件进行排错");
		ToolTipService.SetToolTip(RestoreBtn, isEnglish ? "Restore all installed patches" : "还原所有已安装的补丁");
		ToolTipService.SetToolTip(RefreshStatusBtn, isEnglish ? "Re-detect patch status for all games" : "重新检测所有游戏的补丁开启状态");
		ToolTipService.SetToolTip(DetectRunningBtn, isEnglish ? "Detect currently running game processes, 100% accurate game exe identification" : "检测当前正在运行的游戏进程，100%准确识别游戏真正的exe");
		foreach (GameInfo game in Games)
		{
			game.RefreshStatus();
		}
		if (SelectedGame != null)
		{
			UpdateButtonStates(SelectedGame);
		}
	}

	private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (SettingsService.Instance.RememberGames)
		{
			SettingsService.SaveGames(Games.ToList());
		}
	}

	private async void AutoScanBtn_Click(object sender, RoutedEventArgs e)
	{
		List<GameInfo> obj = await Task.Run(delegate
		{
			List<GameInfo> first = GameScanner.ScanSteamGames();
			List<GameInfo> second = GameScanner.ScanEpicGames();
			List<GameInfo> second2 = GameScanner.ScanGOGGames();
			List<GameInfo> list = Enumerable.Concat(second: GameScanner.ScanUbisoftGames(), first: first.Concat(second).Concat(second2)).ToList();
			List<GameInfo> list2 = new List<GameInfo>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (GameInfo item2 in list)
			{
				string item = item2.GamePath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant();
				string text = ((!string.IsNullOrEmpty(item2.ExePath)) ? item2.ExePath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant() : "");
				if (!hashSet.Contains(item) && (string.IsNullOrEmpty(text) || !hashSet.Contains(text)))
				{
					hashSet.Add(item);
					if (!string.IsNullOrEmpty(text))
					{
						hashSet.Add(text);
					}
					list2.Add(item2);
				}
			}
			return list2;
		});
		int num = 0;
		foreach (GameInfo item3 in obj)
		{
			string normalizedGamePath = item3.GamePath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant();
			string normalizedExePath = ((!string.IsNullOrEmpty(item3.ExePath)) ? item3.ExePath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant() : "");
			if (!Games.Any((GameInfo g) => g.GamePath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant() == normalizedGamePath || (!string.IsNullOrEmpty(normalizedExePath) && !string.IsNullOrEmpty(g.ExePath) && g.ExePath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant() == normalizedExePath)))
			{
				Games.Add(item3);
				SetGameIcon(item3);
				num++;
			}
		}
		SaveGamesNow();
		int count = Games.Count;
		string message = (Translator.IsEnglish ? $"Scan complete! Added {num} new games, total {count} games in list." : $"扫描完成！新增 {num} 个游戏，列表中共有 {count} 个游戏。");
		ShowMessage(message);
	}

	private async void RefreshStatusBtn_Click(object sender, RoutedEventArgs e)
	{
		await Task.Run(delegate
		{
			foreach (GameInfo game in Games)
			{
				try
				{
					string exeDir = Path.GetDirectoryName(game.ExePath) ?? game.GamePath;
					game.FrameGenEnabled = GameScanner.CheckFrameGenEnabled(game.GamePath, exeDir);
					game.Dlss5Enabled = GameScanner.CheckDLSS5Enabled(game.GamePath, exeDir);
					game.Dx9Dlss5Enabled = GameScanner.CheckDX9DLSS5Enabled(game.GamePath, exeDir);
				}
				catch
				{
				}
			}
		});
		if (SelectedGame != null)
		{
			UpdateButtonStates(SelectedGame);
		}
		ShowMessage(Translator.T("Dlg_Refresh_Done"));
	}

	private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isPageLoaded || SortComboBox.SelectedItem == null)
		{
			return;
		}
		ComboBoxItem comboBoxItem = SortComboBox.SelectedItem as ComboBoxItem;
		if (comboBoxItem == null)
		{
			return;
		}
		string text = comboBoxItem.Content?.ToString() ?? "";
		List<GameInfo> list = GameListView.SelectedItems.Cast<GameInfo>().ToList();
		GameInfo selectedGame = SelectedGame;
		List<GameInfo> list2 = Games.ToList();
		switch (text)
		{
		case "名称 A-Z":
		case "Name A-Z":
			list2 = list2.OrderBy<GameInfo, string>((GameInfo g) => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
			break;
		case "名称 Z-A":
		case "Name Z-A":
			list2 = list2.OrderByDescending<GameInfo, string>((GameInfo g) => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
			break;
		case "状态优先":
		case "Status First":
			list2 = list2.OrderByDescending((GameInfo g) => g.FrameGenEnabled || g.Dlss5Enabled || g.Dx9Dlss5Enabled).ThenBy<GameInfo, string>((GameInfo g) => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
			break;
		}
		Games.Clear();
		foreach (GameInfo item in list2)
		{
			Games.Add(item);
		}
		if (selectedGame != null && Games.Contains(selectedGame))
		{
			GameListView.SelectedItem = selectedGame;
		}
		foreach (GameInfo item2 in list)
		{
			if (Games.Contains(item2) && !GameListView.SelectedItems.Contains(item2))
			{
				GameListView.SelectedItems.Add(item2);
			}
		}
		SaveGamesNow();
	}

	private void HomePage_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (FocusManager.GetFocusedElement() is TextBox)
		{
			return;
		}
		if (e.Key == VirtualKey.A && (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
		{
			e.Handled = true;
			GameListView.SelectedItems.Clear();
			{
				foreach (GameInfo game in Games)
				{
					GameListView.SelectedItems.Add(game);
				}
				return;
			}
		}
		if (e.Key == VirtualKey.Delete)
		{
			e.Handled = true;
			CtxRemove_Click(sender, new RoutedEventArgs());
		}
		else if (e.Key == VirtualKey.F5)
		{
			e.Handled = true;
			RefreshStatusBtn_Click(sender, new RoutedEventArgs());
		}
	}

	private async void ManualAddBtn_Click(object sender, RoutedEventArgs e)
	{
		FolderPicker folderPicker = new FolderPicker();
		nint windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
		InitializeWithWindow.Initialize(folderPicker, windowHandle);
		folderPicker.FileTypeFilter.Add("*");
		StorageFolder storageFolder = await folderPicker.PickSingleFolderAsync();
		if (storageFolder != null)
		{
			GameInfo gameInfo = GameScanner.AddGameByDirectory(storageFolder.Path);
			string normalizedPath = storageFolder.Path.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant();
			if (gameInfo != null && !Games.Any((GameInfo g) => g.GamePath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant() == normalizedPath))
			{
				Games.Add(gameInfo);
				SetGameIcon(gameInfo);
				SaveGamesNow();
				ShowMessage(Translator.T("Dlg_Add_Success"));
			}
			else
			{
				ShowMessage(Translator.T("Dlg_Add_Fail"));
			}
		}
	}

	private async void ManualAddDx9Btn_Click(object sender, RoutedEventArgs e)
	{
		FileOpenPicker fileOpenPicker = new FileOpenPicker();
		nint windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
		InitializeWithWindow.Initialize(fileOpenPicker, windowHandle);
		fileOpenPicker.FileTypeFilter.Add(".exe");
		StorageFile storageFile = await fileOpenPicker.PickSingleFileAsync();
		if (storageFile != null)
		{
			GameInfo gameInfo = GameScanner.AddGameByExe(storageFile.Path);
			string normalizedExePath = storageFile.Path.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant();
			if (gameInfo != null && !Games.Any((GameInfo g) => !string.IsNullOrEmpty(g.ExePath) && g.ExePath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant() == normalizedExePath))
			{
				Games.Add(gameInfo);
				SetGameIcon(gameInfo);
				SaveGamesNow();
				ShowMessage(Translator.T("Dlg_Add_Success"));
			}
		}
	}

	private async void DetectRunningBtn_Click(object sender, RoutedEventArgs e)
	{
		_ = 3;
		try
		{
			List<ProcessDetector.GameProcessInfo> runningGames;
			try
			{
				runningGames = ProcessDetector.GetRunningGames();
			}
			catch (Exception ex)
			{
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Error" : "错误"),
					Content = (Translator.IsEnglish ? ("Failed to detect processes: " + ex.Message) : ("检测进程失败：" + ex.Message)),
					CloseButtonText = "OK",
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				return;
			}
			if (runningGames.Count == 0)
			{
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "No Games Found" : "未检测到游戏"),
					Content = (Translator.IsEnglish ? "No running games detected. Please start the game first and try again." : "未检测到正在运行的游戏。请先启动游戏，然后再点击此按钮。"),
					CloseButtonText = "OK",
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				return;
			}
			ListView processList = new ListView
			{
				SelectionMode = ListViewSelectionMode.Single,
				Height = 300.0
			};
			foreach (ProcessDetector.GameProcessInfo item3 in runningGames)
			{
				ListViewItem listViewItem = new ListViewItem
				{
					Tag = item3,
					Padding = new Thickness(4.0)
				};
				StackPanel stackPanel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Spacing = 10.0
				};
				if (item3.Icon != null)
				{
					Microsoft.UI.Xaml.Controls.Image item = new Microsoft.UI.Xaml.Controls.Image
					{
						Source = item3.Icon,
						Width = 24.0,
						Height = 24.0,
						VerticalAlignment = VerticalAlignment.Center
					};
					stackPanel.Children.Add(item);
				}
				StackPanel stackPanel2 = new StackPanel();
				TextBlock textBlock = new TextBlock
				{
					Text = item3.ProcessName,
					FontWeight = FontWeights.Bold,
					VerticalAlignment = VerticalAlignment.Center
				};
				if (!string.IsNullOrEmpty(item3.WindowTitle))
				{
					textBlock.Text = item3.ProcessName + " [" + item3.WindowTitle + "]";
				}
				stackPanel2.Children.Add(textBlock);
				TextBlock item2 = new TextBlock
				{
					Text = $"{item3.MemoryMB} MB  -  {item3.ExePath}",
					FontSize = 11.0,
					Opacity = 0.7,
					TextTrimming = TextTrimming.CharacterEllipsis
				};
				stackPanel2.Children.Add(item2);
				stackPanel.Children.Add(stackPanel2);
				listViewItem.Content = stackPanel;
				processList.Items.Add(listViewItem);
			}
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Select Running Game" : "选择正在运行的游戏"),
				Content = processList,
				PrimaryButtonText = (Translator.IsEnglish ? "Add Selected" : "添加选中"),
				CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() != ContentDialogResult.Primary || !(processList.SelectedItem is ListViewItem { Tag: var tag }))
			{
				return;
			}
			ProcessDetector.GameProcessInfo selectedProc = tag as ProcessDetector.GameProcessInfo;
			if (selectedProc == null)
			{
				return;
			}
			if (Games.Any((GameInfo g) => g.ExePath.Equals(selectedProc.ExePath, StringComparison.OrdinalIgnoreCase)))
			{
				ShowMessage(Translator.IsEnglish ? "Game already in list" : "该游戏已在列表中");
				return;
			}
			GameInfo gameInfo = GameScanner.AddGameByExe(selectedProc.ExePath);
			if (gameInfo != null)
			{
				Games.Add(gameInfo);
				SetGameIcon(gameInfo);
				SaveGamesNow();
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Game is Running" : "游戏正在运行"),
					Content = (Translator.IsEnglish ? $"Detected game is currently running.\n\nGame: {gameInfo.Name}\nExe: {selectedProc.ExePath}\n\n⚠ Important: Please first enable DLSS/Frame Generation in game settings, then close the game before installing patches, otherwise files may be locked and installation may fail." : $"检测到游戏当前正在运行。\n\n游戏：{gameInfo.Name}\n路径：{selectedProc.ExePath}\n\n⚠ 重要提示：请先在游戏设置中开启DLSS/帧生成相关功能，然后再关闭游戏，最后再安装补丁，否则文件可能被占用导致安装失败。"),
					CloseButtonText = "OK",
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				ShowMessage(Translator.T("Dlg_Add_Success"));
			}
			else
			{
				ShowMessage(Translator.IsEnglish ? "Failed to add game" : "添加游戏失败");
			}
		}
		catch (Exception ex2)
		{
			ShowMessage("检测运行中游戏出错：" + ex2.Message + "\n\n堆栈跟踪：\n" + ex2.StackTrace);
		}
	}

	private void GameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		GameInfo selectedGame = SelectedGame;
		if (selectedGame == null)
		{
			ActionPanel.Visibility = Visibility.Collapsed;
			return;
		}
		ActionPanel.Visibility = Visibility.Visible;
		UpdateButtonStates(selectedGame);
	}

	private void GameMenuFlyout_Opening(object sender, object e)
	{
		MenuFlyout menuFlyout = (MenuFlyout)sender;
		_contextMenuGame = null;
		if (menuFlyout.Target == null)
		{
			return;
		}
		FrameworkElement target = menuFlyout.Target;
		if ((object)target == null || !(target.DataContext is GameInfo gameInfo))
		{
			return;
		}
		_contextMenuGame = gameInfo;
		if (_rightClickSelectedGames.Count > 1 && _rightClickSelectedGames.Contains(gameInfo))
		{
			GameListView.SelectedItems.Clear();
			foreach (GameInfo rightClickSelectedGame in _rightClickSelectedGames)
			{
				if (Games.Contains(rightClickSelectedGame))
				{
					GameListView.SelectedItems.Add(rightClickSelectedGame);
				}
			}
		}
		else
		{
			GameListView.SelectedItem = gameInfo;
		}
		bool flag = _rightClickSelectedGames.Count > 1 && _rightClickSelectedGames.Contains(gameInfo);
		IList<MenuFlyoutItemBase> items = menuFlyout.Items;
		if (items.Count < 12)
		{
			return;
		}
		bool isEnglish = Translator.IsEnglish;
		if (flag)
		{
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i] is MenuFlyoutItem menuFlyoutItem)
				{
					menuFlyoutItem.IsEnabled = i == 12;
				}
			}
			if (items.Count > 12 && items[12] is MenuFlyoutItem menuFlyoutItem2)
			{
				menuFlyoutItem2.Text = (isEnglish ? $"Remove Selected ({_rightClickSelectedGames.Count})" : $"移除所选游戏 ({_rightClickSelectedGames.Count})");
			}
			return;
		}
		MenuFlyoutItem menuFlyoutItem3 = (MenuFlyoutItem)items[0];
		menuFlyoutItem3.Text = ((!gameInfo.FrameGenEnabled) ? (isEnglish ? "Enable Frame Gen" : "一键开启多帧生成") : (isEnglish ? "Restore Frame Gen" : "一键还原多帧生成"));
		menuFlyoutItem3.IsEnabled = gameInfo.SupportsFrameGen || gameInfo.FrameGenEnabled;
		menuFlyoutItem3.Visibility = (gameInfo.Is2077 ? Visibility.Collapsed : Visibility.Visible);
		if (gameInfo.IsREEngine && gameInfo.Dlss5Enabled && !gameInfo.FrameGenEnabled)
		{
			menuFlyoutItem3.IsEnabled = false;
		}
		MenuFlyoutItem menuFlyoutItem4 = (MenuFlyoutItem)items[1];
		menuFlyoutItem4.Text = ((!gameInfo.Dlss5Enabled) ? (isEnglish ? "Enable DLSS5" : "开启DLSS5") : (isEnglish ? "Restore DLSS5" : "一键还原DLSS5"));
		if (gameInfo.IsREEngine && gameInfo.FrameGenEnabled && !gameInfo.Dlss5Enabled)
		{
			menuFlyoutItem4.IsEnabled = false;
		}
		else
		{
			menuFlyoutItem4.IsEnabled = true;
		}
		MenuFlyoutItem obj = (MenuFlyoutItem)items[2];
		obj.Text = ((!gameInfo.Dx9Dlss5Enabled) ? (isEnglish ? "Enable DX9 DLSS5 (for old games)" : "开启DX9 DLSS5（适合老游戏）") : (isEnglish ? "Restore DX9 DLSS5" : "一键还原DX9 DLSS5"));
		obj.Visibility = (gameInfo.Is2077 ? Visibility.Collapsed : Visibility.Visible);
		MenuFlyoutItem obj2 = (MenuFlyoutItem)items[3];
		obj2.Text = ((!gameInfo.FrameGenEnabled) ? (isEnglish ? "2077 Exclusive Patch" : "2077专用补丁") : (isEnglish ? "Restore 2077 Patch" : "还原2077补丁"));
		obj2.Visibility = ((!gameInfo.Is2077) ? Visibility.Collapsed : Visibility.Visible);
		MenuFlyoutItem obj3 = (MenuFlyoutItem)items[5];
		obj3.Text = (isEnglish ? "Configure Frame Gen" : "配置多帧生成");
		obj3.IsEnabled = gameInfo.FrameGenEnabled && gameInfo.FrameGenMode == "经典模式";
		MenuFlyoutItem obj4 = (MenuFlyoutItem)items[6];
		obj4.Text = (isEnglish ? "Modify Config Name" : "配置名修改");
		obj4.IsEnabled = gameInfo.FrameGenEnabled && gameInfo.FrameGenMode == "高级模式";
		MenuFlyoutItem obj5 = (MenuFlyoutItem)items[7];
		obj5.Text = (isEnglish ? "Troubleshooting" : "排错修复");
		obj5.IsEnabled = gameInfo.FrameGenEnabled;
		MenuFlyoutItem obj6 = (MenuFlyoutItem)items[9];
		obj6.Text = (isEnglish ? "One-Click Restore" : "一键还原");
		obj6.IsEnabled = gameInfo.FrameGenEnabled || gameInfo.Dlss5Enabled || gameInfo.Dx9Dlss5Enabled;
		if (items.Count > 10)
		{
			MenuFlyoutItem obj7 = (MenuFlyoutItem)items[10];
			obj7.Text = (isEnglish ? "Launch Game" : "启动游戏");
			obj7.IsEnabled = true;
		}
		if (items.Count > 11)
		{
			((MenuFlyoutItem)items[11]).Text = (isEnglish ? "Open Game Folder" : "打开游戏目录");
		}
		if (items.Count > 12)
		{
			((MenuFlyoutItem)items[12]).Text = (isEnglish ? "Remove from List" : "移除列表");
			if (items.Count > 13 && items[13] is MenuFlyoutItem menuFlyoutItem5)
			{
				menuFlyoutItem5.Text = (isEnglish ? "Edit Remark/Tags" : "编辑备注/标签");
			}
		}
	}

	private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
	{
		DependencyObject parent = VisualTreeHelper.GetParent(child);
		while (parent != null)
		{
			if (parent is T result)
			{
				return result;
			}
			parent = VisualTreeHelper.GetParent(parent);
		}
		return null;
	}

	private void UpdateButtonStates(GameInfo game)
	{
		if (GameListView.SelectedItems.Count > 1)
		{
			FrameGenBtn.IsEnabled = false;
			Dlss5Btn.IsEnabled = false;
			Dx9Dlss5Btn.IsEnabled = false;
			Cp2077Btn.IsEnabled = false;
			ConfigBtn.IsEnabled = false;
			ConfigNameBtn.IsEnabled = false;
			TroubleshootBtn.IsEnabled = false;
			RestoreBtn.IsEnabled = false;
			string value = (Translator.IsEnglish ? "Multiple games selected. Only 'Remove from list' is available via right-click menu." : "已选择多个游戏，仅可通过右键菜单使用「移除列表」功能。");
			ToolTipService.SetToolTip(FrameGenBtn, value);
			ToolTipService.SetToolTip(Dlss5Btn, value);
			ToolTipService.SetToolTip(Dx9Dlss5Btn, value);
			ToolTipService.SetToolTip(RestoreBtn, value);
			return;
		}
		if (game.Is2077)
		{
			Cp2077Btn.Visibility = Visibility.Visible;
			Cp2077Btn.IsEnabled = true;
			FrameGenBtn.Visibility = Visibility.Collapsed;
			Dlss5Btn.Visibility = Visibility.Visible;
			Dx9Dlss5Btn.Visibility = Visibility.Collapsed;
			ConfigBtn.IsEnabled = false;
			ConfigNameBtn.IsEnabled = false;
			ToolTipService.SetToolTip(Cp2077Btn, null);
			Cp2077Btn.Content = ((!game.FrameGenEnabled) ? Translator.T("Btn_2077") : (Translator.IsEnglish ? "Restore 2077 Patch" : "还原2077补丁"));
		}
		else
		{
			FrameGenBtn.Visibility = Visibility.Visible;
			Dlss5Btn.Visibility = Visibility.Visible;
			Dx9Dlss5Btn.Visibility = Visibility.Visible;
			Dx9Dlss5Btn.IsEnabled = true;
			Cp2077Btn.Visibility = Visibility.Collapsed;
			ConfigBtn.IsEnabled = game.FrameGenEnabled && game.FrameGenMode == "经典模式";
			ConfigNameBtn.IsEnabled = game.FrameGenEnabled && game.FrameGenMode == "高级模式";
			ToolTipService.SetToolTip(Dx9Dlss5Btn, Translator.IsEnglish ? "For older DX9 games, uses dgVoodoo to translate DX9 to DX11" : "适合老游戏（DX9游戏），通过dgVoodoo将DX9转译到DX11运行");
			ToolTipService.SetToolTip(FrameGenBtn, null);
			ToolTipService.SetToolTip(Dlss5Btn, null);
			ToolTipService.SetToolTip(RestoreBtn, null);
		}
		FrameGenBtn.Content = ((!game.FrameGenEnabled) ? Translator.T("Btn_FrameGen") : (Translator.IsEnglish ? "Restore Frame Gen" : "一键还原多帧生成"));
		FrameGenBtn.IsEnabled = game.SupportsFrameGen || game.FrameGenEnabled;
		if (game.IsREEngine)
		{
			if (game.Dlss5Enabled && !game.FrameGenEnabled)
			{
				FrameGenBtn.IsEnabled = false;
				ToolTipService.SetToolTip(FrameGenBtn, Translator.IsEnglish ? "RE Engine games: DLSS5 conflicts with Frame Generation, cannot enable both" : "RE引擎游戏：DLSS5与多帧生成冲突，无法同时开启");
			}
			else if (game.FrameGenEnabled && !game.Dlss5Enabled)
			{
				Dlss5Btn.IsEnabled = false;
				ToolTipService.SetToolTip(Dlss5Btn, Translator.IsEnglish ? "RE Engine games: DLSS5 conflicts with Frame Generation, cannot enable both" : "RE引擎游戏：DLSS5与多帧生成冲突，无法同时开启");
			}
			else
			{
				Dlss5Btn.IsEnabled = true;
				ToolTipService.SetToolTip(Dlss5Btn, Translator.IsEnglish ? "Enable DLSS5 Super Resolution for selected game" : "为选中游戏开启DLSS5超分辨率");
			}
		}
		else
		{
			Dlss5Btn.IsEnabled = true;
		}
		Dlss5Btn.Content = ((!game.Dlss5Enabled) ? Translator.T("Btn_Dlss5") : (Translator.IsEnglish ? "Restore DLSS5" : "一键还原DLSS5"));
		Dx9Dlss5Btn.Content = ((!game.Dx9Dlss5Enabled) ? Translator.T("Btn_Dx9Dlss5") : (Translator.IsEnglish ? "Restore DX9 DLSS5" : "一键还原DX9 DLSS5"));
		TroubleshootBtn.IsEnabled = game.FrameGenEnabled;
		RestoreBtn.IsEnabled = game.FrameGenEnabled || game.Dlss5Enabled || game.Dx9Dlss5Enabled;
	}

	private async void FrameGenBtn_Click(object sender, RoutedEventArgs e)
	{
		_ = 6;
		try
		{
			GameInfo game = SelectedGame;
			if (game == null)
			{
				return;
			}
			if (game.FrameGenEnabled)
			{
				if (await ConfirmAndRestoreFrameGen(game))
				{
					ShowMessage(Translator.T("Dlg_FrameGen_Restored"));
					game.FrameGenEnabled = false;
					UpdateButtonStates(game);
				}
				return;
			}
			if (game.IsREEngine)
			{
				if (game.Dlss5Enabled)
				{
					await new ContentDialog
					{
						Title = Translator.T("Dlg_Title_Warning"),
						Content = (Translator.IsEnglish ? "RE Engine games: DLSS5 conflicts with Frame Generation. Please restore DLSS5 first, then enable Frame Generation." : "RE引擎游戏：DLSS5与多帧生成冲突，请先还原DLSS5，再开启多帧生成。"),
						CloseButtonText = Translator.T("Common_Close"),
						XamlRoot = base.Content.XamlRoot
					}.ShowAsync();
					return;
				}
				switch (await ShowModeDialog(Translator.T("Dlg_FrameGen_Title"), new(string, string)[3]
				{
					(Translator.T("Dlg_FrameGen_RE"), Translator.IsEnglish ? "RE Engine exclusive patch (conflicts with DLSS5, cannot enable both)" : "RE引擎专属补丁（与DLSS5冲突，无法同时开启）"),
					(Translator.T("Dlg_FrameGen_Classic"), Translator.T("Dlg_FrameGen_ClassicDesc")),
					(Translator.T("Dlg_FrameGen_Advanced"), Translator.T("Dlg_FrameGen_AdvancedDesc"))
				}))
				{
				case 0:
					FilePatcher.InstallREEngineFrameGen(game);
					await new ContentDialog
					{
						Title = Translator.T("Dlg_Title_Tip"),
						Content = (Translator.IsEnglish ? "RE Engine Frame Generation enabled. Note: DLSS5 is now disabled due to conflict." : "RE引擎多帧生成已开启。注意：由于冲突，DLSS5功能已被禁用，无法同时开启。"),
						CloseButtonText = Translator.T("Common_Close"),
						XamlRoot = base.Content.XamlRoot
					}.ShowAsync();
					break;
				case 1:
					FilePatcher.InstallClassicFrameGen(game);
					break;
				case 2:
					FilePatcher.InstallAdvancedFrameGen(game);
					break;
				default:
					return;
				}
			}
			else
			{
				GpuInfo gpuInfo = await GetGpuInfoAsync();
				if (!gpuInfo.IsNvidia)
				{
					await new ContentDialog
					{
						Title = Translator.T("Dlg_Title_Warning"),
						Content = (Translator.IsEnglish ? "Frame Generation is only supported on NVIDIA RTX 20/30/40/50 series graphics cards." : "多帧生成功能仅支持NVIDIA RTX 20/30/40/50系显卡。"),
						CloseButtonText = Translator.T("Common_Close"),
						XamlRoot = base.Content.XamlRoot
					}.ShowAsync();
					return;
				}
				if (gpuInfo.Series == "RTX20")
				{
					FilePatcher.InstallRTX20FrameGen(game);
				}
				else if (gpuInfo.Series == "RTX30")
				{
					FilePatcher.InstallRTX30FrameGen(game);
				}
				else
				{
					switch (await ShowModeDialog(Translator.T("Dlg_FrameGen_Title"), new(string, string)[2]
					{
						(Translator.T("Dlg_FrameGen_Classic"), Translator.T("Dlg_FrameGen_ClassicDesc")),
						(Translator.T("Dlg_FrameGen_Advanced"), Translator.T("Dlg_FrameGen_AdvancedDesc"))
					}))
					{
					case 0:
						FilePatcher.InstallClassicFrameGen(game);
						break;
					case 1:
						FilePatcher.InstallAdvancedFrameGen(game);
						break;
					default:
						return;
					}
				}
			}
			ShowMessage(Translator.T("Dlg_FrameGen_Installed"));
			game.FrameGenEnabled = true;
			UpdateButtonStates(game);
		}
		catch (Exception ex)
		{
			ShowMessage(Translator.T("Common_Error") + ": " + ex.Message);
		}
	}

	private async void Dlss5Btn_Click(object sender, RoutedEventArgs e)
	{
		GameInfo game = SelectedGame;
		if (game == null)
		{
			return;
		}
		if (game.Dlss5Enabled)
		{
			if (await ConfirmAndRestoreDLSS5(game))
			{
				ShowMessage(Translator.T("Dlg_Dlss5_Restored"));
				game.Dlss5Enabled = false;
				UpdateButtonStates(game);
			}
			return;
		}
		if (game.IsYanYun)
		{
			FilePatcher.InstallYanYunDLSS5(game);
			ShowMessage(Translator.T("Dlg_Dlss5_Installed"));
			game.Dlss5Enabled = true;
			UpdateButtonStates(game);
			return;
		}
		if (game.IsREEngine)
		{
			if (game.FrameGenEnabled)
			{
				await new ContentDialog
				{
					Title = Translator.T("Dlg_Title_Warning"),
					Content = (Translator.IsEnglish ? "RE Engine games: DLSS5 conflicts with Frame Generation. Please restore Frame Generation first, then enable DLSS5." : "RE引擎游戏：DLSS5与多帧生成冲突，请先还原多帧生成，再开启DLSS5。"),
					CloseButtonText = Translator.T("Common_Close"),
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
				return;
			}
			if (await new ContentDialog
			{
				Title = Translator.T("Dlg_Title_Tip"),
				Content = Translator.T("Dlg_Dlss5_REWarn"),
				PrimaryButtonText = (Translator.IsEnglish ? "Continue" : "继续安装"),
				CloseButtonText = Translator.T("Common_Cancel"),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() == ContentDialogResult.Primary)
			{
				FilePatcher.InstallREEngineDLSS5(game);
				await new ContentDialog
				{
					Title = Translator.T("Dlg_Title_Tip"),
					Content = (Translator.IsEnglish ? "RE Engine DLSS5 enabled. Note: Frame Generation is now disabled due to conflict." : "RE引擎DLSS5已开启。注意：由于冲突，多帧生成功能已被禁用，无法同时开启。"),
					CloseButtonText = Translator.T("Common_Close"),
					XamlRoot = base.Content.XamlRoot
				}.ShowAsync();
			}
			UpdateButtonStates(game);
			return;
		}
		GpuInfo gpuInfo = await GetGpuInfoAsync();
		if (gpuInfo.IsNvidia)
		{
			FilePatcher.InstallCommonDLSS5(game);
			ShowMessage(Translator.T("Dlg_Dlss5_Installed"));
		}
		else if (gpuInfo.IsAmd && gpuInfo.SupportsDLSS5)
		{
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "AMD GPU Notice" : "AMD显卡提示"),
				Content = Translator.T("Dlg_Dlss5_AmdWarn"),
				PrimaryButtonText = (Translator.IsEnglish ? "Continue" : "继续安装"),
				CloseButtonText = Translator.T("Common_Cancel"),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() == ContentDialogResult.Primary)
			{
				FilePatcher.InstallAmdDLSS5(game);
				ShowMessage(Translator.T("Dlg_Dlss5_AmdSetup"));
			}
		}
		else
		{
			ShowMessage(Translator.T("Dlg_Dlss5_GpuNotSupported"));
		}
		UpdateButtonStates(game);
	}

	private async void Dx9Dlss5Btn_Click(object sender, RoutedEventArgs e)
	{
		GameInfo game = SelectedGame;
		if (game == null)
		{
			return;
		}
		if (game.Dx9Dlss5Enabled)
		{
			switch (await ShowModeDialog(Translator.IsEnglish ? "DX9 DLSS5 Options" : "DX9 DLSS5 选项", new(string, string)[2]
			{
				(Translator.IsEnglish ? "Restore DX9 DLSS5" : "还原DX9 DLSS5", Translator.IsEnglish ? "Remove all DX9 DLSS5 patches" : "还原DX9 DLSS5（删除所有补丁）"),
				(Translator.IsEnglish ? "Replace DLL" : "替换DLL", Translator.IsEnglish ? "Move dgVoodoo files to game's DLL directory (for games where patches don't work in exe directory)" : "替换dll（将dgVoodoo三个补丁移到游戏运行dll目录，适用于补丁不生效的情况）")
			}))
			{
			case 0:
				if (await ConfirmAndRestoreDLSS5(game))
				{
					ShowMessage(Translator.T("Dlg_Dx9_Restored"));
				}
				break;
			case 1:
				if (FilePatcher.ReplaceDgvoodooDll(game))
				{
					ShowMessage(Translator.IsEnglish ? "dgVoodoo files moved to game's DLL directory. Please test the game again." : "已将dgVoodoo三个补丁移到游戏运行dll目录，请重新测试游戏是否生效。");
				}
				else
				{
					ShowMessage(Translator.IsEnglish ? "Could not find game's DLL directory." : "未找到游戏运行dll目录，替换失败。");
				}
				break;
			default:
				return;
			}
			UpdateButtonStates(game);
		}
		else
		{
			switch (await ShowModeDialog(Translator.IsEnglish ? "Select DX9 DLSS5 Mode" : "选择DX9 DLSS5模式", new(string, string)[2]
			{
				(Translator.IsEnglish ? "32-bit" : "32位", Translator.IsEnglish ? "For 32-bit DX9 games" : "适用于32位DX9游戏"),
				(Translator.IsEnglish ? "64-bit" : "64位", Translator.IsEnglish ? "For 64-bit DX9 games" : "适用于64位DX9游戏")
			}))
			{
			case 0:
				FilePatcher.InstallDX9DLSS5_32(game);
				break;
			case 1:
				FilePatcher.InstallDX9DLSS5_64(game);
				break;
			default:
				return;
			}
			ShowMessage(Translator.T("Dlg_Dx9_Installed"));
			game.Dx9Dlss5Enabled = true;
			UpdateButtonStates(game);
		}
	}

	private async void Cp2077Btn_Click(object sender, RoutedEventArgs e)
	{
		GameInfo game = SelectedGame;
		if (game == null)
		{
			return;
		}
		if (game.FrameGenEnabled)
		{
			if (await ConfirmAndRestoreFrameGen(game))
			{
				ShowMessage(Translator.T("Dlg_2077_Restored"));
				game.FrameGenEnabled = false;
				UpdateButtonStates(game);
			}
		}
		else
		{
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "Install 2077 Exclusive Patch" : "安装2077专用补丁"),
				Content = (Translator.IsEnglish ? "Are you sure you want to install the Cyberpunk 2077 exclusive frame generation patch?\n\nPlease make sure you have enabled DLSS/Frame Generation in the game settings first." : "确定要安装赛博朋克2077专用多帧生成补丁吗？\n\n请确保已经先在游戏设置中开启了DLSS/帧生成相关功能。"),
				PrimaryButtonText = (Translator.IsEnglish ? "Install" : "安装"),
				CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() != ContentDialogResult.Primary)
			{
				return;
			}
			FilePatcher.Install2077FrameGen(game);
			await new ContentDialog
			{
				Title = Translator.T("Dlg_2077_Usage"),
				Content = Translator.T("Dlg_2077_UsageText"),
				CloseButtonText = (Translator.IsEnglish ? "Got it" : "我知道了"),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
			game.FrameGenEnabled = true;
			UpdateButtonStates(game);
		}
		UpdateButtonStates(game);
	}

	private async void ConfigBtn_Click(object sender, RoutedEventArgs e)
	{
		GameInfo selectedGame = SelectedGame;
		if (selectedGame == null)
		{
			return;
		}
		string directoryName = Path.GetDirectoryName(selectedGame.ExePath);
		string configPath = Path.Combine(directoryName, "RTX40MFG_config.json");
		if (!File.Exists(configPath))
		{
			ShowMessage(Translator.T("Dlg_Config_NotFound"));
			return;
		}
		MfgConfig config;
		try
		{
			string json = File.ReadAllText(configPath);
			config = JsonSerializer.Deserialize<MfgConfig>(json) ?? new MfgConfig();
		}
		catch
		{
			ShowMessage(Translator.T("Dlg_Config_ReadFail"));
			return;
		}
		ComboBox multiplierCombo = new ComboBox
		{
			Header = Translator.T("Dlg_Config_Multiplier")
		};
		multiplierCombo.Items.Add(Translator.IsEnglish ? "2x" : "2倍");
		multiplierCombo.Items.Add(Translator.IsEnglish ? "3x" : "3倍");
		multiplierCombo.Items.Add(Translator.IsEnglish ? "4x (Recommended)" : "4倍（推荐）");
		multiplierCombo.Items.Add(Translator.IsEnglish ? "5x" : "5倍");
		multiplierCombo.Items.Add(Translator.IsEnglish ? "6x (Experimental)" : "6倍（实验性）");
		multiplierCombo.SelectedIndex = Math.Clamp(config.multiplier - 2, 0, 4);
		ComboBox modeCombo = new ComboBox
		{
			Header = Translator.T("Dlg_Config_Mode")
		};
		modeCombo.Items.Add(Translator.IsEnglish ? "fixed - Fixed multiplier" : "fixed - 固定倍率");
		modeCombo.Items.Add(Translator.IsEnglish ? "dynamic - Dynamic frame gen" : "dynamic - 动态多帧生成");
		modeCombo.SelectedIndex = ((config.mode == "dynamic") ? 1 : 0);
		ComboBox targetFpsCombo = new ComboBox
		{
			Header = (Translator.IsEnglish ? "Dynamic target FPS (dynamic mode only)" : "动态目标帧率（仅dynamic模式生效）"),
			IsEnabled = (config.mode == "dynamic")
		};
		targetFpsCombo.Items.Add(Translator.IsEnglish ? "0 - Auto (monitor refresh rate)" : "0 - 自动跟随显示器刷新率");
		targetFpsCombo.Items.Add("120");
		targetFpsCombo.Items.Add("144");
		targetFpsCombo.Items.Add("165");
		targetFpsCombo.Items.Add("240");
		int[] fpsItems = new int[5] { 0, 120, 144, 165, 240 };
		targetFpsCombo.SelectedIndex = Math.Max(0, Array.IndexOf(fpsItems, config.dynamicTargetFrameRate));
		ToggleSwitch expToggle = new ToggleSwitch
		{
			Header = Translator.T("Dlg_Config_Exp"),
			IsOn = config.dynamicExperimental56,
			OffContent = Translator.T("Settings_Off"),
			OnContent = Translator.T("Settings_On")
		};
		modeCombo.SelectionChanged += delegate
		{
			targetFpsCombo.IsEnabled = modeCombo.SelectedIndex == 1;
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 16.0
		};
		stackPanel.Children.Add(multiplierCombo);
		stackPanel.Children.Add(modeCombo);
		stackPanel.Children.Add(targetFpsCombo);
		stackPanel.Children.Add(expToggle);
		if (await new ContentDialog
		{
			Title = Translator.T("Dlg_Config_Title"),
			Content = stackPanel,
			PrimaryButtonText = Translator.T("Common_Save"),
			CloseButtonText = Translator.T("Common_Cancel"),
			XamlRoot = base.Content.XamlRoot
		}.ShowAsync() != ContentDialogResult.Primary)
		{
			return;
		}
		config.multiplier = multiplierCombo.SelectedIndex + 2;
		config.mode = ((modeCombo.SelectedIndex == 1) ? "dynamic" : "fixed");
		config.dynamicTargetFrameRate = fpsItems[targetFpsCombo.SelectedIndex];
		config.dynamicExperimental56 = expToggle.IsOn;
		try
		{
			string contents = JsonSerializer.Serialize(config, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(configPath, contents);
			ShowMessage(Translator.T("Dlg_Config_Saved"));
		}
		catch
		{
			ShowMessage(Translator.T("Dlg_Config_SaveFail"));
		}
	}

	private async void ConfigNameBtn_Click(object sender, RoutedEventArgs e)
	{
		GameInfo selectedGame = SelectedGame;
		if (selectedGame == null)
		{
			return;
		}
		if (selectedGame.FrameGenMode != "高级模式" && selectedGame.FrameGenMode != "Advanced Mode")
		{
			ShowMessage(Translator.T("Dlg_ConfigName_OnlyAdvanced"));
			return;
		}
		string exeDir = Path.GetDirectoryName(selectedGame.ExePath);
		if (await new ContentDialog
		{
			Title = Translator.T("Dlg_ConfigName_TipTitle"),
			Content = Translator.T("Dlg_ConfigName_TipText"),
			PrimaryButtonText = Translator.T("Dlg_ConfigName_IKnow"),
			CloseButtonText = Translator.T("Common_Cancel"),
			XamlRoot = base.Content.XamlRoot
		}.ShowAsync() != ContentDialogResult.Primary)
		{
			return;
		}
		string[] configNames = new string[12]
		{
			"dinput8", "d3d11", "winmm", "d3d9", "winhttp", "wininet", "dsound", "binkw64", "xinput1_3", "bink2w64",
			"xinput1_4", "xinputuap"
		};
		ComboBox nameCombo = new ComboBox
		{
			Header = Translator.T("Dlg_ConfigName_SelectName")
		};
		string[] array = configNames;
		foreach (string item in array)
		{
			nameCombo.Items.Add(item);
		}
		nameCombo.SelectedIndex = 0;
		ContentDialog dialog = null;
		Button button = new Button
		{
			Content = (Translator.IsEnglish ? "Rename dxgi.dll → d3d12.dll (click to execute)" : "修改dxgi.dll → d3d12.dll（单独操作，点击即执行）"),
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		button.Click += delegate
		{
			dialog?.Hide();
			string text = Path.Combine(exeDir, "dxgi.dll");
			string text2 = Path.Combine(exeDir, "d3d12.dll");
			if (!File.Exists(text))
			{
				ShowMessage(Translator.T("Dlg_ConfigName_DxgiNotFound"));
			}
			else if (File.Exists(text2))
			{
				ShowMessage(Translator.T("Dlg_ConfigName_DxgiConflict"));
			}
			else
			{
				File.Move(text, text2);
				ShowMessage(Translator.T("Dlg_ConfigName_DxgiDone"));
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 12.0
		};
		stackPanel.Children.Add(nameCombo);
		stackPanel.Children.Add(button);
		dialog = new ContentDialog
		{
			Title = Translator.T("Dlg_ConfigName_Title"),
			Content = stackPanel,
			PrimaryButtonText = Translator.T("Dlg_ConfigName_ModifyVersion"),
			CloseButtonText = Translator.T("Common_Close"),
			XamlRoot = base.Content.XamlRoot
		};
		if (await dialog.ShowAsync() != ContentDialogResult.Primary)
		{
			return;
		}
		string newName = configNames[nameCombo.SelectedIndex];
		string versionDll = Path.Combine(exeDir, "version.dll");
		string versionIni = Path.Combine(exeDir, "version.ini");
		string newDll = Path.Combine(exeDir, newName + ".dll");
		string newIni = Path.Combine(exeDir, newName + ".ini");
		if (!File.Exists(versionDll) && !File.Exists(versionIni))
		{
			ShowMessage(Translator.T("Dlg_ConfigName_NotFound"));
		}
		else if (File.Exists(newDll) || File.Exists(newIni))
		{
			ShowMessage(string.Format(Translator.T("Dlg_ConfigName_Conflict"), newName));
		}
		else if (await new ContentDialog
		{
			Title = Translator.T("Dlg_Title_Confirm"),
			Content = string.Format(Translator.T("Dlg_ConfigName_Confirm"), newName),
			PrimaryButtonText = Translator.T("Common_Yes"),
			CloseButtonText = Translator.T("Common_No"),
			XamlRoot = base.Content.XamlRoot
		}.ShowAsync() == ContentDialogResult.Primary)
		{
			if (File.Exists(versionDll))
			{
				File.Move(versionDll, newDll);
			}
			if (File.Exists(versionIni))
			{
				File.Move(versionIni, newIni);
			}
			ShowMessage(string.Format(Translator.T("Dlg_ConfigName_Done"), newName));
		}
	}

	private async void TroubleshootBtn_Click(object sender, RoutedEventArgs e)
	{
		GameInfo game = SelectedGame;
		if (game == null)
		{
			return;
		}
		if (!game.FrameGenEnabled)
		{
			ShowMessage(Translator.T("Dlg_Trouble_NeedFrameGen"));
			return;
		}
		List<(string, bool)> troubleshootFiles = FilePatcher.GetTroubleshootFiles(game);
		if (troubleshootFiles.Count == 0)
		{
			ShowMessage(Translator.T("Dlg_Trouble_NoPatch"));
			return;
		}
		List<CheckBox> checkBoxes = new List<CheckBox>();
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 4.0
		};
		CheckBox checkBox = new CheckBox
		{
			Content = Translator.T("Dlg_Trouble_SelectAll"),
			IsChecked = false,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		foreach (var item5 in troubleshootFiles)
		{
			string item = item5.Item1;
			bool item2 = item5.Item2;
			CheckBox item3 = new CheckBox
			{
				Content = item + (item2 ? "" : (Translator.IsEnglish ? " (not in game, display only)" : "（游戏中不存在，仅展示）")),
				IsEnabled = item2,
				IsChecked = item2,
				Opacity = (item2 ? 1.0 : 0.5)
			};
			checkBoxes.Add(item3);
			stackPanel.Children.Add(item3);
		}
		checkBox.Checked += delegate
		{
			foreach (CheckBox item6 in checkBoxes)
			{
				if (item6.IsEnabled)
				{
					item6.IsChecked = true;
				}
			}
		};
		checkBox.Unchecked += delegate
		{
			foreach (CheckBox item7 in checkBoxes)
			{
				if (item7.IsEnabled)
				{
					item7.IsChecked = false;
				}
			}
		};
		ScrollViewer item4 = new ScrollViewer
		{
			Content = stackPanel,
			MaxHeight = 300.0,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Spacing = 8.0
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = Translator.T("Dlg_Trouble_SelectTip"),
			TextWrapping = TextWrapping.Wrap
		});
		stackPanel2.Children.Add(checkBox);
		stackPanel2.Children.Add(item4);
		if (await new ContentDialog
		{
			Title = Translator.T("Dlg_Trouble_Title"),
			Content = stackPanel2,
			PrimaryButtonText = Translator.T("Dlg_Trouble_ReplaceSelected"),
			CloseButtonText = Translator.T("Common_Cancel"),
			XamlRoot = base.Content.XamlRoot
		}.ShowAsync() != ContentDialogResult.Primary)
		{
			return;
		}
		List<string> list = (from f in checkBoxes.Where((CheckBox cb) => cb.IsChecked == true && cb.IsEnabled).Select(delegate(CheckBox cb)
			{
				object content = cb.Content;
				object obj;
				if (content == null)
				{
					obj = null;
				}
				else
				{
					string? text = content.ToString();
					obj = ((text != null) ? text.Split('（')[0] : null);
				}
				if (obj == null)
				{
					obj = "";
				}
				return (string)obj;
			})
			where !string.IsNullOrEmpty(f)
			select f).ToList();
		if (list.Count == 0)
		{
			ShowMessage(Translator.T("Dlg_Trouble_NoSelect"));
			return;
		}
		int num = FilePatcher.TroubleshootSelected(game, list);
		ShowMessage(string.Format(Translator.T("Dlg_Trouble_Done"), num));
	}

	private async void RestoreBtn_Click(object sender, RoutedEventArgs e)
	{
		GameInfo game = SelectedGame;
		if (game == null)
		{
			return;
		}
		if (FilePatcher.HasBackup(game))
		{
			(List<string> filesToDelete, List<string> filesToRestore, List<string> dirsToDelete) tuple = FilePatcher.PreviewRestoreFrameGen(game);
			(List<string>, List<string>, List<string>) tuple2 = FilePatcher.PreviewRestoreDLSS5(game);
			List<string> filesToDelete = tuple.filesToDelete.Concat(tuple2.Item1).Distinct().ToList();
			List<string> filesToRestore = tuple.filesToRestore.Concat(tuple2.Item2).Distinct().ToList();
			List<string> dirsToDelete = tuple.dirsToDelete.Concat(tuple2.Item3).Distinct().ToList();
			if (await ShowRestoreConfirmDialog(Translator.T("Dlg_Restore_Title"), filesToDelete, filesToRestore, dirsToDelete))
			{
				FilePatcher.RestoreFrameGen(game);
				FilePatcher.RestoreDLSS5(game);
				ShowMessage(Translator.T("Dlg_Restore_Done"));
				game.FrameGenEnabled = false;
				game.Dlss5Enabled = false;
				game.Dx9Dlss5Enabled = false;
				UpdateButtonStates(game);
			}
		}
		else if (await new ContentDialog
		{
			Title = Translator.T("Dlg_Restore_NoResponsibility_Title"),
			Content = Translator.T("Dlg_Restore_NoResponsibility_Warn"),
			PrimaryButtonText = (Translator.IsEnglish ? "Confirm Delete" : "确认删除"),
			CloseButtonText = Translator.T("Common_Cancel"),
			XamlRoot = base.Content.XamlRoot
		}.ShowAsync() == ContentDialogResult.Primary)
		{
			FilePatcher.ForceClean(game);
			ShowMessage(Translator.T("Dlg_Restore_NoResp_Done"));
			game.FrameGenEnabled = false;
			game.Dlss5Enabled = false;
			game.Dx9Dlss5Enabled = false;
			UpdateButtonStates(game);
		}
	}

	private async Task<bool> ShowRestoreConfirmDialog(string title, List<string> filesToDelete, List<string> filesToRestore, List<string> dirsToDelete)
	{
		ContentDialog contentDialog = new ContentDialog
		{
			Title = title,
			PrimaryButtonText = (Translator.IsEnglish ? "Confirm Restore" : "确定还原"),
			CloseButtonText = Translator.T("Common_Cancel"),
			XamlRoot = base.Content.XamlRoot
		};
		ScrollViewer scrollViewer = new ScrollViewer
		{
			MaxHeight = 400.0,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 12.0
		};
		TextBlock item = new TextBlock
		{
			Text = (Translator.IsEnglish ? "⚠\ufe0f Warning: This operation will delete the following files and restore original files. Please make sure these are not your personal mod files!" : "⚠\ufe0f 警告：此操作将删除以下文件并恢复原始文件，请确认这些不是您的个人mod文件！"),
			TextWrapping = TextWrapping.Wrap,
			Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(byte.MaxValue, byte.MaxValue, 150, 50))
		};
		stackPanel.Children.Add(item);
		if (filesToRestore.Count > 0)
		{
			TextBlock item2 = new TextBlock
			{
				Text = (Translator.IsEnglish ? $"\ud83d\udcc1 Files to be restored ({filesToRestore.Count}):" : $"\ud83d\udcc1 将被恢复的文件（{filesToRestore.Count}个）："),
				FontWeight = new FontWeight
				{
					Weight = 700
				}
			};
			stackPanel.Children.Add(item2);
			StackPanel stackPanel2 = new StackPanel
			{
				Spacing = 2.0
			};
			foreach (string item5 in filesToRestore.Take(20))
			{
				stackPanel2.Children.Add(new TextBlock
				{
					Text = "  " + item5,
					FontSize = 11.0,
					TextWrapping = TextWrapping.Wrap
				});
			}
			if (filesToRestore.Count > 20)
			{
				stackPanel2.Children.Add(new TextBlock
				{
					Text = $"  ... and {filesToRestore.Count - 20} more files",
					FontSize = 11.0,
					Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(byte.MaxValue, 150, 150, 150))
				});
			}
			stackPanel.Children.Add(stackPanel2);
		}
		if (filesToDelete.Count > 0)
		{
			TextBlock item3 = new TextBlock
			{
				Text = (Translator.IsEnglish ? $"\ud83d\uddd1\ufe0f Files to be deleted ({filesToDelete.Count}):" : $"\ud83d\uddd1\ufe0f 将被删除的文件（{filesToDelete.Count}个）："),
				FontWeight = new FontWeight
				{
					Weight = 700
				}
			};
			stackPanel.Children.Add(item3);
			StackPanel stackPanel3 = new StackPanel
			{
				Spacing = 2.0
			};
			foreach (string item6 in filesToDelete.Take(20))
			{
				stackPanel3.Children.Add(new TextBlock
				{
					Text = "  " + item6,
					FontSize = 11.0,
					TextWrapping = TextWrapping.Wrap
				});
			}
			if (filesToDelete.Count > 20)
			{
				stackPanel3.Children.Add(new TextBlock
				{
					Text = $"  ... and {filesToDelete.Count - 20} more files",
					FontSize = 11.0,
					Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(byte.MaxValue, 150, 150, 150))
				});
			}
			stackPanel.Children.Add(stackPanel3);
		}
		if (dirsToDelete.Count > 0)
		{
			TextBlock item4 = new TextBlock
			{
				Text = (Translator.IsEnglish ? $"\ud83d\udcc2 Directories to be deleted ({dirsToDelete.Count}):" : $"\ud83d\udcc2 将被删除的目录（{dirsToDelete.Count}个）："),
				FontWeight = new FontWeight
				{
					Weight = 700
				}
			};
			stackPanel.Children.Add(item4);
			StackPanel stackPanel4 = new StackPanel
			{
				Spacing = 2.0
			};
			foreach (string item7 in dirsToDelete)
			{
				stackPanel4.Children.Add(new TextBlock
				{
					Text = "  " + item7,
					FontSize = 11.0,
					TextWrapping = TextWrapping.Wrap
				});
			}
			stackPanel.Children.Add(stackPanel4);
		}
		if (filesToDelete.Count == 0 && filesToRestore.Count == 0 && dirsToDelete.Count == 0)
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = (Translator.IsEnglish ? "No patch files found. The game may already be in original state." : "未找到补丁文件，游戏可能已经是原始状态。"),
				TextWrapping = TextWrapping.Wrap
			});
		}
		scrollViewer.Content = stackPanel;
		contentDialog.Content = scrollViewer;
		return await contentDialog.ShowAsync() == ContentDialogResult.Primary;
	}

	private async Task<bool> ConfirmAndRestoreFrameGen(GameInfo game)
	{
		(List<string>, List<string>, List<string>) tuple = FilePatcher.PreviewRestoreFrameGen(game);
		if (await ShowRestoreConfirmDialog(Translator.IsEnglish ? "Restore Frame Generation" : "还原多帧生成", tuple.Item1, tuple.Item2, tuple.Item3))
		{
			return FilePatcher.RestoreFrameGen(game);
		}
		return false;
	}

	private async Task<bool> ConfirmAndRestoreDLSS5(GameInfo game)
	{
		(List<string>, List<string>, List<string>) tuple = FilePatcher.PreviewRestoreDLSS5(game);
		if (await ShowRestoreConfirmDialog(Translator.IsEnglish ? "Restore DLSS5" : "还原DLSS5", tuple.Item1, tuple.Item2, tuple.Item3))
		{
			return FilePatcher.RestoreDLSS5(game);
		}
		return false;
	}

	private async Task<int> ShowModeDialog(string title, (string name, string desc)[] options)
	{
		ContentDialog contentDialog = new ContentDialog
		{
			Title = title,
			PrimaryButtonText = Translator.T("Common_Confirm"),
			CloseButtonText = Translator.T("Common_Cancel"),
			XamlRoot = base.Content.XamlRoot
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 8.0
		};
		List<RadioButton> radioButtons = new List<RadioButton>();
		for (int i = 0; i < options.Length; i++)
		{
			RadioButton item = new RadioButton
			{
				Content = options[i].name + "\n" + options[i].desc,
				IsChecked = (i == 0),
				Tag = i
			};
			radioButtons.Add(item);
			stackPanel.Children.Add(item);
		}
		contentDialog.Content = stackPanel;
		if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
		{
			return radioButtons.IndexOf(radioButtons.First((RadioButton r) => r.IsChecked == true));
		}
		return -1;
	}

	private void ShowMessage(string message)
	{
		try
		{
			if (_toastTimer == null)
			{
				_toastTimer = new DispatcherTimer();
				_toastTimer.Interval = TimeSpan.FromSeconds(3L);
				_toastTimer.Tick += delegate
				{
					_toastTimer?.Stop();
					ToastBorder.Visibility = Visibility.Collapsed;
				};
			}
			else
			{
				_toastTimer.Stop();
			}
			ToastText.Text = message;
			ToastBorder.Opacity = 0.95;
			ToastBorder.Visibility = Visibility.Visible;
			_toastTimer.Start();
		}
		catch
		{
			try
			{
				ContentDialog contentDialog = new ContentDialog();
				contentDialog.Title = Translator.T("Dlg_Title_Tip");
				contentDialog.Content = message;
				contentDialog.CloseButtonText = Translator.T("Common_Confirm");
				contentDialog.XamlRoot = base.Content.XamlRoot;
				contentDialog.ShowAsync();
			}
			catch
			{
			}
		}
	}

	private void CtxFrameGen_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		FrameGenBtn_Click(sender, e);
	}

	private void CtxDlss5_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		Dlss5Btn_Click(sender, e);
	}

	private void CtxDx9Dlss5_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		Dx9Dlss5Btn_Click(sender, e);
	}

	private void Ctx2077_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		Cp2077Btn_Click(sender, e);
	}

	private void CtxConfig_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		ConfigBtn_Click(sender, e);
	}

	private void CtxConfigName_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		ConfigNameBtn_Click(sender, e);
	}

	private void CtxTroubleshoot_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		TroubleshootBtn_Click(sender, e);
	}

	private void CtxRestore_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		RestoreBtn_Click(sender, e);
	}

	private async void CtxLaunchGame_Click(object sender, RoutedEventArgs e)
	{
		GameInfo gameInfo = _contextMenuGame ?? SelectedGame;
		if (gameInfo == null)
		{
			return;
		}
		try
		{
			string text = gameInfo.ExePath ?? "";
			if (string.IsNullOrEmpty(text) || !File.Exists(text))
			{
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Launch Failed" : "启动失败"),
					Content = (Translator.IsEnglish ? "Game executable not found. Please check if the game path is correct." : "找不到游戏可执行文件，请检查游戏路径是否正确。"),
					CloseButtonText = "OK",
					XamlRoot = base.XamlRoot
				}.ShowAsync();
				return;
			}
			int num = 0;
			object obj = default(object);
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = text,
					UseShellExecute = true,
					WorkingDirectory = (Path.GetDirectoryName(text) ?? "")
				});
			}
			catch (Exception ex)
			{
				obj = ex;
				num = 1;
			}
			if (num == 1)
			{
				Exception ex2 = (Exception)obj;
				await new ContentDialog
				{
					Title = (Translator.IsEnglish ? "Launch Failed" : "启动失败"),
					Content = (Translator.IsEnglish ? "Failed to launch game: " : "启动游戏失败：") + ex2.Message,
					CloseButtonText = "OK",
					XamlRoot = base.XamlRoot
				}.ShowAsync();
			}
		}
		catch
		{
		}
	}

	private void CtxOpenFolder_Click(object sender, RoutedEventArgs e)
	{
		GameInfo gameInfo = _contextMenuGame ?? SelectedGame;
		if (gameInfo == null)
		{
			return;
		}
		try
		{
			string text = "";
			if (!string.IsNullOrEmpty(gameInfo.ExePath) && File.Exists(gameInfo.ExePath))
			{
				text = Path.GetDirectoryName(gameInfo.ExePath) ?? "";
			}
			if (string.IsNullOrEmpty(text) || !Directory.Exists(text))
			{
				text = gameInfo.GamePath;
				if (File.Exists(text))
				{
					text = Path.GetDirectoryName(text) ?? text;
				}
			}
			if (!string.IsNullOrEmpty(text) && !Path.IsPathRooted(text))
			{
				text = Path.GetFullPath(text);
			}
			if (!string.IsNullOrEmpty(text) && Directory.Exists(text))
			{
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = text,
						UseShellExecute = true
					});
					return;
				}
				catch
				{
				}
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "explorer.exe",
						Arguments = "\"" + text + "\"",
						UseShellExecute = true
					});
					return;
				}
				catch
				{
				}
				Process.Start("explorer.exe", text);
			}
			else
			{
				ShowMessage(Translator.IsEnglish ? ("Game folder not found.\nGamePath: " + gameInfo.GamePath + "\nExePath: " + gameInfo.ExePath) : ("游戏目录不存在。\nGamePath: " + gameInfo.GamePath + "\nExePath: " + gameInfo.ExePath));
			}
		}
		catch (Exception ex)
		{
			ShowMessage(Translator.IsEnglish ? ("Failed to open folder: " + ex.Message) : ("打开目录失败：" + ex.Message));
		}
	}

	private async void CtxEditRemark_Click(object sender, RoutedEventArgs e)
	{
		GameInfo game = _contextMenuGame ?? SelectedGame;
		if (game != null)
		{
			StackPanel stackPanel = new StackPanel
			{
				Spacing = 12.0
			};
			TextBlock item = new TextBlock
			{
				Text = (Translator.IsEnglish ? "Remark" : "备注"),
				FontWeight = FontWeights.SemiBold
			};
			TextBox remarkBox = new TextBox
			{
				Text = (game.Remark ?? ""),
				PlaceholderText = (Translator.IsEnglish ? "Enter remark..." : "输入备注..."),
				AcceptsReturn = true,
				Height = 60.0,
				TextWrapping = TextWrapping.Wrap
			};
			TextBlock item2 = new TextBlock
			{
				Text = (Translator.IsEnglish ? "Tags (comma separated)" : "标签（用逗号分隔）"),
				FontWeight = FontWeights.SemiBold
			};
			TextBox tagsBox = new TextBox
			{
				Text = (game.Tags ?? ""),
				PlaceholderText = (Translator.IsEnglish ? "e.g. finished, multiplayer, favorite" : "例如：已通关, 多人, 收藏")
			};
			stackPanel.Children.Add(item);
			stackPanel.Children.Add(remarkBox);
			stackPanel.Children.Add(item2);
			stackPanel.Children.Add(tagsBox);
			if (await new ContentDialog
			{
				Title = (Translator.IsEnglish ? ("Edit Remark/Tags - " + game.Name) : ("编辑备注/标签 - " + game.Name)),
				Content = stackPanel,
				PrimaryButtonText = (Translator.IsEnglish ? "Save" : "保存"),
				CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync() == ContentDialogResult.Primary)
			{
				game.Remark = remarkBox.Text.Trim();
				game.Tags = tagsBox.Text.Trim();
				SaveGamesNow();
				ShowMessage(Translator.IsEnglish ? "Remark/Tags saved" : "备注/标签已保存");
			}
		}
	}

	private void EnsureContextGame()
	{
		if (_contextMenuGame != null)
		{
			GameListView.SelectedItem = _contextMenuGame;
		}
	}

	private void CtxRemove_Click(object sender, RoutedEventArgs e)
	{
		List<GameInfo> list = GameListView.SelectedItems.Cast<GameInfo>().ToList();
		if (list.Count > 0)
		{
			foreach (GameInfo item in list)
			{
				Games.Remove(item);
			}
			SaveGamesNow();
			ShowMessage(Translator.IsEnglish ? $"Removed {list.Count} games from list" : $"已从列表中移除 {list.Count} 个游戏");
		}
		else
		{
			GameInfo contextMenuGame = _contextMenuGame;
			if (contextMenuGame != null)
			{
				Games.Remove(contextMenuGame);
				SaveGamesNow();
			}
		}
	}

	private async void ClearAllBtn_Click(object sender, RoutedEventArgs e)
	{
		if (Games.Count == 0)
		{
			ShowMessage(Translator.IsEnglish ? "Game list is already empty" : "游戏列表已经是空的");
		}
		else if (await new ContentDialog
		{
			Title = (Translator.IsEnglish ? "Clear Game List" : "清除游戏列表"),
			Content = (Translator.IsEnglish ? $"Are you sure you want to clear all {Games.Count} games from the list? This action cannot be undone." : $"确定要清除列表中的所有 {Games.Count} 个游戏吗？此操作不可撤销。"),
			PrimaryButtonText = (Translator.IsEnglish ? "Clear All" : "全部清除"),
			CloseButtonText = (Translator.IsEnglish ? "Cancel" : "取消"),
			XamlRoot = base.Content.XamlRoot,
			RequestedTheme = ((!SettingsService.Instance.DarkMode) ? ElementTheme.Light : ElementTheme.Dark)
		}.ShowAsync() == ContentDialogResult.Primary)
		{
			Games.Clear();
			SaveGamesNow();
			ShowMessage(Translator.IsEnglish ? "Game list cleared" : "游戏列表已清除");
		}
	}

	private void GameListView_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		PointerPoint currentPoint = e.GetCurrentPoint(GameListView);
		if (currentPoint.Properties.IsRightButtonPressed)
		{
			_isRightClicking = true;
			_rightClickSelectedGames = GameListView.SelectedItems.Cast<GameInfo>().ToList();
		}
		else
		{
			if (!currentPoint.Properties.IsLeftButtonPressed)
			{
				return;
			}
			IEnumerable<UIElement> enumerable = VisualTreeHelper.FindElementsInHostCoordinates(currentPoint.Position, GameListView);
			bool flag = false;
			foreach (UIElement item in enumerable)
			{
				if (item is ListViewItem)
				{
					flag = true;
					break;
				}
				DependencyObject dependencyObject = item;
				while (dependencyObject != null)
				{
					if (dependencyObject is ListViewItem)
					{
						flag = true;
						break;
					}
					dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
				}
				if (flag)
				{
					break;
				}
			}
			bool flag2 = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
			if (!flag)
			{
				_isDragSelecting = true;
				_dragStartPoint = currentPoint.Position;
				_preSelectedGames = GameListView.SelectedItems.Cast<GameInfo>().ToList();
				if (!flag2)
				{
					GameListView.SelectedItems.Clear();
				}
				try
				{
					GameListView.CapturePointer(e.Pointer);
				}
				catch
				{
				}
			}
		}
	}

	private void GameListView_PointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (!_isDragSelecting)
		{
			return;
		}
		Windows.Foundation.Point position = e.GetCurrentPoint(GameListView).Position;
		double x = Math.Min(_dragStartPoint.X, position.X);
		double y = Math.Min(_dragStartPoint.Y, position.Y);
		double width = Math.Abs(position.X - _dragStartPoint.X);
		double height = Math.Abs(position.Y - _dragStartPoint.Y);
		Rect rect = new Rect(x, y, width, height);
		foreach (GameInfo game in Games)
		{
			ListViewItem listViewItem = GameListView.ContainerFromItem(game) as ListViewItem;
			if (!(listViewItem != null))
			{
				continue;
			}
			Windows.Foundation.Point point = listViewItem.TransformToVisual(GameListView).TransformPoint(new Windows.Foundation.Point(0f, 0f));
			Rect rect2 = new Rect(point.X, point.Y, listViewItem.ActualWidth, listViewItem.ActualHeight);
			if (!(rect2.Left > rect.Right) && !(rect2.Right < rect.Left) && !(rect2.Top > rect.Bottom) && !(rect2.Bottom < rect.Top))
			{
				if (!GameListView.SelectedItems.Contains(game))
				{
					GameListView.SelectedItems.Add(game);
				}
			}
			else if (!_preSelectedGames.Contains(game))
			{
				GameListView.SelectedItems.Remove(game);
			}
		}
	}

	private void GameListView_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (_isDragSelecting)
		{
			_isDragSelecting = false;
			_preSelectedGames.Clear();
			try
			{
				GameListView.ReleasePointerCapture(e.Pointer);
			}
			catch
			{
			}
		}
		if (_isRightClicking)
		{
			_isRightClicking = false;
		}
	}

	private void GameListView_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
	{
		_isDragSelecting = false;
		_preSelectedGames.Clear();
		_isRightClicking = false;
	}

}
