using System.Drawing.Drawing2D;
using System.Text.Json;
using DLSSFrameGenEnabler.UI;

namespace DLSSFrameGenEnabler;

/// <summary>
/// 多帧生成配置编辑对话框
/// </summary>
public class ConfigEditorForm : Form
{
    #region 颜色主题

    private static readonly Color BgColor = Color.FromArgb(32, 32, 32);
    private static readonly Color BgColorLight = Color.FromArgb(43, 43, 43);
    private static readonly Color BgColorLighter = Color.FromArgb(50, 50, 50);
    private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);
    private static readonly Color SuccessColor = Color.FromArgb(16, 185, 129);
    private static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
    private static readonly Color TextSecondary = Color.FromArgb(200, 200, 200);
    private static readonly Color TextTertiary = Color.FromArgb(150, 150, 150);
    private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);

    #endregion

    private readonly string _configPath;
    private MfgConfig _config;

    // 控件
    private Label _titleLabel = null!;
    private Label _multiplierLabel = null!;
    private ComboBox _multiplierCombo = null!;
    private Label _modeLabel = null!;
    private RadioButton _modeFixedRadio = null!;
    private RadioButton _modeDynamicRadio = null!;
    private Label _fpsLabel = null!;
    private ComboBox _fpsCombo = null!;
    private GroupBox _advancedGroup = null!;
    private CheckBox _debugCheck = null!;
    private CheckBox _exp56Check = null!;
    private ModernButton _saveButton = null!;
    private ModernButton _cancelButton = null!;
    private ModernButton _resetButton = null!;
    private Label _hintLabel = null!;

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
        Text = "多帧生成配置";
        Size = new Size(480, 510);
        MinimumSize = new Size(460, 490);
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
            Text = "多帧生成配置",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Display", 15f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 20)
        };

        // 倍率
        _multiplierLabel = new Label
        {
            Text = "多帧倍率",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 10f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 68)
        };

        _multiplierCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = BgColorLighter,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            Size = new Size(200, 32),
            Location = new Point(24, 92),
            FlatStyle = FlatStyle.Flat
        };
        _multiplierCombo.Items.AddRange(new object[]
        {
            "2 倍",
            "3 倍",
            "4 倍（推荐）",
            "5 倍",
            "6 倍（实验性较强）"
        });

        // 模式
        _modeLabel = new Label
        {
            Text = "运行模式",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 10f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 140)
        };

        _modeFixedRadio = new RadioButton
        {
            Text = "固定倍率",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            AutoSize = true,
            Location = new Point(24, 166),
            BackColor = Color.Transparent
        };
        _modeFixedRadio.CheckedChanged += (_, _) => UpdateFpsEnabled();

        _modeDynamicRadio = new RadioButton
        {
            Text = "动态多帧生成",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            AutoSize = true,
            Location = new Point(140, 166),
            BackColor = Color.Transparent
        };

        // 动态目标帧率
        _fpsLabel = new Label
        {
            Text = "动态目标帧率（仅动态模式生效）",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(24, 200)
        };

        _fpsCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = BgColorLighter,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 10f),
            Size = new Size(200, 32),
            Location = new Point(24, 222),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _fpsCombo.Items.AddRange(new object[]
        {
            "自动跟随显示器刷新率",
            "120 FPS",
            "144 FPS",
            "165 FPS",
            "240 FPS"
        });

        // 高级选项
        _advancedGroup = new GroupBox
        {
            Text = "高级选项（一般无需修改）",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 9f),
            Location = new Point(24, 258),
            Size = new Size(420, 88),
            BackColor = BgColor,
            FlatStyle = FlatStyle.Flat
        };

        _debugCheck = new CheckBox
        {
            Text = "generatedOnlyDebug（仅生成帧调试，正常保持关闭）",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(12, 25),
            BackColor = Color.Transparent
        };

        _exp56Check = new CheckBox
        {
            Text = "dynamicExperimental56（动态模式允许5/6倍，建议关闭）",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI Variable Text", 9f),
            AutoSize = true,
            Location = new Point(12, 52),
            BackColor = Color.Transparent
        };

        _advancedGroup.Controls.Add(_debugCheck);
        _advancedGroup.Controls.Add(_exp56Check);

        // 提示
        _hintLabel = new Label
        {
            Text = "修改后点击保存，配置将写入游戏目录的 RTX40MFG_config.json",
            ForeColor = TextTertiary,
            Font = new Font("Segoe UI Variable Text", 8.5f),
            AutoSize = true,
            Location = new Point(24, 388)
        };

        // 按钮
        _resetButton = new ModernButton
        {
            Text = "恢复默认",
            IsSecondary = true,
            Size = new Size(100, 36),
            Location = new Point(24, 416)
        };
        _resetButton.Click += (_, _) => ResetToDefault();

        _cancelButton = new ModernButton
        {
            Text = "取消",
            IsSecondary = true,
            Size = new Size(100, 36),
            Location = new Point(214, 416)
        };
        _cancelButton.Click += (_, _) => Close();

        _saveButton = new ModernButton
        {
            Text = "保存配置",
            AccentColor = SuccessColor,
            HoverColor = Color.FromArgb(30, 200, 140),
            PressedColor = Color.FromArgb(10, 160, 110),
            Size = new Size(110, 36),
            Location = new Point(324, 416)
        };
        _saveButton.Click += (_, _) => SaveConfig();

        // 组装
        Controls.Add(_titleLabel);
        Controls.Add(_multiplierLabel);
        Controls.Add(_multiplierCombo);
        Controls.Add(_modeLabel);
        Controls.Add(_modeFixedRadio);
        Controls.Add(_modeDynamicRadio);
        Controls.Add(_fpsLabel);
        Controls.Add(_fpsCombo);
        Controls.Add(_advancedGroup);
        Controls.Add(_hintLabel);
        Controls.Add(_resetButton);
        Controls.Add(_cancelButton);
        Controls.Add(_saveButton);
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
        if (_config.mode == "dynamic")
        {
            _modeDynamicRadio.Checked = true;
        }
        else
        {
            _modeFixedRadio.Checked = true;
        }

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
            _config.mode = _modeDynamicRadio.Checked ? "dynamic" : "fixed";
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
        var isDynamic = _modeDynamicRadio.Checked;
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
