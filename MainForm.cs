using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Text.Json;
using DLSSFrameGenEnabler.Models;
using DLSSFrameGenEnabler.Services;
using DLSSFrameGenEnabler.UI;

namespace DLSSFrameGenEnabler;

/// <summary>
/// 主窗体 - DLSS多帧生成开启工具
/// 现代化 WinUI3 风格深色主题界面
/// </summary>
public class MainForm : Form
{
    #region 版本与更新配置

    /// <summary>当前软件版本号</summary>
    public const string CurrentVersion = "1.10.3.2";
    public const string DisplayVersion = "1.10.3.2";

    /// <summary>
    /// 版本信息文件URL（JSON格式）。
    /// 请将此文件托管在可公开访问的地方（如GitHub Gist、码云等），
    /// 每次发布新版本时更新此文件中的版本号和下载链接。
    /// JSON格式：
    /// {
    ///   "version": "1.8.0",
    ///   "releaseDate": "2026-09-06",
    ///   "changelog": "更新说明",
    ///   "downloadUrlKuake": "夸克网盘链接",
    ///   "downloadUrlBaidu": "百度网盘链接",
    ///   "downloadUrlLanzou": "蓝奏云链接"
    /// }
    /// </summary>
    private const string VersionInfoUrl = "https://gist.githubusercontent.com/QSXK1314/a9595f510bc16c77051ee386e085f8f1/raw/3c40d9ee97cd4474586fa0570542eef59c6335e1/version.json";

    #endregion
    #region 颜色主题（WinUI3 / Fluent Design 风格，支持深色/浅色切换）

    /// <summary>当前主题：Dark / Light</summary>
    private string _currentTheme = "Dark";

    // WinUI3 风格颜色
    // 深色模式（Windows 11 深色主题）
    private Color BgColor { get; set; } = Color.FromArgb(32, 32, 32);       // 窗口背景（深灰）
    private Color NavBgColor { get; set; } = Color.FromArgb(28, 28, 28);     // 导航栏背景
    private Color CardBgColor { get; set; } = Color.FromArgb(43, 43, 43);    // 卡片背景
    private Color CardHoverColor { get; set; } = Color.FromArgb(50, 50, 50);  // 卡片hover
    private Color NavItemHover { get; set; } = Color.FromArgb(50, 50, 50);    // 导航项hover
    private Color NavItemSelected { get; set; } = Color.FromArgb(0, 120, 212); // 导航项选中（蓝色）
    private Color AccentColor { get; set; } = Color.FromArgb(0, 120, 212);     // 强调色（WinUI蓝）
    private Color AccentHover { get; set; } = Color.FromArgb(20, 130, 220);    // 强调色hover
    private Color SuccessColor { get; set; } = Color.FromArgb(16, 185, 129);   // 成功绿
    private Color WarningColor { get; set; } = Color.FromArgb(245, 158, 11);   // 警告橙
    private Color DangerColor { get; set; } = Color.FromArgb(220, 50, 50);     // 危险红
    private Color TextPrimary { get; set; } = Color.FromArgb(255, 255, 255);   // 主要文字
    private Color TextSecondary { get; set; } = Color.FromArgb(200, 200, 200); // 次要文字
    private Color TextTertiary { get; set; } = Color.FromArgb(150, 150, 150);  // 三级文字
    private Color BorderColor { get; set; } = Color.FromArgb(60, 60, 60);       // 边框色
    private Color DividerColor { get; set; } = Color.FromArgb(55, 55, 55);      // 分割线

    #endregion

    #region 控件字段（WinUI3风格：左侧导航栏 + 右侧内容区）

    // 左侧导航栏
    private Panel _navPanel = null!;
    private Panel _navHeader = null!;
    private PictureBox _navIcon = null!;
    private Label _navAppName = null!;
    private Label _navAppVersion = null!;
    private Panel _navItemsPanel = null!;
    private List<NavItem> _navItems = new();

    // 右侧内容区
    private Panel _contentPanel = null!;
    private Panel _contentHeader = null!;
    private Label _pageTitle = null!;
    private Label _pageSubtitle = null!;
    private FlowLayoutPanel _pageToolbar = null!;

    // 主页内容（游戏列表）
    private Panel _homePage = null!;
    private DataGridView _gameGridView = null!;
    private ContextMenuStrip _gameContextMenu = null!;
    private Panel _detailPanel = null!;
    private Label _detailGameName = null!;
    private Label _detailStatus = null!;
    private Label _detailGamePath = null!;
    private Label _detailExePath = null!;
    private ModernButton _applyButton = null!;
    private ModernButton _restoreButton = null!;
    private ModernButton _configButton = null!;
    private ModernButton _troubleshootButton = null!;
    private ModernButton _cyberpunkButton = null!;
    private ModernButton _configNameButton = null!;
    private ModernButton _dlss5Button = null!;
    private ModernButton _dx9DLSS5Button = null!;
    private Label _emptyHintLabel = null!;

    // 工具栏按钮（顶部，仅主页显示）
    private ModernButton _scanButton = null!;
    private ModernButton _manualButton = null!;
    private ModernButton _refreshButton = null!;

    // 设置页面
    private Panel _settingsPage = null!;
    private Panel _settingsCard = null!;
    private Label _settingsTitle = null!;
    private ModernButton _settingsThemeButton = null!;

    // 使用说明页面
    private Panel _helpPage = null!;
    private RichTextBox _helpTextBox = null!;

    // 更新内容页面
    private Panel _changelogPage = null!;
    private Panel _changelogToolbar = null!;
    private Panel _changelogCard = null!;
    private RichTextBox _changelogTextBox = null!;
    private ModernButton _changelogCheckUpdateButton = null!;

    // 关于页面
    private Panel _aboutPage = null!;
    private Panel _aboutCard = null!;
    private Label _aboutAppName = null!;
    private Label _aboutVersion = null!;
    private Label _aboutDescription = null!;
    private Label _aboutAuthorLabel = null!;
    private Panel _aboutLinksPanel = null!;
    private PictureBox _githubIcon = null!;
    private PictureBox _bilibiliIcon = null!;
    private PictureBox _xiaoheiheIcon = null!;
    private Label _githubLink = null!;
    private Label _bilibiliLink = null!;
    private Label _xiaoheiheLink = null!;
    private Label _aboutCreditsLabel = null!;
    private Label _aboutCreditsText = null!;

    // 免责声明栏
    private Panel _disclaimerPanel = null!;
    private Label _disclaimerLabel = null!;

    // 状态栏
    private Panel _statusBar = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;

    #endregion

    /// <summary>导航项数据类</summary>
    private class NavItem
    {
        public string Key { get; set; } = "";
        public string Text { get; set; } = "";
        public Panel Panel { get; set; } = null!;
        public Label IconLabel { get; set; } = null!;
        public Label TextLabel { get; set; } = null!;
        public bool IsSelected { get; set; }
    }

    private readonly GameScanner _scanner = new();
    private readonly FilePatcher _patcher = new();
    private List<GameInfo> _games = new();
    private GameInfo? _selectedGame;
    private bool _isBusy;

    public MainForm()
    {
        InitializeComponent();
        CheckPatchFilesOnLoad();
        // 启动时自动检查更新（不显示"已是最新"提示，只在有更新时弹窗）
        _ = CheckForUpdatesAsync(showNoUpdate: false);
    }

    #region 界面初始化

    private void InitializeComponent()
    {
        // ===== 窗体设置 =====
        Text = $"多帧生成+DLSS5开启工具 V{DisplayVersion}";
        Size = new Size(1100, 720);
        MinimumSize = new Size(900, 600);
        BackColor = BgColor;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI Variable Text", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        // 设置窗口图标
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }

