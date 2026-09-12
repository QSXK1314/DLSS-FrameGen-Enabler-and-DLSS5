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
        public string XiaoheiheUrl { get; set; } = "https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=7c7772709af8&new_post_share_style=true";
        public string GithubUrl { get; set; } = "https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5";
        public string BilibiliUrl { get; set; } = "https://space.bilibili.com/414911649";
        public string UpdateCheckUrl { get; set; } = "https://gist.githubusercontent.com/QSXK1314/a9595f510bc16c77051ee386e085f8f1/raw/version.json";
        public bool RememberGames { get; set; } = true; // 是否记住游戏列表
        public string SkipUpdateVersion { get; set; } = ""; // 用户选择不再提示的版本号

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
                    // 迁移旧的小黑盒链接
                    if (settings.XiaoheiheUrl.Contains("link_id=0a86726d8f8b"))
                    {
                        settings.XiaoheiheUrl = "https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=7c7772709af8&new_post_share_style=true";
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
                File.AppendAllText(logPath, $"[{DateTime.Now}] 保存游戏列表，数量：{games.Count}\n");
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(Path.GetDirectoryName(GamesPath)!, "save_debug.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] 保存失败：{ex.Message}\n{ex.StackTrace}\n");
            }
        }

        // 加载游戏列表
        public static List<GameInfo> LoadGames()
        {
            try
            {
                var logPath = Path.Combine(Path.GetDirectoryName(GamesPath)!, "save_debug.log");
                if (File.Exists(GamesPath))
                {
                    var json = File.ReadAllText(GamesPath, System.Text.Encoding.UTF8);
                    var result = JsonSerializer.Deserialize<List<GameInfo>>(json) ?? new List<GameInfo>();
                    File.AppendAllText(logPath, $"[{DateTime.Now}] 加载游戏列表，数量：{result.Count}\n");
                    return result;
                }
                File.AppendAllText(logPath, $"[{DateTime.Now}] 游戏列表文件不存在\n");
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(Path.GetDirectoryName(GamesPath)!, "save_debug.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] 加载失败：{ex.Message}\n");
            }
            return new List<GameInfo>();
        }
    }
}
