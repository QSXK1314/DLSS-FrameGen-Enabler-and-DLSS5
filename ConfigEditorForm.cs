using System.Drawing.Drawing2D;
using System.Text.Json;
using DLSSFrameGenEnabler.UI;

namespace DLSSFrameGenEnabler;

/// <summary>
/// 多帧生成配置编辑对话框
/// </summary>
public class ConfigEditorForm : Form
{
    #region 颜色主题（支持深色/浅色切换）

    /// <summary>是否为浅色主题</summary>
    private bool _isLightTheme;
    public bool IsLightTheme
    {
        get => _isLightTheme;
        set
        {
            _isLightTheme = value;
            ApplyTheme();
        }
    }

    /// <summary>
    /// 应用主题到所有控件（设置IsLightTheme后自动调用）
    /// </summary>
    private void ApplyTheme()
    {
        if (_titleLabel == null) return; // 还未初始化，跳过

        BackColor = BgColor;
        ForeColor = TextPrimary;

        // 标签
        _titleLabel.ForeColor = TextPrimary;
        _multiplierLabel.ForeColor = TextSecondary;
        _modeLabel.ForeColor = TextSecondary;
        _fpsLabel.ForeColor = TextSecondary;
        _advancedLabel.ForeColor = TextSecondary;

        // 下拉框
        _multiplierCombo.BackColor = BgColorLighter;
        _multiplierCombo.ForeColor = TextPrimary;
        _modeCombo.BackColor = BgColorLighter;
        _modeCombo.ForeColor = TextPrimary;
        _fpsCombo.BackColor = BgColorLighter;
        _fpsCombo.ForeColor = TextPrimary;

        // 复选框
        _debugCheck.BackColor = BgColor;
        _debugCheck.ForeColor = TextPrimary;
        _exp56Check.BackColor = BgColor;
        _exp56Check.ForeColor = TextPrimary;

        // ModernButton
        foreach (Control ctrl in Controls)
        {
            if (ctrl is ModernButton mbtn)
            {
                mbtn.IsLightTheme = _isLightTheme;
                mbtn.ForeColor = TextPrimary;
            }
        }

        Invalidate();
        Refresh();
    }

    private Color BgColor => IsLightTheme ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
    private Color BgColorLight => IsLightTheme ? Color.FromArgb(255, 255, 255) : Color.FromArgb(43, 43, 43);
    private Color BgColorLighter => IsLightTheme ? Color.FromArgb(230, 230, 230) : Color.FromArgb(50, 50, 50);
    private Color AccentColor => Color.FromArgb(0, 120, 212);
    private Color SuccessColor => Color.FromArgb(16, 185, 129);
    private Color TextPrimary => IsLightTheme ? Color.FromArgb(30, 30, 30) : Color.FromArgb(255, 255, 255);
    private Color TextSecondary => IsLightTheme ? Color.FromArgb(80, 80, 80) : Color.FromArgb(200, 200, 200);
    private Color TextTertiary => IsLightTheme ? Color.FromArgb(120, 120, 120) : Color.FromArgb(150, 150, 150);
    private Color BorderColor => IsLightTheme ? Color.FromArgb(200, 200, 200) : Color.FromArgb(60, 60, 60);

    #endregion

    private readonly string _configPath;
    private MfgConfig _config;

    // 控件
    private Label _titleLabel = null!;
    private Label _multiplierLabel = null!;
    private ComboBox _multiplierCombo = null!;
    private Label _modeLabel = null!;
    private ComboBox _modeCombo = null!;
    private Label _fpsLabel = null!;
    private ComboBox _fpsCombo = null!;
    private Label _advancedLabel = null!;
    private CheckBox _debugCheck = null!;
    private CheckBox _exp56Check = null!;
    private ModernButton _saveButton = null!;
    private ModernButton _cancelButton = null!;
    private ModernButton _resetButton = null!;

    /// <summary>
    /// 配置是否已保存
    /// </summary>
    public bool Saved { get; private set; }

    public ConfigEditorForm(string configPath)
    {
        _configPath = configPath;
        _config = LoadConfig();
        InitializeComponent();
        LoadConfigToUI();
    }

    #region 初始化

    private void InitializeComponent()
    {
        Text = "编辑多帧配置";
        Size = new Size(660, 380);
        MinimumSize = new Size(640, 360);
        BackColor = BgColor;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI Variable Text", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        DoubleBuffered = true;

        // 标题
        _titleLabel = new Label
        {
            Text = "编辑多帧配置",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 18)
        };

        // 三个下拉框并排
        const int comboY = 72;
        const int comboH = 34;
        const int labelY = 48;
        int[] comboXs = { 24, 234, 444 };
        int[] comboWs = { 190, 190, 190 };

        // 多帧倍率
        _multiplierLabel = new Label
        {
            Text = "多帧倍率",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(comboXs[0], labelY)
        };

