using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DLSSFrameGenEnabler_WinUI3.Services;

/// <summary>
/// dlssg_sm86.ini 的读写：只改我们认识的键，保留出厂文件里的全部注释、空行和其它键。
/// 出厂文件为 UTF-8 无 BOM、LF 换行，写回时保持一致。
/// </summary>
public static class SM86IniConfig
{
	public const string FileName = "dlssg_sm86.ini";

	public static string GetPath(string exeDir)
	{
		return Path.Combine(exeDir, FileName);
	}

	public static Dictionary<string, string> Read(string iniPath)
	{
		Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string section = "";
		foreach (string raw in File.ReadAllLines(iniPath))
		{
			string line = raw.Trim();
			if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
			{
				continue;
			}
			if (line.StartsWith("[") && line.EndsWith("]"))
			{
				section = line.Substring(1, line.Length - 2).Trim();
				continue;
			}
			int eq = line.IndexOf('=');
			if (eq <= 0 || section.Length == 0)
			{
				continue;
			}
			string key = line.Substring(0, eq).Trim();
			string value = line.Substring(eq + 1).Trim();
			result[section + "." + key] = value;
		}
		return result;
	}

	/// <summary>
	/// 按 "节.键" 写入值：已存在的键原地替换，不存在的键插入到对应节末尾；注释全部保留。
	/// </summary>
	public static void Apply(string iniPath, Dictionary<string, string> values)
	{
		string text = File.ReadAllText(iniPath, new UTF8Encoding(false));
		List<string> lines = new List<string>(text.Split('\n'));
		HashSet<string> applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string section = "";
		for (int i = 0; i < lines.Count; i++)
		{
			string trimmed = lines[i].Trim();
			if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
			{
				continue;
			}
			if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
			{
				section = trimmed.Substring(1, trimmed.Length - 2).Trim();
				continue;
			}
			int eq = trimmed.IndexOf('=');
			if (eq <= 0)
			{
				continue;
			}
			string key = section + "." + trimmed.Substring(0, eq).Trim();
			if (!values.ContainsKey(key))
			{
				continue;
			}
			lines[i] = trimmed.Substring(0, eq).Trim() + "=" + values[key];
			applied.Add(key);
		}
		foreach (KeyValuePair<string, string> pair in values)
		{
			if (applied.Contains(pair.Key))
			{
				continue;
			}
			string[] parts = pair.Key.Split('.');
			if (parts.Length != 2)
			{
				continue;
			}
			InsertKey(lines, parts[0], parts[1], pair.Value);
		}
		File.WriteAllText(iniPath, string.Join("\n", lines), new UTF8Encoding(false));
	}

	private static void InsertKey(List<string> lines, string section, string key, string value)
	{
		int start = -1;
		int end = lines.Count;
		for (int i = 0; i < lines.Count; i++)
		{
			string trimmed = lines[i].Trim();
			if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]"))
			{
				continue;
			}
			if (start >= 0)
			{
				end = i;
				break;
			}
			if (trimmed.Substring(1, trimmed.Length - 2).Trim().Equals(section, StringComparison.OrdinalIgnoreCase))
			{
				start = i;
			}
		}
		if (start < 0)
		{
			lines.Add("");
			lines.Add("[" + section + "]");
			lines.Add(key + "=" + value);
			return;
		}
		int insertAt = start + 1;
		for (int i = start + 1; i < end; i++)
		{
			if (lines[i].Trim().Length > 0)
			{
				insertAt = i + 1;
			}
		}
		lines.Insert(insertAt, key + "=" + value);
	}

	/// <summary>
	/// 用补丁目录里的出厂 ini 覆盖游戏目录里的那一份。
	/// </summary>
	public static bool RestoreDefault(string iniPath, string patchDir)
	{
		try
		{
			string source = Path.Combine(patchDir, FileName);
			if (!File.Exists(source))
			{
				return false;
			}
			File.Copy(source, iniPath, overwrite: true);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
