using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebUI.Beta;

/// <summary>
/// 独立安装界面：主程序退出后运行，显示解压/覆盖进度，完成后启动软件。
/// 由主程序复制到临时目录后以 --update-install 参数启动，避免覆盖正在运行的自身文件。
/// </summary>
internal sealed class UpdateInstallerForm : Form
{
    private static readonly Color BackColorDark = Color.FromArgb(0x17, 0x17, 0x1A);
    private static readonly Color PanelColor = Color.FromArgb(0x25, 0x25, 0x2A);
    private static readonly Color AccentColor = Color.FromArgb(0xD9, 0xA4, 0x41);
    private static readonly Color TextColor = Color.FromArgb(0xF5, 0xF5, 0xF0);
    private static readonly Color MutedColor = Color.FromArgb(0xA8, 0xA8, 0xA2);
    private static readonly Color ErrorColor = Color.FromArgb(0xE0, 0x6C, 0x5F);

    private readonly InstallContext _context;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _pathLabel;
    private readonly Label _percentLabel;
    private readonly Label _statusLabel;
    private readonly Label _fileLabel;
    private readonly Label _footerLabel;
    private readonly Panel _barTrack;
    private readonly Panel _barFill;
    private readonly Button _primaryButton;
    private readonly Button _closeButton;
    private readonly Button _openFolderButton;
    private readonly System.Windows.Forms.Timer _marqueeTimer;
    private readonly System.Windows.Forms.Timer _launchTimer;

    private bool _finished;
    private bool _failed;
    private bool _allowClose;
    private bool _marquee;
    private int _marqueeOffset;

    internal static bool IsInstallRequest(string[] args)
    {
        return args.Length > 0 &&
               string.Equals(args[0], UpdateInstallService.InstallerArgument, StringComparison.OrdinalIgnoreCase);
    }

