using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DLSSFrameGenEnabler_WinUI3.Models;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WebUI.Beta;

/// <summary>
/// WebUI 测试版宿主：无边框窗口 + WebView2 铺满，界面全部由 HTML/CSS/JS 绘制。
/// 列表数据单独存在 games_webui_beta.json，首次从正式列表复制，不写正式 games.json。
/// </summary>
public class MainForm : Form
{
    private readonly WebView2 _web = new WebView2();
    private readonly Dictionary<string, string> _iconCache = new Dictionary<string, string>();
    private readonly List<GameInfo> _games = new List<GameInfo>();
    private readonly Dictionary<string, TaskCompletionSource<string?>> _dialogs =
        new Dictionary<string, TaskCompletionSource<string?>>();
    private readonly bool _snapMode;
    private readonly bool _scanMode;
    private readonly bool _demoDialogMode;
    private readonly bool _demoSm86Mode;
    private readonly string _demoPage;
    private readonly bool _demoDetectMode;
    private readonly bool _demoDarkMode;
    private readonly bool _demoEnglishMode;
    private readonly bool _demoScrollEnd;
    private readonly bool _demoStartupMode;
    private readonly bool _demoOnMode;
    private readonly bool _demoSelectMode;
    private readonly bool _demoBackupMode;
    private readonly bool _demoCheckMode;
    private readonly bool _demoBetaMode;
    private readonly bool _demoBigMode;
    private bool _busy;
    private int _dialogSeq;
    private DownloadJob? _downloadJob;
    private readonly System.Windows.Forms.Timer _windowAnimTimer = new System.Windows.Forms.Timer();
    private DateTime _windowAnimStart;
    private Rectangle _windowAnimFrom;
    private Rectangle _windowAnimTo;
    private bool _windowAnimToMax;
    private bool _windowAnimating;
    private bool _windowMaximized;
    private Rectangle _windowRestoreBounds;

    private static readonly string OfficialGamesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DLSSFrameGenEnabler", "games.json");

    private static readonly string BetaGamesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DLSSFrameGenEnabler", "games_webui_beta.json");

    private readonly SettingsService _settings = SettingsService.Instance;

