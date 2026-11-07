using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>プラグインの一覧の1行。行は作り直さず、言語と状態が変わったときに値だけ入れ替える。</summary>
public sealed partial class PluginListItemViewModel : ViewModelBase
{
    private PluginStatus? _status;

    public required PluginInfo Info { get; init; }

    /// <summary>一覧と窓の鍵(<see cref="PluginInfo.Key"/>)。</summary>
    public string PluginKey => Info.Key;

    public int OriginalIndex { get; set; }

    /// <summary>行と窓のアイコン。プラグインに絵が無い・読めなければ null(どちらもアプリのアイコンを出す)。</summary>
    public ImageSource? Icon { get; init; }

    /// <summary>2段目(分類・本体に足すもの・制作者)を出すか。情報を読めなかったプラグイン(読み込みの失敗・削除できなかったフォルダ)は出さない。</summary>
    public bool HasDetails => Info.Version is not null;

    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>
    /// 2段目の分類。窓を開くプラグインはジャンル名、窓が無く本体に何かを足すプラグインは出さない(空。拡張の名前だけ出す)、どちらでもなければ「その他」。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCategory))]
    private string _categoryText = string.Empty;

    /// <summary>2段目の「ウィジェット拡張」(ウィジェットを足すプラグインだけ出す)。</summary>
    [ObservableProperty]
    private string _widgetExtensionText = string.Empty;

    /// <summary>2段目の「通知拡張」(通知の項目を足すプラグインだけ出す)。</summary>
    [ObservableProperty]
    private string _notificationExtensionText = string.Empty;

    public bool HasCategory => CategoryText.Length > 0;

    [ObservableProperty]
    private string _authorText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    private PluginRowState _rowState;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    private bool _canUninstall;

    /// <summary>「エラー」のピルの TIPS(理由)。エラーでなければ空。</summary>
    [ObservableProperty]
    private string _statusToolTip = string.Empty;

    public void RefreshLocalizedText()
    {
        var localization = LocalizationManager.Instance;
        var culture = localization.CurrentCulture;
        DisplayName = Info.GetDisplayName(culture);
        if (HasDetails)
        {
            CategoryText = Info.HasWindow
                ? Info.GetGenre(culture) ?? string.Empty
                : Info.HasExtensions
                    ? string.Empty
                    : localization.GetString("Plugins_GenreOther");
            WidgetExtensionText = Info.HasWidgets ? localization.GetString("Plugins_WidgetExtension") : string.Empty;
            NotificationExtensionText = Info.HasNotifications ? localization.GetString("Plugins_NotificationExtension") : string.Empty;
            AuthorText = localization.Format("Plugins_Author", Info.Author ?? string.Empty);
        }

        if (_status is not null)
        {
            StatusToolTip = FormatStatusToolTip(_status);
        }
    }

    /// <summary>行の状態を <see cref="PluginManager"/> から読み直す(作ったときと、状態が変わった知らせのとき)。</summary>
    public void RefreshStatus()
    {
        _status = PluginManager.Instance.GetStatus(PluginKey);
        RowState = _status.RowState;
        CanUninstall = _status.CanUninstall;
        StatusToolTip = FormatStatusToolTip(_status);
    }

    private static string FormatStatusToolTip(PluginStatus status)
    {
        var localization = LocalizationManager.Instance;
        return status.ErrorReason switch
        {
            null => string.Empty,
            PluginErrorReason.MissingFiles => localization.Format("PluginStatus_MissingFiles", string.Join(", ", status.MissingFiles)),
            PluginErrorReason.Invalid => localization.GetString("PluginStatus_Invalid"),
            PluginErrorReason.Stopped => localization.GetString("PluginStatus_Stopped"),
            PluginErrorReason.InvalidNotifications => localization.GetString("PluginStatus_InvalidNotifications"),
            PluginErrorReason.InvalidWidgets => localization.GetString("PluginStatus_InvalidWidgets"),
            PluginErrorReason.PacketBacklog => localization.GetString("PluginStatus_PacketBacklog"),
            PluginErrorReason.TerminatedApp => localization.GetString("PluginStatus_TerminatedApp"),
            PluginErrorReason.DeletionFailed => localization.GetString("PluginStatus_DeletionFailed"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status.ErrorReason, "Unknown plugin error reason."),
        };
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open()
    {
        PluginManager.Instance.Open(this);
    }

    private bool CanOpen()
    {
        return RowState == PluginRowState.Openable;
    }

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private void Uninstall()
    {
        PluginManager.Instance.Uninstall(PluginKey);
    }
}
