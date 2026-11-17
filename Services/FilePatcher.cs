using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DLSSFrameGenEnabler_WinUI3.Models;

namespace DLSSFrameGenEnabler_WinUI3.Services;

public class FilePatcher
{
	private static readonly string PatchesBaseDir = Path.Combine(AppContext.BaseDirectory, "Patches");

	private static readonly string SharedPatchDir = Path.Combine(PatchesBaseDir, "_Shared");

	private static readonly string BackupBaseDir = Path.Combine(AppContext.BaseDirectory, "backup");

	private static readonly Dictionary<string, string[]> SharedFiles = new Dictionary<string, string[]>
	{
		["nvngx_dlssnr.dll"] = new string[6] { "N卡DLSS5补丁", "A卡DLSS5补丁（仅支持7000系和9000系）", "dx9 32位dlss5补丁\\host64", "dx9 64位dlss5补丁", "燕云十六声DLSS5补丁", "燕云十六声DLSS5补丁\\Streamline" },
		["nvngx_dlss.dll"] = new string[7] { "2077专用多帧生成补丁", "40系原生多帧生成\\排错", "dx9 32位dlss5补丁\\host64", "dx9 64位dlss5补丁", "RE引擎通用dlss5补丁\\DLSS5 on RE", "燕云十六声DLSS5补丁\\Streamline", "老驱动多帧生成\\排错" }
	};

	private static string[] GetFilesWithDepthLimit(string rootPath, string searchPattern, int maxDepth = 4)
	{
		List<string> list = new List<string>();
		try
		{
			list.AddRange(Directory.GetFiles(rootPath, searchPattern, SearchOption.TopDirectoryOnly));
			if (maxDepth <= 1)
			{
				return list.ToArray();
			}
			SearchFilesRecursive(rootPath, searchPattern, 1, maxDepth, list);
		}
		catch
		{
		}
		return list.ToArray();
	}

