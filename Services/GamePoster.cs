using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DLSSFrameGenEnabler_WinUI3.Models;
using Microsoft.Win32;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    /// <summary>
    /// 读取本地游戏库的竖版封面：
    /// Steam 优先 appcache/librarycache/&lt;appid&gt;/library_600x900*.jpg；
    /// Epic 读取 manifest 中的 DieselGameBoxTall 等 KeyImage；
    /// 都没有时返回空字符串，由前端用 exe 图标做兜底海报。
    /// 输出为 9:16、360x640 的 JPEG data URL：完整封面居中显示，空白区域用同图模糊背景补满，避免裁切。
    /// </summary>
    public static class GamePoster
    {
        private static readonly Dictionary<string, string> PosterCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheLock = new object();
        private static readonly List<SteamAppEntry> SteamApps = new List<SteamAppEntry>();
        private static readonly HashSet<string> LoadedSteamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class SteamAppEntry
        {
            public string AppId = "";
            public string InstallDir = "";
            public string Name = "";
            public string LibraryRoot = "";
        }

        private sealed class EpicEntry
        {
            public string InstallLocation = "";
            public string PosterUrl = "";
        }

        public static string GetPoster(GameInfo game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.ExePath))
            {
                return "";
            }

            string key = game.GamePath + "|" + game.ExePath;
            lock (CacheLock)
            {
                if (PosterCache.TryGetValue(key, out string? cached))
                {
                    return cached;
                }
            }

            string poster = "";
            try
            {
                poster = FindSteamPoster(game);
            }
            catch
            {
            }
            if (string.IsNullOrEmpty(poster))
            {
                try
                {
                    poster = FindEpicPoster(game);
                }
                catch
                {
                }
            }

            lock (CacheLock)
            {
                PosterCache[key] = poster;
            }
            return poster;
        }

        private static string FindSteamPoster(GameInfo game)
        {
            string? installDir = GetSteamInstallDir(game.GamePath) ?? GetSteamInstallDir(game.ExePath);
            if (string.IsNullOrEmpty(installDir))
            {
                return "";
            }

            string? libraryRoot = GetSteamLibraryRoot(game.GamePath) ?? GetSteamLibraryRoot(game.ExePath);
            if (!string.IsNullOrEmpty(libraryRoot))
            {
                EnsureSteamLibrary(libraryRoot);
            }

            List<SteamAppEntry> candidates;
            lock (CacheLock)
            {
                candidates = SteamApps
                    .Where(a => string.Equals(a.InstallDir, installDir, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                return "";
            }

            SteamAppEntry best = candidates
                .OrderByDescending(a => NameScore(a.Name, game.Name))
                .ThenByDescending(a => HasLocalPortrait(a))
                .First();

            string local = FindLocalPortrait(best);
            if (!string.IsNullOrEmpty(local))
            {
                return ToPosterDataUrl(local);
            }

            // 本地 librarycache 没有这张竖图时，用 Steam 官方竖版封面兜底。
            if (best.AppId.Length > 0 && best.AppId.All(char.IsDigit))
            {
                return "https://steamcdn-a.akamaihd.net/steam/apps/" + best.AppId + "/library_600x900_2x.jpg";
            }
            return "";
        }

        private static int NameScore(string candidate, string gameName)
        {
            string a = NormalizeName(candidate);
            string b = NormalizeName(gameName);
            if (a.Length == 0 || b.Length == 0)
            {
                return 0;
            }
            if (a == b)
            {
                return 1000;
            }
            if (a.Contains(b) || b.Contains(a))
            {
                return 600;
            }

            int common = 0;
            int max = Math.Min(a.Length, b.Length);
            while (common < max && a[common] == b[common])
            {
                common++;
            }
            return common * 10;
        }

        private static string NormalizeName(string value)
        {
            return Regex.Replace((value ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+", "");
        }

        private static bool HasLocalPortrait(SteamAppEntry app)
        {
            return !string.IsNullOrEmpty(FindLocalPortrait(app));
        }

        private static string FindLocalPortrait(SteamAppEntry app)
        {
            foreach (string cacheRoot in GetSteamCacheRoots(app))
            {
                string appDir = Path.Combine(cacheRoot, app.AppId);
                if (!Directory.Exists(appDir))
                {
                    continue;
                }

                List<string> portraits = new List<string>();
                try
                {
                    portraits.AddRange(Directory.GetFiles(appDir, "library_600x900*.*", SearchOption.AllDirectories));
                }
                catch
                {
                }

                string? portrait = portraits
                    .Where(IsSupportedImage)
                    .OrderByDescending(ScorePortraitFile)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(portrait))
                {
                    return portrait;
                }

                // 很多游戏没有 library_600x900，但有 2:3 的 library_capsule，效果同样接近竖版封面。
                List<string> capsules = new List<string>();
                try
                {
                    capsules.AddRange(Directory.GetFiles(appDir, "library_capsule*.*", SearchOption.AllDirectories));
                }
                catch
                {
                }
                string? capsule = capsules
                    .Where(IsSupportedImage)
                    .OrderByDescending(ScoreCapsuleFile)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(capsule))
                {
                    return capsule;
                }

                // 少数游戏只有 header，先拿来做 9:16 中心裁切，至少不是空图。
                List<string> headers = new List<string>();
                try
                {
                    headers.AddRange(Directory.GetFiles(appDir, "header*.*", SearchOption.TopDirectoryOnly));
                }
                catch
                {
                }
                string? header = headers
                    .Where(IsSupportedImage)
                    .OrderByDescending(ScoreHeaderFile)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(header))
                {
                    return header;
                }
            }
            return "";
        }

        private static bool IsSupportedImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
        }

        private static int ScorePortraitFile(string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            int score = 0;
            if (name.Contains("_2x"))
            {
                score += 100;
            }
            if (name.StartsWith("library_600x900"))
            {
                score += 40;
            }
            if (name.Contains("schinese") || name.Contains("english"))
            {
                score += 5;
            }
            if (ext == ".jpg" || ext == ".jpeg")
            {
                score += 10;
            }
            else if (ext == ".png")
            {
                score += 6;
            }
            return score;
        }

        private static int ScoreHeaderFile(string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            int score = 0;
            if (name.Contains("schinese") || name.Contains("english"))
            {
                score += 5;
            }
            if (ext == ".jpg" || ext == ".jpeg")
            {
                score += 4;
            }
            return score;
        }

        private static int ScoreCapsuleFile(string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            int score = 0;
            if (name.Contains("_2x"))
            {
                score += 100;
            }
            if (name.StartsWith("library_capsule"))
            {
                score += 40;
            }
            if (name.Contains("schinese") || name.Contains("english"))
            {
                score += 5;
            }
            if (ext == ".jpg" || ext == ".jpeg")
            {
                score += 10;
            }
            else if (ext == ".png")
            {
                score += 6;
            }
            return score;
        }

        private static List<string> GetSteamCacheRoots(SteamAppEntry app)
        {
            List<string> roots = new List<string>();
            void AddRoot(string libraryRoot)
            {
                if (string.IsNullOrWhiteSpace(libraryRoot))
                {
                    return;
                }
                string[] candidates =
                {
                    Path.Combine(libraryRoot, "appcache", "librarycache"),
                    Path.Combine(libraryRoot, "steamapps", "librarycache"),
                    Path.Combine(libraryRoot, "steamapps", "appcache", "librarycache")
                };
                foreach (string candidate in candidates)
                {
                    if (Directory.Exists(candidate) && !roots.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    {
                        roots.Add(candidate);
                    }
                }
            }

            AddRoot(app.LibraryRoot);
            foreach (string root in GetSteamLibraryRoots())
            {
                AddRoot(root);
            }
            return roots;
        }

        private static string GetSteamInstallDir(string path)
        {
            const string marker = @"\steamapps\common\";
            if (string.IsNullOrWhiteSpace(path))
            {
                return "";
            }
            int index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return "";
            }
            string tail = path.Substring(index + marker.Length);
            int slash = tail.IndexOf('\\');
            return slash >= 0 ? tail.Substring(0, slash) : tail;
        }

        private static string GetSteamLibraryRoot(string path)
        {
            const string marker = @"\steamapps\common\";
            if (string.IsNullOrWhiteSpace(path))
            {
                return "";
            }
            int index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return index > 0 ? path.Substring(0, index) : "";
        }

        private static List<string> GetSteamLibraryRoots()
        {
            List<string> roots = new List<string>();
            void Add(string? root)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    return;
                }
                string normalized = root.TrimEnd('\\', '/');
                if (Directory.Exists(normalized) && !roots.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    roots.Add(normalized);
                }
            }

            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                if (key != null)
                {
                    Add(key.GetValue("InstallPath") as string);
                }
            }
            catch
            {
            }
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (key != null)
                {
                    Add(key.GetValue("InstallPath") as string);
                }
            }
            catch
            {
            }
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    Add(key.GetValue("SteamPath") as string);
                }
            }
            catch
            {
            }

            string[] defaults =
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam",
                @"D:\Steam",
                @"D:\SteamLibrary",
                @"E:\Steam",
                @"E:\SteamLibrary",
                @"F:\Steam",
                @"F:\SteamLibrary",
                @"G:\Steam",
                @"G:\SteamLibrary"
            };
            foreach (string path in defaults)
            {
                Add(path);
            }

            foreach (string root in roots.ToList())
            {
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf))
                {
                    continue;
                }
                try
                {
                    foreach (string line in File.ReadAllLines(vdf))
                    {
                        string trimmed = line.Trim();
                        if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        string[] parts = trimmed.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            Add(parts[1].Trim('"').Replace("\\\\", "\\"));
                        }
                    }
                }
                catch
                {
                }
            }
            return roots;
        }

        private static void EnsureSteamLibrary(string libraryRoot)
        {
            if (string.IsNullOrWhiteSpace(libraryRoot))
            {
                return;
            }
            lock (CacheLock)
            {
                if (!LoadedSteamRoots.Add(libraryRoot))
                {
                    return;
                }
            }

            string steamApps = Path.Combine(libraryRoot, "steamapps");
            if (!Directory.Exists(steamApps))
            {
                return;
            }

            List<SteamAppEntry> parsed = new List<SteamAppEntry>();
            try
            {
                foreach (string manifest in Directory.GetFiles(steamApps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string appId = Regex.Match(Path.GetFileNameWithoutExtension(manifest), @"appmanifest_(\d+)", RegexOptions.IgnoreCase).Groups[1].Value;
                        if (appId.Length == 0)
                        {
                            continue;
                        }
                        string text = File.ReadAllText(manifest);
                        string installDir = Regex.Match(text, "\"installdir\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                        string name = Regex.Match(text, "\"name\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                        if (installDir.Length == 0)
                        {
                            continue;
                        }
                        parsed.Add(new SteamAppEntry
                        {
                            AppId = appId,
                            InstallDir = installDir.Replace("\\\\", "\\"),
                            Name = name.Replace("\\\\", "\\"),
                            LibraryRoot = libraryRoot
                        });
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            lock (CacheLock)
            {
                foreach (SteamAppEntry entry in parsed)
                {
                    if (!SteamApps.Any(a => a.AppId == entry.AppId && string.Equals(a.LibraryRoot, entry.LibraryRoot, StringComparison.OrdinalIgnoreCase)))
                    {
                        SteamApps.Add(entry);
                    }
                }
            }
        }

        private static string FindEpicPoster(GameInfo game)
        {
            string manifestsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifestsDir))
            {
                return "";
            }

            string gamePath = NormalizePath(game.GamePath);
            foreach (string manifest in Directory.GetFiles(manifestsDir, "*.item", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    EpicEntry entry = ReadEpicEntry(manifest);
                    if (entry.PosterUrl.Length == 0 || entry.InstallLocation.Length == 0)
                    {
                        continue;
                    }
                    string install = NormalizePath(entry.InstallLocation);
                    if (gamePath.Equals(install, StringComparison.OrdinalIgnoreCase)
                        || gamePath.StartsWith(install + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.PosterUrl;
                    }
                }
                catch
                {
                }
            }
            return "";
        }

        private static EpicEntry ReadEpicEntry(string manifest)
        {
            EpicEntry entry = new EpicEntry();
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifest));
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("InstallLocation", out JsonElement install) && install.ValueKind == JsonValueKind.String)
            {
                entry.InstallLocation = install.GetString() ?? "";
            }
            if (!root.TryGetProperty("KeyImages", out JsonElement images) || images.ValueKind != JsonValueKind.Array)
            {
                return entry;
            }

            string[] preferred =
            {
                "DieselGameBoxTall",
                "OfferImageTall",
                "DieselGameBox",
                "Thumbnail"
            };
            for (int i = 0; i < preferred.Length && entry.PosterUrl.Length == 0; i++)
            {
                foreach (JsonElement image in images.EnumerateArray())
                {
                    if (!image.TryGetProperty("type", out JsonElement type) || type.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }
                    if (!string.Equals(type.GetString(), preferred[i], StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (image.TryGetProperty("url", out JsonElement url) && url.ValueKind == JsonValueKind.String)
                    {
                        string value = url.GetString() ?? "";
                        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.PosterUrl = value;
                            break;
                        }
                    }
                }
            }
            return entry;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? "").Replace('/', '\\').TrimEnd('\\');
        }

        private static string ToPosterDataUrl(string path)
        {
            try
            {
                using Image source = Image.FromFile(path);
                if (source.Width < 32 || source.Height < 32)
                {
                    return "";
                }

                const int outWidth = 360;
                const int outHeight = 640;
                double targetRatio = 9.0 / 16.0;
                double sourceRatio = source.Width / (double)source.Height;

                Rectangle coverSrc;
                if (sourceRatio > targetRatio)
                {
                    int cropWidth = (int)Math.Round(source.Height * targetRatio);
                    coverSrc = new Rectangle((source.Width - cropWidth) / 2, 0, cropWidth, source.Height);
                }
                else
                {
                    int cropHeight = (int)Math.Round(source.Width / targetRatio);
                    coverSrc = new Rectangle(0, (source.Height - cropHeight) / 2, source.Width, cropHeight);
                }

                using Bitmap output = new Bitmap(outWidth, outHeight, PixelFormat.Format24bppRgb);
                using (Graphics graphics = Graphics.FromImage(output))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.Clear(Color.FromArgb(18, 20, 24));

                    // 背景：先把原图按 cover 缩到很小的画布，再放大，得到柔和的模糊底。
                    using (Bitmap blurred = new Bitmap(48, 86, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics bg = Graphics.FromImage(blurred))
                        {
                            bg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            bg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            bg.DrawImage(source, new Rectangle(0, 0, blurred.Width, blurred.Height), coverSrc, GraphicsUnit.Pixel);
                        }
                        graphics.DrawImage(blurred, new Rectangle(0, 0, outWidth, outHeight));
                    }
                    graphics.FillRectangle(new SolidBrush(Color.FromArgb(92, 8, 10, 14)), 0, 0, outWidth, outHeight);

                    // 前景：完整封面等比缩放后居中，保留原图全部内容，不再裁边。
                    double scale = Math.Min(340.0 / source.Width, 600.0 / source.Height);
                    int frontWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
                    int frontHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
                    int frontX = (outWidth - frontWidth) / 2;
                    int frontY = (outHeight - frontHeight) / 2;
                    graphics.DrawImage(source, new Rectangle(frontX, frontY, frontWidth, frontHeight), new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
                }

                using MemoryStream stream = new MemoryStream();
                ImageCodecInfo? jpeg = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                EncoderParameters parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, 84L);
                try
                {
                    if (jpeg != null)
                    {
                        output.Save(stream, jpeg, parameters);
                    }
                    else
                    {
                        output.Save(stream, ImageFormat.Jpeg);
                    }
                }
                finally
                {
                    parameters.Dispose();
                }

                return "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray());
            }
            catch
            {
                return "";
            }
        }
    }
}
