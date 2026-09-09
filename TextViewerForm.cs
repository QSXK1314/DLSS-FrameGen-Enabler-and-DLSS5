using DLSSFrameGenEnabler.UI;

namespace DLSSFrameGenEnabler;

/// <summary>
/// 通用内嵌文本查看窗口（用于使用说明、更新内容等）
/// </summary>
public class TextViewerForm : Form
{
    #region 颜色主题

    /// <summary>是否为浅色主题</summary>
    public bool IsLightTheme { get; set; }

    private Color BgColor => IsLightTheme ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
    private Color BgColorLight => IsLightTheme ? Color.FromArgb(255, 255, 255) : Color.FromArgb(43, 43, 43);
    private Color TextPrimary => IsLightTheme ? Color.FromArgb(30, 30, 30) : Color.FromArgb(255, 255, 255);
    private Color TextSecondary => IsLightTheme ? Color.FromArgb(80, 80, 80) : Color.FromArgb(200, 200, 200);
    private Color BorderColor => IsLightTheme ? Color.FromArgb(200, 200, 200) : Color.FromArgb(60, 60, 60);

    #endregion

    // 控件
    private Label _titleLabel = null!;
    private Panel _contentPanel = null!;
    private RichTextBox _contentRichBox = null!;
    private ModernButton _closeButton = null!;

    // 字体
    private Font _normalFont = null!;
    private Font _bigTitleFont = null!;
    private Font _subTitleFont = null!;

    /// <summary>
    /// 创建文本查看窗口
    /// </summary>
    public TextViewerForm(string title, string content, bool isLightTheme = false)
    {
        IsLightTheme = isLightTheme;
        _normalFont = new Font("Segoe UI Variable Text", 10.5f, FontStyle.Regular);
        _bigTitleFont = new Font("Segoe UI Variable Text", 13f, FontStyle.Bold);
        _subTitleFont = new Font("Segoe UI Variable Text", 11.5f, FontStyle.Bold);
        InitializeComponent(title, content);
    }

    #region 初始化

    private void InitializeComponent(string title, string content)
    {
        Text = title;
        Size = new Size(720, 600);
        MinimumSize = new Size(600, 450);
        BackColor = BgColor;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI Variable Text", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        DoubleBuffered = true;

        // 标题
        _titleLabel = new Label
        {
            Text = title,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Variable Text", 14f, FontStyle.Bold),
            Location = new Point(24, 20),
            AutoSize = true
        };
        Controls.Add(_titleLabel);

        // 内容面板（带边框）
        _contentPanel = new Panel
        {
            Location = new Point(24, 60),
            Size = new Size(ClientSize.Width - 48, ClientSize.Height - 130),
            BackColor = BgColorLight,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_contentPanel);

        // RichTextBox
        _contentRichBox = new RichTextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true,
            BackColor = BgColorLight,
            ForeColor = TextPrimary,
            Font = _normalFont,
            Location = new Point(12, 12),
            Size = new Size(_contentPanel.Width - 24, _contentPanel.Height - 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.None,
            Cursor = Cursors.IBeam,
            DetectUrls = false
        };
        _contentPanel.Controls.Add(_contentRichBox);

        // 逐行加载文本并设置格式（确保100%准确）
        LoadContentWithFormatting(content);

        // 滚动到顶部
        _contentRichBox.SelectionStart = 0;
        _contentRichBox.ScrollToCaret();

        // 关闭按钮
        _closeButton = new ModernButton
        {
            Text = "关闭",
            Size = new Size(120, 38),
            Location = new Point(ClientSize.Width - 144, ClientSize.Height - 54),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            IsLightTheme = IsLightTheme,
            IsSecondary = true
        };
        _closeButton.Click += (_, _) => Close();
        Controls.Add(_closeButton);
    }

    /// <summary>
    /// 逐行加载文本并设置格式
    /// </summary>
    private void LoadContentWithFormatting(string content)
    {
        if (string.IsNullOrEmpty(content))
            return;

        // 统一换行符
        var text = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var rawLines = text.Split('\n').Select(l => l.TrimEnd()).ToList();

        _contentRichBox.Clear();
        _contentRichBox.SelectionBackColor = BgColorLight;
        _contentRichBox.SelectionColor = TextPrimary;

        bool isFirstLine = true;

        for (int i = 0; i < rawLines.Count; i++)
        {
            var line = rawLines[i];
            int lineType = 0; // 0=普通，1=大标题，2=小标题

            // 识别标题标记
            if (line.StartsWith("〖大〗"))
            {
                line = line.Substring("〖大〗".Length);
                lineType = 1;
            }
            else if (line.StartsWith("〖小〗"))
            {
                line = line.Substring("〖小〗".Length);
                lineType = 2;
            }

            // 选择当前行的字体
            Font lineFont = lineType switch
            {
                1 => _bigTitleFont,
                2 => _subTitleFont,
                _ => _normalFont
            };

            // 如果不是第一行，先加换行
            if (!isFirstLine)
            {
                _contentRichBox.SelectionFont = _normalFont;
                _contentRichBox.AppendText("\n");
            }
            isFirstLine = false;

            // 追加当前行内容，并设置格式
            _contentRichBox.SelectionFont = lineFont;
            _contentRichBox.SelectionColor = TextPrimary;
            _contentRichBox.AppendText(line);

            // 如果当前行非空，且下一行也非空，则插入一个空行（段落间隔）
            if (!string.IsNullOrWhiteSpace(line) &&
                i + 1 < rawLines.Count &&
                !string.IsNullOrWhiteSpace(rawLines[i + 1]))
            {
                _contentRichBox.SelectionFont = _normalFont;
                _contentRichBox.AppendText("\n");
            }
        }
    }

    #endregion
}