        _multiplierCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = BgColorLighter,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            Size = new Size(comboWs[0], comboH),
            Location = new Point(comboXs[0], comboY),
            FlatStyle = FlatStyle.Flat
        };
        _multiplierCombo.Items.AddRange(new object[]
        {
            "2倍",
            "3倍",
            "4倍(推荐)",
            "5倍",
            "6倍(实验性)"
        });

        // 运行模式
        _modeLabel = new Label
        {
            Text = "运行模式",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(comboXs[1], labelY)
        };

        _modeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = BgColorLighter,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            Size = new Size(comboWs[1], comboH),
            Location = new Point(comboXs[1], comboY),
            FlatStyle = FlatStyle.Flat
        };
        _modeCombo.Items.AddRange(new object[]
        {
            "固定倍率",
            "动态多帧生成"
        });
        _modeCombo.SelectedIndexChanged += (_, _) => UpdateFpsEnabled();

        // 动态目标帧率
        _fpsLabel = new Label
        {
            Text = "动态目标帧率(仅动态模式生效)",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(comboXs[2], labelY)
        };

        _fpsCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = BgColorLighter,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            Size = new Size(comboWs[2], comboH),
            Location = new Point(comboXs[2], comboY),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _fpsCombo.Items.AddRange(new object[]
        {
            "自动跟随刷新率",
            "120 FPS",
            "144 FPS",
            "165 FPS",
            "240 FPS"
        });

        // 高级选项
        _advancedLabel = new Label
        {
            Text = "高级选项(一般无需修改)",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 130)
        };

        _debugCheck = new CheckBox
        {
            Text = "generatedOnlyDebug（仅生成帧调试，默认保持关闭）",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(24, 156),
            BackColor = Color.Transparent
        };

        _exp56Check = new CheckBox
        {
            Text = "dynamicExperimental56（动态模式允许开启5/6倍帧生成，默认保持关闭）",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(24, 182),
            BackColor = Color.Transparent
        };

        // 按钮
        _resetButton = new ModernButton
        {
            Text = "恢复默认",
            IsSecondary = true,
            Size = new Size(120, 38),
            Location = new Point(24, 290)
        };
        _resetButton.Click += (_, _) => ResetToDefault();

        _cancelButton = new ModernButton
        {
            Text = "取消",
            IsSecondary = true,
            Size = new Size(120, 38),
            Location = new Point(340, 290)
        };
        _cancelButton.Click += (_, _) => Close();

        _saveButton = new ModernButton
        {
            Text = "保存配置",
            AccentColor = SuccessColor,
            HoverColor = Color.FromArgb(30, 200, 140),
            PressedColor = Color.FromArgb(10, 160, 110),
            Size = new Size(120, 38),
            Location = new Point(470, 290)
        };
        _saveButton.Click += (_, _) => SaveConfig();

        // 组装
        Controls.Add(_titleLabel);
        Controls.Add(_multiplierLabel);
        Controls.Add(_multiplierCombo);
        Controls.Add(_modeLabel);
        Controls.Add(_modeCombo);
        Controls.Add(_fpsLabel);
        Controls.Add(_fpsCombo);
        Controls.Add(_advancedLabel);
        Controls.Add(_debugCheck);
        Controls.Add(_exp56Check);
        Controls.Add(_resetButton);
        Controls.Add(_cancelButton);
        Controls.Add(_saveButton);

        // 应用主题到所有 ModernButton
        foreach (Control ctrl in Controls)
        {
            if (ctrl is ModernButton mbtn)
            {
                mbtn.IsLightTheme = IsLightTheme;
                mbtn.ForeColor = TextPrimary;
            }
        }
    }

    #endregion

    #region 配置读写

    private MfgConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<MfgConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (config != null) return config;
            }
        }
        catch { }
        // 返回默认配置
        return new MfgConfig();
    }

    private void LoadConfigToUI()
    {
        // 倍率
        var multiplierIndex = Math.Clamp(_config.multiplier - 2, 0, 4);
        _multiplierCombo.SelectedIndex = multiplierIndex;

        // 模式
        _modeCombo.SelectedIndex = _config.mode == "dynamic" ? 1 : 0;

        // 动态帧率
        var fpsIndex = _config.dynamicTargetFrameRate switch
        {
            0 => 0,
            120 => 1,
            144 => 2,
            165 => 3,
            240 => 4,
            _ => 0
        };
        _fpsCombo.SelectedIndex = fpsIndex;

        // 高级选项
        _debugCheck.Checked = _config.generatedOnlyDebug;
        _exp56Check.Checked = _config.dynamicExperimental56;

        UpdateFpsEnabled();
    }

    private void SaveConfig()
    {
        try
        {
            // 从UI读取配置
            _config.multiplier = _multiplierCombo.SelectedIndex + 2;
            _config.mode = _modeCombo.SelectedIndex == 1 ? "dynamic" : "fixed";
            _config.dynamicTargetFrameRate = _fpsCombo.SelectedIndex switch
            {
                0 => 0,
                1 => 120,
                2 => 144,
                3 => 165,
                4 => 240,
                _ => 0
            };
            _config.generatedOnlyDebug = _debugCheck.Checked;
            _config.dynamicExperimental56 = _exp56Check.Checked;

            // 保存
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_configPath, json);

            Saved = true;
            MessageBox.Show(this, "配置已保存成功！\n\n重启游戏后生效。", "保存成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"保存失败：{ex.Message}\n\n请确认游戏未在运行，且有文件写入权限。",
                "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetToDefault()
    {
        var confirm = MessageBox.Show(this, "确定恢复默认配置吗？\n\n默认：4倍倍率 + 固定模式",
            "恢复默认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        _config = new MfgConfig();
        LoadConfigToUI();
    }

    private void UpdateFpsEnabled()
    {
        var isDynamic = _modeCombo.SelectedIndex == 1;
        _fpsCombo.Enabled = isDynamic;
        _fpsLabel.ForeColor = isDynamic ? TextSecondary : TextTertiary;
    }

    #endregion

    #region 圆角窗口

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            DwmSetWindowAttribute(Handle, 33, new[] { 2 }, 4);
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

    #endregion
}

/// <summary>
/// 多帧生成配置模型
/// </summary>
public class MfgConfig
{
    public int version { get; set; } = 7;
    public int dynamicTargetFrameRate { get; set; } = 0;
    public int multiplier { get; set; } = 4;
    public string mode { get; set; } = "fixed";
    public bool generatedOnlyDebug { get; set; } = false;
    public bool dynamicExperimental56 { get; set; } = false;
}