    public MainForm(bool snapMode = false, bool scanMode = false, bool demoDialog = false, bool demoSm86 = false, string demoPage = "", bool demoDetect = false, bool demoDark = false, bool demoEnglish = false, bool demoScrollEnd = false, bool demoStartup = false, bool demoOn = false, bool demoSelect = false, bool demoBackup = false, bool demoCheck = false, bool demoBeta = false, bool demoBig = false)
    {
        WriteStartupLog("MainForm 构造函数开始");
        _snapMode = snapMode;
        _scanMode = scanMode;
        _demoDialogMode = demoDialog;
        _demoSm86Mode = demoSm86;
        _demoPage = demoPage;
        _demoDetectMode = demoDetect;
        _demoDarkMode = demoDark;
        _demoEnglishMode = demoEnglish;
        _demoScrollEnd = demoScrollEnd;
        _demoStartupMode = demoStartup;
        _demoOnMode = demoOn;
        _demoSelectMode = demoSelect;
        _demoBackupMode = demoBackup;
        _demoCheckMode = demoCheck;
        _demoBetaMode = demoBeta;
        _demoBigMode = demoBig;
        Translator.CurrentLanguage = (AppLanguage)_settings.Language;
        if (_demoEnglishMode)
        {
            Translator.CurrentLanguage = AppLanguage.English;
        }
        Text = Translator.IsEnglish
            ? "Frame Gen + DLSS5 Enabler"
            : "多帧生成+DLSS5开启工具";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        // 默认窗口不能超过当前屏幕工作区，避免 1366×768 等小屏上底部被屏幕外裁掉。
        Rectangle workingArea = Screen.PrimaryScreen != null
            ? Screen.PrimaryScreen.WorkingArea
            : SystemInformation.WorkingArea;
        int initialWidth = Math.Min(1285, Math.Max(820, workingArea.Width - 40));
        int initialHeight = Math.Min(844, Math.Max(560, workingArea.Height - 60));
        ClientSize = new Size(initialWidth, initialHeight);
        MinimumSize = new Size(820, 560);
        try
        {
            // 与正式版一致：标题栏和任务栏都使用 app.ico，而不是 .NET 默认图标。
            string iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            Icon = File.Exists(iconPath)
                ? new Icon(iconPath)
                : Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }
        BackColor = _settings.DarkMode ? Color.FromArgb(0x1B, 0x1B, 0x19) : Color.FromArgb(0xF5, 0xF5, 0xF2);
        // 给 WebView2 留出 6px 边框：WebView2 是独立子窗口，会盖住客户区并吞掉
        // WM_NCHITTEST；留边后鼠标落在窗体自身，才能命中四边缩放。
        Padding = new Padding(6);
        MaximizedBounds = Screen.FromControl(this).WorkingArea;

        _web.Dock = DockStyle.Fill;
        _web.DefaultBackgroundColor = BackColor;
        Controls.Add(_web);

        _windowAnimTimer.Interval = 15;
        _windowAnimTimer.Tick += WindowAnimTimer_Tick;

        LoadGames();
        Load += OnFormLoad;
        WriteStartupLog("MainForm 构造函数完成");
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            // WinForms 的 MinimumSize 使用物理像素；按当前 DPI 换算，
            // 保证 125%/150% 缩放下实际仍保留 820x560 的逻辑窗口尺寸。
            int dpi = DeviceDpi;
            MinimumSize = new Size(
                (int)Math.Round(820 * dpi / 96.0),
                (int)Math.Round(560 * dpi / 96.0));
            int pref = 2;
            _ = DwmSetWindowAttribute(Handle, 33, ref pref, sizeof(int));
            int dark = _settings.DarkMode ? 1 : 0;
            _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        }
        catch
        {
        }
    }

    private const int WM_NCHITTEST = 0x0084;
    private const int WM_SIZING = 0x0214;
    private const int WMSZ_LEFT = 1;
    private const int WMSZ_RIGHT = 2;
    private const int WMSZ_TOP = 3;
    private const int WMSZ_TOPLEFT = 4;
    private const int WMSZ_TOPRIGHT = 5;
    private const int WMSZ_BOTTOM = 6;
    private const int WMSZ_BOTTOMLEFT = 7;
    private const int WMSZ_BOTTOMRIGHT = 8;
    private const int HTLEFT = 0x000A;
    private const int HTRIGHT = 0x000B;
    private const int HTTOP = 0x000C;
    private const int HTTOPLEFT = 0x000D;
    private const int HTTOPRIGHT = 0x000E;
    private const int HTBOTTOM = 0x000F;
    private const int HTBOTTOMLEFT = 0x0010;
    private const int HTBOTTOMRIGHT = 0x0011;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    protected override void WndProc(ref Message m)
    {
        // Windows 的原生边缘拖拽不会完全按 WinForms 的 MinimumSize 处理，
        // 这里在 WM_SIZING 阶段直接修正候选矩形，保证真实拖拽也不会小于最小尺寸。
        if (m.Msg == WM_SIZING
            && !_windowAnimating
            && !_windowMaximized
            && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            if (m.LParam != IntPtr.Zero)
            {
                RECT rect = Marshal.PtrToStructure<RECT>(m.LParam);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                int minWidth = MinimumSize.Width;
                int minHeight = MinimumSize.Height;
                int sizingEdge = m.WParam.ToInt32();

                if (width < minWidth && minWidth > 0)
                {
                    if (sizingEdge == WMSZ_LEFT || sizingEdge == WMSZ_TOPLEFT || sizingEdge == WMSZ_BOTTOMLEFT)
                    {
                        rect.Left = rect.Right - minWidth;
                    }
                    else
                    {
                        rect.Right = rect.Left + minWidth;
                    }
                }
                if (height < minHeight && minHeight > 0)
                {
                    if (sizingEdge == WMSZ_TOP || sizingEdge == WMSZ_TOPLEFT || sizingEdge == WMSZ_TOPRIGHT)
                    {
                        rect.Top = rect.Bottom - minHeight;
                    }
                    else
                    {
                        rect.Bottom = rect.Top + minHeight;
                    }
                }

                Marshal.StructureToPtr(rect, m.LParam, false);
            }
            return;
        }

        // 无边框窗口默认不能拖边缩放；在客户区四周返回系统非客户区命中值，
        // 让 Windows 自己完成拖拽缩放、尺寸约束和鼠标指针变化。
        if (m.Msg == WM_NCHITTEST
            && !_windowAnimating
            && !_windowMaximized
            && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            if (m.Result == (IntPtr)1)
            {
                long lParam = m.LParam.ToInt64();
                int screenX = unchecked((short)(lParam & 0xFFFF));
                int screenY = unchecked((short)((lParam >> 16) & 0xFFFF));
                Point point = PointToClient(new Point(screenX, screenY));
                int width = ClientSize.Width;
                int height = ClientSize.Height;
                const int edge = 6;
                const int corner = 10;

                bool left = point.X >= 0 && point.X < edge;
                bool right = point.X <= width - 1 && point.X >= width - edge;
                bool top = point.Y >= 0 && point.Y < edge;
                bool bottom = point.Y <= height - 1 && point.Y >= height - edge;
                bool cornerLeft = point.X >= 0 && point.X < corner;
                bool cornerRight = point.X <= width - 1 && point.X >= width - corner;
                bool cornerTop = point.Y >= 0 && point.Y < corner;
                bool cornerBottom = point.Y <= height - 1 && point.Y >= height - corner;

                if (cornerTop && cornerLeft)
                {
                    m.Result = (IntPtr)HTTOPLEFT;
                }
                else if (cornerTop && cornerRight)
                {
                    m.Result = (IntPtr)HTTOPRIGHT;
                }
                else if (cornerBottom && cornerLeft)
                {
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                }
                else if (cornerBottom && cornerRight)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                }
                else if (left)
                {
                    m.Result = (IntPtr)HTLEFT;
                }
                else if (right)
                {
                    m.Result = (IntPtr)HTRIGHT;
                }
                else if (top)
                {
                    m.Result = (IntPtr)HTTOP;
                }
                else if (bottom)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_windowAnimating || _windowMaximized || WindowState != FormWindowState.Normal)
        {
            return;
        }

        int minWidth = MinimumSize.Width;
        int minHeight = MinimumSize.Height;
        if ((minWidth > 0 && Width < minWidth) || (minHeight > 0 && Height < minHeight))
        {
            Size = new Size(
                Math.Max(Width, minWidth),
                Math.Max(Height, minHeight));
        }
    }

    private async void OnFormLoad(object? sender, EventArgs e)
    {
        try
        {
            WriteStartupLog("OnFormLoad 开始");
            ApplyNativeWindowTransitions();
            // WebView2 默认会把用户数据放在 exe 同级的 .WebView2 目录；如果软件放在
            // Program Files 等只读目录，初始化会失败。固定放到当前用户的 LocalAppData。
            string webViewDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DLSSFrameGenEnabler",
                "WebView2");
            Directory.CreateDirectory(webViewDataFolder);
            CoreWebView2Environment webViewEnvironment =
                await CoreWebView2Environment.CreateAsync(null, webViewDataFolder, null);
            await _web.EnsureCoreWebView2Async(webViewEnvironment);
            CoreWebView2 core = _web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            string root = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            core.SetVirtualHostNameToFolderMapping("app.local", root, CoreWebView2HostResourceAccessKind.Allow);
            core.WebMessageReceived += OnWebMessage;
            core.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    return;
                }
                PushGames();
                if (_demoDialogMode)
                {
                    _ = DemoDialogAsync();
                }
                if (_demoSm86Mode)
                {
                    _ = DemoSm86Async();
                }
                if (_demoPage.Length > 0)
                {
                    _ = SwitchDemoPageAsync(_demoPage);
                }
                if (_demoDetectMode)
                {
                    _ = DemoDetectAsync();
                }
                if (_demoDarkMode)
                {
                    _ = core.ExecuteScriptAsync("window.__beta && window.__beta.theme(true, 2)");
                }
                if (_demoEnglishMode)
                {
                    _ = core.ExecuteScriptAsync("window.__beta && window.__beta.lang(true)");
                }
                if (_demoStartupMode)
                {
                    _ = ShowStartupNoticeAsync();
                }
                if (_demoBetaMode)
                {
                    _ = ShowBetaFeedbackAsync();
                }
                if (_demoBigMode)
                {
                    _ = core.ExecuteScriptAsync("window.__beta && window.__beta.big(true)");
                }
                if (_demoOnMode)
                {
                    _ = core.ExecuteScriptAsync("window.__beta && window.__beta.demoOn()");
                }
                if (_demoSelectMode)
                {
                    _ = core.ExecuteScriptAsync("window.__beta && window.__beta.demoSelect()");
                }
                if (_demoBackupMode)
                {
                    _ = core.ExecuteScriptAsync("window.__beta && window.__beta.demoBackup()");
                }
                if (_demoCheckMode)
                {
                    _ = core.ExecuteScriptAsync("document.getElementById('btnCheckUpdate') && document.getElementById('btnCheckUpdate').click()");
                }
                if (_snapMode)
                {
                    _ = (_scanMode ? ScanThenSnapAsync() : SnapAsync());
                }
                else
                {
                    if ((_settings.ShowStartupDialog || (DLSSFrameGenEnabler_WinUI3.App.IsBeta && _settings.ShowBetaFeedback)) && !_demoDialogMode && !_demoSm86Mode && !_demoDetectMode && !_demoStartupMode && !_demoBetaMode && _demoPage.Length == 0)
                    {
                        _ = ShowStartupDialogsAsync();
                    }
                    if (_settings.AutoCheckUpdate)
                    {
                        _ = CheckUpdateAsync();
                    }
                }
            };
            core.Navigate("https://app.local/index.html");
            WriteStartupLog("WebView2 初始化完成，开始导航");
        }
        catch (Exception ex)
        {
            WriteLog("OnFormLoad FAILED: " + ex.Message);
            WriteStartupLog("OnFormLoad FAILED: " + ex.Message);
            MessageBox.Show(
                "WebView2 初始化失败：\r\n" + ex.Message + "\r\n\r\n请确认已安装 Microsoft Edge WebView2 Runtime。\r\n\r\nWebView2 initialization failed: " + ex.Message + "\r\nMake sure Microsoft Edge WebView2 Runtime is installed.",
                "启动失败 / Startup Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // ---------------- 数据 ----------------

    private void LoadGames()
    {
        try
        {
            string source = File.Exists(BetaGamesPath) ? BetaGamesPath : OfficialGamesPath;
            if (File.Exists(source))
            {
                List<GameInfo>? list = JsonSerializer.Deserialize<List<GameInfo>>(File.ReadAllText(source));
                if (list != null)
                {
                    int originalCount = list.Count;
                    list = list.Where(g => !IsObsoleteGameEntry(g)).ToList();

                    // 旧列表里存在被重复转义成双反斜杠的路径；按归一化路径去重，
                    // 避免修正 8-Zip 后同一款 PBZ 出现两条记录。
                    var unique = new List<GameInfo>();
                    foreach (GameInfo g in list)
                    {
                        string gp = Norm(g.GamePath);
                        string ep = Norm(g.ExePath);
                        bool exists = unique.Any(x =>
                            (gp.Length > 0 && Norm(x.GamePath) == gp) ||
                            (ep.Length > 0 && Norm(x.ExePath) == ep));
                        if (!exists)
                        {
                            unique.Add(g);
                        }
                    }
                    _games.AddRange(unique);
                    if (unique.Count != originalCount)
                    {
                        SaveGames();
                    }
                }
            }
            SettingsService.WriteSaveDebug($"加载游戏列表，数量：{_games.Count}");
        }
        catch (Exception ex)
        {
            WriteLog("LoadGames FAILED: " + ex.Message);
            SettingsService.WriteSaveDebug("加载游戏列表失败：" + ex.Message + "\n" + ex.StackTrace);
        }
    }

    // 旧扫描把 PBZ 目录中的 8-Zip 控制台误识别成游戏，启动时清理历史条目
    private static bool IsObsoleteGameEntry(GameInfo game)
    {
        string name = (game.Name ?? "").Trim();
        string exe = Path.GetFileName(game.ExePath ?? "").ToLowerInvariant();
        return exe == "8za_v2.exe"
            || exe == "8za.exe"
            || name.Equals("8-Zip", StringComparison.OrdinalIgnoreCase)
            || name.Equals("8Zip", StringComparison.OrdinalIgnoreCase)
            || name.Equals("8 Zip", StringComparison.OrdinalIgnoreCase);
    }

    private void SaveGames()
    {
        try
        {
            string? dir = Path.GetDirectoryName(BetaGamesPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (!_settings.RememberGames)
            {
                // 不记住列表：清掉已保存数据，下次启动为空
                File.WriteAllText(BetaGamesPath, "[]");
                SettingsService.WriteSaveDebug("清空保存的游戏列表");
                return;
            }
            File.WriteAllText(BetaGamesPath, JsonSerializer.Serialize(_games));
            SettingsService.WriteSaveDebug($"保存游戏列表，数量：{_games.Count}");
        }
        catch (Exception ex)
        {
            WriteLog("SaveGames FAILED: " + ex.Message);
            SettingsService.WriteSaveDebug("保存游戏列表失败：" + ex.Message + "\n" + ex.StackTrace);
        }
    }

    private static string Norm(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        string original = path.Trim();
        string p = original;
        // 兼容旧数据里被重复转义的反斜杠（D:\\SteamLibrary）
        while (p.Contains("\\\\"))
        {
            p = p.Replace("\\\\", "\\");
        }
        // 保留 UNC 路径开头的 \\
        if (original.StartsWith("\\\\") && !p.StartsWith("\\\\"))
        {
            p = "\\" + p;
        }
        return p.TrimEnd('\\', '/').ToLowerInvariant();
    }

    private bool IsDuplicate(GameInfo game)
    {
        string gp = Norm(game.GamePath);
        string ep = Norm(game.ExePath);
        foreach (GameInfo x in _games)
        {
            if (Norm(x.GamePath) == gp && gp.Length > 0)
            {
                return true;
            }
            if (ep.Length > 0 && Norm(x.ExePath) == ep)
            {
                return true;
            }
        }
        return false;
    }

    // ---------------- 消息 ----------------

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonElement root;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
            root = doc.RootElement.Clone();
        }
        catch
        {
            return;
        }

        string type = root.TryGetProperty("type", out JsonElement t) ? (t.GetString() ?? "") : "";
        switch (type)
        {
            case "ready":
                PostAppInfo();
                PostSettings();
                PushGames();
                break;
            case "rescan":
                PushGames();
                break;
            case "scan":
                _ = RunScanAsync();
                break;
            case "detect":
                _ = DetectRunningAsync();
                break;
            case "openPath":
                string target = root.TryGetProperty("target", out JsonElement tp) ? (tp.GetString() ?? "") : "";
                OpenPath(target);
                break;
            case "refresh":
                _ = RefreshStatusAsync();
                break;
            case "addFolder":
                AddFolder();
                break;
            case "addExe":
                AddExe();
                break;
            case "exportLog":
                _ = ExportLogsAsync();
                break;
            case "configName":
                ApplyConfigName(root);
                break;
            case "troubleshootFiles":
                SendTroubleshootFiles(root);
                break;
            case "troubleshootRun":
                RunTroubleshoot(root);
                break;
            case "saveRemark":
                SaveRemark(root);
                break;
            case "clear":
                ClearGames();
                break;
            case "sm86save":
                SaveSm86(root);
                break;
            case "sm86reset":
                ResetSm86(root);
                break;
            case "action":
                string op = root.TryGetProperty("op", out JsonElement o) ? (o.GetString() ?? "") : "";
                string exe = root.TryGetProperty("exe", out JsonElement x) ? (x.GetString() ?? "") : "";
                _ = HandleActionAsync(op, exe);
                break;
            case "dialogResult":
                string id = root.TryGetProperty("id", out JsonElement i) ? (i.GetString() ?? "") : "";
                string? value = root.TryGetProperty("value", out JsonElement v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString()
                    : null;
                CompleteDialog(id, value);
                break;
            case "setSetting":
                SetSetting(root);
                break;
            case "wallpaperPick":
                PickWallpaper();
                break;
            case "wallpaperReset":
                ResetWallpaper();
                break;
            case "backupOpen":
                OpenBackup();
                break;
            case "backupDelete":
                DeleteBackups(root);
                break;
            case "openUrl":
                OpenUrl(root);
                break;
            case "checkUpdate":
                _ = CheckUpdateAsync(true);
                break;
            case "smoothRefresh":
                _ = RefreshSmoothMotionAsync();
                break;
            case "smoothApply":
                _ = ApplySmoothMotionAsync(root);
                break;
            case "smoothReset":
                _ = ResetSmoothMotionAsync(root);
                break;
            case "smoothDeployMod":
                _ = DeploySmoothMotionModAsync(root);
                break;
            case "smoothRestoreMod":
                _ = RestoreSmoothMotionModAsync(root);
                break;
            case "smoothOpenLog":
                OpenSmoothMotionLog();
                break;
            case "smoothOpenMod":
                OpenSmoothMotionModFolder();
                break;
            case "smoothOpenModLog":
                OpenSmoothMotionModLog();
                break;
            case "downloadLatest":
                _ = DownloadFromGitHubAsync(_settings.GithubUrl);
                break;
            case "downloadPause":
                PauseDownload();
                break;
            case "downloadResume":
                ResumeDownload();
                break;
            case "downloadCancel":
                _ = CancelDownloadAsync();
                break;
            case "downloadOpenFolder":
                OpenDownloadFolder();
                break;
            case "downloadClear":
                ClearDownloadRecord();
                break;
            case "drag":
                BeginDragMove();
                break;
            case "minimize":
                _ = SendMessage(Handle, 0x0112, (IntPtr)0xF020, IntPtr.Zero);
                break;
            case "maximize":
                ToggleMaximize();
                break;
            case "close":
                Close();
                break;
        }
    }

    // ---------------- 弹窗 ----------------

    private sealed class DialogOption
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
        public string Desc { get; set; } = "";
        public string Icon { get; set; } = "";
    }

    private sealed class DownloadJob
    {
        public string FileName { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string TargetPath { get; set; } = "";
        public string PartialPath { get; set; } = "";
        public string ReleaseVersion { get; set; } = "";
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public bool Paused { get; set; }
        public bool Cancelled { get; set; }
        public bool Completed { get; set; }
        public CancellationTokenSource? ReadCts { get; set; }
        public Task? Task { get; set; }
    }

    private Task<string?> ShowChoiceAsync(string title, string text, List<(string, string, string)> options)
    {
        List<DialogOption> list = new List<DialogOption>();
        foreach ((string value, string label, string desc) in options)
        {
            list.Add(new DialogOption { Value = value, Label = label, Desc = desc });
        }
        return ShowDialogAsync(title, text, list);
    }

    private Task<string?> ShowDialogAsync(string title, string text, List<DialogOption> options)
    {
        int seq = ++_dialogSeq;
        string dialogId = "d" + seq;
        TaskCompletionSource<string?> tcs = new TaskCompletionSource<string?>();
        _dialogs[dialogId] = tcs;

        List<object> optionsPayload = new List<object>();
        foreach (DialogOption option in options)
        {
            optionsPayload.Add(new { value = option.Value, label = option.Label, desc = option.Desc, icon = option.Icon });
        }

        PostJson(new { type = "dialog", id = dialogId, title, text, options = optionsPayload });
        return tcs.Task;
    }

    private void CompleteDialog(string id, string? value)
    {
        if (id.Length == 0)
        {
            return;
        }
        TaskCompletionSource<string?>? tcs = _dialogs.TryGetValue(id, out TaskCompletionSource<string?>? found)
            ? found
            : null;
        if (tcs == null)
        {
            return;
        }
        _dialogs.Remove(id);
        tcs.TrySetResult(value);
    }

    private void ShowProgress(string text)
    {
        PostJson(new { type = "progress", text });
    }

    private void HideProgress()
    {
        PostJson(new { type = "progressHide" });
    }

    private void PostJson(object payload)
    {
        try
        {
            _web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }
        catch
        {
        }
    }

    private async Task DemoDialogAsync()
    {
        await Task.Delay(900);
        await ShowChoiceAsync(
            Lang("安装多帧生成", "Install Frame Generation"),
            Lang("请选择安装模式", "Choose an installation mode"),
            new List<(string, string, string)>
            {
                ("classic", Lang("经典模式", "Classic mode"), Lang("替换 nvngx_dlssg.dll，兼容性最好，适合老驱动", "Replaces nvngx_dlssg.dll — best compatibility, good for older drivers")),
                ("advanced", Lang("高级模式（推荐）", "Advanced mode (recommended)"), Lang("安装后可在下方「配置多帧生成」里调整补丁方案", "After installation you can tune the patch scheme under \"Configure\""))
            });
    }

    // ---------------- 开关业务 ----------------

    private async Task HandleActionAsync(string op, string exe)
    {
        GameInfo? game = null;
        foreach (GameInfo g in _games)
        {
            if (Norm(g.ExePath) == Norm(exe))
            {
                game = g;
                break;
            }
        }
        if (game == null)
        {
            PostStatus("找不到这个游戏，请刷新状态后重试");
            return;
        }

        if (op == "launch")
        {
            LaunchGame(game);
            return;
        }
        if (op == "openFolder")
        {
            OpenGameFolder(game);
            return;
        }
        if (op == "remove")
        {
            RemoveGame(game);
            return;
        }

        if (_busy)
        {
            PostStatus("还有操作正在进行，请稍候再试");
            return;
        }

        // 与正式版 UpdateButtonStates 一致：不支持多帧生成且未开启时禁止安装
        if (op == "frame" && !game.SupportsFrameGen && !game.FrameGenEnabled)
        {
            PostStatus("这个游戏不支持多帧生成：目录里没有检测到 DLSS / 帧生成特征");
            return;
        }

        _busy = true;
        try
        {
            if (op == "frame")
            {
                await ToggleFrameGenAsync(game);
            }
            else if (op == "dlss5")
            {
                await ToggleDlss5Async(game);
            }
            else if (op == "restore")
            {
                await RestoreAllAsync(game);
            }
            else if (op == "sm86cfg")
            {
                await OpenSm86ConfigAsync(game);
            }
        }
        catch (Exception ex)
        {
            HideProgress();
            WriteLog("action " + op + " FAILED: " + ex);
            PostStatus("操作失败：" + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ToggleFrameGenAsync(GameInfo game)
    {
        if (game.FrameGenEnabled)
        {
            string? confirm = await ShowChoiceAsync(
                Lang("还原多帧生成", "Restore Frame Generation"),
                Lang("将删除本次安装的多帧生成补丁并恢复备份文件。确定继续吗？", "This will remove the installed frame generation patch and restore the backup files. Continue?"),
                new List<(string, string, string)>
                {
                    ("yes", Lang("确定还原", "Restore"), Lang("删除补丁文件，恢复原始文件", "Remove patch files and restore originals")),
                    ("no", Lang("取消", "Cancel"), Lang("保持现状", "Keep everything as is"))
                });
            if (confirm != "yes")
            {
                return;
            }

            ShowProgress(Lang("正在还原多帧生成…", "Restoring frame generation…"));
            bool restored = await Task.Run(() => FilePatcher.RestoreFrameGen(game));
            HideProgress();
            if (restored)
            {
                game.FrameGenEnabled = false;
                SaveGames();
                PushGames();
                PostSettings();
                PostStatus("多帧生成已还原");
            }
            else
            {
                PostStatus("还原失败，可能没有找到备份记录");
            }
            return;
        }

        string? mode = await ShowChoiceAsync(
            Lang("安装多帧生成", "Install Frame Generation"),
            Lang("请选择安装模式", "Choose an installation mode"),
            new List<(string, string, string)>
            {
                ("classic", Lang("经典模式", "Classic mode"), Lang("替换 nvngx_dlssg.dll，兼容性最好，适合老驱动", "Replaces nvngx_dlssg.dll — best compatibility, good for older drivers")),
                ("advanced", Lang("高级模式（推荐）", "Advanced mode (recommended)"), Lang("可选 MFG 方案或 SM86 方案", "Choose either the MFG scheme or the SM86 scheme"))
            });
        if (mode == null)
        {
            return;
        }

        if (mode == "classic")
        {
            ShowProgress(Lang("正在安装经典多帧生成…", "Installing classic frame generation…"));
            bool ok = await Task.Run(() => FilePatcher.InstallClassicFrameGen(game));
            HideProgress();
            if (ok)
            {
                game.FrameGenEnabled = true;
                SaveGames();
                PushGames();
                PostSettings();
                PostStatus("经典多帧生成安装完成");
            }
            else
            {
                PostStatus("安装失败，请检查游戏目录权限");
            }
            return;
        }

        string? method = await ShowChoiceAsync(
            Lang("选择高级多帧生成方案", "Choose an advanced frame generation scheme"),
            Lang("MFG 方案与 SM86 方案只能二选一开启", "MFG and SM86 can only be enabled one at a time"),
            new List<(string, string, string)>
            {
                ("mfg", Lang("MFG 方案", "MFG scheme"), Lang("画质与帧率比较均衡，会替换游戏原文件", "Balanced image quality and frame rate; replaces original game files")),
                ("sm86", Lang("SM86 方案（推荐）", "SM86 scheme (recommended)"), Lang("不替换游戏原文件，附带内核优化，可自由搭配 DLSS 超分 + XeSS 补帧", "Does not replace original files, includes kernel optimizations, and can pair DLSS upscaling with XeSS frame generation"))
            });
        if (method == null)
        {
            return;
        }

        if (method == "mfg")
        {
            if (game.Dlss5Enabled && game.GitcVersion == "2.0.0")
            {
                string? warn = await ShowChoiceAsync(
                    Lang("兼容性提醒", "Compatibility warning"),
                    Lang("当前游戏已开启 GITC DLSS5 2.0.0。MFG 方案与 2.0.0 同时开启会导致游戏崩溃，建议先还原 DLSS5 或改用 SM86 方案。仍要继续安装 MFG 吗？",
                        "GITC DLSS5 2.0.0 is currently enabled for this game. Enabling MFG and 2.0.0 together will crash the game. Restore DLSS5 first or switch to the SM86 scheme. Continue installing MFG anyway?"),
                    new List<(string, string, string)>
                    {
                        ("go", Lang("仍要继续", "Continue anyway"), Lang("自行承担崩溃风险", "You accept the crash risk")),
                        ("no", Lang("取消安装", "Cancel"), Lang("返回列表", "Return to the list"))
                    });
                if (warn != "go")
                {
                    return;
                }
            }

            ShowProgress(Lang("正在安装 MFG 多帧生成…", "Installing MFG frame generation…"));
            bool ok = await Task.Run(() => FilePatcher.InstallAdvancedFrameGen(game));
            HideProgress();
            if (ok)
            {
                game.FrameGenEnabled = true;
                SaveGames();
                PushGames();
                PostSettings();
                PostStatus("MFG 多帧生成安装完成");
            }
            else
            {
                PostStatus("安装失败，请检查游戏目录权限");
            }
        }
        else if (method == "sm86")
        {
            ShowProgress(Lang("正在安装 SM86 多帧生成…", "Installing SM86 frame generation…"));
            bool ok = await Task.Run(() => FilePatcher.InstallAdvancedSM86FrameGen(game));
            HideProgress();
            if (ok)
            {
                game.FrameGenEnabled = true;
                SaveGames();
                PushGames();
                PostSettings();
                PostStatus("SM86 多帧生成安装完成，可在下方「配置多帧生成」调整参数");
            }
            else
            {
                PostStatus("安装失败，请检查游戏目录权限");
            }
        }
    }

    private async Task ToggleDlss5Async(GameInfo game)
    {
        if (game.Dlss5Enabled)
        {
            string? confirm = await ShowChoiceAsync(
                Lang("还原 DLSS5", "Restore DLSS5"),
                Lang("将删除 GITC DLSS5 相关文件并恢复备份。确定继续吗？", "This will remove GITC DLSS5 files and restore the backups. Continue?"),
                new List<(string, string, string)>
                {
                    ("yes", Lang("确定还原", "Restore"), Lang("删除 GITC 文件并恢复原文件", "Remove GITC files and restore originals")),
                    ("no", Lang("取消", "Cancel"), Lang("保持现状", "Keep everything as is"))
                });
            if (confirm != "yes")
            {
                return;
            }

            ShowProgress(Lang("正在还原 GITC DLSS5…", "Restoring GITC DLSS5…"));
            bool restored = await Task.Run(() => FilePatcher.RestoreGITCDLSS5(game));
            HideProgress();
            if (restored)
            {
                game.Dlss5Enabled = false;
                game.GitcVersion = "1.7.0";
                SaveGames();
                PushGames();
                PostSettings();
                PostStatus("DLSS5 已还原");
            }
            else
            {
                PostStatus("还原失败，可能没有找到备份记录");
            }
            return;
        }

        GpuInfo gpu = GpuDetector.DetectPrimaryGpu();
        if (!gpu.IsNvidia)
        {
            await ShowChoiceAsync(
                Lang("显卡提示", "GPU notice"),
                Lang("GITC DLSS5 仅支持 NVIDIA RTX 系列显卡。" + (gpu.IsAmd ? "\n\nAMD 显卡请打开软件目录下的 Patches\\AMD方案 文件夹，用 NeuralHub 独立工具开启。" : ""),
                    "GITC DLSS5 only supports NVIDIA RTX graphics cards." + (gpu.IsAmd ? "\n\nFor AMD GPUs, open the Patches\\AMD方案 folder in the app directory and use the standalone NeuralHub tool." : "")),
                new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
            return;
        }

        string? version = await ShowChoiceAsync(
            Lang("选择 GITC DLSS5 版本", "Choose a GITC DLSS5 version"),
            Lang("两个版本安装方式相同，可随时还原后换版本", "Both versions install the same way; you can restore and switch at any time"),
            new List<(string, string, string)>
            {
                ("1.7.0", Lang("1.7.0（兼容性最广）", "1.7.0 (widest compatibility)"), Lang("与 MFG 1.7.0 / 1.7.1 都能同时开启，但缺少新版本功能", "Works alongside MFG 1.7.0 / 1.7.1, but lacks newer features")),
                ("2.0.0", Lang("2.0.0（功能更多）", "2.0.0 (more features)"), Lang("与 MFG 1.7.1 冲突会崩溃，但与 SM86 方案不冲突", "Conflicts with MFG 1.7.1 and crashes, but works with the SM86 scheme"))
            });
        if (version == null)
        {
            return;
        }

        if (version == "2.0.0" && game.FrameGenEnabled)
        {
            string? warn = await ShowChoiceAsync(
                Lang("兼容性提醒", "Compatibility warning"),
                Lang("当前游戏已开启多帧生成。GITC DLSS5 2.0.0 与 MFG 1.7.1 存在冲突，同时开启会导致游戏崩溃；如果多帧生成用的是 SM86 方案可以忽略此提醒。仍要继续安装 2.0.0 吗？",
                    "Frame generation is currently enabled for this game. GITC DLSS5 2.0.0 conflicts with MFG 1.7.1 and crashes when both are enabled; if frame generation uses the SM86 scheme, ignore this warning. Continue installing 2.0.0 anyway?"),
                new List<(string, string, string)>
                {
                    ("go", Lang("仍要继续", "Continue anyway"), Lang("自行承担崩溃风险", "You accept the crash risk")),
                    ("no", Lang("取消安装", "Cancel"), Lang("返回列表", "Return to the list"))
                });
            if (warn != "go")
            {
                return;
            }
        }

        string? api = await ShowChoiceAsync(
            Lang("选择渲染 API", "Choose a rendering API"),
            Lang("不确定时选 DX12（绝大多数现代游戏）", "Choose DX12 if unsure (most modern games)"),
            new List<(string, string, string)>
            {
                ("dx12", "DX12", Lang("适用于 DX12 游戏（推荐）", "For DX12 games (recommended)")),
                ("dx9", "DX9", Lang("适用于 DX9 老游戏", "For older DX9 games")),
                ("vulkan", "Vulkan", Lang("适用于 Vulkan 游戏", "For Vulkan games"))
            });
        if (api == null)
        {
            return;
        }

        string installSubDir = "";
        bool is32bit = false;
        if (api == "dx9")
        {
            string? installMode = await ShowChoiceAsync(
                Lang("选择安装方式", "Choose an installation method"),
                Lang("起源引擎游戏（如半条命 2）需要装到 bin 子目录", "Source engine games (e.g. Half-Life 2) must be installed into the bin subfolder"),
                new List<(string, string, string)>
                {
                    ("std", Lang("通用安装", "Standard install"), Lang("安装到游戏 exe 目录", "Install into the game exe folder")),
                    ("src", Lang("起源引擎安装", "Source engine install"), Lang("安装到 bin 子目录", "Install into the bin subfolder"))
                });
            if (installMode == null)
            {
                return;
            }
            if (installMode == "src")
            {
                installSubDir = "bin";
            }

            string? bit = await ShowChoiceAsync(
                Lang("选择游戏位数", "Choose the game architecture"),
                Lang("老 DX9 游戏多为 32 位", "Older DX9 games are usually 32-bit"),
                new List<(string, string, string)>
                {
                    ("32", Lang("32 位游戏", "32-bit game"), Lang("老 DX9 游戏选这个", "Choose this for older DX9 games")),
                    ("64", Lang("64 位游戏", "64-bit game"), Lang("现代游戏选这个", "Choose this for modern games"))
                });
            if (bit == null)
            {
                return;
            }
            is32bit = bit == "32";
        }

        string? gpuChoice = await ShowChoiceAsync(
            Lang("选择显卡型号", "Choose a GPU model"),
            Lang("用于安装对应显卡的 DLSS NR 模型", "Used to install the matching DLSS NR model"),
            new List<(string, string, string)>
            {
                ("2030", Lang("RTX 20/30 系", "RTX 20/30 series"), Lang("适用于 RTX 20 系和 30 系显卡", "For RTX 20 and 30 series cards")),
                ("40", Lang("RTX 40 系", "RTX 40 series"), Lang("适用于 RTX 40 系显卡", "For RTX 40 series cards")),
                ("50", Lang("RTX 50 系", "RTX 50 series"), Lang("适用于 RTX 50 系显卡", "For RTX 50 series cards"))
            });
        if (gpuChoice == null)
        {
            return;
        }

        ShowProgress(Lang("正在安装 GITC DLSS5 基础文件…", "Installing GITC DLSS5 base files…"));
        bool installed = false;
        string error = "";
        try
        {
            installed = await Task.Run(() =>
                FilePatcher.InstallGITCDLSS5_Base(game, api, installSubDir, is32bit, version));
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        if (!installed)
        {
            HideProgress();
            PostStatus("基础文件安装失败：" + error);
            return;
        }

        ShowProgress(Lang("正在安装对应显卡的 DLSS NR 模型…", "Installing the matching DLSS NR model…"));
        bool nrOk = await Task.Run(() => FilePatcher.InstallGITCDLSS5_DLSSNR(game, gpuChoice, installSubDir));
        HideProgress();

        if (nrOk)
        {
            game.Dlss5Enabled = true;
            game.GitcVersion = version;
            SaveGames();
            PushGames();
            PostSettings();
            PostStatus($"GITC DLSS5 {version} 安装成功，进游戏按 Home 打开菜单并手动开启 NR（默认关闭）");
        }
        else
        {
            PostStatus("DLSS NR 模型安装失败");
        }
    }

    private async Task RestoreAllAsync(GameInfo game)
    {
        if (!game.FrameGenEnabled && !game.Dlss5Enabled && !game.Dx9Dlss5Enabled)
        {
            await ShowChoiceAsync(
                Lang("无需还原", "Nothing to restore"),
                Lang("这个游戏当前没有开启任何补丁。", "No patches are currently enabled for this game."),
                new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
            return;
        }

        string? confirm = await ShowChoiceAsync(
            Lang("一键还原", "Restore everything"),
            Lang("将删除多帧生成与 DLSS5 的补丁文件并恢复备份，确定继续吗？", "This will remove frame generation and DLSS5 patch files and restore backups. Continue?"),
            new List<(string, string, string)>
            {
                ("yes", Lang("确定还原", "Restore"), Lang("恢复全部原始文件", "Restore all original files")),
                ("no", Lang("取消", "Cancel"), Lang("保持现状", "Keep everything as is"))
            });
        if (confirm != "yes")
        {
            return;
        }

        ShowProgress(Lang("正在还原…", "Restoring…"));
        await Task.Run(() =>
        {
            if (game.FrameGenEnabled)
            {
                try { FilePatcher.RestoreFrameGen(game); } catch { }
            }
            if (game.Dlss5Enabled || game.Dx9Dlss5Enabled)
            {
                try { FilePatcher.RestoreGITCDLSS5(game); } catch { }
            }
        });
        HideProgress();

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

        SaveGames();
        PushGames();
        PostSettings();
        PostStatus("已还原，状态已重新检测");
    }

    // ---------------- 运行中游戏检测 ----------------
    private async Task DetectRunningAsync()
    {
        List<ProcessDetector.GameProcessInfo> running;
        try
        {
            running = ProcessDetector.GetRunningGames();
        }
        catch (Exception ex)
        {
            PostStatus("检测失败：" + ex.Message);
            return;
        }

        List<ProcessDetector.GameProcessInfo> candidates = new List<ProcessDetector.GameProcessInfo>();
        foreach (ProcessDetector.GameProcessInfo p in running)
        {
            bool already = false;
            foreach (GameInfo g in _games)
            {
                if (Norm(g.ExePath) == Norm(p.ExePath))
                {
                    already = true;
                    break;
                }
            }
            if (!already)
            {
                candidates.Add(p);
            }
        }
        if (candidates.Count > 20)
        {
            candidates = candidates.GetRange(0, 20);
        }

        if (candidates.Count == 0)
        {
            await ShowChoiceAsync(
                Lang("没有检测到", "Nothing detected"),
                Lang("没有发现可添加的运行中游戏进程。已经在列表里的游戏不会重复显示。", "No running game processes were found. Games already in the list are not shown again."),
                new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
            return;
        }

        List<DialogOption> opts = new List<DialogOption>();
        for (int i = 0; i < candidates.Count; i++)
        {
            ProcessDetector.GameProcessInfo p = candidates[i];
            string desc = Path.GetFileName(p.ExePath);
            if (p.WindowTitle.Length > 0)
            {
                desc = p.WindowTitle + "  ·  " + desc;
            }
            desc += "  ·  " + p.MemoryMB + " MB";
            opts.Add(new DialogOption
            {
                Value = "i" + i,
                Label = p.GameName,
                Desc = desc,
                Icon = p.IconDataUrl
            });
        }
        opts.Add(new DialogOption { Value = "cancel", Label = Lang("先不添加", "Don't add"), Desc = Lang("不会修改列表", "The list will not be changed") });

        string? pick = await ShowDialogAsync(
            Lang("检测到 " + candidates.Count + " 个运行中的程序", candidates.Count + " running programs detected"),
            Lang("选择要添加进列表的游戏（按游戏特征和内存排序）：", "Select games to add to the list (sorted by game traits and memory):"),
            opts);

        if (pick == null || pick == "cancel")
        {
            return;
        }
        int index = int.Parse(pick.Substring(1));
        GameInfo? game = GameScanner.AddGameByExe(candidates[index].ExePath);
        if (game == null)
        {
            PostStatus("添加失败，这个 exe 没能识别成游戏");
            return;
        }
        if (IsDuplicate(game))
        {
            PostStatus("这个游戏已经在列表里了");
            return;
        }

        _games.Add(game);
        SaveGames();
        PushGames();
        PostStatus("已添加：" + GameDisplayName(game));
        await ShowChoiceAsync(
            Lang("游戏正在运行", "Game is running"),
            Lang("已添加：" + game.Name + "\n\n请先在游戏设置里开启 DLSS / 帧生成，然后完全退出游戏，再安装补丁；文件被占用会导致安装失败。",
                "Added: " + game.Name + "\n\nEnable DLSS / frame generation in the game settings first, then fully exit the game before installing the patch; locked files will cause installation to fail."),
            new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
    }

    private async Task SwitchDemoPageAsync(string page)
    {
        await Task.Delay(700);
        try
        {
            string js = "(function(){var b=document.querySelector('.nav-item[data-page=\"" + page + "\"]');if(b){b.click();}})()";
            await _web.ExecuteScriptAsync(js);
            if (_demoScrollEnd)
            {
                await Task.Delay(400);
                await _web.ExecuteScriptAsync("(function(){var ps=document.querySelectorAll('.page-pane');for(var i=0;i<ps.length;i++){if(!ps[i].classList.contains('hidden')){ps[i].scrollTop=ps[i].scrollHeight;}}})()");
            }
        }
        catch
        {
        }
    }

    // ---------------- 应用信息 / 打开路径 ----------------

    private void PostAppInfo()
    {
        PostJson(new
        {
            type = "appinfo",
            version = Application.ProductVersion,
            isBeta = DLSSFrameGenEnabler_WinUI3.App.IsBeta,
            dataPath = BetaGamesPath,
            dataDir = Path.GetDirectoryName(BetaGamesPath) ?? "",
            logPath = Path.Combine(AppContext.BaseDirectory, "error.log")
        });
    }

    private void OpenPath(string target)
    {
        try
        {
            string path;
            if (target == "log")
            {
                path = Path.Combine(AppContext.BaseDirectory, "error.log");
                if (!File.Exists(path))
                {
                    PostStatus("日志文件还不存在，先运行一次再来看");
                    return;
                }
            }
            else
            {
                path = Path.GetDirectoryName(BetaGamesPath) ?? "";
            }
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                PostStatus("路径不存在：" + path);
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            PostStatus("打开失败：" + ex.Message);
        }
    }

    // ---------------- 游戏右键菜单操作 ----------------

    private void LaunchGame(GameInfo game)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(game.ExePath) || !File.Exists(game.ExePath))
            {
                PostStatus(Lang("找不到游戏可执行文件，请检查游戏路径", "Game executable not found. Please check the game path."));
                return;
            }

            string workDir = Path.GetDirectoryName(game.ExePath) ?? game.GamePath;
            Process.Start(new ProcessStartInfo
            {
                FileName = game.ExePath,
                WorkingDirectory = workDir,
                UseShellExecute = true
            });
            PostStatus(Lang("正在启动：" + game.Name, "Launching: " + game.Name));
        }
        catch (Exception ex)
        {
            WriteLog("LaunchGame FAILED: " + ex);
            PostStatus(Lang("启动游戏失败：" + ex.Message, "Failed to launch game: " + ex.Message));
        }
    }

    private void OpenGameFolder(GameInfo game)
    {
        try
        {
            string dir = "";
            if (!string.IsNullOrWhiteSpace(game.ExePath) && File.Exists(game.ExePath))
            {
                dir = Path.GetDirectoryName(game.ExePath) ?? "";
            }
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                dir = game.GamePath ?? "";
            }
            if (!string.IsNullOrWhiteSpace(dir) && !Path.IsPathRooted(dir))
            {
                dir = Path.GetFullPath(dir);
            }
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                PostStatus(Lang("游戏目录不存在", "Game folder not found"));
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            WriteLog("OpenGameFolder FAILED: " + ex);
            PostStatus(Lang("打开目录失败：" + ex.Message, "Failed to open folder: " + ex.Message));
        }
    }

    // ---------------- 设置 ----------------

    private static string Lang(string zh, string en)
    {
        return Translator.IsEnglish ? en : zh;
    }

    private static string BackupDir
    {
        get { return Path.Combine(AppContext.BaseDirectory, "backup"); }
    }

    private object BackupInfo()
    {
        long size = 0;
        List<string> dirs = new List<string>();
        try
        {
            DirectoryInfo di = new DirectoryInfo(BackupDir);
            if (di.Exists)
            {
                foreach (FileInfo f in di.GetFiles("*", SearchOption.AllDirectories))
                {
                    size += f.Length;
                }
                foreach (DirectoryInfo d in di.GetDirectories())
                {
                    dirs.Add(d.Name);
                }
            }
        }
        catch
        {
        }
        return new { sizeMb = size / (1024 * 1024), dirs };
    }

    private string ReadWallpaperDataUrl()
    {
        try
        {
            string path = _settings.CustomWallpaperPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return "";
            }
            if (new FileInfo(path).Length > 8L * 1024 * 1024)
            {
                return "";
            }
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string mime = ext == ".png" ? "image/png"
                : ext == ".webp" ? "image/webp"
                : ext == ".bmp" ? "image/bmp"
                : "image/jpeg";
            return "data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(path));
        }
        catch
        {
            return "";
        }
    }

    private void PostSettings()
    {
        GpuInfo[] gpus;
        try
        {
            gpus = GpuDetector.GetAllGpus();
        }
        catch
        {
            gpus = Array.Empty<GpuInfo>();
        }

        List<object> gpuItems = new List<object>();
        foreach (GpuInfo gpu in gpus)
        {
            gpuItems.Add(new { name = gpu.Name, series = gpu.Series, integrated = gpu.IsIntegrated });
        }

        object backup = BackupInfo();
        PostJson(new
        {
            type = "settings",
            dark = _settings.DarkMode,
            anim = _settings.EnableAnimation,
            backdrop = _settings.BackdropType,
            language = _settings.Language,
            fontSize = _settings.FontSize,
            english = Translator.IsEnglish,
            startupDialog = _settings.ShowStartupDialog,
            rememberGames = _settings.RememberGames,
            autoUpdate = _settings.AutoCheckUpdate,
            gpuSelected = _settings.SelectedGpuName,
            gpus = gpuItems,
            wallpaper = ReadWallpaperDataUrl(),
            backup
        });
    }

    private void ApplyNativeWindowTransitions()
    {
        try
        {
            int disabled = _settings.EnableAnimation ? 0 : 1;
            _ = DwmSetWindowAttribute(Handle, 3, ref disabled, sizeof(int));
        }
        catch
        {
        }
    }

    private void ApplyWindowTheme()
    {        bool dark = _settings.DarkMode;
        try
        {
            int useDark = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(Handle, 20, ref useDark, sizeof(int));
        }
        catch
        {
        }
        BackColor = dark ? Color.FromArgb(0x1B, 0x1B, 0x19) : Color.FromArgb(0xF5, 0xF5, 0xF2);
        _web.DefaultBackgroundColor = _settings.BackdropType == 5 ? Color.Transparent : BackColor;
    }

    private async Task ShowStartupNoticeAsync()
    {
        await Task.Delay(700);
        List<DialogOption> options = new List<DialogOption>
        {
            new DialogOption
            {
                Value = "ok",
                Label = Lang("了解并不再弹出", "Understood, don't show again"),
                Desc = ""
            }
        };
        string? result = await ShowDialogAsync(
            Lang("温馨提示", "Friendly Reminder"),
            Lang("本软件目前只在小黑盒免费分享，请勿倒卖。\n本软件已免费开源，也可以前往 GitHub 上下载本软件。\n如果您是在其他渠道购买本软件，请您立刻向渠道方申请退款！",
                "This software is shared for free on Xiaoheihe only, do not resell.\nIt is open source and free — you can also download it from GitHub.\nIf you purchased it from another channel, please request a refund immediately!"),
            options);
        if (result == "ok")
        {
            _settings.ShowStartupDialog = false;
            _settings.Save();
            PostSettings();
        }
    }

    private async Task ShowStartupDialogsAsync()
    {
        if (_settings.ShowStartupDialog)
        {
            await ShowStartupNoticeAsync();
        }
        else
        {
            await Task.Delay(700);
        }

        if (DLSSFrameGenEnabler_WinUI3.App.IsBeta && _settings.ShowBetaFeedback)
        {
            await ShowBetaFeedbackAsync();
        }
    }

    private async Task ShowBetaFeedbackAsync()
    {
        List<DialogOption> options = new List<DialogOption>
        {
            new DialogOption
            {
                Value = "ok",
                Label = Lang("我知道了", "I Understand"),
                Desc = Lang("下次启动仍会显示", "Will appear again next launch")
            },
            new DialogOption
            {
                Value = "off",
                Label = Lang("不再显示此提示", "Don't show this again"),
                Desc = Lang("以后启动不再弹出", "Stop showing this at startup")
            }
        };

        string? result = await ShowDialogAsync(
            Lang("测试版 - 反馈指引", "Beta Version - Feedback Guide"),
            Lang(
                "🔬 测试版本\n" +
                "当你在使用软件过程中遇到问题时，请在设置页打开运行日志（error.log），然后把日志和问题描述一起发给开发者，能更快定位问题。\n\n" +
                "操作步骤：\n" +
                "1. 打开设置页\n" +
                "2. 找到「数据与日志」\n" +
                "3. 点击「打开日志」\n" +
                "4. 把 error.log 和问题描述一起发开发者\n\n" +
                "可以通过 GitHub Issues、哔哩哔哩或小黑盒联系开发者。",
                "🔬 BETA VERSION\n" +
                "If you run into a problem, open the runtime log (error.log) from Settings and send it to the developer together with a description of the issue so it can be diagnosed faster.\n\n" +
                "Steps:\n" +
                "1. Open Settings\n" +
                "2. Find \"Data & Logs\"\n" +
                "3. Click \"Open log\"\n" +
                "4. Send error.log and the problem description to the developer\n\n" +
                "You can reach the developer through GitHub Issues, Bilibili, or Xiaoheihe."),
            options);

        if (result == "off")
        {
            _settings.ShowBetaFeedback = false;
            _settings.Save();
            PostSettings();
        }
    }

    private async Task CheckUpdateAsync(bool manual = false)
    {
        try
        {
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            string json = await client.GetStringAsync(_settings.UpdateCheckUrl);
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("version", out JsonElement versionElement))
            {
                if (manual)
                {
                    await ShowChoiceAsync(
                        Lang("检查更新", "Check for Updates"),
                        Lang("更新服务器没有返回版本信息，请稍后重试。", "The update server returned no version information. Try again later."),
                        new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
                }
                return;
            }
            string remote = versionElement.GetString() ?? "";
            if (remote.Length == 0 || CompareVersion(remote, Application.ProductVersion) <= 0)
            {
                if (manual)
                {
                    await ShowChoiceAsync(
                        Lang("检查更新", "Check for Updates"),
                        Lang("当前已是最新版本：V" + Application.ProductVersion, "You're up to date: V" + Application.ProductVersion),
                        new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
                }
                return;
            }
            if (remote == _settings.SkipUpdateVersion)
            {
                if (manual)
                {
                    await ShowChoiceAsync(
                        Lang("检查更新", "Check for Updates"),
                        Lang("此版本已设置为不再提示。", "You chose not to be reminded about this version."),
                        new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
                }
                return;
            }

            string githubUrl = GetGithubUrl(doc.RootElement);
            string? choice = await ShowDialogAsync(
                Lang("发现新版本：" + remote, "New version available: " + remote),
                Lang("最新版本：" + remote + "\n当前版本：" + Application.ProductVersion +
                     "\n可以下载 GitHub 发行包，或打开项目仓库自行下载。",
                    "Latest version: " + remote + "\nCurrent version: " + Application.ProductVersion +
                    "\nDownload the GitHub release, or open the repository to download it yourself."),
                new List<DialogOption>
                {
                    new DialogOption
                    {
                        Value = "download",
                        Label = Lang("直接下载（GitHub）", "Direct download (GitHub)"),
                        Desc = Lang("从 GitHub Releases 下载最新发行包", "Download the latest release asset from GitHub")
                    },
                    new DialogOption
                    {
                        Value = "browser",
                        Label = Lang("GitHub（浏览器打开）", "GitHub (open in browser)"),
                        Desc = Lang("跳转到项目仓库自行下载", "Open the project repository to download it yourself")
                    },
                    new DialogOption
                    {
                        Value = "skip",
                        Label = Lang("不再提示", "Don't remind again"),
                        Desc = Lang("以后不再提示此版本", "Skip this version from now on")
                    }
                });

            switch (choice)
            {
                case "download":
                    await DownloadFromGitHubAsync(githubUrl, remote);
                    break;
                case "browser":
                    OpenUrl(githubUrl);
                    break;
                case "skip":
                    _settings.SkipUpdateVersion = remote;
                    _settings.Save();
                    break;
                default:
                    break;
            }
        }
        catch
        {
            if (manual)
            {
                await ShowChoiceAsync(
                    Lang("检查更新失败", "Update check failed"),
                    Lang("无法连接更新服务器，请检查网络后重试。", "Could not reach the update server. Check your network and try again."),
                    new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
            }
        }
    }

    private string GetGithubUrl(JsonElement root)
    {
        string githubUrl = _settings.GithubUrl;
        if (root.TryGetProperty("downloadLinks", out JsonElement links) &&
            links.ValueKind == JsonValueKind.Object &&
            links.TryGetProperty("github", out JsonElement nestedGithub))
        {
            string nested = nestedGithub.GetString() ?? "";
            if (nested.Length > 0)
            {
                githubUrl = nested;
            }
        }
        if (root.TryGetProperty("github", out JsonElement flatGithub))
        {
            string flat = flatGithub.GetString() ?? "";
            if (flat.Length > 0)
            {
                githubUrl = flat;
            }
        }
        if (root.TryGetProperty("downloadUrlGithub", out JsonElement downloadGithub))
        {
            string download = downloadGithub.GetString() ?? "";
            if (download.Length > 0)
            {
                githubUrl = download;
            }
        }
        return githubUrl;
    }

    private const string GithubReleasesApi = "https://api.github.com/repos/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5/releases";

    private string GetGithubReleasesApi(string githubUrl)
    {
        try
        {
            Uri uri = new Uri(githubUrl);
            if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = uri.AbsolutePath.Trim('/').Split('/');
                if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
                {
                    return "https://api.github.com/repos/" + parts[0] + "/" + parts[1] + "/releases";
                }
            }
        }
        catch
        {
        }
        return GithubReleasesApi;
    }

    private async Task DownloadFromGitHubAsync(string githubUrl, string? expectedVersion = null)
    {
        try
        {
            if (_downloadJob != null && !_downloadJob.Completed)
            {
                PostJson(new { type = "navigate", page = "download" });
                PostStatus(Lang("已有下载任务正在进行，请先回到下载页面处理。",
                    "A download is already in progress. Please handle it on the Downloads page."));
                return;
            }
            PostStatus(Lang("正在从 GitHub 获取下载信息…", "Getting release information from GitHub…"));
            string apiBase = GetGithubReleasesApi(githubUrl);
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DLSSFrameGenEnabler-WebUI/1.50");

            string releasesJson = await client.GetStringAsync(apiBase + "?per_page=20");
            using JsonDocument doc = JsonDocument.Parse(releasesJson);
            JsonElement release = default;
            bool found = false;
            bool matchedExpected = expectedVersion == null;
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                bool prerelease = item.TryGetProperty("prerelease", out JsonElement pre) && pre.GetBoolean();
                if (prerelease != DLSSFrameGenEnabler_WinUI3.App.IsBeta)
                {
                    continue;
                }
                if (expectedVersion != null)
                {
                    string tag = item.TryGetProperty("tag_name", out JsonElement tagElement)
                        ? (tagElement.GetString() ?? "")
                        : "";
                    if (CompareVersion(tag, expectedVersion) == 0)
                    {
                        release = item.Clone();
                        found = true;
                        matchedExpected = true;
                        break;
                    }
                }
                else
                {
                    release = item.Clone();
                    found = true;
                    break;
                }
            }

            if (expectedVersion != null && !matchedExpected)
            {
                string message = Lang(
                    "GitHub 上暂时没有找到 " + expectedVersion + " 对应的发行包，无法直接下载该版本。\n\n" +
                    "请改用“GitHub（浏览器打开）”前往项目 Releases 页面确认，或稍后再试。",
                    "No release asset matching " + expectedVersion + " was found on GitHub, so this version cannot be downloaded directly.\n\n" +
                    "Use \"GitHub (open in browser)\" to check the project's Releases page, or try again later.");
                await ShowChoiceAsync(
                    Lang("暂时无法直接下载", "Direct download unavailable"),
                    message,
                    new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
                return;
            }

            if (!found)
            {
                using HttpClient latestClient = new HttpClient();
                latestClient.Timeout = TimeSpan.FromSeconds(30);
                latestClient.DefaultRequestHeaders.UserAgent.ParseAdd("DLSSFrameGenEnabler-WebUI/1.50");
                using JsonDocument latestDoc = JsonDocument.Parse(
                    await latestClient.GetStringAsync(apiBase + "/latest"));
                release = latestDoc.RootElement.Clone();
            }

            if (!release.TryGetProperty("assets", out JsonElement assets) ||
                assets.ValueKind != JsonValueKind.Array ||
                assets.GetArrayLength() == 0)
            {
                await ShowChoiceAsync(
                    Lang("下载失败", "Download failed"),
                    Lang("最新版本中没有找到可下载的文件。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                        "No downloadable files were found in the latest release.\n\nTip: Users in mainland China may need a VPN/accelerator for direct GitHub downloads."),
                    new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
                return;
            }

            JsonElement asset = assets[0];
            foreach (JsonElement candidate in assets.EnumerateArray())
            {
                if (candidate.TryGetProperty("name", out JsonElement candidateName) &&
                    (candidateName.GetString() ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    asset = candidate;
                    break;
                }
            }

            string downloadUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                ? (urlElement.GetString() ?? "")
                : "";
            string fileName = asset.TryGetProperty("name", out JsonElement nameElement)
                ? (nameElement.GetString() ?? "update.zip")
                : "update.zip";
            long fileSize = asset.TryGetProperty("size", out JsonElement sizeElement)
                ? sizeElement.GetInt64()
                : 0L;
            if (downloadUrl.Length == 0)
            {
                await ShowChoiceAsync(
                    Lang("下载失败", "Download failed"),
                    Lang("GitHub 没有返回可用的下载地址。", "GitHub did not return a usable download URL."),
                    new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
                return;
            }

            string releaseVersion = release.TryGetProperty("tag_name", out JsonElement releaseTag)
                ? (releaseTag.GetString() ?? "")
                : "";
            if (releaseVersion.Length == 0)
            {
                releaseVersion = expectedVersion ?? "";
            }
            string releaseNotes = release.TryGetProperty("body", out JsonElement releaseBody)
                ? CleanReleaseNotes(releaseBody.GetString() ?? "")
                : "";

            string sizeLine = fileSize > 0
                ? Lang("文件大小：" + (fileSize / 1024.0 / 1024.0).ToString("0.0") + " MB",
                       "File size: " + (fileSize / 1024.0 / 1024.0).ToString("0.0") + " MB")
                : "";
            string notesText = releaseNotes.Length > 0
                ? releaseNotes
                : Lang("GitHub 暂未提供此版本的更新说明。", "GitHub did not provide release notes for this version.");
            string dialogText = Lang(
                "版本：" + releaseVersion +
                (sizeLine.Length > 0 ? "\n" + sizeLine : "") +
                "\n\n更新说明：\n" + notesText,
                "Version: " + releaseVersion +
                (sizeLine.Length > 0 ? "\n" + sizeLine : "") +
                "\n\nUpdate notes:\n" + notesText);
            string? downloadChoice = await ShowDialogAsync(
                Lang("发现新版本：" + releaseVersion, "New version available: " + releaseVersion),
                dialogText,
                new List<DialogOption>
                {
                    new DialogOption
                    {
                        Value = "download",
                        Label = Lang("下载", "Download"),
                        Desc = Lang("选择保存位置并开始下载", "Choose a save location and start downloading")
                    },
                    new DialogOption
                    {
                        Value = "cancel",
                        Label = Lang("取消", "Cancel"),
                        Desc = Lang("返回，不下载", "Go back without downloading")
                    }
                });
            if (downloadChoice != "download")
            {
                return;
            }

            using SaveFileDialog save = new SaveFileDialog();
            save.Title = Lang("保存 GitHub 下载文件", "Save GitHub download");
            save.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            save.FileName = fileName;
            save.Filter = Lang("压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                "Zip files (*.zip)|*.zip|All files (*.*)|*.*");
            save.OverwritePrompt = true;
            if (save.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (_downloadJob != null && !_downloadJob.Completed)
            {
                PostJson(new { type = "navigate", page = "download" });
                PostStatus(Lang("已有下载任务正在进行，请先回到下载页面处理。",
                    "A download is already in progress. Please handle it on the Downloads page."));
                return;
            }

            string targetPath = save.FileName;
            string partialPath = targetPath + ".part";
            try
            {
                if (File.Exists(partialPath))
                {
                    File.Delete(partialPath);
                }
            }
            catch (Exception ex)
            {
                WriteLog("Download cleanup FAILED: " + ex.Message);
                await ShowChoiceAsync(
                    Lang("下载失败", "Download failed"),
                    Lang("无法清理旧的临时下载文件，请关闭占用该文件的程序后重试。",
                        "Could not remove the old temporary download file. Close the program using it and try again."),
                    new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
                return;
            }

            DownloadJob job = new DownloadJob
            {
                FileName = fileName,
                DownloadUrl = downloadUrl,
                TargetPath = targetPath,
                PartialPath = partialPath,
                ReleaseVersion = releaseVersion,
                TotalBytes = fileSize,
                DownloadedBytes = 0,
                StartedAt = DateTime.UtcNow
            };
            _downloadJob = job;
            PostDownloadState(job, "downloading", true);
            job.Task = RunDownloadAsync(job);
        }
        catch (Exception ex)
        {
            WriteLog("DownloadFromGitHub FAILED: " + ex.Message);
            await ShowChoiceAsync(
                Lang("下载失败", "Download failed"),
                Lang("下载失败，可能是网络问题或 GitHub 访问限制导致。\n\n提示：中国大陆用户建议使用加速器后再使用直接下载功能。",
                    "Download failed. This may be due to network issues or GitHub access limits.\n\nTip: Users in mainland China may need a VPN/accelerator for direct GitHub downloads."),
                new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
        }
    }

    private async Task RunDownloadAsync(DownloadJob job)
    {
        try
        {
            await DownloadLoopAsync(job);

            if (job.Cancelled)
            {
                FinalizeCancelledDownload(job);
                return;
            }
            if (job.Paused)
            {
                PostDownloadState(job, "paused");
                return;
            }
            if (job.TotalBytes > 0 && job.DownloadedBytes < job.TotalBytes)
            {
                throw new IOException("Download ended before all bytes were received.");
            }

            if (File.Exists(job.TargetPath))
            {
                File.Delete(job.TargetPath);
            }
            File.Move(job.PartialPath, job.TargetPath);
            job.Completed = true;
            if (job.TotalBytes <= 0)
            {
                job.TotalBytes = job.DownloadedBytes;
            }
            job.DownloadedBytes = job.TotalBytes;
            PostDownloadState(job, "completed");
            PostStatus(Lang("下载完成：" + job.FileName, "Download complete: " + job.FileName));
        }
        catch (OperationCanceledException) when (job.Cancelled)
        {
            FinalizeCancelledDownload(job);
        }
        catch (OperationCanceledException) when (job.Paused)
        {
            PostDownloadState(job, "paused");
        }
        catch (Exception ex)
        {
            if (job.Cancelled)
            {
                FinalizeCancelledDownload(job);
                return;
            }
            CleanupPartial(job);
            if (ReferenceEquals(_downloadJob, job))
            {
                _downloadJob = null;
            }
            WriteLog("Download job FAILED: " + ex.Message);
            PostDownloadState(job, "failed", false,
                Lang("下载失败，可能是网络问题或 GitHub 访问限制导致。",
                    "Download failed. This may be due to network issues or GitHub access limits."));
            PostStatus(Lang("下载失败：" + job.FileName, "Download failed: " + job.FileName));
        }
    }

    private async Task DownloadLoopAsync(DownloadJob job)
    {
        if (job.Paused || job.Cancelled)
        {
            throw new OperationCanceledException();
        }
        long existingBytes = 0;
        try
        {
            if (File.Exists(job.PartialPath))
            {
                existingBytes = new FileInfo(job.PartialPath).Length;
            }
        }
        catch
        {
            existingBytes = 0;
        }
        job.DownloadedBytes = existingBytes;

        if (job.TotalBytes > 0 && job.DownloadedBytes >= job.TotalBytes && File.Exists(job.PartialPath))
        {
            return;
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        job.ReadCts = cts;
        CancellationToken token = cts.Token;
        try
        {
            if (job.Paused || job.Cancelled)
            {
                throw new OperationCanceledException(token);
            }
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(120);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DLSSFrameGenEnabler-WebUI/1.50");

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, job.DownloadUrl);
            if (job.DownloadedBytes > 0)
            {
                request.Headers.Range = new RangeHeaderValue(job.DownloadedBytes, null);
            }

            using HttpResponseMessage response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, token);

            if (job.DownloadedBytes > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                if (job.TotalBytes > 0 && job.DownloadedBytes >= job.TotalBytes)
                {
                    return;
                }
                try
                {
                    if (File.Exists(job.PartialPath))
                    {
                        File.Delete(job.PartialPath);
                    }
                }
                catch
                {
                }
                job.DownloadedBytes = 0;
                throw new IOException("Server rejected the resume range.");
            }

            response.EnsureSuccessStatusCode();

            bool append = job.DownloadedBytes > 0 &&
                          response.StatusCode == HttpStatusCode.PartialContent &&
                          File.Exists(job.PartialPath);
            if (!append)
            {
                job.DownloadedBytes = 0;
            }

            if (job.TotalBytes <= 0)
            {
                if (response.Content.Headers.ContentRange?.Length is long totalLength &&
                    totalLength > 0)
                {
                    job.TotalBytes = totalLength;
                }
                else if (response.Content.Headers.ContentLength is long responseLength &&
                         responseLength > 0)
                {
                    job.TotalBytes = job.DownloadedBytes + responseLength;
                }
            }

            PostDownloadState(job, "downloading");
            using Stream stream = await response.Content.ReadAsStreamAsync(token);
            using FileStream fileStream = new FileStream(
                job.PartialPath,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                true);

            byte[] buffer = new byte[81920];
            DateTime lastReport = DateTime.MinValue;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                if (read <= 0)
                {
                    break;
                }
                await fileStream.WriteAsync(buffer, 0, read, token);
                job.DownloadedBytes += read;
                DateTime now = DateTime.UtcNow;
                if ((now - lastReport).TotalMilliseconds >= 250)
                {
                    lastReport = now;
                    PostDownloadState(job, "downloading");
                }
            }
            fileStream.Flush();
            PostDownloadState(job, "downloading");
        }
        finally
        {
            if (ReferenceEquals(job.ReadCts, cts))
            {
                job.ReadCts = null;
            }
        }
    }

    private void PauseDownload()
    {
        DownloadJob? job = _downloadJob;
        if (job == null || job.Completed || job.Cancelled || job.Paused)
        {
            return;
        }
        job.Paused = true;
        try
        {
            job.ReadCts?.Cancel();
        }
        catch
        {
        }
        PostDownloadState(job, "paused");
    }

    private async void ResumeDownload()
    {
        DownloadJob? job = _downloadJob;
        if (job == null || job.Completed || job.Cancelled || !job.Paused)
        {
            return;
        }
        if (job.Task != null && !job.Task.IsCompleted)
        {
            try
            {
                await job.Task;
            }
            catch
            {
            }
        }
        if (!ReferenceEquals(_downloadJob, job) || job.Cancelled || job.Completed)
        {
            return;
        }
        job.Paused = false;
        PostDownloadState(job, "downloading");
        job.Task = RunDownloadAsync(job);
    }

    private async Task CancelDownloadAsync()
    {
        DownloadJob? job = _downloadJob;
        if (job == null)
        {
            return;
        }
        job.Cancelled = true;
        job.Paused = false;
        try
        {
            job.ReadCts?.Cancel();
        }
        catch
        {
        }
        if (job.Task != null && !job.Task.IsCompleted)
        {
            try
            {
                await job.Task;
            }
            catch
            {
            }
        }
        if (ReferenceEquals(_downloadJob, job))
        {
            FinalizeCancelledDownload(job);
        }
    }

    private void FinalizeCancelledDownload(DownloadJob job)
    {
        CleanupPartial(job);
        if (ReferenceEquals(_downloadJob, job))
        {
            _downloadJob = null;
        }
        PostDownloadState(job, "cancelled");
    }

    private static void CleanupPartial(DownloadJob job)
    {
        try
        {
            if (!string.IsNullOrEmpty(job.PartialPath) && File.Exists(job.PartialPath))
            {
                File.Delete(job.PartialPath);
            }
        }
        catch
        {
        }
    }

    private void OpenDownloadFolder()
    {
        DownloadJob? job = _downloadJob;
        if (job == null)
        {
            return;
        }
        try
        {
            if (job.Completed && File.Exists(job.TargetPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + job.TargetPath + "\"")
                {
                    UseShellExecute = true
                });
                return;
            }
            string folder = Path.GetDirectoryName(job.TargetPath) ?? "";
            if (folder.Length > 0 && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"")
                {
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            PostStatus(Lang("打开下载位置失败：" + ex.Message, "Failed to open download folder: " + ex.Message));
        }
    }

    private void ClearDownloadRecord()
    {
        DownloadJob? job = _downloadJob;
        if (job == null || !job.Completed)
        {
            return;
        }
        _downloadJob = null;
        PostJson(new { type = "downloadClear" });
    }

    private void PostDownloadState(DownloadJob job, string status, bool navigate = false, string? error = null)
    {
        double percent = job.TotalBytes > 0
            ? Math.Min(100.0, job.DownloadedBytes * 100.0 / job.TotalBytes)
            : 0.0;
        double speed = 0.0;
        if (status == "downloading" && job.DownloadedBytes > 0)
        {
            double elapsed = (DateTime.UtcNow - job.StartedAt).TotalSeconds;
            if (elapsed > 1)
            {
                speed = job.DownloadedBytes / elapsed;
            }
        }
        PostJson(new
        {
            type = "downloadState",
            status,
            navigate,
            fileName = job.FileName,
            targetPath = job.TargetPath,
            releaseVersion = job.ReleaseVersion,
            totalBytes = job.TotalBytes,
            downloadedBytes = job.DownloadedBytes,
            percent,
            speed,
            error
        });
    }

    private static int CompareVersion(string a, string b)
    {
        int[] pa = ParseVersionParts(a);
        int[] pb = ParseVersionParts(b);
        int len = Math.Max(pa.Length, pb.Length);
        for (int i = 0; i < len; i++)
        {
            int va = i < pa.Length ? pa[i] : 0;
            int vb = i < pb.Length ? pb[i] : 0;
            if (va != vb)
            {
                return va > vb ? 1 : -1;
            }
        }
        return 0;
    }

    private static string CleanReleaseNotes(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "";
        }
        string text = body;
        // WebUI 弹窗按纯文本显示，先去掉 GitHub Release 里的图片和 HTML 标签，保留文字条目。
        text = Regex.Replace(text, @"<img[^>]*>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]*\)", "");
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]*\)", "$1");
        text = Regex.Replace(text, @"<[^>]+>", "");
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        text = text.Trim();
        const int maxLength = 12000;
        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength).TrimEnd() + "\n…";
        }
        return text;
    }

    private static int[] ParseVersionParts(string text)
    {
        List<int> parts = new List<int>();
        string current = "";
        foreach (char c in text)
        {
            if (char.IsDigit(c))
            {
                current += c;
            }
            else if (current.Length > 0)
            {
                parts.Add(int.Parse(current));
                current = "";
            }
        }
        if (current.Length > 0)
        {
            parts.Add(int.Parse(current));
        }
        return parts.ToArray();
    }

    private void SetSetting(JsonElement root)
    {
        string key = root.TryGetProperty("key", out JsonElement k) ? (k.GetString() ?? "") : "";
        JsonElement v = root.TryGetProperty("value", out JsonElement valueElement) ? valueElement : default;
        switch (key)
        {
            case "dark":
                if (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                {
                    _settings.DarkMode = v.GetBoolean();
                }
                break;
            case "anim":
                if (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                {
                    _settings.EnableAnimation = v.GetBoolean();
                    ApplyNativeWindowTransitions();
                }
                break;
            case "backdrop":
                if (v.ValueKind == JsonValueKind.Number)
                {
                    _settings.BackdropType = v.GetInt32();
                }
                break;
            case "language":
                if (v.ValueKind == JsonValueKind.Number)
                {
                    _settings.Language = v.GetInt32();
                    Translator.CurrentLanguage = (AppLanguage)_settings.Language;
                    Text = Translator.IsEnglish
                        ? "Frame Gen + DLSS5 Enabler"
                        : "多帧生成+DLSS5开启工具";
                    PushGames();
                }
                break;
            case "fontSize":
                if (v.ValueKind == JsonValueKind.Number)
                {
                    int fontSize = v.GetInt32();
                    _settings.FontSize = fontSize < 0 ? 0 : (fontSize > 2 ? 2 : fontSize);
                }
                break;
            case "startupDialog":
                if (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                {
                    _settings.ShowStartupDialog = v.GetBoolean();
                }
                break;
            case "rememberGames":
                if (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                {
                    _settings.RememberGames = v.GetBoolean();
                    if (!_settings.RememberGames)
                    {
                        _games.Clear();
                        SaveGames();
                        PushGames();
                    }
                    else
                    {
                        SaveGames();
                    }
                }
                break;
            case "autoUpdate":
                if (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                {
                    _settings.AutoCheckUpdate = v.GetBoolean();
                }
                break;
            case "gpu":
                _settings.SelectedGpuName = v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
                break;
        }
        _settings.Save();
        ApplyWindowTheme();
        PostSettings();
        if (key == "language")
        {
            PushGames();
        }
    }

    private void PickWallpaper()
    {
        try
        {
            using OpenFileDialog dialog = new OpenFileDialog
            {
                Title = Lang("选择壁纸图片", "Select wallpaper image"),
                Filter = "图片 / Images|*.jpg;*.jpeg;*.png;*.bmp;*.webp"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DLSSFrameGenEnabler");
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, "wallpaper" + Path.GetExtension(dialog.FileName).ToLowerInvariant());
            File.Copy(dialog.FileName, dest, true);
            _settings.CustomWallpaperPath = dest;
            _settings.BackdropType = 4;
            _settings.Save();
            ApplyWindowTheme();
            PostSettings();
        }
        catch (Exception ex)
        {
            PostStatus(Lang("设置壁纸失败：" + ex.Message, "Failed to set wallpaper: " + ex.Message));
        }
    }

    private void ResetWallpaper()
    {
        _settings.CustomWallpaperPath = "";
        _settings.BackdropType = 0;
        _settings.Save();
        ApplyWindowTheme();
        PostSettings();
    }

    private void OpenUrl(JsonElement root)
    {
        string url = root.TryGetProperty("url", out JsonElement u) ? (u.GetString() ?? "") : "";
        OpenUrl(url);
    }

    private void OpenUrl(string url)
    {
        if (url.Length == 0)
        {
            return;
        }
        try
        {
            Uri uri = new Uri(url);
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            WriteLog("OpenUrl FAILED: " + ex.Message);
        }
    }

    private void OpenBackup()
    {
        try
        {
            Directory.CreateDirectory(BackupDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + BackupDir + "\"")
            {
                UseShellExecute = true
            });
            PostSettings();
        }
        catch (Exception ex)
        {
            PostStatus(Lang("打开备份目录失败：" + ex.Message, "Failed to open backup folder: " + ex.Message));
        }
    }

    private void DeleteBackups(JsonElement root)
    {
        string baseDir = Path.GetFullPath(BackupDir);
        int deleted = 0;
        try
        {
            if (root.TryGetProperty("dirs", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    string name = item.GetString() ?? "";
                    if (name.Length == 0)
                    {
                        continue;
                    }
                    string full = Path.GetFullPath(Path.Combine(baseDir, name));
                    if (!full.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (Directory.Exists(full))
                    {
                        Directory.Delete(full, true);
                        deleted++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog("DeleteBackups FAILED: " + ex.Message);
        }
        PostSettings();
        PostStatus(Lang($"已删除 {deleted} 个备份", $"Deleted {deleted} backup(s)"));
    }

    // ---------------- SM86 配置 ----------------

    private GameInfo? FindGame(string exe)
    {
        foreach (GameInfo g in _games)
        {
            if (Norm(g.ExePath) == Norm(exe))
            {
                return g;
            }
        }
        return null;
    }

    private async Task OpenSm86ConfigAsync(GameInfo game)
    {
        string exeDir = Path.GetDirectoryName(game.ExePath) ?? "";
        string iniPath = SM86IniConfig.GetPath(exeDir);
        if (!File.Exists(iniPath))
        {
            await ShowChoiceAsync(
                Lang("找不到配置文件", "Configuration file not found"),
                Lang("游戏目录里没有 dlssg_sm86.ini，请先重新安装 SM86 方案。", "dlssg_sm86.ini is missing from the game folder. Reinstall the SM86 scheme first."),
                new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
            return;
        }

        Dictionary<string, string> cfg = SM86IniConfig.Read(iniPath);
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["General.Enabled"] = Sm86Int(cfg, "General.Enabled", 1) != 0 ? "1" : "0",
            ["FrameGeneration.Engine"] = Sm86Str(cfg, "FrameGeneration.Engine", "DLSS"),
            ["FrameGeneration.MaxGeneratedFrames"] = Sm86Int(cfg, "FrameGeneration.MaxGeneratedFrames", 3).ToString(),
            ["DLSSG.Optimized"] = Math.Clamp(Sm86Int(cfg, "DLSSG.Optimized", 1), 0, 3).ToString(),
            ["DLSSG.Preset"] = Sm86Str(cfg, "DLSSG.Preset", "A"),
            ["SuperResolution.Mode"] = Sm86Str(cfg, "SuperResolution.Mode", "Off"),
            ["Logging.Level"] = Math.Clamp(Sm86Int(cfg, "Logging.Level", 1), 0, 3).ToString(),
            ["Logging.FrameTiming"] = Sm86Int(cfg, "Logging.FrameTiming", 0) != 0 ? "1" : "0",
            ["XeSSFG.FlowResolution"] = Sm86Str(cfg, "XeSSFG.FlowResolution", "Middle"),
            ["XeSSFG.UseHudless"] = Sm86Str(cfg, "XeSSFG.UseHudless", "Auto")
        };

        PostJson(new
        {
            type = "sm86data",
            exe = game.ExePath,
            name = game.Name,
            proxy = FilePatcher.DetectSm86Proxy(exeDir),
            values
        });
    }

    private void SaveSm86(JsonElement root)
    {
        string exe = root.TryGetProperty("exe", out JsonElement e) ? (e.GetString() ?? "") : "";
        GameInfo? game = FindGame(exe);
        if (game == null)
        {
            PostStatus("找不到这个游戏");
            return;
        }
        string exeDir = Path.GetDirectoryName(game.ExePath) ?? "";
        string iniPath = SM86IniConfig.GetPath(exeDir);
        if (!File.Exists(iniPath))
        {
            PostStatus("找不到 dlssg_sm86.ini，请先重新安装 SM86 方案");
            return;
        }

        _busy = true;
        try
        {
            string proxy = root.TryGetProperty("proxy", out JsonElement p) ? (p.GetString() ?? "") : "";
            if (!string.IsNullOrEmpty(proxy) &&
                !proxy.Equals(FilePatcher.DetectSm86Proxy(exeDir), StringComparison.OrdinalIgnoreCase))
            {
                string error = "";
                if (!FilePatcher.SwitchSm86Proxy(exeDir, proxy, out error))
                {
                    PostStatus("切换代理 DLL 失败：" + error);
                    return;
                }
            }

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("values", out JsonElement vals) && vals.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty prop in vals.EnumerateObject())
                {
                    values[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? (prop.Value.GetString() ?? "")
                        : prop.Value.ToString();
                }
            }

            File.Copy(iniPath, iniPath + ".bak", overwrite: true);
            SM86IniConfig.Apply(iniPath, values);
            PostStatus("SM86 配置已保存，请完全退出游戏后重新启动");
        }
        catch (Exception ex)
        {
            PostStatus("保存失败：" + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ResetSm86(JsonElement root)
    {
        string exe = root.TryGetProperty("exe", out JsonElement e) ? (e.GetString() ?? "") : "";
        GameInfo? game = FindGame(exe);
        if (game == null)
        {
            PostStatus("找不到这个游戏");
            return;
        }
        string exeDir = Path.GetDirectoryName(game.ExePath) ?? "";
        string iniPath = SM86IniConfig.GetPath(exeDir);
        string patchDir = Path.Combine(AppContext.BaseDirectory, "Patches", "SM86多帧生成");
        bool ok = SM86IniConfig.RestoreDefault(iniPath, patchDir);
        PostStatus(ok ? "已恢复出厂 dlssg_sm86.ini，请完全退出游戏后重新启动" : "未找到出厂 ini 文件，请重新安装 SM86 方案");
    }

    private static int Sm86Int(Dictionary<string, string> cfg, string key, int fallback)
    {
        if (cfg.TryGetValue(key, out string value) && int.TryParse(value, out int num))
        {
            return num;
        }
        return fallback;
    }

    private static string Sm86Str(Dictionary<string, string> cfg, string key, string fallback)
    {
        if (cfg.TryGetValue(key, out string value) && value.Length > 0)
        {
            return value;
        }
        return fallback;
    }

    private async Task DemoSm86Async()
    {
        await Task.Delay(900);
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["General.Enabled"] = "1",
            ["FrameGeneration.Engine"] = "DLSS",
            ["FrameGeneration.MaxGeneratedFrames"] = "3",
            ["DLSSG.Optimized"] = "1",
            ["DLSSG.Preset"] = "A",
            ["SuperResolution.Mode"] = "Off",
            ["Logging.Level"] = "1",
            ["Logging.FrameTiming"] = "0",
            ["XeSSFG.FlowResolution"] = "Middle",
            ["XeSSFG.UseHudless"] = "Auto"
        };
        PostJson(new
        {
            type = "sm86data",
            exe = "",
            name = Lang("影之刃零 · PBZ", "Phantom Blade Zero · PBZ"),
            proxy = "version.dll",
            values
        });
    }

    private async Task DemoDetectAsync()
    {
        await Task.Delay(900);
        try
        {
            List<ProcessDetector.GameProcessInfo> real = ProcessDetector.GetRunningGames();
            WriteLog("detect demo: real candidates=" + real.Count);
            for (int i = 0; i < real.Count && i < 5; i++)
            {
                WriteLog("  " + real[i].ProcessName + " | " + real[i].ExePath + " | icon=" + (real[i].IconDataUrl.Length > 0));
            }
        }
        catch (Exception ex)
        {
            WriteLog("detect demo err: " + ex.Message);
        }
        string icon = ProcessDetector.GetExeIconDataUrl(Application.ExecutablePath);
        WriteLog("demo detect icon base64 len=" + icon.Length);
        List<DialogOption> opts = new List<DialogOption>
        {
            new DialogOption { Value = "i0", Label = Lang("影之刃零", "Phantom Blade Zero"), Desc = "Phantom Blade Zero  ·  PBZ-Win64-Shipping.exe  ·  4821 MB", Icon = icon },
            new DialogOption { Value = "i1", Label = Lang("鸣潮", "Wuthering Waves"), Desc = "Wuthering Waves  ·  Client-Win64-Shipping.exe  ·  3106 MB", Icon = icon },
            new DialogOption { Value = "i2", Label = Lang("明末：渊虚之羽", "Wuchang: Fallen Feathers"), Desc = "Wuchang: Fallen Feathers  ·  Wuchang-Win64-Shipping.exe  ·  2744 MB", Icon = icon },
            new DialogOption { Value = "cancel", Label = Lang("先不添加", "Don't add"), Desc = Lang("不会修改列表", "The list will not be changed") }
        };
        await ShowDialogAsync(
            Lang("检测到 3 个运行中的程序", "3 running programs detected"),
            Lang("选择要添加进列表的游戏（按游戏特征和内存排序）：", "Select games to add to the list (sorted by game traits and memory):"),
            opts);
    }

    // ---------------- 扫描 / 刷新 / 添加 ----------------

    private async Task RunScanAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        PostStatus("正在扫描 Steam / Epic / GOG / Ubisoft 游戏库…");
        _games.RemoveAll(g => IsObsoleteGameEntry(g));

        List<GameInfo> scanned;
        try
        {
            scanned = await Task.Run(() =>
            {
                // 与正式版 AutoScanBtn_Click 完全一致的扫描顺序与去重
                List<GameInfo> first = GameScanner.ScanSteamGames();
                List<GameInfo> second = GameScanner.ScanEpicGames();
                List<GameInfo> second2 = GameScanner.ScanGOGGames();
                List<GameInfo> all = new List<GameInfo>();
                all.AddRange(first);
                all.AddRange(second);
                all.AddRange(second2);
                all.AddRange(GameScanner.ScanUbisoftGames());

                List<GameInfo> unique = new List<GameInfo>();
                HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (GameInfo g in all)
                {
                    string gp = g.GamePath.TrimEnd('\\', '/').ToLowerInvariant();
                    string ep = string.IsNullOrEmpty(g.ExePath) ? "" : g.ExePath.TrimEnd('\\', '/').ToLowerInvariant();
                    if (!hashSet.Contains(gp) && (string.IsNullOrEmpty(ep) || !hashSet.Contains(ep)))
                    {
                        hashSet.Add(gp);
                        if (!string.IsNullOrEmpty(ep))
                        {
                            hashSet.Add(ep);
                        }
                        unique.Add(g);
                    }
                }

                // 影之刃零特殊处理：手动添加（与正式版一致）
                string[] pbzPaths =
                {
                    @"D:\SteamLibrary\steamapps\common\PBZ",
                    @"C:\Program Files (x86)\Steam\steamapps\common\PBZ"
                };
                foreach (string pbzPath in pbzPaths)
                {
                    string pbzExe = Path.Combine(pbzPath, "platform", "steam", "PBZ.exe");
                    if (File.Exists(pbzExe))
                    {
                        string gp = pbzPath.TrimEnd('\\', '/').ToLowerInvariant();
                        string ep = pbzExe.TrimEnd('\\', '/').ToLowerInvariant();
                        if (!hashSet.Contains(gp) && !hashSet.Contains(ep))
                        {
                            hashSet.Add(gp);
                            hashSet.Add(ep);
                            GameInfo game = new GameInfo();
                            game.Name = Lang("影之刃零", "Phantom Blade Zero");
                            game.GamePath = pbzPath;
                            game.ExePath = pbzExe;
                            game.SupportsFrameGen = true;
                            unique.Add(game);
                        }
                        break;
                    }
                }

                return unique;
            });
        }
        catch (Exception ex)
        {
            _busy = false;
            PostStatus("扫描失败：" + ex.Message);
            return;
        }

        WriteLog($"scan completed: {scanned.Count} games found before dedupe");

        int added = 0;
        foreach (GameInfo game in scanned)
        {
            if (string.IsNullOrWhiteSpace(game.GamePath))
            {
                continue;
            }
            if (IsDuplicate(game))
            {
                continue;
            }
            _games.Add(game);
            added++;
        }

        SaveGames();
        _busy = false;
        PushGames();
        PostStatus($"扫描完成：新增 {added} 个，列表共 {_games.Count} 个游戏");
    }

    private async Task RefreshStatusAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        PostStatus("正在刷新补丁状态…");

        await Task.Run(() =>
        {
            foreach (GameInfo game in _games)
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

        SaveGames();
        _busy = false;
        PushGames();
        PostStatus($"状态已刷新（共 {_games.Count} 个游戏）");
    }

    private void AddFolder()
    {
        using FolderBrowserDialog dlg = new FolderBrowserDialog();
        dlg.Description = Lang("选择游戏文件夹，会自动识别里面的游戏 exe", "Select a game folder; the game exe inside will be detected automatically");
        dlg.UseDescriptionForTitle = true;
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        GameInfo? game = GameScanner.AddGameByDirectory(dlg.SelectedPath);
        if (game == null)
        {
            PostStatus("没能在这个文件夹里识别出游戏 exe");
            return;
        }
        if (IsDuplicate(game))
        {
            PostStatus("这个游戏已经在列表里了");
            return;
        }

        _games.Add(game);
        SaveGames();
        PushGames();
        PostStatus("已添加：" + GameDisplayName(game));
    }

    private void AddExe()
    {
        using OpenFileDialog dlg = new OpenFileDialog();
        dlg.Title = Lang("选择游戏 exe", "Select game exe");
        dlg.Filter = Lang("游戏程序 (*.exe)|*.exe", "Game executables (*.exe)|*.exe");
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        GameInfo? game = GameScanner.AddGameByExe(dlg.FileName);
        if (game == null)
        {
            PostStatus("没能识别这个 exe");
            return;
        }
        if (IsDuplicate(game))
        {
            PostStatus("这个游戏已经在列表里了");
            return;
        }

        _games.Add(game);
        SaveGames();
        PushGames();
        PostStatus("已添加：" + GameDisplayName(game));
    }

    private async Task ExportLogsAsync()
    {
        ShowProgress(Lang("正在打包日志…", "Packaging logs…"));
        string? zipPath = null;
        string? error = null;
        try
        {
            zipPath = BuildLogZip();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            WriteLog("ExportLogs FAILED: " + ex);
        }
        HideProgress();

        if (zipPath != null)
        {
            string? choice = await ShowChoiceAsync(
                Lang("导出日志", "Export Logs"),
                Lang("日志已导出到桌面：\n" + zipPath, "Logs exported to Desktop:\n" + zipPath),
                new List<(string, string, string)>
                {
                    ("open", Lang("打开桌面", "Open Desktop"), ""),
                    ("ok", Lang("知道了", "Got it"), "")
                });
            if (choice == "open")
            {
                try { Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); }
                catch { }
            }
            return;
        }

        await ShowChoiceAsync(
            Lang("导出失败", "Export Failed"),
            Lang("日志导出失败：" + (error ?? "未知错误"), "Failed to export logs: " + (error ?? "Unknown error")),
            new List<(string, string, string)> { ("ok", Lang("知道了", "Got it"), "") });
    }

    private string BuildLogZip()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(desktop))
        {
            throw new IOException("Desktop folder not found");
        }
        Directory.CreateDirectory(desktop);

        string tempDir = Path.Combine(Path.GetTempPath(), "DFGE_Logs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DLSSFrameGenEnabler");
            CopyLogForExport(Path.Combine(AppContext.BaseDirectory, "error.log"), Path.Combine(tempDir, "error.log"));
            CopyLogForExport(Path.Combine(AppContext.BaseDirectory, "startup.log"), Path.Combine(tempDir, "startup.log"));
            CopyLogForExport(Path.Combine(appData, "save_debug.log"), Path.Combine(tempDir, "save_debug.log"));
            CopyLogForExport(Path.Combine(appData, "settings.json"), Path.Combine(tempDir, "settings.json"));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("导出时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("软件版本: " + Application.ProductVersion);
            sb.AppendLine("软件目录: " + AppContext.BaseDirectory);
            sb.AppendLine("操作系统: " + RuntimeInformation.OSDescription);
            sb.AppendLine("64 位系统: " + Environment.Is64BitOperatingSystem);
            sb.AppendLine(".NET: " + RuntimeInformation.FrameworkDescription);
            sb.AppendLine("处理器数: " + Environment.ProcessorCount);
            sb.AppendLine("深色模式: " + _settings.DarkMode);
            sb.AppendLine("动画效果: " + _settings.EnableAnimation);
            sb.AppendLine("窗口材质: " + _settings.BackdropType);
            sb.AppendLine("语言: " + _settings.Language);
            sb.AppendLine("字体大小: " + _settings.FontSize);
            sb.AppendLine("记住游戏列表: " + _settings.RememberGames);
            sb.AppendLine("启动弹窗: " + _settings.ShowStartupDialog);
            sb.AppendLine("选定显卡: " + (_settings.SelectedGpuName ?? ""));
            File.WriteAllText(Path.Combine(tempDir, "system_info.txt"), sb.ToString(), new UTF8Encoding(false));

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string zipPath = Path.Combine(desktop, "DLSS软件日志_" + stamp + ".zip");
            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);
            return zipPath;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); }
            catch { }
        }
    }

    private static void CopyLogForExport(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }
        if (new FileInfo(source).Length > 5 * 1024 * 1024)
        {
            string[] lines = File.ReadAllLines(source);
            string[] tail = lines.Skip(Math.Max(0, lines.Length - 1000)).ToArray();
            File.WriteAllLines(destination, tail, new UTF8Encoding(false));
            return;
        }
        File.Copy(source, destination, true);
    }

    private void ApplyConfigName(JsonElement root)
    {
        string exe = root.TryGetProperty("exe", out JsonElement x) ? (x.GetString() ?? "") : "";
        string name = root.TryGetProperty("name", out JsonElement n) ? (n.GetString() ?? "") : "";
        GameInfo? game = FindGame(exe);
        if (game == null)
        {
            PostStatus(Lang("找不到这个游戏", "Game not found"));
            return;
        }
        if (game.FrameGenMode != "高级模式")
        {
            PostStatus(Lang("配置名修改仅对高级模式开放", "Config name modify is only available for Advanced Mode"));
            return;
        }
        string[] allowed = new[]
        {
            "dinput8", "winmm", "d3d9", "d3d10", "d3d11", "d3d12",
            "dxgi", "dsound", "wininet", "winhttp", "binkw64", "bink2w64",
            "xinput1_1", "xinput1_2", "xinput1_3", "xinput1_4", "xinput9_1_0", "xinputuap"
        };
        if (!allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            PostStatus(Lang("配置名不合法", "Invalid config name"));
            return;
        }
        try
        {
            string exeDir = Path.GetDirectoryName(game.ExePath) ?? "";
            string versionDll = Path.Combine(exeDir, "version.dll");
            string newDll = Path.Combine(exeDir, name + ".dll");
            if (!File.Exists(versionDll))
            {
                PostStatus(Lang("未找到 version.dll，请先确认高级模式已正确安装", "version.dll not found. Confirm Advanced Mode is installed"));
                return;
            }
            if (name.Equals("version", StringComparison.OrdinalIgnoreCase) || File.Exists(newDll))
            {
                PostStatus(Lang("目标配置文件已存在，请勿更改配置名", "The target config file already exists"));
                return;
            }
            File.Move(versionDll, newDll);
            FilePatcher.RecordConfigNameRename(game, versionDll, newDll);
            PostStatus(Lang("已修改为 " + name + ".dll，请自行测试能否进入游戏", "Renamed to " + name + ".dll. Test whether the game launches"));
        }
        catch (Exception ex)
        {
            WriteLog("ConfigName FAILED: " + ex);
            PostStatus(Lang("配置名修改失败：" + ex.Message, "Failed to modify config name: " + ex.Message));
        }
    }

    private void SendTroubleshootFiles(JsonElement root)
    {
        string exe = root.TryGetProperty("exe", out JsonElement x) ? (x.GetString() ?? "") : "";
        GameInfo? game = FindGame(exe);
        if (game == null)
        {
            PostStatus(Lang("找不到这个游戏", "Game not found"));
            return;
        }
        if (!game.FrameGenEnabled)
        {
            PostStatus(Lang("请先开启多帧生成后再使用排错修复", "Enable frame generation before using troubleshoot repair"));
            return;
        }
        try
        {
            List<(string fileName, bool existsInGame)> files = FilePatcher.GetTroubleshootFiles(game);
            if (files.Count == 0)
            {
                PostStatus(Lang("没有找到可用的排错文件", "No troubleshoot files found"));
                return;
            }
            List<object> items = new List<object>();
            foreach ((string fileName, bool existsInGame) in files)
            {
                items.Add(new { name = fileName, exists = existsInGame });
            }
            PostJson(new { type = "troubleshootFiles", exe = game.ExePath, items });
        }
        catch (Exception ex)
        {
            WriteLog("TroubleshootFiles FAILED: " + ex);
            PostStatus(Lang("读取排错文件失败：" + ex.Message, "Failed to read troubleshoot files: " + ex.Message));
        }
    }

    private void RunTroubleshoot(JsonElement root)
    {
        string exe = root.TryGetProperty("exe", out JsonElement x) ? (x.GetString() ?? "") : "";
        GameInfo? game = FindGame(exe);
        if (game == null)
        {
            PostStatus(Lang("找不到这个游戏", "Game not found"));
            return;
        }
        if (!game.FrameGenEnabled)
        {
            PostStatus(Lang("请先开启多帧生成后再使用排错修复", "Enable frame generation before using troubleshoot repair"));
            return;
        }
        try
        {
            List<string> selected = new List<string>();
            if (root.TryGetProperty("files", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    string file = item.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(file) && Path.GetFileName(file) == file)
                    {
                        selected.Add(file);
                    }
                }
            }
            selected = selected.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (selected.Count == 0)
            {
                PostStatus(Lang("没有选择任何排错文件", "No troubleshoot files selected"));
                return;
            }
            int replaced = FilePatcher.TroubleshootSelected(game, selected);
            if (replaced > 0) PostSettings();
            PostStatus(replaced > 0
                ? Lang($"排错修复完成，已替换 {replaced} 个文件", $"Troubleshoot completed, replaced {replaced} file(s)")
                : Lang("排错修复完成，但没有文件被替换", "Troubleshoot completed, but no files were replaced"));
        }
        catch (Exception ex)
        {
            WriteLog("TroubleshootRun FAILED: " + ex);
            PostStatus(Lang("排错修复失败：" + ex.Message, "Troubleshoot repair failed: " + ex.Message));
        }
    }

    private void SaveRemark(JsonElement root)
    {
        string exe = root.TryGetProperty("exe", out JsonElement x) ? (x.GetString() ?? "") : "";
        GameInfo? game = FindGame(exe);
        if (game == null)
        {
            PostStatus(Lang("找不到这个游戏", "Game not found"));
            return;
        }
        string remark = root.TryGetProperty("remark", out JsonElement r) ? (r.GetString() ?? "") : "";
        string tags = root.TryGetProperty("tags", out JsonElement t) ? (t.GetString() ?? "") : "";
        game.Remark = remark.Trim();
        game.Tags = tags.Trim();
        SaveGames();
        PushGames();
        PostStatus(Lang("备注/标签已保存", "Remark/tags saved"));
    }

    private void ClearGames()
    {
        _games.Clear();
        SaveGames();
        PushGames();
        PostStatus("列表已清空");
    }

    private void RemoveGame(GameInfo game)
    {
        string name = GameDisplayName(game);
        _games.Remove(game);
        _iconCache.Remove(game.ExePath);
        SaveGames();
        PushGames();
        PostStatus(Lang("已从列表移除：" + name, "Removed from list: " + name));
    }

    // ---------------- Smooth Motion ----------------

    private bool _smoothBusy;

    private static string FormatDriverVersion(uint version)
    {
        if (version == 0)
        {
            return "未知";
        }
        return (version / 100).ToString() + "." + (version % 100).ToString("00");
    }

    private static int JsonInt(JsonElement root, string key, int fallback)
    {
        if (root.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
        {
            try
            {
                return value.GetInt32();
            }
            catch
            {
            }
        }
        return fallback;
    }

    private async Task RefreshSmoothMotionAsync()
    {
        if (_smoothBusy)
        {
            PostStatus(Lang("Smooth Motion 正在处理，请稍候", "Smooth Motion is busy, please wait"));
            return;
        }
        _smoothBusy = true;
        try
        {
            PostJson(new { type = "smoothLoading" });
            GpuInfo gpu = GpuDetector.DetectPrimaryGpu();
            SmoothMotionNvapi.ProbeResult probe = await Task.Run(() => SmoothMotionNvapi.Probe());

            List<SmoothMotionNvapi.GameState> states = new List<SmoothMotionNvapi.GameState>();
            if (probe.Available)
            {
                List<SmoothMotionNvapi.GameRef> refs = new List<SmoothMotionNvapi.GameRef>();
                foreach (GameInfo game in _games)
                {
                    refs.Add(new SmoothMotionNvapi.GameRef(game.ExePath, game.Name));
                }
                try
                {
                    states = await Task.Run(() => SmoothMotionNvapi.ReadGames(refs));
                }
                catch (Exception ex)
                {
                    probe.Error = ex.Message;
                }
            }

            SmoothMotionModService.ModPackageInfo package = SmoothMotionModService.GetPackageInfo();
            List<object> gameItems = new List<object>();
            int modCount = 0;
            for (int i = 0; i < _games.Count; i++)
            {
                GameInfo game = _games[i];
                SmoothMotionNvapi.GameState? state = i < states.Count ? states[i] : null;
                SmoothMotionModService.ModGameStatus modStatus = SmoothMotionModService.GetStatus(game.ExePath, gpu.Series);
                if (modStatus.Installed)
                {
                    modCount++;
                }
                gameItems.Add(new
                {
                    exe = game.ExePath,
                    name = GameDisplayName(game),
                    profile = state != null ? state.ProfileName : "",
                    profileFound = state != null && state.ProfileFound,
                    enabled = state != null && state.Enabled,
                    apiMask = state != null ? state.ApiMask : 7,
                    error = state != null ? state.Error : (probe.Error ?? ""),
                    modInstalled = modStatus.Installed,
                    modConflict = modStatus.Conflict,
                    modWrongEngine = modStatus.WrongEngine,
                    modEngine = modStatus.Engine,
                    modExpectedEngine = modStatus.ExpectedEngine,
                    modMessage = modStatus.Message,
                    modDll = modStatus.DllPath
                });
            }

            PostJson(new
            {
                type = "smoothState",
                gpu = new
                {
                    name = gpu.Name,
                    series = gpu.Series,
                    isNvidia = gpu.IsNvidia,
                    official = gpu.Series is "RTX40" or "RTX50",
                    needsMod = gpu.Series is "RTX20" or "RTX30"
                },
                driver = new
                {
                    version = FormatDriverVersion(probe.DriverVersion),
                    branch = probe.Branch,
                    available = probe.Available,
                    supported = probe.SmoothMotionSupported,
                    baseEnabled = probe.BaseEnabled,
                    error = probe.Error
                },
                games = gameItems,
                mod = new
                {
                    ready = package.Ready,
                    anyReady = package.AnyReady,
                    rtx20Ready = package.Rtx20Ready,
                    rtx30Ready = package.Rtx30Ready,
                    rtx20Size = package.Rtx20Size,
                    rtx30Size = package.Rtx30Size,
                    rtx20Sha256 = package.Rtx20Sha256,
                    rtx30Sha256 = package.Rtx30Sha256,
                    rtx20Path = package.Rtx20Path,
                    rtx30Path = package.Rtx30Path,
                    supported = gpu.Series is "RTX20" or "RTX30",
                    engine = SmoothMotionModService.EngineForGpu(gpu.Series),
                    error = package.Error,
                    installedCount = modCount
                }
            });
        }
        catch (Exception ex)
        {
            PostJson(new { type = "smoothError", message = ex.Message });
        }
        finally
        {
            _smoothBusy = false;
        }
    }

    private static bool IsProcessElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 用管理员权限启动同一 EXE 的一次性写入模式。
    /// 返回 null 表示用户在 UAC 里点了“否”；返回结果对象表示管理员进程已经执行完。
    /// </summary>
    private async Task<SmoothMotionNvapi.ApplyResult?> RunElevatedSmoothApplyAsync(
        GameInfo game,
        bool enabled,
        int apiMask)
    {
        string resultPath = Path.Combine(
            Path.GetTempPath(),
            "dfge-smooth-" + Guid.NewGuid().ToString("N") + ".json");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments =
                SmoothMotionElevated.ApplySwitch +
                " --exe \"" + game.ExePath.Replace("\"", "\\\"") + "\"" +
                " --name \"" + (game.Name ?? "").Replace("\"", "\\\"") + "\"" +
                " --enabled " + (enabled ? "1" : "0") +
                " --apis " + apiMask +
                " --result \"" + resultPath.Replace("\"", "\\\"") + "\""
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return new SmoothMotionNvapi.ApplyResult
                {
                    Success = false,
                    Enabled = enabled,
                    ApiMask = apiMask,
                    Error = "无法启动管理员权限进程"
                };
            }

            await process.WaitForExitAsync();
            if (!File.Exists(resultPath))
            {
                return new SmoothMotionNvapi.ApplyResult
                {
                    Success = false,
                    Enabled = enabled,
                    ApiMask = apiMask,
                    Error = "管理员权限进程没有返回结果"
                };
            }

            string json = await File.ReadAllTextAsync(resultPath);
            SmoothMotionNvapi.ApplyResult? result = JsonSerializer.Deserialize<SmoothMotionNvapi.ApplyResult>(
                json,
                new JsonSerializerOptions { IncludeFields = true });
            return result ?? new SmoothMotionNvapi.ApplyResult
            {
                Success = false,
                Enabled = enabled,
                ApiMask = apiMask,
                Error = "管理员权限进程返回了空结果"
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED：用户在 UAC 提示里点了“否”。
            return null;
        }
        catch (Exception ex)
        {
            return new SmoothMotionNvapi.ApplyResult
            {
                Success = false,
                Enabled = enabled,
                ApiMask = apiMask,
                Error = ex.Message
            };
        }
        finally
        {
            try
            {
                if (File.Exists(resultPath))
                {
                    File.Delete(resultPath);
                }
            }
            catch
            {
            }
        }
    }

    private async Task<SmoothMotionNvapi.ApplyResult?> RunElevatedSmoothResetAsync(GameInfo game)
    {
        string resultPath = Path.Combine(
            Path.GetTempPath(),
            "dfge-smooth-" + Guid.NewGuid().ToString("N") + ".json");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments =
                SmoothMotionElevated.ResetSwitch +
                " --exe \"" + game.ExePath.Replace("\"", "\\\"") + "\"" +
                " --name \"" + (game.Name ?? "").Replace("\"", "\\\"") + "\"" +
                " --result \"" + resultPath.Replace("\"", "\\\"") + "\""
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return new SmoothMotionNvapi.ApplyResult
                {
                    Success = false,
                    Error = "无法启动管理员权限进程"
                };
            }

            await process.WaitForExitAsync();
            if (!File.Exists(resultPath))
            {
                return new SmoothMotionNvapi.ApplyResult
                {
                    Success = false,
                    Error = "管理员权限进程没有返回结果"
                };
            }

            string json = await File.ReadAllTextAsync(resultPath);
            SmoothMotionNvapi.ApplyResult? result = JsonSerializer.Deserialize<SmoothMotionNvapi.ApplyResult>(
                json,
                new JsonSerializerOptions { IncludeFields = true });
            return result ?? new SmoothMotionNvapi.ApplyResult
            {
                Success = false,
                Error = "管理员权限进程返回了空结果"
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return null;
        }
        catch (Exception ex)
        {
            return new SmoothMotionNvapi.ApplyResult
            {
                Success = false,
                Error = ex.Message
            };
        }
        finally
        {
            try
            {
                if (File.Exists(resultPath))
                {
                    File.Delete(resultPath);
                }
            }
            catch
            {
            }
        }
    }

    private async Task ApplySmoothMotionAsync(JsonElement root)
    {
        if (_smoothBusy)
        {
            PostStatus(Lang("Smooth Motion 正在处理，请稍候", "Smooth Motion is busy, please wait"));
            return;
        }

        string exe = root.TryGetProperty("exe", out JsonElement exeElement) ? (exeElement.GetString() ?? "") : "";
        bool enabled = root.TryGetProperty("enabled", out JsonElement enabledElement) &&
            (enabledElement.ValueKind == JsonValueKind.True || enabledElement.ValueKind == JsonValueKind.False) &&
            enabledElement.GetBoolean();
        int apiMask = JsonInt(root, "apis", 7);
        if (apiMask <= 0)
        {
            apiMask = 7;
        }

        GameInfo? game = null;
        foreach (GameInfo item in _games)
        {
            if (Norm(item.ExePath) == Norm(exe))
            {
                game = item;
                break;
            }
        }
        if (game == null)
        {
            PostStatus(Lang("找不到这个游戏，请刷新列表后重试", "Game not found, refresh the list and try again"));
            return;
        }

        _smoothBusy = true;
        SmoothMotionNvapi.ApplyResult result;
        try
        {
            // Enabled APIs 是驱动里的受保护设置，普通权限写会返回 -137。
            // 默认值 7（全开）本来就不写这一项；只有单 API 选择才需要临时提权。
            if (enabled && apiMask != 7 && !IsProcessElevated())
            {
                SmoothMotionNvapi.ApplyResult? elevatedResult =
                    await RunElevatedSmoothApplyAsync(game, enabled, apiMask);
                if (elevatedResult == null)
                {
                    PostJson(new
                    {
                        type = "smoothError",
                        message = Lang(
                            "写入 APIs 需要管理员权限，已取消。请重新选择并在 UAC 提示里点击“是”。",
                            "Administrator permission is required to write Enabled APIs. Select again and choose Yes in the UAC prompt."),
                        exe = game.ExePath
                    });
                    PostStatus(Lang(
                        "Smooth Motion 未写入：需要管理员权限",
                        "Smooth Motion not written: administrator permission required"));
                    return;
                }
                result = elevatedResult;
            }
            else
            {
                result = await Task.Run(() => SmoothMotionNvapi.Apply(game.ExePath, game.Name, enabled, apiMask));
            }
        }
        catch (Exception ex)
        {
            result = new SmoothMotionNvapi.ApplyResult { Error = ex.Message };
        }
        finally
        {
            _smoothBusy = false;
        }

        if (result.Success)
        {
            string message = enabled
                ? Lang("Smooth Motion 已开启：" + result.ProfileName, "Smooth Motion enabled: " + result.ProfileName)
                : Lang("Smooth Motion 已关闭：" + result.ProfileName, "Smooth Motion disabled: " + result.ProfileName);
            if (!string.IsNullOrWhiteSpace(result.Warning))
            {
                message += " · " + result.Warning;
            }
            PostStatus(message);
            await RefreshSmoothMotionAsync();
        }
        else
        {
            string error = result.Error ?? "";
            if (error.Contains("-175"))
            {
                error += Lang("。请右键以管理员身份运行本软件后重试。", ". Right-click the app and run as administrator, then retry.");
            }
            else if (error.Contains("-137"))
            {
                error += Lang("。写入 Enabled APIs 需要管理员权限。", ". Administrator permission is required to write Enabled APIs.");
            }
            PostJson(new { type = "smoothError", message = error, exe = game.ExePath });
            PostStatus(Lang("Smooth Motion 写入失败：" + error, "Smooth Motion write failed: " + error));
        }
    }

    private async Task ResetSmoothMotionAsync(JsonElement root)
    {
        if (_smoothBusy)
        {
            PostStatus(Lang("Smooth Motion 正在处理，请稍候", "Smooth Motion is busy, please wait"));
            return;
        }

        string exe = root.TryGetProperty("exe", out JsonElement exeElement) ? (exeElement.GetString() ?? "") : "";
        GameInfo? game = null;
        foreach (GameInfo item in _games)
        {
            if (Norm(item.ExePath) == Norm(exe))
            {
                game = item;
                break;
            }
        }
        if (game == null)
        {
            PostStatus(Lang("找不到这个游戏，请刷新列表后重试", "Game not found, refresh the list and try again"));
            return;
        }

        _smoothBusy = true;
        SmoothMotionNvapi.ApplyResult result;
        try
        {
            result = await Task.Run(() => SmoothMotionNvapi.Reset(game.ExePath, game.Name));

            // 删除 Enabled APIs 同样可能因为受保护设置返回 -137；普通权限先试一次，
            // 只有确实被权限挡住时才再启动一次管理员进程，避免每次恢复默认都弹 UAC。
            if (!IsProcessElevated() &&
                !string.IsNullOrWhiteSpace(result.Warning) &&
                (result.Warning.Contains("-137") || result.Warning.Contains("管理员权限")))
            {
                SmoothMotionNvapi.ApplyResult? elevatedResult =
                    await RunElevatedSmoothResetAsync(game);
                if (elevatedResult == null)
                {
                    PostJson(new
                    {
                        type = "smoothError",
                        message = Lang(
                            "恢复 Enabled APIs 需要管理员权限，已取消。请重新操作并在 UAC 提示里点击“是”。",
                            "Administrator permission is required to reset Enabled APIs. Try again and choose Yes in the UAC prompt."),
                        exe = game.ExePath
                    });
                    PostStatus(Lang(
                        "Smooth Motion 未完全恢复：需要管理员权限",
                        "Smooth Motion not fully reset: administrator permission required"));
                    return;
                }

                result = elevatedResult;
            }
        }
        catch (Exception ex)
        {
            result = new SmoothMotionNvapi.ApplyResult { Error = ex.Message };
        }
        finally
        {
            _smoothBusy = false;
        }

        if (result.Success)
        {
            PostStatus(Lang(
                string.IsNullOrWhiteSpace(result.Warning) ? "已恢复 Smooth Motion 默认" : result.Warning,
                string.IsNullOrWhiteSpace(result.Warning) ? "Smooth Motion restored to default" : result.Warning));
            await RefreshSmoothMotionAsync();
        }
        else
        {
            PostJson(new { type = "smoothError", message = result.Error, exe = game.ExePath });
            PostStatus(Lang("恢复默认失败：" + result.Error, "Restore failed: " + result.Error));
        }
    }

    private async Task DeploySmoothMotionModAsync(JsonElement root)
    {
        string exe = root.TryGetProperty("exe", out JsonElement exeElement) ? (exeElement.GetString() ?? "") : "";
        GameInfo? game = null;
        foreach (GameInfo item in _games)
        {
            if (Norm(item.ExePath) == Norm(exe))
            {
                game = item;
                break;
            }
        }
        if (game == null)
        {
            PostStatus(Lang("找不到这个游戏，请刷新列表后重试", "Game not found, refresh the list and try again"));
            return;
        }

        GpuInfo gpu = GpuDetector.DetectPrimaryGpu();
        string message = SmoothMotionModService.Deploy(game.ExePath, gpu.Series);
        PostStatus(message);
        await RefreshSmoothMotionAsync();
    }

    private async Task RestoreSmoothMotionModAsync(JsonElement root)
    {
        string exe = root.TryGetProperty("exe", out JsonElement exeElement) ? (exeElement.GetString() ?? "") : "";
        GameInfo? game = null;
        foreach (GameInfo item in _games)
        {
            if (Norm(item.ExePath) == Norm(exe))
            {
                game = item;
                break;
            }
        }
        if (game == null)
        {
            PostStatus(Lang("找不到这个游戏，请刷新列表后重试", "Game not found, refresh the list and try again"));
            return;
        }

        string message = SmoothMotionModService.Restore(game.ExePath);
        PostStatus(message);
        await RefreshSmoothMotionAsync();
    }

    private void OpenSmoothMotionLog()
    {
        string path = @"C:\NvPresent";
        if (!Directory.Exists(path))
        {
            PostStatus(Lang(
                "日志目录还不存在。先在游戏里开启 Smooth Motion 并运行一次，驱动才会创建 C:\\NvPresent。",
                "The log folder does not exist yet. Enable Smooth Motion and run the game once; the driver will create C:\\NvPresent."));
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            PostStatus(Lang("打开日志目录失败：" + ex.Message, "Failed to open log folder: " + ex.Message));
        }
    }

    private void OpenSmoothMotionModFolder()
    {
        string path = SmoothMotionModService.PatchRoot;
        if (!Directory.Exists(path))
        {
            PostStatus(Lang(
                "内置补丁目录不存在：" + path + "。请重新安装完整软件包。",
                "Built-in patch folder does not exist: " + path + ". Please reinstall the complete package."));
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            PostStatus(Lang("打开补丁目录失败：" + ex.Message, "Failed to open patch folder: " + ex.Message));
        }
    }

    private void OpenSmoothMotionModLog()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string rtxmfg = Path.Combine(localAppData, "RTXMFG");
        string path = Directory.Exists(rtxmfg) ? rtxmfg : @"C:\NvPresent";
        if (!Directory.Exists(path))
        {
            PostStatus(Lang(
                "还没有 Mod 日志目录。先部署 RTX30 补丁并运行一次游戏；RTX30 会在 %LOCALAPPDATA%\\RTXMFG 创建日志，官方驱动日志在 C:\\NvPresent。",
                "No Mod log folder yet. Deploy the RTX30 patch and run the game once; RTX30 creates logs in %LOCALAPPDATA%\\RTXMFG, official driver logs are in C:\\NvPresent."));
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            PostStatus(Lang("打开 Mod 日志目录失败：" + ex.Message, "Failed to open Mod log folder: " + ex.Message));
        }
    }

    // ---------------- 推送到前端 ----------------

    private static string GameDisplayName(GameInfo game)
    {
        if (!Translator.IsEnglish)
        {
            return game.Name;
        }
        string? fromDb = GameNameDatabase.GetGameName(game.ExePath, true);
        if (!string.IsNullOrWhiteSpace(fromDb))
        {
            return fromDb;
        }
        string root = Path.GetDirectoryName(game.ExePath) ?? "";
        return GameNameDatabase.EnglishFallback(game.Name, game.ExePath, root);
    }

    private void PushGames()
    {
        List<object> items = new List<object>();
        foreach (GameInfo game in _games)
        {
            bool frameGen = game.FrameGenEnabled;
            bool dlss5 = game.Dlss5Enabled;
            bool dx9 = game.Dx9Dlss5Enabled;

            string status;
            string kind;
            if (frameGen && dlss5)
            {
                status = Lang("已开启（多帧 + DLSS5）", "Enabled (Frame Gen + DLSS5)");
                kind = "on";
            }
            else if (frameGen)
            {
                status = Lang("已开启（多帧生成）", "Enabled (Frame Gen)");
                kind = "on";
            }
            else if (dlss5)
            {
                status = Lang("已开启（DLSS5）", "Enabled (DLSS5)");
                kind = "on";
            }
            else if (dx9)
            {
                status = Lang("已开启（DX9 DLSS5）", "Enabled (DX9 DLSS5)");
                kind = "on";
            }
            else
            {
                status = Lang("未开启", "Not Enabled");
                kind = "off";
            }

            string exeDirForSm86 = Path.GetDirectoryName(game.ExePath) ?? "";
            bool sm86 = exeDirForSm86.Length > 0 && File.Exists(SM86IniConfig.GetPath(exeDirForSm86));

            List<string> tagsList = new List<string>();
            if (!string.IsNullOrWhiteSpace(game.Remark))
            {
                tagsList.Add("\uD83D\uDCDD " + game.Remark);
            }
            if (!string.IsNullOrWhiteSpace(game.Tags))
            {
                tagsList.Add("\uD83C\uDFF7\uFE0F " + game.Tags);
            }

            items.Add(new
            {
                name = GameDisplayName(game),
                exe = game.ExePath,
                gamePath = game.GamePath,
                status,
                kind,
                icon = GetIcon(game.ExePath),
                poster = GamePoster.GetPoster(game),
                tags = string.Join("  |  ", tagsList),
                sm86,
                supportsFrameGen = game.SupportsFrameGen,
                frameOn = frameGen,
                dlssOn = dlss5,
                frameMode = game.FrameGenMode ?? "",
                remark = game.Remark ?? "",
                tagList = game.Tags ?? "",
                gitc = game.GitcVersion,
                gpu = game.GpuSeries
            });
        }

        PostJson(new { type = "games", total = items.Count, items });
    }

    private static readonly Dictionary<string, string> StatusEn = new Dictionary<string, string>
    {
        ["还有操作正在进行，请稍候再试"] = "Another operation is in progress, please wait.",
        ["找不到这个游戏，请刷新状态后重试"] = "Game not found, refresh the status and try again.",
        ["这个游戏不支持多帧生成：目录里没有检测到 DLSS / 帧生成特征"] = "This game does not support frame generation: no DLSS / frame-gen signature found.",
        ["多帧生成已还原"] = "Frame generation restored.",
        ["还原失败，可能没有找到备份记录"] = "Restore failed, no backup record found.",
        ["经典多帧生成安装完成"] = "Classic frame generation installed.",
        ["安装失败，请检查游戏目录权限"] = "Installation failed, check folder permissions.",
        ["MFG 多帧生成安装完成"] = "MFG frame generation installed.",
        ["SM86 多帧生成安装完成，可在下方「配置多帧生成」调整参数"] = "SM86 frame generation installed. Use \"Configure\" below to tune it.",
        ["DLSS5 已还原"] = "DLSS5 restored.",
        ["DLSS NR 模型安装失败"] = "Failed to install the DLSS NR model.",
        ["已还原，状态已重新检测"] = "Restored, status re-checked.",
        ["添加失败，这个 exe 没能识别成游戏"] = "Failed to add: this exe was not recognized as a game.",
        ["这个游戏已经在列表里了"] = "This game is already in the list.",
        ["日志文件还不存在，先运行一次再来看"] = "Log file does not exist yet, run the app once first.",
        ["找不到这个游戏"] = "Game not found.",
        ["找不到 dlssg_sm86.ini，请先重新安装 SM86 方案"] = "dlssg_sm86.ini not found, reinstall the SM86 scheme first.",
        ["SM86 配置已保存，请完全退出游戏后重新启动"] = "SM86 configuration saved. Fully exit the game and relaunch.",
        ["已恢复出厂 dlssg_sm86.ini，请完全退出游戏后重新启动"] = "Factory dlssg_sm86.ini restored. Fully exit the game and relaunch.",
        ["未找到出厂 ini 文件，请重新安装 SM86 方案"] = "Factory ini not found, reinstall the SM86 scheme.",
        ["正在扫描 Steam / Epic / GOG / Ubisoft 游戏库…"] = "Scanning Steam / Epic / GOG / Ubisoft libraries…",
        ["正在刷新补丁状态…"] = "Refreshing patch status…",
        ["没能在这个文件夹里识别出游戏 exe"] = "No game exe was recognized in this folder.",
        ["没能识别这个 exe"] = "This exe was not recognized.",
        ["列表已清空"] = "List cleared."
    };

    private void PostStatus(string text)
    {
        PostJson(new { type = "status", text = Translator.IsEnglish ? TranslateStatus(text) : text });
    }

    private static string TranslateStatus(string text)
    {
        if (StatusEn.TryGetValue(text, out string? exact))
        {
            return exact;
        }
        if (text.StartsWith("已添加："))
        {
            return "Added: " + text.Substring(4);
        }
        if (text.StartsWith("操作失败："))
        {
            return "Operation failed: " + text.Substring(5);
        }
        if (text.StartsWith("扫描失败："))
        {
            return "Scan failed: " + text.Substring(5);
        }
        if (text.StartsWith("检测失败："))
        {
            return "Detection failed: " + text.Substring(5);
        }
        if (text.StartsWith("基础文件安装失败："))
        {
            return "Failed to install base files: " + text.Substring(9);
        }
        if (text.StartsWith("切换代理 DLL 失败："))
        {
            return "Failed to switch proxy DLL: " + text.Substring(12);
        }
        if (text.StartsWith("保存失败："))
        {
            return "Save failed: " + text.Substring(5);
        }
        if (text.StartsWith("打开失败："))
        {
            return "Failed to open: " + text.Substring(5);
        }
        if (text.StartsWith("路径不存在："))
        {
            return "Path not found: " + text.Substring(6);
        }
        if (text.StartsWith("扫描完成："))
        {
            Match scan = Regex.Match(text, @"扫描完成：新增 (\d+) 个，列表共 (\d+) 个游戏");
            if (scan.Success)
            {
                return "Scan complete: " + scan.Groups[1].Value + " new, " + scan.Groups[2].Value + " games in total.";
            }
            return "Scan complete.";
        }
        if (text.StartsWith("状态已刷新"))
        {
            Match refresh = Regex.Match(text, @"状态已刷新（共 (\d+) 个游戏）");
            if (refresh.Success)
            {
                return "Status refreshed (" + refresh.Groups[1].Value + " games).";
            }
            return "Status refreshed.";
        }
        if (text.StartsWith("GITC DLSS5 ") && text.Contains("安装成功"))
        {
            return "GITC DLSS5 installed. Press Home in game and enable NR manually (off by default).";
        }
        return text;
    }

    private string GetIcon(string exe)
    {
        if (string.IsNullOrWhiteSpace(exe))
        {
            return "";
        }
        if (_iconCache.TryGetValue(exe, out string? cached))
        {
            return cached;
        }

        string data = IconExtractor.GetDataUrl(exe);
        _iconCache[exe] = data;
        return data;
    }

    // ---------------- 窗口 ----------------

    private async Task ScanThenSnapAsync()
    {
        await RunScanAsync();
        await SnapAsync();
    }

    private async Task SnapAsync()
    {
        try
        {
            await Task.Delay(8000);
            string path = Path.Combine(AppContext.BaseDirectory, "预览_主界面.png");
            using (FileStream fs = File.Create(path))
            {
                await _web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
            }
            WriteLog("snapshot saved: " + path);
        }
        catch (Exception ex)
        {
            WriteLog("Snapshot FAILED: " + ex.Message);
        }
        Close();
    }

    private void BeginDragMove()
    {
        if (_windowMaximized || _windowAnimating || WindowState == FormWindowState.Maximized)
        {
            return;
        }
        WriteLog("drag start");
        _ = ReleaseCapture();
        _ = SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
    }

    private void ToggleMaximize()
    {
        if (_windowAnimating)
        {
            return;
        }

        if (!_settings.EnableAnimation)
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
                _windowMaximized = false;
                return;
            }
            if (_windowMaximized)
            {
                Bounds = _windowRestoreBounds;
                _windowMaximized = false;
            }
            else
            {
                _windowRestoreBounds = Bounds;
                Bounds = Screen.FromControl(this).WorkingArea;
                _windowMaximized = true;
            }
            return;
        }

        if (WindowState == FormWindowState.Maximized)
        {
            _windowMaximized = true;
            _windowRestoreBounds = RestoreBounds;
        }

        if (_windowMaximized)
        {
            AnimateWindowTo(_windowRestoreBounds, false);
        }
        else
        {
            _windowRestoreBounds = Bounds;
            AnimateWindowTo(Screen.FromControl(this).WorkingArea, true);
        }
    }

    private void AnimateWindowTo(Rectangle target, bool toMaximized)
    {
        if (WindowState != FormWindowState.Normal)
        {
            WindowState = FormWindowState.Normal;
        }

        if (Bounds == target)
        {
            _windowMaximized = toMaximized;
            return;
        }

        _windowAnimFrom = Bounds;
        _windowAnimTo = target;
        _windowAnimToMax = toMaximized;
        _windowAnimStart = DateTime.UtcNow;
        _windowAnimating = true;
        _windowAnimTimer.Start();
    }

    private void WindowAnimTimer_Tick(object? sender, EventArgs e)
    {
        double progress = Math.Min(1.0, (DateTime.UtcNow - _windowAnimStart).TotalMilliseconds / 190.0);
        double eased = 1 - Math.Pow(1 - progress, 3);
        Bounds = new Rectangle(
            (int)Math.Round(_windowAnimFrom.X + (_windowAnimTo.X - _windowAnimFrom.X) * eased),
            (int)Math.Round(_windowAnimFrom.Y + (_windowAnimTo.Y - _windowAnimFrom.Y) * eased),
            (int)Math.Round(_windowAnimFrom.Width + (_windowAnimTo.Width - _windowAnimFrom.Width) * eased),
            (int)Math.Round(_windowAnimFrom.Height + (_windowAnimTo.Height - _windowAnimFrom.Height) * eased));

        if (progress >= 1.0)
        {
            _windowAnimTimer.Stop();
            Bounds = _windowAnimTo;
            _windowMaximized = _windowAnimToMax;
            _windowAnimating = false;
        }
    }


    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try
        {
            DownloadJob? job = _downloadJob;
            if (job != null)
            {
                job.Cancelled = true;
                job.ReadCts?.Cancel();
            }
        }
        catch
        {
        }
        try { _windowAnimTimer.Stop(); _windowAnimTimer.Dispose(); } catch { }
        try
        {
            _web.Dispose();
        }
        catch
        {
        }
        base.OnFormClosed(e);
    }

    internal static void WriteStartupLog(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "startup.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n",
                new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "error.log"),
                $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] {message}\n");
        }
        catch
        {
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
