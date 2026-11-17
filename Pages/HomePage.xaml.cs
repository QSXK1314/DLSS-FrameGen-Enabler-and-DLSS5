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

	private Button? _gitcBtn;
	private Dictionary<string, bool> _gitcEnabledGames = new Dictionary<string, bool>();

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

		// 动态创建GITC DLSS5按钮（避免XAML编译器bug）
		_gitcBtn = new Button();
		_gitcBtn.Content = "开启DLSS5";
		_gitcBtn.Click += GitcDlss5Btn_Click;
		// 插入到Dlss5Btn后面
		int insertIndex = ActionPanel.Children.IndexOf(Dlss5Btn) + 1;
		ActionPanel.Children.Insert(insertIndex, _gitcBtn);

		base.Loaded += (s, e) =>
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
				// 多帧生成与GITC DLSS5已确认可以共存，不再做互斥处理。
				// 启动时按实际安装状态重建GITC按钮状态（原来只存在内存，重启后会显示错误）。
				if (item.Dlss5Enabled && FilePatcher.HasGitcBackup(item))
				{
					_gitcEnabledGames[item.ExePath ?? ""] = true;
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

	/// <summary>
	/// 判断某个游戏的 GITC DLSS5 当前是否处于开启状态。
	/// 先读内存缓存，缓存没有时用游戏持久化状态+备份目录兜底，避免重启后按钮显示错误。
	/// </summary>
	private bool IsGitcOn(string gameKey)
	{
		if (!string.IsNullOrEmpty(gameKey) && _gitcEnabledGames.TryGetValue(gameKey, out bool cached) && cached)
		{
			return true;
		}
		GameInfo? game = Games.FirstOrDefault(g => string.Equals(g.ExePath ?? "", gameKey, StringComparison.OrdinalIgnoreCase));
		if (game != null && game.Dlss5Enabled && FilePatcher.HasGitcBackup(game))
		{
			if (!string.IsNullOrEmpty(gameKey))
			{
				_gitcEnabledGames[gameKey] = true;
			}
			return true;
		}
		return false;
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
		Cp2077Btn.Content = Translator.T("Btn_2077");
		ConfigBtn.Content = Translator.T("Btn_Config");
		ConfigNameBtn.Content = Translator.T("Btn_ConfigName");
		TroubleshootBtn.Content = Translator.T("Btn_Troubleshoot");
		RestoreBtn.Content = Translator.T("Btn_Restore");
		RefreshStatusBtn.Content = Translator.T("Btn_RefreshStatus");
		ClearAllBtn.Content = (isEnglish ? "Clear All" : "清除列表");
		DetectRunningBtn.Content = (isEnglish ? "Detect Running Games" : "检测运行中游戏");
		// GITC DLSS5按钮文字和提示
		ToolTipService.SetToolTip(_gitcBtn, isEnglish ? "Enable/Disable DLSS5 for supported games" : "为支持的游戏开启/关闭DLSS5");
		// 初始化按钮文字
		if (SelectedGame != null)
		{
			string gameKey = SelectedGame.ExePath ?? "";
			bool isGitcOn = IsGitcOn(gameKey);
			_gitcBtn.Content = isEnglish
				? (isGitcOn ? "Disable DLSS5" : "Enable DLSS5")
				: (isGitcOn ? "关闭DLSS5" : "开启DLSS5");
		}
		else
		{
			_gitcBtn.Content = isEnglish ? "Enable DLSS5" : "开启DLSS5";
		}
		DisclaimerText.Text = Translator.T("Disclaimer");
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

	private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		// 空函数，避免编译错误
	}

	private void CtxDx9Dlss5_Click(object sender, RoutedEventArgs e)
	{
		// 旧DX9 DLSS5功能已废弃，仅保留控件占位避免XAML连接错位
	}

	private void Dx9Dlss5Btn_Click(object sender, RoutedEventArgs e)
	{
		// 旧DX9 DLSS5功能已废弃，仅保留控件占位避免XAML连接错位
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
			
			// 影之刃零特殊处理：手动添加
			var pbzPaths = new[]
			{
				@"D:\SteamLibrary\steamapps\common\PBZ",
				@"C:\Program Files (x86)\Steam\steamapps\common\PBZ"
			};
			foreach (var pbzPath in pbzPaths)
			{
				var pbzExeDir = System.IO.Path.Combine(pbzPath, "platform", "steam");
				var pbzExe = System.IO.Path.Combine(pbzExeDir, "PBZ.exe");
				if (System.IO.File.Exists(pbzExe))
				{
					string item = pbzPath.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant();
					string text = pbzExe.TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant();
					if (!hashSet.Contains(item) && !hashSet.Contains(text))
					{
						hashSet.Add(item);
						hashSet.Add(text);
						var game = new GameInfo();
						game.Name = "影之刃零";
						game.GamePath = pbzPath;
						game.ExePath = pbzExe;
						game.SupportsFrameGen = true;
						list2.Add(game);
					}
					break;
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

	// 临时禁用SortComboBox排序功能，解决XAML连接错误
	//private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	//{
	//	if (!_isPageLoaded || SortComboBox.SelectedItem == null)
	//	{
	//		return;
	//	}
	//	ComboBoxItem comboBoxItem = SortComboBox.SelectedItem as ComboBoxItem;
	//	if (comboBoxItem == null)
	//	{
	//		return;
	//	}
	//	string text = comboBoxItem.Content?.ToString() ?? "";
	//	List<GameInfo> list = GameListView.SelectedItems.Cast<GameInfo>().ToList();
	//	GameInfo selectedGame = SelectedGame;
	//	List<GameInfo> list2 = Games.ToList();
	//	switch (text)
	//	{
	//	case "名称 A-Z":
	//	case "Name A-Z":
	//		list2 = list2.OrderBy<GameInfo, string>((GameInfo g) => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
	//		break;
	//	case "名称 Z-A":
	//	case "Name Z-A":
	//		list2 = list2.OrderByDescending<GameInfo, string>((GameInfo g) => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
	//		break;
	//	case "状态优先":
	//	case "Status First":
	//		list2 = list2.OrderByDescending((GameInfo g) => g.FrameGenEnabled || g.Dlss5Enabled || g.Dx9Dlss5Enabled).ThenBy<GameInfo, string>((GameInfo g) => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
	//		break;
	//	}
	//	Games.Clear();
	//	foreach (GameInfo item in list2)
	//	{
	//		Games.Add(item);
	//	}
	//	if (selectedGame != null && Games.Contains(selectedGame))
	//	{
	//		GameListView.SelectedItem = selectedGame;
	//	}
	//	foreach (GameInfo item2 in list)
	//	{
	//		if (Games.Contains(item2) && !GameListView.SelectedItems.Contains(item2))
	//		{
	//			GameListView.SelectedItems.Add(item2);
	//		}
	//	}
	//	SaveGamesNow();
	//}

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

		// 恢复GITC DLSS5状态
		string gameKey = selectedGame.ExePath ?? "";
		if (IsGitcOn(gameKey))
		{
			RestoreBtn.IsEnabled = true;
			if (_gitcBtn != null)
			{
				_gitcBtn.Content = Translator.IsEnglish ? "Disable DLSS5" : "关闭DLSS5";
			}
		}
		else
		{
			if (_gitcBtn != null)
			{
				_gitcBtn.Content = Translator.IsEnglish ? "Enable DLSS5" : "开启DLSS5";
			}
		}
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
		MenuFlyoutItem menuFlyoutItem4 = (MenuFlyoutItem)items[1];
		// 旧版DLSS5功能已废弃，强制隐藏
		menuFlyoutItem4.Visibility = Visibility.Collapsed;
		MenuFlyoutItem obj = (MenuFlyoutItem)items[2];
		// 旧DX9 DLSS5功能已废弃，强制隐藏
		obj.Visibility = Visibility.Collapsed;
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
		// 强制永远隐藏旧版DLSS5相关控件
		Dx9Dlss5Btn.Visibility = Visibility.Collapsed;
		SortComboBox.Visibility = Visibility.Collapsed;
		Dlss5Btn.Visibility = Visibility.Collapsed;
		if (GameListView.SelectedItems.Count > 1)
		{
			FrameGenBtn.IsEnabled = false;
			Dlss5Btn.IsEnabled = false;
			//Dx9Dlss5Btn.IsEnabled = false;
			//GitcDlss5Btn.IsEnabled = false;
			Cp2077Btn.IsEnabled = false;
			ConfigBtn.IsEnabled = false;
			ConfigNameBtn.IsEnabled = false;
			TroubleshootBtn.IsEnabled = false;
			RestoreBtn.IsEnabled = false;
			string value = (Translator.IsEnglish ? "Multiple games selected. Only 'Remove from list' is available via right-click menu." : "已选择多个游戏，仅可通过右键菜单使用「移除列表」功能。");
			ToolTipService.SetToolTip(FrameGenBtn, value);
			ToolTipService.SetToolTip(Dlss5Btn, value);
			//ToolTipService.SetToolTip(Dx9Dlss5Btn, value);
			//ToolTipService.SetToolTip(GitcDlss5Btn, value);
			ToolTipService.SetToolTip(RestoreBtn, value);
			return;
		}
		if (game.Is2077)
		{
			Cp2077Btn.Visibility = Visibility.Visible;
			Cp2077Btn.IsEnabled = true;
			FrameGenBtn.Visibility = Visibility.Collapsed;
			Dlss5Btn.Visibility = Visibility.Collapsed;
			//Dx9Dlss5Btn.Visibility = Visibility.Collapsed;
			ConfigBtn.IsEnabled = false;
			ConfigNameBtn.IsEnabled = false;
			ToolTipService.SetToolTip(Cp2077Btn, null);
			Cp2077Btn.Content = ((!game.FrameGenEnabled) ? Translator.T("Btn_2077") : (Translator.IsEnglish ? "Restore 2077 Patch" : "还原2077补丁"));
		}
		else
		{
			FrameGenBtn.Visibility = Visibility.Visible;
			Dlss5Btn.Visibility = Visibility.Collapsed;
			//Dx9Dlss5Btn.Visibility = Visibility.Collapsed;
			Cp2077Btn.Visibility = Visibility.Collapsed;
			ConfigBtn.IsEnabled = game.FrameGenEnabled && game.FrameGenMode == "经典模式";
			ConfigNameBtn.IsEnabled = game.FrameGenEnabled && game.FrameGenMode == "高级模式";
			ToolTipService.SetToolTip(FrameGenBtn, null);
			ToolTipService.SetToolTip(Dlss5Btn, null);
			ToolTipService.SetToolTip(RestoreBtn, null);
		}
		FrameGenBtn.Content = ((!game.FrameGenEnabled) ? Translator.T("Btn_FrameGen") : (Translator.IsEnglish ? "Restore Frame Gen" : "一键还原多帧生成"));
		FrameGenBtn.IsEnabled = game.SupportsFrameGen || game.FrameGenEnabled;
		Dlss5Btn.IsEnabled = true;
		ToolTipService.SetToolTip(Dlss5Btn, Translator.IsEnglish ? "Enable DLSS5 Super Resolution for selected game" : "为选中游戏开启DLSS5超分辨率画质");
		Dlss5Btn.Content = ((!game.Dlss5Enabled) ? Translator.T("Btn_Dlss5") : (Translator.IsEnglish ? "Restore DLSS5" : "一键还原DLSS5"));
		//Dx9Dlss5Btn.Content = ((!game.Dx9Dlss5Enabled) ? Translator.T("Btn_Dx9Dlss5") : (Translator.IsEnglish ? "Restore DX9 DLSS5" : "一键还原DX9 DLSS5"));
		// GITC DLSS5按钮文字切换
		if (_gitcBtn != null)
		{
			string gameKey = game.ExePath ?? "";
			bool isGitcOn = IsGitcOn(gameKey);
			_gitcBtn.Content = Translator.IsEnglish
				? (isGitcOn ? "Disable DLSS5" : "Enable DLSS5")
				: (isGitcOn ? "关闭DLSS5" : "开启DLSS5");
			_gitcBtn.IsEnabled = true;
		}
		//{
		//	string gameKey = game.ExePath ?? "";
		//	bool isGitcEnabled = _gitcEnabledGames.Contains(gameKey);
		//	_gitcBtn.Content = isGitcEnabled ? (Translator.IsEnglish ? "Restore GITC DLSS5" : "还原GITC DLSS5") : "GITC DLSS5";
		//}
		TroubleshootBtn.IsEnabled = game.FrameGenEnabled;
		RestoreBtn.IsEnabled = game.FrameGenEnabled || game.Dlss5Enabled || game.Dx9Dlss5Enabled;
	}

        private async Task<bool> CheckBackupLimit()
        {
            try
            {
                string backupDir = Path.Combine(AppContext.BaseDirectory, "backup");
                if(!Directory.Exists(backupDir)) return true;
                long size = new DirectoryInfo(backupDir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                long mb = size / (1024 * 1024);
                if(mb >= 1024)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "备份空间不足",
                        Content = $"当前备份目录已占用{mb}MB，安装补丁后会超过1024MB的上限。请先前往设置页面删除不需要的旧备份，再安装新补丁。",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return false;
                }
                return true;
            }
            catch { return true; }
        }

	private async void FrameGenBtn_Click(object sender, RoutedEventArgs e)
	{
			if(!await CheckBackupLimit()) return;
		_ = 6;
		try
		{
			GameInfo game = SelectedGame;
			if (game == null)
			{
				return;
			}

			// AMD显卡检测：直接弹NeuralHub引导窗
			GpuInfo gpu = await GetGpuInfoAsync();
			if (gpu.IsAmd)
			{
				string amdGuide = Translator.IsEnglish ?
@"[AMD GPU DLSS Setup Guide]
This software no longer supports AMD GPU patching. Please use the standalone NeuralHub tool to enable all DLSS features:

1. Install NeuralHub: Run Patches\AMD方案\NeuralHub_v0.8.0_setup.exe. It will install to C:\Users\YourUsername\AppData\Local\Danielblnc\NeuralHub by default. Just click Next all the way until Finish.
2. Initial Setup: After launching the tool, click Start first, then import the 4 DLL files one by one:
   - Step 1: Click the red Import button and select nvngx_dlssnr.dll (DLSS5 Super Resolution)
   - Step 2: Click the red Import button and select nvngx_dlss.dll (DLSS Super Resolution)
   - Step 3: Click the red Import button and select nvngx_dlssd.dll (Ray Reconstruction)
   - Step 4: Click the red Import button and select nvngx_dlssg.dll (Frame Generation)
   Click Finish after all files are imported.
3. Patch your game: Select the game you want to patch inside NeuralHub, click Install Mod Loader on the right, then follow the prompts to finish. Press End in-game to open the ReShade configuration menu.
4. Uninstall patches: Back in NeuralHub, select the patched game, click the 3-dot button next to the Play button, select Edit Mod Loader, then click Remove.
5. Enable Frame Gen / Ray Reconstruction: Set the NVIDIA GPU Spoof option to Streamline or All. This spoofs an NVIDIA GPU to unlock NVIDA-exclusive Frame Generation and Ray Reconstruction for AMD cards.

All required files are in the AMD方案 folder that will open automatically." :
@"【AMD显卡DLSS使用指南】
本软件不再支持AMD显卡的补丁安装，请使用独立工具NeuralHub完成所有DLSS功能：

1. 安装NeuralHub：运行Patches\AMD方案\NeuralHub_v0.8.0_setup.exe，默认会安装到C:\Users\你的用户名\AppData\Local\Danielblnc\NeuralHub，一路点Next直到Finish即可。
2. 初始配置：打开软件后先点Start，然后依次导入4个文件：
   - 第一步点红色Import按钮选择nvngx_dlssnr.dll（DLSS5超分）
   - 第二步点红色Import按钮选择nvngx_dlss.dll（DLSS超分）
   - 第三步点红色Import按钮选择nvngx_dlssd.dll（光线重构）
   - 第四步点红色Import按钮选择nvngx_dlssg.dll（帧生成）
   全部导入完成后点Finish。
3. 给游戏打补丁：在NeuralHub软件内选中要安装的游戏，点击右侧Install Mod Loader，一路点Next即可完成安装，游戏内默认按End键打开ReShade配置菜单。
4. 卸载补丁：回到NeuralHub选中对应游戏，点Play按钮旁边的三个点按钮，选择Edit Mod Loader，再点Remove即可卸载。
5. 开启帧生成/光线重构：需要把NVIDIA GPU Spoof选项设置为Streamline或者All，这个功能是伪装成N卡，让游戏对AMD用户开放N卡专属的帧生成和光线重构功能。

所有需要的文件都在打开的AMD方案文件夹里。";
				await new ContentDialog
				{
					Title = Translator.IsEnglish ? "AMD GPU DLSS Setup Guide" : "AMD显卡DLSS使用指南",
					Content = amdGuide,
					PrimaryButtonText = Translator.IsEnglish ? "Open AMD Folder" : "打开AMD方案文件夹",
					CloseButtonText = Translator.T("Common_Cancel"),
					XamlRoot = this.Content.XamlRoot
				}.ShowAsync();
				try
				{
					var amdDir = Path.Combine(AppContext.BaseDirectory, "Patches", "AMD方案");
					if (Directory.Exists(amdDir)) System.Diagnostics.Process.Start("explorer.exe", amdDir);
				}
				catch { }
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
				// 新的 GITC DLSS5 与多帧生成已确认可以共存，不再拦截。
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
				// 20系、30系、40系现在都用统一的安装方法，不再单独识别显卡型号
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
			if(!await CheckBackupLimit()) return;
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
		GpuInfo gpuInfo = await GetGpuInfoAsync();
		if (gpuInfo.IsNvidia)
		{
			FilePatcher.InstallGITCDLSS5_Base(game, "dx12");
			ShowMessage(Translator.T("Dlg_Dlss5_Installed"));
		}
		else if (gpuInfo.IsAmd)
		{
			await new ContentDialog
			{
				Title = (Translator.IsEnglish ? "AMD GPU Transition Notice" : "AMD显卡过渡提示"),
				Content = (Translator.IsEnglish
					? "This software no longer supports AMD GPU DLSS patching.\n\nPlease open the Patches\\AMD方案 folder in the software directory, run NeuralHub_v0.8.0_setup.exe, and import the 4 DLL files in that folder to enable DLSS super resolution, ray reconstruction, frame generation and DLSS5 on AMD cards."
					: "本软件不再支持AMD显卡的DLSS补丁安装。\n\n请打开软件目录下的 Patches\\AMD方案 文件夹，运行NeuralHub_v0.8.0_setup.exe，导入同目录下的四个dll文件，即可为AMD显卡开启DLSS超分、光线重构、帧生成和DLSS5功能。"),
				PrimaryButtonText = (Translator.IsEnglish ? "Open Folder" : "打开AMD方案文件夹"),
				CloseButtonText = Translator.T("Common_Cancel"),
				XamlRoot = base.Content.XamlRoot
			}.ShowAsync();
			try
			{
				var amdDir = Path.Combine(AppContext.BaseDirectory, "Patches", "AMD方案");
				if (Directory.Exists(amdDir))
				{
					System.Diagnostics.Process.Start("explorer.exe", amdDir);
				}
			}
			catch { }
		}
		else
		{
			ShowMessage(Translator.T("Dlg_Dlss5_GpuNotSupported"));
		}
		UpdateButtonStates(game);
	}

	private async void GitcDlss5Btn_Click(object sender, RoutedEventArgs e)
	{
		if (!await CheckBackupLimit()) return;
		GameInfo game = SelectedGame;
		if (game == null)
		{
			return;
		}

		// 如果已经开启了，就执行还原
		string gameKey = game.ExePath ?? "";
		if (IsGitcOn(gameKey))
		{
			DoRestoreGITC(game);
			return;
		}

		// 安装逻辑全部拆到独立函数，避免触发XAML编译器bug
		await DoInstallGITC(game);
	}

	private async Task DoInstallGITC(GameInfo game)
	{
		// AMD显卡检测：直接弹NeuralHub引导窗
		GpuInfo gpu = await GetGpuInfoAsync();
		if (gpu.IsAmd)
		{
			string amdGuide = Translator.IsEnglish ?
@"[AMD GPU DLSS Setup Guide]
This software no longer supports AMD GPU patching. Please use the standalone NeuralHub tool to enable all DLSS features:

1. Install NeuralHub: Run Patches\AMD方案\NeuralHub_v0.8.0_setup.exe. It will install to C:\Users\YourUsername\AppData\Local\Danielblnc\NeuralHub by default. Just click Next all the way until Finish.
2. Initial Setup: After launching the tool, click Start first, then import the 4 DLL files one by one:
   - Step 1: Click the red Import button and select nvngx_dlssnr.dll (DLSS5 Super Resolution)
   - Step 2: Click the red Import button and select nvngx_dlss.dll (DLSS Super Resolution)
   - Step 3: Click the red Import button and select nvngx_dlssd.dll (Ray Reconstruction)
   - Step 4: Click the red Import button and select nvngx_dlssg.dll (Frame Generation)
   Click Finish after all files are imported.
3. Patch your game: Select the game you want to patch inside NeuralHub, click Install Mod Loader on the right, then follow the prompts to finish. Press End in-game to open the ReShade configuration menu.
4. Uninstall patches: Back in NeuralHub, select the patched game, click the 3-dot button next to the Play button, select Edit Mod Loader, then click Remove.
5. Enable Frame Gen / Ray Reconstruction: Set the NVIDIA GPU Spoof option to Streamline or All. This spoofs an NVIDIA GPU to unlock NVIDA-exclusive Frame Generation and Ray Reconstruction for AMD cards.

All required files are in the AMD方案 folder that will open automatically." :
@"【AMD显卡DLSS使用指南】
本软件不再支持AMD显卡的补丁安装，请使用独立工具NeuralHub完成所有DLSS功能：

1. 安装NeuralHub：运行Patches\AMD方案\NeuralHub_v0.8.0_setup.exe，默认会安装到C:\Users\你的用户名\AppData\Local\Danielblnc\NeuralHub，一路点Next直到Finish即可。
2. 初始配置：打开软件后先点Start，然后依次导入4个文件：
   - 第一步点红色Import按钮选择nvngx_dlssnr.dll（DLSS5超分）
   - 第二步点红色Import按钮选择nvngx_dlss.dll（DLSS超分）
   - 第三步点红色Import按钮选择nvngx_dlssd.dll（光线重构）
   - 第四步点红色Import按钮选择nvngx_dlssg.dll（帧生成）
   全部导入完成后点Finish。
3. 给游戏打补丁：在NeuralHub软件内选中要安装的游戏，点击右侧Install Mod Loader，一路点Next即可完成安装，游戏内默认按End键打开ReShade配置菜单。
4. 卸载补丁：回到NeuralHub选中对应游戏，点Play按钮旁边的三个点按钮，选择Edit Mod Loader，再点Remove即可卸载。
5. 开启帧生成/光线重构：需要把NVIDIA GPU Spoof选项设置为Streamline或者All，这个功能是伪装成N卡，让游戏对AMD用户开放N卡专属的帧生成和光线重构功能。

所有需要的文件都在打开的AMD方案文件夹里。";
			await new ContentDialog
			{
				Title = Translator.IsEnglish ? "AMD GPU DLSS Setup Guide" : "AMD显卡DLSS使用指南",
				Content = amdGuide,
				PrimaryButtonText = Translator.IsEnglish ? "Open AMD Folder" : "打开AMD方案文件夹",
				CloseButtonText = Translator.T("Common_Cancel"),
				XamlRoot = this.Content.XamlRoot
			}.ShowAsync();
			try
			{
				var amdDir = Path.Combine(AppContext.BaseDirectory, "Patches", "AMD方案");
				if (Directory.Exists(amdDir)) System.Diagnostics.Process.Start("explorer.exe", amdDir);
			}
			catch { }
			return;
		}

		// 第一步：选择渲染API模式
		int apiChoice = await ShowModeDialog(
			Translator.IsEnglish ? "Select GITC DLSS5 API Mode" : "选择GITC DLSS5渲染API模式",
			new (string, string)[]
			{
				(Translator.IsEnglish ? "DX12" : "DX12", Translator.IsEnglish ? "For DX12 games (recommended)" : "适用于DX12游戏（推荐）"),
				(Translator.IsEnglish ? "DX9" : "DX9", Translator.IsEnglish ? "For DX9 games" : "适用于DX9游戏"),
				(Translator.IsEnglish ? "Vulkan" : "Vulkan", Translator.IsEnglish ? "For Vulkan games" : "适用于Vulkan游戏")
			});

		if (apiChoice == -1) return;

		// 第二步：选择显卡型号
		int gpuChoice = await ShowModeDialog(
			Translator.IsEnglish ? "Select Your GPU Model" : "选择你的显卡型号",
			new (string, string)[]
			{
				(Translator.IsEnglish ? "RTX 20/30 Series" : "RTX 20/30系", Translator.IsEnglish ? "For RTX 20 and 30 series cards" : "适用于RTX 20系和30系显卡"),
				(Translator.IsEnglish ? "RTX 40 Series" : "RTX 40系", Translator.IsEnglish ? "For RTX 40 series cards" : "适用于RTX 40系显卡"),
				(Translator.IsEnglish ? "RTX 50 Series" : "RTX 50系", Translator.IsEnglish ? "For RTX 50 series cards" : "适用于RTX 50系显卡")
			});

		if (gpuChoice == -1) return;

		// 根据选择的API设置对应的dll名
		string apiName = apiChoice switch
		{
			0 => "dx12",
			1 => "dx9",
			2 => "vulkan",
			_ => "dx12"
		};

		// 如果是DX9模式，才需要选安装方式和游戏位数
		string installSubDir = "";
		bool is32bit = false;
		if (apiName == "dx9")
		{
			// 第一个弹窗：选择安装方式
			int installModeChoice = await ShowModeDialog(
				Translator.IsEnglish ? "Please select installation mode:" : "请选择安装方式",
				new (string, string)[]
				{
					(Translator.IsEnglish ? "Standard installation" : "通用安装", Translator.IsEnglish ? "Install to game exe directory" : "安装到游戏exe目录"),
					(Translator.IsEnglish ? "Source Engine installation" : "起源引擎安装", Translator.IsEnglish ? "Install to bin subdirectory (like Half-Life 2)" : "安装到bin子目录（比如半条命2）")
				});
			if (installModeChoice == -1) return;
			if (installModeChoice == 1)
			{
				installSubDir = "bin";
			}

			// 第二个弹窗：选择游戏位数
			int bitChoice = await ShowModeDialog(
				Translator.IsEnglish ? "Please select game bitness:" : "请选择游戏位数",
				new (string, string)[]
				{
					(Translator.IsEnglish ? "32-bit game" : "32位游戏", Translator.IsEnglish ? "For old DX9 games" : "老DX9游戏选这个"),
					(Translator.IsEnglish ? "64-bit game" : "64位游戏", Translator.IsEnglish ? "For modern games" : "现代游戏选这个")
				});
			if (bitChoice == -1) return;
			is32bit = (bitChoice == 0);
		}

		// 安装基础文件
		ShowMessage(Translator.IsEnglish ? $"Installing GITC DLSS5 {apiName.ToUpper()} base files..." : $"正在安装GITC DLSS5 {apiName.ToUpper()}基础文件...");
		bool success = false;
		string errorMsg = "";
		try
		{
			success = FilePatcher.InstallGITCDLSS5_Base(game, apiName, installSubDir, is32bit);
		}
		catch (Exception ex)
		{
			errorMsg = ex.Message;
			success = false;
		}

		if (!success)
		{
			ShowMessage(Translator.IsEnglish ? $"Failed to install base files: {errorMsg}" : $"基础文件安装失败：{errorMsg}");
			return;
		}

		// 根据显卡型号安装对应的dlssnr.dll
		string gpuType = gpuChoice switch
		{
			0 => "2030",
			1 => "40",
			2 => "50",
			_ => "40"
		};

		ShowMessage(Translator.IsEnglish ? "Installing DLSS NR model for your GPU..." : "正在安装对应显卡的DLSS NR模型...");
		success = FilePatcher.InstallGITCDLSS5_DLSSNR(game, gpuType, installSubDir);

		if (success)
		{
			_gitcEnabledGames[game.ExePath ?? ""] = true;
			// 合并到现有DLSS5状态显示，不用加新标签
			game.Dlss5Enabled = true;
			ShowMessage(Translator.IsEnglish ? "GITC DLSS5 installed successfully! Press Home in game to open the menu." : "GITC DLSS5安装成功！进入游戏后按Home键打开菜单。");
		}
		else
		{
			ShowMessage(Translator.IsEnglish ? "Failed to install DLSS NR model." : "DLSS NR模型安装失败。");
		}

		UpdateButtonStates(game);
		// 安装成功后，在UpdateButtonStates之后再点亮还原按钮
		if (success)
		{
			RestoreBtn.IsEnabled = true;
			if (_gitcBtn != null)
			{
				_gitcBtn.Content = Translator.IsEnglish ? "Disable DLSS5" : "关闭DLSS5";
			}
		}
	}

	private async void DoRestoreGITC(GameInfo game)
	{
		// 确认弹窗
		var dialog = new ContentDialog
		{
			Title = Translator.IsEnglish ? "Restore GITC DLSS5" : "还原GITC DLSS5",
			Content = Translator.IsEnglish ? "Are you sure you want to restore GITC DLSS5? This will delete all GITC related files." : "确定要还原GITC DLSS5吗？将删除所有GITC相关文件。",
			PrimaryButtonText = Translator.IsEnglish ? "Yes" : "确定",
			CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
			XamlRoot = Content.XamlRoot
		};
		var result = await dialog.ShowAsync();
		if (result != ContentDialogResult.Primary) return;

		bool success = FilePatcher.RestoreGITCDLSS5(game);
		if (success)
		{
			_gitcEnabledGames.Remove(game.ExePath ?? "");
			// 清除合并的DLSS5状态显示
			game.Dlss5Enabled = false;
			if (_gitcBtn != null)
			{
				_gitcBtn.Content = Translator.IsEnglish ? "Enable DLSS5" : "开启DLSS5";
			}
			ShowMessage(Translator.IsEnglish ? "DLSS5 restored successfully." : "DLSS5已还原。");
		}
		else
		{
			ShowMessage(Translator.IsEnglish ? "Failed to restore DLSS5." : "DLSS5还原失败。");
		}
	}

	private async void Cp2077Btn_Click(object sender, RoutedEventArgs e)
	{
			if(!await CheckBackupLimit()) return;
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
		// MFGUnlocker v1.7.0 支持的配置名列表
		string[] configNames = new string[]
		{
			"dinput8", "winmm", "d3d9", "d3d10", "d3d11", "d3d12", 
			"dxgi", "dsound", "wininet", "winhttp", "binkw64", "bink2w64",
			"xinput1_1", "xinput1_2", "xinput1_3", "xinput1_4", "xinput9_1_0", "xinputuap"
		};
		ComboBox nameCombo = new ComboBox
		{
			Header = Translator.T("Dlg_ConfigName_SelectName")
		};
		foreach (string item in configNames)
		{
			nameCombo.Items.Add(item);
		}
		nameCombo.SelectedIndex = 0;
		ContentDialog dialog = new ContentDialog
		{
			Title = Translator.T("Dlg_ConfigName_Title"),
			Content = nameCombo,
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
		string newDll = Path.Combine(exeDir, newName + ".dll");
		if (!File.Exists(versionDll))
		{
			ShowMessage(Translator.T("Dlg_ConfigName_NotFound"));
		}
		else if (File.Exists(newDll))
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
			File.Move(versionDll, newDll);
			FilePatcher.RecordConfigNameRename(selectedGame, versionDll, newDll);
			ShowMessage(string.Format(Translator.T("Dlg_ConfigName_Done"), newName));
		}
	}

	private async void TroubleshootBtn_Click(object sender, RoutedEventArgs e)
	{
			if(!await CheckBackupLimit()) return;
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
				// 按实际开启的功能分别还原，没装的功能不再跟着跑一遍
				string gameKey = game.ExePath ?? "";
				bool gitcOn = IsGitcOn(gameKey);
				if (game.FrameGenEnabled)
				{
					FilePatcher.RestoreFrameGen(game);
				}
				if (gitcOn)
				{
					FilePatcher.RestoreGITCDLSS5(game);
				}
				else if (game.Dlss5Enabled)
				{
					FilePatcher.RestoreDLSS5(game);
				}
				_gitcEnabledGames.Remove(game.ExePath ?? "");
				ShowMessage(Translator.T("Dlg_Restore_Done"));
				game.FrameGenEnabled = false;
				game.Dlss5Enabled = false;
				game.Dx9Dlss5Enabled = false;
				UpdateButtonStates(game);
				if (_gitcBtn != null)
				{
					_gitcBtn.Content = Translator.IsEnglish ? "Enable DLSS5" : "开启DLSS5";
				}
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
			FilePatcher.RestoreGITCDLSS5(game);
			_gitcEnabledGames.Remove(game.ExePath ?? "");
			ShowMessage(Translator.T("Dlg_Restore_NoResp_Done"));
			game.FrameGenEnabled = false;
			game.Dlss5Enabled = false;
			game.Dx9Dlss5Enabled = false;
			UpdateButtonStates(game);
			if (_gitcBtn != null)
			{
				_gitcBtn.Content = Translator.IsEnglish ? "Enable DLSS5" : "开启DLSS5";
			}
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

	private async void CtxGitcDlss5_Click(object sender, RoutedEventArgs e)
	{
		EnsureContextGame();
		if (!await CheckBackupLimit()) return;
		GameInfo game = SelectedGame;
		if (game == null)
		{
			return;
		}

		// 第一步：选择渲染API模式
		int apiChoice = await ShowModeDialog(
			Translator.IsEnglish ? "Select GITC DLSS5 API Mode" : "选择GITC DLSS5渲染API模式",
			new (string, string)[]
			{
				(Translator.IsEnglish ? "DX12" : "DX12", Translator.IsEnglish ? "For DX12 games (recommended)" : "适用于DX12游戏（推荐）"),
				(Translator.IsEnglish ? "DX9" : "DX9", Translator.IsEnglish ? "For DX9 games" : "适用于DX9游戏"),
				(Translator.IsEnglish ? "Vulkan" : "Vulkan", Translator.IsEnglish ? "For Vulkan games" : "适用于Vulkan游戏")
			});

		if (apiChoice == -1) return;

		// 第二步：选择显卡型号
		int gpuChoice = await ShowModeDialog(
			Translator.IsEnglish ? "Select Your GPU Model" : "选择你的显卡型号",
			new (string, string)[]
			{
				(Translator.IsEnglish ? "RTX 20/30 Series" : "RTX 20/30系", Translator.IsEnglish ? "For RTX 20 and 30 series cards" : "适用于RTX 20系和30系显卡"),
				(Translator.IsEnglish ? "RTX 40 Series" : "RTX 40系", Translator.IsEnglish ? "For RTX 40 series cards" : "适用于RTX 40系显卡"),
				(Translator.IsEnglish ? "RTX 50 Series" : "RTX 50系", Translator.IsEnglish ? "For RTX 50 series cards" : "适用于RTX 50系显卡")
			});

		if (gpuChoice == -1) return;

		// 根据选择的API设置对应的dll名
		string apiName = apiChoice switch
		{
			0 => "dx12",
			1 => "dx9",
			2 => "vulkan",
			_ => "dx12"
		};

		// 如果是DX9模式，才需要选安装方式和游戏位数
		string installSubDir = "";
		bool is32bit = false;
		if (apiName == "dx9")
		{
			// 第一个弹窗：选择安装方式
			int installModeChoice = await ShowModeDialog(
				Translator.IsEnglish ? "Please select installation mode:" : "请选择安装方式",
				new (string, string)[]
				{
					(Translator.IsEnglish ? "Standard installation" : "通用安装", Translator.IsEnglish ? "Install to game exe directory" : "安装到游戏exe目录"),
					(Translator.IsEnglish ? "Source Engine installation" : "起源引擎安装", Translator.IsEnglish ? "Install to bin subdirectory (like Half-Life 2)" : "安装到bin子目录（比如半条命2）")
				});
			if (installModeChoice == -1) return;
			if (installModeChoice == 1)
			{
				installSubDir = "bin";
			}

			// 第二个弹窗：选择游戏位数
			int bitChoice = await ShowModeDialog(
				Translator.IsEnglish ? "Please select game bitness:" : "请选择游戏位数",
				new (string, string)[]
				{
					(Translator.IsEnglish ? "32-bit game" : "32位游戏", Translator.IsEnglish ? "For old DX9 games" : "老DX9游戏选这个"),
					(Translator.IsEnglish ? "64-bit game" : "64位游戏", Translator.IsEnglish ? "For modern games" : "现代游戏选这个")
				});
			if (bitChoice == -1) return;
			is32bit = (bitChoice == 0);
		}

		// 安装基础文件
		ShowMessage(Translator.IsEnglish ? $"Installing GITC DLSS5 {apiName.ToUpper()} base files..." : $"正在安装GITC DLSS5 {apiName.ToUpper()}基础文件...");
		bool success = false;
		string errorMsg = "";
		try
		{
			success = FilePatcher.InstallGITCDLSS5_Base(game, apiName, installSubDir, is32bit);
		}
		catch (Exception ex)
		{
			errorMsg = ex.Message;
			success = false;
		}

		if (!success)
		{
			ShowMessage(Translator.IsEnglish ? $"Failed to install base files: {errorMsg}" : $"基础文件安装失败：{errorMsg}");
			return;
		}

		// 根据显卡型号安装对应的dlssnr.dll
		string gpuType = gpuChoice switch
		{
			0 => "2030",
			1 => "40",
			2 => "50",
			_ => "40"
		};

		ShowMessage(Translator.IsEnglish ? "Installing DLSS NR model for your GPU..." : "正在安装对应显卡的DLSS NR模型...");
		success = FilePatcher.InstallGITCDLSS5_DLSSNR(game, gpuType, installSubDir);

		if (success)
		{
			ShowMessage(Translator.IsEnglish ? "GITC DLSS5 installed successfully! Press Home in game to open the menu." : "GITC DLSS5安装成功！进入游戏后按Home键打开菜单。");
		}
		else
		{
			ShowMessage(Translator.IsEnglish ? "Failed to install DLSS NR model." : "DLSS NR模型安装失败。");
		}

		UpdateButtonStates(game);
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
