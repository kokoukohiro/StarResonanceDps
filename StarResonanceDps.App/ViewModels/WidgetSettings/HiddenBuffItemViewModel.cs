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

    /// <summary>最終行。区切り線を消して二重にならないようにする。行が増減すると変わる。</summary>
    [ObservableProperty]
    private bool _isLast;

    public void RefreshMetadata()
    {
        DisplayName = CombatDataCatalog.GetBuffName(BaseId);
        IconPath = CombatIconResolver.ResolveBuffIcon(CombatDataCatalog.GetBuffOwnIconName(BaseId));
    }

    [RelayCommand]
    private void Remove()
    {
        _onRemove(this);
    }
}
