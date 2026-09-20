using System;
using System.Management;
using System.Linq;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    public class GpuInfo
    {
        public string Name { get; set; } = "";
        public string Series { get; set; } = ""; // RTX20, RTX30, RTX40, RTX50, AMD, Intel, Other
        public bool IsNvidia { get; set; }
        public bool IsAmd { get; set; }
        public bool IsIntegrated { get; set; }
        public bool SupportsDLSS5 { get; set; }
        public bool SupportsFrameGen { get; set; }
    }

    public class GpuDetector
    {
        public static GpuInfo DetectPrimaryGpu()
        {
            // 如果用户手动选择了显卡，优先用用户选择的
            var selected = SettingsService.Instance.SelectedGpuName;
            if (!string.IsNullOrEmpty(selected))
            {
                var gpu = GetAllGpus().FirstOrDefault(g => g.Name == selected);
                if (gpu != null) return gpu;
            }

            var gpus = GetAllGpus();
            // 优先选择NVIDIA独立显卡（排除核显和虚拟显卡）
            var nvidiaGpu = gpus.FirstOrDefault(g => g.IsNvidia && !g.IsIntegrated && 
                !g.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase));
            if (nvidiaGpu != null) return nvidiaGpu;
            
            // 其次选择AMD独立显卡
            var amdGpu = gpus.FirstOrDefault(g => g.IsAmd && !g.IsIntegrated && 
                !g.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase));
            if (amdGpu != null) return amdGpu;
            
            // 再次选择任意非核显
            var discrete = gpus.FirstOrDefault(g => !g.IsIntegrated && 
                !g.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase));
            return discrete ?? gpus.FirstOrDefault() ?? new GpuInfo { Name = "未知显卡", Series = "Other" };
        }

        public static GpuInfo[] GetAllGpus()
        {
            var result = new System.Collections.Generic.List<GpuInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "";
                    var gpu = new GpuInfo { Name = name };

                    if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    {
                        gpu.IsNvidia = true;
                        gpu.Series = DetectNvidiaSeries(name);
                        gpu.SupportsDLSS5 = gpu.Series is "RTX20" or "RTX30" or "RTX40" or "RTX50";
                        gpu.SupportsFrameGen = gpu.Series is "RTX20" or "RTX30" or "RTX40" or "RTX50";
                    }
                    else if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    {
                        gpu.IsAmd = true;
                        gpu.Series = "AMD";
                        gpu.SupportsDLSS5 = IsAmdSupported(name);
                        gpu.SupportsFrameGen = false;
                    }
                    else if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                    {
                        gpu.Series = "Intel";
                        gpu.SupportsDLSS5 = false;
                        gpu.SupportsFrameGen = false;
                    }

                    // 判断是否核显
                    gpu.IsIntegrated = name.Contains("HD Graphics", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("UHD Graphics", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Iris", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Radeon Graphics", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Radeon(TM)", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Vega", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Standard VGA", StringComparison.OrdinalIgnoreCase);

                    result.Add(gpu);
                }
            }
            catch { }
            return result.ToArray();
        }

        private static string DetectNvidiaSeries(string name)
        {
            if (name.Contains("RTX 50") || name.Contains("RTX50")) return "RTX50";
            if (name.Contains("RTX 40") || name.Contains("RTX40")) return "RTX40";
            if (name.Contains("RTX 30") || name.Contains("RTX30")) return "RTX30";
            if (name.Contains("RTX 20") || name.Contains("RTX20")) return "RTX20";
            if (name.Contains("GTX 16") || name.Contains("GTX16")) return "GTX16";
            if (name.Contains("GTX 10") || name.Contains("GTX10")) return "GTX10";
            return "OtherNvidia";
        }

        private static bool IsAmdSupported(string name)
        {
            // 仅支持7000系和9000系
            return name.Contains("RX 7900") || name.Contains("RX 7800") || name.Contains("RX 7700") ||
                   name.Contains("RX 7600") || name.Contains("RX 9070") || name.Contains("RX 9060") ||
                   name.Contains("9000") || name.Contains("7000");
        }
    }
}
