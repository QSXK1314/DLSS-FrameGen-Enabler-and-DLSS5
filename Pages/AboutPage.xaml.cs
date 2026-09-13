using DLSSFrameGenEnabler_WinUI3.Services;
using Microsoft.UI.Xaml.Controls;

namespace DLSSFrameGenEnabler_WinUI3.Pages
{
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();
            Translator.LanguageChanged += UpdateLanguageTexts;
            UpdateLanguageTexts();
        }

        private void UpdateLanguageTexts()
        {
            bool en = Translator.IsEnglish;
            TitleText.Text = Translator.T("About_Title");
            AppNameText.Text = en ? "Frame Gen + DLSS5 Enabler" : "多帧生成+DLSS5开启工具";
            VersionText.Text = en ? "Version: V1.13.0.0" : "版本：V1.13.0.0";
            DevLangText.Text = Translator.T("About_DevLang");
            OpenSourceText.Text = en ? "This software is open source and free. Welcome to Star and Fork!" : "本软件已免费开源，欢迎Star和Fork！";
            AuthorLinksText.Text = en ? "Author Links" : "作者链接";
            GithubProfileText.Text = en ? "GitHub Profile" : "GitHub 个人主页";
            GithubRepoText.Text = en ? "GitHub Repository" : "GitHub 项目仓库";
            BilibiliText.Text = Translator.T("About_Bilibili");
            XiaoheiheText.Text = Translator.T("About_Xiaoheihe");
            ThanksText.Text = Translator.T("About_Thanks");
            ThanksIntroText.Text = en ? "The following community projects made this software possible:" : "以下社区项目使本软件成为可能：";
            
            // 更新致谢列表
            Thanks1.Text = en ? "• NVIDIA DLSS / DLSS-G / DLSS-NR (NVIDIA Official)" : "• NVIDIA DLSS / DLSS-G / DLSS-NR（NVIDIA官方）";
            Thanks2.Text = en ? "• Universal RTX 40 MFG Unlock (Community)" : "• Universal RTX 40 MFG Unlock（社区版）";
            Thanks3.Text = en ? "• ReShade (Crosire)" : "• ReShade（Crosire）";
            Thanks4.Text = en ? "• RenoDX DLSS5 Addon (Community)" : "• RenoDX DLSS5 Addon（社区版）";
            Thanks5.Text = en ? "• DLSS5 Feeder (Community)" : "• DLSS5 Feeder（社区版）";
            Thanks6.Text = en ? "• dgVoodoo2 (Dege)" : "• dgVoodoo2（Dege）";
            Thanks7.Text = en ? "• LumeniteFX (Community)" : "• LumeniteFX（社区版）";
            Thanks8.Text = en ? "• RE Framework / DLSS5 on RE (Community)" : "• RE Framework / DLSS5 on RE（社区版）";
            Thanks9.Text = en ? "• Cyber Engine Tweaks (Cyberpunk 2077)" : "• Cyber Engine Tweaks（赛博朋克2077）";
            Thanks10.Text = en ? "• RTX 20/30 Series Frame Gen Patch (Community)" : "• RTX 20/30系多帧生成补丁（社区版）";
            
            DisclaimerTitleText.Text = en ? "Disclaimer" : "免责声明";
            DisclaimerContentText.Text = en ?
                "This software is for learning and communication only, not for commercial use. Not recommended for online games or games with anti-cheat. This software modifies game files, which may result in account bans. We are not responsible for any bans." :
                "本软件仅供学习交流使用，请勿用于商业用途。不建议在网游上和带反作弊的游戏使用该软件，本质是修改文件，而修改文件则可能导致游戏封号，如果遇到游戏封号，本软件概不负责。";
        }
    }
}
