using DLSSFrameGenEnabler_WinUI3.Models;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using Windows.Storage.Pickers;

namespace DLSSFrameGenEnabler_WinUI3.Pages
{
    public sealed partial class HomePage : Page
    {
        public ObservableCollection<GameInfo> Games { get; } = new();
        private GameInfo? SelectedGame => GameListView.SelectedItem as GameInfo;
        private GpuInfo? _gpuInfo;
        private GameInfo? _contextMenuGame;

        // 框选相关变量
        private bool _isDragSelecting = false;
        private Windows.Foundation.Point _dragStartPoint;
        private List<GameInfo> _preSelectedGames = new();

        // 右键多选相关变量
        private List<GameInfo> _rightClickSelectedGames = new();
        private bool _isRightClicking = false;

        // 为游戏设置图标
        private void SetGameIcon(GameInfo game)
        {
            if (game == null || string.IsNullOrEmpty(game.ExePath)) return;
            if (!File.Exists(game.ExePath)) return;

            var exePath = game.ExePath;
            var dispatcher = this.DispatcherQueue;

            // 在后台线程提取图标，避免阻塞UI
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                byte[]? iconBytes = null;
                try
                {
                    if (File.Exists(exePath))
                    {
                        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                        if (icon != null)
                        {
                            using var bitmap = icon.ToBitmap();
                            using var stream = new MemoryStream();
                            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            iconBytes = stream.ToArray();
                        }
                    }
                }
                catch { }

                if (iconBytes != null)
                {
                    dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        try
                        {
                            using var stream = new MemoryStream(iconBytes);
                            var bitmapImage = new BitmapImage();
                            bitmapImage.SetSource(stream.AsRandomAccessStream());
                            game.Icon = bitmapImage;
                        }
                        catch { }
                    });
                }
            });
        }

        // 获取显卡信息（后台线程执行，避免UI卡死）
        private async System.Threading.Tasks.Task<GpuInfo> GetGpuInfoAsync()
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    return GpuDetector.DetectPrimaryGpu();
                }
                catch
                {
                    return new GpuInfo { Name = "未知显卡", Series = "Other" };
                }
            });
        }

        public HomePage()
        {
            this.InitializeComponent();
            GameListView.ItemsSource = Games;
            Translator.LanguageChanged += UpdateLanguageTexts;
            UpdateLanguageTexts();

            // 加载保存的游戏列表（如果开启了记住游戏列表）
            if (SettingsService.Instance.RememberGames)
            {
                var savedGames = SettingsService.LoadGames();
                foreach (var game in savedGames)
                {
                    // 修正RE引擎游戏的冲突状态（DLSS5和多帧生成不能同时开启）
                    if (game.IsREEngine && game.FrameGenEnabled && game.Dlss5Enabled)
                    {
                        game.FrameGenEnabled = false;
                    }
                    game.PropertyChanged += Game_PropertyChanged;
                    Games.Add(game);
                    SetGameIcon(game);
                }
            }

            // 监听游戏列表变化，自动保存（如果开启了记住游戏列表）
            Games.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (GameInfo game in e.NewItems)
                    {
                        game.PropertyChanged += Game_PropertyChanged;
                    }
                }
                if (SettingsService.Instance.RememberGames)
                {
                    SettingsService.SaveGames(Games.ToList());
                }
            };

            // 后台检测显卡，避免阻塞UI
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                _gpuInfo = GpuDetector.DetectPrimaryGpu();
            });
        }

        // 显式保存游戏列表
        public void SaveGamesNow()
        {
            try
            {
                if (SettingsService.Instance.RememberGames)
                {
                    SettingsService.SaveGames(Games.ToList());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存游戏列表失败: {ex.Message}");
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

        // 更新语言文本
        private void UpdateLanguageTexts()
        {
            bool en = Translator.IsEnglish;
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
            DetectRunningBtn.Content = en ? "Detect Running Games" : "检测运行中游戏";
            DisclaimerText.Text = Translator.T("Disclaimer");

            // 更新ToolTip
            ToolTipService.SetToolTip(AutoScanBtn, en ? "Auto scan games in Steam and Epic libraries" : "自动扫描Steam和Epic游戏库中的游戏");
            ToolTipService.SetToolTip(ManualAddBtn, en ? "Select game folder, auto-detect game exe" : "选择游戏文件夹，自动识别游戏运行exe");
            ToolTipService.SetToolTip(ManualAddDx9Btn, en ? "Manually select game exe, for DX9 games or auto-detection failures" : "手动选择游戏exe文件，适用于DX9游戏或自动识别失败的游戏");
            ToolTipService.SetToolTip(FrameGenBtn, en ? "Enable Frame Generation for selected game" : "为选中游戏开启多帧生成功能");
            ToolTipService.SetToolTip(Dlss5Btn, en ? "Enable DLSS5 Super Resolution for selected game" : "为选中游戏开启DLSS5超分辨率");
            ToolTipService.SetToolTip(Dx9Dlss5Btn, en ? "Enable DLSS5 for DX9 games (requires translation)" : "为DX9游戏开启DLSS5（需转译）");
            ToolTipService.SetToolTip(Cp2077Btn, en ? "Cyberpunk 2077 exclusive frame gen patch" : "赛博朋克2077专用多帧生成补丁");
            ToolTipService.SetToolTip(ConfigBtn, en ? "Edit frame gen config (Classic mode only)" : "编辑多帧生成配置文件（仅经典模式）");
            ToolTipService.SetToolTip(ConfigNameBtn, en ? "Modify patch DLL config name (Advanced mode only)" : "修改补丁DLL配置名（仅高级模式）");
            ToolTipService.SetToolTip(TroubleshootBtn, en ? "Replace sl. prefixed patch files for troubleshooting" : "替换sl.开头的补丁文件进行排错");
            ToolTipService.SetToolTip(RestoreBtn, en ? "Restore all installed patches" : "还原所有已安装的补丁");
            ToolTipService.SetToolTip(RefreshStatusBtn, en ? "Re-detect patch status for all games" : "重新检测所有游戏的补丁开启状态");
            ToolTipService.SetToolTip(DetectRunningBtn, en ? "Detect currently running game processes, 100% accurate game exe identification" : "检测当前正在运行的游戏进程，100%准确识别游戏真正的exe");

            // 刷新所有游戏的状态显示
            foreach (var game in Games)
            {
                game.RefreshStatus();
            }

            // 刷新当前选中游戏的按钮状态
            if (SelectedGame != null)
            {
                UpdateButtonStates(SelectedGame);
            }
        }

        // 游戏属性变化时保存
        private void Game_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (SettingsService.Instance.RememberGames)
            {
                SettingsService.SaveGames(Games.ToList());
            }
        }

        // 自动扫描
        private async void AutoScanBtn_Click(object sender, RoutedEventArgs e)
        {
            // 注意：不要调用 Games.Clear()，否则会把手动添加的游戏也清掉！
            // 自动扫描结果会追加到现有游戏列表中，并自动去重
            
            // 在后台线程执行扫描，避免UI卡死
            var scanResult = await System.Threading.Tasks.Task.Run(() =>
            {
                var steamGames = GameScanner.ScanSteamGames();
                var epicGames = GameScanner.ScanEpicGames();
                var gogGames = GameScanner.ScanGOGGames();
                var ubisoftGames = GameScanner.ScanUbisoftGames();
                var allGames = steamGames.Concat(epicGames).Concat(gogGames).Concat(ubisoftGames).ToList();
                
                // 加强去重：规范化路径后比较，同时比较GamePath和ExePath
                var uniqueGames = new List<GameInfo>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var game in allGames)
                {
                    // 规范化路径：去掉结尾的斜杠，统一比较
                    var normalizedGamePath = game.GamePath.TrimEnd('\\', '/').ToLowerInvariant();
                    var normalizedExePath = !string.IsNullOrEmpty(game.ExePath) 
                        ? game.ExePath.TrimEnd('\\', '/').ToLowerInvariant() 
                        : "";
                    
                    // 如果GamePath或ExePath已经存在，就跳过（避免重复）
                    if (seenPaths.Contains(normalizedGamePath) || 
                        (!string.IsNullOrEmpty(normalizedExePath) && seenPaths.Contains(normalizedExePath)))
                    {
                        continue;
                    }
                    
                    seenPaths.Add(normalizedGamePath);
                    if (!string.IsNullOrEmpty(normalizedExePath))
                    {
                        seenPaths.Add(normalizedExePath);
                    }
                    uniqueGames.Add(game);
                }
                return uniqueGames;
            });
            
            int addedCount = 0;
            foreach (var game in scanResult)
            {
                // 再次检查（双重保险）：同时检查GamePath和ExePath，避免与现有游戏重复
                var normalizedGamePath = game.GamePath.TrimEnd('\\', '/').ToLowerInvariant();
                var normalizedExePath = !string.IsNullOrEmpty(game.ExePath) 
                    ? game.ExePath.TrimEnd('\\', '/').ToLowerInvariant() 
                    : "";
                
                bool alreadyExists = Games.Any(g => 
                    g.GamePath.TrimEnd('\\', '/').ToLowerInvariant() == normalizedGamePath ||
                    (!string.IsNullOrEmpty(normalizedExePath) && 
                     !string.IsNullOrEmpty(g.ExePath) && 
                     g.ExePath.TrimEnd('\\', '/').ToLowerInvariant() == normalizedExePath));
                
                if (!alreadyExists)
                {
                    Games.Add(game);
                    SetGameIcon(game);
                    addedCount++;
                }
            }
            SaveGamesNow(); // 显式保存
            
            // 显示更详细的提示：新增了多少个，总共多少个
            var totalCount = Games.Count;
            var message = Translator.IsEnglish ? 
                $"Scan complete! Added {addedCount} new games, total {totalCount} games in list." :
                $"扫描完成！新增 {addedCount} 个游戏，列表中共有 {totalCount} 个游戏。";
            ShowMessage(message);
        }

        // 刷新状态
        private async void RefreshStatusBtn_Click(object sender, RoutedEventArgs e)
        {
            // 在后台线程执行检测，避免UI卡死
            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var game in Games)
                {
                    try
                    {
                        var exeDir = Path.GetDirectoryName(game.ExePath) ?? game.GamePath;
                        game.FrameGenEnabled = GameScanner.CheckFrameGenEnabled(game.GamePath, exeDir);
                        game.Dlss5Enabled = GameScanner.CheckDLSS5Enabled(game.GamePath, exeDir);
                        game.Dx9Dlss5Enabled = GameScanner.CheckDX9DLSS5Enabled(game.GamePath, exeDir);
                    }
                    catch { }
                }
            });
            
            // 刷新按钮状态
            if (SelectedGame != null)
            {
                UpdateButtonStates(SelectedGame);
            }
            
            ShowMessage(Translator.T("Dlg_Refresh_Done"));
        }

        // 手动添加（目录模式）
        private async void ManualAddBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var game = GameScanner.AddGameByDirectory(folder.Path);
                // 加强去重：规范化路径后比较
                var normalizedPath = folder.Path.TrimEnd('\\', '/').ToLowerInvariant();
                if (game != null && !Games.Any(g => g.GamePath.TrimEnd('\\', '/').ToLowerInvariant() == normalizedPath))
                {
                    Games.Add(game);
                    SetGameIcon(game);
                    SaveGamesNow(); // 显式保存
                    ShowMessage(Translator.T("Dlg_Add_Success"));
                }
                else
                {
                    ShowMessage(Translator.T("Dlg_Add_Fail"));
                }
            }
        }

        // 手动添加游戏（exe模式，保底选项，支持所有游戏）
        private async void ManualAddDx9Btn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".exe");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                // 手动添加exe是保底选项，支持所有游戏（DX9/DX11/DX12）
                // 不强制isDx9Manual，让AddGameByExe自动检测DX支持情况
                var game = GameScanner.AddGameByExe(file.Path, isDx9Manual: false);
                // 加强去重：规范化路径后比较ExePath
                var normalizedExePath = file.Path.TrimEnd('\\', '/').ToLowerInvariant();
                if (game != null && !Games.Any(g => !string.IsNullOrEmpty(g.ExePath) && g.ExePath.TrimEnd('\\', '/').ToLowerInvariant() == normalizedExePath))
                {
                    Games.Add(game);
                    SetGameIcon(game);
                    SaveGamesNow(); // 显式保存
                    ShowMessage(Translator.T("Dlg_Add_Success"));
                }
            }
        }

        // 检测运行中游戏按钮
        private async void DetectRunningBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 直接检测进程（枚举进程很快，不需要异步）
                List<ProcessDetector.GameProcessInfo> runningGames;
                try
                {
                    runningGames = ProcessDetector.GetRunningGames();
                }
                catch (Exception ex)
                {
                    var errDialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "Error" : "错误",
                        Content = Translator.IsEnglish ? $"Failed to detect processes: {ex.Message}" : $"检测进程失败：{ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errDialog.ShowAsync();
                    return;
                }

                // 如果没有检测到游戏
                if (runningGames.Count == 0)
                {
                    var noGameDialog = new ContentDialog
                    {
                        Title = Translator.IsEnglish ? "No Games Found" : "未检测到游戏",
                        Content = Translator.IsEnglish ?
                            "No running games detected. Please start the game first and try again." :
                            "未检测到正在运行的游戏。请先启动游戏，然后再点击此按钮。",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await noGameDialog.ShowAsync();
                    return;
                }

                // 创建进程选择列表
                var processList = new ListView
                {
                    SelectionMode = ListViewSelectionMode.Single,
                    Height = 300
                };

                foreach (var proc in runningGames)
                {
                    var item = new ListViewItem
                    {
                        Tag = proc,
                        Padding = new Microsoft.UI.Xaml.Thickness(4)
                    };

                    var container = new Microsoft.UI.Xaml.Controls.StackPanel
                    {
                        Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
                        Spacing = 10
                    };

                    // 图标
                    if (proc.Icon != null)
                    {
                        var iconImage = new Microsoft.UI.Xaml.Controls.Image
                        {
                            Source = proc.Icon,
                            Width = 24,
                            Height = 24,
                            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center
                        };
                        container.Children.Add(iconImage);
                    }

                    // 文字信息
                    var textPanel = new Microsoft.UI.Xaml.Controls.StackPanel();
                    var nameText = new Microsoft.UI.Xaml.Controls.TextBlock
                    {
                        Text = proc.ProcessName,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center
                    };
                    if (!string.IsNullOrEmpty(proc.WindowTitle))
                    {
                        nameText.Text = $"{proc.ProcessName} [{proc.WindowTitle}]";
                    }
                    textPanel.Children.Add(nameText);

                    var pathText = new Microsoft.UI.Xaml.Controls.TextBlock
                    {
                        Text = $"{proc.MemoryMB} MB  -  {proc.ExePath}",
                        FontSize = 11,
                        Opacity = 0.7,
                        TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis
                    };
                    textPanel.Children.Add(pathText);

                    container.Children.Add(textPanel);
                    item.Content = container;
                    processList.Items.Add(item);
                }

                var selectDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Select Running Game" : "选择正在运行的游戏",
                    Content = processList,
                    PrimaryButtonText = Translator.IsEnglish ? "Add Selected" : "添加选中",
                    CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await selectDialog.ShowAsync();
                if (result == ContentDialogResult.Primary && processList.SelectedItem is ListViewItem selectedItem &&
                    selectedItem.Tag is ProcessDetector.GameProcessInfo selectedProc)
                {
                    // 检查游戏是否已经在列表中
                    if (Games.Any(g => g.ExePath.Equals(selectedProc.ExePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        ShowMessage(Translator.IsEnglish ? "Game already in list" : "该游戏已在列表中");
                        return;
                    }

                    // 使用手动添加exe的方式添加游戏（保底选项，支持所有游戏，自动检测DX支持）
                    var game = GameScanner.AddGameByExe(selectedProc.ExePath, isDx9Manual: false);
                    if (game != null)
                    {
                        Games.Add(game);
                        SetGameIcon(game);
                        SaveGamesNow();

                        // 提示用户游戏正在运行，安装补丁前请先关闭
                        var warningDialog = new ContentDialog
                        {
                            Title = Translator.IsEnglish ? "Game is Running" : "游戏正在运行",
                            Content = Translator.IsEnglish ?
                                $"Detected game is currently running.\n\nGame: {game.Name}\nExe: {selectedProc.ExePath}\n\n⚠ Important: Please first enable DLSS/Frame Generation in game settings, then close the game before installing patches, otherwise files may be locked and installation may fail." :
                                $"检测到游戏当前正在运行。\n\n游戏：{game.Name}\n路径：{selectedProc.ExePath}\n\n⚠ 重要提示：请先在游戏设置中开启DLSS/帧生成相关功能，然后再关闭游戏，最后再安装补丁，否则文件可能被占用导致安装失败。",
                            CloseButtonText = "OK",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await warningDialog.ShowAsync();

                        ShowMessage(Translator.T("Dlg_Add_Success"));
                    }
                    else
                    {
                        ShowMessage(Translator.IsEnglish ? "Failed to add game" : "添加游戏失败");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"检测运行中游戏出错：{ex.Message}\n\n堆栈跟踪：\n{ex.StackTrace}");
            }
        }

        // 选中游戏
        private void GameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var game = SelectedGame;
            if (game == null)
            {
                ActionPanel.Visibility = Visibility.Collapsed;
                return;
            }

            ActionPanel.Visibility = Visibility.Visible;
            UpdateButtonStates(game);
        }

        // 右键菜单打开前处理：自动选中右键的游戏，并更新菜单项状态
        private void GameMenuFlyout_Opening(object sender, object e)
        {
            var flyout = (MenuFlyout)sender;
            _contextMenuGame = null;
            if (flyout.Target == null) return;

            // Target是DataTemplate里的Grid，它的DataContext就是GameInfo
            if (flyout.Target is FrameworkElement fe && fe.DataContext is GameInfo game)
            {
                _contextMenuGame = game;

                // ===== 保留多选状态 =====
                // 如果右键点击前有多个游戏被选中，并且右键点击的游戏也在选中列表中，保留多选
                // 否则只选中右键点击的这个游戏
                if (_rightClickSelectedGames.Count > 1 && _rightClickSelectedGames.Contains(game))
                {
                    // 恢复之前的多选状态（ListView可能已经把选择改成了单选）
                    GameListView.SelectedItems.Clear();
                    foreach (var g in _rightClickSelectedGames)
                    {
                        if (Games.Contains(g))
                        {
                            GameListView.SelectedItems.Add(g);
                        }
                    }
                }
                else
                {
                    // 只选中右键点击的这个游戏
                    GameListView.SelectedItem = game;
                }

                // 判断是否是多选状态
                bool isMultiSelect = _rightClickSelectedGames.Count > 1 && _rightClickSelectedGames.Contains(game);

                // 动态更新菜单项
                var items = flyout.Items;
                if (items.Count >= 10)
                {
                    bool en = Translator.IsEnglish;

                    // ===== 多选状态：只保留"移除列表"可用，其他全部禁用 =====
                    if (isMultiSelect)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            if (items[i] is MenuFlyoutItem mfi)
                            {
                                // 索引11是"移除列表"，其他全部禁用
                                mfi.IsEnabled = (i == 11);
                            }
                        }
                        // 更新移除列表的文字，显示选中数量
                        if (items.Count > 11 && items[11] is MenuFlyoutItem removeItem)
                        {
                            removeItem.Text = en ? 
                                $"Remove Selected ({_rightClickSelectedGames.Count})" : 
                                $"移除所选游戏 ({_rightClickSelectedGames.Count})";
                        }
                        return; // 多选状态下不需要更新其他菜单项
                    }

                    // 0: 多帧生成
                    var frameGenItem = (MenuFlyoutItem)items[0];
                    frameGenItem.Text = game.FrameGenEnabled ?
                        (en ? "Restore Frame Gen" : "一键还原多帧生成") :
                        (en ? "Enable Frame Gen" : "一键开启多帧生成");
                    frameGenItem.IsEnabled = game.SupportsFrameGen || game.FrameGenEnabled;
                    // 2077游戏：隐藏通用多帧生成，使用专用补丁
                    frameGenItem.Visibility = game.Is2077 ? Visibility.Collapsed : Visibility.Visible;
                    
                    // RE引擎游戏：DLSS5和多帧生成冲突
                    if (game.IsREEngine)
                    {
                        if (game.Dlss5Enabled && !game.FrameGenEnabled)
                        {
                            frameGenItem.IsEnabled = false;
                        }
                    }

                    // 1: DLSS5
                    var dlss5Item = (MenuFlyoutItem)items[1];
                    dlss5Item.Text = game.Dlss5Enabled ?
                        (en ? "Restore DLSS5" : "一键还原DLSS5") :
                        (en ? "Enable DLSS5" : "开启DLSS5");
                    
                    // RE引擎游戏：DLSS5和多帧生成冲突
                    if (game.IsREEngine && game.FrameGenEnabled && !game.Dlss5Enabled)
                    {
                        dlss5Item.IsEnabled = false;
                    }
                    else
                    {
                        dlss5Item.IsEnabled = true;
                    }

                    // 2: DX9 DLSS5
                    var dx9Item = (MenuFlyoutItem)items[2];
                    dx9Item.Text = game.Dx9Dlss5Enabled ?
                        (en ? "Restore DX9 DLSS5" : "一键还原DX9 DLSS5") :
                        (en ? "Enable DX9 DLSS5 (for old games)" : "开启DX9 DLSS5（适合老游戏）");
                    // 所有非2077游戏都显示DX9 DLSS5选项
                    dx9Item.Visibility = game.Is2077 ? Visibility.Collapsed : Visibility.Visible;

                    // 3: 2077专用
                    var cp2077Item = (MenuFlyoutItem)items[3];
                    cp2077Item.Text = game.FrameGenEnabled ?
                        (en ? "Restore 2077 Patch" : "还原2077补丁") :
                        (en ? "2077 Exclusive Patch" : "2077专用补丁");
                    cp2077Item.Visibility = game.Is2077 ? Visibility.Visible : Visibility.Collapsed;

                    // 5: 配置多帧生成
                    var configItem = (MenuFlyoutItem)items[5];
                    configItem.Text = en ? "Configure Frame Gen" : "配置多帧生成";
                    configItem.IsEnabled = game.FrameGenEnabled && game.FrameGenMode == "经典模式";

                    // 6: 配置名修改
                    var configNameItem = (MenuFlyoutItem)items[6];
                    configNameItem.Text = en ? "Modify Config Name" : "配置名修改";
                    configNameItem.IsEnabled = game.FrameGenEnabled && game.FrameGenMode == "高级模式";

                    // 7: 排错修复
                    var troubleshootItem = (MenuFlyoutItem)items[7];
                    troubleshootItem.Text = en ? "Troubleshooting" : "排错修复";
                    troubleshootItem.IsEnabled = game.FrameGenEnabled;

                    // 9: 一键还原
                    var restoreItem = (MenuFlyoutItem)items[9];
                    restoreItem.Text = en ? "One-Click Restore" : "一键还原";
                    restoreItem.IsEnabled = game.FrameGenEnabled || game.Dlss5Enabled || game.Dx9Dlss5Enabled;

                    // 10: 打开游戏目录
                    if (items.Count > 10)
                    {
                        var openFolderItem = (MenuFlyoutItem)items[10];
                        openFolderItem.Text = en ? "Open Game Folder" : "打开游戏目录";
                    }

                    // 11: 移除列表
                    if (items.Count > 11)
                    {
                        var removeItem = (MenuFlyoutItem)items[11];
                        removeItem.Text = en ? "Remove from List" : "移除列表";
                    }
                }
            }
        }

        // 查找指定类型的父元素
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typed) return typed;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        // 更新按钮状态
        private void UpdateButtonStates(GameInfo game)
        {
            // ===== 多选状态：禁用所有下方功能按钮 =====
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
                
                // 给按钮加上提示
                var multiSelectTip = Translator.IsEnglish ? 
                    "Multiple games selected. Only 'Remove from list' is available via right-click menu." : 
                    "已选择多个游戏，仅可通过右键菜单使用「移除列表」功能。";
                ToolTipService.SetToolTip(FrameGenBtn, multiSelectTip);
                ToolTipService.SetToolTip(Dlss5Btn, multiSelectTip);
                ToolTipService.SetToolTip(Dx9Dlss5Btn, multiSelectTip);
                ToolTipService.SetToolTip(RestoreBtn, multiSelectTip);
                return;
            }
            
            // 2077特殊处理
            if (game.Is2077)
            {
                Cp2077Btn.Visibility = Visibility.Visible;
                Cp2077Btn.IsEnabled = true; // 确保2077按钮可用（多选禁用后恢复）
                FrameGenBtn.Visibility = Visibility.Collapsed;
                Dlss5Btn.Visibility = Visibility.Visible;
                Dx9Dlss5Btn.Visibility = Visibility.Collapsed;
                ConfigBtn.IsEnabled = false;
                ConfigNameBtn.IsEnabled = false;
                
                // 清除多选状态下的提示
                ToolTipService.SetToolTip(Cp2077Btn, null);

                // 2077按钮文字根据安装状态切换
                Cp2077Btn.Content = game.FrameGenEnabled ?
                    (Translator.IsEnglish ? "Restore 2077 Patch" : "还原2077补丁") :
                    Translator.T("Btn_2077");
            }
            else
            {
                // 其他游戏：同时显示多帧生成、通用DLSS5、DX9 DLSS5，让用户自己选择
                FrameGenBtn.Visibility = Visibility.Visible;
                Dlss5Btn.Visibility = Visibility.Visible;
                Dx9Dlss5Btn.Visibility = Visibility.Visible;
                Dx9Dlss5Btn.IsEnabled = true; // 确保DX9 DLSS5按钮可用（多选禁用后恢复）
                Cp2077Btn.Visibility = Visibility.Collapsed;
                ConfigBtn.IsEnabled = game.FrameGenEnabled && game.FrameGenMode == "经典模式";
                ConfigNameBtn.IsEnabled = game.FrameGenEnabled && game.FrameGenMode == "高级模式";
                
                // 清除多选状态下的提示，恢复正常提示
                ToolTipService.SetToolTip(Dx9Dlss5Btn, Translator.IsEnglish ? 
                    "For older DX9 games, uses dgVoodoo to translate DX9 to DX11" : 
                    "适合老游戏（DX9游戏），通过dgVoodoo将DX9转译到DX11运行");
                ToolTipService.SetToolTip(FrameGenBtn, null);
                ToolTipService.SetToolTip(Dlss5Btn, null);
                ToolTipService.SetToolTip(RestoreBtn, null);
            }

            // 多帧生成按钮文字
            FrameGenBtn.Content = game.FrameGenEnabled ?
                (Translator.IsEnglish ? "Restore Frame Gen" : "一键还原多帧生成") :
                Translator.T("Btn_FrameGen");
            FrameGenBtn.IsEnabled = game.SupportsFrameGen || game.FrameGenEnabled;
            
            // RE引擎游戏：DLSS5和多帧生成冲突，不能同时开启
            if (game.IsREEngine)
            {
                if (game.Dlss5Enabled && !game.FrameGenEnabled)
                {
                    // 已开启DLSS5，禁用多帧生成按钮
                    FrameGenBtn.IsEnabled = false;
                    ToolTipService.SetToolTip(FrameGenBtn, Translator.IsEnglish ? 
                        "RE Engine games: DLSS5 conflicts with Frame Generation, cannot enable both" : 
                        "RE引擎游戏：DLSS5与多帧生成冲突，无法同时开启");
                }
                else if (game.FrameGenEnabled && !game.Dlss5Enabled)
                {
                    // 已开启多帧生成，禁用DLSS5按钮
                    Dlss5Btn.IsEnabled = false;
                    ToolTipService.SetToolTip(Dlss5Btn, Translator.IsEnglish ? 
                        "RE Engine games: DLSS5 conflicts with Frame Generation, cannot enable both" : 
                        "RE引擎游戏：DLSS5与多帧生成冲突，无法同时开启");
                }
                else
                {
                    Dlss5Btn.IsEnabled = true;
                    ToolTipService.SetToolTip(Dlss5Btn, Translator.IsEnglish ? 
                        "Enable DLSS5 Super Resolution for selected game" : 
                        "为选中游戏开启DLSS5超分辨率");
                }
            }
            else
            {
                Dlss5Btn.IsEnabled = true;
            }

            // DLSS5按钮文字
            Dlss5Btn.Content = game.Dlss5Enabled ?
                (Translator.IsEnglish ? "Restore DLSS5" : "一键还原DLSS5") :
                Translator.T("Btn_Dlss5");

            // DX9 DLSS5按钮文字
            Dx9Dlss5Btn.Content = game.Dx9Dlss5Enabled ?
                (Translator.IsEnglish ? "Restore DX9 DLSS5" : "一键还原DX9 DLSS5") :
                Translator.T("Btn_Dx9Dlss5");

            // 排错按钮
            TroubleshootBtn.IsEnabled = game.FrameGenEnabled;

            // 还原按钮
            RestoreBtn.IsEnabled = game.FrameGenEnabled || game.Dlss5Enabled || game.Dx9Dlss5Enabled;
        }

        // 开启多帧生成
        private async void FrameGenBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var game = SelectedGame;
                if (game == null) return;

                if (game.FrameGenEnabled)
                {
                    // 还原（带确认）
                    if (await ConfirmAndRestoreFrameGen(game))
                    {
                        ShowMessage(Translator.T("Dlg_FrameGen_Restored"));
                        UpdateButtonStates(game);
                    }
                    return;
                }

                // RE引擎游戏
                if (game.IsREEngine)
                {
                    // RE引擎游戏：DLSS5和多帧生成冲突
                    if (game.Dlss5Enabled)
                    {
                        var conflictDialog = new ContentDialog
                        {
                            Title = Translator.T("Dlg_Title_Warning"),
                            Content = Translator.IsEnglish ? 
                                "RE Engine games: DLSS5 conflicts with Frame Generation. Please restore DLSS5 first, then enable Frame Generation." : 
                                "RE引擎游戏：DLSS5与多帧生成冲突，请先还原DLSS5，再开启多帧生成。",
                            CloseButtonText = Translator.T("Common_Close"),
                            XamlRoot = this.Content.XamlRoot
                        };
                        await conflictDialog.ShowAsync();
                        return;
                    }
                    
                    var result = await ShowModeDialog(Translator.T("Dlg_FrameGen_Title"),
                        new[] { (Translator.T("Dlg_FrameGen_RE"), Translator.IsEnglish ? "RE Engine exclusive patch (conflicts with DLSS5, cannot enable both)" : "RE引擎专属补丁（与DLSS5冲突，无法同时开启）"),
                                (Translator.T("Dlg_FrameGen_Classic"), Translator.T("Dlg_FrameGen_ClassicDesc")),
                                (Translator.T("Dlg_FrameGen_Advanced"), Translator.T("Dlg_FrameGen_AdvancedDesc")) });
                    if (result == 0) 
                    {
                        FilePatcher.InstallREEngineFrameGen(game);
                        // 安装成功后提示用户无法开启DLSS5
                        var warnDialog = new ContentDialog
                        {
                            Title = Translator.T("Dlg_Title_Tip"),
                            Content = Translator.IsEnglish ? 
                                "RE Engine Frame Generation enabled. Note: DLSS5 is now disabled due to conflict." : 
                                "RE引擎多帧生成已开启。注意：由于冲突，DLSS5功能已被禁用，无法同时开启。",
                            CloseButtonText = Translator.T("Common_Close"),
                            XamlRoot = this.Content.XamlRoot
                        };
                        await warnDialog.ShowAsync();
                    }
                    else if (result == 1) FilePatcher.InstallClassicFrameGen(game);
                    else if (result == 2) FilePatcher.InstallAdvancedFrameGen(game);
                    else return; // 取消
                }
                else
                {
                    // 根据显卡系列选择
                    var gpu = await GetGpuInfoAsync();
                    if (!gpu.IsNvidia)
                    {
                        // 非NVIDIA显卡不支持多帧生成
                        var notSupportedDialog = new ContentDialog
                        {
                            Title = Translator.T("Dlg_Title_Warning"),
                            Content = Translator.IsEnglish ?
                                "Frame Generation is only supported on NVIDIA RTX 20/30/40/50 series graphics cards." :
                                "多帧生成功能仅支持NVIDIA RTX 20/30/40/50系显卡。",
                            CloseButtonText = Translator.T("Common_Close"),
                            XamlRoot = this.Content.XamlRoot
                        };
                        await notSupportedDialog.ShowAsync();
                        return;
                    }
                    if (gpu.Series == "RTX20")
                    {
                        FilePatcher.InstallRTX20FrameGen(game);
                    }
                    else if (gpu.Series == "RTX30")
                    {
                        FilePatcher.InstallRTX30FrameGen(game);
                    }
                    else
                    {
                        // RTX40/50系：经典/高级选择
                        var result = await ShowModeDialog(Translator.T("Dlg_FrameGen_Title"),
                            new[] { (Translator.T("Dlg_FrameGen_Classic"), Translator.T("Dlg_FrameGen_ClassicDesc")),
                                    (Translator.T("Dlg_FrameGen_Advanced"), Translator.T("Dlg_FrameGen_AdvancedDesc")) });
                        if (result == 0) FilePatcher.InstallClassicFrameGen(game);
                        else if (result == 1) FilePatcher.InstallAdvancedFrameGen(game);
                        else return; // 取消
                    }
                }

                ShowMessage(Translator.T("Dlg_FrameGen_Installed"));
                UpdateButtonStates(game);
            }
            catch (Exception ex)
            {
                ShowMessage($"{Translator.T("Common_Error")}: {ex.Message}");
            }
        }

        // 开启DLSS5
        private async void Dlss5Btn_Click(object sender, RoutedEventArgs e)
        {
            var game = SelectedGame;
            if (game == null) return;

            if (game.Dlss5Enabled)
            {
                if (await ConfirmAndRestoreDLSS5(game))
                {
                    ShowMessage(Translator.T("Dlg_Dlss5_Restored"));
                    UpdateButtonStates(game);
                }
                return;
            }

            // 燕云十六声特殊处理
            if (game.IsYanYun)
            {
                FilePatcher.InstallYanYunDLSS5(game);
                ShowMessage(Translator.T("Dlg_Dlss5_Installed"));
                UpdateButtonStates(game);
                return;
            }

            // RE引擎
            if (game.IsREEngine)
            {
                // RE引擎游戏：DLSS5和多帧生成冲突
                if (game.FrameGenEnabled)
                {
                    var conflictDialog = new ContentDialog
                    {
                        Title = Translator.T("Dlg_Title_Warning"),
                        Content = Translator.IsEnglish ? 
                            "RE Engine games: DLSS5 conflicts with Frame Generation. Please restore Frame Generation first, then enable DLSS5." : 
                            "RE引擎游戏：DLSS5与多帧生成冲突，请先还原多帧生成，再开启DLSS5。",
                        CloseButtonText = Translator.T("Common_Close"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    await conflictDialog.ShowAsync();
                    return;
                }
                
                var dialog = new ContentDialog
                {
                    Title = Translator.T("Dlg_Title_Tip"),
                    Content = Translator.T("Dlg_Dlss5_REWarn"),
                    PrimaryButtonText = Translator.IsEnglish ? "Continue" : "继续安装",
                    CloseButtonText = Translator.T("Common_Cancel"),
                    XamlRoot = this.Content.XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    FilePatcher.InstallREEngineDLSS5(game);
                    
                    // 安装成功后提示用户无法开启多帧生成
                    var warnDialog = new ContentDialog
                    {
                        Title = Translator.T("Dlg_Title_Tip"),
                        Content = Translator.IsEnglish ? 
                            "RE Engine DLSS5 enabled. Note: Frame Generation is now disabled due to conflict." : 
                            "RE引擎DLSS5已开启。注意：由于冲突，多帧生成功能已被禁用，无法同时开启。",
                        CloseButtonText = Translator.T("Common_Close"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    await warnDialog.ShowAsync();
                }
                UpdateButtonStates(game);
                return;
            }

            // 通用DLSS5
            var gpu = await GetGpuInfoAsync();
            if (gpu.IsNvidia)
            {
                FilePatcher.InstallCommonDLSS5(game);
                ShowMessage(Translator.T("Dlg_Dlss5_Installed"));
            }
            else if (gpu.IsAmd && gpu.SupportsDLSS5)
            {
                var dialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "AMD GPU Notice" : "AMD显卡提示",
                    Content = Translator.T("Dlg_Dlss5_AmdWarn"),
                    PrimaryButtonText = Translator.IsEnglish ? "Continue" : "继续安装",
                    CloseButtonText = Translator.T("Common_Cancel"),
                    XamlRoot = this.Content.XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
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

        // DX9 DLSS5
        private async void Dx9Dlss5Btn_Click(object sender, RoutedEventArgs e)
        {
            var game = SelectedGame;
            if (game == null) return;

            if (game.Dx9Dlss5Enabled)
            {
                // 已开启DX9 DLSS5：弹出选择框，让用户选择还原还是替换dll
                var result = await ShowModeDialog(
                    Translator.IsEnglish ? "DX9 DLSS5 Options" : "DX9 DLSS5 选项",
                    new[] { 
                        (Translator.IsEnglish ? "Restore DX9 DLSS5" : "还原DX9 DLSS5", Translator.IsEnglish ? "Remove all DX9 DLSS5 patches" : "还原DX9 DLSS5（删除所有补丁）"),
                        (Translator.IsEnglish ? "Replace DLL" : "替换DLL", Translator.IsEnglish ? "Move dgVoodoo files to game's DLL directory (for games where patches don't work in exe directory)" : "替换dll（将dgVoodoo三个补丁移到游戏运行dll目录，适用于补丁不生效的情况）")
                    });
                
                if (result == 0)
                {
                    // 还原（带确认）
                    if (await ConfirmAndRestoreDLSS5(game))
                    {
                        ShowMessage(Translator.T("Dlg_Dx9_Restored"));
                    }
                }
                else if (result == 1)
                {
                    // 替换dll
                    bool success = FilePatcher.ReplaceDgvoodooDll(game);
                    if (success)
                    {
                        ShowMessage(Translator.IsEnglish ? 
                            "dgVoodoo files moved to game's DLL directory. Please test the game again." : 
                            "已将dgVoodoo三个补丁移到游戏运行dll目录，请重新测试游戏是否生效。");
                    }
                    else
                    {
                        ShowMessage(Translator.IsEnglish ? 
                            "Could not find game's DLL directory." : 
                            "未找到游戏运行dll目录，替换失败。");
                    }
                }
                else return; // 取消
                
                UpdateButtonStates(game);
                return;
            }

            var result2 = await ShowModeDialog(Translator.IsEnglish ? "Select DX9 DLSS5 Mode" : "选择DX9 DLSS5模式",
                new[] { 
                    (Translator.IsEnglish ? "32-bit" : "32位", Translator.IsEnglish ? "For 32-bit DX9 games" : "适用于32位DX9游戏"), 
                    (Translator.IsEnglish ? "64-bit" : "64位", Translator.IsEnglish ? "For 64-bit DX9 games" : "适用于64位DX9游戏") 
                });

            if (result2 == 0) FilePatcher.InstallDX9DLSS5_32(game);
            else if (result2 == 1) FilePatcher.InstallDX9DLSS5_64(game);
            else return; // 取消

            ShowMessage(Translator.T("Dlg_Dx9_Installed"));
            UpdateButtonStates(game);
        }

        // 2077专用
        private async void Cp2077Btn_Click(object sender, RoutedEventArgs e)
        {
            var game = SelectedGame;
            if (game == null) return;

            if (game.FrameGenEnabled)
            {
                if (await ConfirmAndRestoreFrameGen(game))
                {
                    ShowMessage(Translator.T("Dlg_2077_Restored"));
                }
            }
            else
            {
                // 安装前确认对话框
                var confirmDialog = new ContentDialog
                {
                    Title = Translator.IsEnglish ? "Install 2077 Exclusive Patch" : "安装2077专用补丁",
                    Content = Translator.IsEnglish ?
                        "Are you sure you want to install the Cyberpunk 2077 exclusive frame generation patch?\n\nPlease make sure you have enabled DLSS/Frame Generation in the game settings first." :
                        "确定要安装赛博朋克2077专用多帧生成补丁吗？\n\n请确保已经先在游戏设置中开启了DLSS/帧生成相关功能。",
                    PrimaryButtonText = Translator.IsEnglish ? "Install" : "安装",
                    CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                    XamlRoot = this.Content.XamlRoot
                };
                var result = await confirmDialog.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                FilePatcher.Install2077FrameGen(game);

                // 弹出使用说明
                var usageDialog = new ContentDialog
                {
                    Title = Translator.T("Dlg_2077_Usage"),
                    Content = Translator.T("Dlg_2077_UsageText"),
                    CloseButtonText = Translator.IsEnglish ? "Got it" : "我知道了",
                    XamlRoot = this.Content.XamlRoot
                };
                await usageDialog.ShowAsync();
            }
            UpdateButtonStates(game);
        }

        // 配置多帧生成
        private async void ConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            var game = SelectedGame;
            if (game == null) return;

            var exeDir = Path.GetDirectoryName(game.ExePath)!;
            var configPath = Path.Combine(exeDir, "RTX40MFG_config.json");

            if (!File.Exists(configPath))
            {
                ShowMessage(Translator.T("Dlg_Config_NotFound"));
                return;
            }

            // 读取配置
            MfgConfig config;
            try
            {
                var json = File.ReadAllText(configPath);
                config = System.Text.Json.JsonSerializer.Deserialize<MfgConfig>(json) ?? new MfgConfig();
            }
            catch
            {
                ShowMessage(Translator.T("Dlg_Config_ReadFail"));
                return;
            }

            // 构建编辑界面
            var multiplierCombo = new ComboBox { Header = Translator.T("Dlg_Config_Multiplier") };
            multiplierCombo.Items.Add(Translator.IsEnglish ? "2x" : "2倍");
            multiplierCombo.Items.Add(Translator.IsEnglish ? "3x" : "3倍");
            multiplierCombo.Items.Add(Translator.IsEnglish ? "4x (Recommended)" : "4倍（推荐）");
            multiplierCombo.Items.Add(Translator.IsEnglish ? "5x" : "5倍");
            multiplierCombo.Items.Add(Translator.IsEnglish ? "6x (Experimental)" : "6倍（实验性）");
            multiplierCombo.SelectedIndex = Math.Clamp(config.multiplier - 2, 0, 4);

            var modeCombo = new ComboBox { Header = Translator.T("Dlg_Config_Mode") };
            modeCombo.Items.Add(Translator.IsEnglish ? "fixed - Fixed multiplier" : "fixed - 固定倍率");
            modeCombo.Items.Add(Translator.IsEnglish ? "dynamic - Dynamic frame gen" : "dynamic - 动态多帧生成");
            modeCombo.SelectedIndex = config.mode == "dynamic" ? 1 : 0;

            var targetFpsCombo = new ComboBox { Header = Translator.IsEnglish ? "Dynamic target FPS (dynamic mode only)" : "动态目标帧率（仅dynamic模式生效）", IsEnabled = config.mode == "dynamic" };
            targetFpsCombo.Items.Add(Translator.IsEnglish ? "0 - Auto (monitor refresh rate)" : "0 - 自动跟随显示器刷新率");
            targetFpsCombo.Items.Add("120");
            targetFpsCombo.Items.Add("144");
            targetFpsCombo.Items.Add("165");
            targetFpsCombo.Items.Add("240");
            var fpsItems = new[] { 0, 120, 144, 165, 240 };
            targetFpsCombo.SelectedIndex = Math.Max(0, Array.IndexOf(fpsItems, config.dynamicTargetFrameRate));

            var expToggle = new ToggleSwitch
            {
                Header = Translator.T("Dlg_Config_Exp"),
                IsOn = config.dynamicExperimental56,
                OffContent = Translator.T("Settings_Off"),
                OnContent = Translator.T("Settings_On")
            };

            modeCombo.SelectionChanged += (s, args) =>
            {
                targetFpsCombo.IsEnabled = modeCombo.SelectedIndex == 1;
            };

            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(multiplierCombo);
            panel.Children.Add(modeCombo);
            panel.Children.Add(targetFpsCombo);
            panel.Children.Add(expToggle);

            var dialog = new ContentDialog
            {
                Title = Translator.T("Dlg_Config_Title"),
                Content = panel,
                PrimaryButtonText = Translator.T("Common_Save"),
                CloseButtonText = Translator.T("Common_Cancel"),
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                config.multiplier = multiplierCombo.SelectedIndex + 2;
                config.mode = modeCombo.SelectedIndex == 1 ? "dynamic" : "fixed";
                config.dynamicTargetFrameRate = fpsItems[targetFpsCombo.SelectedIndex];
                config.dynamicExperimental56 = expToggle.IsOn;

                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configPath, json);
                    ShowMessage(Translator.T("Dlg_Config_Saved"));
                }
                catch
                {
                    ShowMessage(Translator.T("Dlg_Config_SaveFail"));
                }
            }
        }

        // 配置类
        private class MfgConfig
        {
            public int version { get; set; } = 7;
            public int dynamicTargetFrameRate { get; set; } = 0;
            public int multiplier { get; set; } = 4;
            public string mode { get; set; } = "fixed";
            public bool generatedOnlyDebug { get; set; } = false;
            public bool dynamicExperimental56 { get; set; } = false;
        }

        // 配置名修改
        private async void ConfigNameBtn_Click(object sender, RoutedEventArgs e)
        {
            var game = SelectedGame;
            if (game == null) return;

            if (game.FrameGenMode != "高级模式" && game.FrameGenMode != "Advanced Mode")
            {
                ShowMessage(Translator.T("Dlg_ConfigName_OnlyAdvanced"));
                return;
            }

            var exeDir = Path.GetDirectoryName(game.ExePath)!;

            // 先弹出使用提示
            var tipDialog = new ContentDialog
            {
                Title = Translator.T("Dlg_ConfigName_TipTitle"),
                Content = Translator.T("Dlg_ConfigName_TipText"),
                PrimaryButtonText = Translator.T("Dlg_ConfigName_IKnow"),
                CloseButtonText = Translator.T("Common_Cancel"),
                XamlRoot = this.Content.XamlRoot
            };
            if (await tipDialog.ShowAsync() != ContentDialogResult.Primary) return;

            // 配置名列表
            var configNames = new[] { "dinput8", "d3d11", "winmm", "d3d9", "winhttp", "wininet", "dsound", "binkw64", "xinput1_3", "bink2w64", "xinput1_4", "xinputuap" };

            var nameCombo = new ComboBox { Header = Translator.T("Dlg_ConfigName_SelectName") };
            foreach (var name in configNames) nameCombo.Items.Add(name);
            nameCombo.SelectedIndex = 0;

            ContentDialog? dialog = null;
            var dxgiButton = new Button
            {
                Content = Translator.IsEnglish ? "Rename dxgi.dll → d3d12.dll (click to execute)" : "修改dxgi.dll → d3d12.dll（单独操作，点击即执行）",
                Margin = new Thickness(0, 8, 0, 0)
            };
            dxgiButton.Click += (s, args) =>
            {
                // 先关闭外层弹窗，避免嵌套
                dialog?.Hide();

                var dxgiPath = Path.Combine(exeDir, "dxgi.dll");
                var d3d12Path = Path.Combine(exeDir, "d3d12.dll");
                if (!File.Exists(dxgiPath))
                {
                    ShowMessage(Translator.T("Dlg_ConfigName_DxgiNotFound"));
                    return;
                }
                if (File.Exists(d3d12Path))
                {
                    ShowMessage(Translator.T("Dlg_ConfigName_DxgiConflict"));
                    return;
                }
                File.Move(dxgiPath, d3d12Path);
                ShowMessage(Translator.T("Dlg_ConfigName_DxgiDone"));
            };

            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(nameCombo);
            panel.Children.Add(dxgiButton);

            dialog = new ContentDialog
            {
                Title = Translator.T("Dlg_ConfigName_Title"),
                Content = panel,
                PrimaryButtonText = Translator.T("Dlg_ConfigName_ModifyVersion"),
                CloseButtonText = Translator.T("Common_Close"),
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var newName = configNames[nameCombo.SelectedIndex];
                var versionDll = Path.Combine(exeDir, "version.dll");
                var versionIni = Path.Combine(exeDir, "version.ini");
                var newDll = Path.Combine(exeDir, newName + ".dll");
                var newIni = Path.Combine(exeDir, newName + ".ini");

                if (!File.Exists(versionDll) && !File.Exists(versionIni))
                {
                    ShowMessage(Translator.T("Dlg_ConfigName_NotFound"));
                    return;
                }

                // 检测冲突
                if (File.Exists(newDll) || File.Exists(newIni))
                {
                    ShowMessage(string.Format(Translator.T("Dlg_ConfigName_Conflict"), newName));
                    return;
                }

                // 外层弹窗已关闭，这里可以安全弹窗确认
                var confirm = new ContentDialog
                {
                    Title = Translator.T("Dlg_Title_Confirm"),
                    Content = string.Format(Translator.T("Dlg_ConfigName_Confirm"), newName),
                    PrimaryButtonText = Translator.T("Common_Yes"),
                    CloseButtonText = Translator.T("Common_No"),
                    XamlRoot = this.Content.XamlRoot
                };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                {
                    if (File.Exists(versionDll)) File.Move(versionDll, newDll);
                    if (File.Exists(versionIni)) File.Move(versionIni, newIni);
                    ShowMessage(string.Format(Translator.T("Dlg_ConfigName_Done"), newName));
                }
            }
        }

        // 排错修复
        private async void TroubleshootBtn_Click(object sender, RoutedEventArgs e)
        {
            var game = SelectedGame;
            if (game == null) return;

            if (!game.FrameGenEnabled)
            {
                ShowMessage(Translator.T("Dlg_Trouble_NeedFrameGen"));
                return;
            }

            // 获取可替换文件列表
            var files = FilePatcher.GetTroubleshootFiles(game);
            if (files.Count == 0)
            {
                ShowMessage(Translator.T("Dlg_Trouble_NoPatch"));
                return;
            }

            // 构建文件选择列表
            var checkBoxes = new List<CheckBox>();
            var filePanel = new StackPanel { Spacing = 4 };

            var selectAllCheckBox = new CheckBox
            {
                Content = Translator.T("Dlg_Trouble_SelectAll"),
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 8)
            };

            foreach (var (fileName, existsInGame) in files)
            {
                var cb = new CheckBox
                {
                    Content = fileName + (existsInGame ? "" : (Translator.IsEnglish ? " (not in game, display only)" : "（游戏中不存在，仅展示）")),
                    IsEnabled = existsInGame,
                    IsChecked = existsInGame,
                    Opacity = existsInGame ? 1 : 0.5
                };
                checkBoxes.Add(cb);
                filePanel.Children.Add(cb);
            }

            selectAllCheckBox.Checked += (s, args) =>
            {
                foreach (var cb in checkBoxes)
                {
                    if (cb.IsEnabled) cb.IsChecked = true;
                }
            };
            selectAllCheckBox.Unchecked += (s, args) =>
            {
                foreach (var cb in checkBoxes)
                {
                    if (cb.IsEnabled) cb.IsChecked = false;
                }
            };

            var scrollViewer = new ScrollViewer
            {
                Content = filePanel,
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var mainPanel = new StackPanel { Spacing = 8 };
            mainPanel.Children.Add(new TextBlock
            {
                Text = Translator.T("Dlg_Trouble_SelectTip"),
                TextWrapping = TextWrapping.Wrap
            });
            mainPanel.Children.Add(selectAllCheckBox);
            mainPanel.Children.Add(scrollViewer);

            var dialog = new ContentDialog
            {
                Title = Translator.T("Dlg_Trouble_Title"),
                Content = mainPanel,
                PrimaryButtonText = Translator.T("Dlg_Trouble_ReplaceSelected"),
                CloseButtonText = Translator.T("Common_Cancel"),
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var filesToReplace = checkBoxes
                    .Where(cb => cb.IsChecked == true && cb.IsEnabled)
                    .Select(cb => cb.Content?.ToString()?.Split('（')[0] ?? "")
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToList();

                if (filesToReplace.Count == 0)
                {
                    ShowMessage(Translator.T("Dlg_Trouble_NoSelect"));
                    return;
                }

                var replaced = FilePatcher.TroubleshootSelected(game, filesToReplace);
                ShowMessage(string.Format(Translator.T("Dlg_Trouble_Done"), replaced));
            }
        }

        // 一键还原
        private async void RestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            var game = SelectedGame;
            if (game == null) return;

            if (FilePatcher.HasBackup(game))
            {
                // 预览两个还原操作的文件并合并
                var previewFrameGen = FilePatcher.PreviewRestoreFrameGen(game);
                var previewDlss5 = FilePatcher.PreviewRestoreDLSS5(game);

                var allFilesToDelete = previewFrameGen.filesToDelete
                    .Concat(previewDlss5.filesToDelete)
                    .Distinct()
                    .ToList();
                var allFilesToRestore = previewFrameGen.filesToRestore
                    .Concat(previewDlss5.filesToRestore)
                    .Distinct()
                    .ToList();
                var allDirsToDelete = previewFrameGen.dirsToDelete
                    .Concat(previewDlss5.dirsToDelete)
                    .Distinct()
                    .ToList();

                var confirmed = await ShowRestoreConfirmDialog(
                    Translator.T("Dlg_Restore_Title"),
                    allFilesToDelete,
                    allFilesToRestore,
                    allDirsToDelete);

                if (confirmed)
                {
                    FilePatcher.RestoreFrameGen(game);
                    FilePatcher.RestoreDLSS5(game);
                    ShowMessage(Translator.T("Dlg_Restore_Done"));
                    UpdateButtonStates(game);
                }
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = Translator.T("Dlg_Restore_NoResponsibility_Title"),
                    Content = Translator.T("Dlg_Restore_NoResponsibility_Warn"),
                    PrimaryButtonText = Translator.IsEnglish ? "Confirm Delete" : "确认删除",
                    CloseButtonText = Translator.T("Common_Cancel"),
                    XamlRoot = this.Content.XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    FilePatcher.ForceClean(game);
                    ShowMessage(Translator.T("Dlg_Restore_NoResp_Done"));
                    UpdateButtonStates(game);
                }
            }
        }

        // 显示还原确认对话框（列出会被删除和恢复的文件）
        private async System.Threading.Tasks.Task<bool> ShowRestoreConfirmDialog(string title, List<string> filesToDelete, List<string> filesToRestore, List<string> dirsToDelete)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                PrimaryButtonText = Translator.IsEnglish ? "Confirm Restore" : "确定还原",
                CloseButtonText = Translator.T("Common_Cancel"),
                XamlRoot = this.Content.XamlRoot
            };

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 400,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var panel = new StackPanel { Spacing = 12 };

            // 警告提示
            var warnText = new TextBlock
            {
                Text = Translator.IsEnglish ? 
                    "⚠️ Warning: This operation will delete the following files and restore original files. Please make sure these are not your personal mod files!" :
                    "⚠️ 警告：此操作将删除以下文件并恢复原始文件，请确认这些不是您的个人mod文件！",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 150, 50))
            };
            panel.Children.Add(warnText);

            // 会被恢复的文件
            if (filesToRestore.Count > 0)
            {
                var restoreTitle = new TextBlock
                {
                    Text = Translator.IsEnglish ? $"📁 Files to be restored ({filesToRestore.Count}):" : $"📁 将被恢复的文件（{filesToRestore.Count}个）：",
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 }
                };
                panel.Children.Add(restoreTitle);

                var restorePanel = new StackPanel { Spacing = 2 };
                foreach (var f in filesToRestore.Take(20))
                {
                    restorePanel.Children.Add(new TextBlock { Text = "  " + f, FontSize = 11, TextWrapping = TextWrapping.Wrap });
                }
                if (filesToRestore.Count > 20)
                {
                    restorePanel.Children.Add(new TextBlock { Text = $"  ... and {filesToRestore.Count - 20} more files", FontSize = 11, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 150, 150)) });
                }
                panel.Children.Add(restorePanel);
            }

            // 会被删除的文件
            if (filesToDelete.Count > 0)
            {
                var deleteTitle = new TextBlock
                {
                    Text = Translator.IsEnglish ? $"🗑️ Files to be deleted ({filesToDelete.Count}):" : $"🗑️ 将被删除的文件（{filesToDelete.Count}个）：",
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 }
                };
                panel.Children.Add(deleteTitle);

                var deletePanel = new StackPanel { Spacing = 2 };
                foreach (var f in filesToDelete.Take(20))
                {
                    deletePanel.Children.Add(new TextBlock { Text = "  " + f, FontSize = 11, TextWrapping = TextWrapping.Wrap });
                }
                if (filesToDelete.Count > 20)
                {
                    deletePanel.Children.Add(new TextBlock { Text = $"  ... and {filesToDelete.Count - 20} more files", FontSize = 11, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 150, 150)) });
                }
                panel.Children.Add(deletePanel);
            }

            // 会被删除的目录
            if (dirsToDelete.Count > 0)
            {
                var dirTitle = new TextBlock
                {
                    Text = Translator.IsEnglish ? $"📂 Directories to be deleted ({dirsToDelete.Count}):" : $"📂 将被删除的目录（{dirsToDelete.Count}个）：",
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 }
                };
                panel.Children.Add(dirTitle);

                var dirPanel = new StackPanel { Spacing = 2 };
                foreach (var d in dirsToDelete)
                {
                    dirPanel.Children.Add(new TextBlock { Text = "  " + d, FontSize = 11, TextWrapping = TextWrapping.Wrap });
                }
                panel.Children.Add(dirPanel);
            }

            // 如果没有任何文件要处理
            if (filesToDelete.Count == 0 && filesToRestore.Count == 0 && dirsToDelete.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = Translator.IsEnglish ? "No patch files found. The game may already be in original state." : "未找到补丁文件，游戏可能已经是原始状态。",
                    TextWrapping = TextWrapping.Wrap
                });
            }

            scrollViewer.Content = panel;
            dialog.Content = scrollViewer;

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        // 确认并还原多帧生成
        private async System.Threading.Tasks.Task<bool> ConfirmAndRestoreFrameGen(GameInfo game)
        {
            var preview = FilePatcher.PreviewRestoreFrameGen(game);
            var confirmed = await ShowRestoreConfirmDialog(
                Translator.IsEnglish ? "Restore Frame Generation" : "还原多帧生成",
                preview.filesToDelete,
                preview.filesToRestore,
                preview.dirsToDelete);

            if (confirmed)
            {
                return FilePatcher.RestoreFrameGen(game);
            }
            return false;
        }

        // 确认并还原DLSS5
        private async System.Threading.Tasks.Task<bool> ConfirmAndRestoreDLSS5(GameInfo game)
        {
            var preview = FilePatcher.PreviewRestoreDLSS5(game);
            var confirmed = await ShowRestoreConfirmDialog(
                Translator.IsEnglish ? "Restore DLSS5" : "还原DLSS5",
                preview.filesToDelete,
                preview.filesToRestore,
                preview.dirsToDelete);

            if (confirmed)
            {
                return FilePatcher.RestoreDLSS5(game);
            }
            return false;
        }

        // 显示模式选择对话框
        private async System.Threading.Tasks.Task<int> ShowModeDialog(string title, (string name, string desc)[] options)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                PrimaryButtonText = Translator.T("Common_Confirm"),
                CloseButtonText = Translator.T("Common_Cancel"),
                XamlRoot = this.Content.XamlRoot
            };

            var panel = new StackPanel { Spacing = 8 };
            var radioButtons = new List<RadioButton>();
            for (int i = 0; i < options.Length; i++)
            {
                var rb = new RadioButton
                {
                    Content = $"{options[i].name}\n{options[i].desc}",
                    IsChecked = i == 0,
                    Tag = i
                };
                radioButtons.Add(rb);
                panel.Children.Add(rb);
            }
            dialog.Content = panel;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                return radioButtons.IndexOf(radioButtons.First(r => r.IsChecked == true));
            }
            return -1;
        }

        // 显示消息
        private async void ShowMessage(string message)
        {
            var dialog = new ContentDialog
            {
                Title = Translator.T("Dlg_Title_Tip"),
                Content = message,
                CloseButtonText = Translator.T("Common_Confirm"),
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        // ========== 右键菜单 ==========
        private void CtxFrameGen_Click(object sender, RoutedEventArgs e) { EnsureContextGame(); FrameGenBtn_Click(sender, e); }
        private void CtxDlss5_Click(object sender, RoutedEventArgs e) { EnsureContextGame(); Dlss5Btn_Click(sender, e); }
        private void CtxDx9Dlss5_Click(object sender, RoutedEventArgs e) { EnsureContextGame(); Dx9Dlss5Btn_Click(sender, e); }
        private void Ctx2077_Click(object sender, RoutedEventArgs e) { EnsureContextGame(); Cp2077Btn_Click(sender, e); }
        private void CtxConfig_Click(object sender, RoutedEventArgs e) { EnsureContextGame(); ConfigBtn_Click(sender, e); }
        private void CtxConfigName_Click(object sender, RoutedEventArgs e) { EnsureContextGame(); ConfigNameBtn_Click(sender, e); }
        private void CtxTroubleshoot_Click(object sender, RoutedEventArgs e) { EnsureContextGame(); TroubleshootBtn_Click(sender, e); }
        private void CtxRestore_Click(object sender, RoutedEventArgs e) { EnsureContextGame(); RestoreBtn_Click(sender, e); }
        private void CtxOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var game = _contextMenuGame ?? SelectedGame;
            if (game != null)
            {
                try
                {
                    // 优先使用exe所在的目录（因为exe所在的目录通常就是游戏真正的运行目录）
                    // 如果exe路径不存在，再使用GamePath
                    string folderPath = "";
                    
                    if (!string.IsNullOrEmpty(game.ExePath) && File.Exists(game.ExePath))
                    {
                        folderPath = Path.GetDirectoryName(game.ExePath) ?? "";
                    }
                    
                    // 如果exe路径不可用，使用GamePath
                    if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    {
                        folderPath = game.GamePath;
                        // 如果GamePath是一个文件，就打开它所在的目录
                        if (File.Exists(folderPath))
                        {
                            folderPath = Path.GetDirectoryName(folderPath) ?? folderPath;
                        }
                    }
                    
                    // 确保路径是绝对路径
                    if (!string.IsNullOrEmpty(folderPath) && !Path.IsPathRooted(folderPath))
                    {
                        folderPath = Path.GetFullPath(folderPath);
                    }
                    
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        // 方式1：直接用目录路径作为FileName，让系统自动打开资源管理器（最可靠）
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = folderPath,
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                            return;
                        }
                        catch { }
                        
                        // 方式2：如果方式1失败，使用explorer.exe打开
                        try
                        {
                            var psi2 = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"\"{folderPath}\"",
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi2);
                            return;
                        }
                        catch { }
                        
                        // 方式3：最后手段
                        System.Diagnostics.Process.Start("explorer.exe", folderPath);
                    }
                    else
                    {
                        ShowMessage(Translator.IsEnglish ? 
                            $"Game folder not found.\nGamePath: {game.GamePath}\nExePath: {game.ExePath}" : 
                            $"游戏目录不存在。\nGamePath: {game.GamePath}\nExePath: {game.ExePath}");
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage(Translator.IsEnglish ? $"Failed to open folder: {ex.Message}" : $"打开目录失败：{ex.Message}");
                }
            }
        }

        // 确保右键菜单操作的是右键选中的游戏
        private void EnsureContextGame()
        {
            if (_contextMenuGame != null)
            {
                GameListView.SelectedItem = _contextMenuGame;
            }
        }

        private void CtxRemove_Click(object sender, RoutedEventArgs e)
        {
            // 批量移除：移除所有选中的游戏
            var selectedGames = GameListView.SelectedItems.Cast<GameInfo>().ToList();
            if (selectedGames.Count > 0)
            {
                foreach (var game in selectedGames)
                {
                    Games.Remove(game);
                }
                SaveGamesNow(); // 显式保存
                ShowMessage(Translator.IsEnglish ? 
                    $"Removed {selectedGames.Count} games from list" : 
                    $"已从列表中移除 {selectedGames.Count} 个游戏");
            }
            else
            {
                // 如果没有选中的游戏，移除右键选中的游戏
                var game = _contextMenuGame;
                if (game != null)
                {
                    Games.Remove(game);
                    SaveGamesNow();
                }
            }
        }

        // 一键清除游戏列表
        private async void ClearAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Games.Count == 0)
            {
                ShowMessage(Translator.IsEnglish ? "Game list is already empty" : "游戏列表已经是空的");
                return;
            }

            var confirmDialog = new ContentDialog
            {
                Title = Translator.IsEnglish ? "Clear Game List" : "清除游戏列表",
                Content = Translator.IsEnglish ? 
                    $"Are you sure you want to clear all {Games.Count} games from the list? This action cannot be undone." :
                    $"确定要清除列表中的所有 {Games.Count} 个游戏吗？此操作不可撤销。",
                PrimaryButtonText = Translator.IsEnglish ? "Clear All" : "全部清除",
                CloseButtonText = Translator.IsEnglish ? "Cancel" : "取消",
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = SettingsService.Instance.DarkMode ? ElementTheme.Dark : ElementTheme.Light
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Games.Clear();
                SaveGamesNow();
                ShowMessage(Translator.IsEnglish ? "Game list cleared" : "游戏列表已清除");
            }
        }

        // ===== 框选功能 =====

        private void GameListView_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(GameListView);

            // ===== 右键按下：记录当前选中状态，防止右键点击取消多选 =====
            if (pointerPoint.Properties.IsRightButtonPressed)
            {
                _isRightClicking = true;
                // 记录右键点击前所有选中的游戏
                _rightClickSelectedGames = GameListView.SelectedItems.Cast<GameInfo>().ToList();
                return;
            }

            // ===== 左键按下：检查是否在空白区域，开始框选 =====
            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                // 检查点击位置是否在某个ListViewItem上
                var hitTest = VisualTreeHelper.FindElementsInHostCoordinates(pointerPoint.Position, GameListView);
                bool isOnItem = false;
                foreach (var elem in hitTest)
                {
                    if (elem is ListViewItem)
                    {
                        isOnItem = true;
                        break;
                    }
                    // 向上查找父元素，看是否在ListViewItem内
                    var parent = elem as DependencyObject;
                    while (parent != null)
                    {
                        if (parent is ListViewItem)
                        {
                            isOnItem = true;
                            break;
                        }
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    if (isOnItem) break;
                }

                // 检查Ctrl键是否按下
                bool isCtrlPressed = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

                // 如果在空白区域按下，开始框选
                if (!isOnItem)
                {
                    _isDragSelecting = true;
                    _dragStartPoint = pointerPoint.Position;
                    // 记录之前选中的游戏（按住Ctrl时保留，否则清空选择）
                    _preSelectedGames = GameListView.SelectedItems.Cast<GameInfo>().ToList();
                    if (!isCtrlPressed)
                    {
                        GameListView.SelectedItems.Clear();
                    }
                    // 捕获指针，确保即使鼠标移出控件也能收到PointerReleased
                    try { GameListView.CapturePointer(e.Pointer); } catch { }
                }
            }
        }

        private void GameListView_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragSelecting) return;

            var pointerPoint = e.GetCurrentPoint(GameListView);
            var currentPoint = pointerPoint.Position;

            // 计算框选矩形
            double left = Math.Min(_dragStartPoint.X, currentPoint.X);
            double top = Math.Min(_dragStartPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - _dragStartPoint.X);
            double height = Math.Abs(currentPoint.Y - _dragStartPoint.Y);
            var selectionRect = new Windows.Foundation.Rect(left, top, width, height);

            // 遍历所有游戏，检查是否在框选矩形内
            foreach (var game in Games)
            {
                var container = GameListView.ContainerFromItem(game) as ListViewItem;
                if (container != null)
                {
                    // 获取item相对于GameListView的位置
                    var transform = container.TransformToVisual(GameListView);
                    var itemPosition = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    var itemRect = new Windows.Foundation.Rect(itemPosition.X, itemPosition.Y, container.ActualWidth, container.ActualHeight);

                    // 检查是否相交（手动实现矩形相交检查）
                    bool intersects = !(itemRect.Left > selectionRect.Right ||
                                       itemRect.Right < selectionRect.Left ||
                                       itemRect.Top > selectionRect.Bottom ||
                                       itemRect.Bottom < selectionRect.Top);

                    if (intersects)
                    {
                        if (!GameListView.SelectedItems.Contains(game))
                        {
                            GameListView.SelectedItems.Add(game);
                        }
                    }
                    else
                    {
                        // 如果不是之前选中的（按住Ctrl的情况），就取消选中
                        if (!_preSelectedGames.Contains(game))
                        {
                            GameListView.SelectedItems.Remove(game);
                        }
                    }
                }
            }
        }

        private void GameListView_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // 结束框选
            if (_isDragSelecting)
            {
                _isDragSelecting = false;
                _preSelectedGames.Clear();
                try { GameListView.ReleasePointerCapture(e.Pointer); } catch { }
            }
            // 结束右键状态
            if (_isRightClicking)
            {
                _isRightClicking = false;
            }
        }

        private void GameListView_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            // 指针捕获丢失时，确保框选状态正确结束
            _isDragSelecting = false;
            _preSelectedGames.Clear();
            _isRightClicking = false;
        }
    }
}
