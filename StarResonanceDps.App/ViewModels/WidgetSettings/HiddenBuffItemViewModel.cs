using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 「非表示リスト」設定の1行。左にアイコンと名前、右に削除ボタン。
/// 名前とアイコンは一覧の行と同じ引き方。
/// </summary>
public sealed partial class HiddenBuffItemViewModel : ObservableObject
{
    private readonly Action<HiddenBuffItemViewModel> _onRemove;

    public HiddenBuffItemViewModel(int baseId, Action<HiddenBuffItemViewModel> onRemove)
    {
        BaseId = baseId;
        _onRemove = onRemove;
        RefreshMetadata();
    }

    public int BaseId { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    /// <summary>絵が Icons.xaml の絵(装備効果)なら型抜き。一覧の行と同じ引き方。</summary>
    [ObservableProperty]
    private ImageBrush? _iconMask;

    /// <summary>絵がモジュール効果の絵か。一覧の行と同じく、絵の大きさを切り替える。</summary>
    [ObservableProperty]
    private bool _isModuleEffectIcon;

    /// <summary>絵が因子の絵か。一覧の行と同じく、絵の位置を切り替える。</summary>
    [ObservableProperty]
    private bool _isFactorIcon;

    /// <summary>絵のパスはあるのにファイルが見つからないか。一覧の行と同じく、はてなを出す。パスが空なら何も出さない。</summary>
    [ObservableProperty]
    private bool _showsUnknownIcon;

    /// <summary>最終行。区切り線を消して二重にならないようにする。行が増減すると変わる。</summary>
    [ObservableProperty]
    private bool _isLast;

    public void RefreshMetadata()
    {
        // 一覧と同じ名前と絵の引き方(付与元の由来は分からないので無し)。名前と絵は番号ごと(表と手修正)。
        // 番号で名前の引けないバフは名前が空になる。
        DisplayName = CombatDataCatalog.GetBuffDisplayName(BaseId, SourceLanding.None);
        var iconName = CombatDataCatalog.GetBuffOwnIconName(BaseId);
        IconPath = CombatIconResolver.ResolveBuffIcon(iconName);
        IconMask = CombatIconResolver.ResolveBuffIconMask(iconName);
        IsModuleEffectIcon = CombatDataCatalog.IsModEffectIcon(iconName);
        IsFactorIcon = CombatIconResolver.IsFactorIcon(iconName);
        ShowsUnknownIcon = !string.IsNullOrWhiteSpace(iconName) && IconPath is null && IconMask is null;
    }

    [RelayCommand]
    private void Remove()
    {
        _onRemove(this);
    }
}
