using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    /// <summary>
    /// 渲染 API 与引擎识别结果。优先读 PE 导入表，不再只靠 exe 位数或目录里是否
    /// 存在 d3d9.dll（ReShade 自己也会放 d3d9.dll，单看文件会误判）。
    /// </summary>
    public sealed class RendererInfo
    {
        public string Api { get; set; } = "未知";
        public bool IsDX9 { get; set; }
        public bool SupportsDX11 { get; set; }
        public bool SupportsDX12 { get; set; }
        public bool SupportsVulkan { get; set; }
        public bool SupportsOpenGL { get; set; }
        public bool IsSourceEngine { get; set; }
        public string InstallDir { get; set; } = "";
        public string Confidence { get; set; } = "Low";
        public List<string> Evidence { get; set; } = new List<string>();
    }

    public static class RendererDetector
    {
        private static readonly string[] SourceExeNames =
        {
            "hl2.exe", "gmod.exe", "csgo.exe", "left4dead.exe", "left4dead2.exe",
            "portal.exe", "portal2.exe", "tf.exe", "hl2_launcher.exe"
        };

        private static readonly string[] SourceGameDirs =
        {
            "hl2", "cstrike", "csgo", "left4dead", "left4dead2",
            "portal", "portal2", "tf", "garrysmod", "dod", "sourcemod"
        };

        public static RendererInfo Detect(string exePath, string? gameRoot = null)
        {
            var info = new RendererInfo();
            string exeDir = Path.GetDirectoryName(exePath) ?? "";
            string root = !string.IsNullOrWhiteSpace(gameRoot) && Directory.Exists(gameRoot)
                ? gameRoot
                : exeDir;
            info.InstallDir = exeDir;

            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return info;
            }

            var modules = new List<string>();
            AddIfExists(modules, exePath);
            AddIfExists(modules, Path.Combine(root, "bin", "engine.dll"));
            AddIfExists(modules, Path.Combine(root, "bin", "materialsystem.dll"));
            AddIfExists(modules, Path.Combine(root, "bin", "shaderapidx9.dll"));
            AddIfExists(modules, Path.Combine(root, "bin", "shaderapidx11.dll"));
            AddIfExists(modules, Path.Combine(root, "bin", "studiorender.dll"));
            AddIfExists(modules, Path.Combine(root, "bin", "launcher.dll"));
            AddIfExists(modules, Path.Combine(root, "bin", "shaderapidx9.dll"));
            AddIfExists(modules, Path.Combine(root, "bin", "shaderapidx11.dll"));
            AddIfExists(modules, Path.Combine(exeDir, "bin", "engine.dll"));
            AddIfExists(modules, Path.Combine(exeDir, "bin", "shaderapidx9.dll"));
            AddIfExists(modules, Path.Combine(exeDir, "bin", "shaderapidx11.dll"));
            AddIfExists(modules, Path.Combine(root, "UnityPlayer.dll"));
            AddIfExists(modules, Path.Combine(root, "Engine", "Binaries", "Win64", "UnityPlayer.dll"));
            AddIfExists(modules, Path.Combine(root, "Binaries", "Win64", "UnityPlayer.dll"));

            // D3DX9 运行库本身会导入 d3d9；部分老游戏 exe 只直接导入 d3dx9_*.dll。
            try
            {
                foreach (string d3dx in Directory.GetFiles(root, "d3dx9_*.dll", SearchOption.TopDirectoryOnly))
                    AddIfExists(modules, d3dx);
                foreach (string d3dx in Directory.GetFiles(exeDir, "d3dx9_*.dll", SearchOption.TopDirectoryOnly))
                    AddIfExists(modules, d3dx);
            }
            catch
            {
            }

            var imports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string module in modules)
            {
                foreach (string name in ReadImportNames(module))
                    imports.Add(name.ToLowerInvariant());
            }

            bool hasBinEngine = File.Exists(Path.Combine(root, "bin", "engine.dll"))
                || File.Exists(Path.Combine(exeDir, "bin", "engine.dll"));
            bool hasBinMaterials = File.Exists(Path.Combine(root, "bin", "materialsystem.dll"))
                || File.Exists(Path.Combine(exeDir, "bin", "materialsystem.dll"));
            bool hasShader9 = File.Exists(Path.Combine(root, "bin", "shaderapidx9.dll"))
                || File.Exists(Path.Combine(exeDir, "bin", "shaderapidx9.dll"));
            bool hasShader11 = File.Exists(Path.Combine(root, "bin", "shaderapidx11.dll"))
                || File.Exists(Path.Combine(exeDir, "bin", "shaderapidx11.dll"));

            string exeName = Path.GetFileName(exePath).ToLowerInvariant();
            bool knownSourceExe = SourceExeNames.Contains(exeName);
            bool sourceGameDir = SourceGameDirs.Any(d => Directory.Exists(Path.Combine(root, d))
                && File.Exists(Path.Combine(root, d, "gameinfo.txt")));
            bool sourceRootGameInfo = File.Exists(Path.Combine(root, "gameinfo.txt"))
                || Directory.Exists(Path.Combine(root, "platform"));
            bool source2 = File.Exists(Path.Combine(root, "game", "bin", "win64", "engine2.dll"))
                || File.Exists(Path.Combine(root, "game", "bin", "win64", "engine2.dll"))
                || exeName == "cs2.exe";

            info.IsSourceEngine = !source2 && hasBinEngine && (hasBinMaterials || hasShader9 || hasShader11
                || knownSourceExe || sourceGameDir || sourceRootGameInfo);
            if (info.IsSourceEngine)
            {
                info.Evidence.Add("bin\\engine.dll");
                if (hasBinMaterials) info.Evidence.Add("bin\\materialsystem.dll");
                if (hasShader9) info.Evidence.Add("bin\\shaderapidx9.dll");
                if (hasShader11) info.Evidence.Add("bin\\shaderapidx11.dll");
                if (knownSourceExe) info.Evidence.Add("Source exe: " + exeName);
                if (sourceGameDir || sourceRootGameInfo) info.Evidence.Add("Source gameinfo/platform");
            }

            bool d3d9 = imports.Contains("d3d9.dll") || hasShader9;
            bool d3d11 = imports.Contains("d3d11.dll") || imports.Contains("dxgi.dll") || hasShader11;
            bool d3d12 = imports.Contains("d3d12.dll");
            bool vulkan = imports.Contains("vulkan-1.dll");
            bool opengl = imports.Contains("opengl32.dll");

            if (d3d9) info.Evidence.Add("import d3d9.dll");
            if (imports.Contains("d3d11.dll")) info.Evidence.Add("import d3d11.dll");
            if (imports.Contains("dxgi.dll")) info.Evidence.Add("import dxgi.dll");
            if (d3d12) info.Evidence.Add("import d3d12.dll");
            if (vulkan) info.Evidence.Add("import vulkan-1.dll");
            if (opengl) info.Evidence.Add("import opengl32.dll");

            info.IsDX9 = d3d9;
            info.SupportsDX11 = d3d11 || d3d12;
            info.SupportsDX12 = d3d12;
            info.SupportsVulkan = vulkan;
            info.SupportsOpenGL = opengl;

            if (d3d12)
            {
                info.Api = "DX12";
                info.Confidence = "High";
            }
            else if (vulkan)
            {
                info.Api = "Vulkan";
                info.Confidence = "High";
            }
            else if (d3d11)
            {
                info.Api = "DX11";
                info.Confidence = "High";
            }
            else if (d3d9)
            {
                info.Api = "DX9";
                info.Confidence = "High";
            }
            else if (opengl)
            {
                info.Api = "OpenGL";
                info.Confidence = "High";
            }
            else
            {
                // 没有读到导入表时的保守回退。只作为兜底，不作为主要依据。
                info.IsDX9 = Is32BitExe(exePath) && !d3d11 && !d3d12 && !vulkan;
                info.SupportsDX11 = !info.IsDX9;
                info.Api = info.IsDX9 ? "DX9（位数兜底）" : "DX11 / DX12（位数兜底）";
                info.Confidence = "Low";
                info.Evidence.Add("PE import table unavailable; fallback to exe architecture");
            }

            // Source 引擎可能同时带 DX9/DX11 渲染器。保留两个标志，界面显示为 DX9 / DX11。
            if (info.IsSourceEngine && hasShader9 && hasShader11)
            {
                info.IsDX9 = true;
                info.SupportsDX11 = true;
                info.Api = "DX9 / DX11";
                info.Confidence = "High";
            }

            if (info.IsSourceEngine)
            {
                string binDir = Path.Combine(root, "bin");
                if (Directory.Exists(binDir))
                    info.InstallDir = binDir;
                else
                {
                    string exeBin = Path.Combine(exeDir, "bin");
                    info.InstallDir = Directory.Exists(exeBin) ? exeBin : exeDir;
                }
            }
            else
            {
                info.InstallDir = exeDir;
            }

            return info;
        }

        private static void AddIfExists(List<string> modules, string path)
        {
            if (File.Exists(path) && !modules.Contains(path, StringComparer.OrdinalIgnoreCase))
                modules.Add(path);
        }

        /// <summary>
        /// 读取 PE 的普通导入表和延迟导入表，只取 DLL 名称。
        /// </summary>
        private static IEnumerable<string> ReadImportNames(string path)
        {
            var names = new List<string>();
            try
            {
                using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using BinaryReader reader = new BinaryReader(fs);
                if (fs.Length < 0x40) return names;

                fs.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                if (peOffset <= 0 || peOffset + 0x80 > fs.Length) return names;
                fs.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550) return names; // PE\0\0

                fs.Position = peOffset + 4;
                reader.ReadUInt16(); // machine
                ushort sectionCount = reader.ReadUInt16();
                fs.Position = peOffset + 20;
                ushort optionalSize = reader.ReadUInt16();
                long optionalStart = peOffset + 24;
                fs.Position = optionalStart;
                ushort magic = reader.ReadUInt16();
                long dataDirStart = optionalStart + (magic == 0x20B ? 112 : 96);

                uint importRva = ReadUInt32At(fs, dataDirStart + 8);
                uint delayRva = ReadUInt32At(fs, dataDirStart + 13 * 8);
                long sectionStart = optionalStart + optionalSize;

                var sections = new List<(uint Va, uint Size, uint Pointer)>();
                for (int i = 0; i < sectionCount; i++)
                {
                    long off = sectionStart + i * 40L;
                    if (off + 40 > fs.Length) break;
                    uint virtualSize = ReadUInt32At(fs, off + 8);
                    uint virtualAddress = ReadUInt32At(fs, off + 12);
                    uint rawSize = ReadUInt32At(fs, off + 16);
                    uint rawPointer = ReadUInt32At(fs, off + 20);
                    sections.Add((virtualAddress, Math.Max(virtualSize, rawSize), rawPointer));
                }

                if (importRva != 0 && importRva < 0x80000000)
                {
                    long importOff = RvaToOffset(importRva, sections);
                    if (importOff > 0 && importOff < fs.Length)
                    {
                        for (int i = 0; i < 1024; i++)
                        {
                            long entry = importOff + i * 20L;
                            if (entry + 20 > fs.Length) break;
                            uint originalFirstThunk = ReadUInt32At(fs, entry);
                            uint nameRva = ReadUInt32At(fs, entry + 12);
                            uint firstThunk = ReadUInt32At(fs, entry + 16);
                            if (originalFirstThunk == 0 && nameRva == 0 && firstThunk == 0) break;
                            AddDllName(names, nameRva, fs, sections);
                        }
                    }
                }

                if (delayRva != 0 && delayRva < 0x80000000)
                {
                    long delayOff = RvaToOffset(delayRva, sections);
                    if (delayOff > 0 && delayOff < fs.Length)
                    {
                        for (int i = 0; i < 512; i++)
                        {
                            long entry = delayOff + i * 32L;
                            if (entry + 32 > fs.Length) break;
                            uint attributes = ReadUInt32At(fs, entry);
                            uint nameRva = ReadUInt32At(fs, entry + 4);
                            uint moduleHandle = ReadUInt32At(fs, entry + 8);
                            if (attributes == 0 && nameRva == 0 && moduleHandle == 0) break;
                            AddDllName(names, nameRva, fs, sections);
                        }
                    }
                }
            }
            catch
            {
                // 读不到导入表时由调用方回退到位数判断。
            }
            return names;
        }

        private static void AddDllName(List<string> names, uint nameRva, FileStream fs, List<(uint Va, uint Size, uint Pointer)> sections)
        {
            if (nameRva == 0 || nameRva >= 0x80000000) return;
            long offset = RvaToOffset(nameRva, sections);
            if (offset <= 0 || offset >= fs.Length) return;
            fs.Position = offset;
            var bytes = new List<byte>();
            for (int i = 0; i < 260; i++)
            {
                if (fs.Position >= fs.Length) break;
                byte b = (byte)fs.ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
            if (bytes.Count == 0) return;
            string name = System.Text.Encoding.ASCII.GetString(bytes.ToArray()).Trim().ToLowerInvariant();
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !names.Contains(name))
                names.Add(name);
        }

        private static long RvaToOffset(uint rva, List<(uint Va, uint Size, uint Pointer)> sections)
        {
            foreach (var section in sections)
            {
                if (rva >= section.Va && rva < section.Va + section.Size)
                    return section.Pointer + (rva - section.Va);
            }
            return -1;
        }

        private static uint ReadUInt32At(FileStream fs, long offset)
        {
            if (offset < 0 || offset + 4 > fs.Length) return 0;
            fs.Position = offset;
            return (uint)fs.ReadByte()
                | ((uint)fs.ReadByte() << 8)
                | ((uint)fs.ReadByte() << 16)
                | ((uint)fs.ReadByte() << 24);
        }

        private static bool Is32BitExe(string exePath)
        {
            try
            {
                using FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using BinaryReader reader = new BinaryReader(fs);
                if (fs.Length < 0x40) return false;
                fs.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                if (peOffset <= 0 || peOffset + 6 > fs.Length) return false;
                fs.Position = peOffset + 4;
                ushort machine = reader.ReadUInt16();
                return machine == 0x14C; // i386
            }
            catch
            {
                return false;
            }
        }
    }
}
