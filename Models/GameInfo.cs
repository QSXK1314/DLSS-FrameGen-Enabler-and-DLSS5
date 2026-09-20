using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Xaml.Media;

namespace DLSSFrameGenEnabler_WinUI3.Models
{
    public class GameInfo : INotifyPropertyChanged
    {
        private string _name = "";
        private string _gamePath = "";
        private string _exePath = "";
        private bool _isREEngine;
        private bool _isDX9;
        private bool _supportsDX11;
        private bool _supportsFrameGen;
        private bool _is2077;
        private bool _isYanYun;
        private bool _frameGenEnabled;
        private bool _dlss5Enabled;
        private bool _dx9Dlss5Enabled;
        private string _frameGenMode = "";
        private string _gpuSeries = "";
        private ImageSource? _icon;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string GamePath
        {
            get => _gamePath;
            set { _gamePath = value; OnPropertyChanged(); }
        }

        public string ExePath
        {
            get => _exePath;
            set { _exePath = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get
            {
                if (Is2077 && FrameGenEnabled)
                {
                    return Dlss5Enabled ? Translator.T("Status_All") : Translator.T("Status_2077");
                }
                else if (IsREEngine && FrameGenEnabled && Dlss5Enabled)
                {
                    // RE引擎游戏DLSS5和多帧生成冲突，不可能同时有效
                    // 如果两个都标记为已开启（异常情况），优先显示DLSS5
                    return Translator.T("Status_Dlss5");
                }
                else if (FrameGenEnabled && Dlss5Enabled)
                {
                    return Translator.T("Status_All");
                }
                else if (FrameGenEnabled)
                {
                    return Translator.T("Status_FrameGen");
                }
                else if (Dlss5Enabled)
                {
                    return Translator.T("Status_Dlss5");
                }
                else if (Dx9Dlss5Enabled)
                {
                    return Translator.IsEnglish ? "Enabled (DX9 DLSS5)" : "已开启（DX9 DLSS5）";
                }
                else
                {
                    return Translator.T("Status_NotEnabled");
                }
            }
        }

        public bool IsREEngine
        {
            get => _isREEngine;
            set { _isREEngine = value; OnPropertyChanged(); }
        }

        public bool IsDX9
        {
            get => _isDX9;
            set { _isDX9 = value; OnPropertyChanged(); }
        }

        public bool SupportsDX11
        {
            get => _supportsDX11;
            set { _supportsDX11 = value; OnPropertyChanged(); }
        }

        public bool SupportsFrameGen
        {
            get => _supportsFrameGen;
            set { _supportsFrameGen = value; OnPropertyChanged(); }
        }

        public bool Is2077
        {
            get => _is2077;
            set { _is2077 = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
        }

        public bool IsYanYun
        {
            get => _isYanYun;
            set { _isYanYun = value; OnPropertyChanged(); }
        }

        public bool FrameGenEnabled
        {
            get => _frameGenEnabled;
            set { _frameGenEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
        }

        public bool Dlss5Enabled
        {
            get => _dlss5Enabled;
            set { _dlss5Enabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
        }

        public bool Dx9Dlss5Enabled
        {
            get => _dx9Dlss5Enabled;
            set { _dx9Dlss5Enabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
        }

        public string FrameGenMode
        {
            get => _frameGenMode;
            set { _frameGenMode = value; OnPropertyChanged(); }
        }

        public string GpuSeries
        {
            get => _gpuSeries;
            set { _gpuSeries = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        // 语言变化时刷新所有游戏的状态显示
        public void RefreshStatus()
        {
            OnPropertyChanged(nameof(Status));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
