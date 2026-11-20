using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Feedback
{
    public partial class MainForm : Form
    {
        private List<string> collectedFiles = new List<string>();
        private string? lastZipPath = null;
        private EmailConfig? emailConfig = null;
        private string currentLang = "zh"; // zh=中文, en=英文

        // 多语言字典
        private Dictionary<string, Dictionary<string, string>> lang = new Dictionary<string, Dictionary<string, string>>()
        {
            ["zh"] = new Dictionary<string, string>()
            {
                ["windowTitle"] = "问题反馈 - DLSS5+多帧生成开启工具",
                ["title"] = "问题反馈",
                ["descLabel"] = "问题描述：",
                ["contactLabel"] = "联系方式（可选）：",
                ["collectButton"] = "自动收集日志和配置",
                ["collectNotCollected"] = "未收集（请先点击收集日志，再发送给开发者）",
                ["collectCollected"] = "已收集 {0} 个文件",
                ["collectNotFound"] = "未找到日志文件",
                ["method1"] = "方式一（推荐）",
                ["sendButton"] = "直接发送给开发者",
                ["sending"] = "正在发送邮件...",
                ["sendSuccess"] = "邮件发送成功！",
                ["sendFailed"] = "邮件发送失败：",
                ["method2"] = "方式二：保存反馈文件到桌面",
                ["tip"] = "推荐使用方式一，任何网络环境都能用\n方式二保存到桌面后，通过QQ/微信/邮件发给开发者",
                ["langLabel"] = "语言：",
                ["collectSuccessTitle"] = "收集成功",
                ["collectSuccessMsg"] = "成功收集 {0} 个文件！\n\n软件目录：{1}\n\n收集的文件：\n{2}",
                ["collectWarnTitle"] = "提示",
                ["collectWarnMsg"] = "未找到日志文件！\n\n软件目录：{0}\n\n请确认软件目录是否正确，以及软件是否运行过生成了日志文件。",
                ["collectError"] = "收集日志时出错：{0}",
                ["errorTitle"] = "错误",
                ["pleaseEnterDesc"] = "请填写问题描述！",
                ["createZipError"] = "创建反馈文件时出错：{0}",
                ["emailConfigError"] = "邮件配置错误！",
                ["sendSuccessTitle"] = "发送成功",
                ["sendSuccessMsg"] = "反馈已成功发送到开发者邮箱！\n\n开发者会尽快查看并回复。",
                ["sendFailedTitle"] = "发送失败",
                ["sendFailedMsg"] = "邮件发送失败：{0}\n\n请检查网络连接和邮箱配置，或者使用其他方式发送。",
                ["submitSuccessTitle"] = "提交成功",
                ["submitSuccessMsg"] = "反馈提交成功！\n\n反馈文件已保存到桌面：\n{0}",
                ["saveError"] = "保存反馈文件时出错：{0}",
                ["feedbackInfoTitle"] = "问题反馈",
                ["submitTime"] = "提交时间：",
                ["contact"] = "联系方式：",
                ["desc"] = "问题描述：",
                ["sysInfo"] = "系统信息：",
                ["os"] = "操作系统：",
                ["is64bit"] = "64位系统：",
                ["cpuCount"] = "处理器数量：",
                ["machineName"] = "机器名：",
                ["backupInfo"] = "备份目录存在，包含以下游戏备份：\n",
                ["emailSubject"] = "【问题反馈】DLSS5+多帧生成开启工具 - {0}",
                ["emailBody"] = "收到新的问题反馈！\n\n提交时间：{0}\n详见附件中的反馈信息.txt\n\n此邮件由反馈工具自动发送，请勿直接回复。",
                ["emailFromName"] = "DLSS5+多帧生成工具反馈"
            },
            ["en"] = new Dictionary<string, string>()
            {
                ["windowTitle"] = "Feedback - DLSS5+MFG Enabler",
                ["title"] = "Feedback",
                ["descLabel"] = "Description:",
                ["contactLabel"] = "Contact (optional):",
                ["collectButton"] = "Auto Collect Logs & Config",
                ["collectNotCollected"] = "Not collected (please collect logs first, then send to developer)",
                ["collectCollected"] = "Collected {0} files",
                ["collectNotFound"] = "No log files found",
                ["method1"] = "Method 1 (Recommended)",
                ["sendButton"] = "Send Directly to Developer",
                ["sending"] = "Sending email...",
                ["sendSuccess"] = "Email sent successfully!",
                ["sendFailed"] = "Email failed: ",
                ["method2"] = "Method 2: Save Feedback to Desktop",
                ["tip"] = "Method 1 recommended, works in any network environment\nMethod 2: save to desktop, then send via QQ/WeChat/Email",
                ["langLabel"] = "Language:",
                ["collectSuccessTitle"] = "Collect Success",
                ["collectSuccessMsg"] = "Successfully collected {0} files!\n\nApp directory: {1}\n\nCollected files:\n{2}",
                ["collectWarnTitle"] = "Notice",
                ["collectWarnMsg"] = "No log files found!\n\nApp directory: {0}\n\nPlease verify the app directory is correct and the app has been run to generate logs.",
                ["collectError"] = "Error collecting logs: {0}",
                ["errorTitle"] = "Error",
                ["pleaseEnterDesc"] = "Please enter a description!",
                ["createZipError"] = "Error creating feedback file: {0}",
                ["emailConfigError"] = "Email configuration error!",
                ["sendSuccessTitle"] = "Send Success",
                ["sendSuccessMsg"] = "Feedback successfully sent to developer!\n\nDeveloper will review and reply soon.",
                ["sendFailedTitle"] = "Send Failed",
                ["sendFailedMsg"] = "Email failed: {0}\n\nPlease check network connection and email config, or use another method.",
                ["submitSuccessTitle"] = "Submit Success",
                ["submitSuccessMsg"] = "Feedback submitted successfully!\n\nFeedback file saved to desktop:\n{0}",
                ["saveError"] = "Error saving feedback file: {0}",
                ["feedbackInfoTitle"] = "Feedback",
                ["submitTime"] = "Submit Time: ",
                ["contact"] = "Contact: ",
                ["desc"] = "Description: ",
                ["sysInfo"] = "System Info:",
                ["os"] = "OS: ",
                ["is64bit"] = "64-bit: ",
                ["cpuCount"] = "CPU Cores: ",
                ["machineName"] = "Machine Name: ",
                ["backupInfo"] = "Backup directory exists, contains the following game backups:\n",
                ["emailSubject"] = "[Feedback] DLSS5+MFG Enabler - {0}",
                ["emailBody"] = "New feedback received!\n\nSubmit time: {0}\nSee feedback_info.txt in attachment\n\nThis email is auto-sent by feedback tool, please do not reply directly.",
                ["emailFromName"] = "DLSS5+MFG Enabler Feedback"
            }
        };

        private string T(string key)
        {
            if (lang.ContainsKey(currentLang) && lang[currentLang].ContainsKey(key))
                return lang[currentLang][key];
            return key;
        }

        private string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public MainForm()
        {
            // 检测系统语言
            if (System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh"))
                currentLang = "zh";
            else
                currentLang = "en";

            InitializeComponent();
            this.Text = T("windowTitle");
            this.Size = new Size(600, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            LoadEmailConfig();
            UpdateUILanguage();
        }

        private void InitializeComponent()
        {
            // 语言选择（右上角）
            Label langLabel = new Label();
            langLabel.Name = "langLabel";
            langLabel.Text = T("langLabel");
            langLabel.Font = new Font("微软雅黑", 9);
            langLabel.Location = new Point(420, 20);
            langLabel.Size = new Size(50, 20);
            this.Controls.Add(langLabel);

            ComboBox langCombo = new ComboBox();
            langCombo.Name = "langCombo";
            langCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            langCombo.Items.AddRange(new string[] { "中文", "English" });
            langCombo.SelectedIndex = currentLang == "zh" ? 0 : 1;
            langCombo.Location = new Point(475, 17);
            langCombo.Size = new Size(100, 25);
            langCombo.Font = new Font("微软雅黑", 9);
            langCombo.SelectedIndexChanged += LangCombo_SelectedIndexChanged;
            this.Controls.Add(langCombo);

            // 标题
            Label titleLabel = new Label();
            titleLabel.Name = "titleLabel";
            titleLabel.Text = T("title");
            titleLabel.Font = new Font("微软雅黑", 16, FontStyle.Bold);
            titleLabel.Location = new Point(20, 15);
            titleLabel.Size = new Size(200, 30);
            this.Controls.Add(titleLabel);

            // 问题描述
            Label descLabel = new Label();
            descLabel.Name = "descLabel";
            descLabel.Text = T("descLabel");
            descLabel.Font = new Font("微软雅黑", 10);
            descLabel.Location = new Point(20, 50);
            descLabel.Size = new Size(100, 20);
            this.Controls.Add(descLabel);

            TextBox descTextBox = new TextBox();
            descTextBox.Name = "descTextBox";
            descTextBox.Multiline = true;
            descTextBox.ScrollBars = ScrollBars.Vertical;
            descTextBox.Location = new Point(20, 75);
            descTextBox.Size = new Size(540, 110);
            descTextBox.Font = new Font("微软雅黑", 9);
            this.Controls.Add(descTextBox);

            // 联系方式
            Label contactLabel = new Label();
            contactLabel.Name = "contactLabel";
            contactLabel.Text = T("contactLabel");
            contactLabel.Font = new Font("微软雅黑", 10);
            contactLabel.Location = new Point(20, 195);
            contactLabel.Size = new Size(150, 20);
            this.Controls.Add(contactLabel);

            TextBox contactTextBox = new TextBox();
            contactTextBox.Name = "contactTextBox";
            contactTextBox.Location = new Point(20, 220);
            contactTextBox.Size = new Size(540, 25);
            contactTextBox.Font = new Font("微软雅黑", 9);
            this.Controls.Add(contactTextBox);

            // 收集日志按钮
            Button collectButton = new Button();
            collectButton.Name = "collectButton";
            collectButton.Text = T("collectButton");
            collectButton.Location = new Point(20, 260);
            collectButton.Size = new Size(180, 32);
            collectButton.Font = new Font("微软雅黑", 9);
            collectButton.Click += CollectButton_Click;
            this.Controls.Add(collectButton);

            Label collectStatus = new Label();
            collectStatus.Name = "collectStatus";
            collectStatus.Text = T("collectNotCollected");
            collectStatus.Font = new Font("微软雅黑", 8);
            collectStatus.ForeColor = Color.OrangeRed;
            collectStatus.Location = new Point(210, 270);
            collectStatus.Size = new Size(350, 18);
            this.Controls.Add(collectStatus);

            // 分隔线1
            Panel separator1 = new Panel();
            separator1.BackColor = Color.FromArgb(220, 220, 220);
            separator1.Location = new Point(20, 305);
            separator1.Size = new Size(540, 1);
            this.Controls.Add(separator1);

            // 方式一：邮件发送
            Label emailLabel = new Label();
            emailLabel.Name = "emailLabel";
            emailLabel.Text = T("method1");
            emailLabel.Font = new Font("微软雅黑", 9, FontStyle.Bold);
            emailLabel.ForeColor = Color.FromArgb(0, 100, 0);
            emailLabel.Location = new Point(20, 318);
            emailLabel.Size = new Size(300, 20);
            this.Controls.Add(emailLabel);

            Button emailButton = new Button();
            emailButton.Name = "emailButton";
            emailButton.Text = T("sendButton");
            emailButton.Location = new Point(20, 342);
            emailButton.Size = new Size(250, 35);
            emailButton.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            emailButton.BackColor = Color.FromArgb(0, 150, 80);
            emailButton.ForeColor = Color.White;
            emailButton.FlatStyle = FlatStyle.Flat;
            emailButton.Click += EmailButton_Click;
            this.Controls.Add(emailButton);

            Label sendStatus = new Label();
            sendStatus.Name = "sendStatus";
            sendStatus.Text = "";
            sendStatus.Font = new Font("微软雅黑", 8);
            sendStatus.ForeColor = Color.Gray;
            sendStatus.Location = new Point(280, 352);
            sendStatus.Size = new Size(280, 18);
            this.Controls.Add(sendStatus);

            // 分隔线2
            Panel separator2 = new Panel();
            separator2.BackColor = Color.FromArgb(220, 220, 220);
            separator2.Location = new Point(20, 395);
            separator2.Size = new Size(540, 1);
            this.Controls.Add(separator2);

            // 方式二：保存到本地
            Button submitButton = new Button();
            submitButton.Name = "submitButton";
            submitButton.Text = T("method2");
            submitButton.Location = new Point(20, 410);
            submitButton.Size = new Size(540, 38);
            submitButton.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            submitButton.BackColor = Color.FromArgb(100, 100, 100);
            submitButton.ForeColor = Color.White;
            submitButton.FlatStyle = FlatStyle.Flat;
            submitButton.Click += SubmitButton_Click;
            this.Controls.Add(submitButton);

            Label tipLabel = new Label();
            tipLabel.Name = "tipLabel";
            tipLabel.Text = T("tip");
            tipLabel.Font = new Font("微软雅黑", 8);
            tipLabel.ForeColor = Color.Gray;
            tipLabel.Location = new Point(20, 460);
            tipLabel.Size = new Size(540, 40);
            this.Controls.Add(tipLabel);
        }

        private void LangCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            ComboBox? combo = sender as ComboBox;
            if (combo != null && combo.SelectedIndex >= 0)
            {
                currentLang = combo.SelectedIndex == 0 ? "zh" : "en";
                UpdateUILanguage();
            }
        }

        private void UpdateUILanguage()
        {
            this.Text = T("windowTitle");

            // 更新所有控件
            foreach (Control ctrl in this.Controls)
            {
                switch (ctrl.Name)
                {
                    case "titleLabel":
                        ctrl.Text = T("title");
                        break;
                    case "descLabel":
                        ctrl.Text = T("descLabel");
                        break;
                    case "contactLabel":
                        ctrl.Text = T("contactLabel");
                        break;
                    case "collectButton":
                        ctrl.Text = T("collectButton");
                        break;
                    case "collectStatus":
                        // 只有显示"未收集"时才更新，已收集的状态保留
                        if (ctrl.ForeColor == Color.OrangeRed || ctrl.Text.Contains("未收集") || ctrl.Text.Contains("Not collected"))
                            ctrl.Text = T("collectNotCollected");
                        break;
                    case "emailLabel":
                        ctrl.Text = T("method1");
                        break;
                    case "emailButton":
                        ctrl.Text = T("sendButton");
                        break;
                    case "submitButton":
                        ctrl.Text = T("method2");
                        break;
                    case "tipLabel":
                        ctrl.Text = T("tip");
                        break;
                    case "langLabel":
                        ctrl.Text = T("langLabel");
                        break;
                }
            }
        }

        private void LoadEmailConfig()
        {
            // 硬编码邮箱配置，用户无需配置
            emailConfig = new EmailConfig
            {
                smtpServer = "smtp.qq.com",
                smtpPort = 587,
                fromEmail = "2669101491@qq.com",
                fromPassword = "yppskgvwfwikeade",
                toEmail = "2669101491@qq.com",
                enableSsl = true
            };
        }

        private void CollectButton_Click(object? sender, EventArgs e)
        {
            collectedFiles.Clear();
            int fileCount = 0;
            try
            {
                // 尝试多种方式找到软件主目录（DLSS5+MFG Enable文件夹）
                string appDir = "";
                
                // 方式1：当前目录下有DLSS5+MFG Enable文件夹（Feedback.exe和DFGE.exe都在测试版根目录）
                string tryDir1 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DLSS5+MFG Enable"));
                // 方式2：上一级目录下有DLSS5+MFG Enable文件夹
                string tryDir2 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "DLSS5+MFG Enable"));
                // 方式3：当前目录就是DLSS5+MFG Enable目录
                string tryDir3 = AppDomain.CurrentDomain.BaseDirectory;
                
                if (Directory.Exists(tryDir1))
                {
                    appDir = tryDir1;
                }
                else if (Directory.Exists(tryDir2))
                {
                    appDir = tryDir2;
                }
                else if (Directory.Exists(tryDir3) &&
                         (File.Exists(Path.Combine(tryDir3, "startup.log")) ||
                          File.Exists(Path.Combine(tryDir3, "error.log"))))
                {
                    appDir = tryDir3;
                }
                else
                {
                    // 都找不到，用方式1作为默认
                    appDir = tryDir1;
                }

                // 收集软件目录下所有的.log和.txt文件
                if (Directory.Exists(appDir))
                {
                    string[] allLogFiles = Directory.GetFiles(appDir, "*.log", SearchOption.TopDirectoryOnly);
                    string[] allTxtFiles = Directory.GetFiles(appDir, "*.txt", SearchOption.TopDirectoryOnly);
                    foreach (string file in allLogFiles)
                    {
                        collectedFiles.Add(file);
                        fileCount++;
                    }
                    foreach (string file in allTxtFiles)
                    {
                        collectedFiles.Add(file);
                        fileCount++;
                    }

                    // 收集软件目录下backup文件夹的信息
                    string backupDir = Path.Combine(appDir, "backup");
                    if (Directory.Exists(backupDir))
                    {
                        string backupInfo = T("backupInfo");
                        foreach (string gameDir in Directory.GetDirectories(backupDir))
                            backupInfo += "  - " + Path.GetFileName(gameDir) + "\n";
                        string backupInfoFile = Path.Combine(Path.GetTempPath(), "backup_info.txt");
                        File.WriteAllText(backupInfoFile, backupInfo);
                        collectedFiles.Add(backupInfoFile);
                        fileCount++;
                    }
                }

                // 收集AppData下的配置文件
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DLSSFrameGenEnabler");
                if (Directory.Exists(appDataDir))
                {
                    string[] configFiles = { "games.json", "games_webui_beta.json", "games_webui.json", "settings.json" };
                    foreach (string configFile in configFiles)
                    {
                        string filePath = Path.Combine(appDataDir, configFile);
                        if (File.Exists(filePath)) { collectedFiles.Add(filePath); fileCount++; }
                    }

                    // WebUI 的设置/游戏列表保存日志位于 AppData，单独按 *.log 收集。
                    foreach (string logFile in Directory.GetFiles(appDataDir, "*.log", SearchOption.TopDirectoryOnly))
                    {
                        collectedFiles.Add(logFile);
                        fileCount++;
                    }
                }

                Label collectStatus = this.Controls.Find("collectStatus", true)[0] as Label;
                if (fileCount > 0)
                {
                    collectStatus.Text = T("collectCollected", fileCount);
                    collectStatus.ForeColor = Color.Green;
                    string fileList = "";
                    foreach (string f in collectedFiles)
                    {
                        fileList += "  - " + Path.GetFileName(f) + "\n";
                    }
                    MessageBox.Show(T("collectSuccessMsg", fileCount, appDir, fileList), T("collectSuccessTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    collectStatus.Text = T("collectNotFound");
                    collectStatus.ForeColor = Color.Orange;
                    MessageBox.Show(T("collectWarnMsg", appDir), T("collectWarnTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(T("collectError", ex.Message), T("errorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string? CreateFeedbackZip()
        {
            TextBox descTextBox = this.Controls.Find("descTextBox", true)[0] as TextBox;
            TextBox contactTextBox = this.Controls.Find("contactTextBox", true)[0] as TextBox;
            string description = descTextBox.Text.Trim();
            string contact = contactTextBox.Text.Trim();
            if (string.IsNullOrEmpty(description))
            {
                MessageBox.Show(T("pleaseEnterDesc"), T("collectWarnTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), $"Feedback_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(tempDir);
                string feedbackInfo = $"{T("feedbackInfoTitle")}\n==========\n\n";
                feedbackInfo += $"{T("submitTime")}{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                feedbackInfo += $"{T("contact")}{contact}\n\n";
                feedbackInfo += $"{T("desc")}\n{description}\n\n";
                feedbackInfo += $"{T("sysInfo")}\n";
                feedbackInfo += $"  {T("os")}{Environment.OSVersion}\n";
                feedbackInfo += $"  {T("is64bit")}{Environment.Is64BitOperatingSystem}\n";
                feedbackInfo += $"  {T("cpuCount")}{Environment.ProcessorCount}\n";
                feedbackInfo += $"  {T("machineName")}{Environment.MachineName}\n";
                string infoFileName = currentLang == "zh" ? "反馈信息.txt" : "feedback_info.txt";
                File.WriteAllText(Path.Combine(tempDir, infoFileName), feedbackInfo, Encoding.UTF8);
                foreach (string file in collectedFiles)
                {
                    if (File.Exists(file))
                    {
                        string fileName = Path.GetFileName(file);
                        File.Copy(file, Path.Combine(tempDir, fileName), true);
                    }
                }
                string zipPrefix = currentLang == "zh" ? "反馈" : "feedback";
                string zipPath = Path.Combine(Path.GetTempPath(), $"{zipPrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);
                Directory.Delete(tempDir, true);
                return zipPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(T("createZipError", ex.Message), T("errorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private async void EmailButton_Click(object? sender, EventArgs e)
        {
            if (emailConfig == null)
            {
                MessageBox.Show(T("emailConfigError"), T("errorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string? zipPath = CreateFeedbackZip();
            if (zipPath == null) return;
            lastZipPath = zipPath;
            Button? emailButton = sender as Button;
            Label sendStatus = this.Controls.Find("sendStatus", true)[0] as Label;
            try
            {
                if (emailButton != null) emailButton.Enabled = false;
                sendStatus.Text = T("sending");
                sendStatus.ForeColor = Color.Orange;
                await Task.Run(() =>
                {
                    using (SmtpClient smtp = new SmtpClient(emailConfig.smtpServer, emailConfig.smtpPort))
                    {
                        smtp.EnableSsl = emailConfig.enableSsl;
                        smtp.Credentials = new NetworkCredential(emailConfig.fromEmail, emailConfig.fromPassword);
                        using (MailMessage mail = new MailMessage())
                        {
                            mail.From = new MailAddress(emailConfig.fromEmail, T("emailFromName"));
                            mail.To.Add(emailConfig.toEmail);
                            mail.Subject = T("emailSubject", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                            mail.Body = T("emailBody", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            mail.IsBodyHtml = false;
                            mail.Attachments.Add(new Attachment(zipPath));
                            smtp.Send(mail);
                        }
                    }
                });
                sendStatus.Text = T("sendSuccess");
                sendStatus.ForeColor = Color.Green;
                MessageBox.Show(T("sendSuccessMsg"), T("sendSuccessTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearForm();
            }
            catch (Exception ex)
            {
                sendStatus.Text = T("sendFailed") + ex.Message;
                sendStatus.ForeColor = Color.Red;
                MessageBox.Show(T("sendFailedMsg", ex.Message), T("sendFailedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { if (emailButton != null) emailButton.Enabled = true; }
        }

        private void SubmitButton_Click(object? sender, EventArgs e)
        {
            string? zipPath = CreateFeedbackZip();
            if (zipPath == null) return;
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = Path.GetFileName(zipPath);
                string destPath = Path.Combine(desktopPath, fileName);
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(zipPath, destPath);
                lastZipPath = destPath;
                MessageBox.Show(T("submitSuccessMsg", fileName), T("submitSuccessTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(T("saveError", ex.Message), T("errorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearForm()
        {
            TextBox descTextBox = this.Controls.Find("descTextBox", true)[0] as TextBox;
            TextBox contactTextBox = this.Controls.Find("contactTextBox", true)[0] as TextBox;
            Label collectStatus = this.Controls.Find("collectStatus", true)[0] as Label;
            descTextBox.Text = "";
            contactTextBox.Text = "";
            collectedFiles.Clear();
            collectStatus.Text = T("collectNotCollected");
            collectStatus.ForeColor = Color.OrangeRed;
        }
    }

    public class EmailConfig
    {
        public string smtpServer { get; set; } = "";
        public int smtpPort { get; set; } = 587;
        public string fromEmail { get; set; } = "";
        public string fromPassword { get; set; } = "";
        public string toEmail { get; set; } = "";
        public bool enableSsl { get; set; } = true;
    }
}
