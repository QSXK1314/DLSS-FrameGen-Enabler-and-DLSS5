using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DLSSFrameGenEnabler_WinUI3.Models;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DLSSFrameGenEnabler", "settings.json");

        private static readonly string GamesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DLSSFrameGenEnabler", "games.json");

        public bool DarkMode { get; set; } = true;
        public bool ShowStartupDialog { get; set; } = true;
        public bool EnableAnimation { get; set; } = false;
        public bool ShowUpdateLogOnFirstRun { get; set; } = true;
        public string LastVersion { get; set; } = "";
        public string SelectedGpuName { get; set; } = ""; // 空表示自动检测
        public int Language { get; set; } = 0; // 0=Auto, 1=Chinese, 2=English
        public int BackdropType { get; set; } = 0; // 0=默认, 1=Mica云母, 2=Acrylic亚克力, 4=自定义壁纸, 5=纯透明模糊
        public string CustomWallpaperPath { get; set; } = ""; // 自定义壁纸路径
        public string XiaoheiheUrl { get; set; } = "https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=f255727103c5&new_post_share_style=true";
        public string GithubUrl { get; set; } = "https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5";
        public string BilibiliUrl { get; set; } = "https://space.bilibili.com/414911649";
        
        // 正式版和测试版使用不同的version URL（都是不带commit hash的永久链接，修改内容不会失效）
        private const string StableUpdateUrl = "https://gist.githubusercontent.com/QSXK1314/a9595f510bc16c77051ee386e085f8f1/raw/version.json";
        private const string BetaUpdateUrl = "https://gist.githubusercontent.com/QSXK1314/64d60ba126dc8ddabda2dfe39efe0970/raw/version-beta.json";
        
        // 动态返回对应版本的检查更新URL
        public string UpdateCheckUrl 
        { 
            get => App.IsBeta ? BetaUpdateUrl : StableUpdateUrl;
            set { /* 忽略set，URL由版本类型动态决定 */ } 
        }
        public bool RememberGames { get; set; } = true; // 是否记住游戏列表
        public string SkipUpdateVersion { get; set; } = ""; // 用户选择不再提示的版本号
        public double WindowWidth { get; set; } = 0; // 记忆的窗口宽度，0表示使用默认
        public double WindowHeight { get; set; } = 0; // 记忆的窗口高度，0表示使用默认
        public bool ShowUsageGuide { get; set; } = true; // 是否显示使用前说明弹窗
        public bool ShowBetaFeedback { get; set; } = true; // 是否显示测试版反馈提示弹窗

        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= Load();

        private static SettingsService Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<SettingsService>(json) ?? new SettingsService();
                    // 迁移旧的小黑盒链接（只要不是最新链接就都迁移）
                    var latestXiaoheiheUrl = "https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=f255727103c5&new_post_share_style=true";
                    if (!string.IsNullOrEmpty(settings.XiaoheiheUrl) && !settings.XiaoheiheUrl.Contains("link_id=f255727103c5"))
                    {
                        settings.XiaoheiheUrl = latestXiaoheiheUrl;
                        settings.Save();
                    }
                    return settings;
                }
            }
            catch { }
            return new SettingsService();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        // 保存游戏列表
        public static void SaveGames(List<GameInfo> games)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GamesPath)!);
                var json = JsonSerializer.Serialize(games, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GamesPath, json, System.Text.Encoding.UTF8);
                // 写入调试日志
                var logPath = Path.Combine(Path.GetDirectoryName(GamesPath)!, "save_debug.log");
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] 保存游戏列表，数量：{games.Count}\n"); } catch { }
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(GamesPath)!);
                    var logPath = Path.Combine(Path.GetDirectoryName(GamesPath)!, "save_debug.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] 保存失败：{ex.Message}\n{ex.StackTrace}\n");
                }
                catch { }
            }
        }

        // 加载游戏列表
        public static List<GameInfo> LoadGames()
        {
            try
            {
                // 确保目录存在（关键修复：新电脑上目录不存在会导致崩溃）
                var gamesDir = Path.GetDirectoryName(GamesPath);
                if (!string.IsNullOrEmpty(gamesDir))
                {
                    Directory.CreateDirectory(gamesDir);
                }

                var logPath = Path.Combine(gamesDir!, "save_debug.log");
                if (File.Exists(GamesPath))
                {
                    var json = File.ReadAllText(GamesPath, System.Text.Encoding.UTF8);
                    var result = JsonSerializer.Deserialize<List<GameInfo>>(json) ?? new List<GameInfo>();
                    try { File.AppendAllText(logPath, $"[{DateTime.Now}] 加载游戏列表，数量：{result.Count}\n"); } catch { }
                    return result;
                }
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] 游戏列表文件不存在\n"); } catch { }
            }
            catch (Exception ex)
            {
                // 日志写入也要包在try-catch中，避免二次崩溃
                try
                {
                    var gamesDir = Path.GetDirectoryName(GamesPath);
                    if (!string.IsNullOrEmpty(gamesDir))
                    {
                        Directory.CreateDirectory(gamesDir);
                    }
                    var logPath = Path.Combine(gamesDir!, "save_debug.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] 加载失败：{ex.Message}\n");
                }
                catch { }
            }
            return new List<GameInfo>();
        }
    }
}