        // ===== 左侧导航栏 =====
        _navPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 260,
            BackColor = NavBgColor
        };

        // 导航栏顶部（应用信息）
        _navHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = NavBgColor
        };

        _navIcon = new PictureBox
        {
            Size = new Size(36, 36),
            Location = new Point(16, 20),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            _navIcon.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap();
        }
        catch { }

        _navAppName = new Label
        {
            Text = "多帧生成+DLSS5",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(60, 18)
        };

        _navAppVersion = new Label
        {
            Text = $"V{DisplayVersion}",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = true,
            Location = new Point(60, 40)
        };

        _navHeader.Controls.Add(_navIcon);
        _navHeader.Controls.Add(_navAppName);
        _navHeader.Controls.Add(_navAppVersion);

        // 导航项面板
        _navItemsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = NavBgColor,
            Padding = new Padding(8, 8, 8, 8),
            AutoScroll = true
        };

        // 创建导航项
        CreateNavItem("home", "🏠  主页", 0);
        CreateNavItem("settings", "⚙️  设置", 1);
        CreateNavItem("help", "📖  使用说明", 2);
        CreateNavItem("changelog", "📝  更新内容", 3);
        CreateNavItem("about", "ℹ️  关于", 4);

        // 默认选中主页
        SelectNavItem("home");

        _navPanel.Controls.Add(_navItemsPanel);
        _navPanel.Controls.Add(_navHeader);

        // ===== 右侧内容区 =====
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor
        };

        // 内容区顶部（页面标题+工具栏，工具栏仅主页显示）
        _contentHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = BgColor
        };

        _pageTitle = new Label
        {
            Text = "主页",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 18f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 12)
        };

        _pageSubtitle = new Label
        {
            Text = "管理游戏的多帧生成和DLSS5补丁",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(26, 42)
        };

        // 工具栏（右上角，仅主页显示）
        _pageToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 360,
            BackColor = BgColor,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(0, 16, 0, 0),
            Visible = true
        };

        _refreshButton = new ModernButton
        {
            Text = "刷新状态",
            IsSecondary = true,
            Size = new Size(100, 36),
            Margin = new Padding(6, 0, 0, 0)
        };
        _refreshButton.Click += (_, _) => RefreshGameStatus();

        _manualButton = new ModernButton
        {
            Text = "手动添加",
            IsSecondary = true,
            Size = new Size(100, 36),
            Margin = new Padding(6, 0, 0, 0)
        };
        _manualButton.Click += (_, _) => ShowManualAddMenu();

        _scanButton = new ModernButton
        {
            Text = "自动扫描",
            AccentColor = AccentColor,
            HoverColor = AccentHover,
            Size = new Size(100, 36),
            Margin = new Padding(6, 0, 0, 0)
        };
        _scanButton.Click += async (_, _) => await ScanGamesAsync();

        _pageToolbar.Controls.Add(_refreshButton);
        _pageToolbar.Controls.Add(_manualButton);
        _pageToolbar.Controls.Add(_scanButton);

        _contentHeader.Controls.Add(_pageSubtitle);
        _contentHeader.Controls.Add(_pageTitle);
        _contentHeader.Controls.Add(_pageToolbar);

        // ===== 主页内容（游戏列表+详情面板）=====
        _homePage = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor
        };

        // 游戏列表
        _gameGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = BgColor,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToOrderColumns = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 38,
            GridColor = DividerColor,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ShowCellToolTips = false,
            ScrollBars = ScrollBars.Vertical
        };

        // 列标题样式
        _gameGridView.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = CardBgColor,
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold),
            SelectionBackColor = CardBgColor,
            SelectionForeColor = TextSecondary,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0)
        };

        // 默认单元格样式
        _gameGridView.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = BgColor,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            SelectionBackColor = Color.FromArgb(0, 120, 212),
            SelectionForeColor = Color.White,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0)
        };

        // 交替行样式（与默认行相同）
        _gameGridView.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = BgColor,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(0, 120, 212),
            SelectionForeColor = Color.White
        };

        _gameGridView.RowTemplate.Height = 34;
        _gameGridView.RowTemplate.MinimumHeight = 34;

        // 添加列
        _gameGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "GameName", HeaderText = "游戏名称", FillWeight = 260 });
        _gameGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "平台", FillWeight = 70 });
        _gameGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", FillWeight = 100 });
        _gameGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "InstallPath", HeaderText = "游戏路径", FillWeight = 350 });
        _gameGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tag", HeaderText = "Tag", Visible = false });

        // 状态列颜色格式化
        _gameGridView.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex == 2 && e.RowIndex >= 0)
            {
                var row = _gameGridView.Rows[e.RowIndex];
                var game = row.Cells["Tag"].Value as GameInfo;
                if (game != null)
                {
                    bool isEnabled = game.IsPatched || game.IsAdvancedPatched || game.IsCyberpunkPatched || game.IsDLSS5Patched || game.IsDX9DLSS5Patched;
                    e.CellStyle.ForeColor = isEnabled ? SuccessColor : TextTertiary;
                    e.CellStyle.Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold);
                    e.CellStyle.SelectionForeColor = Color.White;
                }
            }
            if (e.ColumnIndex == 3 && e.RowIndex >= 0)
            {
                e.CellStyle.ForeColor = TextTertiary;
                e.CellStyle.Font = new Font("Segoe UI Variable Text", 8.5f);
                e.CellStyle.SelectionForeColor = Color.White;
            }
            if (e.ColumnIndex == 1 && e.RowIndex >= 0)
            {
                e.CellStyle.ForeColor = TextSecondary;
                e.CellStyle.SelectionForeColor = Color.White;
            }
        };

        _gameGridView.SelectionChanged += (_, _) => OnGameSelected();

        // 初始化右键菜单
        InitGameContextMenu();

        // 空状态提示
        _emptyHintLabel = new Label
        {
            Text = "点击右上角「自动扫描」自动检测 Steam / Epic 已安装的游戏\n或点击「手动添加」指定游戏目录",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 12f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        // ===== 详情面板 =====
        _detailPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 210,
            BackColor = CardBgColor
        };

        // 按钮布局：常驻按钮在前，非常驻按钮在后
        const int btnW = 190;
        const int btnH = 38;
        const int row1Y = 72;
        const int row2Y = 120;
        const int row3Y = 168;
        int[] btnXs = { 20, 224, 428, 632 };

        _detailGameName = new Label
        {
            Text = "未选择游戏",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 13f, FontStyle.Bold),
            AutoSize = false,
            Location = new Point(20, 10),
            Size = new Size(300, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            AutoEllipsis = true
        };

        _detailStatus = new Label
        {
            Text = "",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Bold),
            AutoSize = false,
            Location = new Point(20, 40),
            Size = new Size(300, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            AutoEllipsis = true
        };

        _detailGamePath = new Label
        {
            Text = "",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = false,
            Location = new Point(340, 12),
            Size = new Size(520, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoEllipsis = true
        };

        _detailExePath = new Label
        {
            Text = "",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = false,
            Location = new Point(340, 36),
            Size = new Size(520, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoEllipsis = true
        };

        // 第一行：常驻按钮（编辑配置、排错、DLSS5、多帧生成）
        _configButton = new ModernButton
        {
            Text = "编辑多帧配置",
            IsSecondary = true,
            Size = new Size(btnW, btnH),
            Location = new Point(btnXs[0], row1Y),
            Enabled = false,
            AccentColor = AccentColor,
            HoverColor = AccentHover
        };
        _configButton.Click += (_, _) => OpenConfigEditor();

        _troubleshootButton = new ModernButton
        {
            Text = "排错修复(替换sl.dll)",
            IsSecondary = true,
            Size = new Size(btnW, btnH),
            Location = new Point(btnXs[1], row1Y),
            Enabled = false,
            AccentColor = WarningColor,
            HoverColor = Color.FromArgb(255, 170, 30)
        };
        _troubleshootButton.Click += async (_, _) => await ApplyTroubleshootAsync();

        _dlss5Button = new ModernButton
        {
            Text = "开启DLSS5",
            IsSecondary = false,
            Size = new Size(btnW, btnH),
            Location = new Point(btnXs[2], row1Y),
            Enabled = false,
            AccentColor = Color.FromArgb(249, 115, 22),
            HoverColor = Color.FromArgb(251, 146, 60)
        };
        _dlss5Button.Click += async (_, _) => await ToggleDLSS5Async();

        _applyButton = new ModernButton
        {
            Text = "一键开启多帧生成",
            AccentColor = SuccessColor,
            HoverColor = Color.FromArgb(30, 200, 140),
            PressedColor = Color.FromArgb(10, 160, 110),
            Size = new Size(btnW, btnH),
            Location = new Point(btnXs[3], row1Y),
            Enabled = false
        };
        _applyButton.Click += async (_, _) => await ApplyPatchAsync();

        _restoreButton = new ModernButton
        {
            Text = "一键还原",
            IsSecondary = true,
            Size = new Size(btnW, btnH),
            Location = new Point(btnXs[3], row1Y),
            Enabled = false,
            Visible = false
        };
        _restoreButton.Click += async (_, _) => await RestorePatchAsync();

        // 第二行：非常驻按钮（2077专用、配置名修改、DX9 DLSS5）
        _cyberpunkButton = new ModernButton
        {
            Text = "2077专用补丁",
            IsSecondary = true,
            Size = new Size(btnW, btnH),
            Location = new Point(btnXs[0], row2Y),
            Enabled = false,
            Visible = false,
            AccentColor = Color.FromArgb(255, 100, 0),
            HoverColor = Color.FromArgb(255, 130, 30)
        };
        _cyberpunkButton.Click += async (_, _) => await ApplyCyberpunk2077PatchAsync();

        _configNameButton = new ModernButton
        {
            Text = "配置名修改",
            IsSecondary = true,
            Size = new Size(btnW, btnH),
            Location = new Point(btnXs[1], row2Y),
            Enabled = false,
            Visible = false,
            AccentColor = Color.FromArgb(0, 150, 180),
            HoverColor = Color.FromArgb(0, 180, 210)
        };
        _configNameButton.Click += (_, _) => OpenConfigNameEditor();

        _dx9DLSS5Button = new ModernButton
        {
            Text = "开启DX9 DLSS5",
            IsSecondary = false,
            Size = new Size(btnW, btnH),
            Location = new Point(btnXs[2], row2Y),
            Enabled = false,
            Visible = false,
            AccentColor = Color.FromArgb(168, 85, 247),
            HoverColor = Color.FromArgb(192, 132, 252)
        };
        _dx9DLSS5Button.Click += async (_, _) => await ToggleDX9DLSS5Async();

        _detailPanel.Controls.Add(_detailGameName);
        _detailPanel.Controls.Add(_detailStatus);
        _detailPanel.Controls.Add(_detailGamePath);
        _detailPanel.Controls.Add(_detailExePath);
        _detailPanel.Controls.Add(_applyButton);
        _detailPanel.Controls.Add(_restoreButton);
        _detailPanel.Controls.Add(_cyberpunkButton);
        _detailPanel.Controls.Add(_configNameButton);
        _detailPanel.Controls.Add(_configButton);
        _detailPanel.Controls.Add(_troubleshootButton);
        _detailPanel.Controls.Add(_dlss5Button);
        _detailPanel.Controls.Add(_dx9DLSS5Button);

        // ===== 设置页面 =====
        _settingsPage = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            Visible = false,
            Padding = new Padding(24, 20, 24, 20)
        };

        _settingsCard = new Panel
        {
            BackColor = CardBgColor,
            Location = new Point(24, 20),
            Size = new Size(700, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _settingsTitle = new Label
        {
            Text = "常规设置",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 14f, FontStyle.Bold),
            Location = new Point(20, 16),
            AutoSize = true
        };

        _settingsThemeButton = new ModernButton
        {
            Text = "☀️  切换到浅色模式",
            IsSecondary = true,
            Size = new Size(200, 40),
            Location = new Point(20, 60)
        };
        _settingsThemeButton.Click += (_, _) => ToggleTheme();

        _settingsCard.Controls.Add(_settingsTitle);
        _settingsCard.Controls.Add(_settingsThemeButton);
        _settingsPage.Controls.Add(_settingsCard);

        // ===== 使用说明页面 =====
        _helpPage = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            Visible = false,
            Padding = new Padding(24, 20, 24, 20)
        };

        _helpTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = CardBgColor,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Margin = new Padding(0)
        };
        _helpPage.Controls.Add(_helpTextBox);

        // ===== 更新内容页面 =====
        _changelogPage = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            Visible = false,
            Padding = new Padding(24, 20, 24, 20)
        };

        // 顶部工具栏（检查更新按钮）
        _changelogToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = BgColor
        };

        _changelogCheckUpdateButton = new ModernButton
        {
            Text = "检查软件更新",
            IsSecondary = true,
            Size = new Size(140, 36),
            Location = new Point(0, 7)
        };
        _changelogCheckUpdateButton.Click += async (_, _) => await CheckForUpdatesAsync(showNoUpdate: true);

        _changelogToolbar.Controls.Add(_changelogCheckUpdateButton);

        // 内容卡片
        _changelogCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CardBgColor,
            Padding = new Padding(16)
        };

        _changelogTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = CardBgColor,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Margin = new Padding(0)
        };
        _changelogCard.Controls.Add(_changelogTextBox);
        _changelogPage.Controls.Add(_changelogCard);
        _changelogPage.Controls.Add(_changelogToolbar);

        // ===== 关于页面 =====
        _aboutPage = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            Visible = false,
            Padding = new Padding(24, 20, 24, 20)
        };

        _aboutCard = new Panel
        {
            BackColor = CardBgColor,
            Location = new Point(24, 20),
            Size = new Size(700, 680),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _aboutAppName = new Label
        {
            Text = "多帧生成+DLSS5开启工具",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 18f, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true
        };

        _aboutVersion = new Label
        {
            Text = $"版本 V{DisplayVersion}",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 10f),
            Location = new Point(22, 55),
            AutoSize = true
        };

        _aboutDescription = new Label
        {
            Text = "一键为支持 DLSS 的游戏开启多帧生成和 DLSS5 功能\n支持经典模式、高级模式、2077专用补丁、RE引擎通用补丁等\nRTX 50 系以下显卡也能开启多帧生成",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 10f),
            Location = new Point(22, 90),
            AutoSize = false,
            Size = new Size(650, 60)
        };

        _aboutAuthorLabel = new Label
        {
            Text = "作者",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 12f, FontStyle.Bold),
            Location = new Point(22, 170),
            AutoSize = true
        };

        _aboutLinksPanel = new Panel
        {
            Location = new Point(22, 200),
            Size = new Size(650, 120),
            BackColor = Color.Transparent
        };

        // 致谢标题
        _aboutCreditsLabel = new Label
        {
            Text = "致谢（社区开源项目）",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 12f, FontStyle.Bold),
            Location = new Point(22, 340),
            AutoSize = true
        };

        // 致谢内容
        _aboutCreditsText = new Label
        {
            Text = "DLSS-Enabler (artur07305) · RTX40MFG-Unlock (dashdogy)\n" +
                   "MFGAdaUnlock (mavismmg) · RenoDX (clshortfuse)\n" +
                   "DLSS5-Feeder (jlrouzies-fr) · OptiScaler (dbz400)\n" +
                   "dlssg-to-fsr3 (Nukem9) · DLSSTweaks (emoose)\n" +
                   "ReShade (crosire) · DLSS Unlocked (ShyVortex)\n" +
                   "nvngx_dlssnr.dll patch (Uncle Burrito / RenoDX社区)\n\n" +
                   "本软件仅作学习交流使用，所有补丁版权归原作者及NVIDIA所有",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 9f),
            Location = new Point(22, 375),
            AutoSize = false,
            Size = new Size(650, 280)
        };

        // GitHub链接
        _githubIcon = new PictureBox
        {
            Size = new Size(24, 24),
            Location = new Point(0, 0),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        _githubIcon.Image = LoadEmbeddedImage("DLSSFrameGenEnabler.Resources.github.jpg");

        _githubLink = new Label
        {
            Text = "GitHub（待添加）",
            ForeColor = AccentColor,
            Font = new Font("Segoe UI Variable Text", 10f),
            Location = new Point(34, 2),
            AutoSize = true,
            Cursor = Cursors.Hand
        };

        // 哔哩哔哩链接
        _bilibiliIcon = new PictureBox
        {
            Size = new Size(24, 24),
            Location = new Point(0, 40),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        _bilibiliIcon.Image = LoadEmbeddedImage("DLSSFrameGenEnabler.Resources.bilibili.png");

        _bilibiliLink = new Label
        {
            Text = "哔哩哔哩",
            ForeColor = AccentColor,
            Font = new Font("Segoe UI Variable Text", 10f),
            Location = new Point(34, 42),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        _bilibiliLink.Click += (_, _) => OpenUrl("https://space.bilibili.com/414911649");

        // 小黑盒链接
        _xiaoheiheIcon = new PictureBox
        {
            Size = new Size(24, 24),
            Location = new Point(0, 80),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        _xiaoheiheIcon.Image = LoadEmbeddedImage("DLSSFrameGenEnabler.Resources.xiaoheihe.png");

        _xiaoheiheLink = new Label
        {
            Text = "小黑盒",
            ForeColor = AccentColor,
            Font = new Font("Segoe UI Variable Text", 10f),
            Location = new Point(34, 82),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        _xiaoheiheLink.Click += (_, _) => OpenUrl("https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=d2b27269b51e&new_post_share_style=true");

        _aboutLinksPanel.Controls.Add(_githubIcon);
        _aboutLinksPanel.Controls.Add(_githubLink);
        _aboutLinksPanel.Controls.Add(_bilibiliIcon);
        _aboutLinksPanel.Controls.Add(_bilibiliLink);
        _aboutLinksPanel.Controls.Add(_xiaoheiheIcon);
        _aboutLinksPanel.Controls.Add(_xiaoheiheLink);

        _aboutCard.Controls.Add(_aboutAppName);
        _aboutCard.Controls.Add(_aboutVersion);
        _aboutCard.Controls.Add(_aboutDescription);
        _aboutCard.Controls.Add(_aboutAuthorLabel);
        _aboutCard.Controls.Add(_aboutLinksPanel);
        _aboutCard.Controls.Add(_aboutCreditsLabel);
        _aboutCard.Controls.Add(_aboutCreditsText);
        _aboutPage.Controls.Add(_aboutCard);

        // ===== 免责声明栏 =====
        _disclaimerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = Color.FromArgb(35, 25, 20)
        };

        _disclaimerLabel = new Label
        {
            Text = "⚠ 免责声明：不建议在网游和带反作弊的游戏上使用本软件。本软件本质是修改游戏文件，修改文件可能导致游戏封号，如遇封号本软件概不负责。",
            ForeColor = Color.FromArgb(210, 160, 100),
            Font = new Font("Segoe UI Variable Text", 8f),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(10, 0, 10, 0)
        };
        _disclaimerPanel.Controls.Add(_disclaimerLabel);

        // ===== 状态栏 =====
        _statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = NavBgColor
        };

        _statusLabel = new Label
        {
            Text = "就绪",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = true,
            Location = new Point(16, 8)
        };

        _progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Size = new Size(200, 6),
            Location = new Point(850, 13),
            Visible = false,
            BackColor = NavBgColor
        };

        _statusBar.Controls.Add(_statusLabel);
        _statusBar.Controls.Add(_progressBar);

        // ===== 组装主页 =====
        _homePage.Controls.Add(_gameGridView);
        _homePage.Controls.Add(_emptyHintLabel);
        _homePage.Controls.Add(_detailPanel);

        // ===== 组装内容区 =====
        _contentPanel.Controls.Add(_homePage);
        _contentPanel.Controls.Add(_settingsPage);
        _contentPanel.Controls.Add(_helpPage);
        _contentPanel.Controls.Add(_changelogPage);
        _contentPanel.Controls.Add(_aboutPage);
        _contentPanel.Controls.Add(_statusBar);
        _contentPanel.Controls.Add(_disclaimerPanel);
        _contentPanel.Controls.Add(_contentHeader);

        // ===== 组装窗体 =====
        Controls.Add(_contentPanel);
        Controls.Add(_navPanel);

        _emptyHintLabel.BringToFront();

        // 加载使用说明和更新内容文本
        LoadHelpText();
        LoadChangelogText();
    }

    /// <summary>创建导航项</summary>
    private void CreateNavItem(string key, string text, int index)
    {
        var itemPanel = new Panel
        {
            Width = _navItemsPanel.ClientSize.Width - 16,
            Height = 40,
            Location = new Point(8, 8 + index * 46),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Tag = key
        };

        var textLabel = new Label
        {
            Text = text,
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 10f),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
            Cursor = Cursors.Hand
        };

        itemPanel.Controls.Add(textLabel);

        // 点击事件
        void OnClick(object? sender, EventArgs e)
        {
            SelectNavItem(key);
            HandleNavClick(key);
        }

        itemPanel.Click += OnClick;
        textLabel.Click += OnClick;

        // Hover效果
        itemPanel.MouseEnter += (_, _) => { if (!_navItems.First(i => i.Key == key).IsSelected) itemPanel.BackColor = NavItemHover; };
        itemPanel.MouseLeave += (_, _) => { if (!_navItems.First(i => i.Key == key).IsSelected) itemPanel.BackColor = Color.Transparent; };

        _navItemsPanel.Controls.Add(itemPanel);
        _navItems.Add(new NavItem { Key = key, Text = text, Panel = itemPanel, TextLabel = textLabel });
    }

    /// <summary>选中导航项</summary>
    private void SelectNavItem(string key)
    {
        foreach (var item in _navItems)
        {
            if (item.Key == key)
            {
                item.IsSelected = true;
                item.Panel.BackColor = NavItemSelected;
                item.TextLabel.ForeColor = Color.White;
                item.TextLabel.Font = new Font("Segoe UI Variable Text", 10f, FontStyle.Bold);
            }
            else
            {
                item.IsSelected = false;
                item.Panel.BackColor = Color.Transparent;
                item.TextLabel.ForeColor = TextSecondary;
                item.TextLabel.Font = new Font("Segoe UI Variable Text", 10f);
            }
        }
    }

    /// <summary>处理导航点击</summary>
    private void HandleNavClick(string key)
    {
        // 隐藏所有页面
        _homePage.Visible = false;
        _settingsPage.Visible = false;
        _helpPage.Visible = false;
        _changelogPage.Visible = false;
        _aboutPage.Visible = false;
        _pageToolbar.Visible = false;

        switch (key)
        {
            case "home":
                _pageTitle.Text = "主页";
                _pageSubtitle.Text = "管理游戏的多帧生成和DLSS5补丁";
                _homePage.Visible = true;
                // 主页工具栏一直显示（自动扫描、手动添加等）
                _pageToolbar.Visible = true;
                break;
            case "settings":
                _pageTitle.Text = "设置";
                _pageSubtitle.Text = "软件设置和偏好";
                _settingsPage.Visible = true;
                break;
            case "help":
                _pageTitle.Text = "使用说明";
                _pageSubtitle.Text = "软件详细使用方法";
                _helpPage.Visible = true;
                break;
            case "changelog":
                _pageTitle.Text = "更新内容";
                _pageSubtitle.Text = "版本更新记录";
                _changelogPage.Visible = true;
                break;
            case "about":
                _pageTitle.Text = "关于";
                _pageSubtitle.Text = "软件信息";
                _aboutPage.Visible = true;
                break;
        }
    }

    /// <summary>加载使用说明文本（硬编码，不依赖外部文件）</summary>
    private void LoadHelpText()
    {
        _helpTextBox.Text = @"多帧生成+DLSS5开启工具 使用说明

【重要说明】
由于米哈游系列游戏反作弊较为严苛，所以暂不加入对米哈游游戏的适配，建议玩家们可以去使用【大力喜鹊】或者下载【HoYoshade】和【XXMI】开启DLSS5和多帧生成功能。
另外由于Vulkan使用率并没有DX广泛，所以暂时也不加入适配，还请玩家们见谅我没有对这两个方向去适配，毕竟个人时间和精力实在有限。

【一、软件简介】
本软件可以一键为支持 DLSS 的游戏开启多帧生成和 DLSS5 功能，让 RTX 50 系以下的显卡也能体验多帧生成带来的流畅画面。
支持 RTX 20系、30系、40系显卡开启多帧生成，支持 N卡和 A卡（7000系/9000系）开启 DLSS5。

【二、快速开始】
1. 点击左侧「主页」
2. 点击右上角「自动扫描」，软件会自动检测 Steam / Epic / EA / 育碧 / GOG 已安装的游戏
3. 在游戏列表中选中要操作的游戏
4. 点击下方「一键开启多帧生成」或「开启DLSS5」按钮
5. 等待操作完成即可

【三、手动添加游戏】
如果自动扫描没有找到你的游戏，可以点击「手动添加」：
- 默认模式：选择游戏文件夹，软件自动识别游戏 exe
- DX9手动添加：自行选择游戏真正的 exe 运行程序（适用于 DX9 游戏）

【四、多帧生成功能】
点击「一键开启多帧生成」后，根据显卡型号和游戏类型有不同模式可选：

RTX 40系显卡：
- 经典模式：适合老驱动（616.56之前），只对310.8及之前的帧生成版本有效，即装即用
- 高级模式：适合新驱动，对310.9及以上的帧生成版本有效，目前兼容性最强（部分游戏需要自行修改配置名才能进入游戏）

RTX 20系/30系显卡：
- 自动使用对应显卡系列的专用多帧生成补丁

RE引擎游戏（目前仅支持部分新游戏，如生化危机9、鬼武者、识质存在等）：
- 自动使用 RE 引擎专用多帧生成补丁
- 注意：RE引擎多帧生成与DLSS5互斥，同时开启会导致游戏闪退

开启后可以点击「编辑多帧配置」调整参数（仅经典模式）。

【五、DLSS5功能】
点击「开启DLSS5」后，软件会自动检测你的显卡类型（N卡/A卡），安装对应的补丁：
- N卡：安装 ReShade 框架 + renodx-dlss5 插件 + nvngx_dlssnr.dll + nvngx_dlss.dll
- A卡：安装 ReShade 框架 + AMD 设置程序（仅支持 RX 7000系和9000系）

RE引擎游戏会自动使用RE引擎通用补丁方案（包含 dinput8.dll + RE_DLSS5_Core.dll）。

注意：
- A卡用户开启DLSS5后，需要在游戏中开启 FSR3.0 以上，不要选择 FSR2.0，否则会导致游戏崩溃
- RE引擎游戏开启DLSS5后，暂时无法开启游戏自带的帧生成，建议使用DLSS5自带的AI插帧

【六、DX9 DLSS5功能】
对于 DX9 游戏，可以使用「开启DX9 DLSS5」功能：
- 软件会自动安装 dgVoodoo2（DX9转DX11）+ DLSS5-Feeder + ReShade 框架
- 支持 32位和64位 DX9 游戏
- 部分游戏（如半条命2）可能需要点击「替换dll」功能调整 dgVoodoo2 的位置到 bin 目录
- 游戏内配置需要用户自行完成（按 HOME 键打开 ReShade 配置界面）

【七、排错修复】
如果开启多帧生成后游戏没有生效，可以点击「排错修复」：
- 软件会自动检测游戏目录中的 sl. 开头的文件
- 只替换游戏中已有的文件，不会新增文件
- 经典模式和高级模式使用不同的排错补丁
- 需开启多帧生成后才能使用此功能

【八、配置名修改】
部分网络游戏（如异环、鸣潮）需要修改补丁的配置名才能进入游戏：
- 点击「配置名修改」（仅高级模式可用）
- 选择合适的配置名（如 dinput8、dsound 等）
- 软件会自动修改 version.dll 和 version.ini 的文件名
- dxgi.dll 只能改成 d3d12.dll
- 修改前会检测是否有同名文件，避免冲突
- 使用前请确认游戏出现报错、非法模块、无法开启多帧生成等问题才建议使用

【九、2077专用补丁】
赛博朋克2077比较特殊，需要使用专用补丁：
- 选中2077游戏后，只能点击「2077专用补丁」按钮
- 软件会自动把所有文件复制到 bin\x64 目录
- 使用后只能点击一键还原，其他按钮不可用
- 按照弹窗提示操作即可

【十、一键还原】
点击「一键还原」可以恢复游戏到原始状态：
- 软件会自动删除所有添加的补丁文件
- 替换的文件会自动恢复原始版本
- 还原前软件会自动备份游戏目录文件快照，确保不会误删游戏文件
- DLSS5 和多帧生成的还原分别通过各自的按钮实现

【十一、无责还原】
如果游戏没有备份文件，但检测到补丁特征，可以使用「无责还原」：
- 此功能会删除所有补丁相关文件
- 注意：可能会删除游戏源文件，使用前请仔细阅读警告
- 本软件只提供删除功能，并不提供恢复游戏源文件功能
- Steam/Epic游戏建议使用后验证游戏完整性
- 无法验证完整性的游戏，建议删除后重新开启多帧生成或DLSS5功能

【十二、右键菜单】
在游戏列表中右键点击游戏，可以快速操作：
- 一键开启/关闭多帧生成
- 开启/关闭 DLSS5
- 开启 DX9 DLSS5
- 排错修复
- 从列表中移除
- 交互逻辑与主界面按钮一致

【十三、主题切换】
点击左侧「设置」，可以切换浅色/深色模式。
- 浅色模式：列表每一行都是白色，选中行为蓝色
- 深色模式：深色背景，适合夜间使用

【十四、检查更新】
点击左侧「更新内容」，然后点击「检查软件更新」，可以检测是否有新版本。
新版本第一次打开软件时会自动弹出更新内容窗口。

【十五、显卡检测说明】
- 软件会优先检测独立显卡，避免核显导致的误判
- A卡 DLSS5 仅支持 RX 7000系和9000系显卡
- 不支持的显卡会提示无法开启 DLSS5

【十六、特殊游戏适配】
- 燕云十六声：已特殊适配，支持 DLSS5 + 多帧生成同时开启
- 异环：已特殊适配，nvngx_dlssg.dll 放到 NVIDIA 目录，其他补丁放到真正 exe 目录
- 半条命2：已特殊适配，dgVoodoo2 三补丁放到 bin 目录
- RE引擎游戏：通过检测 re_chunk_000.pak 文件自动识别

【十七、注意事项】
1. 不建议在网游和带反作弊的游戏上使用本软件
2. 本软件本质是修改游戏文件，修改文件可能导致游戏封号
3. 如遇游戏封号，本软件概不负责
4. A卡用户开启DLSS5后，需要在游戏中开启FSR3.0以上，不要选择FSR2.0
5. RE引擎游戏开启DLSS5后，暂时无法开启游戏自带的帧生成，建议使用DLSS5自带的AI插帧
6. 米哈游系列游戏暂不适配，建议使用【大力喜鹊】或【HoYoshade】【XXMI】
7. Vulkan 游戏暂不适配
";
    }

    /// <summary>加载更新内容文本（硬编码，不依赖外部文件）</summary>
    private void LoadChangelogText()
    {
        _changelogTextBox.Text = $@"当前版本 V{DisplayVersion}

【V1.10.3.2 更新内容】

一、新增显卡支持
- 支持 RTX 20系显卡开启多帧生成
- 支持 RTX 30系显卡开启多帧生成
- 软件自动检测显卡型号，调用对应系列的专用补丁
- RTX 40系继续使用经典模式和高级模式

二、新增 RE 引擎多帧生成支持
- 支持 RE 引擎游戏开启多帧生成（目前仅支持部分新游戏，如生化危机9、鬼武者、识质存在等）
- 通过检测 re_chunk_000.pak 文件自动识别 RE 引擎游戏
- RE 引擎游戏开启多帧生成时自动使用 RE 引擎专用补丁
- RE 引擎多帧生成与 DLSS5 互斥（冲突会导致闪退）

三、燕云十六声特殊适配
- 支持燕云十六声开启 DLSS5 + 多帧生成（可同时开启）
- 特殊识别 Engine\Binaries\Win64r 目录
- 自动安装 D3DCompiler_47.dll 和 ReShadePreset.ini
- 中文字体支持（MiSans-Bold.ttf）

四、DX9 DLSS5 完善
- 支持 DX9 32位游戏开启 DLSS5（dgVoodoo2 + DLSS5-Feeder 跨进程方案）
- 支持 DX9 64位游戏开启 DLSS5（直接加载方案）
- 手动添加 DX9 游戏时可自行选择真正的 exe 运行程序
- 替换 dll 功能：自动将 dgVoodoo2 三补丁移动到游戏运行 dll 目录（如半条命2的 bin 目录）

五、自动扫描平台扩展
- 新增 EA 平台游戏库自动扫描
- 新增育碧平台游戏库自动扫描
- 新增 GOG 平台游戏库自动扫描
- 继续支持 Steam 和 Epic 平台

六、右键菜单功能
- 游戏列表右键点击可快速操作
- 支持一键开启/关闭多帧生成、DLSS5、DX9 DLSS5
- 支持排错修复、从列表中移除
- 交互逻辑与主界面按钮完全一致

七、无责还原功能
- 无备份文件的游戏可使用无责还原删除补丁
- 使用前严重警告可能删除游戏源文件
- 建议 Steam/Epic 游戏使用后验证完整性

八、RE 引擎互斥逻辑修复
- 修复 RE 引擎游戏开启 DLSS5 后多帧生成按钮仍可交互的问题
- 修复 RE 引擎游戏开启多帧生成后 DLSS5 按钮仍可交互的问题
- 修复右键菜单中 RE 引擎互斥逻辑不生效的问题
- 修复按钮重叠导致点击还原按钮的问题
- 修复 RefreshGameStatus 不更新 DLSS5 和 RE 引擎状态的问题

九、其他修复
- 修复 reshade-shaders 目录名下划线/连字符不一致的问题
- 修复版本号不同步的问题（标题、导航栏、关于页统一）
- 修复浅色模式下列表白黑交替的问题（每一行都是白色，选中行蓝色）
- 修复手动添加 exe 时无法识别多帧生成支持的问题
- 修复卸载游戏占位符被误加入列表的问题
- 修复引擎 exe 被误识别为游戏 exe 的问题
- 修复异环特殊路径适配（nvngx_dlssg.dll 放 NVIDIA 目录）
- 修复半条命2 bin 目录识别问题
- 修复 2077 专用补丁还原问题
- 修复备份文件被误加入列表的问题
- 修复渲染 API 检测逻辑（避免 DX11+ 游戏误判为 DX9）

【V1.9.7.2 更新内容】

一、核心运行时更新
- nvngx_dlss.dll 从 310.8.0.0 更新到 310.9.1.0（最新版）
- nvngx_dlssg.dll（高级模式）从 310.9.0.0 更新到 310.9.1.0（最新版）
- 经典模式和 RTX20 系专用版本保持不变（兼容性需要）

二、关于页面添加社区作者致谢
- 新增致谢区域，列出所有使用的社区开源项目及作者
- 包括：DLSS-Enabler、RTX40MFG-Unlock、MFGAdaUnlock、RenoDX、DLSS5-Feeder、OptiScaler、dlssg-to-fsr3、DLSSTweaks、ReShade、DLSS Unlocked 等
- 深浅色模式自动适配

【V1.8.6.8 更新内容】

一、DX9 64位 reshade-shaders 缺失修复（关键修复）
- 修复 GetSharedDirectory 方法未正确处理子目录提取的 bug
- 添加缓存完整性检查，旧版本错误提取的缓存会自动删除重新提取

二、起源引擎 dgvoodoo bin 目录识别修复
- 修复半条命2等 Source 引擎游戏 dgvoodoo 补丁无法正确识别 bin 目录的问题
- 添加 Source 引擎特殊处理（最高优先级）

三、DX9 检测逻辑改进
- 新增 DetectRenderAPIType 方法，返回4种渲染 API 类型
- 检测逻辑更严格：exe 引用 d3d9.dll 且不引用 d3d11/d3d12/dxgi 才判定为 DX9

四、游戏 exe 识别逻辑加强
- 支持 Win64r 等变体目录名（如燕云十六声的 Engine\Binaries\Win64r）

【V1.8.6.5 更新内容】

一、UI 重构（模仿 WinUI3 风格）
- 全新左侧导航栏布局，参考 Windows 11 设置界面
- 主页、设置、使用说明、更新内容、关于五个页面
- 使用说明和更新内容直接显示在右侧内容区，不再弹窗

二、关于页面
- 软件介绍、版本信息
- 作者栏：GitHub、哔哩哔哩、小黑盒链接

三、浅色/深色模式
- 所有页面完整适配浅色和深色模式

【历史版本】

V1.8.6：体积优化，建立 Shared 共享目录存放重复大文件
V1.8.5：无责还原功能
V1.8.4：DX9 DLSS5 功能（32位）
V1.8.3：RE 引擎通用 DLSS5 补丁
V1.8.2：显卡检测优化（优先独立显卡）
V1.8.1：备份还原加强（快照对比）
V1.8.0：DLSS5 功能（N卡/A卡）
V1.7.x：配置名修改功能
V1.7.0：经典/高级双模式
V1.6.x：全盘扫描 + 免责声明
V1.5.x：浅色/深色模式
V1.4.x：排错修复功能
V1.3.x：2077专用补丁
V1.2.x：自动扫描 Epic 库
V1.1.x：手动添加游戏
V1.0 ~ V1.1：基础功能（经典模式多帧生成、Steam 库扫描、一键还原）

【重要说明】
由于米哈游系列游戏反作弊较为严苛，暂不加入对米哈游游戏的适配，建议使用【大力喜鹊】或【HoYoshade】【XXMI】。
由于 Vulkan 使用率并没有 DX 广泛，暂时也不加入适配，还请玩家们见谅。
";
    }

    #endregion

    #region 业务逻辑

    private async void CheckPatchFilesOnLoad()
    {
        // 使用快速检查，不触发补丁提取
        var (ok, usingEmbedded, needExtract) = _patcher.QuickCheckPatchFiles();
        if (!ok && needExtract)
        {
            SetStatus("正在加载内置补丁...", TextSecondary);
            // 在后台线程提取补丁，不阻塞UI
            await Task.Run(() => _patcher.CheckPatchFiles());
            SetStatus("补丁文件就绪（内置补丁已加载），点击「自动扫描」开始", TextSecondary);
        }
        else if (!ok)
        {
            SetStatus("警告：补丁文件缺失，请将补丁文件放入 Patches 文件夹", WarningColor);
        }
        else
        {
            var source = usingEmbedded ? "内置补丁已加载" : "外部 Patches 文件夹";
            SetStatus($"补丁文件就绪（{source}），点击「自动扫描」开始", TextSecondary);
        }
    }

    private async Task ScanGamesAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        SetBusyUI(true);
        SetStatus("正在自动扫描游戏库...");

        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            _games = await Task.Run(() => _scanner.ScanAllGames(progress));

            // 检测DX9游戏（在后台线程中执行，避免阻塞UI）
            await Task.Run(() =>
            {
                foreach (var game in _games)
                {
                    if (!string.IsNullOrEmpty(game.GameExePath) && File.Exists(game.GameExePath))
                    {
                        var apiType = FilePatcher.DetectRenderAPIType(game.GameExePath);
                        // 只有纯DX9游戏才标记为DX9游戏，MultiAPI优先使用DX11+通用DLSS5
                        game.IsDX9Game = (apiType == FilePatcher.RenderAPIType.DX9Only);
                        game.SupportsDX11OrAbove = (apiType == FilePatcher.RenderAPIType.DX11AndAbove || apiType == FilePatcher.RenderAPIType.MultiAPI);
                        game.Is32Bit = FilePatcher.Is32BitExecutable(game.GameExePath);
                        game.IsREEngine = FilePatcher.DetectREEngineGame(game);
                        // 检测DX9 DLSS5是否已开启
                        game.IsDX9DLSS5Patched = FilePatcher.IsDX9DLSS5Enabled(game);
                    }
                }
            });

            UpdateGameList();
            SetStatus($"扫描完成，共找到 {_games.Count} 个游戏");
        }
        catch (Exception ex)
        {
            SetStatus($"扫描失败：{ex.Message}", Color.FromArgb(255, 100, 100));
        }
        finally
        {
            _isBusy = false;
            SetBusyUI(false);
        }
    }

    /// <summary>
    /// 显示手动添加模式选择菜单
    /// </summary>
    private void ShowManualAddMenu()
    {
        var menu = new ContextMenuStrip
        {
            RenderMode = ToolStripRenderMode.System,
            BackColor = CardBgColor,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 9.5f)
        };

        var itemDefault = new ToolStripMenuItem("默认模式（自动识别游戏exe）");
        itemDefault.Click += (_, _) => ManualSelectFolder();

        var itemDx9 = new ToolStripMenuItem("手动模式（自行选择游戏exe，支持dx9游戏）");
        itemDx9.Click += (_, _) => ManualSelectDx9Exe();

        menu.Items.Add(itemDefault);
        menu.Items.Add(itemDx9);

        // 在按钮下方显示菜单
        var buttonPos = _manualButton.PointToScreen(new Point(0, _manualButton.Height));
        menu.Show(buttonPos);
    }

    /// <summary>
    /// DX9手动添加：直接让用户选择游戏真正的运行exe，自动推导游戏目录
    /// </summary>
    private void ManualSelectDx9Exe()
    {
        // 直接弹出文件选择框，让用户选择游戏真正的运行exe
        using var fileDialog = new OpenFileDialog
        {
            Title = "请选择游戏真正的运行exe程序（DX9游戏）",
            Filter = "可执行文件 (*.exe)|*.exe",
            FileName = ""
        };

        if (fileDialog.ShowDialog() != DialogResult.OK) return;
        var exePath = fileDialog.FileName;

        if (!File.Exists(exePath))
        {
            MessageBox.Show(this, "选择的exe文件不存在。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 自动推导游戏目录：
        // 如果exe在常见的子目录（如Binaries、Win64、bin、x64等），往上找一级作为游戏目录
        // 否则就用exe所在目录作为游戏目录
        var exeDir = Path.GetDirectoryName(exePath)!;
        var exeDirName = Path.GetFileName(exeDir).ToLowerInvariant();
        var commonExeDirs = new[] { "binaries", "win64", "win32", "bin", "x64", "x86", "win64shipping", "win32shipping" };
        
        string gameDir;
        if (commonExeDirs.Contains(exeDirName) && Directory.GetParent(exeDir) != null)
        {
            // 如果父目录也是常见的中间目录（如Binaries），再往上找一级
            var parentDir = Directory.GetParent(exeDir)!.FullName;
            var parentDirName = Path.GetFileName(parentDir).ToLowerInvariant();
            if (commonExeDirs.Contains(parentDirName) && Directory.GetParent(parentDir) != null)
            {
                gameDir = Directory.GetParent(parentDir)!.FullName;
            }
            else
            {
                gameDir = parentDir;
            }
        }
        else
        {
            gameDir = exeDir;
        }

        SetStatus("正在添加游戏...");

        // 创建GameInfo对象
        var gameName = Path.GetFileName(gameDir);
        var apiType = FilePatcher.DetectRenderAPIType(exePath);
        var isDx9Only = (apiType == FilePatcher.RenderAPIType.DX9Only);
        var game = new GameInfo
        {
            Name = gameName,
            InstallPath = gameDir,
            GameExePath = exePath,
            Source = isDx9Only ? "手动选择(DX9)" : "手动选择",
            SupportsFrameGen = false,
            IsDX9Game = isDx9Only, // 只有纯DX9游戏才标记为DX9游戏
            SupportsDX11OrAbove = (apiType == FilePatcher.RenderAPIType.DX11AndAbove || apiType == FilePatcher.RenderAPIType.MultiAPI),
            Is32Bit = FilePatcher.Is32BitExecutable(exePath),
            IsDX9DLSS5Patched = false
        };

        // 检测RE引擎
        game.IsREEngine = FilePatcher.DetectREEngineGame(game);

        // 检测是否已开启DX9 DLSS5
        game.IsDX9DLSS5Patched = FilePatcher.IsDX9DLSS5Enabled(game);

        // 检测是否有帧生成文件（使用智能查找，优先Nvidia目录，和自动扫描逻辑一致）
        // 先从推导的gameDir搜索，搜不到则从exeDir逐级往上搜索，确保不会漏掉
        string? dlssgPath = GameScanner.FindCorrectDlssgDll(gameDir);
        if (dlssgPath == null)
        {
            // 从exeDir开始逐级往上搜索，最多往上找5级
            var searchDir = exeDir;
            for (int level = 0; level < 5 && searchDir != null; level++)
            {
                dlssgPath = GameScanner.FindCorrectDlssgDll(searchDir);
                if (dlssgPath != null)
                {
                    // 找到了，更新gameDir为搜索到的目录（确保游戏根目录正确）
                    gameDir = searchDir;
                    break;
                }
                searchDir = Directory.GetParent(searchDir)?.FullName;
            }
        }
        if (dlssgPath != null)
        {
            game.SupportsFrameGen = true;
            game.DlssgDllPath = dlssgPath;
            game.InstallPath = gameDir; // 确保InstallPath是正确的游戏根目录
        }

        // 添加到列表（如果不存在）
        if (!_games.Any(g => g.InstallPath.Equals(game.InstallPath, StringComparison.OrdinalIgnoreCase)))
        {
            _games.Add(game);
            _games = _games.OrderBy(x => x.Name).ToList();
        }
        else
        {
            var existing = _games.First(g => g.InstallPath.Equals(game.InstallPath, StringComparison.OrdinalIgnoreCase));
            existing.GameExePath = exePath;
            existing.IsDX9Game = true;
            existing.SupportsDX11OrAbove = game.SupportsDX11OrAbove;
            existing.Is32Bit = game.Is32Bit;
            existing.IsDX9DLSS5Patched = game.IsDX9DLSS5Patched;
            existing.IsREEngine = game.IsREEngine;
            existing.SupportsFrameGen = game.SupportsFrameGen;
            existing.DlssgDllPath = game.DlssgDllPath;
            game = existing;
        }

        UpdateGameList();

        // 选中刚添加的游戏
        foreach (DataGridViewRow row in _gameGridView.Rows)
        {
            var g = row.Cells["Tag"].Value as GameInfo;
            if (g != null && g.InstallPath.Equals(game.InstallPath, StringComparison.OrdinalIgnoreCase))
            {
                row.Selected = true;
                if (row.Index >= 0)
                    _gameGridView.FirstDisplayedScrollingRowIndex = row.Index;
                break;
            }
        }
        SetStatus($"已添加DX9游戏：{game.Name}（运行程序：{Path.GetFileName(exePath)}）");
    }

    private void ManualSelectFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "请选择游戏安装目录（包含游戏运行exe的文件夹）",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        SetStatus("正在扫描所选目录...");
        var game = _scanner.ScanSingleDirectory(dialog.SelectedPath);

        if (game == null)
        {
            MessageBox.Show(this,
                "在所选目录中未找到游戏运行程序。\n\n请确认：\n1. 选择的是游戏的根安装目录\n2. 该目录下确实有游戏的运行exe文件\n\n注意：不支持帧生成的游戏也可以添加，只能开启DLSS5。",
                "未找到游戏运行程序", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetStatus("所选目录未找到游戏运行程序", WarningColor);
            return;
        }

        // 添加到列表（如果不存在）
        if (!_games.Any(g => g.InstallPath.Equals(game.InstallPath, StringComparison.OrdinalIgnoreCase)))
        {
            // 检测是否为DX9游戏
            if (!string.IsNullOrEmpty(game.GameExePath) && File.Exists(game.GameExePath))
            {
                var apiType2 = FilePatcher.DetectRenderAPIType(game.GameExePath);
                // 只有纯DX9游戏才标记为DX9游戏，MultiAPI优先使用DX11+通用DLSS5
                game.IsDX9Game = (apiType2 == FilePatcher.RenderAPIType.DX9Only);
                game.SupportsDX11OrAbove = (apiType2 == FilePatcher.RenderAPIType.DX11AndAbove || apiType2 == FilePatcher.RenderAPIType.MultiAPI);
                game.Is32Bit = FilePatcher.Is32BitExecutable(game.GameExePath);
                game.IsDX9DLSS5Patched = FilePatcher.IsDX9DLSS5Enabled(game);
                game.IsREEngine = FilePatcher.DetectREEngineGame(game);
            }
            _games.Add(game);
            _games = _games.OrderBy(x => x.Name).ToList();
        }
        else
        {
            var existing = _games.First(g => g.InstallPath.Equals(game.InstallPath, StringComparison.OrdinalIgnoreCase));
            existing.DlssgDllPath = game.DlssgDllPath;
            existing.GameExePath = game.GameExePath;
            existing.IsPatched = game.IsPatched;
            // 重新检测DX9状态
            if (!string.IsNullOrEmpty(existing.GameExePath) && File.Exists(existing.GameExePath))
            {
                var apiType3 = FilePatcher.DetectRenderAPIType(existing.GameExePath);
                // 只有纯DX9游戏才标记为DX9游戏，MultiAPI优先使用DX11+通用DLSS5
                existing.IsDX9Game = (apiType3 == FilePatcher.RenderAPIType.DX9Only);
                existing.SupportsDX11OrAbove = (apiType3 == FilePatcher.RenderAPIType.DX11AndAbove || apiType3 == FilePatcher.RenderAPIType.MultiAPI);
                existing.Is32Bit = FilePatcher.Is32BitExecutable(existing.GameExePath);
                existing.IsDX9DLSS5Patched = FilePatcher.IsDX9DLSS5Enabled(existing);
                existing.IsREEngine = FilePatcher.DetectREEngineGame(existing);
            }
            game = existing;
        }

        UpdateGameList();

        // 选中刚添加的游戏
        foreach (DataGridViewRow row in _gameGridView.Rows)
        {
            var g = row.Cells["Tag"].Value as GameInfo;
            if (g != null && g.InstallPath.Equals(game.InstallPath, StringComparison.OrdinalIgnoreCase))
            {
                row.Selected = true;
                if (row.Index >= 0)
                    _gameGridView.FirstDisplayedScrollingRowIndex = row.Index;
                break;
            }
        }
        SetStatus($"已添加游戏：{game.Name}");
    }

    private void RefreshGameStatus()
    {
        foreach (var game in _games)
        {
            game.IsPatched = GameScanner.CheckAlreadyPatched(game);
            game.IsAdvancedPatched = GameScanner.CheckAlreadyAdvancedPatched(game);
            game.IsCyberpunkPatched = GameScanner.CheckAlreadyCyberpunkPatched(game);
            game.IsREFrameGenPatched = GameScanner.CheckAlreadyREFrameGenPatched(game);
            game.IsDLSS5Patched = FilePatcher.IsDLSS5Enabled(game);
            game.IsREEngine = FilePatcher.DetectREEngineGame(game);
        }
        UpdateGameList();
        OnGameSelected();
        SetStatus("状态已刷新");
    }

    private void UpdateGameList()
    {
        // 记录当前选中的游戏（刷新后恢复选中，避免跳到第一个）
        string? selectedGamePath = null;
        if (_gameGridView.SelectedRows.Count > 0)
        {
            var selectedGame = _gameGridView.SelectedRows[0].Cells["Tag"].Value as GameInfo;
            if (selectedGame != null)
                selectedGamePath = selectedGame.InstallPath;
        }

        _gameGridView.Rows.Clear();

        if (_games.Count == 0)
        {
            _emptyHintLabel.Visible = true;
            _gameGridView.Visible = false;
            return;
        }

        _emptyHintLabel.Visible = false;
        _gameGridView.Visible = true;

        int restoreIndex = -1;
        for (int i = 0; i < _games.Count; i++)
        {
            var game = _games[i];
            string statusText;
            if (game.SupportsFrameGen)
            {
                statusText = game.IsPatched || game.IsAdvancedPatched || game.IsCyberpunkPatched || game.IsDLSS5Patched
                    ? "● 已开启" : "○ 未开启";
            }
            else
            {
                statusText = game.IsDLSS5Patched
                    ? "● 已开启（DLSS5）" : "○ 仅支持DLSS5";
            }
            int rowIndex = _gameGridView.Rows.Add(game.Name, game.Source, statusText, game.InstallPath, game);

            // 直接设置行的背景色，确保所有行都是统一颜色（不使用交替行颜色）
            var row = _gameGridView.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = _currentTheme == "Light" ? Color.FromArgb(255, 255, 255) : Color.FromArgb(32, 32, 32);
            row.DefaultCellStyle.ForeColor = _currentTheme == "Light" ? Color.FromArgb(30, 30, 30) : Color.FromArgb(240, 240, 240);
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 212);
            row.DefaultCellStyle.SelectionForeColor = Color.White;

            // 找到之前选中的游戏，记录索引
            if (selectedGamePath != null && game.InstallPath == selectedGamePath)
                restoreIndex = i;
        }

        // 恢复选中状态
        if (restoreIndex >= 0 && restoreIndex < _gameGridView.Rows.Count)
        {
            _gameGridView.ClearSelection();
            _gameGridView.Rows[restoreIndex].Selected = true;
            _gameGridView.CurrentCell = _gameGridView.Rows[restoreIndex].Cells[0];
        }
    }

    private void OnGameSelected()
    {
        if (_gameGridView.SelectedRows.Count == 0)
        {
            _selectedGame = null;
            _detailGameName.Text = "未选择游戏";
            _detailStatus.Text = "";
            _detailGamePath.Text = "";
            _detailExePath.Text = "";
            _applyButton.Enabled = false;
            _applyButton.Visible = true;
            _restoreButton.Enabled = false;
            _restoreButton.Visible = false;
            _configButton.Enabled = false;
            _troubleshootButton.Enabled = false;
            _cyberpunkButton.Visible = false;
            _cyberpunkButton.Enabled = false;
            _configNameButton.Visible = false;
            _configNameButton.Enabled = false;
            _dlss5Button.Enabled = false;
            _dlss5Button.Text = "开启DLSS5";
            _dx9DLSS5Button.Visible = false;
            _dx9DLSS5Button.Enabled = false;
            return;
        }

        var row = _gameGridView.SelectedRows[0];
        _selectedGame = row.Cells["Tag"].Value as GameInfo;
        if (_selectedGame == null) return;

        // 重新检测RE引擎属性（确保从保存列表加载的游戏也能正确识别）
        try
        {
            _selectedGame.IsREEngine = FilePatcher.DetectREEngineGame(_selectedGame);
        }
        catch { }

        _detailGameName.Text = _selectedGame.Name;
        string frameGenInfo = _selectedGame.SupportsFrameGen ? "支持帧生成" : "仅支持DLSS5";
        _detailStatus.Text = $"状态：{_selectedGame.PatchStatus}  |  平台：{_selectedGame.Source}  |  {frameGenInfo}";
        _detailStatus.ForeColor = _selectedGame.IsPatched ? SuccessColor : TextTertiary;
        _detailGamePath.Text = $"游戏目录：{_selectedGame.InstallPath}";
        _detailExePath.Text = $"运行程序：{_selectedGame.GameExePath ?? "未找到"}";

        var hasBackup = FilePatcher.HasBackup(_selectedGame);
        var hasPatchFiles = FilePatcher.HasPatchFiles(_selectedGame);
        var isAnyPatched = _selectedGame.IsPatched || _selectedGame.IsAdvancedPatched || _selectedGame.IsCyberpunkPatched || _selectedGame.IsREFrameGenPatched;
        var isCyberpunk = FilePatcher.IsCyberpunk2077(_selectedGame);

        // 2077专用按钮：仅当游戏是赛博朋克2077时显示
        _cyberpunkButton.Visible = isCyberpunk;

        if (isCyberpunk)
        {
            // 2077游戏：未打补丁时只能点2077专用补丁，已打补丁时只能点一键还原
            bool isCpPatched = _selectedGame.IsCyberpunkPatched;
            bool canRestore = hasBackup;
            bool canForceRestore = !hasBackup && (isCpPatched || hasPatchFiles);

            // 一键开启按钮：2077游戏始终隐藏（只能用专用补丁或还原）
            _applyButton.Visible = false;
            _applyButton.Enabled = false;

            // 一键还原/无责还原按钮
            if (canRestore)
            {
                _restoreButton.Text = "一键还原";
                _restoreButton.AccentColor = WarningColor;
                _restoreButton.HoverColor = Color.FromArgb(255, 170, 30);
            }
            else if (canForceRestore)
            {
                _restoreButton.Text = "无责还原";
                _restoreButton.AccentColor = Color.FromArgb(220, 50, 50);
                _restoreButton.HoverColor = Color.FromArgb(255, 80, 80);
            }
            _restoreButton.Visible = canRestore || canForceRestore;
            _restoreButton.Enabled = !_isBusy && (canRestore || canForceRestore);

            // 其他按钮禁用
            _configButton.Enabled = false;
            _troubleshootButton.Enabled = false;
            _configNameButton.Visible = false;
            _configNameButton.Enabled = false;

            // 2077专用补丁按钮：始终启用（点击时再判断是否已打补丁）
            _cyberpunkButton.Visible = true;
            _cyberpunkButton.Enabled = true;
            _cyberpunkButton.BringToFront();

            // DLSS5按钮：2077也可以用，根据状态切换文字
            _dlss5Button.Enabled = !_isBusy;
            _dlss5Button.Text = _selectedGame.IsDLSS5Patched ? "还原DLSS5" : "开启DLSS5";
            _dlss5Button.AccentColor = _selectedGame.IsDLSS5Patched ? WarningColor : Color.FromArgb(0, 200, 150);
            _dlss5Button.HoverColor = _selectedGame.IsDLSS5Patched ? Color.FromArgb(255, 170, 30) : Color.FromArgb(0, 230, 180);
        }
        else
        {
            // 普通游戏
            bool isPatchedState = isAnyPatched || hasBackup;
            bool canFrameGen = _selectedGame.SupportsFrameGen;
            bool isREEngine = _selectedGame.IsREEngine;
            bool reEngineDLSS5Enabled = isREEngine && _selectedGame.IsDLSS5Patched;
            bool reEngineFrameGenEnabled = isREEngine && (_selectedGame.IsPatched || _selectedGame.IsAdvancedPatched || _selectedGame.IsREFrameGenPatched);

            // 一键开启多帧生成：只有支持帧生成且未打补丁时才能交互
            // RE引擎特殊：已开启DLSS5时不能开启多帧生成（冲突闪退）
            _applyButton.Enabled = !_isBusy && !isAnyPatched && canFrameGen && !reEngineDLSS5Enabled;
            _applyButton.Visible = !isPatchedState;
            _applyButton.Text = _selectedGame.IsAdvancedPatched ? "已开启（高级模式）" :
                               (_selectedGame.IsPatched ? "已开启（经典模式）" :
                               (_selectedGame.IsREFrameGenPatched ? "已开启（RE引擎）" :
                               (canFrameGen ? "一键开启多帧生成" : "不支持帧生成")));
            // RE引擎已开启DLSS5时，按钮文字提示冲突
            if (reEngineDLSS5Enabled && canFrameGen && !_selectedGame.IsREFrameGenPatched)
            {
                _applyButton.Text = "DLSS5已开启（冲突）";
            }
            // 不支持帧生成时，按钮显示为禁用样式（深灰色）
            if (!canFrameGen || reEngineDLSS5Enabled)
            {
                _applyButton.AccentColor = Color.FromArgb(80, 80, 80);
                _applyButton.HoverColor = Color.FromArgb(80, 80, 80);
            }
            else
            {
                // 支持帧生成时，重置为绿色
                _applyButton.AccentColor = SuccessColor;
                _applyButton.HoverColor = Color.FromArgb(30, 200, 140);
                _applyButton.PressedColor = Color.FromArgb(10, 160, 110);
            }

            // 一键还原/无责还原：有备份时显示一键还原，无备份但有补丁文件时显示无责还原
            // 注意：只有当游戏打了多帧生成补丁时才显示还原按钮，避免与开启按钮重叠
            // DLSS5的还原通过_dlss5Button实现（点击"还原DLSS5"）
            bool hasFrameGenPatch = _selectedGame.IsPatched || _selectedGame.IsAdvancedPatched || _selectedGame.IsREFrameGenPatched;
            bool canRestore = hasBackup && canFrameGen && hasFrameGenPatch;
            bool canForceRestore = !hasBackup && hasPatchFiles && canFrameGen && hasFrameGenPatch;
            if (canRestore)
            {
                _restoreButton.Text = "一键还原";
                _restoreButton.AccentColor = WarningColor;
                _restoreButton.HoverColor = Color.FromArgb(255, 170, 30);
            }
            else if (canForceRestore)
            {
                _restoreButton.Text = "无责还原";
                _restoreButton.AccentColor = Color.FromArgb(220, 50, 50); // 红色警告
                _restoreButton.HoverColor = Color.FromArgb(255, 80, 80);
            }
            _restoreButton.Enabled = !_isBusy && (canRestore || canForceRestore);
            _restoreButton.Visible = canRestore || canForceRestore;

            // 配置多帧生成：只有支持帧生成且已打经典模式补丁时才能交互
            _configButton.Enabled = !_isBusy && _selectedGame.IsPatched && canFrameGen &&
                                    !string.IsNullOrEmpty(_selectedGame.GameExeDirectory) &&
                                    Directory.Exists(_selectedGame.GameExeDirectory);

            // 排错修复：只有支持帧生成且已开启多帧生成（经典或高级模式）时才能交互
            _troubleshootButton.Enabled = !_isBusy && canFrameGen && (_selectedGame.IsPatched || _selectedGame.IsAdvancedPatched);
            _cyberpunkButton.Enabled = false;

            // 配置名修改按钮：仅当高级模式已开启时显示
            _configNameButton.Visible = _selectedGame.IsAdvancedPatched;
            _configNameButton.Enabled = _selectedGame.IsAdvancedPatched && !_isBusy;

            // DLSS5按钮：所有游戏都可以用（包括不支持帧生成的游戏）
            // DLSS5按钮：未开启时橙色高亮，开启后（还原状态）和无法交互时灰色
            // RE引擎特殊：已开启多帧生成时不能开启DLSS5（冲突闪退）
            bool reEngineFrameGenConflict = isREEngine && reEngineFrameGenEnabled && !_selectedGame.IsDLSS5Patched;
            _dlss5Button.Enabled = !_isBusy && !reEngineFrameGenConflict;
            _dlss5Button.Text = _selectedGame.IsDLSS5Patched ? "还原DLSS5" :
                               (reEngineFrameGenConflict ? "多帧生成已开启（冲突）" : "开启DLSS5");
            if (_selectedGame.IsDLSS5Patched)
            {
                // 已开启（还原状态）：灰色
                _dlss5Button.AccentColor = Color.FromArgb(100, 100, 100);
                _dlss5Button.HoverColor = Color.FromArgb(120, 120, 120);
            }
            else if (reEngineFrameGenConflict)
            {
                // RE引擎多帧生成已开启，冲突禁用：深灰色
                _dlss5Button.AccentColor = Color.FromArgb(80, 80, 80);
                _dlss5Button.HoverColor = Color.FromArgb(80, 80, 80);
            }
            else
            {
                // 未开启：橙色高亮
                _dlss5Button.AccentColor = Color.FromArgb(249, 115, 22);
                _dlss5Button.HoverColor = Color.FromArgb(251, 146, 60);
            }

            // DX9 DLSS5按钮显示逻辑：
            // 按钮显示逻辑：
            // 1. 纯DX9游戏（isDx9Game=true）：只显示DX9 DLSS5，隐藏普通DLSS5
            // 2. 明确支持DX11+游戏（supportsDx11=true且不是DX9）：只显示普通DLSS5
            // 3. Unknown类型（两个都是false）：两个按钮都显示，让用户自行选择
            bool isDx9Game = _selectedGame.IsDX9Game;
            bool supportsDx11 = _selectedGame.SupportsDX11OrAbove;

            if (isDx9Game)
            {
                // DX9游戏：隐藏普通DLSS5按钮，只显示DX9 DLSS5按钮
                _dlss5Button.Visible = false;
                _dx9DLSS5Button.Visible = true;
                _dx9DLSS5Button.Enabled = !_isBusy;
                _dx9DLSS5Button.Text = _selectedGame.IsDX9DLSS5Patched ? "还原DX9 DLSS5" : "开启DX9 DLSS5";
                if (_selectedGame.IsDX9DLSS5Patched)
                {
                    // 已开启（还原状态）：灰色
                    _dx9DLSS5Button.AccentColor = Color.FromArgb(100, 100, 100);
                    _dx9DLSS5Button.HoverColor = Color.FromArgb(120, 120, 120);
                }
                else
                {
                    // 未开启：紫色高亮
                    _dx9DLSS5Button.AccentColor = Color.FromArgb(168, 85, 247);
                    _dx9DLSS5Button.HoverColor = Color.FromArgb(192, 132, 252);
                }
            }
            else if (supportsDx11)
            {
                // 明确支持DX11+游戏：显示普通DLSS5按钮，隐藏DX9按钮
                _dlss5Button.Visible = true;
                _dx9DLSS5Button.Visible = false;
            }
            else
            {
                // Unknown类型：两个按钮都显示，让用户自行选择
                _dlss5Button.Visible = true;
                _dx9DLSS5Button.Visible = true;
                _dx9DLSS5Button.Enabled = !_isBusy;
                _dx9DLSS5Button.Text = _selectedGame.IsDX9DLSS5Patched ? "还原DX9 DLSS5" : "开启DX9 DLSS5";
                if (_selectedGame.IsDX9DLSS5Patched)
                {
                    _dx9DLSS5Button.AccentColor = Color.FromArgb(100, 100, 100);
                    _dx9DLSS5Button.HoverColor = Color.FromArgb(120, 120, 120);
                }
                else
                {
                    _dx9DLSS5Button.AccentColor = Color.FromArgb(168, 85, 247);
                    _dx9DLSS5Button.HoverColor = Color.FromArgb(192, 132, 252);
                }
            }
        }
    }

    /// <summary>
    /// 初始化游戏列表右键菜单
    /// </summary>
    private void InitGameContextMenu()
    {
        _gameContextMenu = new ContextMenuStrip
        {
            RenderMode = ToolStripRenderMode.System,
            BackColor = CardBgColor,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 9.5f)
        };

        // 菜单项
        var itemApply = new ToolStripMenuItem("一键开启多帧生成");
        itemApply.Click += async (_, _) => await ApplyPatchAsync();

        var itemRestore = new ToolStripMenuItem("一键还原多帧生成");
        itemRestore.Click += async (_, _) => await RestorePatchAsync();

        var itemConfig = new ToolStripMenuItem("编辑多帧配置");
        itemConfig.Click += (_, _) => OpenConfigEditor();

        var itemTroubleshoot = new ToolStripMenuItem("排错修复（替换sl.dll）");
        itemTroubleshoot.Click += async (_, _) => await ApplyTroubleshootAsync();

        var itemConfigName = new ToolStripMenuItem("配置名修改");
        itemConfigName.Click += (_, _) => OpenConfigNameEditor();

        var itemCyberpunk = new ToolStripMenuItem("2077专属多帧生成补丁");
        itemCyberpunk.Click += async (_, _) => await ApplyCyberpunk2077PatchAsync();

        var itemDlss5 = new ToolStripMenuItem("开启DLSS5");
        itemDlss5.Click += async (_, _) => await ToggleDLSS5Async();

        var itemDx9Dlss5 = new ToolStripMenuItem("开启DX9 DLSS5");
        itemDx9Dlss5.Click += async (_, _) => await ToggleDX9DLSS5Async();

        var itemRemove = new ToolStripMenuItem("从列表中移除");
        itemRemove.Click += (_, _) => RemoveSelectedGame();

        var itemOpenFolder = new ToolStripMenuItem("打开游戏目录");
        itemOpenFolder.Click += (_, _) =>
        {
            if (_selectedGame != null && Directory.Exists(_selectedGame.InstallPath))
                System.Diagnostics.Process.Start("explorer.exe", _selectedGame.InstallPath);
        };

        // 添加到菜单
        _gameContextMenu.Items.Add(itemApply);
        _gameContextMenu.Items.Add(itemRestore);
        _gameContextMenu.Items.Add(new ToolStripSeparator());
        _gameContextMenu.Items.Add(itemConfig);
        _gameContextMenu.Items.Add(itemTroubleshoot);
        _gameContextMenu.Items.Add(itemConfigName);
        _gameContextMenu.Items.Add(itemCyberpunk);
        _gameContextMenu.Items.Add(new ToolStripSeparator());
        _gameContextMenu.Items.Add(itemDlss5);
        _gameContextMenu.Items.Add(itemDx9Dlss5);
        _gameContextMenu.Items.Add(new ToolStripSeparator());
        _gameContextMenu.Items.Add(itemOpenFolder);
        _gameContextMenu.Items.Add(itemRemove);

        // 打开菜单时根据游戏状态动态设置启用/禁用
        _gameContextMenu.Opening += (_, e) =>
        {
            if (_selectedGame == null)
            {
                e.Cancel = true;
                return;
            }

            var hasBackup = FilePatcher.HasBackup(_selectedGame);
            var hasPatchFiles = FilePatcher.HasPatchFiles(_selectedGame);
            var isAnyPatched = _selectedGame.IsPatched || _selectedGame.IsAdvancedPatched || _selectedGame.IsCyberpunkPatched || _selectedGame.IsREFrameGenPatched;
            var isCyberpunk = FilePatcher.IsCyberpunk2077(_selectedGame);
            bool isPatchedState = isAnyPatched || hasBackup;

            // 通用设置
            itemApply.Enabled = !_isBusy;
            itemRestore.Enabled = !_isBusy;
            itemConfig.Enabled = !_isBusy;
            itemTroubleshoot.Enabled = !_isBusy;
            itemConfigName.Enabled = !_isBusy;
            itemCyberpunk.Enabled = !_isBusy;
            itemDlss5.Enabled = !_isBusy;
            itemOpenFolder.Enabled = true;

            if (isCyberpunk)
            {
                // 2077游戏
                bool isCpPatched = _selectedGame.IsCyberpunkPatched;
                bool canRestore = hasBackup;
                bool canForceRestore = !hasBackup && (isCpPatched || hasPatchFiles);

                itemApply.Visible = false;
                itemApply.Enabled = false;
                itemRestore.Visible = canRestore || canForceRestore;
                itemRestore.Enabled = !_isBusy && (canRestore || canForceRestore);
                itemRestore.Text = canRestore ? "一键还原" : "无责还原（删除补丁）";
                itemConfig.Visible = false;
                itemConfig.Enabled = false;
                itemTroubleshoot.Visible = false;
                itemTroubleshoot.Enabled = false;
                itemConfigName.Visible = false;
                itemConfigName.Enabled = false;
                itemCyberpunk.Visible = true;
                itemCyberpunk.Enabled = !_isBusy;
                itemCyberpunk.Text = isCpPatched ? "2077专属补丁（已开启）" : "2077专属多帧生成补丁";
            }
            else
            {
                // 普通游戏
                bool canFrameGen = _selectedGame.SupportsFrameGen;
                bool canRestore = hasBackup && canFrameGen;
                bool canForceRestore = !hasBackup && hasPatchFiles && canFrameGen;
                bool isREEngine = _selectedGame.IsREEngine;
                bool reEngineDLSS5Enabled = isREEngine && _selectedGame.IsDLSS5Patched;
                bool reEngineFrameGenEnabled = isREEngine && (_selectedGame.IsPatched || _selectedGame.IsAdvancedPatched || _selectedGame.IsREFrameGenPatched);

                itemApply.Visible = !isPatchedState;
                itemApply.Enabled = !_isBusy && !isAnyPatched && canFrameGen && !reEngineDLSS5Enabled;
                itemApply.Text = _selectedGame.IsAdvancedPatched ? "已开启（高级模式）" :
                                 (_selectedGame.IsPatched ? "已开启（经典模式）" :
                                 (_selectedGame.IsREFrameGenPatched ? "已开启（RE引擎）" :
                                 (canFrameGen ? (reEngineDLSS5Enabled ? "DLSS5已开启（冲突）" : "一键开启多帧生成") : "不支持帧生成")));
                itemRestore.Visible = canRestore || canForceRestore;
                itemRestore.Enabled = !_isBusy && (canRestore || canForceRestore);
                itemRestore.Text = canRestore ? "一键还原多帧生成" : "无责还原（删除补丁）";
                itemConfig.Visible = true;
                itemConfig.Enabled = !_isBusy && _selectedGame.IsPatched && canFrameGen &&
                                    !string.IsNullOrEmpty(_selectedGame.GameExeDirectory) &&
                                    Directory.Exists(_selectedGame.GameExeDirectory);
                itemTroubleshoot.Visible = true;
                itemTroubleshoot.Enabled = !_isBusy && canFrameGen && (_selectedGame.IsPatched || _selectedGame.IsAdvancedPatched);
                itemConfigName.Visible = _selectedGame.IsAdvancedPatched;
                itemConfigName.Enabled = _selectedGame.IsAdvancedPatched && !_isBusy;
                itemCyberpunk.Visible = false;
                itemCyberpunk.Enabled = false;
            }

            // DLSS5按钮显示逻辑：
            // 1. 纯DX9游戏：只显示DX9 DLSS5
            // 2. 明确支持DX11+游戏：只显示普通DLSS5
            // 3. Unknown类型：两个都显示，让用户选择
            bool isDx9Game = _selectedGame.IsDX9Game;
            bool supportsDx11 = _selectedGame.SupportsDX11OrAbove;

            if (isDx9Game)
            {
                // DX9游戏：隐藏普通DLSS5，只显示DX9 DLSS5
                itemDlss5.Visible = false;
                itemDlss5.Enabled = false;
                itemDx9Dlss5.Visible = true;
                itemDx9Dlss5.Enabled = !_isBusy;
                itemDx9Dlss5.Text = _selectedGame.IsDX9DLSS5Patched ? "还原DX9 DLSS5" : "开启DX9 DLSS5";
            }
            else if (supportsDx11)
            {
                // 明确支持DX11+游戏：显示普通DLSS5，隐藏DX9 DLSS5
                itemDlss5.Visible = true;
                // RE引擎特殊：已开启多帧生成时不能开启DLSS5（冲突闪退）
                bool reEngineFrameGenConflict = _selectedGame.IsREEngine &&
                                                (_selectedGame.IsPatched || _selectedGame.IsAdvancedPatched || _selectedGame.IsREFrameGenPatched) &&
                                                !_selectedGame.IsDLSS5Patched;
                itemDlss5.Enabled = !_isBusy && !reEngineFrameGenConflict;
                itemDlss5.Text = _selectedGame.IsDLSS5Patched ? "还原DLSS5" :
                                 (reEngineFrameGenConflict ? "多帧生成已开启（冲突）" : "开启DLSS5");
                itemDx9Dlss5.Visible = false;
                itemDx9Dlss5.Enabled = false;
            }
            else
            {
                // Unknown类型：两个都显示，让用户选择
                bool reEngineFrameGenConflict = _selectedGame.IsREEngine &&
                                                (_selectedGame.IsPatched || _selectedGame.IsAdvancedPatched || _selectedGame.IsREFrameGenPatched) &&
                                                !_selectedGame.IsDLSS5Patched;
                itemDlss5.Visible = true;
                itemDlss5.Enabled = !_isBusy && !reEngineFrameGenConflict;
                itemDlss5.Text = _selectedGame.IsDLSS5Patched ? "还原DLSS5" :
                                 (reEngineFrameGenConflict ? "多帧生成已开启（冲突）" : "开启DLSS5");
                itemDx9Dlss5.Visible = true;
                itemDx9Dlss5.Enabled = !_isBusy;
                itemDx9Dlss5.Text = _selectedGame.IsDX9DLSS5Patched ? "还原DX9 DLSS5" : "开启DX9 DLSS5";
            }

            // 移除列表按钮始终可用
            itemRemove.Enabled = true;
        };

        // 关联到 DataGridView
        _gameGridView.ContextMenuStrip = _gameContextMenu;

        // 右键点击时自动选中该行
        _gameGridView.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                _gameGridView.ClearSelection();
                _gameGridView.Rows[e.RowIndex].Selected = true;
                _selectedGame = _gameGridView.Rows[e.RowIndex].Cells["Tag"].Value as GameInfo;
            }
        };
    }

    /// <summary>
    /// 从列表中移除选中的游戏
    /// </summary>
    private void RemoveSelectedGame()
    {
        if (_selectedGame == null) return;

        var confirm = MessageBox.Show(this,
            $"确认要将《{_selectedGame.Name}》从列表中移除吗？\n\n" +
            "注意：这只会从软件列表中移除，不会删除游戏文件或已安装的补丁。",
            "移除游戏确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _games.Remove(_selectedGame);
        _selectedGame = null;
        UpdateGameList();
        SetStatus($"已从列表中移除游戏");
    }

    /// <summary>
    /// 打开多帧生成配置编辑器
    /// </summary>
    private void OpenConfigEditor()
    {
        if (_selectedGame == null) return;

        // 高级模式下不开放配置编辑器
        if (_selectedGame.IsAdvancedPatched)
        {
            MessageBox.Show(this,
                "高级模式已经集成所有功能，没有 RTX40MFG_config.json 配置文件。\n\n" +
                "「编辑多帧配置」功能仅对经典模式开放。\n\n" +
                "如需调整多帧生成参数，请在游戏内按 Home 键打开 ReShade 配置界面进行调节。",
                "仅经典模式可用", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrEmpty(_selectedGame.GameExeDirectory))
        {
            MessageBox.Show(this, "未找到游戏目录，无法编辑配置。", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var configPath = Path.Combine(_selectedGame.GameExeDirectory, "RTX40MFG_config.json");

        if (!File.Exists(configPath))
        {
            var result = MessageBox.Show(this,
                $"当前游戏目录下未找到 RTX40MFG_config.json 配置文件。\n\n" +
                $"可能原因：\n" +
                $"1. 尚未开启多帧生成（请先点击「一键开启」）\n" +
                $"2. 配置文件已被删除\n\n" +
                $"是否创建一个默认配置文件？",
                "配置文件不存在", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            // 创建默认配置
            try
            {
                var defaultConfig = new MfgConfig();
                var json = System.Text.Json.JsonSerializer.Serialize(defaultConfig,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"创建配置文件失败：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        using var editor = new ConfigEditorForm(configPath);
        editor.IsLightTheme = _currentTheme == "Light";
        editor.ShowDialog(this);

        // 如果保存了配置，刷新状态
        if (editor.Saved)
        {
            SetStatus($"《{_selectedGame.Name}》配置已更新，重启游戏后生效", SuccessColor);
        }
    }

    private async Task ApplyPatchAsync()
    {
        if (_selectedGame == null || _isBusy) return;

        // 检测显卡系列
        var nvidiaSeries = FilePatcher.DetectNvidiaSeries();
        var gpuType = FilePatcher.DetectGpuType();

        // 弹出模式选择对话框（根据显卡系列和游戏类型动态显示选项）
        var mode = ShowModeSelectDialog(nvidiaSeries, gpuType, _selectedGame.IsREEngine);
        if (mode == 0) return; // 用户取消

        _isBusy = true;
        SetBusyUI(true);

        try
        {
            PatchResult result;
            var progress = new Progress<string>(msg => SetStatus(msg));

            switch (mode)
            {
                case 1: // 经典模式（RTX40系）
                    SetStatus($"正在为《{_selectedGame.Name}》打经典模式补丁...");
                    result = await Task.Run(() => _patcher.ApplyPatch(_selectedGame, progress));
                    break;
                case 2: // 高级模式（RTX40系）
                    SetStatus($"正在为《{_selectedGame.Name}》打高级模式补丁...");
                    result = await Task.Run(() => _patcher.ApplyAdvancedPatch(_selectedGame, progress));
                    break;
                case 3: // RTX20系多帧生成
                    SetStatus($"正在为《{_selectedGame.Name}》打RTX20系多帧生成补丁...");
                    result = await Task.Run(() => _patcher.ApplyRTX20Patch(_selectedGame, progress));
                    break;
                case 4: // RTX30系多帧生成
                    SetStatus($"正在为《{_selectedGame.Name}》打RTX30系多帧生成补丁...");
                    result = await Task.Run(() => _patcher.ApplyRTX30Patch(_selectedGame, progress));
                    break;
                case 5: // RE引擎多帧生成
                    SetStatus($"正在为《{_selectedGame.Name}》打RE引擎多帧生成补丁...");
                    result = await Task.Run(() => _patcher.ApplyREFrameGenPatch(_selectedGame, progress));
                    break;
                default:
                    return;
            }

            if (result.Success)
            {
                SetStatus($"成功：{result.Message}", SuccessColor);
                MessageBox.Show(this, result.Message, "开启成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                SetStatus($"失败：{result.Message}", Color.FromArgb(255, 100, 100));
                MessageBox.Show(this, result.Message, "开启失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            RefreshGameStatus();
        }
        catch (Exception ex)
        {
            SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
        }
        finally
        {
            _isBusy = false;
            SetBusyUI(false);
        }
    }

    /// <summary>
    /// 显示模式选择对话框（根据显卡系列和游戏类型动态显示选项）
    /// 返回：0=取消，1=经典模式，2=高级模式，3=RTX20系，4=RTX30系，5=RE引擎多帧生成
    /// </summary>
    private int ShowModeSelectDialog(string nvidiaSeries, string gpuType, bool isREEngine)
    {
        using var dialog = new Form
        {
            Text = "选择开启模式",
            Size = new Size(600, 460),
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI Variable Text", 9.5f),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false
        };

        var titleLabel = new Label
        {
            Text = "选择开启模式",
            Font = new Font("Segoe UI Variable Display", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 255, 255),
            AutoSize = true,
            Location = new Point(24, 18)
        };

        string gpuInfo = gpuType == "NVIDIA" ? $"检测到显卡：{nvidiaSeries}" : $"检测到显卡：{gpuType}";
        var subtitleLabel = new Label
        {
            Text = $"为《{_selectedGame?.Name ?? "所选游戏"}》选择多帧生成开启方式\n{gpuInfo}" + (isREEngine ? "（RE引擎游戏）" : ""),
            ForeColor = Color.FromArgb(160, 160, 160),
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(26, 46)
        };

        int selectedMode = 0;
        var buttons = new List<Button>();

        // 根据显卡系列添加对应按钮
        if (nvidiaSeries == "RTX40" || nvidiaSeries == "RTX50")
        {
            // RTX40/50系：经典模式 + 高级模式
            var classicBtn = CreateModeButton("经典模式",
                "适合老驱动（616.56之前的驱动）\n只对310.8及之前的帧生成版本有效\n普遍用于不需要折腾、即装即用的游戏\n已更新到最新驱动的建议使用高级模式",
                Color.FromArgb(0, 120, 212), () => { selectedMode = 1; dialog.Close(); });
            buttons.Add(classicBtn);

            var advancedBtn = CreateModeButton("高级模式（推荐）",
                "适合新驱动\n适合310.9及以下的帧生成版本有效\n目前兼容性最强\n（部分游戏需要自行修改配置才能进入游戏）",
                Color.FromArgb(147, 51, 234), () => { selectedMode = 2; dialog.Close(); });
            buttons.Add(advancedBtn);
        }
        else if (nvidiaSeries == "RTX30")
        {
            // RTX30系：RTX30系多帧生成
            var rtx30Btn = CreateModeButton("RTX30系多帧生成",
                "专为RTX30系显卡优化的多帧生成方案\n安装后即可体验多帧生成功能\n如有问题可使用一键还原恢复",
                Color.FromArgb(34, 197, 94), () => { selectedMode = 4; dialog.Close(); });
            buttons.Add(rtx30Btn);
        }
        else if (nvidiaSeries == "RTX20")
        {
            // RTX20系：RTX20系多帧生成
            var rtx20Btn = CreateModeButton("RTX20系多帧生成",
                "专为RTX20系显卡优化的多帧生成方案\n安装后即可体验多帧生成功能\n如有问题可使用一键还原恢复",
                Color.FromArgb(249, 115, 22), () => { selectedMode = 3; dialog.Close(); });
            buttons.Add(rtx20Btn);
        }
        else
        {
            // 未知显卡系列：显示经典+高级（兜底）
            var classicBtn = CreateModeButton("经典模式",
                "适合老驱动（616.56之前的驱动）\n只对310.8及之前的帧生成版本有效",
                Color.FromArgb(0, 120, 212), () => { selectedMode = 1; dialog.Close(); });
            buttons.Add(classicBtn);

            var advancedBtn = CreateModeButton("高级模式（推荐）",
                "适合新驱动，兼容性最强\n（部分游戏需要自行修改配置）",
                Color.FromArgb(147, 51, 234), () => { selectedMode = 2; dialog.Close(); });
            buttons.Add(advancedBtn);
        }

        // RE引擎游戏：添加RE引擎多帧生成选项
        if (isREEngine)
        {
            var reBtn = CreateModeButton("RE引擎多帧生成",
                "专为RE引擎游戏优化的多帧生成方案\n需要dinput8.dll作为RE框架\n生化危机系列、鬼泣5等RE引擎游戏适用",
                Color.FromArgb(236, 72, 153), () => { selectedMode = 5; dialog.Close(); });
            buttons.Add(reBtn);
        }

        // 动态布局按钮
        int btnWidth = buttons.Count <= 2 ? 250 : 170;
        int btnHeight = 200;
        int startX = 24;
        int gap = buttons.Count <= 2 ? 30 : 15;
        int yPos = 90;

        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].Size = new Size(btnWidth, btnHeight);
            buttons[i].Location = new Point(startX + i * (btnWidth + gap), yPos);
            dialog.Controls.Add(buttons[i]);
        }

        // 调整对话框高度
        dialog.Height = 380;

        var hintLabel = new Label
        {
            Text = "提示：所有模式都可以通过「一键还原」恢复",
            ForeColor = Color.FromArgb(120, 120, 120),
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = true,
            Location = new Point(24, 310)
        };

        var cancelBtn = new Button
        {
            Text = "取消",
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(80, 32),
            Location = new Point(480, 300),
            Cursor = Cursors.Hand
        };
        cancelBtn.FlatAppearance.BorderSize = 0;
        cancelBtn.Click += (_, _) => { selectedMode = 0; dialog.Close(); };

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(subtitleLabel);
        dialog.Controls.Add(hintLabel);
        dialog.Controls.Add(cancelBtn);

        ApplyThemeToForm(dialog);
        dialog.ShowDialog(this);
        return selectedMode;
    }

    /// <summary>
    /// 创建模式选择按钮
    /// </summary>
    private Button CreateModeButton(string title, string description, Color backColor, Action onClick)
    {
        var btn = new Button
        {
            Text = $"{title}\n\n{description}",
            Font = new Font("Segoe UI Variable Text", 8.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = backColor,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private async Task RestorePatchAsync()
    {
        if (_selectedGame == null || _isBusy) return;

        var hasBackup = FilePatcher.HasBackup(_selectedGame);
        var isAnyPatched = _selectedGame.IsPatched || _selectedGame.IsAdvancedPatched || _selectedGame.IsCyberpunkPatched || _selectedGame.IsREFrameGenPatched;
        bool isForceRestore = !hasBackup;

        if (isForceRestore)
        {
            // 无责还原：严重警告
            var warning = "⚠️ 无责还原严重警告 ⚠️\n\n" +
                          "本功能用于删除您手动添加到游戏目录的补丁文件。\n\n" +
                          "【重要提醒】\n" +
                          "1. 本软件只提供删除功能，不提供恢复游戏源文件的功能！\n" +
                          "2. 使用后等同于删除游戏的源文件，可能导致游戏无法运行！\n" +
                          "3. 如果是Steam或Epic平台的游戏，使用后请务必去验证游戏完整性！\n" +
                          "4. 如果是无法验证完整性的游戏，建议删除后重新开启多帧生成或DLSS5功能以便游戏能正常运行！\n\n" +
                          "确认要继续删除《" + _selectedGame.Name + "》目录中的补丁文件吗？";

            var confirm = MessageBox.Show(this, warning, "无责还原 - 严重警告",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            // 二次确认
            var confirm2 = MessageBox.Show(this,
                "再次确认：删除后无法恢复！\n\nSteam/Epic游戏请删除后验证完整性，其他游戏请重新安装补丁。\n\n确定要继续吗？",
                "最终确认", MessageBoxButtons.YesNo, MessageBoxIcon.Stop);
            if (confirm2 != DialogResult.Yes) return;

            _isBusy = true;
            SetBusyUI(true);
            SetStatus($"正在无责还原《{_selectedGame.Name}》...");

            try
            {
                var removedFiles = await Task.Run(() => FilePatcher.ForceRemovePatchFiles(_selectedGame));

                if (removedFiles.Count > 0)
                {
                    var fileList = string.Join("\n", removedFiles.Take(20));
                    if (removedFiles.Count > 20)
                        fileList += $"\n... 等共 {removedFiles.Count} 个文件";

                    SetStatus($"无责还原完成，已删除 {removedFiles.Count} 个文件", Color.FromArgb(255, 150, 50));
                    MessageBox.Show(this,
                        $"无责还原完成！\n\n已删除 {removedFiles.Count} 个补丁文件：\n\n{fileList}\n\n" +
                        "【后续操作】\n" +
                        "• Steam/Epic游戏：请在平台中验证游戏完整性\n" +
                        "• 其他游戏：建议重新开启多帧生成或DLSS5功能",
                        "无责还原完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus("未找到可删除的补丁文件", Color.FromArgb(255, 150, 50));
                    MessageBox.Show(this, "未在游戏目录中找到补丁文件。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                RefreshGameStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
            }
            finally
            {
                _isBusy = false;
                SetBusyUI(false);
            }
        }
        else
        {
            // 正常一键还原
            var confirm = MessageBox.Show(this,
                $"即将还原《{_selectedGame.Name}》到打补丁前的状态，确认继续吗？",
                "确认还原", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            _isBusy = true;
            SetBusyUI(true);
            SetStatus($"正在还原《{_selectedGame.Name}》...");

            try
            {
                var progress = new Progress<string>(msg => SetStatus(msg));
                var result = await Task.Run(() => _patcher.RestorePatch(_selectedGame, progress));

                if (result.Success)
                {
                    SetStatus($"成功：{result.Message}", SuccessColor);
                    MessageBox.Show(this, result.Message, "还原成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus($"失败：{result.Message}", Color.FromArgb(255, 100, 100));
                    MessageBox.Show(this, result.Message, "还原失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                RefreshGameStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
            }
            finally
            {
                _isBusy = false;
                SetBusyUI(false);
            }
        }
    }

    /// <summary>
    /// 排错修复：根据游戏模式自动选择经典/高级排错补丁
    /// </summary>
    private async Task ApplyTroubleshootAsync()
    {
        if (_selectedGame == null || _isBusy) return;

        var isAdvanced = _selectedGame.IsAdvancedPatched;
        var modeName = isAdvanced ? "高级模式" : "经典模式";

        var confirm = MessageBox.Show(this,
            $"即将为《{_selectedGame.Name}》执行{modeName}排错修复：\n\n" +
            $"• 自动扫描游戏目录中所有 sl.*.dll 文件\n" +
            $"• 仅替换游戏中已存在的同名文件（不会新增文件）\n" +
            $"• 使用{modeName}专用的排错补丁\n" +
            $"• 替换前自动备份原文件\n\n" +
            $"适用于：开启多帧生成后仍然没有生效的情况。\n\n" +
            $"确认继续吗？",
            $"{modeName}排错修复确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _isBusy = true;
        SetBusyUI(true);
        SetStatus($"正在为《{_selectedGame.Name}》执行{modeName}排错修复...");

        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            TroubleshootResult result;

            if (isAdvanced)
            {
                result = await Task.Run(() => _patcher.ApplyAdvancedTroubleshoot(_selectedGame, progress));
            }
            else
            {
                result = await Task.Run(() => _patcher.ApplyTroubleshoot(_selectedGame, progress));
            }

            if (result.Success)
            {
                SetStatus($"{modeName}排错修复完成，共替换 {result.ReplacedFiles.Count} 个文件", SuccessColor);
                MessageBox.Show(this, result.Message, $"{modeName}排错修复完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                SetStatus($"{modeName}排错修复：{result.Message}", WarningColor);
                MessageBox.Show(this, result.Message, $"{modeName}排错修复",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
            MessageBox.Show(this, $"排错修复时发生错误：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isBusy = false;
            SetBusyUI(false);
        }
    }

    /// <summary>
    /// 赛博朋克2077专用补丁
    /// </summary>
    private async Task ApplyCyberpunk2077PatchAsync()
    {
        if (_selectedGame == null || _isBusy) return;

        var confirm = MessageBox.Show(this,
            $"即将为《{_selectedGame.Name}》安装赛博朋克2077专用补丁：\n\n" +
            $"• 将所有专用文件复制到游戏 bin\\x64 目录\n" +
            $"• 替换前自动备份原文件\n" +
            $"• 安装完成后会显示使用说明\n\n" +
            $"注意：此功能专为赛博朋克2077设计，其他游戏请勿使用。\n\n" +
            $"确认继续吗？",
            "2077专用补丁确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _isBusy = true;
        SetBusyUI(true);
        SetStatus($"正在为《{_selectedGame.Name}》安装2077专用补丁...");

        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var result = await Task.Run(() => _patcher.ApplyCyberpunk2077Patch(_selectedGame, progress));

            if (result.Success)
            {
                SetStatus("2077专用补丁安装成功", SuccessColor);
                MessageBox.Show(this, result.Message, "2077专用补丁安装成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                SetStatus($"安装失败：{result.Message}", Color.FromArgb(255, 100, 100));
                MessageBox.Show(this, result.Message, "安装失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            RefreshGameStatus();
        }
        catch (Exception ex)
        {
            SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
            MessageBox.Show(this, $"安装时发生错误：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isBusy = false;
            SetBusyUI(false);
        }
    }

    /// <summary>
    /// DLSS5补丁：开启/还原切换
    /// </summary>
    private async Task ToggleDLSS5Async()
    {
        if (_selectedGame == null)
        {
            MessageBox.Show(this, "请先选择一个游戏。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrEmpty(_selectedGame.GameExeDirectory) ||
            !Directory.Exists(_selectedGame.GameExeDirectory))
        {
            MessageBox.Show(this, "无法确定游戏运行目录，请先扫描游戏。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_isBusy) return;

        // 如果已开启DLSS5，执行还原
        if (_selectedGame.IsDLSS5Patched)
        {
            var confirm = MessageBox.Show(this,
                $"确认要为《{_selectedGame.Name}》还原DLSS5补丁吗？\n\n" +
                "还原后将删除所有DLSS5补丁文件，游戏恢复到未开启DLSS5的状态。",
                "还原DLSS5确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _isBusy = true;
            SetBusyUI(true);
            SetStatus($"正在为《{_selectedGame.Name}》还原DLSS5补丁...");

            try
            {
                var result = await Task.Run(() => _patcher.RestoreDLSS5(_selectedGame));
                if (result.Success)
                {
                    SetStatus("DLSS5补丁还原成功", SuccessColor);
                    MessageBox.Show(this, result.Message, "还原成功",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus($"还原失败：{result.Message}", Color.FromArgb(255, 100, 100));
                    MessageBox.Show(this, result.Message, "还原失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                RefreshGameStatus();
            }
            catch (Exception ex)
            {
                SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
                MessageBox.Show(this, $"还原时发生错误：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isBusy = false;
                SetBusyUI(false);
            }
            return;
        }

        // 未开启DLSS5，先选择方案
        var isREEngine = FilePatcher.DetectREEngineGame(_selectedGame);
        string selectedMode = "default"; // default / reengine

        if (isREEngine)
        {
            // RE引擎游戏（包括生化危机系列）：弹出RE引擎通用方案
            selectedMode = ShowDlss5ModeSelector("reengine");
            if (string.IsNullOrEmpty(selectedMode))
                return; // 用户取消了
        }
        else
        {
            // 非RE引擎游戏：检测显卡类型，使用通用方案
            var gpuType = FilePatcher.DetectGpuType();
            if (gpuType == "Unknown")
            {
                MessageBox.Show(this,
                    "无法检测到显卡类型，请确认已安装NVIDIA或AMD显卡驱动。\n\nDLSS5需要NVIDIA RTX显卡或AMD RX 7000系/9000系显卡。",
                    "无法检测显卡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // A卡需要检测是否为7000系或9000系，其他系列不支持DLSS5
            if (gpuType == "AMD" && !FilePatcher.IsAmd9000Series())
            {
                MessageBox.Show(this,
                    "❌ 您的AMD显卡不支持DLSS5！\n\n" +
                    "目前DLSS5仅支持AMD RX 7000系和9000系显卡。\n" +
                    "您的显卡不在支持范围内，无法使用DLSS5功能。",
                    "显卡不支持DLSS5",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            selectedMode = "default";
        }

        // 执行安装
        await InstallDlss5PatchAsync(selectedMode);
    }

    /// <summary>
    /// 切换DX9 DLSS5补丁（开启/还原）
    /// </summary>
    private async Task ToggleDX9DLSS5Async()
    {
        if (_selectedGame == null)
        {
            MessageBox.Show(this, "请先选择一个游戏。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrEmpty(_selectedGame.GameExePath) ||
            !File.Exists(_selectedGame.GameExePath))
        {
            MessageBox.Show(this, "无法确定游戏exe路径，请先扫描游戏。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_isBusy) return;

        // 如果已开启DX9 DLSS5，弹出选择菜单
        if (_selectedGame.IsDX9DLSS5Patched)
        {
            // 根据当前主题设置颜色
            bool isLight = _currentTheme == "Light";
            var bgColor = isLight ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
            var textColor = isLight ? Color.FromArgb(30, 30, 30) : Color.White;
            var btnCancelBg = isLight ? Color.FromArgb(220, 220, 220) : Color.FromArgb(80, 80, 80);
            var btnCancelText = isLight ? Color.FromArgb(30, 30, 30) : Color.White;

            // 创建自定义选择对话框
            using var form = new Form
            {
                Text = "DX9 DLSS5 操作选择",
                Size = new Size(460, 260),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = bgColor
            };

            var label = new Label
            {
                Text = $"《{_selectedGame.Name}》已开启DX9 DLSS5\n\n请选择要执行的操作：",
                Location = new Point(25, 20),
                Size = new Size(400, 60),
                ForeColor = textColor,
                Font = new Font("微软雅黑", 10),
                BackColor = Color.Transparent
            };

            var btnRestore = new Button
            {
                Text = "还原DX9 DLSS5",
                Location = new Point(25, 95),
                Size = new Size(185, 42),
                BackColor = Color.FromArgb(220, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 10),
                DialogResult = DialogResult.Yes,
                Cursor = Cursors.Hand
            };
            btnRestore.FlatAppearance.BorderSize = 0;

            var btnMoveDll = new Button
            {
                Text = "替换dll位置再测试",
                Location = new Point(230, 95),
                Size = new Size(185, 42),
                BackColor = Color.FromArgb(249, 115, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 10),
                DialogResult = DialogResult.No,
                Cursor = Cursors.Hand
            };
            btnMoveDll.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(175, 155),
                Size = new Size(90, 34),
                BackColor = btnCancelBg,
                ForeColor = btnCancelText,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 9),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            form.Controls.AddRange(new Control[] { label, btnRestore, btnMoveDll, btnCancel });
            form.AcceptButton = btnRestore;
            form.CancelButton = btnCancel;

            var choice = form.ShowDialog(this);

            if (choice == DialogResult.Cancel) return;

            _isBusy = true;
            SetBusyUI(true);

            if (choice == DialogResult.Yes)
            {
                // 还原DX9 DLSS5
                SetStatus($"正在为《{_selectedGame.Name}》还原DX9 DLSS5补丁...");
                try
                {
                    var result = await Task.Run(() => _patcher.RestoreDX9DLSS5(_selectedGame));
                    if (result.Success)
                    {
                        SetStatus("DX9 DLSS5补丁还原成功", SuccessColor);
                        MessageBox.Show(this, result.Message, "还原成功",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        SetStatus($"还原失败：{result.Message}", Color.FromArgb(255, 100, 100));
                        MessageBox.Show(this, result.Message, "还原失败",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    RefreshGameStatus();
                }
                catch (Exception ex)
                {
                    SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
                    MessageBox.Show(this, $"还原时发生错误：{ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _isBusy = false;
                    SetBusyUI(false);
                }
            }
            else if (choice == DialogResult.No)
            {
                // 替换dll位置再测试
                SetStatus($"正在为《{_selectedGame.Name}》移动dgvoodoo到dll运行目录...");
                try
                {
                    var dllDir = FilePatcher.FindDllRuntimeDirectory(_selectedGame);
                    var dllDirDisplay = string.IsNullOrEmpty(dllDir) ? "未找到" : dllDir;

                    var confirmMove = MessageBox.Show(this,
                        $"检测到游戏的dll运行目录：\n{dllDirDisplay}\n\n" +
                        $"将把dgvoodoo的三个文件（D3D9.dll、dgVoodoo.conf、dgVoodooCpl.exe）\n" +
                        $"从游戏exe目录移动到上述目录。\n\n" +
                        $"确认移动吗？",
                        "替换dll位置确认",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (confirmMove != DialogResult.Yes)
                    {
                        _isBusy = false;
                        SetBusyUI(false);
                        return;
                    }

                    var result = await Task.Run(() => _patcher.MoveDgVoodooToDllDirectory(_selectedGame));
                    if (result.Success)
                    {
                        SetStatus("dgvoodoo文件移动成功", SuccessColor);
                        MessageBox.Show(this, result.Message, "移动成功",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        SetStatus($"移动失败：{result.Message}", Color.FromArgb(255, 200, 100));
                        MessageBox.Show(this, result.Message, "提示",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
                    MessageBox.Show(this, $"移动时发生错误：{ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _isBusy = false;
                    SetBusyUI(false);
                }
            }
            return;
        }

        // 未开启DX9 DLSS5，执行安装
        var gpuType = FilePatcher.DetectGpuType();
        if (!gpuType.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this,
                $"❌ DX9 DLSS5目前仅支持NVIDIA显卡！\n\n" +
                $"您的显卡是{gpuType}，无法使用DX9 DLSS5功能。\n" +
                $"（DX9 DLSS5需要NVIDIA NGX运行时支持）",
                "显卡不支持DX9 DLSS5",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var is32Bit = FilePatcher.Is32BitExecutable(_selectedGame.GameExePath!);
        var bitness = is32Bit ? "32位" : "64位";

        var confirmInstall = MessageBox.Show(this,
            $"确认要为《{_selectedGame.Name}》开启DX9 DLSS5吗？\n\n" +
            $"游戏类型：{bitness} DX9游戏\n" +
            $"将自动安装：dgVoodoo2 + ReShade + DLSS5-Feeder + LumeniteFX\n\n" +
            $"注意：\n" +
            $"1. DX9游戏只能使用DLAA模式，无法超分辨率\n" +
            $"2. 游戏外配置将自动完成，启动游戏后按Home键开启效果\n" +
            $"3. 首次启动可能需要等待几秒加载插件",
            "开启DX9 DLSS5确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirmInstall != DialogResult.Yes) return;

        _isBusy = true;
        SetBusyUI(true);
        SetStatus($"正在为《{_selectedGame.Name}》安装DX9 DLSS5补丁...");

        try
        {
            var result = await Task.Run(() => _patcher.InstallDX9DLSS5(_selectedGame));
            if (result.Success)
            {
                SetStatus("DX9 DLSS5补丁安装成功", SuccessColor);
                MessageBox.Show(this, result.Message, "安装成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                SetStatus($"安装失败：{result.Message}", Color.FromArgb(255, 100, 100));
                MessageBox.Show(this, result.Message, "安装失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshGameStatus();
        }
        catch (Exception ex)
        {
            SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
            MessageBox.Show(this, $"安装时发生错误：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isBusy = false;
            SetBusyUI(false);
        }
    }

    // 用于记录方案选择弹窗的结果
    private bool _dlss5ModeSelectorResult = true;
    private string _dlss5ModeSelected = "default"; // default / reengine

    /// <summary>
    /// 显示DLSS5方案选择弹窗
    /// gameType: "reengine"（RE引擎游戏，只显示RE通用）
    /// </summary>
    private string ShowDlss5ModeSelector(string gameType)
    {
        _dlss5ModeSelectorResult = true;
        _dlss5ModeSelected = "reengine"; // 默认选中RE引擎通用

        using var dialog = new Form
        {
            Text = "选择DLSS5方案",
            Size = new Size(420, 280),
            BackColor = _currentTheme == "Light" ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32),
            ForeColor = _currentTheme == "Light" ? Color.FromArgb(30, 30, 30) : Color.White,
            Font = new Font("Segoe UI Variable Text", 9.5f),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var titleLabel = new Label
        {
            Text = "检测到RE引擎游戏",
            Font = new Font("Segoe UI Variable Display", 14f, FontStyle.Bold),
            ForeColor = _currentTheme == "Light" ? Color.FromArgb(30, 30, 30) : Color.White,
            AutoSize = true,
            Location = new Point(24, 18)
        };

        var hintLabel = new Label
        {
            Text = "检测到您选择的是RE引擎游戏（包括生化危机系列）。\n\n" +
                   "将使用RE引擎通用方案开启DLSS5。\n" +
                   "包含ReShade框架 + RE引擎专属补丁。",
            ForeColor = _currentTheme == "Light" ? Color.FromArgb(80, 80, 80) : Color.FromArgb(200, 200, 200),
            Location = new Point(24, 55),
            Size = new Size(360, 90)
        };

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(hintLabel);

        // 确认按钮（RE引擎通用）
        var confirmBtn = new UI.ModernButton
        {
            Text = "开启DLSS5（RE引擎通用）",
            AccentColor = Color.FromArgb(0, 120, 212),
            HoverColor = Color.FromArgb(20, 130, 220),
            PressedColor = Color.FromArgb(0, 100, 190),
            Size = new Size(240, 45),
            Location = new Point(80, 155),
            IsLightTheme = _currentTheme == "Light"
        };
        confirmBtn.Click += (_, _) =>
        {
            _dlss5ModeSelected = "reengine";
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        dialog.Controls.Add(confirmBtn);

        // 取消按钮
        var cancelBtn = new UI.ModernButton
        {
            Text = "取消",
            IsSecondary = true,
            Size = new Size(100, 36),
            Location = new Point(290, 210),
            IsLightTheme = _currentTheme == "Light"
        };
        cancelBtn.Click += (_, _) =>
        {
            _dlss5ModeSelectorResult = false;
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        };
        dialog.Controls.Add(cancelBtn);

        dialog.ShowDialog(this);

        if (!_dlss5ModeSelectorResult)
            return "";

        return _dlss5ModeSelected;
    }

    /// <summary>
    /// 执行DLSS5补丁安装
    /// </summary>
    private async Task InstallDlss5PatchAsync(string selectedMode)
    {
        // 确认安装
        string confirmMessage;
        bool useREEngineMode = selectedMode == "reengine";

        if (useREEngineMode)
        {
            confirmMessage = $"确认要为《{_selectedGame!.Name}》开启DLSS5（RE引擎通用）吗？\n\n" +
                           $"将安装RE引擎通用DLSS5补丁到：\n{_selectedGame.GameExeDirectory}\n\n" +
                           "⚠️ 重要警告：\n" +
                           "1. 开启DLSS5后，请勿开启游戏自带帧生成，否则会闪退！\n" +
                           "2. 本软件的多帧生成功能将不可用（与DLSS5冲突）\n" +
                           "3. 建议使用DLSS5自带的AI插帧或NVIDIA的AI插帧\n\n" +
                           "开启DLSS5后，游戏中需要在设置里手动开启DLSS超分辨率功能。";
        }
        else
        {
            var gpuType = FilePatcher.DetectGpuType();
            var gpuName = gpuType == "NVIDIA" ? "NVIDIA" : "AMD";
            confirmMessage = $"确认要为《{_selectedGame!.Name}》开启DLSS5吗？\n\n" +
                           $"检测到您的显卡：{gpuName}\n\n" +
                           $"将安装DLSS5（{gpuType}）补丁到：\n{_selectedGame.GameExeDirectory}\n\n" +
                           (gpuType == "AMD" ? "注意：A卡安装完成后会自动打开设置程序，请按照提示完成设置。\n\n" : "") +
                           "开启DLSS5后，游戏中需要在设置里手动开启DLSS帧生成功能。";
        }

        var installConfirm = MessageBox.Show(this, confirmMessage, "开启DLSS5确认",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (installConfirm != DialogResult.Yes) return;

        _isBusy = true;
        SetBusyUI(true);
        SetStatus($"正在为《{_selectedGame.Name}》安装DLSS5补丁...");

        try
        {
            var result = await Task.Run(() => _patcher.InstallDLSS5(_selectedGame, useREEngineMode));
            if (result.Success)
            {
                SetStatus("DLSS5补丁安装成功", SuccessColor);
                MessageBox.Show(this, result.Message, "安装成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                // A卡通用模式安装成功后，弹出FSR3.0注意事项
                if (!useREEngineMode && FilePatcher.DetectGpuType() == "AMD")
                {
                    MessageBox.Show(this,
                        "⚠️ AMD显卡DLSS5使用注意事项 ⚠️\n\n" +
                        "AMD显卡较为特殊，需要开启FSR3.0以上才会启动DLSS5\n" +
                        "（目前方案，如果有新的方案能解决办法就会更新）\n\n" +
                        "❌ 重要警告：请勿选择FSR2.0！\n" +
                        "否则会导致游戏崩溃，并且后续无论是重启电脑和重装游戏\n" +
                        "都无法再打开游戏！\n\n" +
                        "如果不小心使用了FSR2.0导致游戏崩溃，作者正在寻找解决办法，\n" +
                        "找到后会更新软件加入修复功能。\n\n" +
                        "请在游戏设置中选择 FSR3.0 或更高版本，切勿选择FSR2.0！",
                        "AMD显卡DLSS5重要提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // RE引擎模式安装成功后，弹出帧生成注意事项
                if (useREEngineMode)
                {
                    MessageBox.Show(this,
                        "⚠️ RE引擎DLSS5使用注意事项 ⚠️\n\n" +
                        "1. 开启DLSS5之后，请勿开启游戏自带的帧生成功能！\n" +
                        "   游戏自带帧生成与DLSS5 MOD冲突，会导致游戏闪退。\n\n" +
                        "2. 本软件的多帧生成功能已自动禁用（与DLSS5冲突）。\n" +
                        "   如需使用多帧生成，请先还原DLSS5。\n\n" +
                        "3. 建议使用以下方式实现帧生成：\n" +
                        "   · DLSS5自带的AI插帧功能\n" +
                        "   · NVIDIA显卡的AI插帧功能（如适用）\n\n" +
                        "至于插帧是否生效，需要您自行进入游戏检测。\n\n" +
                        "如果后续有新的方案能解决游戏自带帧生成的问题，\n" +
                        "软件会及时更新。",
                        "RE引擎DLSS5重要提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                SetStatus($"安装失败：{result.Message}", Color.FromArgb(255, 100, 100));
                MessageBox.Show(this, result.Message, "安装失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshGameStatus();
        }
        catch (Exception ex)
        {
            SetStatus($"发生错误：{ex.Message}", Color.FromArgb(255, 100, 100));
            MessageBox.Show(this, $"安装时发生错误：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isBusy = false;
            SetBusyUI(false);
        }
    }

    /// <summary>
    /// 配置名修改功能（仅高级模式可用）
    /// </summary>
    private void OpenConfigNameEditor()
    {
        if (_selectedGame == null || string.IsNullOrEmpty(_selectedGame.GameExeDirectory)) return;

        // 打开前先弹出提示
        var notice = MessageBox.Show(this,
            "在使用该功能前请确认：\n\n" +
            "您使用了高级模式后，游戏出现了报错、非法模块、无法开启多帧生成等问题，才建议使用本功能。\n\n" +
            "如果没有问题，一切运行良好，请保持默认配置，不要修改！\n\n" +
            "确认继续吗？",
            "配置名修改 - 使用前提示", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (notice != DialogResult.Yes) return;

        var exeDir = _selectedGame.GameExeDirectory;
        var configNames = new[] { "dinput8", "d3d11", "winmm", "d3d9", "winhttp", "wininet",
                                  "dsound", "binkw64", "xinput1_3", "bink2w64", "xinput1_4", "xinputuap" };

        using var dialog = new Form
        {
            Text = "配置名称修改 - " + _selectedGame.Name,
            Size = new Size(680, 520),
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI Variable Text", 9.5f),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false
        };

        var titleLabel = new Label
        {
            Text = "配置名称修改",
            Font = new Font("Segoe UI Variable Display", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(20, 14)
        };

        var hintLabel = new Label
        {
            Text = "根据游戏错误报告自行修改，直到某一个配置名称可以进入游戏\n修改 version.dll 和 version.ini 的配置名称，例如：version.dll → winmm.dll",
            ForeColor = Color.FromArgb(160, 160, 160),
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = false,
            Size = new Size(620, 36),
            Location = new Point(22, 42)
        };

        // dxgi 区域（放在上面）
        var dxgiHintLabel = new Label
        {
            Text = "修改 dxgi 的配置名称（dxgi.dll 只能改成 d3d12.dll）",
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 90)
        };

        var dxgiBtn = new Button
        {
            Text = "将 dxgi.dll 改成 d3d12.dll",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(83, 7, 178),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(260, 40),
            Location = new Point(22, 112),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold)
        };
        dxgiBtn.FlatAppearance.BorderSize = 0;
        dxgiBtn.Click += (_, _) =>
        {
            var targetDll = Path.Combine(exeDir, "d3d12.dll");
            if (File.Exists(targetDll))
            {
                MessageBox.Show(dialog,
                    "检测到游戏目录中已存在 d3d12.dll 文件！\n\n请勿更改 dxgi.dll 的配置名称！",
                    "文件冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(dialog,
                "确认将 dxgi.dll 修改为 d3d12.dll？\n\n" +
                "注意：dxgi.dll 只能改成 d3d12.dll。\n\n" +
                "修改后请自行启动游戏检测是否能正常进入。",
                "确认修改 dxgi.dll", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                var dxgiDll = Path.Combine(exeDir, "dxgi.dll");
                if (File.Exists(dxgiDll))
                {
                    File.Move(dxgiDll, targetDll);
                    MessageBox.Show(dialog,
                        "修改成功！\n\n已将 dxgi.dll → d3d12.dll\n\n请启动游戏检测是否能正常进入。",
                        "修改成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(dialog, "未找到 dxgi.dll 文件，可能已经被修改过了。",
                        "文件不存在", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(dialog, $"修改失败：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // version 配置名按钮网格（6列2行）
        var versionHintLabel = new Label
        {
            Text = "修改 version 配置名（version.dll + version.ini）",
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 170)
        };

        const int cfgBtnW = 96;
        const int cfgBtnH = 38;
        const int cfgBtnGap = 10;
        int[] cfgBtnXs = { 22, 128, 234, 340, 446, 552 };
        int cfgBtnY1 = 192;
        int cfgBtnY2 = 240;

        for (int i = 0; i < configNames.Length; i++)
        {
            var name = configNames[i];
            int x = cfgBtnXs[i % 6];
            int y = i < 6 ? cfgBtnY1 : cfgBtnY2;

            var btn = new Button
            {
                Text = name,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(64, 167, 210),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(cfgBtnW, cfgBtnH),
                Location = new Point(x, y),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            var cfgName = name;
            btn.Click += (_, _) =>
            {
                // 检测同名文件
                var targetDll = Path.Combine(exeDir, cfgName + ".dll");
                var targetIni = Path.Combine(exeDir, cfgName + ".ini");
                var conflictFiles = new List<string>();
                if (File.Exists(targetDll)) conflictFiles.Add(cfgName + ".dll");
                if (File.Exists(targetIni)) conflictFiles.Add(cfgName + ".ini");

                if (conflictFiles.Count > 0)
                {
                    MessageBox.Show(dialog,
                        $"检测到游戏目录中已存在相同文件：\n{string.Join("、", conflictFiles)}\n\n" +
                        $"请勿更改为此配置名称，请选择其他配置名！",
                        "文件冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 确认修改
                var confirm = MessageBox.Show(dialog,
                    $"确认将 version.dll 和 version.ini 修改为：\n\n" +
                    $"  {cfgName}.dll\n  {cfgName}.ini\n\n" +
                    $"修改后请自行启动游戏检测是否能正常进入。\n" +
                    $"如果无法进入游戏，请回来继续更改其他配置名。",
                    $"确认修改为 {cfgName}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes) return;

                try
                {
                    var versionDll = Path.Combine(exeDir, "version.dll");
                    var versionIni = Path.Combine(exeDir, "version.ini");

                    if (File.Exists(versionDll))
                        File.Move(versionDll, targetDll);
                    if (File.Exists(versionIni))
                        File.Move(versionIni, targetIni);

                    MessageBox.Show(dialog,
                        $"修改成功！\n\n已将 version.dll → {cfgName}.dll\n已将 version.ini → {cfgName}.ini\n\n" +
                        $"请启动游戏检测是否能正常进入。",
                        "修改成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(dialog, $"修改失败：{ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            dialog.Controls.Add(btn);
        }

        // 当前配置状态
        var statusLabel = new Label
        {
            Text = "当前配置状态检测中...",
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = false,
            Size = new Size(620, 60),
            Location = new Point(22, 295)
        };
        UpdateConfigNameStatus(statusLabel, exeDir);

        // 底部按钮：使用前必看 + 取消
        var guideBtn = new Button
        {
            Text = "使用前必看！",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(255, 140, 0),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 38),
            Location = new Point(22, 410),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold)
        };
        guideBtn.FlatAppearance.BorderSize = 0;
        guideBtn.Click += (_, _) =>
        {
            MessageBox.Show(this,
                "【使用前必看】\n\n" +
                "• 鸣潮：使用了高级模式之后默认不需要更改配置名，但是需要绕过启动器启动，直接从游戏目录里开启游戏。\n\n" +
                "• 异环：需要修改 dxgi.dll 和两个 version 文件，建议将 version 改成 dsound。\n\n" +
                "• 明末：渊虚之羽：只需要修改 version 配置名称，需要自行检测哪一个配置名称可以开启多帧生成。\n\n" +
                "• 其他游戏：自行尝试各个配置名，直到能正常进入游戏并开启多帧生成。",
                "使用前必看", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var cancelBtn = new Button
        {
            Text = "取消",
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 38),
            Location = new Point(524, 410),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Variable Text", 9.5f)
        };
        cancelBtn.FlatAppearance.BorderSize = 0;
        cancelBtn.Click += (_, _) => { dialog.DialogResult = DialogResult.OK; dialog.Close(); };

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(hintLabel);
        dialog.Controls.Add(dxgiHintLabel);
        dialog.Controls.Add(dxgiBtn);
        dialog.Controls.Add(versionHintLabel);
        dialog.Controls.Add(statusLabel);
        dialog.Controls.Add(guideBtn);
        dialog.Controls.Add(cancelBtn);

        ApplyThemeToForm(dialog);
        dialog.ShowDialog(this);
        RefreshGameStatus();
    }

    /// <summary>
    /// 更新配置名状态显示
    /// </summary>
    private void UpdateConfigNameStatus(Label label, string exeDir)
    {
        var status = "当前配置状态：\n";
        var versionDll = Path.Combine(exeDir, "version.dll");
        var versionIni = Path.Combine(exeDir, "version.ini");
        var dxgiDll = Path.Combine(exeDir, "dxgi.dll");
        var d3d12Dll = Path.Combine(exeDir, "d3d12.dll");

        if (File.Exists(versionDll))
            status += "  • version.dll：存在（默认配置名）\n";
        else
        {
            // 查找已修改的version配置名
            var modified = Directory.GetFiles(exeDir, "*.dll")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .FirstOrDefault(n => n != "version" && File.Exists(Path.Combine(exeDir, n + ".ini")));
            status += $"  • version配置：已修改为 {modified ?? "未知"}\n";
        }

        if (File.Exists(dxgiDll))
            status += "  • dxgi.dll：存在（默认配置名）\n";
        else if (File.Exists(d3d12Dll))
            status += "  • dxgi配置：已修改为 d3d12.dll\n";
        else
            status += "  • dxgi.dll：未找到\n";

        label.Text = status;
    }

    #endregion

    #region UI 辅助方法

    private void SetStatus(string message, Color? color = null)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(message, color));
            return;
        }
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color ?? TextSecondary;
    }

    private void SetBusyUI(bool busy)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetBusyUI(busy));
            return;
        }
        _progressBar.Visible = busy;
        _scanButton.Enabled = !busy;
        _manualButton.Enabled = !busy;
        _refreshButton.Enabled = !busy;
        OnGameSelected();
    }

    #endregion

    #region 窗口过程（圆角窗口）

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            var hwnd = Handle;
            DwmSetWindowAttribute(hwnd, 33, new[] { 2 }, 4);
        }
        catch { }

        // 加载并应用用户保存的主题
        LoadThemeSetting();
        if (_currentTheme == "Light")
            ApplyTheme();

        // 显示启动提示（如果用户未选择"不再弹出"）
        ShowStartupNotice();

        // 新版本第一次启动时弹出更新内容
        CheckAndShowChangelogOnFirstRun();
    }

    /// <summary>
    /// 显示启动提示弹窗（小黑盒免费分享声明）
    /// </summary>
    private void ShowStartupNotice()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "settings.ini");
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                if (content.Contains("ShowStartupNotice=false"))
                    return; // 用户已选择不再弹出
            }
        }
        catch { }

        using var dialog = new Form
        {
            Text = "重要提示",
            Size = new Size(620, 420),
            MinimumSize = new Size(580, 380),
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI Variable Text", 9.5f),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.Sizable,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false
        };

        var titleLabel = new Label
        {
            Text = "重要提示",
            Font = new Font("Segoe UI Variable Display", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 200, 50),
            AutoSize = true,
            Location = new Point(24, 18)
        };

        var contentLabel = new Label
        {
            Text = "本软件目前只在小黑盒免费分享，请勿倒卖！\n\n" +
                   "如果您是在其他渠道购买本软件，请您立刻向渠道方申请退款！\n\n" +
                   "小黑盒链接（点击可跳转浏览器打开）：",
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI Variable Text", 9.5f),
            AutoSize = false,
            Size = new Size(560, 110),
            Location = new Point(24, 55)
        };

        var linkLabel = new LinkLabel
        {
            Text = "https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=d2b27269b51e&new_post_share_style=true",
            ForeColor = Color.FromArgb(100, 180, 255),
            LinkColor = Color.FromArgb(100, 180, 255),
            ActiveLinkColor = Color.FromArgb(150, 200, 255),
            VisitedLinkColor = Color.FromArgb(150, 120, 200),
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = false,
            Size = new Size(560, 60),
            Location = new Point(24, 165),
            LinkBehavior = LinkBehavior.AlwaysUnderline
        };
        linkLabel.LinkClicked += (_, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = linkLabel.Text,
                    UseShellExecute = true
                });
            }
            catch { }
        };

        var closeBtn = new Button
        {
            Text = "关闭",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 120, 212),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 38),
            Location = new Point(24, 320),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.Click += (_, _) => { dialog.DialogResult = DialogResult.OK; dialog.Close(); };

        var dontShowBtn = new Button
        {
            Text = "了解并不再弹出",
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(160, 38),
            Location = new Point(420, 320),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        dontShowBtn.FlatAppearance.BorderSize = 0;
        dontShowBtn.Click += (_, _) =>
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "settings.ini");
                File.WriteAllText(configPath, "ShowStartupNotice=false");
            }
            catch { }
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(contentLabel);
        dialog.Controls.Add(linkLabel);
        dialog.Controls.Add(closeBtn);
        dialog.Controls.Add(dontShowBtn);

        ApplyThemeToForm(dialog);
        dialog.ShowDialog(this);
    }

    #region 使用说明与更新内容

    /// <summary>使用说明文本</summary>
    private const string HelpText = @"〖大〗多帧生成+DLSS5开启工具 使用说明


〖大〗软件简介

本工具可以一键为支持DLSS的游戏开启多帧生成（MFG）和DLSS5功能，让RTX 40系及以下显卡也能体验多帧生成，同时支持N卡和A卡开启DLSS5。


〖大〗一、快速开始

1. 点击「自动扫描」按钮，软件会自动扫描Steam和Epic游戏库

2. 或者点击「手动添加」选择游戏文件夹

3. 在游戏列表中选中要操作的游戏

4. 点击下方的「开启多帧生成」或「开启DLSS5」按钮

5. 按照提示选择模式，等待安装完成即可


〖大〗二、多帧生成功能

〖小〗经典模式

适合老驱动（616.56之前的驱动）

只对310.8及之前的帧生成版本有效

即装即用，不需要折腾

适合不再/无法更新驱动的游戏

〖小〗高级模式

适合新驱动

对310.9及以下的帧生成版本有效

目前兼容性最强

部分游戏需要自行修改配置名才能进入游戏

〖小〗2077专用模式

仅适用于《赛博朋克2077》

经典模式和高级模式无法在2077中开启多帧生成

安装后请按照弹窗说明操作

〖小〗配置多帧生成（仅经典模式）

点击「配置多帧生成」可以调整：

生成倍率（2x/3x/4x）

低延迟模式

帧率限制

调试信息显示

〖小〗配置名修改（仅高级模式）

部分游戏（如异环、鸣潮、明末）使用高级模式后可能出现报错、非法模块、无法开启多帧生成等问题，此时可以修改配置名来解决：

点击「配置名修改」

选择一个配置名（如dsound、dinput8等）

确认修改后启动游戏测试

如果不行，换一个配置名继续测试

dxgi.dll只能改成d3d12.dll，有单独按钮

〖小〗使用前必看

鸣潮：默认不需要改配置名，但需绕过启动器直接启动游戏

异环：需要修改dxgi.dll和两个version文件，建议version改成dsound

明末：只需要修改version配置名，自行测试哪个可用

〖小〗排错修复

如果开启多帧生成后仍然无法使用，可以尝试排错修复

软件会自动替换游戏目录中已有的sl.开头的文件

注意：只替换游戏中已有的文件，不会新增


〖大〗三、DLSS5功能

〖小〗方案选择逻辑

软件会自动检测游戏类型，并显示对应的方案选项：

非RE引擎游戏：直接使用通用方案（自动检测N卡/A卡）

RE引擎游戏（鬼泣5、怪物猎人、生化危机等）：使用RE引擎通用方案

〖小〗通用方案（自动检测显卡）

适用于非RE引擎游戏，软件会自动检测显卡类型：

N卡：安装N卡专用补丁

A卡：安装A卡专用补丁（仅支持RX 7000系和9000系）

〖小〗A卡用户注意

安装完成后会自动打开dlssnr_on_amd_setup.exe

请按照程序提示完成设置

必须在游戏中开启FSR3.0以上才会启动DLSS5

不要选择FSR2.0，否则会导致游戏崩溃

崩溃后可能无法通过重启或重装游戏恢复

〖小〗RE引擎通用方案

检测到RE引擎游戏（生化危机、鬼泣5、怪物猎人等）时可用

适用于所有RE引擎游戏

包含ReShade框架 + RE引擎专属补丁（RE_DLSS5_Core.dll、version.dll等）

补丁会安装到游戏根目录（真正运行exe所在目录）

〖小〗DLSS5还原

点击「还原DLSS5」可以删除所有DLSS5补丁

DLSS5补丁都是新增文件，不会替换游戏原文件

还原后游戏目录完全恢复原样


〖大〗四、一键还原

点击「一键还原」可以：

1. 恢复被替换的nvngx_dlssg.dll等游戏原文件

2. 删除所有新增的补丁文件

3. 恢复排错替换的sl.开头文件

4. 清理所有配置名修改产生的文件

注意：还原前请确保游戏未在运行。


〖大〗五、游戏列表操作

〖小〗选中游戏

点击列表中的游戏即可选中，右侧会显示游戏详情

〖小〗右键菜单

在游戏列表中右键点击游戏，可以快速操作：

开启/还原多帧生成

开启/还原DLSS5

排错修复

配置多帧生成

配置名修改

2077专用补丁（仅2077显示）

按钮的可交互状态与主界面下方按钮一致

〖小〗状态显示

状态栏会显示当前游戏的补丁状态：

未开启：没有安装任何补丁

已开启（多帧生成）：仅安装了多帧生成

已开启（DLSS5）：仅安装了DLSS5

已开启（全部）：多帧生成和DLSS5都安装了

已开启（专属多帧生成）：2077专用补丁


〖大〗六、其他功能

〖小〗刷新状态

重新检测所有游戏的补丁状态

〖小〗检查更新

检测是否有新版本，支持夸克、百度、蓝奏云三渠道下载

〖小〗浅色/深色主题

点击右上角按钮切换主题，设置会自动保存

〖小〗自动扫描

扫描Steam和Epic游戏库中的所有游戏

支持帧生成的游戏：可以开启多帧生成和DLSS5

不支持帧生成的游戏：只能开启DLSS5，多帧生成按钮禁用

〖小〗手动添加

手动选择游戏文件夹添加到列表

即使游戏没有帧生成文件（nvngx_dlssg.dll），也可以添加并开启DLSS5

不支持帧生成的游戏，多帧生成相关按钮会禁用，只能开启DLSS5


〖大〗七、ReShade配置界面中文注释

开启多帧生成或DLSS5后，在游戏中按 Home 键可打开ReShade配置界面。

以下是RenoDX-DLSSNR和DLSS MFG两个插件的常用选项中文注释：


〖小〗RenoDX-DLSSNR（DLSS5神经渲染）

Enable DLSS Neural Rendering：启用DLSS神经渲染（总开关，开启后DLSS5生效）

Enable Upscaling：启用超分辨率（开启后同时启用DLSS超分辨率）

Neural Rendering：神经渲染（神经渲染总开关，建议开启）

Neural Uplift：神经提升（神经提升开关，建议与神经渲染一起开启）

NR Intensity：神经渲染强度（建议20-35%，数值越高效果越强但可能失真）

Local Structure：局部结构强度（建议10-25%，增强局部细节和纹理）

Local Tone：局部色调（建议低到中，控制局部明暗变化）

Color Influence：颜色影响（建议低，控制颜色变化程度）

UI Correction：界面校正（建议开启，防止游戏UI被神经渲染影响）

Debug View：调试视图（建议关闭，开启后显示调试信息）


〖小〗DLSS MFG（多帧生成）

Enabled：启用（多帧生成总开关）

Multiplier / MFG multiplier：帧生成倍率（选择2倍/3倍/4倍，4倍为实验性）

Dynamic：动态模式（开启后根据性能自动调整倍率）

MaxCount：最大倍率（设置DLSS报告的最大帧生成倍率，默认4）

TemporalFix：时间修复（插值校正，建议开启，提升画面稳定性）

ForceFlipMeteringOff：强制关闭翻转计量（仅在3倍/4倍冻结时开启，开启后需重启游戏）


〖小〗操作提示

在ReShade界面中点击对应的复选框可启用/禁用功能

拖动滑块可调整数值，修改后即时生效

如不确定如何设置，保持默认值即可


〖大〗八、注意事项

〖小〗免责声明

不建议在网游和带反作弊的游戏上使用本软件。

本软件本质是修改游戏文件，修改文件可能导致游戏封号。

如遇封号，本软件概不负责。

〖小〗文件备份

安装补丁前软件会自动备份游戏原文件

还原时会自动恢复，不会误删游戏文件

〖小〗游戏运行

安装或还原补丁时，请确保游戏未在运行

否则可能导致文件被占用而操作失败

〖小〗异环等特殊路径游戏

nvngx_dlssg.dll会放到Nvidia目录

其他补丁会放到真正运行exe的目录

不会放到Engine\Binaries\Win64目录


〖大〗九、常见问题

Q：开启多帧生成后游戏里没有选项？

A：确保游戏支持DLSS帧生成，并且在游戏设置中开启了DLSS。

Q：高级模式进不去游戏？

A：尝试使用「配置名修改」功能，更换配置名后再试。

Q：A卡开启DLSS5后游戏崩溃？

A：确保在游戏中选择了FSR3.0以上，不要选FSR2.0。

Q：扫描不到游戏？

A：可以使用「手动添加」功能，选择游戏根目录。

Q：还原后游戏出问题？

A：软件会自动备份原文件，还原时会恢复。如果仍有问题，可以验证游戏文件完整性。


〖大〗感谢使用！祝您游戏愉快！";

    /// <summary>更新内容文本（V1.8.1.3）</summary>
    private const string ChangelogText = @"〖大〗更新日志 V1.8.6.8


〖大〗V1.8.6.8


〖小〗1. DX9 64位 reshade-shaders 缺失修复（关键修复）

修复 GetSharedDirectory 方法未正确处理子目录提取的 bug

之前 Shared 目录下的 reshade-shaders-common 和 Fonts 等子目录文件无法正确提取

导致 DX9 64位游戏安装后 reshade-shaders 文件缺失，ReShade 显示配置加载失效

添加缓存完整性检查，旧版本错误提取的缓存会自动删除重新提取


〖小〗2. 起源引擎 dgvoodoo bin 目录识别修复

修复半条命2等 Source 引擎游戏 dgvoodoo 补丁无法正确识别 bin 目录的问题

添加 Source 引擎特殊处理（最高优先级）

检测 bin 目录下是否有至少3个 Source 引擎特征文件（shaderapidx9.dll、materialsystem.dll、engine.dll 等）

同时检查上级目录下的 bin 目录


〖小〗3. DX9 检测逻辑改进

新增 DetectRenderAPIType 方法，返回4种渲染 API 类型

避免把 DX11+ 游戏（如燕云十六声）误判为 DX9 游戏

检测逻辑更严格：exe 引用 d3d9.dll 且不引用 d3d11/d3d12/dxgi 才判定为 DX9

目录检测不把 d3d9.dll 作为 DX9 特征（很多游戏目录都有）


〖小〗4. 游戏 exe 识别逻辑加强

支持 Win64r 等变体目录名（如燕云十六声的 Engine\Binaries\Win64r）

新增 FindDirectoriesByPrefix 方法，支持前缀匹配

策略1改为查找以 Win64 开头的目录（支持 Win64、Win64r、Win64_Shipping 等）


〖小〗5. reshade-shaders 命名修复

自动将下划线版本（reshade_shaders）重命名为连字符版本（reshade-shaders）

如果两个目录都存在，自动合并文件后删除下划线版本

同时更新已安装文件列表中的路径


〖小〗6. 手动添加按钮文字优化

原文字：DX9手动添加（自行选择游戏exe）

新文字：手动模式（自行选择游戏exe，支持dx9游戏）


〖大〗V1.8.6.5(1)


〖小〗1. 新增无责还原功能

当游戏没有备份文件，但检测到目录中有补丁特征文件时，还原按钮自动变为「无责还原」（红色警告样式）

适用于用户自己手动添加过补丁，忘记怎么删除的情况

使用前会弹出严重警告，二次确认后才会执行：

本软件只提供删除功能，不提供恢复游戏源文件的功能

使用后等同于删除游戏的源文件，可能导致游戏无法运行

Steam/Epic游戏使用后请务必验证游戏完整性

无法验证完整性的游戏，建议删除后重新开启多帧生成或DLSS5功能


〖大〗V1.8.6.5


〖小〗1. 软件体积大幅优化

删除各功能中重复利用的补丁文件，创建Shared共享目录统一管理：

nvngx_dlssnr.dll（158MB）：DLSS5通用、DX9共用

nvngx_dlss.dll（56MB）：DLSS5通用、DX9、2077专用共用

dxgi.dll（5MB）：DLSS5通用、DX9、高级模式共用

renodx-dlss5.addon64（1.65MB）：DLSS5 N卡、DX9共用

软件体积从626MB减小到446MB，节省180MB（28.8%）

所有功能保持完整，补丁安装逻辑自动从Shared目录复制共享文件


〖大〗V1.8.5.5


〖小〗1. 启动速度优化

修复软件启动慢的问题（每次打开需要等待2-3秒）

优化补丁资源提取逻辑：

启动时不再提取所有补丁文件，改为延迟加载，只在需要使用时才提取

使用固定的缓存目录（基于版本号），第二次启动后直接使用缓存，无需重新提取

已存在的文件跳过，避免重复提取

DLSS5补丁也使用缓存目录，第一次使用后后续启动秒开

现在软件启动速度大幅提升，基本可以做到秒开


〖大〗V1.8.4.5


〖小〗1. 完善RE引擎游戏检测列表

新增以下RE引擎游戏的识别支持：

怪物猎人崛起（Monster Hunter Rise）

怪物猎人物语3（Monster Hunter Stories 3）

祇：女神之路（Kunitsu-Gami: Path of the Goddess）

识质存在（Pragmata）

鬼武者：剑之道（Onimusha: Way of the Sword）

生化危机9：安魂曲（Resident Evil Requiem）

现在软件可以正确识别以上游戏为RE引擎游戏，并提供RE引擎通用DLSS5补丁方案


〖大〗V1.8.3.5


〖小〗1. 显卡检测逻辑优化

修复带核显的CPU（如AMD Ryzen 7 9800X3D、Intel i5-12600K等）导致DLSS5无法开启的问题

原逻辑会优先识别到核显，误判为不支持DLSS5

新逻辑优先检测独立显卡，忽略核显，只有在没有独立显卡时才检测核显

只要有一个独立显卡是NVIDIA或AMD 7000/9000系，就允许开启DLSS5


〖大〗V1.8.3.4(2) 累积修复更新（2次）


〖小〗1. DLSS5按钮橙色高亮修复

修复DLSS5按钮没有显示橙色的问题

原因是按钮的IsSecondary属性被设置为true，导致使用透明背景而非AccentColor

将IsSecondary改为false，按钮现在显示实心橙色

未开启时橙色高亮，开启后（还原状态）灰色，与绿色的多帧生成按钮明确区分


〖小〗2. 详情面板文字遮挡修复

修复详情面板中游戏名称和状态标签文字过长时遮挡右边内容的问题

将游戏名称标签和状态标签从AutoSize改为固定宽度270px

启用AutoEllipsis，长文字自动显示省略号，不会遮挡右边的游戏目录和运行程序


〖大〗V1.8.3.4


〖小〗1. 新增ReShade配置界面中文注释

在使用说明中添加RenoDX-DLSSNR和DLSS MFG的常用选项中文注释

包括RenoDX-DLSSNR的神经渲染、神经提升、NR强度、局部结构等选项

包括DLSS MFG的帧生成倍率、动态模式、时间修复等选项

方便不懂英文的用户理解和配置ReShade界面


〖小〗2. DLSS5按钮改为橙色高亮

开启DLSS5按钮使用橙色，与一键开启多帧生成的绿色区分开来

未开启时显示亮橙色，已开启（还原状态）时显示深橙色

鼠标悬停时颜色变亮，视觉效果更明显


〖大〗V1.8.2.3(2) 累积修复更新（2次）


〖小〗1. 排错功能交互逻辑修复

排错功能需要用户开启了多帧生成功能才能允许交互

未开启多帧生成（经典或高级模式）时，排错按钮禁用

右键菜单中的排错菜单项同步修改


〖小〗2. 编辑多帧配置界面浅色模式修复

修复编辑多帧配置界面在浅色模式下仍然是黑色背景的bug

原因是设置IsLightTheme属性后没有重新应用主题

添加ApplyTheme方法，设置IsLightTheme后自动重新应用所有控件颜色

包括标签、下拉框、复选框、按钮等所有控件的颜色


〖大〗V1.8.2.3


〖小〗1. 新增EA、育碧、GOG平台自动扫描

自动扫描从原来的Steam、Epic两个平台扩展到五个平台

支持EA Desktop、育碧Uplay/Connect、GOG Galaxy

通过注册表获取安装路径，同时检查默认安装目录

扫描逻辑与Steam/Epic完全一致


〖小〗2. 修复排错功能检测不到sl.文件的bug

修复ScanExistingSlDlls方法中depth变量的bug

原来depth变量是处理的目录数量而非真正的目录深度

导致游戏目录超过15个子目录时停止扫描，漏掉sl.文件

现在使用元组跟踪真正的目录深度，最大深度20层

同时检查经典和高级两个补丁列表，确保sl.nvperf.dll也能被检测到


〖大〗V1.8.1.3


〖小〗1. 浅色模式游戏列表行颜色彻底修复

修复浅色模式下游戏列表黑白交替的问题

所有行统一为白色，只有选中行是蓝色

在添加行时直接设置行背景色，确保生效


〖小〗2. 浅色模式一键开启多帧生成按钮颜色修复

修复切换游戏后按钮颜色不重置的问题

支持帧生成的游戏按钮显示为绿色

不支持帧生成的游戏按钮显示为深灰色


〖大〗V1.8.1.2


〖小〗1. 版本号同步修复

修复左上方大标题版本号停留在旧版本的问题

所有版本号统一使用CurrentVersion常量

以后更新版本号只需修改一处


〖小〗2. DLSS5按钮位置调整

将开启DLSS5按钮从第三行移到第一行

放在手动添加和一键开启多帧生成中间

布局更紧凑，消除空白


〖小〗3. 第二行按钮位置调整

将编辑多帧配置和排错修复移到第二行前两个位置

将2077专用补丁和配置名修改移到后两个位置

未选择游戏时第二行前两个位置不会空白


〖大〗V1.8.1.1


〖小〗1. 游戏exe识别逻辑加强

修复识别到vc_redist.x64.exe等运行库安装程序的问题

添加vc_redist、redist、dxsetup等过滤关键词

加强策略2排序逻辑，排除Engine/Extras/Redist等目录


〖小〗2. 过滤关键词误过滤修复

修复be关键词误过滤BETGameSteam等游戏exe的问题

修复steam关键词误过滤GameSteam等游戏exe的问题

移除太短的关键词（be、eac、x64等）

移除平台相关关键词（steam、epic等）


〖小〗3. 手动添加逻辑优化

修复手动添加必须有帧生成文件的问题

不支持帧生成的游戏也可以添加并开启DLSS5

优化提示文字，说明不支持帧生成的游戏只能开启DLSS5


〖大〗小功能更新汇总


〖小〗1. 更新检测功能

GitHub Gist+夸克/百度/蓝奏云三渠道下载

启动时自动检测更新

手动检查更新按钮


〖小〗2. 浅色/深色主题切换

双主题支持

一键切换浅色/深色模式

设置自动保存


〖小〗3. 游戏列表右键菜单

右键快速操作

一键开启/关闭DLSS5或多帧生成

交互逻辑与主界面按钮一致


〖小〗4. 使用说明+更新内容

内嵌文本窗口

使用说明详细讲解软件功能

更新内容记录每个版本的更新


〖小〗5. 自动扫描优化

扫描所有游戏，不只是有帧生成的

不支持帧生成的游戏也能添加

只能开启DLSS5，多帧生成按钮禁用


〖小〗6. 手动添加优化

支持无帧生成游戏添加

只要求找到游戏真正运行exe

不要求必须有nvngx_dlssg.dll


〖小〗7. RE引擎游戏检测

通过检测re_chunk_000.pak文件识别RE引擎游戏

生化危机、鬼泣5、怪物猎人等自动识别

RE引擎游戏使用RE引擎通用DLSS5方案


〖小〗8. 更换软件图标

更换为DLSS on图标

软件左上角和任务栏图标同步更新


〖小〗9. RE引擎DLSS5开启提示

RE引擎游戏开启DLSS5后弹出提示

开启DLSS5后暂时无法开启游戏自带的帧生成

建议使用DLSS5自带的AI插帧或NVIDIA的AI插帧


〖小〗10. 状态栏显示优化

已开启（多帧生成）/已开启（DLSS5）/已开启（全部）

2077专用补丁显示已开启（专属多帧生成）

不支持帧生成的游戏显示仅支持DLSS5


〖小〗11. 启动提示弹窗

软件启动时弹出小黑盒分享提示

请勿倒卖，其他渠道购买请申请退款

小黑盒链接可点击跳转浏览器

可设置不再弹出


〖小〗12. 免责声明

底部免责声明栏

不建议在网游和带反作弊的游戏使用

修改文件可能导致游戏封号，本软件概不负责


〖小〗13. 图形化配置编辑器

RTX40MFG_config.json可视化编辑

仅经典模式开放

各项配置说明详细


〖小〗14. 游戏列表优化（从修复改算为小功能）

ListView替换为DataGridView

解决文字黏连和遮挡问题

列表一目了然


〖小〗15. UI现代化（从修复改算为小功能）

WinUI3风格深色主题

PPT设计稿重构

深灰=禁用、绿色=主操作、浅灰=次操作、蓝色=选中


〖小〗16. 按钮布局优化（从修复改算为小功能）

按钮位置多次调整

第一行：自动扫描、手动添加、开启DLSS5、一键开启多帧生成

第二行：编辑配置、排错修复、2077专用、配置名修改


〖小〗17. 标题栏优化（从修复改算为小功能）

标题栏高度增加

标题和副标题位置调整

确保文字完整显示不被遮挡


〖小〗18. 深浅色适配优化（从修复改算为小功能）

浅色模式全面适配

按钮、列表、二级菜单全部适配

深色/浅色模式切换流畅


〖小〗19. 不同分辨率UI自适应

多分辨率支持

窗口拖拽UI自适应

按钮文字不被截断


〖小〗20. 小黑盒链接可点击

启动弹窗链接遮挡修复

链接可点击跳转浏览器

文字显示完整


〖大〗大功能更新汇总


〖小〗第1组：经典模式+高级模式

经典模式：适合老驱动，即装即用

高级模式：适合新驱动，兼容性最强

两种模式一键切换


〖小〗第2组：2077专用补丁+经典排错修复

2077专用：赛博朋克2077单独方案，14个补丁+plugins子目录

经典排错：替换sl.开头的补丁文件，只替换已有的不新增


〖小〗第3组：高级排错修复+配置名修改

高级排错：高级模式专用排错补丁

配置名修改：修改version.dll/ini为其他配置名，解决二游非法模块

15个配置名可选，dxgi.dll只能改成d3d12.dll


〖小〗第4组：DLSS5 N卡支持+DLSS5 A卡支持

N卡：自动检测N卡，安装对应补丁

A卡：自动检测A卡，安装对应补丁，自动打开设置程序

A卡支持7000系+9000系


〖小〗第5组：生化危机专属DLSS5（已删除）+RE引擎通用DLSS5

生化危机专属：RE2~RE8专属dinput8.dll（因闪退问题已删除）

RE引擎通用：RE引擎游戏通用方案，含ReShade框架

生化危机游戏统一使用RE引擎通用方案


〖小〗第6组：快照还原逻辑+删除生化危机专属补丁

快照还原：安装前记录文件快照，还原时精确删除不误删

替换的文件自动备份，还原时恢复

删除生化危机专属补丁，统一使用RE引擎通用方案，节省体积


〖大〗历史版本


〖小〗V1.8.1

更换软件图标

新增RE引擎DLSS5开启提示


〖小〗V1.8

删除生化危机专用DLSS5补丁

生化危机游戏统一使用RE引擎通用补丁

RE引擎游戏检测优化


〖小〗V1.7

新增RE引擎游戏文件检测

通过检测 re_chunk_000.pak 文件识别RE引擎游戏


〖小〗V1.6.1

体积优化

删除不需要的日志文件和文档文件

共享重复的大文件（nvngx_dlss.dll）


〖小〗V1.6

加强扫描逻辑，排除空文件夹

游戏exe识别优化，排除引擎exe

删除全盘扫描功能


〖小〗V1.5

新增DLSS5功能（支持N卡和A卡）

新增RE引擎通用DLSS5方案

新增浅色/深色主题切换

新增游戏列表右键菜单

状态栏显示优化

备份还原逻辑加强

A卡支持范围扩大到7000系+9000系

修复选中状态丢失问题

异环等特殊路径游戏识别优化

新增使用说明和更新内容功能


〖小〗V1.3

新增更新检测功能

支持夸克、百度、蓝奏云三渠道下载

新增配置名修改功能

新增2077专用补丁

新增排错修复功能


〖小〗V1.0

基础多帧生成开启功能

经典模式和高级模式

Steam/Epic自动扫描

手动添加

一键还原功能";

    /// <summary>
    /// 显示使用说明窗口（与页面版同步）
    /// </summary>
    private void ShowHelp()
    {
        // 确保文本已加载
        if (_helpTextBox != null && !string.IsNullOrEmpty(_helpTextBox.Text))
        {
            using var form = new TextViewerForm("使用说明", _helpTextBox.Text, _currentTheme == "Light");
            form.ShowDialog(this);
        }
        else
        {
            LoadHelpText();
            using var form = new TextViewerForm("使用说明", _helpTextBox.Text, _currentTheme == "Light");
            form.ShowDialog(this);
        }
    }

    /// <summary>
    /// 显示更新内容窗口（与页面版同步）
    /// </summary>
    private void ShowChangelog()
    {
        // 确保文本已加载
        if (_changelogTextBox != null && !string.IsNullOrEmpty(_changelogTextBox.Text))
        {
            using var form = new TextViewerForm($"更新内容 - V{DisplayVersion}", _changelogTextBox.Text, _currentTheme == "Light");
            form.ShowDialog(this);
        }
        else
        {
            LoadChangelogText();
            using var form = new TextViewerForm($"更新内容 - V{DisplayVersion}", _changelogTextBox.Text, _currentTheme == "Light");
            form.ShowDialog(this);
        }
    }

    /// <summary>
    /// 检查是否是新版本第一次启动，如果是则弹出更新内容
    /// </summary>
    private void CheckAndShowChangelogOnFirstRun()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "settings.ini");
            var lastVersion = "";

            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                foreach (var line in content.Split('\n'))
                {
                    if (line.StartsWith("LastVersion="))
                    {
                        lastVersion = line.Substring("LastVersion=".Length).Trim();
                        break;
                    }
                }
            }

            // 如果上次版本不等于当前版本，说明是新版本第一次启动
            if (lastVersion != CurrentVersion)
            {
                // 延迟弹出，让主界面先加载完成
                this.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        ShowChangelog();
                    }
                    catch { }
                }));

                // 保存当前版本号
                SaveLastVersion(CurrentVersion);
            }
        }
        catch { }
    }

    /// <summary>
    /// 保存上次运行的版本号到settings.ini
    /// </summary>
    private void SaveLastVersion(string version)
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "settings.ini");
            var content = "";
            if (File.Exists(configPath))
                content = File.ReadAllText(configPath);

            // 移除旧的LastVersion行
            var lines = content.Split('\n')
                .Where(l => !l.StartsWith("LastVersion="))
                .ToList();
            lines.Add($"LastVersion={version}");

            File.WriteAllText(configPath, string.Join("\n", lines));
        }
        catch { }
    }

    #endregion

    #region 更新检测功能

    /// <summary>版本信息数据模型</summary>
    private class VersionInfo
    {
        public string version { get; set; } = "";
        public string releaseDate { get; set; } = "";
        public string changelog { get; set; } = "";
        public string downloadUrlKuake { get; set; } = "";
        public string downloadUrlBaidu { get; set; } = "";
        public string downloadUrlLanzou { get; set; } = "";
    }

    /// <summary>
    /// 异步检查更新
    /// </summary>
    /// <param name="showNoUpdate">是否在没有更新时显示提示</param>
    private async Task CheckForUpdatesAsync(bool showNoUpdate)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "DLSSFrameGenEnabler");

            var json = await httpClient.GetStringAsync(VersionInfoUrl);
            var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);

            if (versionInfo == null || string.IsNullOrEmpty(versionInfo.version))
            {
                if (showNoUpdate)
                    MessageBox.Show(this, "无法获取版本信息，请检查网络连接。", "检查更新",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 对比版本号
            if (CompareVersions(versionInfo.version, CurrentVersion) > 0)
            {
                // 有新版本，显示更新提示
                ShowUpdateDialog(versionInfo);
            }
            else if (showNoUpdate)
            {
                MessageBox.Show(this, $"当前已是最新版本！\n\n当前版本：v{DisplayVersion}", "检查更新",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            if (showNoUpdate)
            {
                MessageBox.Show(this,
                    $"检查更新失败：{ex.Message}\n\n请检查网络连接，或稍后重试。\n\n" +
                    $"提示：首次使用需要在代码中配置版本信息文件URL。",
                    "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            // 静默失败，不影响正常使用
        }
    }

    /// <summary>
    /// 显示更新提示弹窗
    /// </summary>
    private void ShowUpdateDialog(VersionInfo versionInfo)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => ShowUpdateDialog(versionInfo)));
            return;
        }

        using var dialog = new Form
        {
            Text = "发现新版本",
            Size = new Size(580, 520),
            MinimumSize = new Size(540, 480),
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI Variable Text", 9.5f),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false
        };

        // 标题
        var titleLabel = new Label
        {
            Text = "🎉 发现新版本",
            Font = new Font("Segoe UI Variable Display", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 185, 129),
            AutoSize = true,
            Location = new Point(24, 18)
        };

        // 版本信息
        var versionLabel = new Label
        {
            Text = $"最新版本：v{versionInfo.version}    当前版本：v{DisplayVersion}" +
                   (!string.IsNullOrEmpty(versionInfo.releaseDate) ? $"    发布日期：{versionInfo.releaseDate}" : ""),
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(24, 52)
        };

        // 更新说明标题
        var changelogTitle = new Label
        {
            Text = "更新内容：",
            Font = new Font("Segoe UI Variable Text", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 220, 220),
            AutoSize = true,
            Location = new Point(24, 82)
        };

        // 更新说明内容（可滚动）
        var changelogBox = new TextBox
        {
            Text = string.IsNullOrEmpty(versionInfo.changelog) ? "暂无更新说明" : versionInfo.changelog,
            ForeColor = Color.FromArgb(210, 210, 210),
            BackColor = Color.FromArgb(40, 40, 40),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI Variable Text", 9.5f),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(516, 160),
            Location = new Point(24, 108)
        };

        // 下载链接标题
        var downloadTitle = new Label
        {
            Text = "下载地址（点击跳转浏览器）：",
            Font = new Font("Segoe UI Variable Text", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 220, 220),
            AutoSize = true,
            Location = new Point(24, 282)
        };

        // 夸克网盘链接
        var kuakeLink = new LinkLabel
        {
            Text = "📦 夸克网盘下载",
            ForeColor = Color.FromArgb(100, 180, 255),
            LinkColor = Color.FromArgb(100, 180, 255),
            ActiveLinkColor = Color.FromArgb(150, 200, 255),
            Font = new Font("Segoe UI Variable Text", 10f),
            AutoSize = true,
            Location = new Point(24, 308),
            LinkBehavior = LinkBehavior.AlwaysUnderline,
            Enabled = !string.IsNullOrEmpty(versionInfo.downloadUrlKuake)
        };
        if (!kuakeLink.Enabled)
        {
            kuakeLink.Text = "📦 夸克网盘下载（暂不可用）";
            kuakeLink.LinkColor = Color.FromArgb(100, 100, 100);
        }
        kuakeLink.LinkClicked += (_, _) =>
        {
            if (!string.IsNullOrEmpty(versionInfo.downloadUrlKuake))
                OpenUrl(versionInfo.downloadUrlKuake);
        };

        // 百度网盘链接
        var baiduLink = new LinkLabel
        {
            Text = "🔍 百度网盘下载",
            ForeColor = Color.FromArgb(100, 180, 255),
            LinkColor = Color.FromArgb(100, 180, 255),
            ActiveLinkColor = Color.FromArgb(150, 200, 255),
            Font = new Font("Segoe UI Variable Text", 10f),
            AutoSize = true,
            Location = new Point(24, 334),
            LinkBehavior = LinkBehavior.AlwaysUnderline,
            Enabled = !string.IsNullOrEmpty(versionInfo.downloadUrlBaidu)
        };
        if (!baiduLink.Enabled)
        {
            baiduLink.Text = "🔍 百度网盘下载（暂不可用）";
            baiduLink.LinkColor = Color.FromArgb(100, 100, 100);
        }
        baiduLink.LinkClicked += (_, _) =>
        {
            if (!string.IsNullOrEmpty(versionInfo.downloadUrlBaidu))
                OpenUrl(versionInfo.downloadUrlBaidu);
        };

        // 蓝奏云链接
        var lanzouLink = new LinkLabel
        {
            Text = "☁️ 蓝奏云下载",
            ForeColor = Color.FromArgb(100, 180, 255),
            LinkColor = Color.FromArgb(100, 180, 255),
            ActiveLinkColor = Color.FromArgb(150, 200, 255),
            Font = new Font("Segoe UI Variable Text", 10f),
            AutoSize = true,
            Location = new Point(24, 360),
            LinkBehavior = LinkBehavior.AlwaysUnderline,
            Enabled = !string.IsNullOrEmpty(versionInfo.downloadUrlLanzou)
        };
        if (!lanzouLink.Enabled)
        {
            lanzouLink.Text = "☁️ 蓝奏云下载（暂不可用）";
            lanzouLink.LinkColor = Color.FromArgb(100, 100, 100);
        }
        lanzouLink.LinkClicked += (_, _) =>
        {
            if (!string.IsNullOrEmpty(versionInfo.downloadUrlLanzou))
                OpenUrl(versionInfo.downloadUrlLanzou);
        };

        // 稍后再说按钮
        var laterBtn = new Button
        {
            Text = "稍后再说",
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 40),
            Location = new Point(24, 425),
            Cursor = Cursors.Hand
        };
        laterBtn.FlatAppearance.BorderSize = 0;
        laterBtn.Click += (_, _) => { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); };

        // 立即更新按钮
        var updateBtn = new Button
        {
            Text = "立即更新（打开下载页）",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(16, 185, 129),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(220, 40),
            Location = new Point(330, 425),
            Cursor = Cursors.Hand
        };
        updateBtn.FlatAppearance.BorderSize = 0;
        updateBtn.Click += (_, _) =>
        {
            // 优先打开夸克网盘，其次百度网盘，最后蓝奏云
            if (!string.IsNullOrEmpty(versionInfo.downloadUrlKuake))
                OpenUrl(versionInfo.downloadUrlKuake);
            else if (!string.IsNullOrEmpty(versionInfo.downloadUrlBaidu))
                OpenUrl(versionInfo.downloadUrlBaidu);
            else if (!string.IsNullOrEmpty(versionInfo.downloadUrlLanzou))
                OpenUrl(versionInfo.downloadUrlLanzou);
            else
                MessageBox.Show(this, "暂无可用的下载链接。", "更新",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(versionLabel);
        dialog.Controls.Add(changelogTitle);
        dialog.Controls.Add(changelogBox);
        dialog.Controls.Add(downloadTitle);
        dialog.Controls.Add(kuakeLink);
        dialog.Controls.Add(baiduLink);
        dialog.Controls.Add(lanzouLink);
        dialog.Controls.Add(laterBtn);
        dialog.Controls.Add(updateBtn);

        ApplyThemeToForm(dialog);
        dialog.ShowDialog(this);
    }

    /// <summary>
    /// 比较两个版本号的大小
    /// </summary>
    /// <returns>大于0表示v1较新，小于0表示v2较新，等于0表示相同</returns>
    private static int CompareVersions(string v1, string v2)
    {
        try
        {
            var parts1 = v1.TrimStart('v').Split('.');
            var parts2 = v2.TrimStart('v').Split('.');
            var length = Math.Max(parts1.Length, parts2.Length);

            for (var i = 0; i < length; i++)
            {
                var num1 = i < parts1.Length && int.TryParse(parts1[i], out var n1) ? n1 : 0;
                var num2 = i < parts2.Length && int.TryParse(parts2[i], out var n2) ? n2 : 0;
                if (num1 != num2)
                    return num1.CompareTo(num2);
            }
            return 0;
        }
        catch
        {
            return string.Compare(v1, v2, StringComparison.Ordinal);
        }
    }

    /// <summary>用默认浏览器打开URL</summary>
    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    #endregion

    #region 主题切换功能

    /// <summary>
    /// 切换深色/浅色主题
    /// </summary>
    private void ToggleTheme()
    {
        _currentTheme = _currentTheme == "Dark" ? "Light" : "Dark";
        _settingsThemeButton.Text = _currentTheme == "Dark" ? "☀️  切换到浅色模式" : "🌙  切换到深色模式";
        ApplyTheme();
        SaveThemeSetting();
    }

    /// <summary>
    /// 应用当前主题到所有控件（WinUI3风格）
    /// </summary>
    private void ApplyTheme()
    {
        if (_currentTheme == "Light")
        {
            // 浅色模式（Windows 11 浅色主题）
            BgColor = Color.FromArgb(243, 243, 243);
            NavBgColor = Color.FromArgb(249, 249, 249);
            CardBgColor = Color.FromArgb(255, 255, 255);
            CardHoverColor = Color.FromArgb(245, 245, 245);
            NavItemHover = Color.FromArgb(230, 230, 230);
            NavItemSelected = Color.FromArgb(0, 120, 212);
            AccentColor = Color.FromArgb(0, 120, 212);
            AccentHover = Color.FromArgb(20, 130, 220);
            SuccessColor = Color.FromArgb(16, 185, 129);
            WarningColor = Color.FromArgb(245, 158, 11);
            DangerColor = Color.FromArgb(220, 50, 50);
            TextPrimary = Color.FromArgb(30, 30, 30);
            TextSecondary = Color.FromArgb(80, 80, 80);
            TextTertiary = Color.FromArgb(120, 120, 120);
            BorderColor = Color.FromArgb(200, 200, 200);
            DividerColor = Color.FromArgb(220, 220, 220);
        }
        else
        {
            // 深色模式（Windows 11 深色主题）
            BgColor = Color.FromArgb(32, 32, 32);
            NavBgColor = Color.FromArgb(28, 28, 28);
            CardBgColor = Color.FromArgb(43, 43, 43);
            CardHoverColor = Color.FromArgb(50, 50, 50);
            NavItemHover = Color.FromArgb(50, 50, 50);
            NavItemSelected = Color.FromArgb(0, 120, 212);
            AccentColor = Color.FromArgb(0, 120, 212);
            AccentHover = Color.FromArgb(20, 130, 220);
            SuccessColor = Color.FromArgb(16, 185, 129);
            WarningColor = Color.FromArgb(245, 158, 11);
            DangerColor = Color.FromArgb(220, 50, 50);
            TextPrimary = Color.FromArgb(255, 255, 255);
            TextSecondary = Color.FromArgb(200, 200, 200);
            TextTertiary = Color.FromArgb(150, 150, 150);
            BorderColor = Color.FromArgb(60, 60, 60);
            DividerColor = Color.FromArgb(55, 55, 55);
        }

        // 更新窗体
        BackColor = BgColor;
        ForeColor = TextPrimary;

        // 更新导航栏
        if (_navPanel != null)
        {
            _navPanel.BackColor = NavBgColor;
            _navHeader.BackColor = NavBgColor;
            _navItemsPanel.BackColor = NavBgColor;
            _navAppName.ForeColor = TextPrimary;
            _navAppVersion.ForeColor = TextTertiary;
        }

        // 更新导航项
        foreach (var item in _navItems)
        {
            if (item.IsSelected)
            {
                item.Panel.BackColor = NavItemSelected;
                item.TextLabel.ForeColor = Color.White;
            }
            else
            {
                item.Panel.BackColor = Color.Transparent;
                item.TextLabel.ForeColor = TextSecondary;
            }
        }

        // 更新内容区
        if (_contentPanel != null)
        {
            _contentPanel.BackColor = BgColor;
            _contentHeader.BackColor = BgColor;
            _pageToolbar.BackColor = BgColor;
            _pageTitle.ForeColor = TextPrimary;
            _pageSubtitle.ForeColor = TextTertiary;
        }

        // 更新所有页面背景
        if (_homePage != null) _homePage.BackColor = BgColor;
        if (_settingsPage != null) _settingsPage.BackColor = BgColor;
        if (_helpPage != null) _helpPage.BackColor = BgColor;
        if (_changelogPage != null) _changelogPage.BackColor = BgColor;
        if (_aboutPage != null) _aboutPage.BackColor = BgColor;

        // 更新卡片背景色（关键：修复浅色模式下卡片黑色的问题）
        if (_settingsCard != null) _settingsCard.BackColor = CardBgColor;
        if (_settingsTitle != null) _settingsTitle.ForeColor = TextPrimary;
        if (_changelogCard != null) _changelogCard.BackColor = CardBgColor;
        if (_changelogToolbar != null) _changelogToolbar.BackColor = BgColor;
        if (_aboutCard != null) _aboutCard.BackColor = CardBgColor;

        // 更新RichTextBox颜色
        if (_helpTextBox != null)
        {
            _helpTextBox.BackColor = CardBgColor;
            _helpTextBox.ForeColor = TextPrimary;
        }
        if (_changelogTextBox != null)
        {
            _changelogTextBox.BackColor = CardBgColor;
            _changelogTextBox.ForeColor = TextPrimary;
        }

        // 更新关于页面
        if (_aboutAppName != null) _aboutAppName.ForeColor = TextPrimary;
        if (_aboutVersion != null) _aboutVersion.ForeColor = TextTertiary;
        if (_aboutDescription != null) _aboutDescription.ForeColor = TextSecondary;
        if (_aboutAuthorLabel != null) _aboutAuthorLabel.ForeColor = TextPrimary;
        if (_aboutCreditsLabel != null) _aboutCreditsLabel.ForeColor = TextPrimary;
        if (_aboutCreditsText != null) _aboutCreditsText.ForeColor = TextSecondary;
        if (_aboutLinksPanel != null) _aboutLinksPanel.BackColor = CardBgColor;

        // 更新详情面板
        if (_detailPanel != null)
        {
            _detailPanel.BackColor = CardBgColor;
            _detailGameName.ForeColor = TextPrimary;
            _detailStatus.ForeColor = TextTertiary;
            _detailGamePath.ForeColor = TextSecondary;
            _detailExePath.ForeColor = TextTertiary;
        }

        // 更新免责声明栏（跟随主题）
        if (_disclaimerPanel != null)
        {
            if (_currentTheme == "Light")
            {
                _disclaimerPanel.BackColor = Color.FromArgb(255, 248, 240);
                _disclaimerLabel.ForeColor = Color.FromArgb(150, 100, 50);
            }
            else
            {
                _disclaimerPanel.BackColor = Color.FromArgb(35, 25, 20);
                _disclaimerLabel.ForeColor = Color.FromArgb(210, 160, 100);
            }
        }

        // 更新状态栏
        if (_statusBar != null)
        {
            _statusBar.BackColor = NavBgColor;
            _statusLabel.ForeColor = TextSecondary;
        }

        // 更新 DataGridView 特殊样式
        if (_gameGridView != null)
        {
            _gameGridView.BackgroundColor = BgColor;
            _gameGridView.GridColor = DividerColor;
            _gameGridView.ColumnHeadersDefaultCellStyle.BackColor = CardBgColor;
            _gameGridView.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            _gameGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = CardBgColor;
            _gameGridView.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextSecondary;
            _gameGridView.DefaultCellStyle.BackColor = BgColor;
            _gameGridView.DefaultCellStyle.ForeColor = TextPrimary;
            _gameGridView.DefaultCellStyle.SelectionBackColor = AccentColor;
            _gameGridView.DefaultCellStyle.SelectionForeColor = Color.White;
            _gameGridView.AlternatingRowsDefaultCellStyle.BackColor = BgColor;
            _gameGridView.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
            _gameGridView.AlternatingRowsDefaultCellStyle.SelectionBackColor = AccentColor;
            _gameGridView.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;

            // 直接遍历所有行
            for (int i = 0; i < _gameGridView.Rows.Count; i++)
            {
                var row = _gameGridView.Rows[i];
                row.DefaultCellStyle.BackColor = BgColor;
                row.DefaultCellStyle.ForeColor = TextPrimary;
                row.DefaultCellStyle.SelectionBackColor = AccentColor;
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }

            _gameGridView.Invalidate();
            _gameGridView.Refresh();
        }

        // 递归设置所有 ModernButton 的主题
        SetModernButtonTheme(this, _currentTheme == "Light");

        // 刷新所有 ModernButton
        foreach (Control ctrl in Controls)
            RefreshModernButtons(ctrl);
    }

    /// <summary>
    /// 递归更新控件颜色
    /// </summary>
    private void UpdateControlColors(Control parent)
    {
        foreach (Control ctrl in parent.Controls)
        {
            if (ctrl is Panel panel)
            {
                // 详情面板、导航栏、状态栏用卡片/导航背景
                if (ctrl == _detailPanel || ctrl == _navPanel || ctrl == _navHeader || 
                    ctrl == _navItemsPanel || ctrl == _statusBar ||
                    ctrl == _contentHeader || ctrl == _pageToolbar)
                    panel.BackColor = CardBgColor;
                else
                    panel.BackColor = BgColor;
            }
            else if (ctrl is Label label)
            {
                label.ForeColor = TextPrimary;
                if (label == _detailStatus || label == _detailGamePath || label == _detailExePath)
                    label.ForeColor = TextSecondary;
                if (label == _pageSubtitle || label == _navAppVersion)
                    label.ForeColor = TextTertiary;
            }
            else if (ctrl is Button btn)
            {
                btn.ForeColor = TextPrimary;
                if (btn.BackColor == Color.FromArgb(50, 50, 50) || btn.BackColor == Color.FromArgb(43, 43, 43))
                    btn.BackColor = NavBgColor;
            }
            else if (ctrl is TextBox textBox)
            {
                textBox.BackColor = CardBgColor;
                textBox.ForeColor = TextPrimary;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (ctrl is ComboBox combo)
            {
                combo.BackColor = CardBgColor;
                combo.ForeColor = TextPrimary;
            }
            else if (ctrl is LinkLabel link)
            {
                link.ForeColor = TextSecondary;
                link.LinkColor = AccentColor;
            }
            else if (ctrl is CheckBox check)
            {
                check.ForeColor = TextPrimary;
            }
            else if (ctrl is ProgressBar)
            {
                // ProgressBar 颜色由系统控制
            }

            // 递归子控件
            if (ctrl.HasChildren)
                UpdateControlColors(ctrl);
        }
    }

    /// <summary>
    /// 应用主题到任意弹窗（递归设置所有控件颜色）
    /// </summary>
    private void ApplyThemeToForm(Form form)
    {
        bool isLight = _currentTheme == "Light";
        Color bg = isLight ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
        Color bgLight = isLight ? Color.FromArgb(255, 255, 255) : Color.FromArgb(43, 43, 43);
        Color textPrimary = isLight ? Color.FromArgb(30, 30, 30) : Color.FromArgb(255, 255, 255);
        Color textSecondary = isLight ? Color.FromArgb(80, 80, 80) : Color.FromArgb(200, 200, 200);

        form.BackColor = bg;
        form.ForeColor = textPrimary;

        foreach (Control ctrl in form.Controls)
        {
            ApplyThemeToControl(ctrl, isLight, bg, bgLight, textPrimary, textSecondary);
        }
    }

    /// <summary>
    /// 递归应用主题到控件
    /// </summary>
    private void ApplyThemeToControl(Control ctrl, bool isLight, Color bg, Color bgLight, Color textPrimary, Color textSecondary)
    {
        if (ctrl is Label lbl)
        {
            lbl.ForeColor = textPrimary;
            lbl.BackColor = Color.Transparent;
        }
        else if (ctrl is Button btn)
        {
            btn.ForeColor = textPrimary;
            if (btn.BackColor == Color.FromArgb(50, 50, 50) || btn.BackColor == Color.FromArgb(43, 43, 43))
                btn.BackColor = isLight ? Color.FromArgb(230, 230, 230) : btn.BackColor;
            else if (btn.BackColor == Color.FromArgb(0, 120, 212))
                btn.BackColor = Color.FromArgb(0, 120, 212); // 主按钮保持蓝色
        }
        else if (ctrl is TextBox tb)
        {
            tb.BackColor = bgLight;
            tb.ForeColor = textPrimary;
        }
        else if (ctrl is ComboBox cb)
        {
            cb.BackColor = bgLight;
            cb.ForeColor = textPrimary;
        }
        else if (ctrl is CheckBox chk)
        {
            chk.ForeColor = textPrimary;
        }
        else if (ctrl is LinkLabel ll)
        {
            ll.ForeColor = textSecondary;
            ll.LinkColor = Color.FromArgb(0, 120, 212);
        }
        else if (ctrl is Panel pnl)
        {
            pnl.BackColor = bg;
        }
        else if (ctrl is UI.ModernButton mbtn)
        {
            mbtn.IsLightTheme = isLight;
            mbtn.ForeColor = textPrimary;
            mbtn.Invalidate();
        }

        if (ctrl.HasChildren)
        {
            foreach (Control child in ctrl.Controls)
            {
                ApplyThemeToControl(child, isLight, bg, bgLight, textPrimary, textSecondary);
            }
        }
    }

    /// <summary>
    /// 递归设置 ModernButton 的主题
    /// </summary>
    private void SetModernButtonTheme(Control parent, bool isLight)
    {
        foreach (Control ctrl in parent.Controls)
        {
            if (ctrl is UI.ModernButton btn)
            {
                btn.IsLightTheme = isLight;
                btn.ForeColor = isLight ? Color.FromArgb(30, 30, 30) : Color.White;
                btn.Invalidate();
            }
            if (ctrl.HasChildren)
                SetModernButtonTheme(ctrl, isLight);
        }
    }

    /// <summary>
    /// 递归刷新 ModernButton 的颜色
    /// </summary>
    private void RefreshModernButtons(Control parent)
    {
        foreach (Control ctrl in parent.Controls)
        {
            if (ctrl is UI.ModernButton btn)
            {
                btn.Invalidate();
            }
            if (ctrl.HasChildren)
                RefreshModernButtons(ctrl);
        }
    }

    /// <summary>
    /// 保存主题设置到 settings.ini
    /// </summary>
    private void SaveThemeSetting()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "settings.ini");
            var content = File.Exists(configPath) ? File.ReadAllText(configPath) : "";
            // 移除旧的主题设置
            content = System.Text.RegularExpressions.Regex.Replace(content, @"Theme=\w+\r?\n?", "");
            // 添加新的主题设置
            content += $"Theme={_currentTheme}\n";
            File.WriteAllText(configPath, content);
        }
        catch { }
    }

    /// <summary>
    /// 从 settings.ini 加载主题设置
    /// </summary>
    private void LoadThemeSetting()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "settings.ini");
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                if (content.Contains("Theme=Light"))
                {
                    _currentTheme = "Light";
                    _settingsThemeButton.Text = "🌙  切换到深色模式";
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 从嵌入资源加载图片
    /// </summary>
    private static Image? LoadEmbeddedImage(string resourceName)
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
                return Image.FromStream(stream);
        }
        catch { }
        return null;
    }

    #endregion

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

    #endregion
}
