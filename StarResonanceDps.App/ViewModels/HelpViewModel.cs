using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>ヘルプタブ。アプリの版と、プロジェクトのページ(<see cref="ProjectLinks"/>)を開く命令。</summary>
public sealed partial class HelpViewModel : ObservableObject
{
    /// <summary>アプリの版。実行ファイルの版(csprojのVersion)の先頭3つの数字。</summary>
    private readonly string _version = (typeof(HelpViewModel).Assembly.GetName().Version
        ?? throw new InvalidOperationException("The application assembly has no version.")).ToString(3);

    /// <summary>上の欄の版の行(「バージョン 4.0.0」の形)。言語を切り替えたら<see cref="RefreshTexts"/>で入れ直す。</summary>
    [ObservableProperty]
    private string _versionText = string.Empty;

    public HelpViewModel()
    {
        RefreshTexts();
    }

    public void RefreshTexts()
    {
        VersionText = LocalizationManager.Instance.Format("Help_Version", _version);
    }

    [RelayCommand]
    private void OpenLink(string url)
    {
        ExternalLinkOpener.Open(url);
    }
}
