using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DLSSFrameGenEnabler_WinUI3.Models;

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

	private string _remark = "";

	private string _tags = "";

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			_name = value;
			OnPropertyChanged("Name");
		}
	}

	public string GamePath
	{
		get
		{
			return _gamePath;
		}
		set
		{
			_gamePath = value;
			OnPropertyChanged("GamePath");
		}
	}

	public string ExePath
	{
		get
		{
			return _exePath;
		}
		set
		{
			_exePath = value;
			OnPropertyChanged("ExePath");
		}
	}

	public string Status
	{
		get
		{
			if (Is2077 && FrameGenEnabled)
			{
				if (!Dlss5Enabled)
				{
					return Translator.T("Status_2077");
				}
				return Translator.T("Status_All");
			}
			if (IsREEngine && FrameGenEnabled && Dlss5Enabled)
			{
				return Translator.T("Status_Dlss5");
			}
			if (FrameGenEnabled && Dlss5Enabled)
			{
				return Translator.T("Status_All");
			}
			if (FrameGenEnabled)
			{
				return Translator.T("Status_FrameGen");
			}
			if (Dlss5Enabled)
			{
				return Translator.T("Status_Dlss5");
			}
			if (Dx9Dlss5Enabled)
			{
				if (!Translator.IsEnglish)
				{
					return "已开启（DX9 DLSS5）";
				}
				return "Enabled (DX9 DLSS5)";
			}
			return Translator.T("Status_NotEnabled");
		}
	}

	public bool IsREEngine
	{
		get
		{
			return _isREEngine;
		}
		set
		{
			_isREEngine = value;
			OnPropertyChanged("IsREEngine");
		}
	}

	public bool IsDX9
	{
		get
		{
			return _isDX9;
		}
		set
		{
			_isDX9 = value;
			OnPropertyChanged("IsDX9");
		}
	}

	public bool SupportsDX11
	{
		get
		{
			return _supportsDX11;
		}
		set
		{
			_supportsDX11 = value;
			OnPropertyChanged("SupportsDX11");
		}
	}

	public bool SupportsFrameGen
	{
		get
		{
			return _supportsFrameGen;
		}
		set
		{
			_supportsFrameGen = value;
			OnPropertyChanged("SupportsFrameGen");
		}
	}

	public bool Is2077
	{
		get
		{
			return _is2077;
		}
		set
		{
			_is2077 = value;
			OnPropertyChanged("Is2077");
			OnPropertyChanged("Status");
		}
	}

	public bool IsYanYun
	{
		get
		{
			return _isYanYun;
		}
		set
		{
			_isYanYun = value;
			OnPropertyChanged("IsYanYun");
		}
	}

	public bool FrameGenEnabled
	{
		get
		{
			return _frameGenEnabled;
		}
		set
		{
			_frameGenEnabled = value;
			OnPropertyChanged("FrameGenEnabled");
			OnPropertyChanged("Status");
		}
	}

	public bool Dlss5Enabled
	{
		get
		{
			return _dlss5Enabled;
		}
		set
		{
			_dlss5Enabled = value;
			OnPropertyChanged("Dlss5Enabled");
			OnPropertyChanged("Status");
		}
	}

	public bool Dx9Dlss5Enabled
	{
		get
		{
			return _dx9Dlss5Enabled;
		}
		set
		{
			_dx9Dlss5Enabled = value;
			OnPropertyChanged("Dx9Dlss5Enabled");
			OnPropertyChanged("Status");
		}
	}

	public string FrameGenMode
	{
		get
		{
			return _frameGenMode;
		}
		set
		{
			_frameGenMode = value;
			OnPropertyChanged("FrameGenMode");
		}
	}

	public string GpuSeries
	{
		get
		{
			return _gpuSeries;
		}
		set
		{
			_gpuSeries = value;
			OnPropertyChanged("GpuSeries");
		}
	}

	[JsonIgnore]
	public ImageSource? Icon
	{
		get
		{
			return _icon;
		}
		set
		{
			_icon = value;
			OnPropertyChanged("Icon");
		}
	}

	public string Remark
	{
		get
		{
			return _remark;
		}
		set
		{
			_remark = value;
			OnPropertyChanged("Remark");
			OnPropertyChanged("RemarkTagsDisplay");
			OnPropertyChanged("HasRemarkTags");
		}
	}

	public string Tags
	{
		get
		{
			return _tags;
		}
		set
		{
			_tags = value;
			OnPropertyChanged("Tags");
			OnPropertyChanged("RemarkTagsDisplay");
			OnPropertyChanged("HasRemarkTags");
		}
	}

	[JsonIgnore]
	public string RemarkTagsDisplay
	{
		get
		{
			List<string> list = new List<string>();
			if (!string.IsNullOrWhiteSpace(Remark))
			{
				list.Add("\ud83d\udcdd " + Remark);
			}
			if (!string.IsNullOrWhiteSpace(Tags))
			{
				list.Add("\ud83c\udff7\ufe0f " + Tags);
			}
			return string.Join("  |  ", list);
		}
	}

	[JsonIgnore]
	public Visibility HasRemarkTags
	{
		get
		{
			if (string.IsNullOrWhiteSpace(Remark) && string.IsNullOrWhiteSpace(Tags))
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public void RefreshStatus()
	{
		OnPropertyChanged("Status");
	}

	protected void OnPropertyChanged([CallerMemberName] string? name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