    internal static int Run(string[] args)
    {
        if (!TryParse(args, out InstallContext context, out string error))
        {
            MessageBox.Show(
                error,
                "安装更新",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        Application.Run(new UpdateInstallerForm(context));
        return 0;
    }

    private static bool TryParse(string[] args, out InstallContext context, out string error)
    {
        context = new InstallContext();
        error = "";
        if (args.Length < 5)
        {
            error = "安装参数不完整，请回到软件内重新下载更新。";
            return false;
        }

        string zipPath = args[1];
        string installRoot = args[2];
        string appExePath = args[4];
        string version = args.Length > 5 ? args[5] : "";
        if (!int.TryParse(args[3], out int processId))
        {
            processId = 0;
        }
        if (!File.Exists(zipPath))
        {
            error = "找不到下载好的增量包：" + zipPath;
            return false;
        }
        if (!Directory.Exists(installRoot))
        {
            error = "找不到软件安装位置：" + installRoot;
            return false;
        }

        context = new InstallContext
        {
            ZipPath = Path.GetFullPath(zipPath),
            InstallRoot = Path.GetFullPath(installRoot),
            MainProcessId = processId,
            AppExePath = appExePath ?? "",
            Version = version ?? ""
        };
        return true;
    }

    internal UpdateInstallerForm(InstallContext context)
    {
        _context = context;
        Text = "安装更新";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 360);
        BackColor = BackColorDark;
        ForeColor = TextColor;
        ShowInTaskbar = true;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        try
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }

        Panel accent = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientSize.Width, 4),
            BackColor = AccentColor
        };
        Controls.Add(accent);

        _titleLabel = new Label
        {
            AutoSize = false,
            Location = new Point(32, 29),
            Size = new Size(540, 38),
            Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = TextColor,
            Text = "正在安装更新"
        };
        Controls.Add(_titleLabel);

        _subtitleLabel = new Label
        {
            AutoSize = false,
            Location = new Point(34, 72),
            Size = new Size(550, 24),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = MutedColor,
            Text = string.IsNullOrWhiteSpace(_context.Version)
                ? "准备覆盖软件文件"
                : "正在更新到 " + _context.Version.Trim()
        };
        Controls.Add(_subtitleLabel);

        _pathLabel = new Label
        {
            AutoSize = false,
            Location = new Point(34, 101),
            Size = new Size(552, 20),
            Font = new Font("Microsoft YaHei UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(0x7E, 0x7E, 0x79),
            AutoEllipsis = true,
            Text = "安装位置：" + _context.InstallRoot
        };
        Controls.Add(_pathLabel);

        _barTrack = new Panel
        {
            Location = new Point(34, 143),
            Size = new Size(552, 12),
            BackColor = PanelColor
        };
        Controls.Add(_barTrack);

        _barFill = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(0, 12),
            BackColor = AccentColor
        };
        _barTrack.Controls.Add(_barFill);

        _percentLabel = new Label
        {
            AutoSize = false,
            Location = new Point(34, 164),
            Size = new Size(70, 22),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = TextColor,
            Text = "0%"
        };
        Controls.Add(_percentLabel);

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(106, 164),
            Size = new Size(480, 22),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = MutedColor,
            TextAlign = ContentAlignment.TopRight,
            Text = "准备安装…"
        };
        Controls.Add(_statusLabel);

        _fileLabel = new Label
        {
            AutoSize = false,
            Location = new Point(34, 197),
            Size = new Size(552, 19),
            Font = new Font("Microsoft YaHei UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(0x7E, 0x7E, 0x79),
            AutoEllipsis = true,
            Text = "正在读取增量包…"
        };
        Controls.Add(_fileLabel);

        _footerLabel = new Label
        {
            AutoSize = false,
            Location = new Point(34, 286),
            Size = new Size(320, 40),
            Font = new Font("Microsoft YaHei UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = MutedColor,
            Text = "安装过程中请勿关闭窗口\r\n软件退出后会自动覆盖文件"
        };
        Controls.Add(_footerLabel);

        _marqueeTimer = new System.Windows.Forms.Timer { Interval = 45 };
        _marqueeTimer.Tick += MarqueeTimer_Tick;
        _launchTimer = new System.Windows.Forms.Timer { Interval = 2500 };
        _launchTimer.Tick += (_, _) =>
        {
            _launchTimer.Stop();
            LaunchAppAndClose();
        };

        _primaryButton = CreateButton("启动软件", 120);
        _primaryButton.Location = new Point(382, 286);
        _primaryButton.Visible = false;
        _primaryButton.Click += (_, _) => LaunchAppAndClose();
        Controls.Add(_primaryButton);

        _openFolderButton = CreateButton("打开安装目录", 120);
        _openFolderButton.Location = new Point(382, 286);
        _openFolderButton.Visible = false;
        _openFolderButton.Click += (_, _) => OpenInstallFolder();
        Controls.Add(_openFolderButton);

        _closeButton = CreateButton("关闭", 80);
        _closeButton.Location = new Point(506, 286);
        _closeButton.Visible = false;
        _closeButton.Click += (_, _) =>
        {
            _launchTimer.Stop();
            _allowClose = true;
            Close();
        };
        Controls.Add(_closeButton);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        _ = RunInstallAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && !_finished)
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _marqueeTimer.Stop();
        _launchTimer.Stop();
        string? tempDir = Path.GetDirectoryName(Application.ExecutablePath);
        if (tempDir != null &&
            tempDir.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) &&
            tempDir.IndexOf("DFGE-Update-", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UpdateInstallService.ScheduleSelfCleanup(tempDir, Application.ExecutablePath);
        }
        base.OnFormClosed(e);
    }

    private async Task RunInstallAsync()
    {
        try
        {
            _fileLabel.Text = "正在等待主程序退出…";
            _statusLabel.Text = "等待软件退出…";
            StartMarquee();
            bool exited = await UpdateInstallService.WaitForMainProcessAsync(_context.MainProcessId, 45000);
            if (!exited)
            {
                throw new TimeoutException("主程序没有在 45 秒内退出，请关闭软件后重新安装。");
            }

            // 给主程序窗体、WebView2 子进程和文件句柄一点收尾时间。
            await Task.Delay(700);
            StopMarquee();

            if (!UpdateInstallService.IsSupportedPackage(_context.ZipPath, out string reason))
            {
                throw new InvalidOperationException(reason);
            }

            _titleLabel.Text = "正在覆盖软件文件";
            _statusLabel.Text = "正在解压增量包…";
            _fileLabel.Text = "正在读取增量包…";

            Progress<InstallProgress> progress = new Progress<InstallProgress>(UpdateProgress);
            await Task.Run(() => UpdateInstallService.ExtractPackage(
                _context.ZipPath,
                _context.InstallRoot,
                progress,
                CancellationToken.None));

            FinishSuccess();
        }
        catch (Exception ex)
        {
            FinishFailure(ex);
        }
    }

    private void UpdateProgress(InstallProgress progress)
    {
        if (_finished)
        {
            return;
        }
        StopMarquee();
        SetProgress(progress.Percent);
        _percentLabel.Text = progress.Percent + "%";
        _statusLabel.Text = progress.FilesDone + " / " + progress.FilesTotal + " 个文件";
        if (!string.IsNullOrWhiteSpace(progress.CurrentFile))
        {
            _fileLabel.Text = "正在覆盖：" + progress.CurrentFile.Replace(Path.DirectorySeparatorChar, '/');
        }
    }

    private void FinishSuccess()
    {
        _finished = true;
        _failed = false;
        _allowClose = true;
        TopMost = false;
        StopMarquee();
        SetProgress(100);
        _percentLabel.Text = "100%";
        _titleLabel.Text = "安装完成";
        _titleLabel.ForeColor = TextColor;
        _subtitleLabel.Text = string.IsNullOrWhiteSpace(_context.Version)
            ? "增量包已覆盖到软件目录"
            : "已更新到 " + _context.Version.Trim();
        _statusLabel.Text = "文件覆盖完成";
        _fileLabel.Text = "增量包已覆盖到 DLSS5+MFG Enable 目录";
        _footerLabel.Text = "安装完成，正在启动软件…\r\n如未自动启动，可点击“启动软件”";
        _primaryButton.Visible = true;
        _primaryButton.Enabled = true;
        _closeButton.Visible = true;
        _closeButton.Enabled = true;
        _openFolderButton.Visible = false;
        UpdateInstallService.WriteLog("安装成功，版本：" + _context.Version + "，安装根：" + _context.InstallRoot);
        _launchTimer.Start();
    }

    private void FinishFailure(Exception ex)
    {
        _finished = true;
        _failed = true;
        _allowClose = true;
        TopMost = false;
        StopMarquee();
        _launchTimer.Stop();
        _barFill.BackColor = ErrorColor;
        _titleLabel.Text = "安装未完成";
        _titleLabel.ForeColor = ErrorColor;
        _statusLabel.ForeColor = ErrorColor;
        _statusLabel.Text = "安装失败";
        _fileLabel.Text = ex.Message;
        _footerLabel.ForeColor = MutedColor;
        _footerLabel.Text = "可关闭窗口后重新下载增量包\r\n若已开始解压，部分文件可能已覆盖";
        _primaryButton.Visible = false;
        _openFolderButton.Visible = true;
        _closeButton.Visible = true;
        _closeButton.Enabled = true;
        UpdateInstallService.WriteLog("安装失败：" + ex);
    }

    private void LaunchAppAndClose()
    {
        if (_failed)
        {
            return;
        }
        try
        {
            _launchTimer.Stop();
            string appExe = _context.AppExePath;
            if (string.IsNullOrWhiteSpace(appExe) || !File.Exists(appExe))
            {
                appExe = UpdateInstallService.GetPreferredAppExe(_context.InstallRoot, false);
            }
            if (!File.Exists(appExe))
            {
                throw new FileNotFoundException("找不到更新后的软件程序。", appExe);
            }

            _primaryButton.Enabled = false;
            _statusLabel.ForeColor = MutedColor;
            _statusLabel.Text = "正在启动软件…";
            _footerLabel.Text = "安装完成，正在启动软件…";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = appExe,
                WorkingDirectory = Path.GetDirectoryName(appExe) ?? _context.InstallRoot,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            UpdateInstallService.WriteLog("安装后启动软件：" + appExe);
            _allowClose = true;
            Close();
        }
        catch (Exception ex)
        {
            _launchTimer.Stop();
            _primaryButton.Enabled = true;
            _titleLabel.Text = "安装完成";
            _titleLabel.ForeColor = TextColor;
            _statusLabel.ForeColor = ErrorColor;
            _statusLabel.Text = "启动失败";
            _fileLabel.Text = ex.Message;
            _footerLabel.Text = "文件已安装完成，可手动到安装目录启动软件";
            _primaryButton.Visible = false;
            _openFolderButton.Visible = true;
            _closeButton.Visible = true;
            UpdateInstallService.WriteLog("安装后启动失败：" + ex);
        }
    }

    private void OpenInstallFolder()
    {
        try
        {
            string folder = UpdateInstallService.GetInnerDirectory(_context.InstallRoot);
            if (!Directory.Exists(folder))
            {
                folder = _context.InstallRoot;
            }
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "打开安装目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void StartMarquee()
    {
        _marquee = true;
        _marqueeOffset = 0;
        _barFill.BackColor = AccentColor;
        _barFill.Width = 90;
        _barFill.Left = 0;
        _marqueeTimer.Start();
    }

    private void StopMarquee()
    {
        _marquee = false;
        _marqueeTimer.Stop();
    }

    private void MarqueeTimer_Tick(object? sender, EventArgs e)
    {
        if (!_marquee || _barTrack.ClientSize.Width <= 0)
        {
            return;
        }
        _marqueeOffset += 8;
        if (_marqueeOffset > _barTrack.ClientSize.Width)
        {
            _marqueeOffset = -_barFill.Width;
        }
        _barFill.Left = _marqueeOffset;
    }

    private void SetProgress(int percent)
    {
        if (_marquee)
        {
            return;
        }
        int value = Math.Max(0, Math.Min(100, percent));
        _barFill.Left = 0;
        _barFill.Width = (int)Math.Round(_barTrack.ClientSize.Width * value / 100.0);
    }

    private Button CreateButton(string text, int width)
    {
        Button button = new Button
        {
            Text = text,
            Size = new Size(width, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentColor,
            ForeColor = Color.FromArgb(0x20, 0x1A, 0x0B),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    internal sealed class InstallContext
    {
        internal string ZipPath { get; set; } = "";
        internal string InstallRoot { get; set; } = "";
        internal int MainProcessId { get; set; }
        internal string AppExePath { get; set; } = "";
        internal string Version { get; set; } = "";
    }
}
