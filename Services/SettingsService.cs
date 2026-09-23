using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using DLSSFrameGenEnabler_WinUI3.Models;

namespace DLSSFrameGenEnabler_WinUI3.Services;

public class SettingsService
{
	private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DLSSFrameGenEnabler", "settings.json");

	private static readonly string GamesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DLSSFrameGenEnabler", "games.json");

	private const string StableUpdateUrl = "https://gist.githubusercontent.com/QSXK1314/a9595f510bc16c77051ee386e085f8f1/raw/version.json";

	private const string BetaUpdateUrl = "https://gist.githubusercontent.com/QSXK1314/64d60ba126dc8ddabda2dfe39efe0970/raw/version-beta.json";

	private static SettingsService? _instance;

	public bool DarkMode { get; set; } = true;

	public bool ShowStartupDialog { get; set; } = true;

	public bool EnableAnimation { get; set; }

	public bool ShowUpdateLogOnFirstRun { get; set; } = true;

	public string LastVersion { get; set; } = "";

	public string SelectedGpuName { get; set; } = "";

	public int Language { get; set; }

	public int BackdropType { get; set; }

	public string CustomWallpaperPath { get; set; } = "";

	public string XiaoheiheUrl { get; set; } = "https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=f255727103c5&new_post_share_style=true";

	public string GithubUrl { get; set; } = "https://github.com/QSXK1314/DLSS-FrameGen-Enabler-and-DLSS5";

	public string BilibiliUrl { get; set; } = "https://space.bilibili.com/414911649";

	public string UpdateCheckUrl
	{
		get
		{
			return StableUpdateUrl;
		}
		set
		{
		}
	}

	public bool UseBetaUpdate { get; set; } = false;
	public bool RememberGames { get; set; } = true;

	public string SkipUpdateVersion { get; set; } = "";

	public double WindowWidth { get; set; }

	public double WindowHeight { get; set; }

	public bool ShowUsageGuide { get; set; } = true;

	public bool ShowBetaFeedback { get; set; } = false;

	public bool AutoCheckUpdate { get; set; } = true;

	public static SettingsService Instance => _instance ?? (_instance = Load());

	private static SettingsService Load()
	{
		try
		{
			if (File.Exists(SettingsPath))
			{
				SettingsService settingsService = JsonSerializer.Deserialize<SettingsService>(File.ReadAllText(SettingsPath)) ?? new SettingsService();
				string xiaoheiheUrl = "https://api.xiaoheihe.cn/v3/bbs/app/api/web/share?h_camp=link&h_src=YXBwX3NoYXJl&link_id=f255727103c5&new_post_share_style=true";
				if (!string.IsNullOrEmpty(settingsService.XiaoheiheUrl) && !settingsService.XiaoheiheUrl.Contains("link_id=f255727103c5"))
				{
					settingsService.XiaoheiheUrl = xiaoheiheUrl;
					settingsService.Save();
				}
				return settingsService;
			}
		}
		catch
		{
		}
		return new SettingsService();
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
			string contents = JsonSerializer.Serialize(this, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(SettingsPath, contents);
		}
		catch
		{
		}
	}

	public static void SaveGames(List<GameInfo> games)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(GamesPath));
			string contents = JsonSerializer.Serialize(games, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(GamesPath, contents, Encoding.UTF8);
			string path = Path.Combine(Path.GetDirectoryName(GamesPath), "save_debug.log");
			try
			{
				File.AppendAllText(path, $"[{DateTime.Now}] 保存游戏列表，数量：{games.Count}\n");
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(GamesPath));
				File.AppendAllText(Path.Combine(Path.GetDirectoryName(GamesPath), "save_debug.log"), $"[{DateTime.Now}] 保存失败：{ex.Message}\n{ex.StackTrace}\n");
			}
			catch
			{
			}
		}
	}

	public static List<GameInfo> LoadGames()
	{
		try
		{
			string directoryName = Path.GetDirectoryName(GamesPath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string path = Path.Combine(directoryName, "save_debug.log");
			if (File.Exists(GamesPath))
			{
				List<GameInfo> list = JsonSerializer.Deserialize<List<GameInfo>>(File.ReadAllText(GamesPath, Encoding.UTF8)) ?? new List<GameInfo>();
				try
				{
					File.AppendAllText(path, $"[{DateTime.Now}] 加载游戏列表，数量：{list.Count}\n");
				}
				catch
				{
				}
				return list;
			}
			try
			{
				File.AppendAllText(path, $"[{DateTime.Now}] 游戏列表文件不存在\n");
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			try
			{
				string directoryName2 = Path.GetDirectoryName(GamesPath);
				if (!string.IsNullOrEmpty(directoryName2))
				{
					Directory.CreateDirectory(directoryName2);
				}
				File.AppendAllText(Path.Combine(directoryName2, "save_debug.log"), $"[{DateTime.Now}] 加载失败：{ex.Message}\n");
			}
			catch
			{
			}
		}
		return new List<GameInfo>();
	}
}
