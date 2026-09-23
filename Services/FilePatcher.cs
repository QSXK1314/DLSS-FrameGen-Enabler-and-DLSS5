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

	private static void CopyPatchFiles(string patchDir, string destDir, string backupDir, bool backupExisting = true, string[]? skipExtensions = null)
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
				if (backupExisting & flag)
				{
					BackupFile(text2, backupDir);
				}
				File.Copy(text, text2, overwrite: true);
				if (!flag)
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

	private static string GetBackupDir(GameInfo game, string feature)
		{
			string baseName = game.Name;
			string dirName = baseName;
			int ver = 2;
			while(Directory.Exists(Path.Combine(BackupBaseDir, dirName, feature))) {
				dirName = baseName + "V" + ver;
				ver++;
			}
			string text = Path.Combine(BackupBaseDir, dirName, feature);
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
	private static void CopyDirectory(string source, string dest, bool overwrite = true, bool includeSharedFiles = false, string? backupDir = null)
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
			CopyDirectory(text5, Path.Combine(dest, Path.GetFileName(text5)), overwrite, includeSharedFiles: false, backupDir);
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

		private static void BackupFile(string sourceFile, string backupDir)
		{
			if (!File.Exists(sourceFile))
			{
				return;
			}
			string gameRoot = "";
			string gamePathFile = Path.Combine(backupDir, "game_path.txt");
			if(File.Exists(gamePathFile)) {
				gameRoot = File.ReadAllText(gamePathFile).Trim();
			}
			string relPath = Path.GetFileName(sourceFile);
			if(!string.IsNullOrEmpty(gameRoot) && sourceFile.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase)) {
				relPath = sourceFile.Substring(gameRoot.Length).TrimStart(Path.DirectorySeparatorChar);
			}
			string text = Path.Combine(backupDir, relPath);
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			if (!File.Exists(text))
			{
				File.Copy(sourceFile, text, overwrite: true);
			}
			try
			{
				string path = Path.Combine(backupDir, "backup_manifest.txt");
				string item = relPath + "|" + sourceFile;
				List<string> list = (File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>());
				if (!list.Any((string e) => e.EndsWith("|" + sourceFile, StringComparison.OrdinalIgnoreCase)))
				{
					list.Add(item);
					File.WriteAllLines(path, list);
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
				if (array2.Length == 2 && array2[0].EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
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
			string text = Path.Combine(exeDir, "nvngx_dlssg.dll");
			if (File.Exists(text))
			{
				return text;
			}
			string[] filesWithDepthLimit = GetFilesWithDepthLimit(gamePath, "nvngx_dlssg.dll", 8);
			if (filesWithDepthLimit.Length == 0)
			{
				return null;
			}
			string text2 = filesWithDepthLimit.FirstOrDefault((string f) => f.Contains("plugins", StringComparison.OrdinalIgnoreCase));
			if (text2 != null)
			{
				return text2;
			}
			string text3 = filesWithDepthLimit.FirstOrDefault((string f) => f.Contains("Streamline", StringComparison.OrdinalIgnoreCase));
			if (text3 != null)
			{
				return text3;
			}
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
	private static string? FindNvngxDlss(string gamePath, string exeDir)
	{
		try
		{
			string text = Path.Combine(exeDir, "nvngx_dlss.dll");
			if (File.Exists(text))
			{
				return text;
			}
			string[] filesWithDepthLimit = GetFilesWithDepthLimit(gamePath, "nvngx_dlss.dll", 8);
			if (filesWithDepthLimit.Length == 0)
			{
				return null;
			}
			string text2 = filesWithDepthLimit.FirstOrDefault((string f) => f.Contains("plugins", StringComparison.OrdinalIgnoreCase));
			if (text2 != null)
			{
				return text2;
			}
			string text3 = filesWithDepthLimit.FirstOrDefault((string f) => f.Contains("Streamline", StringComparison.OrdinalIgnoreCase));
			if (text3 != null)
			{
				return text3;
			}
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
			string backupDir = GetBackupDir(game, "FrameGen_Classic");
				string debugPath = System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "dlss_debug.txt");
				System.IO.File.WriteAllText(debugPath, "gamePath=" + gamePath + "\r\nexeDir=" + exeDir + "\r\n");
				var allDlss = System.IO.Directory.GetFiles(gamePath, "nvngx_dlss.dll", System.IO.SearchOption.AllDirectories);
				System.IO.File.AppendAllText(debugPath, "Found " + allDlss.Length + " dlss files\r\n");
				foreach(string dlssFile in allDlss) {
					System.IO.File.AppendAllText(debugPath, dlssFile + "\r\n");
					string dlssSrc = System.IO.Path.Combine(GetPatchDir("_Shared"), "nvngx_dlss.dll");
					if(System.IO.File.Exists(dlssSrc)) {
						BackupFile(dlssFile, backupDir);
						System.IO.File.Copy(dlssSrc, dlssFile, true);
						System.IO.File.AppendAllText(debugPath, "Replaced: " + dlssFile + "\r\n");
					}
				}
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
			string dlssPath = FindNvngxDlss(gamePath, exeDir);
			string dlssSource = Path.Combine(GetPatchDir("_Shared"), "nvngx_dlss.dll");
			if(File.Exists(dlssSource)) {
				if(dlssPath != null) {
					BackupFile(dlssPath, backupDir);
					File.Copy(dlssSource, dlssPath, overwrite: true);
				} else {
					string dlssExePath = Path.Combine(exeDir, "nvngx_dlss.dll");
					bool dlssExists = File.Exists(dlssExePath);
					File.Copy(dlssSource, dlssExePath, overwrite: true);
					if(!dlssExists) {
						RecordAddedFile(backupDir, dlssExePath);
					}
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
			string backupDir = GetBackupDir(game, "FrameGen_Advanced");
				// 递归备份替换所有sl.*.dll补丁文件
				foreach(string slFile in System.IO.Directory.GetFiles(gamePath, "sl.*.dll", System.IO.SearchOption.AllDirectories)) {
					string slPatchPath = System.IO.Path.Combine(GetPatchDir("_Shared"), System.IO.Path.GetFileName(slFile));
					if(System.IO.File.Exists(slPatchPath)) {
						BackupFile(slFile, backupDir);
						System.IO.File.Copy(slPatchPath, slFile, true);
					}
				}
				var allDlssAdv = System.IO.Directory.GetFiles(gamePath, "nvngx_dlss.dll", System.IO.SearchOption.AllDirectories);
				foreach(string dlssFile in allDlssAdv) {
					string dlssSrcAdv = System.IO.Path.Combine(GetPatchDir("_Shared"), "nvngx_dlss.dll");
					if(System.IO.File.Exists(dlssSrcAdv)) {
						BackupFile(dlssFile, backupDir);
						System.IO.File.Copy(dlssSrcAdv, dlssFile, true);
					}
				}
			string text = FindNvngxDlssg(gamePath, exeDir);
			string sourceFileName = Path.Combine(GetPatchDir("40系原生多帧生成"), "nvngx_dlssg.dll");
			if (text != null)
			{
				BackupFile(text, backupDir);
				try { File.Copy(sourceFileName, text, overwrite: true); } catch { }
			}
			else
			{
				string text2 = Path.Combine(exeDir, "nvngx_dlssg.dll");
				bool num = File.Exists(text2);
				try { File.Copy(sourceFileName, text2, overwrite: true); } catch { }
				if (!num)
				{
					RecordAddedFile(backupDir, text2);
				}
			}
			string text3 = Path.Combine(exeDir, "nvngx_dlss.dll");
			bool num2 = File.Exists(text3);
			if (num2)
			{
				BackupFile(text3, backupDir);
			}
				try { File.Copy(ResolvePatchFile(GetPatchDir("40系原生多帧生成\\排错"), "nvngx_dlss.dll"), text3, overwrite: true); } catch { }
			if (!num2)
			{
				RecordAddedFile(backupDir, text3);
			}
			string text4 = Path.Combine(exeDir, "version.dll");
			bool num3 = File.Exists(text4);
			if (num3)
			{
				BackupFile(text4, backupDir);
			}
				try { File.Copy(Path.Combine(GetPatchDir("40系原生多帧生成"), "version.dll"), text4, overwrite: true); } catch { }
			if (!num3)
			{
				RecordAddedFile(backupDir, text4);
			}
			string[] files = Directory.GetFiles(GetPatchDir("40系原生多帧生成\\reshade"));
			foreach (string text5 in files)
			{
				string text6 = Path.Combine(exeDir, Path.GetFileName(text5));
				bool num4 = File.Exists(text6);
				if (num4)
				{
					BackupFile(text6, backupDir);
				}
				try { File.Copy(text5, text6, overwrite: true); } catch { }
				if (!num4)
				{
					RecordAddedFile(backupDir, text6);
				}
			}
			string patchDir = GetPatchDir("dlss5 reshade配置页面\\reshade-shaders");
			string dest = Path.Combine(exeDir, "reshade-shaders");
			if (Directory.Exists(patchDir))
			{
				CopyDirectory(patchDir, dest, overwrite: true, includeSharedFiles: false, backupDir);
			}
			CopyFontsFiles(exeDir, backupDir);
			files = Directory.GetFiles(GetPatchDir("40系原生多帧生成\\Universal-RTX-40-MFG-Unlock-v1.2.1"));
			foreach (string text7 in files)
			{
				string fileName = Path.GetFileName(text7);
				if (fileName == "global.ini")
				{
					string text8 = Path.Combine(exeDir, "version.ini");
					bool num5 = File.Exists(text8);
					if (num5)
					{
						BackupFile(text8, backupDir);
					}
					File.Copy(text7, text8, overwrite: true);
					if (!num5)
					{
						RecordAddedFile(backupDir, text8);
					}
				}
				else if (fileName.EndsWith(".dll") || fileName.EndsWith(".asi") || fileName.EndsWith(".addon64"))
				{
					string text9 = Path.Combine(exeDir, fileName);
					bool num6 = File.Exists(text9);
					if (num6)
					{
						BackupFile(text9, backupDir);
					}
					File.Copy(text7, text9, overwrite: true);
					if (!num6)
					{
						RecordAddedFile(backupDir, text9);
					}
				}
			}
			game.FrameGenEnabled = true;
			game.FrameGenMode = "高级模式";
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool InstallRTX20FrameGen(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "FrameGen_RTX20");
			string patchDir = GetPatchDir("20系多帧生成");
			CopyPatchFiles(patchDir, exeDir, backupDir, backupExisting: true, new string[1] { ".md" });
			string text = Path.Combine(patchDir, "runtime");
			string dest = Path.Combine(exeDir, "runtime");
			if (Directory.Exists(text))
			{
				CopyDirectory(text, dest, overwrite: true, includeSharedFiles: false, backupDir);
			}
			game.FrameGenEnabled = true;
			game.FrameGenMode = "RTX20系";
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool InstallRTX30FrameGen(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "FrameGen_RTX30");
			CopyPatchFiles(GetPatchDir("30系多帧生成"), exeDir, backupDir);
			game.FrameGenEnabled = true;
			game.FrameGenMode = "RTX30系";
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool InstallREEngineFrameGen(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "FrameGen_RE");
			CopyPatchFiles(GetPatchDir("RE引擎多帧生成"), exeDir, backupDir);
			string text = Path.Combine(exeDir, "dinput8.dll");
			bool num = File.Exists(text);
			if (num)
			{
				BackupFile(text, backupDir);
			}
			File.Copy(Path.Combine(GetPatchDir("RE框架"), "dinput8.dll"), text, overwrite: true);
			if (!num)
			{
				RecordAddedFile(backupDir, text);
			}
			game.FrameGenEnabled = true;
			game.FrameGenMode = "RE引擎";
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
			string backupDir = GetBackupDir(game, "FrameGen_2077");
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
			string backupDir = GetBackupDir(game, "DLSS5_Common");
			CopyDirectory(GetPatchDir("dlss5 reshade配置页面"), exeDir, overwrite: true, includeSharedFiles: true, backupDir);
			CopyFontsFiles(exeDir, backupDir);
			CopyPatchFiles(GetPatchDir("N卡DLSS5补丁"), exeDir, backupDir);
			game.Dlss5Enabled = true;
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
			string backupDir = GetBackupDir(game, "DLSS5_AMD");
			CopyDirectory(GetPatchDir("dlss5 reshade配置页面"), exeDir, overwrite: true, includeSharedFiles: true, backupDir);
			CopyFontsFiles(exeDir, backupDir);
			CopyPatchFiles(GetPatchDir("A卡DLSS5补丁（仅支持7000系和9000系）"), exeDir, backupDir);
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

	public static bool InstallREEngineDLSS5(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "DLSS5_RE");
			CopyDirectory(GetPatchDir("dlss5 reshade配置页面"), exeDir, overwrite: true, includeSharedFiles: true, backupDir);
			CopyFontsFiles(exeDir, backupDir);
			string text = Path.Combine(exeDir, "dinput8.dll");
			bool num = File.Exists(text);
			if (num)
			{
				BackupFile(text, backupDir);
			}
			File.Copy(Path.Combine(GetPatchDir("RE框架"), "dinput8.dll"), text, overwrite: true);
			if (!num)
			{
				RecordAddedFile(backupDir, text);
			}
			CopyPatchFiles(GetPatchDir("RE引擎通用dlss5补丁\\DLSS5 on RE"), exeDir, backupDir, backupExisting: true, new string[2] { ".jpg", ".md" });
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
			string backupDir = GetBackupDir(game, "DLSS5_YanYun");
			string patchDir = GetPatchDir("燕云十六声DLSS5补丁");
			CopyPatchFiles(patchDir, exeDir, backupDir);
			CopyFontsFiles(exeDir, backupDir);
			string text = Path.Combine(patchDir, "reshade-shaders");
			if (Directory.Exists(text))
			{
				CopyDirectory(text, Path.Combine(exeDir, "reshade-shaders"), overwrite: true, includeSharedFiles: false, backupDir);
			}
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

	public static bool InstallDX9DLSS5_32(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "DX9DLSS5_32");
			string patchDir = GetPatchDir("dx9 32位dlss5补丁");
			CopyPatchFiles(patchDir, exeDir, backupDir);
			CopyFontsFiles(exeDir, backupDir);
			string text = Path.Combine(patchDir, "reshade-shaders");
			if (Directory.Exists(text))
			{
				CopyDirectory(text, Path.Combine(exeDir, "reshade-shaders"), overwrite: true, includeSharedFiles: false, backupDir);
			}
			string text2 = Path.Combine(patchDir, "host64");
			if (Directory.Exists(text2))
			{
				CopyDirectory(text2, Path.Combine(exeDir, "host64"), overwrite: true, includeSharedFiles: true, backupDir);
			}
			game.Dx9Dlss5Enabled = true;
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool InstallDX9DLSS5_64(GameInfo game)
	{
		try
		{
			EnsurePatchesExtracted();
			string exeDir = GetExeDir(game);
			string backupDir = GetBackupDir(game, "DX9DLSS5_64");
			string patchDir = GetPatchDir("dx9 64位dlss5补丁");
			CopyPatchFiles(patchDir, exeDir, backupDir);
			CopyFontsFiles(exeDir, backupDir);
			string text = Path.Combine(patchDir, "reshade-shaders");
			if (Directory.Exists(text))
			{
				CopyDirectory(text, Path.Combine(exeDir, "reshade-shaders"), overwrite: true, includeSharedFiles: false, backupDir);
			}
			game.Dx9Dlss5Enabled = true;
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
			files = new string[3] { "reshade-shaders", "runtime", "host64" };
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
			string path = Path.Combine(BackupBaseDir, game.Name);
			if (!Directory.Exists(path))
			{
				return (filesToDelete: list, filesToRestore: list2, dirsToDelete: list3);
			}
			List<string> list4 = (from d in Directory.GetDirectories(path)
				where Path.GetFileName(d).Contains("DLSS5") || Path.GetFileName(d).Contains("DX9DLSS5")
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

	public static bool RestoreFrameGen(GameInfo game)
	{
		try
		{
			string exeDir = GetExeDir(game);
			string text = ((!string.IsNullOrEmpty(game.GamePath) && Directory.Exists(game.GamePath)) ? game.GamePath : exeDir);
			string path = Path.Combine(BackupBaseDir, game.Name);
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
				foreach(string subDir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories)) {
					string manifestPath = Path.Combine(subDir, "backup_manifest.txt");
					if(File.Exists(manifestPath)) {
						foreach(string line in File.ReadAllLines(manifestPath)) {
							string[] parts = line.Split('|', 2);
							if(parts.Length == 2) {
								string originalPath = parts[1];
								string relPath = parts[0];
								string backupFile = Path.Combine(subDir, relPath);
								if(File.Exists(backupFile)) {
									Directory.CreateDirectory(Path.GetDirectoryName(originalPath));
									File.Copy(backupFile, originalPath, overwrite: true);
								}
							}
						}
					}
				}
			{
				foreach (string item2 in (from d in Directory.GetDirectories(path)
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
			int num2 = 0;
			foreach (string item5 in hashSet3)
			{
				try
				{
					if (File.Exists(item5) && !item5.Contains(BackupBaseDir))
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
					if (hashSet.Contains(fileName2) && dictionary.TryGetValue(fileName2, out var value))
					{
						string text7 = ((!dictionary2.TryGetValue(fileName2, out var value2) || !File.Exists(value2)) ? text6 : value2);
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
					if (flag2 && IsAddedPatchFile(fileName2))
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
					if (Directory.Exists(item6) && !item6.Contains(BackupBaseDir))
					{
						Directory.Delete(item6, recursive: true);
					}
				}
				catch
				{
				}
			}
			files = new string[4] { "reshade-shaders", "runtime", "host64", "Fonts" };
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
						if (flag3)
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
			try
			{
				if (Directory.Exists(path))
				{
					foreach (string item7 in (from d in Directory.GetDirectories(path)
						where Path.GetFileName(d).Contains("FrameGen")
						select d).ToList())
					{
						try
						{
							// Directory.Delete(item7, recursive: true);
						}
						catch
						{
						}
					}
					if (Directory.GetDirectories(path).Length == 0 && Directory.GetFiles(path).Length == 0)
					{
						try
						{
							// Directory.Delete(path, recursive: true);
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
			game.FrameGenEnabled = false;
			game.FrameGenMode = "";
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
			string path = Path.Combine(BackupBaseDir, game.Name);
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
					if (File.Exists(item5) && !item5.Contains(BackupBaseDir))
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
					if (hashSet.Contains(fileName2) && dictionary.TryGetValue(fileName2, out var value))
					{
						string text7 = ((!dictionary2.TryGetValue(fileName2, out var value2) || !File.Exists(value2)) ? text6 : value2);
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
					if (flag2 && IsAddedPatchFile(fileName2))
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
					if (Directory.Exists(item6) && !item6.Contains(BackupBaseDir))
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
						if (flag3)
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
			try
			{
				if (Directory.Exists(path))
				{
					foreach (string item7 in (from d in Directory.GetDirectories(path)
						where Path.GetFileName(d).Contains("DLSS5") || Path.GetFileName(d).Contains("DX9DLSS5")
						select d).ToList())
					{
						try
						{
							// Directory.Delete(item7, recursive: true);
						}
						catch
						{
						}
					}
					if (Directory.GetDirectories(path).Length == 0 && Directory.GetFiles(path).Length == 0)
					{
						try
						{
							// Directory.Delete(path, recursive: true);
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
			game.Dlss5Enabled = false;
			game.Dx9Dlss5Enabled = false;
			return true;
		}
		catch
		{
			return false;
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

	private static string? FindSlFilesDir(string gameRoot)
	{
		try
		{
			string[] filesWithDepthLimit = GetFilesWithDepthLimit(gameRoot, "sl.*.dll", 15);
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
			string text = FindSlFilesDir(game.GamePath);
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
			string text = FindSlFilesDir(game.GamePath);
			if (text == null)
			{
				return 0;
			}
			string backupDir = GetBackupDir(game, "Troubleshoot");
			string path = ((!(game.FrameGenMode == "经典模式")) ? GetPatchDir("40系原生多帧生成\\排错") : GetPatchDir("老驱动多帧生成\\排错"));
			int num = 0;
			foreach (string item in filesToReplace)
			{
				string text2 = Path.Combine(path, item);
				string text3 = Path.Combine(text, item);
				if (File.Exists(text2) && File.Exists(text3))
				{
					BackupFile(text3, backupDir);
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
