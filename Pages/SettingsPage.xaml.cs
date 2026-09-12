using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;

namespace DLSSFrameGenEnabler_WinUI3.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isLoaded = false;
        private bool _isDisposed = false;

        public SettingsPage()
        {
            this.InitializeComponent();
            DarkModeToggle.IsOn = SettingsService.Instance.DarkMode;
            AnimationToggle.IsOn = SettingsService.Instance.EnableAnimation;
            StartupDialogToggle.IsOn = SettingsService.Instance.ShowStartupDialog;
            RememberGamesToggle.IsOn = SettingsService.Instance.RememberGames;
            LanguageCombo.SelectedIndex = SettingsService.Instance.Language;
            // 根据BackdropType找到对应的ComboBoxItem（因为删除了高斯模糊选项，不能直接用索引）
            var backdropType = SettingsService.Instance.BackdropType;
            // 如果是旧的高斯模糊类型(3)，重置为默认(0)
            if (backdropType == 3) backdropType = 0;
            foreach (ComboBoxItem item in BackdropCombo.Items)
            {
                if (item.Tag is string tag && int.TryParse(tag, out var t) && t == backdropType)
                {
                    BackdropCombo.SelectedItem = item;
                    break;
                }
            }
            // 根据当前材质类型显示/隐藏自定义壁纸按钮
            WallpaperButtons.Visibility = backdropType == 4 ? Visibility.Visible : Visibility.Collapsed;
            TransparentTipText.Visibility = backdropType == 5 ? Visibility.Visible : Visibility.Collapsed;
            UpdateLanguageTexts();

            // 后台加载显卡列表，避免UI线程卡死
            _ = LoadGpuListAsync();
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
            GpuTipText.Text = Translator.IsEnglish ? "If auto-detection fails, manually select GPU" : "如果自动检测出错，可以手动选择显卡";
            GpuComboBox.Header = Translator.T("Settings_GpuHeader");
            AboutText.Text = Translator.T("Settings_About");
            VersionText.Text = Translator.IsEnglish ? "Version: V1.12.5.2" : "版本：V1.12.5.2";
            DevLangText.Text = Translator.T("About_DevLang");

            // 更新语言下拉框的选项文本
            if (LanguageCombo.Items.Count >= 3)
            {
                ((ComboBoxItem)LanguageCombo.Items[0]).Content = Translator.T("Settings_Lang_Auto");
                ((ComboBoxItem)LanguageCombo.Items[1]).Content = Translator.T("Settings_Lang_Chinese");
                ((ComboBoxItem)LanguageCombo.Items[2]).Content = Translator.T("Settings_Lang_English");
            }

            // 更新材质下拉框的选项文本
            if (BackdropCombo.Items.Count >= 5)
            {
                ((ComboBoxItem)BackdropCombo.Items[0]).Content = Translator.T("Settings_Backdrop_Default");
                ((ComboBoxItem)BackdropCombo.Items[1]).Content = Translator.T("Settings_Backdrop_Mica");
                ((ComboBoxItem)BackdropCombo.Items[2]).Content = Translator.T("Settings_Backdrop_Acrylic");
                ((ComboBoxItem)BackdropCombo.Items[3]).Content = Translator.T("Settings_Backdrop_TransparentBlur");
                ((ComboBoxItem)BackdropCombo.Items[4]).Content = Translator.T("Settings_Backdrop_Custom");
            }

            // 更新自定义壁纸按钮文本
            SelectWallpaperBtn.Content = Translator.T("Settings_SelectWallpaper");
            ResetWallpaperBtn.Content = Translator.T("Settings_ResetWallpaper");

            // 更新显卡列表的"自动检测"文本（不重新查询显卡）
            if (GpuComboBox.Items.Count > 0)
            {
                GpuComboBox.Items[0] = Translator.T("Settings_AutoDetect");
            }
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out var lang))
            {
                SettingsService.Instance.Language = lang;
                SettingsService.Instance.Save();
                Translator.CurrentLanguage = (AppLanguage)lang;
            }
        }

        private async System.Threading.Tasks.Task LoadGpuListAsync()
        {
            // 先显示"加载中"
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isDisposed) return;
                GpuComboBox.Items.Clear();
                GpuComboBox.Items.Add(Translator.T("Settings_AutoDetect"));
                GpuComboBox.IsEnabled = false;
            });

            // 后台线程查询显卡（WMI查询耗时，不能在UI线程执行）
            var gpus = await System.Threading.Tasks.Task.Run(() =>
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

            if (_isDisposed) return;

            // 回到UI线程更新列表
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isDisposed) return;
                try
                {
                    GpuComboBox.Items.Clear();
                    GpuComboBox.Items.Add(Translator.T("Settings_AutoDetect"));
                    foreach (var gpu in gpus)
                    {
                        GpuComboBox.Items.Add(gpu.Name);
                    }

                    // 选中当前设置
                    var selected = SettingsService.Instance.SelectedGpuName;
                    if (string.IsNullOrEmpty(selected))
                    {
                        GpuComboBox.SelectedIndex = 0;
                    }
                    else
                    {
                        GpuComboBox.SelectedItem = selected;
                        if (GpuComboBox.SelectedItem == null) GpuComboBox.SelectedIndex = 0;
                    }
                    GpuComboBox.IsEnabled = true;
                }
                catch { }
            });
        }

        private void GpuComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
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

        private void DarkModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsService.Instance.DarkMode = DarkModeToggle.IsOn;
            SettingsService.Instance.Save();
            // 使用深浅色切换动画
            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.AnimateThemeChange(DarkModeToggle.IsOn);
            }
            else
            {
                App.ApplyTheme(DarkModeToggle.IsOn ? ElementTheme.Dark : ElementTheme.Light);
            }
        }

        private void AnimationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsService.Instance.EnableAnimation = AnimationToggle.IsOn;
            SettingsService.Instance.Save();
            App.ApplyAnimation(AnimationToggle.IsOn);
        }

        private void StartupDialogToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsService.Instance.ShowStartupDialog = StartupDialogToggle.IsOn;
            SettingsService.Instance.Save();
        }

        private void RememberGamesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsService.Instance.RememberGames = RememberGamesToggle.IsOn;
            SettingsService.Instance.Save();
            if (RememberGamesToggle.IsOn)
            {
                // 开启时，立即保存当前游戏列表
                if (App.MainWindow is MainWindow mw && mw.NavFramePublic.Content is HomePage homePage)
                {
                    SettingsService.SaveGames(homePage.Games.ToList());
                }
            }
            else
            {
                // 关闭时，清除已保存的游戏列表
                SettingsService.SaveGames(new System.Collections.Generic.List<Models.GameInfo>());
            }
        }

        private void BackdropCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (BackdropCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out var type))
            {
                SettingsService.Instance.BackdropType = type;
                SettingsService.Instance.Save();

                // 显示/隐藏自定义壁纸按钮
                WallpaperButtons.Visibility = type == 4 ? Visibility.Visible : Visibility.Collapsed;
                // 显示/隐藏纯透明提示
                TransparentTipText.Visibility = type == 5 ? Visibility.Visible : Visibility.Collapsed;

                // 应用到主窗口
                if (App.MainWindow is MainWindow mw)
                {
                    mw.ApplyBackdrop(type);
                }
            }
        }

        private async void SelectWallpaperBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".bmp");
                picker.FileTypeFilter.Add(".webp");

                // 获取窗口句柄
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    SettingsService.Instance.CustomWallpaperPath = file.Path;
                    SettingsService.Instance.Save();

                    // 应用自定义壁纸
                    if (App.MainWindow is MainWindow mw)
                    {
                        mw.ApplyCustomWallpaper(file.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择壁纸失败: {ex.Message}");
            }
        }

        private void ResetWallpaperBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.Instance.CustomWallpaperPath = "";
            SettingsService.Instance.Save();

            // 恢复默认材质
            if (App.MainWindow is MainWindow mw)
            {
                mw.ApplyBackdrop(0);
            }
            BackdropCombo.SelectedIndex = 0;
        }
    }
}
