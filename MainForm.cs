using System.Drawing.Drawing2D;
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
    #region 颜色主题（WinUI3 深色模式）

    private static readonly Color BgColor = Color.FromArgb(32, 32, 32);
    private static readonly Color BgColorLight = Color.FromArgb(43, 43, 43);
    private static readonly Color BgColorLighter = Color.FromArgb(50, 50, 50);
    private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentHover = Color.FromArgb(20, 130, 220);
    private static readonly Color SuccessColor = Color.FromArgb(16, 185, 129);
    private static readonly Color WarningColor = Color.FromArgb(245, 158, 11);
    private static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
    private static readonly Color TextSecondary = Color.FromArgb(200, 200, 200);
    private static readonly Color TextTertiary = Color.FromArgb(150, 150, 150);
    private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);

    #endregion

    #region 控件字段

    private Panel _titleBar = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private Panel _toolBar = null!;
    private ModernButton _scanButton = null!;
    private ModernButton _manualButton = null!;
    private ModernButton _refreshButton = null!;
    private ModernButton _fullScanButton = null!;
    private DataGridView _gameGridView = null!;
    private Panel _detailPanel = null!;
    private Label _detailGameName = null!;
    private Label _detailStatus = null!;
    private Label _detailGamePath = null!;
    private Label _detailExePath = null!;
    private ModernButton _applyButton = null!;
    private ModernButton _restoreButton = null!;
    private ModernButton _configButton = null!;
    private ModernButton _troubleshootButton = null!;
    private Panel _statusBar = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;
    private Label _emptyHintLabel = null!;

    #endregion

    private readonly GameScanner _scanner = new();
    private readonly FilePatcher _patcher = new();
    private List<GameInfo> _games = new();
    private GameInfo? _selectedGame;
    private bool _isBusy;

    public MainForm()
    {
        InitializeComponent();
        CheckPatchFilesOnLoad();
    }

    #region 界面初始化

    private void InitializeComponent()
    {
        // 窗体设置
        Text = "DLSS 多帧生成开启工具";
        Size = new Size(960, 680);
        MinimumSize = new Size(800, 560);
        BackColor = BgColor;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI Variable Text", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        // ===== 标题栏 =====
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = BgColorLight
        };

        _titleLabel = new Label
        {
            Text = "DLSS 多帧生成开启工具",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 16f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 12)
        };

        _subtitleLabel = new Label
        {
            Text = "一键为支持 DLSS 的游戏开启多帧生成 · RTX 50 系以下显卡可用",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(26, 38)
        };

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_subtitleLabel);

        // ===== 工具栏 =====
        _toolBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = BgColor,
            Padding = new Padding(20, 10, 20, 10)
        };

        _scanButton = new ModernButton
        {
            Text = "扫描游戏库",
            AccentColor = AccentColor,
            HoverColor = AccentHover,
            Size = new Size(150, 38),
            Location = new Point(20, 9)
        };
        _scanButton.Click += async (_, _) => await ScanGamesAsync();

        _manualButton = new ModernButton
        {
            Text = "手动选择文件夹",
            IsSecondary = true,
            Size = new Size(165, 38),
            Location = new Point(190, 9)
        };
        _manualButton.Click += (_, _) => ManualSelectFolder();

        _refreshButton = new ModernButton
        {
            Text = "刷新状态",
            IsSecondary = true,
            Size = new Size(100, 38),
            Location = new Point(365, 9)
        };
        _refreshButton.Click += (_, _) => RefreshGameStatus();

        _fullScanButton = new ModernButton
        {
            Text = "全盘扫描游戏",
            IsSecondary = true,
            Size = new Size(130, 38),
            Location = new Point(475, 9),
            AccentColor = Color.FromArgb(147, 51, 234),
            HoverColor = Color.FromArgb(167, 71, 254)
        };
        _fullScanButton.Click += async (_, _) => await FullScanAsync();

        _toolBar.Controls.Add(_scanButton);
        _toolBar.Controls.Add(_manualButton);
        _toolBar.Controls.Add(_refreshButton);
        _toolBar.Controls.Add(_fullScanButton);

        // ===== 游戏列表（DataGridView，清晰表格）=====
        _gameGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(32, 32, 32),
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
            ColumnHeadersHeight = 34,
            GridColor = Color.FromArgb(50, 50, 50),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ShowCellToolTips = false,
            ScrollBars = ScrollBars.Vertical
        };

        // 列标题样式
        _gameGridView.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.FromArgb(235, 235, 235),
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold),
            SelectionBackColor = Color.FromArgb(45, 45, 45),
            SelectionForeColor = Color.FromArgb(235, 235, 235),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        // 默认单元格样式
        _gameGridView.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.FromArgb(240, 240, 240),
            Font = new Font("Segoe UI Variable Text", 10f),
            SelectionBackColor = Color.FromArgb(0, 120, 212),
            SelectionForeColor = Color.White,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        // 交替行样式
        _gameGridView.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(37, 37, 37),
            ForeColor = Color.FromArgb(240, 240, 240),
            SelectionBackColor = Color.FromArgb(0, 120, 212),
            SelectionForeColor = Color.White
        };

        // 行高
        _gameGridView.RowTemplate.Height = 30;
        _gameGridView.RowTemplate.MinimumHeight = 30;

        // 添加列
        _gameGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "GameName", HeaderText = "游戏名称", FillWeight = 260 });
        _gameGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "平台", FillWeight = 70 });
        _gameGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", FillWeight = 90 });
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
                    e.CellStyle.ForeColor = game.IsPatched
                        ? Color.FromArgb(16, 185, 129)
                        : Color.FromArgb(160, 160, 160);
                    e.CellStyle.Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold);
                    e.CellStyle.SelectionForeColor = Color.White;
                }
            }
            // 路径列用稍小字体和稍暗颜色
            if (e.ColumnIndex == 3 && e.RowIndex >= 0)
            {
                e.CellStyle.ForeColor = Color.FromArgb(150, 150, 150);
                e.CellStyle.Font = new Font("Segoe UI Variable Text", 8.5f);
                e.CellStyle.SelectionForeColor = Color.White;
            }
            // 平台列
            if (e.ColumnIndex == 1 && e.RowIndex >= 0)
            {
                e.CellStyle.ForeColor = Color.FromArgb(200, 200, 200);
                e.CellStyle.SelectionForeColor = Color.White;
            }
        };

        _gameGridView.SelectionChanged += (_, _) => OnGameSelected();

        // 空状态提示
        _emptyHintLabel = new Label
        {
            Text = "点击上方「扫描游戏库」自动检测 Steam / Epic 已安装的游戏\n或点击「手动选择文件夹」指定游戏目录",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 11f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        // ===== 详情面板 =====
        _detailPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 210,
            BackColor = BgColorLight,
            Padding = new Padding(20, 14, 20, 14)
        };

        _detailGameName = new Label
        {
            Text = "未选择游戏",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 14)
        };

        _detailStatus = new Label
        {
            Text = "",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 40)
        };

        _detailGamePath = new Label
        {
            Text = "",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = true,
            Location = new Point(20, 62),
            MaximumSize = new Size(560, 0)
        };

        _detailExePath = new Label
        {
            Text = "",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = true,
            Location = new Point(20, 82),
            MaximumSize = new Size(560, 0)
        };

        _applyButton = new ModernButton
        {
            Text = "一键开启多帧生成",
            AccentColor = SuccessColor,
            HoverColor = Color.FromArgb(30, 200, 140),
            PressedColor = Color.FromArgb(10, 160, 110),
            Size = new Size(210, 38),
            Location = new Point(700, 10),
            Enabled = false
        };
        _applyButton.Click += async (_, _) => await ApplyPatchAsync();

        _restoreButton = new ModernButton
        {
            Text = "一键还原",
            IsSecondary = true,
            Size = new Size(210, 38),
            Location = new Point(700, 54),
            Enabled = false
        };
        _restoreButton.Click += async (_, _) => await RestorePatchAsync();

        _configButton = new ModernButton
        {
            Text = "编辑多帧配置",
            IsSecondary = true,
            Size = new Size(210, 38),
            Location = new Point(700, 98),
            Enabled = false,
            AccentColor = AccentColor,
            HoverColor = AccentHover
        };
        _configButton.Click += (_, _) => OpenConfigEditor();

        _troubleshootButton = new ModernButton
        {
            Text = "排错修复（替换sl.dll）",
            IsSecondary = true,
            Size = new Size(210, 38),
            Location = new Point(700, 142),
            Enabled = false,
            AccentColor = WarningColor,
            HoverColor = Color.FromArgb(255, 170, 30)
        };
        _troubleshootButton.Click += async (_, _) => await ApplyTroubleshootAsync();

        _detailPanel.Controls.Add(_detailGameName);
        _detailPanel.Controls.Add(_detailStatus);
        _detailPanel.Controls.Add(_detailGamePath);
        _detailPanel.Controls.Add(_detailExePath);
        _detailPanel.Controls.Add(_applyButton);
        _detailPanel.Controls.Add(_restoreButton);
        _detailPanel.Controls.Add(_configButton);
        _detailPanel.Controls.Add(_troubleshootButton);

        // ===== 免责声明栏 =====
        var disclaimerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            BackColor = Color.FromArgb(30, 20, 20)
        };

        var disclaimerLabel = new Label
        {
            Text = "⚠ 免责声明：不建议在网游和带反作弊的游戏上使用本软件。本软件本质是修改游戏文件，修改文件可能导致游戏封号，如遇封号本软件概不负责。",
            ForeColor = Color.FromArgb(200, 140, 80),
            Font = new Font("Segoe UI Variable Text", 8f),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(10, 0, 10, 0)
        };
        disclaimerPanel.Controls.Add(disclaimerLabel);

        // ===== 状态栏 =====
        _statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = BgColorLighter
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
            Location = new Point(730, 13),
            Visible = false,
            BackColor = BgColorLighter
        };

        _statusBar.Controls.Add(_statusLabel);
        _statusBar.Controls.Add(_progressBar);

        // ===== 组装控件（按 Dock 布局顺序）=====
        Controls.Add(_gameGridView);
        Controls.Add(_emptyHintLabel);
        Controls.Add(_detailPanel);
        Controls.Add(_statusBar);
        Controls.Add(disclaimerPanel);
        Controls.Add(_toolBar);
        Controls.Add(_titleBar);

        _emptyHintLabel.BringToFront();
    }

    #endregion

    #region 业务逻辑

    private void CheckPatchFilesOnLoad()
    {
        var (ok, missing, usingEmbedded) = _patcher.CheckPatchFiles();
        if (!ok)
        {
            SetStatus($"警告：补丁文件缺失（{missing}），请将补丁文件放入 Patches 文件夹", WarningColor);
        }
        else
        {
            var source = usingEmbedded ? "内置补丁已加载" : "外部 Patches 文件夹";
            SetStatus($"补丁文件就绪（{source}），点击「扫描游戏库」开始", TextSecondary);
        }
    }

    private async Task ScanGamesAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        SetBusyUI(true);
        SetStatus("正在扫描游戏库...");

        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            _games = await Task.Run(() => _scanner.ScanAllGames(progress));
            UpdateGameList();
            SetStatus($"扫描完成，共找到 {_games.Count} 个支持 DLSS 多帧生成的游戏");
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

    private void ManualSelectFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "请选择游戏安装目录（包含 nvngx_dlssg.dll 的文件夹）",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        SetStatus("正在扫描所选目录...");
        var game = _scanner.ScanSingleDirectory(dialog.SelectedPath);

        if (game == null)
        {
            MessageBox.Show(this,
                "在所选目录中未找到 nvngx_dlssg.dll 文件。\n\n请确认：\n1. 选择的是游戏的根安装目录\n2. 该游戏确实支持 DLSS 多帧生成",
                "未找到 DLSS 文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetStatus("所选目录不包含 nvngx_dlssg.dll", WarningColor);
            return;
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
            existing.DlssgDllPath = game.DlssgDllPath;
            existing.GameExePath = game.GameExePath;
            existing.IsPatched = game.IsPatched;
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
        }
        UpdateGameList();
        OnGameSelected();
        SetStatus("状态已刷新");
    }

    /// <summary>
    /// 全盘扫描：扫描所有本地分区中支持 DLSS 的游戏
    /// </summary>
    private async Task FullScanAsync()
    {
        if (_isBusy) return;

        var confirm = MessageBox.Show(this,
            "全盘扫描将遍历所有本地分区查找 nvngx_dlssg.dll 文件。\n\n" +
            "• 扫描时间取决于硬盘大小和文件数量，可能需要几分钟\n" +
            "• 会自动跳过系统目录和明显无关的目录\n" +
            "• 找到的游戏会添加到列表中\n\n" +
            "确认开始全盘扫描吗？",
            "全盘扫描确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _isBusy = true;
        SetBusyUI(true);
        SetStatus("正在全盘扫描，这可能需要几分钟...");

        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var foundGames = await Task.Run(() => _scanner.ScanAllDrives(progress));

            // 合并到现有列表（去重）
            var added = 0;
            foreach (var game in foundGames)
            {
                if (!_games.Any(g => g.InstallPath.Equals(game.InstallPath, StringComparison.OrdinalIgnoreCase)))
                {
                    _games.Add(game);
                    added++;
                }
            }
            _games = _games.OrderBy(x => x.Name).ToList();

            UpdateGameList();
            SetStatus($"全盘扫描完成，新发现 {added} 个游戏，当前共 {_games.Count} 个");

            if (added == 0 && foundGames.Count > 0)
            {
                MessageBox.Show(this,
                    $"全盘扫描完成，找到 {foundGames.Count} 个游戏，但均已在列表中。",
                    "扫描完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (foundGames.Count == 0)
            {
                MessageBox.Show(this,
                    "全盘扫描未找到任何包含 nvngx_dlssg.dll 的游戏。\n\n" +
                    "可能原因：\n" +
                    "1. 电脑上没有安装支持 DLSS 多帧生成的游戏\n" +
                    "2. 游戏安装在移动硬盘或网络驱动器上（全盘扫描仅扫描本地固定分区）\n" +
                    "3. 游戏文件被隐藏或权限不足",
                    "未找到游戏", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"全盘扫描失败：{ex.Message}", Color.FromArgb(255, 100, 100));
            MessageBox.Show(this, $"全盘扫描时发生错误：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isBusy = false;
            SetBusyUI(false);
        }
    }

    private void UpdateGameList()
    {
        _gameGridView.Rows.Clear();

        if (_games.Count == 0)
        {
            _emptyHintLabel.Visible = true;
            _gameGridView.Visible = false;
            return;
        }

        _emptyHintLabel.Visible = false;
        _gameGridView.Visible = true;

        foreach (var game in _games)
        {
            var statusText = game.IsPatched ? "● 已开启" : "○ 未开启";
            _gameGridView.Rows.Add(game.Name, game.Source, statusText, game.InstallPath, game);
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
            _restoreButton.Enabled = false;
            _configButton.Enabled = false;
            _troubleshootButton.Enabled = false;
            return;
        }

        var row = _gameGridView.SelectedRows[0];
        _selectedGame = row.Cells["Tag"].Value as GameInfo;
        if (_selectedGame == null) return;

        _detailGameName.Text = _selectedGame.Name;
        _detailStatus.Text = $"状态：{_selectedGame.PatchStatus}  |  平台：{_selectedGame.Source}";
        _detailStatus.ForeColor = _selectedGame.IsPatched ? SuccessColor : TextTertiary;
        _detailGamePath.Text = $"游戏目录：{_selectedGame.InstallPath}";
        _detailExePath.Text = $"运行程序：{_selectedGame.GameExePath ?? "未找到"}";

        var hasBackup = FilePatcher.HasBackup(_selectedGame);
        _applyButton.Enabled = !_isBusy && !_selectedGame.IsPatched;
        _applyButton.Text = _selectedGame.IsPatched ? "已开启多帧生成" : "一键开启多帧生成";
        _restoreButton.Enabled = !_isBusy && (_selectedGame.IsPatched || hasBackup);
        // 编辑配置按钮：只有已打补丁且游戏exe目录存在时才启用
        _configButton.Enabled = !_isBusy && _selectedGame.IsPatched &&
                                !string.IsNullOrEmpty(_selectedGame.GameExeDirectory) &&
                                Directory.Exists(_selectedGame.GameExeDirectory);
        // 排错修复按钮：选中游戏即可用（扫描并替换游戏中已有的sl.*.dll）
        _troubleshootButton.Enabled = !_isBusy;
    }

    /// <summary>
    /// 打开多帧生成配置编辑器
    /// </summary>
    private void OpenConfigEditor()
    {
        if (_selectedGame == null || string.IsNullOrEmpty(_selectedGame.GameExeDirectory))
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

        var confirm = MessageBox.Show(this,
            $"即将为《{_selectedGame.Name}》开启 DLSS 多帧生成：\n\n" +
            $"1. 替换 nvngx_dlssg.dll（原文件将自动备份）\n" +
            $"2. 复制 RTX40MFG.asi / RTX40MFG_config.json / version.dll 到游戏目录\n\n" +
            $"确认继续吗？",
            "确认开启", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _isBusy = true;
        SetBusyUI(true);
        SetStatus($"正在为《{_selectedGame.Name}》打补丁...");

        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var result = await Task.Run(() => _patcher.ApplyPatch(_selectedGame, progress));

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

    private async Task RestorePatchAsync()
    {
        if (_selectedGame == null || _isBusy) return;

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

    /// <summary>
    /// 排错修复：替换游戏中已存在的 sl.*.dll 文件
    /// </summary>
    private async Task ApplyTroubleshootAsync()
    {
        if (_selectedGame == null || _isBusy) return;

        var confirm = MessageBox.Show(this,
            $"即将为《{_selectedGame.Name}》执行排错修复：\n\n" +
            $"• 自动扫描游戏目录中所有 sl.*.dll 文件\n" +
            $"• 仅替换游戏中已存在的同名文件（不会新增文件）\n" +
            $"• 替换前自动备份原文件\n\n" +
            $"适用于：开启多帧生成后仍然没有生效的情况。\n\n" +
            $"确认继续吗？",
            "排错修复确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _isBusy = true;
        SetBusyUI(true);
        SetStatus($"正在为《{_selectedGame.Name}》执行排错修复...");

        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var result = await Task.Run(() => _patcher.ApplyTroubleshoot(_selectedGame, progress));

            if (result.Success)
            {
                SetStatus($"排错修复完成，共替换 {result.ReplacedFiles.Count} 个文件", SuccessColor);
                MessageBox.Show(this, result.Message, "排错修复完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                SetStatus($"排错修复：{result.Message}", WarningColor);
                MessageBox.Show(this, result.Message, "排错修复",
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
        _fullScanButton.Enabled = !busy;
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
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

    #endregion
}