	private static void SearchFilesRecursive(string dir, string pattern, int currentDepth, int maxDepth, List<string> result)
	{
		if (currentDepth >= maxDepth)
		{
			return;
		}
		try
		{
			string[] directories = Directory.GetDirectories(dir);
			foreach (string text in directories)
			{
				try
				{
					result.AddRange(Directory.GetFiles(text, pattern, SearchOption.TopDirectoryOnly));
					if (currentDepth + 1 < maxDepth)
					{
						SearchFilesRecursive(text, pattern, currentDepth + 1, maxDepth, result);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static string[] GetDirectoriesWithDepthLimit(string rootPath, string searchPattern, int maxDepth = 4)
	{
		List<string> list = new List<string>();
		try
		{
			SearchDirectoriesRecursive(rootPath, searchPattern, 0, maxDepth, list);
		}
		catch
		{
		}
		return list.ToArray();
	}

	private static void SearchDirectoriesRecursive(string dir, string pattern, int currentDepth, int maxDepth, List<string> result)
	{
		if (currentDepth >= maxDepth)
		{
			return;
		}
		try
		{
			string[] directories = Directory.GetDirectories(dir);
			foreach (string text in directories)
			{
				try
				{
					string fileName = Path.GetFileName(text);
					if (pattern == "*" || fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
					{
						result.Add(text);
					}
					SearchDirectoriesRecursive(text, pattern, currentDepth + 1, maxDepth, result);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	public static void EnsurePatchesExtracted()
	{
	}

	private static string ResolvePatchFile(string patchDir, string fileName)
	{
		string text = Path.Combine(patchDir, fileName);
		if (File.Exists(text))
		{
			return text;
		}
		string text2 = Path.Combine(SharedPatchDir, fileName);
		if (File.Exists(text2))
		{
			return text2;
		}
		return text;
	}

	private static void CopyPatchFiles(string patchDir, string destDir, string backupDir, bool backupExisting = true, string[]? skipExtensions = null, string[]? treatAsAddedFiles = null, string[]? skipBackupFiles = null)
	{
		List<string> list = new List<string>();
		if (Directory.Exists(patchDir))
		{
			string[] files = Directory.GetFiles(patchDir);
			for (int i = 0; i < files.Length; i++)
			{
				string fileName = Path.GetFileName(files[i]);
				if (skipExtensions != null)
				{
					string ext = Path.GetExtension(fileName).ToLower();
					if (skipExtensions.Any((string e) => ext == e.ToLower()))
					{
						continue;
					}
				}
				list.Add(fileName);
			}
		}
		if (Directory.Exists(SharedPatchDir))
		{
			string[] files = Directory.GetFiles(SharedPatchDir);
			for (int i = 0; i < files.Length; i++)
			{
				string fileName2 = Path.GetFileName(files[i]);
				if (!list.Contains(fileName2))
				{
					list.Add(fileName2);
				}
			}
		}
		foreach (string item in list)
		{
			string text = ResolvePatchFile(patchDir, item);
			if (File.Exists(text))
			{
				string text2 = Path.Combine(destDir, item);
				bool flag = File.Exists(text2);
				// 新增补丁（如dlssnr等）不备份原文件，只记录去向
				bool isAddedPatch = IsAddedPatchFile(item);
					// 额外指定的新增补丁文件（如DLSS5的nvngx_dlss.dll）
					if (treatAsAddedFiles != null && treatAsAddedFiles.Any((string f) => f.Equals(item, StringComparison.OrdinalIgnoreCase)))
					{
						isAddedPatch = true;
					}
				if (backupExisting && flag && !isAddedPatch)
				{
					BackupFile(text2, backupDir);
				}
				File.Copy(text, text2, overwrite: true);
				if (!flag || isAddedPatch)
				{
					RecordAddedFile(backupDir, text2);
				}
			}
		}
	}

	private static string GetPatchDir(string subDir)
	{
		return Path.Combine(PatchesBaseDir, subDir);
	}

	private static string GetExeDir(GameInfo game)
	{
		return Path.GetDirectoryName(game.ExePath);
	}

	private static string GetBackupDir(GameInfo game, string feature, bool incremental = false)
	{
		// DLSS5和多帧生成使用独立的基础目录，避免互相影响递增
		string dirSuffix = feature.Contains("DLSS5") ? " DLSS5" : "";
		// 把游戏名里的Windows非法字符替换成下划线
		char[] invalidChars = Path.GetInvalidFileNameChars();
		string safeGameName = new string(game.Name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
		string baseGameDir = Path.Combine(BackupBaseDir, safeGameName + dirSuffix);
		string targetGameDir = baseGameDir;
		
		if (incremental)
		{
			// 递增备份：检查已存在的备份目录，创建下一个递增的
			int version = 1;
			if (Directory.Exists(baseGameDir))
			{
				version = 2;
				while (Directory.Exists(baseGameDir + "V" + version))
				{
					version++;
				}
				targetGameDir = baseGameDir + "V" + version;
			}
		}
		
		string text = Path.Combine(targetGameDir, feature);
		Directory.CreateDirectory(text);
		try
		{
			File.WriteAllText(Path.Combine(text, "game_path.txt"), game.GamePath);
		}
		catch
		{
		}
		return text;
	}

	private static void CopyDirectory(string source, string dest, bool overwrite = true, bool includeSharedFiles = false, string? backupDir = null, string? gameRootDir = null)
	{
		bool num = Directory.Exists(dest);
		Directory.CreateDirectory(dest);
		if (!num && backupDir != null)
		{
			RecordAddedDir(backupDir, dest);
		}
		List<string> list = new List<string>(Directory.GetFiles(source));
		string[] files;
		if (includeSharedFiles && Directory.Exists(SharedPatchDir))
		{
			files = Directory.GetFiles(SharedPatchDir);
			foreach (string text in files)
			{
				string fileName = Path.GetFileName(text);
				string text2 = Path.Combine(source, fileName);
				if (!File.Exists(text2) && !list.Contains(text2))
				{
					list.Add(text);
				}
			}
		}
		foreach (string item in list)
		{
			string fileName2 = Path.GetFileName(item);
			string text3 = Path.Combine(dest, fileName2);
			string text4 = ResolvePatchFile(source, fileName2);
			if (File.Exists(text4))
			{
				bool num2 = File.Exists(text3);
				// 目标文件已存在时必须先备份原文件，否则还原时会把它当成补丁文件删掉
				if (num2 && backupDir != null)
				{
					BackupFile(text3, backupDir, gameRootDir);
				}
				File.Copy(text4, text3, overwrite);
				if (!num2 && backupDir != null)
				{
					RecordAddedFile(backupDir, text3);
				}
			}
		}
		files = Directory.GetDirectories(source);
		foreach (string text5 in files)
		{
			CopyDirectory(text5, Path.Combine(dest, Path.GetFileName(text5)), overwrite, includeSharedFiles: false, backupDir, gameRootDir);
		}
	}

	private static void CopyFontsFiles(string exeDir, string? backupDir = null)
	{
		try
		{
			string text = Path.Combine(SharedPatchDir, "Fonts");
			if (!Directory.Exists(text))
			{
				return;
			}
			string text2 = Path.Combine(exeDir, "Fonts");
			bool num = Directory.Exists(text2);
			if (!num)
			{
				Directory.CreateDirectory(text2);
			}
			if (!num && backupDir != null)
			{
				RecordAddedDir(backupDir, text2);
			}
			string[] files = Directory.GetFiles(text, "*", SearchOption.AllDirectories);
			foreach (string text3 in files)
			{
				string path = text3.Substring(text.Length + 1);
				string text4 = Path.Combine(text2, path);
				string directoryName = Path.GetDirectoryName(text4);
				if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
					if (backupDir != null)
					{
						RecordAddedDir(backupDir, directoryName);
					}
				}
				bool num2 = File.Exists(text4);
				// 游戏自带同名字体时先备份，否则还原会把原生字体删掉
				if (num2 && backupDir != null)
				{
					BackupFile(text4, backupDir, exeDir);
				}
				File.Copy(text3, text4, overwrite: true);
				if (!num2 && backupDir != null)
				{
					RecordAddedFile(backupDir, text4);
				}
			}
		}
		catch
		{
		}
	}

	private static void CopyReShadeFiles(string exeDir, string? backupDir = null)
	{
		try
		{
			string gitcDir = GetPatchDir("GITC-DLSS5补丁\\dx12");
			// 复制ReShade.ini配置文件
			string iniSrc = Path.Combine(gitcDir, "ReShade.ini");
			if (File.Exists(iniSrc))
			{
				string iniDest = Path.Combine(exeDir, "ReShade.ini");
				if (!File.Exists(iniDest))
				{
					File.Copy(iniSrc, iniDest, overwrite: true);
					if (backupDir != null) RecordAddedFile(backupDir, iniDest);
				}
			}
			// 复制reshade-shaders着色器文件夹
			string shadersSrc = Path.Combine(gitcDir, "reshade-shaders");
			if (Directory.Exists(shadersSrc))
			{
				string shadersDest = Path.Combine(exeDir, "reshade-shaders");
				if (!Directory.Exists(shadersDest))
				{
					CopyDirectory(shadersSrc, shadersDest, overwrite: true, includeSharedFiles: false, backupDir);
				}
			}
		}
		catch
		{
		}
	}

	private static void BackupFile(string sourceFile, string backupDir, string gameRootDir = null)
	{
		if (!File.Exists(sourceFile))
		{
			return;
		}
		string fileName = Path.GetFileName(sourceFile);
		string relativePath = fileName;
		string text;
		
		// 如果传入了游戏根目录，就按照原文件的目录结构备份
		if (!string.IsNullOrEmpty(gameRootDir) && sourceFile.StartsWith(gameRootDir, StringComparison.OrdinalIgnoreCase))
		{
			relativePath = sourceFile.Substring(gameRootDir.Length).TrimStart('\\', '/');
			text = Path.Combine(backupDir, relativePath);
			string backupFileDir = Path.GetDirectoryName(text);
			if (!string.IsNullOrEmpty(backupFileDir) && !Directory.Exists(backupFileDir))
			{
				Directory.CreateDirectory(backupFileDir);
			}
		}
		else
		{
			text = Path.Combine(backupDir, fileName);
		}
		
		if (!File.Exists(text))
		{
			File.Copy(sourceFile, text, overwrite: true);
		}
		try
		{
			string path = Path.Combine(backupDir, "backup_manifest.txt");
			string item = relativePath + "|" + sourceFile;
			List<string> list = (File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>());
			if (!list.Any((string e) => e.StartsWith(relativePath + "|")))
			{
				list.Add(item);
				File.WriteAllLines(path, list);
			}
		}
		catch
		{
		}
		try
		{
			string text2 = Path.Combine(backupDir, "pre_install_snapshot.txt");
			if (File.Exists(text2))
			{
				return;
			}
			string text3 = Path.GetDirectoryName(sourceFile);
			while (!string.IsNullOrEmpty(text3) && Directory.GetFiles(text3, "*.exe").Length == 0)
			{
				DirectoryInfo parent = Directory.GetParent(text3);
				if (parent == null)
				{
					break;
				}
				text3 = parent.FullName;
			}
			if (!string.IsNullOrEmpty(text3) && Directory.Exists(text3))
			{
				SaveDirectorySnapshot(text3, text2);
			}
		}
		catch
		{
		}
	}

	private static void SaveDirectorySnapshot(string rootPath, string snapshotFile)
	{
		try
		{
			string[] filesWithDepthLimit = GetFilesWithDepthLimit(rootPath, "*", 8);
			using StreamWriter streamWriter = new StreamWriter(snapshotFile);
			streamWriter.WriteLine("ROOT|" + rootPath);
			string[] array = filesWithDepthLimit;
			foreach (string text in array)
			{
				try
				{
					string value = text.Substring(rootPath.Length).TrimStart(new char[2] { '\\', '/' });
					streamWriter.WriteLine(value);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static (string rootPath, HashSet<string> files) LoadDirectorySnapshot(string snapshotFile)
	{
		string item = "";
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			if (File.Exists(snapshotFile))
			{
				string[] array = File.ReadAllLines(snapshotFile);
				foreach (string text in array)
				{
					if (!string.IsNullOrWhiteSpace(text))
					{
						if (text.StartsWith("ROOT|"))
						{
							item = text.Substring(5).Trim();
						}
						else
						{
							hashSet.Add(text.Trim().Replace('/', '\\'));
						}
					}
				}
			}
		}
		catch
		{
		}
		return (rootPath: item, files: hashSet);
	}

	private static string? GetOriginalPath(string backupDir, string fileName)
	{
		try
		{
			string path = Path.Combine(backupDir, "backup_manifest.txt");
			if (!File.Exists(path))
			{
				return null;
			}
			string[] array = File.ReadAllLines(path);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split('|', 2);
				if (array2.Length == 2 && array2[0] == fileName)
				{
					return array2[1];
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static void RecordAddedFile(string backupDir, string filePath)
	{
		try
		{
			string path = Path.Combine(backupDir, "added_files.txt");
			List<string> list = (File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>());
			if (!list.Contains<string>(filePath, StringComparer.OrdinalIgnoreCase))
			{
				list.Add(filePath);
				File.WriteAllLines(path, list);
			}
		}
		catch
		{
		}
	}

	private static void RecordAddedDir(string backupDir, string dirPath)
	{
		try
		{
			string path = Path.Combine(backupDir, "added_dirs.txt");
			List<string> list = (File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>());
			if (!list.Contains<string>(dirPath, StringComparer.OrdinalIgnoreCase))
			{
				list.Add(dirPath);
				File.WriteAllLines(path, list);
			}
		}
		catch
		{
		}
	}
	public static void RecordConfigNameRename(GameInfo game, string oldFilePath, string newFilePath)
	{
		try
		{
			string latestBackupDir = null;
			DateTime latestTime = DateTime.MinValue;
			if (Directory.Exists(BackupBaseDir))
			{
				string[] allDirs = Directory.GetDirectories(BackupBaseDir);
				foreach (string dir in allDirs)
				{
					string dirName = Path.GetFileName(dir);
					if (dirName.Equals(game.Name, StringComparison.OrdinalIgnoreCase) || 
						dirName.StartsWith(game.Name + "V", StringComparison.OrdinalIgnoreCase) ||
						dirName.StartsWith(game.Name + "v", StringComparison.OrdinalIgnoreCase))
					{
						DateTime dirTime = Directory.GetLastWriteTime(dir);
						if (dirTime > latestTime)
						{
							latestTime = dirTime;
							latestBackupDir = dir;
						}
					}
				}
			}
			if (latestBackupDir == null) return;
			string frameGenDir = Path.Combine(latestBackupDir, "FrameGen_Advanced");
			if (!Directory.Exists(frameGenDir))
			{
				string[] subDirs = Directory.GetDirectories(latestBackupDir);
				foreach (string sd in subDirs)
				{
					if (Path.GetFileName(sd).Contains("FrameGen"))
					{
						frameGenDir = sd;
						break;
					}
				}
			}
			if (!Directory.Exists(frameGenDir)) return;
			string addedFilesPath = Path.Combine(frameGenDir, "added_files.txt");
			if (File.Exists(addedFilesPath))
			{
				List<string> list = File.ReadAllLines(addedFilesPath).ToList();
				list.RemoveAll(f => f.Equals(oldFilePath, StringComparison.OrdinalIgnoreCase));
				if (!list.Contains(newFilePath, StringComparer.OrdinalIgnoreCase))
				{
					list.Add(newFilePath);
				}
				File.WriteAllLines(addedFilesPath, list);
			}
			else
			{
				RecordAddedFile(frameGenDir, newFilePath);
			}
		}
		catch
		{
		}
	}

	private static HashSet<string> LoadAddedFiles(string backupDir)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			string path = Path.Combine(backupDir, "added_files.txt");
			if (File.Exists(path))
			{
				string[] array = File.ReadAllLines(path);
				foreach (string text in array)
				{
					if (!string.IsNullOrWhiteSpace(text))
					{
						hashSet.Add(text.Trim());
					}
				}
			}
		}
		catch
		{
		}
		return hashSet;
	}

	// 获取DLSS5新增的所有文件列表（用于多帧生成安装时跳过备份这些文件）
	private static string[] GetDLSS5AddedFiles(GameInfo game)
	{
		List<string> result = new List<string>();
		try
		{
			string dlss5BaseName = game.Name + " DLSS5";
			if (Directory.Exists(BackupBaseDir))
			{
				foreach (string bd in Directory.GetDirectories(BackupBaseDir))
				{
					string bdName = Path.GetFileName(bd);
					if (bdName.Equals(dlss5BaseName, StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(dlss5BaseName + "V", StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(dlss5BaseName + "v", StringComparison.OrdinalIgnoreCase))
					{
						foreach (string subDir in Directory.GetDirectories(bd))
						{
							// 读取added_files.txt中的新增文件
							foreach (string f in LoadAddedFiles(subDir))
							{
								if (!result.Contains(f, StringComparer.OrdinalIgnoreCase))
								{
									result.Add(f);
								}
							}
							// 同时读取backup_manifest.txt中的被替换文件（如dxgi.dll、ReShade.ini等）
							string manifestPath = Path.Combine(subDir, "backup_manifest.txt");
							if (File.Exists(manifestPath))
							{
								foreach (string line in File.ReadAllLines(manifestPath))
								{
									string[] parts = line.Split('|', 2);
									if (parts.Length == 2)
									{
										string originalPath = parts[1];
										if (!result.Contains(originalPath, StringComparer.OrdinalIgnoreCase))
										{
											result.Add(originalPath);
										}
									}
								}
							}
						}
					}
				}
			}
		}
		catch
		{
		}
		return result.ToArray();
	}

	private static HashSet<string> LoadAddedDirs(string backupDir)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			string path = Path.Combine(backupDir, "added_dirs.txt");
			if (File.Exists(path))
			{
				string[] array = File.ReadAllLines(path);
				foreach (string text in array)
				{
					if (!string.IsNullOrWhiteSpace(text))
					{
						hashSet.Add(text.Trim());
					}
				}
			}
		}
		catch
		{
		}
		return hashSet;
	}

	private static bool IsAddedPatchFile(string fileName)
	{
		string text = fileName.ToLower();
		if (text.StartsWith("reshade"))
		{
			return true;
		}
		if (text.StartsWith("rtx40mfg"))
		{
			return true;
		}
		if (text.StartsWith("dlss5-feed"))
		{
			return true;
		}
		if (text.StartsWith("dgvoodoo"))
		{
			return true;
		}
		if (text.StartsWith("re_dlss5"))
		{
			return true;
		}
		if (text == "nvngx_dlssnr.dll")
		{
			return true;
		}
		if (new string[10] { "dxgi.dll", "d3d9.dll", "d3d11.dll", "d3d12.dll", "version.dll", "dinput8.dll", "winmm.dll", "dsound.dll", "xinputuap.dll", "dlssnr_on_amd_setup.exe" }.Contains(text))
		{
			return true;
		}
		if (new string[3] { "version.ini", "global.ini", "dlssg_sm86.ini" }.Contains(text))
		{
			return true;
		}
		if (text.EndsWith(".addon64") || text.EndsWith(".asi") || text.EndsWith(".fx"))
		{
			return true;
		}
		if (text == "misans-bold.ttf")
		{
			return true;
		}
		if (text == "rtx40mfg-universal.status.json")
		{
			return true;
		}
		return false;
	}

	// 判断是否是多帧生成特有的文件（DLSS5还原时不应该删除这些文件）
	private static bool IsFrameGenSpecificFile(string fileName)
	{
		string text = fileName.ToLower();
		// 帧生成补丁
		if (text == "nvngx_dlssg.dll") return true;
		// version相关
		if (text == "version.dll" || text == "version.ini") return true;
		// RTX40MFG相关
		if (text.StartsWith("rtx40mfg")) return true;
		// 排错补丁
		if (text.StartsWith("sl.")) return true;
		// dlssg配置
		if (text == "dlssg_sm86.ini") return true;
		return false;
	}

	private static bool IsReplacablePatchFile(string fileName)
	{
		string text = fileName.ToLower();
		switch (text)
		{
		case "nvngx_dlss.dll":
			return true;
		case "nvngx_dlssg.dll":
			return true;
		case "nvngx_dlssd.dll":
			return true;
		default:
			if (text.StartsWith("sl."))
			{
				return true;
			}
			switch (text)
			{
			case "d3dcompiler_47.dll":
				return true;
			case "nvlowlatencyvk.dll":
				return true;
			case "binkw64.dll":
			case "bink2w64.dll":
				return true;
			case "xinput1_3.dll":
			case "xinput1_4.dll":
				return true;
			case "winhttp.dll":
			case "wininet.dll":
				return true;
			default:
				return false;
			}
		}
	}

	private static bool IsPatchFile(string fileName)
	{
		if (!IsAddedPatchFile(fileName))
		{
			return IsReplacablePatchFile(fileName);
		}
		return true;
	}

	private static string? FindFileWithPriority(string[] files, string exeDir)
	{
		if (files == null || files.Length == 0)
		{
			return null;
		}
		string text = files.FirstOrDefault((string f) => f.Contains("plugins", StringComparison.OrdinalIgnoreCase));
		if (text != null)
		{
			return text;
		}
		string text2 = files.FirstOrDefault((string f) => f.Contains("Streamline", StringComparison.OrdinalIgnoreCase));
		if (text2 != null)
		{
			return text2;
		}
		string text3 = files.FirstOrDefault((string f) => Path.GetDirectoryName(f)?.Equals(exeDir, StringComparison.OrdinalIgnoreCase) ?? false);
		if (text3 != null)
		{
			return text3;
		}
		return files.FirstOrDefault();
	}

	private static string? FindNvngxDlssg(string gamePath, string exeDir)
	{
		try
		{
			// 影之刃零特殊处理：从exe目录往上找，直接搜索正确的文件路径
			string searchDir = exeDir;
			for (int i = 0; i < 5; i++) // 最多往上找5层
			{
				string testPath = Path.Combine(searchDir, "Engine", "Plugins", "Runtime", "Nvidia", "StreamlineCore", "Binaries", "ThirdParty", "Win64", "nvngx_dlssg.dll");
				if (File.Exists(testPath))
				{
					return testPath;
				}
				var parent = Directory.GetParent(searchDir);
				if (parent == null) break;
				searchDir = parent.FullName;
			}

			// 1. 先检查exe目录下
			string text = Path.Combine(exeDir, "nvngx_dlssg.dll");
			if (File.Exists(text))
			{
				return text;
			}
			// 2. 精准判断游戏最根目录
			string rootDir = exeDir;
			// 优先使用gamePath（用户手动指定的游戏根目录）
			if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
			{
				rootDir = gamePath;
			}
			else
			{
				// 从exe目录往上找，精准判断游戏最根目录
				string checkDir = exeDir;
				string bestRoot = exeDir;
				for (int i = 0; i < 8; i++) // 最多往上找8层
				{
					DirectoryInfo parent = Directory.GetParent(checkDir);
					if (parent == null || parent.FullName == checkDir) break;
					// 检查是否是盘符根目录，如果是就停止（不能回到盘符）
					if (parent.FullName.Length <= 3) break;
					checkDir = parent.FullName;
					// 判断是否是游戏最根目录：包含Engine目录（UE引擎游戏）
					if (Directory.Exists(Path.Combine(checkDir, "Engine")))
					{
						bestRoot = checkDir;
						break;
					}
					// 或者包含多个游戏特征目录（Content、Data、Binaries等）
					int featureCount = 0;
					string[] gameFeatures = { "Content", "content", "Data", "data", "Binaries", "binaries", "Retail", "retail", "Game", "game" };
					foreach (string feature in gameFeatures)
					{
						if (Directory.Exists(Path.Combine(checkDir, feature))) featureCount++;
					}
					if (featureCount >= 2)
					{
						bestRoot = checkDir;
						break;
					}
					// 或者有大于5MB的exe（游戏启动器通常在根目录）
					try
					{
						foreach (string f in Directory.GetFiles(checkDir, "*.exe", SearchOption.TopDirectoryOnly))
						{
							if (new FileInfo(f).Length > 5 * 1024 * 1024)
							{
								bestRoot = checkDir;
								break;
							}
						}
					}
					catch { }
				}
				rootDir = bestRoot;
			}
			// 3. 在游戏最根目录下搜索，深度15层
			string[] filesWithDepthLimit = GetFilesWithDepthLimit(rootDir, "nvngx_dlssg.dll", 15);
			if (filesWithDepthLimit.Length == 0)
			{
				return null;
			}
			// 4. 优先选择包含plugins的路径
			string text2 = filesWithDepthLimit.FirstOrDefault((string f) => f.Contains("plugins", StringComparison.OrdinalIgnoreCase));
			if (text2 != null)
			{
				return text2;
			}
			// 5. 然后选择包含Streamline的路径
			string text3 = filesWithDepthLimit.FirstOrDefault((string f) => f.Contains("Streamline", StringComparison.OrdinalIgnoreCase));
			if (text3 != null)
			{
				return text3;
			}
			// 6. 然后选择exe目录下的
			string text4 = filesWithDepthLimit.FirstOrDefault((string f) => Path.GetDirectoryName(f)?.Equals(exeDir, StringComparison.OrdinalIgnoreCase) ?? false);
			if (text4 != null)
			{
				return text4;
			}
			return filesWithDepthLimit.FirstOrDefault();
		}
		catch
		{
			return null;
		}
	}
private static string? FindNvngxDlss(string gamePath, string exeDir, bool skipExeDirFirst = false)
	{
		try
		{
			// 影之刃零特殊处理：从exe目录往上找，直接搜索正确的文件路径
			string searchDir = exeDir;
			for (int i = 0; i < 5; i++) // 最多往上找5层
			{
				string testPath = Path.Combine(searchDir, "Engine", "Plugins", "Runtime", "Nvidia", "DLSS", "Binaries", "ThirdParty", "Win64", "nvngx_dlss.dll");
				if (File.Exists(testPath))
				{
					return testPath;
				}
				var parent = Directory.GetParent(searchDir);
				if (parent == null) break;
				searchDir = parent.FullName;
			}

			// 1. 先检查exe目录下（当DLSS5已安装时，跳过这一步，避免找到DLSS5的dlss）
			if (!skipExeDirFirst)
			{
			string text = Path.Combine(exeDir, "nvngx_dlss.dll");
			if (File.Exists(text))
			{
				return text;
			}
			}
			// 2. 精准判断游戏最根目录
			string rootDir = exeDir;
			// 优先使用gamePath（用户手动指定的游戏根目录）
			if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
			{
				rootDir = gamePath;
			}
			else
			{
				// 从exe目录往上找，精准判断游戏最根目录
				string checkDir = exeDir;
				string bestRoot = exeDir;
				for (int i = 0; i < 8; i++) // 最多往上找8层
				{
					DirectoryInfo parent = Directory.GetParent(checkDir);
					if (parent == null || parent.FullName == checkDir) break;
					// 检查是否是盘符根目录，如果是就停止（不能回到盘符）
					if (parent.FullName.Length <= 3) break;
					checkDir = parent.FullName;
					// 判断是否是游戏最根目录：包含Engine目录（UE引擎游戏）
					if (Directory.Exists(Path.Combine(checkDir, "Engine")))
					{
						bestRoot = checkDir;
						break;
					}
					// 或者包含多个游戏特征目录（Content、Data、Binaries等）
					int featureCount = 0;
					string[] gameFeatures = { "Content", "content", "Data", "data", "Binaries", "binaries", "Retail", "retail", "Game", "game" };
					foreach (string feature in gameFeatures)
					{
						if (Directory.Exists(Path.Combine(checkDir, feature))) featureCount++;
					}
					if (featureCount >= 2)
					{
						bestRoot = checkDir;
						break;
					}
					// 或者有大于5MB的exe（游戏启动器通常在根目录）
					try
					{
						foreach (string f in Directory.GetFiles(checkDir, "*.exe", SearchOption.TopDirectoryOnly))
						{
							if (new FileInfo(f).Length > 5 * 1024 * 1024)
							{
								bestRoot = checkDir;
								break;
							}
						}
					}
					catch { }
				}
				rootDir = bestRoot;
			}
			// 3. 在游戏最根目录下搜索，深度15层
			string[] filesWithDepthLimit = GetFilesWithDepthLimit(rootDir, "nvngx_dlss.dll", 15);
			if (filesWithDepthLimit.Length == 0)
			{
				return null;
			}
			// 4. 优先选择包含plugins的路径
			string text2 = filesWithDepthLimit.FirstOrDefault((string f) => f.Contains("plugins", StringComparison.OrdinalIgnoreCase));
			if (text2 != null)
			{
				return text2;
			}
			// 5. 然后选择包含Streamline的路径
			string text3 = filesWithDepthLimit.FirstOrDefault((string f) => f.Contains("Streamline", StringComparison.OrdinalIgnoreCase));
			if (text3 != null)
			{
				return text3;
			}
			// 6. 然后选择exe目录下的
			string text4 = filesWithDepthLimit.FirstOrDefault((string f) => Path.GetDirectoryName(f)?.Equals(exeDir, StringComparison.OrdinalIgnoreCase) ?? false);
			if (text4 != null)
			{
				return text4;
			}
			return filesWithDepthLimit.FirstOrDefault();
		}
		catch
		{
			return null;
		}
	}

	public static bool InstallClassicFrameGen(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string gamePath = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			string backupDir = GetBackupDir(game, "FrameGen_Classic", true);
			string text = FindNvngxDlssg(gamePath, exeDir);
			string sourceFileName = Path.Combine(GetPatchDir("老驱动多帧生成\\核心"), "nvngx_dlssg.dll");
			if (text != null)
			{
				BackupFile(text, backupDir);
				File.Copy(sourceFileName, text, overwrite: true);
			}
			else
			{
				string text2 = Path.Combine(exeDir, "nvngx_dlssg.dll");
				bool num = File.Exists(text2);
				File.Copy(sourceFileName, text2, overwrite: true);
				if (!num)
				{
					RecordAddedFile(backupDir, text2);
				}
			}
			string patchDir = GetPatchDir("老驱动多帧生成\\核心");
			string[] array = new string[3] { "RTX40MFG.asi", "RTX40MFG_config.json", "version.dll" };
			foreach (string path in array)
			{
				string sourceFileName2 = Path.Combine(patchDir, path);
				string text3 = Path.Combine(exeDir, path);
				bool num2 = File.Exists(text3);
				if (num2)
				{
					BackupFile(text3, backupDir);
				}
				File.Copy(sourceFileName2, text3, overwrite: true);
				if (!num2)
				{
					RecordAddedFile(backupDir, text3);
				}
			}
			game.FrameGenEnabled = true;
			game.FrameGenMode = "经典模式";
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool InstallAdvancedFrameGen(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string gamePath = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			string backupDir = GetBackupDir(game, "FrameGen_Advanced", true);

			// 1. 替换 nvngx_dlssg.dll
			string dlssgPath = FindNvngxDlssg(gamePath, exeDir);
			string dlssgSource = Path.Combine(GetPatchDir("40系原生多帧生成"), "nvngx_dlssg.dll");
			if (dlssgPath != null)
			{
				BackupFile(dlssgPath, backupDir, gamePath);
				File.Copy(dlssgSource, dlssgPath, overwrite: true);
			}
			else
			{
				string dlssgExePath = Path.Combine(exeDir, "nvngx_dlssg.dll");
				bool dlssgExists = File.Exists(dlssgExePath);
				File.Copy(dlssgSource, dlssgExePath, overwrite: true);
				if (!dlssgExists)
				{
					RecordAddedFile(backupDir, dlssgExePath);
				}
			}

			// 2. 替换 nvngx_dlss.dll
			string dlssPath = FindNvngxDlss(gamePath, exeDir, skipExeDirFirst: game.Dlss5Enabled);
			string dlssSource = ResolvePatchFile(GetPatchDir("40系原生多帧生成\\排错"), "nvngx_dlss.dll");
			bool skipDlss = false;
			if (game.Dlss5Enabled)
			{
				if (dlssPath != null && Path.GetDirectoryName(dlssPath)?.Equals(exeDir, StringComparison.OrdinalIgnoreCase) == true)
				{
					skipDlss = true;
				}
				else if (dlssPath == null)
				{
					skipDlss = true;
				}
			}
			if (!skipDlss && dlssPath != null)
			{
				BackupFile(dlssPath, backupDir, gamePath);
				File.Copy(dlssSource, dlssPath, overwrite: true);
			}
			else if (!skipDlss)
			{
				string dlssExePath = Path.Combine(exeDir, "nvngx_dlss.dll");
				bool dlssExists = File.Exists(dlssExePath);
				File.Copy(dlssSource, dlssExePath, overwrite: true);
				if (!dlssExists)
				{
					RecordAddedFile(backupDir, dlssExePath);
				}
			}

			// 3. 新增 version.dll（MFGUnlocker v1.7.0）
			string versionPath = Path.Combine(exeDir, "version.dll");
			bool versionExists = File.Exists(versionPath);
			if (versionExists)
			{
				BackupFile(versionPath, backupDir);
			}
			File.Copy(Path.Combine(GetPatchDir("40系原生多帧生成"), "version.dll"), versionPath, overwrite: true);
			if (!versionExists)
			{
				RecordAddedFile(backupDir, versionPath);
			}

			// 4. 新增 MFGUnlocker 配置文件（快捷键已设置为HOME键）
			string uiIniPath = Path.Combine(exeDir, "RTX40MFG-UI.ini");
			bool uiIniExists = File.Exists(uiIniPath);
			if (!uiIniExists)
			{
				File.Copy(Path.Combine(GetPatchDir("40系原生多帧生成"), "RTX40MFG-UI.ini"), uiIniPath, overwrite: true);
				RecordAddedFile(backupDir, uiIniPath);
			}

			// 5. RE引擎游戏自动适配：检测到re_chunk_000.pak就额外安装RE框架dinput8.dll绕过完整性检查
			if (GameScanner.CheckREEngine(gamePath))
			{
				string dinput8Path = Path.Combine(exeDir, "dinput8.dll");
				bool dinput8Exists = File.Exists(dinput8Path);
				if (dinput8Exists)
				{
					BackupFile(dinput8Path, backupDir);
				}
				File.Copy(Path.Combine(GetPatchDir("RE框架"), "dinput8.dll"), dinput8Path, overwrite: true);
				if (!dinput8Exists)
				{
					RecordAddedFile(backupDir, dinput8Path);
				}
			}

			game.FrameGenEnabled = true;
			game.FrameGenMode = "高级模式";

			// 燕云十六声特殊适配：自动同步补丁文件到Win64rh目录
			try
			{
				var yanYunOtherDir = Path.Combine(Path.GetDirectoryName(exeDir), "Win64rh");
				if (game.Name.Contains("燕云", StringComparison.OrdinalIgnoreCase) && Directory.Exists(yanYunOtherDir))
				{
					foreach (var addedFile in Directory.GetFiles(exeDir))
					{
						var fileName = Path.GetFileName(addedFile);
						// 同步我们安装的所有补丁文件
						if (fileName is "nvngx_dlssg.dll" or "nvngx_dlss.dll" or "version.dll" or "RTX40MFG-UI.ini" or "dinput8.dll")
						{
							File.Copy(addedFile, Path.Combine(yanYunOtherDir, fileName), overwrite: true);
						}
					}
				}
			}
			catch { }

			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool Install2077FrameGen(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "FrameGen_2077", true);
			string patchDir = GetPatchDir("2077专用多帧生成补丁");
			CopyPatchFiles(patchDir, exeDir, backupDir, backupExisting: true, new string[2] { ".txt", ".png" });
			string text = Path.Combine(patchDir, "plugins");
			string dest = Path.Combine(exeDir, "plugins");
			if (Directory.Exists(text))
			{
				CopyDirectory(text, dest, overwrite: true, includeSharedFiles: false, backupDir);
			}
			game.FrameGenEnabled = true;
			game.FrameGenMode = "2077专用";
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool InstallCommonDLSS5(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "DLSS5_Common", true);
			CopyFontsFiles(exeDir, backupDir);
			CopyReShadeFiles(exeDir, backupDir);
			CopyPatchFiles(GetPatchDir("N卡DLSS5补丁"), exeDir, backupDir, treatAsAddedFiles: new string[] { "nvngx_dlss.dll" });
			game.Dlss5Enabled = true;
			return true;
		}
		catch
		{
			return false;
		}
	}

	// ==================== GITC-DLSS5 新版安装函数 ====================

	/// <summary>
	/// 安装GITC-DLSS5基础文件
	/// </summary>
	/// <param name="api">渲染API：dx12 / dx9 / vulkan</param>
	/// <param name="installSubDir">安装子目录，比如起源引擎填"bin"，默认空</param>
	/// <param name="is32bit">游戏是否为32位，默认false（64位）</param>
	public static bool InstallGITCDLSS5_Base(GameInfo game, string api, string installSubDir = "", bool is32bit = false)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			// 如果有子目录，就拼上
			string targetDir = string.IsNullOrEmpty(installSubDir) 
				? exeDir 
				: Path.Combine(exeDir, installSubDir);
			string backupDir = GetBackupDir(game, "GITC_DLSS5", true);
			string gamePath = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			
			// 复制dx12基础文件到目标目录（目标已存在的同名文件会先备份，避免还原时误删游戏原生文件）
			CopyDirectory(GetPatchDir("GITC-DLSS5补丁\\dx12"), targetDir, overwrite: true, includeSharedFiles: false, backupDir, gamePath);
			
			// 根据不同API，把主dll重命名为正确的名字
			string mainDllName;
			switch (api.ToLower())
			{
				case "dx9":
					mainDllName = "d3d9.dll";
					break;
				case "vulkan":
					mainDllName = "vulkan.dll";
					break;
				case "dx12":
				default:
					mainDllName = "dxgi.dll";
					break;
			}

			// 删除默认的dxgi.dll（不管什么API都先删掉）
			string defaultDxgiPath = Path.Combine(targetDir, "dxgi.dll");
			if (File.Exists(defaultDxgiPath))
			{
				File.Delete(defaultDxgiPath);
			}

			// 删除临时的x86/x64 dll文件（如果复制过去的话）
			foreach (string tempDll in new[] { "dxgi_x86.dll", "dxgi_x64.dll" })
			{
				string tempPath = Path.Combine(targetDir, tempDll);
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
			
			// 根据位数选择对应的reshade主dll，复制并重命名
			string patchDir = GetPatchDir("GITC-DLSS5补丁\\dx12");
			string dllSource = is32bit ? Path.Combine(patchDir, "dxgi_x86.dll") : Path.Combine(patchDir, "dxgi_x64.dll");
			string dllDest = Path.Combine(targetDir, mainDllName);
			File.Copy(dllSource, dllDest, overwrite: true);
			// 记录新增的主dll，还原时按清单删除；若同名文件原本存在，前面CopyDirectory已备份
			RecordAddedFile(backupDir, dllDest);

			// RE引擎游戏自动适配：检测到re_chunk_000.pak就额外复制RE框架dinput8.dll绕过完整性检查
			if (GameScanner.CheckREEngine(gamePath))
			{
				string dinput8Path = Path.Combine(exeDir, "dinput8.dll");
				// 玩家可能自己装了ReShade的dinput8代理，覆盖前必须备份
				if (File.Exists(dinput8Path))
				{
					BackupFile(dinput8Path, backupDir, gamePath);
				}
				else
				{
					RecordAddedFile(backupDir, dinput8Path);
				}
				File.Copy(Path.Combine(GetPatchDir("RE框架"), "dinput8.dll"), dinput8Path, overwrite: true);
			}

			// 燕云十六声特殊适配：自动同步整个GITC目录文件到Win64rh目录
			try
			{
				var yanYunOtherDir = Path.Combine(Path.GetDirectoryName(targetDir), "Win64rh");
				if (game.Name.Contains("燕云", StringComparison.OrdinalIgnoreCase) && Directory.Exists(yanYunOtherDir))
				{
					// 传backupDir并记录，还原时按清单清理Win64rh，不再整目录硬删
					CopyDirectory(targetDir, yanYunOtherDir, overwrite: true, includeSharedFiles: false, backupDir, gamePath);
				}
			}
			catch { }

			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"GITC安装失败：{ex.Message}");
			throw new Exception($"GITC安装失败：{ex.Message}", ex);
		}
	}

	/// <summary>
	/// 根据显卡型号复制对应的nvngx_dlssnr.dll到GITC-Uplift文件夹
	/// </summary>
	/// <param name="gpuType">显卡型号：2030 / 40 / 50</param>
	/// <param name="installSubDir">安装子目录，比如起源引擎填"bin"，默认空</param>
	public static bool InstallGITCDLSS5_DLSSNR(GameInfo game, string gpuType, string installSubDir = "")
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			// 如果有子目录，就拼上
			string targetDir = string.IsNullOrEmpty(installSubDir) 
				? exeDir 
				: Path.Combine(exeDir, installSubDir);
			string destDir = Path.Combine(targetDir, "GITC-Uplift");
			string gamePath = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			// 复用本次安装的备份目录，把dlssnr dll也登记进去，否则还原时目录非空不会被删除
			string? backupDir = FindLatestGITCBackupDir(game);
			
			// 确保目标文件夹存在
			if (!Directory.Exists(destDir))
			{
				Directory.CreateDirectory(destDir);
				if (backupDir != null)
				{
					RecordAddedDir(backupDir, destDir);
				}
			}
			
			// 根据显卡型号选择对应的dll
			string sourceFile;
			switch (gpuType)
			{
				case "2030":
					sourceFile = GetPatchDir("GITC-DLSS5补丁\\20-30系dlssnr\\nvngx_dlssnr.dll");
					break;
				case "40":
					sourceFile = GetPatchDir("GITC-DLSS5补丁\\40系dlssnr\\nvngx_dlssnr.dll");
					break;
				case "50":
					sourceFile = GetPatchDir("GITC-DLSS5补丁\\50系dlssnr\\nvngx_dlssnr.dll");
					break;
				default:
					return false;
			}
			
			// 复制dll到GITC-Uplift文件夹
			string destFile = Path.Combine(destDir, "nvngx_dlssnr.dll");
			if (File.Exists(destFile))
			{
				if (backupDir != null) BackupFile(destFile, backupDir, gamePath);
			}
			else if (backupDir != null)
			{
				RecordAddedFile(backupDir, destFile);
			}
			File.Copy(sourceFile, destFile, overwrite: true);
			
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 查找该游戏最新的 GITC_DLSS5 备份子目录，没有则返回 null
	/// </summary>
	private static string? FindLatestGITCBackupDir(GameInfo game)
	{
		try
		{
			if (!Directory.Exists(BackupBaseDir)) return null;
			string baseName = game.Name + " DLSS5";
			string? best = null;
			int bestVer = -1;
			foreach (string d in Directory.GetDirectories(BackupBaseDir))
			{
				string name = Path.GetFileName(d);
				int ver = 0;
				bool match = name.Equals(baseName, StringComparison.OrdinalIgnoreCase);
				if (!match && name.StartsWith(baseName + "V", StringComparison.OrdinalIgnoreCase))
				{
					if (int.TryParse(name.Substring(baseName.Length + 1), out int n))
					{
						ver = n;
						match = true;
					}
				}
				if (!match) continue;
				string featureDir = Path.Combine(d, "GITC_DLSS5");
				if (Directory.Exists(featureDir) && ver >= bestVer)
				{
					bestVer = ver;
					best = featureDir;
				}
			}
			return best;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// 是否存在 GITC DLSS5 的安装备份（用于还原入口判断实际装了什么）
	/// </summary>
	public static bool HasGitcBackup(GameInfo game)
	{
		return FindLatestGITCBackupDir(game) != null;
	}

	/// <summary>
	/// 按备份清单还原：先恢复被替换的原文件，再删除记录在案的新增文件/目录。
	/// 只处理备份里明确记录过的路径，绝不碰未记录的原生文件。
	/// </summary>
	private static bool RestoreFromBackupRecords(string featureBackupDir, string gameRootDir)
	{
		bool changed = false;
		HashSet<string> restoredTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// 1) 恢复被替换的原文件（backup_manifest.txt: 相对路径|原绝对路径）
		string manifestPath = Path.Combine(featureBackupDir, "backup_manifest.txt");
		if (File.Exists(manifestPath))
		{
			foreach (string line in File.ReadAllLines(manifestPath))
			{
				try
				{
					string[] parts = line.Split('|', 2);
					if (parts.Length != 2) continue;
					string rel = parts[0].Trim();
					string orig = parts[1].Trim();
					if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(orig)) continue;
					if (orig.Contains(BackupBaseDir, StringComparison.OrdinalIgnoreCase)) continue;
					string bak = Path.Combine(featureBackupDir, rel);
					if (!File.Exists(bak)) continue;
					string? dir = Path.GetDirectoryName(orig);
					if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
					File.Copy(bak, orig, overwrite: true);
					restoredTargets.Add(orig);
					changed = true;
				}
				catch
				{
				}
			}
		}

		// 2) 删除新增文件（已恢复原文件的跳过）
		string addedFilesPath = Path.Combine(featureBackupDir, "added_files.txt");
		if (File.Exists(addedFilesPath))
		{
			foreach (string line in File.ReadAllLines(addedFilesPath))
			{
				try
				{
					string f = line.Trim();
					if (string.IsNullOrWhiteSpace(f)) continue;
					if (f.Contains(BackupBaseDir, StringComparison.OrdinalIgnoreCase)) continue;
					if (restoredTargets.Contains(f)) continue;
					if (File.Exists(f))
					{
						File.Delete(f);
						changed = true;
					}
				}
				catch
				{
				}
			}
		}

		// 3) 删除新增目录（只删彻底变空的目录，含原生文件的目录保留）
		string addedDirsPath = Path.Combine(featureBackupDir, "added_dirs.txt");
		if (File.Exists(addedDirsPath))
		{
			List<string> dirs = File.ReadAllLines(addedDirsPath).Select(x => x.Trim())
				.Where(x => !string.IsNullOrWhiteSpace(x) && !x.Contains(BackupBaseDir, StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(x => x.Length)
				.ToList();
			foreach (string d in dirs)
			{
				try
				{
					if (Directory.Exists(d) && Directory.GetFileSystemEntries(d).Length == 0)
					{
						Directory.Delete(d, recursive: false);
						changed = true;
					}
				}
				catch
				{
				}
			}
		}

		return changed;
	}

	/// <summary>
	/// 清理所有安装位置下的 GITC-Uplift 目录（exe目录、bin、燕云Win64rh）
	/// </summary>
	private static void CleanupGitcUpliftDirs(string exeDir)
	{
		string[] dirs = new string[]
		{
			exeDir,
			Path.Combine(exeDir, "bin"),
			Path.Combine(Path.GetDirectoryName(exeDir) ?? exeDir, "Win64rh")
		};
		foreach (string dir in dirs)
		{
			string uplift = Path.Combine(dir, "GITC-Uplift");
			if (Directory.Exists(uplift))
			{
				CleanupGitcUpliftDir(uplift);
			}
		}
	}

	/// <summary>
	/// 删除 GITC-Uplift 内由本软件安装的文件，并自底向上删除空目录。
	/// 只处理这个专属目录里的两个已知文件，目录里若有其他文件则保留目录。
	/// </summary>
	private static void CleanupGitcUpliftDir(string upliftDir)
	{
		try { File.Delete(Path.Combine(upliftDir, "nvngx_dlssnr.dll")); } catch { }
		try { File.Delete(Path.Combine(upliftDir, "Put nvngx_dlssnr.dll here.txt")); } catch { }
		try
		{
			foreach (string sub in Directory.GetDirectories(upliftDir))
			{
				CleanupGitcUpliftDir(sub);
			}
		}
		catch
		{
		}
		try
		{
			if (Directory.GetFileSystemEntries(upliftDir).Length == 0)
			{
				Directory.Delete(upliftDir, recursive: false);
			}
		}
		catch
		{
		}
	}

	/// <summary>
	/// 还原GITC DLSS5：优先按安装时记录的备份清单还原；没有备份记录时只做保守清理，
	/// 不再无条件删除 dxgi/d3d9/vulkan/dinput8/ReShade.ini 和整个 Fonts 目录。
	/// </summary>
	public static bool RestoreGITCDLSS5(GameInfo game)
	{
		try
		{
			string exeDir = GetExeDir(game);
			string gameRoot = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			string? gitcBackup = FindLatestGITCBackupDir(game);

			if (gitcBackup != null)
			{
				// 有安装备份：只还原备份里记录过的文件/目录（含bin、Win64rh等所有安装过的位置）
				RestoreFromBackupRecords(gitcBackup, gameRoot);
				// 旧版安装可能没有把 nvngx_dlssnr.dll 记录进清单，这里兜底清掉GITC专属目录
				CleanupGitcUpliftDirs(exeDir);
				return true;
			}

			// 无备份记录（极少数旧安装）：只删GITC专属文件，原生文件和Fonts目录一律保留
			string[] gitcOnlyFiles = new string[]
			{
				"gitc-uplift.addon32",
				"gitc-uplift.addon64",
				"gitc-uplift-helper64.exe",
				"MiSans-Bold.ttf",
				"ReShadePreset.ini"
			};
			string[] searchDirs = new string[]
			{
				exeDir,
				Path.Combine(exeDir, "bin"),
				Path.Combine(Path.GetDirectoryName(exeDir) ?? exeDir, "Win64rh")
			};
			foreach (string dir in searchDirs)
			{
				if (!Directory.Exists(dir)) continue;
				foreach (string file in gitcOnlyFiles)
				{
					try
					{
						string filePath = Path.Combine(dir, file);
						if (File.Exists(filePath)) File.Delete(filePath);
					}
					catch
					{
					}
				}
				try
				{
					string upliftDir = Path.Combine(dir, "GITC-Uplift");
					if (Directory.Exists(upliftDir)) CleanupGitcUpliftDir(upliftDir);
				}
				catch
				{
				}
				// Fonts目录只删GITC自带的MiSans字体，不删整个目录
				try
				{
					string fontsDir = Path.Combine(dir, "Fonts");
					if (Directory.Exists(fontsDir))
					{
						foreach (string font in Directory.GetFiles(fontsDir, "MiSans*.ttf", SearchOption.AllDirectories))
						{
							try { File.Delete(font); } catch { }
						}
					}
				}
				catch
				{
				}
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool InstallAmdDLSS5(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "DLSS5_AMD", true);
			CopyFontsFiles(exeDir, backupDir);
			CopyReShadeFiles(exeDir, backupDir);
			CopyPatchFiles(GetPatchDir("A卡DLSS5补丁（仅支持7000系和9000系）"), exeDir, backupDir, treatAsAddedFiles: new string[] { "nvngx_dlss.dll" });
			string text = Path.Combine(exeDir, "dlssnr_on_amd_setup.exe");
			if (File.Exists(text))
			{
				Process.Start(text);
			}
			game.Dlss5Enabled = true;
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool InstallYanYunDLSS5(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "DLSS5_YanYun", true);
			string patchDir = GetPatchDir("燕云十六声DLSS5补丁");
			CopyPatchFiles(patchDir, exeDir, backupDir, treatAsAddedFiles: new string[] { "nvngx_dlss.dll" });
			CopyFontsFiles(exeDir, backupDir);
			CopyReShadeFiles(exeDir, backupDir);
			string text2 = Path.Combine(patchDir, "Streamline");
			string dest = Path.Combine(game.GamePath, "Streamline");
			if (Directory.Exists(text2))
			{
				string text3 = GetDirectoriesWithDepthLimit(game.GamePath, "Streamline").FirstOrDefault();
				if (text3 != null)
				{
					CopyDirectory(text2, text3, overwrite: true, includeSharedFiles: true, backupDir);
				}
				else
				{
					CopyDirectory(text2, dest, overwrite: true, includeSharedFiles: true, backupDir);
				}
			}
			game.Dlss5Enabled = true;
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool ReplaceDgvoodooDll(GameInfo game)
	{
		try
		{
			string exeDir = GetExeDir(game);
			string gamePath = game.GamePath;
			string[] array = new string[3] { "D3D9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe" };
			string text = null;
			string text2 = Path.Combine(exeDir, "bin");
			if (Directory.Exists(text2))
			{
				text = text2;
			}
			if (text == null)
			{
				string text3 = Path.Combine(gamePath, "bin");
				if (Directory.Exists(text3))
				{
					text = text3;
				}
			}
			if (text == null)
			{
				string text4 = Path.Combine(gamePath, "Bin32");
				if (Directory.Exists(text4))
				{
					text = text4;
				}
			}
			if (text == null)
			{
				try
				{
					List<string> list = (from d in GetDirectoriesWithDepthLimit(gamePath, "*")
						where d != exeDir
						where Directory.GetFiles(d, "*.dll").Any()
						orderby d.Length
						select d).ToList();
					if (list.Count > 0)
					{
						text = list[0];
					}
				}
				catch
				{
				}
			}
			if (text == null)
			{
				return false;
			}
			string[] array2 = array;
			foreach (string path in array2)
			{
				string text5 = Path.Combine(exeDir, path);
				string text6 = Path.Combine(text, path);
				if (File.Exists(text5))
				{
					if (File.Exists(text6))
					{
						File.Delete(text6);
					}
					File.Move(text5, text6);
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static (List<string> filesToDelete, List<string> filesToRestore, List<string> dirsToDelete) PreviewRestoreFrameGen(GameInfo game)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		try
		{
			string exeDir = GetExeDir(game);
			string text = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			string path = Path.Combine(BackupBaseDir, game.Name);
			if (!Directory.Exists(path))
			{
				return (filesToDelete: list, filesToRestore: list2, dirsToDelete: list3);
			}
			List<string> list4 = (from d in Directory.GetDirectories(path)
				where Path.GetFileName(d).Contains("FrameGen")
				select d).ToList();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string text2 = text;
			bool flag = false;
			string[] files;
			foreach (string item2 in list4)
			{
				string text3 = Path.Combine(item2, "pre_install_snapshot.txt");
				if (File.Exists(text3) && !flag)
				{
					var (text4, hashSet3) = LoadDirectorySnapshot(text3);
					if (!string.IsNullOrEmpty(text4) && Directory.Exists(text4))
					{
						text2 = text4;
						hashSet2 = hashSet3;
						flag = true;
					}
				}
				files = Directory.GetFiles(item2);
				foreach (string text5 in files)
				{
					string fileName = Path.GetFileName(text5);
					switch (fileName)
					{
					case "backup_manifest.txt":
					case "game_path.txt":
					case "pre_install_snapshot.txt":
						continue;
					}
					hashSet.Add(fileName);
					if (!dictionary.ContainsKey(fileName))
					{
						dictionary[fileName] = text5;
					}
					string originalPath = GetOriginalPath(item2, fileName);
					if (!string.IsNullOrEmpty(originalPath) && !dictionary2.ContainsKey(fileName))
					{
						dictionary2[fileName] = originalPath;
					}
					list2.Add(originalPath ?? Path.Combine(exeDir, fileName));
				}
			}
			files = GetFilesWithDepthLimit(text, "*", 8);
			foreach (string text6 in files)
			{
				if (text6.Contains(BackupBaseDir))
				{
					continue;
				}
				string fileName2 = Path.GetFileName(text6);
				if (hashSet.Contains(fileName2))
				{
					continue;
				}
				bool flag2 = true;
				if (flag)
				{
					try
					{
						if (text6.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
						{
							string item = text6.Substring(text2.Length).TrimStart(new char[2] { '\\', '/' });
							if (hashSet2.Contains(item))
							{
								flag2 = false;
							}
						}
					}
					catch
					{
					}
				}
				if (flag2 && IsAddedPatchFile(fileName2))
				{
					list.Add(text6);
				}
			}
			files = new string[1] { "reshade-shaders" };
			foreach (string searchPattern in files)
			{
				try
				{
					string[] directoriesWithDepthLimit = GetDirectoriesWithDepthLimit(text, searchPattern, 8);
					foreach (string text7 in directoriesWithDepthLimit)
					{
						if (text7.Contains(BackupBaseDir) || !Directory.Exists(text7))
						{
							continue;
						}
						bool flag3 = true;
						if (flag)
						{
							try
							{
								if (text7.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
								{
									string relativePath = text7.Substring(text2.Length).TrimStart(new char[2] { '\\', '/' });
									if (hashSet2.Any((string f) => f.StartsWith(relativePath + "\\", StringComparison.OrdinalIgnoreCase)))
									{
										flag3 = false;
									}
								}
							}
							catch
							{
							}
						}
						if (flag3)
						{
							list3.Add(text7);
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return (filesToDelete: list.Distinct().ToList(), filesToRestore: list2.Distinct().ToList(), dirsToDelete: list3.Distinct().ToList());
	}

	public static (List<string> filesToDelete, List<string> filesToRestore, List<string> dirsToDelete) PreviewRestoreDLSS5(GameInfo game)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		try
		{
			string exeDir = GetExeDir(game);
			string text = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			string dlss5BaseName = game.Name + " DLSS5";
			List<string> list4 = new List<string>();
			if (Directory.Exists(BackupBaseDir))
			{
				foreach (string bd in Directory.GetDirectories(BackupBaseDir))
				{
					string bdName = Path.GetFileName(bd);
					if (bdName.Equals(dlss5BaseName, StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(dlss5BaseName + "V", StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(dlss5BaseName + "v", StringComparison.OrdinalIgnoreCase))
					{
						list4.Add(bd);
					}
				}
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string text2 = text;
			bool flag = false;
			string[] files;
			foreach (string item2 in list4)
			{
				string text3 = Path.Combine(item2, "pre_install_snapshot.txt");
				if (File.Exists(text3) && !flag)
				{
					var (text4, hashSet3) = LoadDirectorySnapshot(text3);
					if (!string.IsNullOrEmpty(text4) && Directory.Exists(text4))
					{
						text2 = text4;
						hashSet2 = hashSet3;
						flag = true;
					}
				}
				files = Directory.GetFiles(item2);
				foreach (string text5 in files)
				{
					string fileName = Path.GetFileName(text5);
					switch (fileName)
					{
					case "backup_manifest.txt":
					case "game_path.txt":
					case "pre_install_snapshot.txt":
						continue;
					}
					hashSet.Add(fileName);
					if (!dictionary.ContainsKey(fileName))
					{
						dictionary[fileName] = text5;
					}
					string originalPath = GetOriginalPath(item2, fileName);
					if (!string.IsNullOrEmpty(originalPath) && !dictionary2.ContainsKey(fileName))
					{
						dictionary2[fileName] = originalPath;
					}
					list2.Add(originalPath ?? Path.Combine(exeDir, fileName));
				}
			}
			files = GetFilesWithDepthLimit(text, "*", 8);
			foreach (string text6 in files)
			{
				if (text6.Contains(BackupBaseDir))
				{
					continue;
				}
				string fileName2 = Path.GetFileName(text6);
				if (hashSet.Contains(fileName2))
				{
					continue;
				}
				bool flag2 = true;
				if (flag)
				{
					try
					{
						if (text6.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
						{
							string item = text6.Substring(text2.Length).TrimStart(new char[2] { '\\', '/' });
							if (hashSet2.Contains(item))
							{
								flag2 = false;
							}
						}
					}
					catch
					{
					}
				}
				if (flag2 && IsAddedPatchFile(fileName2))
				{
					list.Add(text6);
				}
			}
			files = new string[3] { "reshade-shaders", "host64", "runtime" };
			foreach (string searchPattern in files)
			{
				try
				{
					string[] directoriesWithDepthLimit = GetDirectoriesWithDepthLimit(text, searchPattern, 8);
					foreach (string text7 in directoriesWithDepthLimit)
					{
						if (text7.Contains(BackupBaseDir) || !Directory.Exists(text7))
						{
							continue;
						}
						bool flag3 = true;
						if (flag)
						{
							try
							{
								if (text7.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
								{
									string relativePath = text7.Substring(text2.Length).TrimStart(new char[2] { '\\', '/' });
									if (hashSet2.Any((string f) => f.StartsWith(relativePath + "\\", StringComparison.OrdinalIgnoreCase)))
									{
										flag3 = false;
									}
								}
							}
							catch
							{
							}
						}
						// 如果DLSS5还在使用，不删除共用的reshade-shaders目录
						string dirNameFG = Path.GetFileName(text7.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
						bool isSharedDirFG = dirNameFG.Equals("reshade-shaders", StringComparison.OrdinalIgnoreCase);
						bool skipSharedDirFG = isSharedDirFG && game.Dlss5Enabled;
						if (flag3 && !skipSharedDirFG)
						{
							list3.Add(text7);
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return (filesToDelete: list.Distinct().ToList(), filesToRestore: list2.Distinct().ToList(), dirsToDelete: list3.Distinct().ToList());
	}

	public static bool RestoreFrameGen(GameInfo game)
	{
		try
		{
			string exeDir = GetExeDir(game);
			string text = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			string path = Path.Combine(BackupBaseDir, game.Name);
			List<string> allGameBackupDirs = new List<string>();
			if (Directory.Exists(BackupBaseDir))
			{
				// 收集所有匹配的备份目录
				List<string> matchedDirs = new List<string>();
				foreach (string bd in Directory.GetDirectories(BackupBaseDir))
				{
					string bdName = Path.GetFileName(bd);
					if ((bdName.Equals(game.Name, StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(game.Name + "V", StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(game.Name + "v", StringComparison.OrdinalIgnoreCase)) && !bdName.StartsWith(game.Name + " DLSS5", StringComparison.OrdinalIgnoreCase))
					{
						matchedDirs.Add(bd);
					}
				}
				// 只保留最新的备份目录（版本号最大的）
				if (matchedDirs.Count > 0)
				{
					string latestDir = matchedDirs[0];
					int latestVersion = 0;
					foreach (string d in matchedDirs)
					{
						string dName = Path.GetFileName(d);
						int ver = 0;
						if (dName.Equals(game.Name, StringComparison.OrdinalIgnoreCase))
						{
							ver = 1;
						}
						else
						{
							string verStr = dName.Substring(game.Name.Length).TrimStart("Vv".ToCharArray());
							if (int.TryParse(verStr, out int v))
							{
								ver = v;
							}
						}
						if (ver > latestVersion)
						{
							latestVersion = ver;
							latestDir = d;
						}
					}
					allGameBackupDirs.Add(latestDir);

				}
			}
			new List<string>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string text2 = text;
			bool flag = false;
			HashSet<string> hashSet3 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet4 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] files;
			foreach (string gameBackupDir in allGameBackupDirs)
			{
				if (Directory.Exists(gameBackupDir))
				{
				foreach (string item2 in (from d in Directory.GetDirectories(gameBackupDir)
					where Path.GetFileName(d).Contains("FrameGen")
					select d).ToList())
				{
					string text3 = Path.Combine(item2, "pre_install_snapshot.txt");
					if (File.Exists(text3) && !flag)
					{
						var (text4, hashSet5) = LoadDirectorySnapshot(text3);
						if (!string.IsNullOrEmpty(text4) && Directory.Exists(text4))
						{
							text2 = text4;
							hashSet2 = hashSet5;
							flag = true;
						}
					}
					foreach (string item3 in LoadAddedFiles(item2))
					{
						hashSet3.Add(item3);
					}
					foreach (string item4 in LoadAddedDirs(item2))
					{
						hashSet4.Add(item4);
					}
					files = Directory.GetFiles(item2);
					foreach (string text5 in files)
					{
						string fileName = Path.GetFileName(text5);
						switch (fileName)
						{
						case "backup_manifest.txt":
						case "game_path.txt":
						case "pre_install_snapshot.txt":
						case "added_files.txt":
						case "added_dirs.txt":
							continue;
						}
						hashSet.Add(fileName);
						if (!dictionary.ContainsKey(fileName))
						{
							dictionary[fileName] = text5;
						}
						string originalPath = GetOriginalPath(item2, fileName);
						if (!string.IsNullOrEmpty(originalPath) && !dictionary2.ContainsKey(fileName))
						{
							dictionary2[fileName] = originalPath;
						}
					}
				}
				}
			}
			int num2 = 0;
				foreach (string item5 in hashSet3)
					try
					{
						// 精细化还原：当DLSS5还在使用时，只删除多帧生成特有的文件，不删除共用文件
						string item5FileName = Path.GetFileName(item5);
						bool shouldDeleteFG = true;
						if (game.Dlss5Enabled)
						{
							// DLSS5还在使用时，只删除多帧生成特有的文件
							shouldDeleteFG = IsFrameGenSpecificFile(item5FileName);
							// version.dll和version.ini无论有没有修改配置名都得删除
							if (item5FileName.Equals("version.dll", StringComparison.OrdinalIgnoreCase) || item5FileName.Equals("version.ini", StringComparison.OrdinalIgnoreCase))
							{
								shouldDeleteFG = true;
							}
							// 如果DLSS5还在使用，不删除reshade-shaders和Fonts目录里的文件（共用目录）
							string item5DirFG = Path.GetDirectoryName(item5) ?? "";
							bool isInReshadeShadersDirFG = item5DirFG.Contains("reshade-shaders") || item5DirFG.Contains("ReShade-shaders");
							bool isInFontsDirFG = item5DirFG.Contains("Fonts") || item5DirFG.Contains("fonts");
							if (isInReshadeShadersDirFG || isInFontsDirFG)
							{
								shouldDeleteFG = false;
							}
						}
						if (File.Exists(item5) && !item5.Contains(BackupBaseDir) && shouldDeleteFG)
						{
							File.Delete(item5);
							num2++;
						}
					}
					catch
					{
					}
				// 清理MFGUnlocker v1.7.0动态生成的配置和日志文件
try
{
    string[] mfgPatterns = new string[] 
    { 
        "RTX40MFG-UI.ini", 
        "RTX40MFG-UI-layout.ini",
        "RTXMFG-Universal.json",
        "RTXMFG-Universal.status.json",
        "RTXMFG-*.log",
        "RTXMFG-*-intervals.csv"
    };
    foreach (string pattern in mfgPatterns)
    {
        try
        {
            foreach (string f in Directory.GetFiles(exeDir, pattern))
            {
                // 精细化还原：当DLSS5还在使用时，也要删除这些MFG特有文件
                bool shouldDeleteMfg = true;
                if (game.Dlss5Enabled)
                {
                    shouldDeleteMfg = true; // 这些都是MFG特有文件，DLSS5还原时也应该删除
                }
                if (File.Exists(f) && shouldDeleteMfg)
                {
                    File.Delete(f);
                    num2++;
                }
            }
        }
        catch { }
    }
}
catch { }
// 还原被替换的文件（根据backup_manifest.txt记录精准还原，支持子目录备份）
				foreach (string gameBackupDir3 in allGameBackupDirs)
				{
					if (Directory.Exists(gameBackupDir3))
					{
						foreach (string frameGenDir in (from d in Directory.GetDirectories(gameBackupDir3)
							where Path.GetFileName(d).Contains("FrameGen")
							select d).ToList())
						{
							string manifestPath = Path.Combine(frameGenDir, "backup_manifest.txt");
							if (File.Exists(manifestPath))
							{
								foreach (string line in File.ReadAllLines(manifestPath))
								{
									try
									{
										string[] parts = line.Split('|', 2);
										if (parts.Length == 2)
										{
											string relativePath = parts[0];
											string originalPath = parts[1];
											string backupFilePath = Path.Combine(frameGenDir, relativePath);
								// 精细化还原：当DLSS5还在使用时，只还原多帧生成特有的被替换文件，不还原共用文件
								string restoreFileName = Path.GetFileName(originalPath);
								bool shouldRestoreFG = true;
								if (game.Dlss5Enabled)
								{
									// DLSS5还在使用时，只还原多帧生成特有的被替换文件
									// dlssg必须还原
									bool isDlssgRestore = restoreFileName.Equals("nvngx_dlssg.dll", StringComparison.OrdinalIgnoreCase);
									// dlss只有不在exe目录下才还原（在exe目录下留给DLSS5处理）
									bool isDlssRestoreFG = restoreFileName.Equals("nvngx_dlss.dll", StringComparison.OrdinalIgnoreCase);
									bool isDlssInExeDirFG = isDlssRestoreFG && Path.GetDirectoryName(originalPath)?.Equals(exeDir, StringComparison.OrdinalIgnoreCase) == true;
									// 排错补丁必须还原
									bool isSlPatchRestore = restoreFileName.StartsWith("sl.", StringComparison.OrdinalIgnoreCase);
									shouldRestoreFG = isDlssgRestore || (isDlssRestoreFG && !isDlssInExeDirFG) || isSlPatchRestore;
								}
								if (File.Exists(backupFilePath) && !originalPath.Contains(BackupBaseDir) && shouldRestoreFG)
											{
												string directoryName = Path.GetDirectoryName(originalPath);
												if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
												{
													Directory.CreateDirectory(directoryName);
												}
												File.Copy(backupFilePath, originalPath, overwrite: true);
											}
										}
									}
									catch
									{
									}
								}
							}
						}
					}
				}
			foreach (string item6 in hashSet4)
			{
				try
				{
					// 不删除任何目录！防止误删游戏原生目录（Engine等）里的原有文件
					// 最多留下空目录，不影响游戏运行，安全第一
					// if (Directory.Exists(item6) && !item6.Contains(BackupBaseDir) && !skipDeleteDirFG && !isGameNativeDir)
					// {
					//     Directory.Delete(item6, recursive: true);
					// }
				}
				catch
				{
				}
			}
			// 搜索并删除共用目录（reshade-shaders、host64、runtime、Fonts）
			// 如果DLSS5已经还原了，就可以删除这些共用目录
			string[] searchDirsFG = new string[4] { "reshade-shaders", "host64", "runtime", "Fonts" };
			foreach (string searchPatternFG in searchDirsFG)
			{
				try
				{
					string[] directoriesWithDepthLimitFG = GetDirectoriesWithDepthLimit(text, searchPatternFG, 8);
					foreach (string text8FG in directoriesWithDepthLimitFG)
					{
						if (text8FG.Contains(BackupBaseDir) || !Directory.Exists(text8FG))
						{
							continue;
						}
						bool flag3FG = true;
						if (flag)
						{
							try
							{
								if (text8FG.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
								{
									string relativePathFG = text8FG.Substring(text2.Length).TrimStart(new char[2] { '\\', '/' });
									if (hashSet2.Any((string f) => f.StartsWith(relativePathFG + "\\", StringComparison.OrdinalIgnoreCase)))
									{
										flag3FG = false;
									}
								}
							}
							catch
							{
							}
						}
						// 如果DLSS5还在使用，不删除共用的reshade-shaders和Fonts目录
						string dirNameFG2 = Path.GetFileName(text8FG.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
							bool isSharedDirFG2 = dirNameFG2.Equals("reshade-shaders", StringComparison.OrdinalIgnoreCase) || dirNameFG2.Equals("Fonts", StringComparison.OrdinalIgnoreCase);
							// 同时判断目录路径是否包含reshade-shaders或Fonts（防止子目录被删除）
							bool isInSharedDirPathFG = text8FG.Contains("reshade-shaders") || text8FG.Contains("ReShade-shaders") || text8FG.Contains("Fonts") || text8FG.Contains("fonts");
							bool skipSharedDirFG = (isSharedDirFG2 || isInSharedDirPathFG) && game.Dlss5Enabled;
						if (flag3FG && !skipSharedDirFG)
						{
							try
							{
								// 不删除目录！防止误删游戏原生目录，只删文件就够了
								// Directory.Delete(text8FG, recursive: true);
							}
							catch
							{
							}
						}
					}
				}
				catch
				{
				}
			}
			// 后方还原检查：如果DLSS5已经被还原了，检查并清理DLSS5的残余文件
			if (!game.Dlss5Enabled)
			{
				try
				{
					CleanupResidualFiles(game, exeDir, "DLSS5");
				}
				catch
				{
				}
			}
			game.FrameGenEnabled = false;
			game.FrameGenMode = "";

			// 燕云十六声特殊适配：自动清理Win64rh目录里的多帧生成补丁文件
			try
			{
				var yanYunOtherDir = Path.Combine(Path.GetDirectoryName(exeDir), "Win64rh");
				if (game.Name.Contains("燕云", StringComparison.OrdinalIgnoreCase) && Directory.Exists(yanYunOtherDir))
				{
					string[] filesToDelete = { "nvngx_dlssg.dll", "nvngx_dlss.dll", "version.dll", "RTX40MFG-UI.ini", "dinput8.dll" };
					foreach (var file in filesToDelete)
					{
						var filePath = Path.Combine(yanYunOtherDir, file);
						if (File.Exists(filePath)) File.Delete(filePath);
					}
				}
			}
			catch { }

			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool RestoreDLSS5(GameInfo game)
	{
		try
		{
			string exeDir = GetExeDir(game);
			string text = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);

				// 判断多帧生成的dlss是否在游戏exe目录下（用于DLSS5还原时决定是否删除exe目录下的dlss）
				bool frameGenDlssInExeDir = false;
				if (game.FrameGenEnabled && Directory.Exists(BackupBaseDir))
				{
					string fgBaseName = game.Name;
					List<string> fgDirs = new List<string>();
					foreach (string bd in Directory.GetDirectories(BackupBaseDir))
					{
						string bdName = Path.GetFileName(bd);
						if ((bdName.Equals(fgBaseName, StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(fgBaseName + "V", StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(fgBaseName + "v", StringComparison.OrdinalIgnoreCase)) && !bdName.StartsWith(fgBaseName + " DLSS5", StringComparison.OrdinalIgnoreCase))
						{
							fgDirs.Add(bd);
						}
					}
					// 找最新的多帧生成备份目录
					string latestFgDir = null;
					int latestFgVer = 0;
					foreach (string d in fgDirs)
					{
						string dName = Path.GetFileName(d);
						int ver = dName.Equals(fgBaseName, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
						if (ver == 0)
						{
							string verStr = dName.Substring(fgBaseName.Length).TrimStart("Vv".ToCharArray());
							if (int.TryParse(verStr, out int v)) ver = v;
						}
						if (ver > latestFgVer) { latestFgVer = ver; latestFgDir = d; }
					}
					// 读取多帧生成备份中的backup_manifest.txt，判断dlss是否在exe目录下
					if (latestFgDir != null)
					{
						foreach (string fgSubDir in Directory.GetDirectories(latestFgDir))
						{
							if (Path.GetFileName(fgSubDir).Contains("FrameGen"))
							{
								string manifestPath = Path.Combine(fgSubDir, "backup_manifest.txt");
								if (File.Exists(manifestPath))
								{
									foreach (string line in File.ReadAllLines(manifestPath))
									{
										string[] parts = line.Split('|', 2);
										if (parts.Length == 2 && Path.GetFileName(parts[1]).Equals("nvngx_dlss.dll", StringComparison.OrdinalIgnoreCase))
										{
											if (Path.GetDirectoryName(parts[1])?.Equals(exeDir, StringComparison.OrdinalIgnoreCase) == true)
											{
												frameGenDlssInExeDir = true;
											}
										}
									}
								}
							}
						}
					}
				}
			// 查找最新的DLSS5备份目录（包括递增的V2、V3等）
			string dlss5BaseName = game.Name + " DLSS5";
			string path = Path.Combine(BackupBaseDir, dlss5BaseName);
			if (Directory.Exists(BackupBaseDir))
			{
				List<string> dlss5Dirs = new List<string>();
				foreach (string bd in Directory.GetDirectories(BackupBaseDir))
				{
					string bdName = Path.GetFileName(bd);
					if (bdName.Equals(dlss5BaseName, StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(dlss5BaseName + "V", StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(dlss5BaseName + "v", StringComparison.OrdinalIgnoreCase))
					{
						dlss5Dirs.Add(bd);
					}
				}
				// 只使用最新的备份目录（版本号最大的）
				if (dlss5Dirs.Count > 0)
				{
					string latestDir = dlss5Dirs[0];
					int latestVersion = 0;
					foreach (string d in dlss5Dirs)
					{
						string dName = Path.GetFileName(d);
						int ver = 0;
						if (dName.Equals(dlss5BaseName, StringComparison.OrdinalIgnoreCase))
						{
							ver = 1;
						}
						else
						{
							string verStr = dName.Substring(dlss5BaseName.Length).TrimStart("Vv".ToCharArray());
							if (int.TryParse(verStr, out int v))
							{
								ver = v;
							}
						}
						if (ver > latestVersion)
						{
							latestVersion = ver;
							latestDir = d;
						}
					}
					path = latestDir;
				}
			}
			new List<string>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string text2 = text;
			bool flag = false;
			HashSet<string> hashSet3 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet4 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] files;
			if (Directory.Exists(path))
			{
				foreach (string item2 in (from d in Directory.GetDirectories(path)
					where Path.GetFileName(d).Contains("DLSS5") || Path.GetFileName(d).Contains("DX9DLSS5")
					select d).ToList())
				{
					string text3 = Path.Combine(item2, "pre_install_snapshot.txt");
					if (File.Exists(text3) && !flag)
					{
						var (text4, hashSet5) = LoadDirectorySnapshot(text3);
						if (!string.IsNullOrEmpty(text4) && Directory.Exists(text4))
						{
							text2 = text4;
							hashSet2 = hashSet5;
							flag = true;
						}
					}
					foreach (string item3 in LoadAddedFiles(item2))
					{
						hashSet3.Add(item3);
					}
					foreach (string item4 in LoadAddedDirs(item2))
					{
						hashSet4.Add(item4);
					}
					files = Directory.GetFiles(item2);
					foreach (string text5 in files)
					{
						string fileName = Path.GetFileName(text5);
						switch (fileName)
						{
						case "backup_manifest.txt":
						case "game_path.txt":
						case "pre_install_snapshot.txt":
						case "added_files.txt":
						case "added_dirs.txt":
							continue;
						}
						hashSet.Add(fileName);
						if (!dictionary.ContainsKey(fileName))
						{
							dictionary[fileName] = text5;
						}
						string originalPath = GetOriginalPath(item2, fileName);
						if (!string.IsNullOrEmpty(originalPath) && !dictionary2.ContainsKey(fileName))
						{
							dictionary2[fileName] = originalPath;
						}
					}
				}
			}
			int num2 = 0;
			foreach (string item5 in hashSet3)
			{
				try
				{
							// 如果多帧生成还在使用，对于dlss只有多帧生成的dlss也在exe目录下才跳过，其他共用文件直接跳过
							bool isDlssFileDLSS5 = Path.GetFileName(item5).Equals("nvngx_dlss.dll", StringComparison.OrdinalIgnoreCase);
							bool isDlssInExeDir = isDlssFileDLSS5 && Path.GetDirectoryName(item5)?.Equals(exeDir, StringComparison.OrdinalIgnoreCase) == true;
							bool skipDeleteDLSS5 = isDlssInExeDir && game.FrameGenEnabled && frameGenDlssInExeDir;
							bool isDxgiFileDLSS5 = Path.GetFileName(item5).Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase);
							skipDeleteDLSS5 = skipDeleteDLSS5 || (isDxgiFileDLSS5 && game.FrameGenEnabled);
							bool isReShadeIniDLSS5 = Path.GetFileName(item5).Equals("ReShade.ini", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(item5).Equals("global.ini", StringComparison.OrdinalIgnoreCase);
							skipDeleteDLSS5 = skipDeleteDLSS5 || (isReShadeIniDLSS5 && game.FrameGenEnabled);
							// 多帧生成特有的文件（如version.dll、nvngx_dlssg.dll等），DLSS5还原时不删除
							skipDeleteDLSS5 = skipDeleteDLSS5 || (IsFrameGenSpecificFile(Path.GetFileName(item5)) && game.FrameGenEnabled);
								// 如果多帧生成还在使用，不删除reshade-shaders和Fonts目录里的文件（共用目录）
								if (game.FrameGenEnabled)
								{
									string item5Dir = Path.GetDirectoryName(item5) ?? "";
									bool isInReshadeShadersDir = item5Dir.Contains("reshade-shaders") || item5Dir.Contains("ReShade-shaders");
									bool isInFontsDir = item5Dir.Contains("Fonts") || item5Dir.Contains("fonts");
									skipDeleteDLSS5 = skipDeleteDLSS5 || isInReshadeShadersDir || isInFontsDir;
								}






					if (File.Exists(item5) && !item5.Contains(BackupBaseDir) && !skipDeleteDLSS5)
					{
						File.Delete(item5);
						num2++;
					}
				}
				catch
				{
				}
			}
			string[] filesWithDepthLimit = GetFilesWithDepthLimit(text, "*", 8);
			int num3 = 0;
			files = filesWithDepthLimit;
			foreach (string text6 in files)
			{
				try
				{
					if (text6.Contains(BackupBaseDir) || hashSet3.Contains(text6))
					{
						continue;
					}
					string fileName2 = Path.GetFileName(text6);
						// 如果多帧生成还在使用，不还原共用的nvngx_dlss.dll
						bool isDlssRestoreDLSS5 = fileName2.Equals("nvngx_dlss.dll", StringComparison.OrdinalIgnoreCase);
							bool skipRestoreDLSS5 = false;
							if (isDlssRestoreDLSS5 && game.FrameGenEnabled)
							{
								// 场景三：多帧生成的dlss也在exe目录下，先不还原，等多帧生成还原后再还原
								if (frameGenDlssInExeDir)
								{
									skipRestoreDLSS5 = true;
								}
								// 场景一和场景二：多帧生成的dlss不在exe目录下，DLSS5可以正常还原dlss
								else
								{
									skipRestoreDLSS5 = false;
								}
							}
						bool isDxgiRestoreDLSS5 = fileName2.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase);
						skipRestoreDLSS5 = skipRestoreDLSS5 || (isDxgiRestoreDLSS5 && game.FrameGenEnabled);
						if (!skipRestoreDLSS5 && hashSet.Contains(fileName2) && dictionary.TryGetValue(fileName2, out var value))
					{
						string text7;
						if (dictionary2.TryGetValue(fileName2, out var value2) && File.Exists(value2))
						{
							// 如果备份记录中有原路径，只还原原路径下的文件，其他目录下的同名文件跳过
							if (!string.Equals(text6, value2, StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
							text7 = value2;
						}
						else
						{
							text7 = text6;
						}
						string directoryName = Path.GetDirectoryName(text7);
						if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
						{
							Directory.CreateDirectory(directoryName);
						}
						File.Copy(value, text7, overwrite: true);
						num3++;
						continue;
					}
					bool flag2 = true;
					if (flag)
					{
						try
						{
							if (text6.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
							{
								string item = text6.Substring(text2.Length).TrimStart(new char[2] { '\\', '/' });
								if (hashSet2.Contains(item))
								{
									flag2 = false;
								}
							}
						}
						catch
						{
						}
					}
						// 如果多帧生成还在使用，对于dlss只有多帧生成的dlss也在exe目录下才跳过，其他共用文件直接跳过
						bool isDlssDeleteDLSS5 = fileName2.Equals("nvngx_dlss.dll", StringComparison.OrdinalIgnoreCase);
						bool isDlssFileInExeDir = isDlssDeleteDLSS5 && Path.GetDirectoryName(text6)?.Equals(exeDir, StringComparison.OrdinalIgnoreCase) == true;
						bool skipDeleteDLSS5Restore = isDlssFileInExeDir && game.FrameGenEnabled && frameGenDlssInExeDir;
						bool isDxgiDeleteDLSS5Restore = fileName2.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase);
						skipDeleteDLSS5Restore = skipDeleteDLSS5Restore || (isDxgiDeleteDLSS5Restore && game.FrameGenEnabled);




							// 如果多帧生成还在使用，不删除共用的ReShade文件
							bool isSharedReshadeFile = fileName2.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase) || fileName2.Equals("ReShade.ini", StringComparison.OrdinalIgnoreCase) || fileName2.Equals("global.ini", StringComparison.OrdinalIgnoreCase);
							bool skipSharedReshadeFile = isSharedReshadeFile && game.FrameGenEnabled;
							// 多帧生成特有的文件（如version.dll、nvngx_dlssg.dll等），DLSS5还原时不删除
							bool isFrameGenSpecificFile = IsFrameGenSpecificFile(fileName2) && game.FrameGenEnabled;
							// 如果多帧生成还在使用，不删除reshade-shaders和Fonts目录里的文件（共用目录）
							bool isInSharedDir = false;
							if (game.FrameGenEnabled)
							{
								string fileDir2 = Path.GetDirectoryName(text6) ?? "";
								bool isInReshadeDir2 = fileDir2.Contains("reshade-shaders") || fileDir2.Contains("ReShade-shaders");
								bool isInFontDir2 = fileDir2.Contains("Fonts") || fileDir2.Contains("fonts");
								isInSharedDir = isInReshadeDir2 || isInFontDir2;
							}
if (flag2 && IsAddedPatchFile(fileName2) && !skipDeleteDLSS5Restore && !skipSharedReshadeFile && !isFrameGenSpecificFile && !isInSharedDir)
					{
						File.Delete(text6);
						num2++;
					}
				}
				catch
				{
				}
			}
			foreach (string item6 in hashSet4)
			{
				try
				{
					// 如果多帧生成还在使用，不删除共用的reshade-shaders和Fonts目录
					string dirNameDLSS5 = Path.GetFileName(item6.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
						bool isSharedDirDLSS5 = dirNameDLSS5.Equals("reshade-shaders", StringComparison.OrdinalIgnoreCase) || dirNameDLSS5.Equals("Fonts", StringComparison.OrdinalIgnoreCase);
						// 同时判断目录路径是否包含reshade-shaders或Fonts（防止子目录被删除）
						bool isInSharedDirPathDLSS5 = item6.Contains("reshade-shaders") || item6.Contains("ReShade-shaders") || item6.Contains("Fonts") || item6.Contains("fonts");
						bool skipDeleteDirDLSS5 = (isSharedDirDLSS5 || isInSharedDirPathDLSS5) && game.FrameGenEnabled;
					if (Directory.Exists(item6) && !item6.Contains(BackupBaseDir) && !skipDeleteDirDLSS5)
					{
						Directory.Delete(item6, recursive: true);
					}
				}
				catch
				{
				}
			}
			files = new string[4] { "reshade-shaders", "host64", "runtime", "Fonts" };
			foreach (string searchPattern in files)
			{
				try
				{
					string[] directoriesWithDepthLimit = GetDirectoriesWithDepthLimit(text, searchPattern, 8);
					foreach (string text8 in directoriesWithDepthLimit)
					{
						if (text8.Contains(BackupBaseDir) || !Directory.Exists(text8))
						{
							continue;
						}
						bool flag3 = true;
						if (flag)
						{
							try
							{
								if (text8.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
								{
									string relativePath = text8.Substring(text2.Length).TrimStart(new char[2] { '\\', '/' });
									if (hashSet2.Any((string f) => f.StartsWith(relativePath + "\\", StringComparison.OrdinalIgnoreCase)))
									{
										flag3 = false;
									}
								}
							}
							catch
							{
							}
						}
							// 如果多帧生成还在使用，不删除共用的reshade-shaders和Fonts目录（包括子目录）
							string dirName = Path.GetFileName(text8.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
							bool isSharedDir = dirName.Equals("reshade-shaders", StringComparison.OrdinalIgnoreCase) || dirName.Equals("Fonts", StringComparison.OrdinalIgnoreCase);
							// 同时判断目录路径是否包含reshade-shaders或Fonts（防止子目录被删除）
							bool isInSharedDirPath = text8.Contains("reshade-shaders") || text8.Contains("ReShade-shaders") || text8.Contains("Fonts") || text8.Contains("fonts");
							bool skipSharedDir = (isSharedDir || isInSharedDirPath) && game.FrameGenEnabled;
							if (flag3 && !skipSharedDir)
						{
							try
							{
								Directory.Delete(text8, recursive: true);
							}
							catch
							{
							}
						}
					}
				}
				catch
				{
				}
			}
			// 后方还原检查：如果多帧生成已经被还原了，检查并清理多帧生成的残余文件
			if (!game.FrameGenEnabled)
			{
				try
				{
					CleanupResidualFiles(game, exeDir, "FrameGen");
				}
				catch
				{
				}
			}
			// 还原后保留备份目录，不删除（用户要求备份文件必须保留，直到用户自行删除）
			game.Dlss5Enabled = false;
			game.Dx9Dlss5Enabled = false;
			return true;
		}
		catch
		{
			return false;
		}
	}

	
	/// <summary>
	/// 后方还原检查：清理另一个功能的残余文件
	/// </summary>
	/// <param name="game">游戏信息</param>
	/// <param name="exeDir">游戏exe目录</param>
	/// <param name="targetType">目标类型："FrameGen"或"DLSS5"</param>
	private static void CleanupResidualFiles(GameInfo game, string exeDir, string targetType)
	{
		try
		{
			if (!Directory.Exists(BackupBaseDir)) return;

			// 查找目标类型的最新备份目录
			string baseName = targetType == "DLSS5" ? game.Name + " DLSS5" : game.Name;
			List<string> targetDirs = new List<string>();
			foreach (string bd in Directory.GetDirectories(BackupBaseDir))
			{
				string bdName = Path.GetFileName(bd);
				if (targetType == "DLSS5")
				{
					if (bdName.Equals(baseName, StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(baseName + "V", StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(baseName + "v", StringComparison.OrdinalIgnoreCase))
					{
						targetDirs.Add(bd);
					}
				}
				else
				{
					if ((bdName.Equals(baseName, StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(baseName + "V", StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(baseName + "v", StringComparison.OrdinalIgnoreCase)) && !bdName.StartsWith(baseName + " DLSS5", StringComparison.OrdinalIgnoreCase))
					{
						targetDirs.Add(bd);
					}
				}
			}

			// 找最新的备份目录
			string latestDir = null;
			int latestVer = 0;
			foreach (string d in targetDirs)
			{
				string dName = Path.GetFileName(d);
				int ver = dName.Equals(baseName, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
				if (ver == 0)
				{
					string verStr = dName.Substring(baseName.Length).TrimStart("Vv".ToCharArray());
					if (int.TryParse(verStr, out int v)) ver = v;
				}
				if (ver > latestVer) { latestVer = ver; latestDir = d; }
			}

			if (latestDir == null) return;

			// 读取备份中的added_files.txt、added_dirs.txt和backup_manifest.txt
			HashSet<string> addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> addedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> replacedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (string subDir in Directory.GetDirectories(latestDir))
			{
				string subDirName = Path.GetFileName(subDir);
				bool isTargetDir = targetType == "DLSS5" ? (subDirName.Contains("DLSS5") || subDirName.Contains("DX9DLSS5")) : subDirName.Contains("FrameGen");
				if (!isTargetDir) continue;

				// 读取added_files.txt
				string addedFilesPath = Path.Combine(subDir, "added_files.txt");
				if (File.Exists(addedFilesPath))
				{
					foreach (string line in File.ReadAllLines(addedFilesPath))
					{
						if (!string.IsNullOrWhiteSpace(line))
						{
							addedFiles.Add(line.Trim());
						}
					}
				}

				// 读取added_dirs.txt（只有安装时记录为新增的目录才允许删除）
				string addedDirsPath = Path.Combine(subDir, "added_dirs.txt");
				if (File.Exists(addedDirsPath))
				{
					foreach (string line in File.ReadAllLines(addedDirsPath))
					{
						if (!string.IsNullOrWhiteSpace(line))
						{
							addedDirs.Add(line.Trim());
						}
					}
				}

				// 读取backup_manifest.txt
				string manifestPath = Path.Combine(subDir, "backup_manifest.txt");
				if (File.Exists(manifestPath))
				{
					foreach (string line in File.ReadAllLines(manifestPath))
					{
						string[] parts = line.Split('|', 2);
						if (parts.Length == 2)
						{
							string relativePath = parts[0];
							string originalPath = parts[1];
							string backupFilePath = Path.Combine(subDir, relativePath);
							if (File.Exists(backupFilePath) && !replacedFiles.ContainsKey(originalPath))
							{
								replacedFiles[originalPath] = backupFilePath;
							}
						}
					}
				}
			}

			// 清理残余的新增文件
			// 因为是后方还原检查，另一个功能已经还原了，所以共用文件也应该删除
			foreach (string addedFile in addedFiles)
			{
				try
				{
					if (File.Exists(addedFile) && !addedFile.Contains(BackupBaseDir) && !replacedFiles.ContainsKey(addedFile))
					{
						File.Delete(addedFile);
					}
				}
				catch
				{
				}
			}
			// 不再按固定文件名清单硬删共用文件：未记录在 added_files.txt 的可能是游戏原生文件，一律保留。
			// 已记录的补丁文件已在上面的循环里删除，被替换的原文件会在下面恢复。
			// 不再按配置名清单盲删 d3d11/d3d12/version.ini 等，未登记为新增的文件一律保留。

			// 还原残余的被替换文件
			// 因为是后方还原检查，另一个功能已经还原了，所以dlss也应该还原
			foreach (var kvp in replacedFiles)
			{
				try
				{
					string originalPath = kvp.Key;
					string backupFilePath = kvp.Value;
					string fileName = Path.GetFileName(originalPath);

					bool isDlssg = fileName.Equals("nvngx_dlssg.dll", StringComparison.OrdinalIgnoreCase);
					bool isDlss = fileName.Equals("nvngx_dlss.dll", StringComparison.OrdinalIgnoreCase);
					bool isSlPatch = fileName.StartsWith("sl.", StringComparison.OrdinalIgnoreCase);

					bool shouldRestore = false;
					if (targetType == "FrameGen")
					{
						// 后方还原检查时，另一个功能已经还原了，所以所有被替换文件都应该还原
						shouldRestore = true;
					}
					else
					{
						// 后方还原检查时，另一个功能已经还原了，所以所有被替换文件都应该还原
						shouldRestore = true;
					}

					if (shouldRestore && File.Exists(backupFilePath) && !originalPath.Contains(BackupBaseDir))
					{
						string directoryName = Path.GetDirectoryName(originalPath);
						if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
						{
							Directory.CreateDirectory(directoryName);
						}
						File.Copy(backupFilePath, originalPath, overwrite: true);
					}
				}
				catch
				{
				}
			}
			// 后方还原检查：只删除安装时记录为“新增目录”的空目录。
			// Fonts、runtime、host64 等目录在不少游戏里本来就是原生的，未记录的一律保留，
			// 绝不能整目录递归删除，否则会破坏游戏本体。
			string[] residualDirsToCheck = new string[] { "reshade-shaders", "Fonts", "host64", "runtime" };
			foreach (string dirName in residualDirsToCheck)
			{
				try
				{
					string dirPath = Path.Combine(exeDir, dirName);
					if (!Directory.Exists(dirPath)) continue;
					if (!addedDirs.Contains(dirPath)) continue;
					if (Directory.GetFileSystemEntries(dirPath).Length == 0)
					{
						Directory.Delete(dirPath, recursive: false);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

public static bool HasBackup(GameInfo game)
	{
		string path = Path.Combine(BackupBaseDir, game.Name);
		if (Directory.Exists(path))
		{
			return Directory.GetDirectories(path).Length != 0;
		}
		return false;
	}

	public static bool ForceClean(GameInfo game)
	{
		try
		{
			string exeDir = GetExeDir(game);
			string[] array = new string[40]
			{
				"RTX40MFG.asi", "RTX40MFG_config.json", "version.dll", "version.ini", "nvngx_dlss.dll", "nvngx_dlssg.dll", "nvngx_dlssnr.dll", "nvngx_dlssd.dll", "dxgi.dll", "ReShade.ini",
				"ReShade.log", "ReShadePreset.ini", "dinput8.dll", "dlssg_sm86.ini", "RTX40MFGCore.dll", "RTX40MFG-UI.addon64", "renodx-dlss5.addon64", "D3DCompiler_47.dll", "RE_DLSS5_Core.dll", "RE_DLSS5_Core_settings.json",
				"dlssnr_on_amd_setup.exe", "D3D9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe", "dlss5-feed.addon32", "dlss5-feed.addon64", "sl.common.dll", "sl.dlss.dll", "sl.dlss_g.dll", "sl.interposer.dll",
				"sl.nis.dll", "sl.reflex.dll", "sl.pcl.dll", "sl.deepdvc.dll", "sl.dlss_d.dll", "sl.dlss_nr.dll", "sl.nvperf.dll", "sl.directsr.dll", "NvLowLatencyVk.dll", "nvngx_deepdvc.dll"
			};
			foreach (string path in array)
			{
				string path2 = Path.Combine(exeDir, path);
				if (File.Exists(path2))
				{
					File.Delete(path2);
				}
			}
			array = new string[3] { "reshade-shaders", "host64", "runtime" };
			foreach (string path3 in array)
			{
				string path4 = Path.Combine(exeDir, path3);
				if (Directory.Exists(path4))
				{
					Directory.Delete(path4, recursive: true);
				}
			}
			game.FrameGenEnabled = false;
			game.Dlss5Enabled = false;
			game.Dx9Dlss5Enabled = false;
			return true;
		}
		catch
		{
			return false;
		}
	}

				private static string? FindSlFilesDir(string exeDir)
	{
		try
		{
			// 1. 先检查exe目录下
			string[] exeFiles = Directory.GetFiles(exeDir, "sl.*.dll", SearchOption.TopDirectoryOnly);
			if (exeFiles.Length != 0)
			{
				return exeDir;
			}
			// 2. 精准判断游戏最根目录
			string rootDir = exeDir;
			string checkDir = exeDir;
			string engineRoot = null;  // 包含Engine目录的根目录（最准确）
			string featureRoot = null; // 包含多个游戏特征目录的根目录（保底）
			for (int i = 0; i < 8; i++) // 最多往上找8层
			{
				DirectoryInfo parent = Directory.GetParent(checkDir);
				if (parent == null || parent.FullName == checkDir) break;
				// 检查是否是盘符根目录，如果是就停止（不能回到盘符）
				if (parent.FullName.Length <= 3) break;
				checkDir = parent.FullName;
				// 判断是否包含Engine目录（UE引擎游戏，最准确）
				if (Directory.Exists(Path.Combine(checkDir, "Engine")))
				{
					engineRoot = checkDir;
					// 找到Engine目录后继续往上找，找最上层的Engine目录（真正的游戏根目录）
				}
				// 如果还没找到Engine目录，检查是否包含多个游戏特征目录（作为保底）
				if (engineRoot == null)
				{
					int featureCount = 0;
					string[] gameFeatures = { "Content", "content", "Data", "data", "Binaries", "binaries", "Retail", "retail", "Game", "game" };
					foreach (string feature in gameFeatures)
					{
						if (Directory.Exists(Path.Combine(checkDir, feature))) featureCount++;
					}
					if (featureCount >= 2)
					{
						featureRoot = checkDir;
					}
				}
			}
			// 优先选择包含Engine目录的根目录（最准确）
			if (engineRoot != null)
			{
				rootDir = engineRoot;
			}
			// 如果没有Engine目录，用包含多个游戏特征目录的根目录（保底）
			else if (featureRoot != null)
			{
				rootDir = featureRoot;
			}
			// 3. 在游戏最根目录下搜索，深度15层
			string[] filesWithDepthLimit = GetFilesWithDepthLimit(rootDir, "sl.*.dll", 15);
			if (filesWithDepthLimit.Length != 0)
			{
				return Path.GetDirectoryName(filesWithDepthLimit[0]);
			}
		}
		catch
		{
		}
		return null;
	}
public static List<(string fileName, bool existsInGame)> GetTroubleshootFiles(GameInfo game)
	{
		List<(string, bool)> list = new List<(string, bool)>();
		try
		{
			EnsurePatchesExtracted();
				string gameRoot = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : Path.GetDirectoryName(game.ExePath));
				string text = FindSlFilesDir(Path.GetDirectoryName(game.ExePath));
			if (text == null)
			{
				return list;
			}
			string path = ((!(game.FrameGenMode == "经典模式")) ? GetPatchDir("40系原生多帧生成\\排错") : GetPatchDir("老驱动多帧生成\\排错"));
			if (!Directory.Exists(path))
			{
				return list;
			}
			string[] files = Directory.GetFiles(path, "sl.*.dll");
			for (int i = 0; i < files.Length; i++)
			{
				string fileName = Path.GetFileName(files[i]);
				bool item = File.Exists(Path.Combine(text, fileName));
				list.Add((fileName, item));
			}
		}
		catch
		{
		}
		return list;
	}

	public static int TroubleshootSelected(GameInfo game, List<string> filesToReplace)
	{
		try
		{
			EnsurePatchesExtracted();
				string gameRoot = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : Path.GetDirectoryName(game.ExePath));
				string text = FindSlFilesDir(Path.GetDirectoryName(game.ExePath));
			if (text == null)
			{
				return 0;
			}
				// 找到最新的多帧生成备份目录，排错补丁备份到那里
				string backupDir = null;
				if (Directory.Exists(BackupBaseDir))
				{
					List<string> frameGenDirs = new List<string>();
					foreach (string bd in Directory.GetDirectories(BackupBaseDir))
					{
						string bdName = Path.GetFileName(bd);
						if ((bdName.Equals(game.Name, StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(game.Name + "V", StringComparison.OrdinalIgnoreCase) || bdName.StartsWith(game.Name + "v", StringComparison.OrdinalIgnoreCase)) && !bdName.StartsWith(game.Name + " DLSS5", StringComparison.OrdinalIgnoreCase))
						{
							foreach (string fgDir in Directory.GetDirectories(bd))
							{
								if (Path.GetFileName(fgDir).Contains("FrameGen"))
								{
									frameGenDirs.Add(fgDir);
								}
							}
						}
					}
					if (frameGenDirs.Count > 0)
					{
						// 找最新的（修改时间最晚的）
						backupDir = frameGenDirs.OrderByDescending(d => Directory.GetLastWriteTime(d)).First();
					}
				}
				if (backupDir == null)
				{
					backupDir = GetBackupDir(game, "Troubleshoot");
				}
			string path = ((!(game.FrameGenMode == "经典模式")) ? GetPatchDir("40系原生多帧生成\\排错") : GetPatchDir("老驱动多帧生成\\排错"));
			int num = 0;
			foreach (string item in filesToReplace)
			{
				string text2 = Path.Combine(path, item);
				string text3 = Path.Combine(text, item);
				if (File.Exists(text2) && File.Exists(text3))
				{
						BackupFile(text3, backupDir, gameRoot);
					File.Copy(text2, text3, overwrite: true);
					num++;
				}
			}
			return num;
		}
		catch
		{
			return 0;
		}
	}

	public static (int replaced, int skipped) Troubleshoot(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "Troubleshoot");
			string path = ((!(game.FrameGenMode == "经典模式")) ? GetPatchDir("40系原生多帧生成\\排错") : GetPatchDir("老驱动多帧生成\\排错"));
			if (!Directory.Exists(path))
			{
				return (replaced: 0, skipped: 0);
			}
			int num = 0;
			int num2 = 0;
			string[] files = Directory.GetFiles(path, "sl.*.dll");
			foreach (string text in files)
			{
				string fileName = Path.GetFileName(text);
				string text2 = Path.Combine(exeDir, fileName);
				if (File.Exists(text2))
				{
					BackupFile(text2, backupDir);
					File.Copy(text, text2, overwrite: true);
					num++;
				}
				else
				{
					num2++;
				}
			}
			return (replaced: num, skipped: num2);
		}
		catch
		{
			return (replaced: 0, skipped: 0);
		}
	}
}






