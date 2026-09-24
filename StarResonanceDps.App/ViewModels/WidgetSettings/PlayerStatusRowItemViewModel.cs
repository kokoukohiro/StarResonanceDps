using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 「ステータス」設定の1行。左にアイコン、中央に項目名、右にスイッチ。
/// 作りは <see cref="OtherRoleSkillItemViewModel"/> と同じ。
/// </summary>
public sealed partial class PlayerStatusRowItemViewModel : ObservableObject
{
    private readonly Action _onVisibilityChanged;
    private bool _isLoading;

    public PlayerStatusRowItemViewModel(int attrId, bool isLast, Action onVisibilityChanged)
    {
        AttrId = attrId;
        IsLast = isLast;
        Key = attrId.ToString(CultureInfo.InvariantCulture);
        _onVisibilityChanged = onVisibilityChanged;
        RefreshMetadata();
    }

    public int AttrId { get; }

    /// <summary>最終行。区切り線を消して二重にならないようにする。</summary>
    public bool IsLast { get; }

    public string Key { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private Brush? _iconMask;

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>スイッチの右に出す ON / OFF。</summary>
    public string StateText => LocalizationManager.Instance.GetString(
        IsVisible ? "Settings_Switch_On" : "Settings_Switch_Off");

    public void RefreshMetadata()
    {
        DisplayName = LocalizationManager.Instance.GetString(PlayerStatusEntry.GetRowNameKey(AttrId));
        IconMask = PlayerStatusEntry.GetRowIconMask(AttrId);
        OnPropertyChanged(nameof(StateText));
    }

    /// <summary>読み込み時はプレビュー反映を鳴らさずに値だけ入れる。</summary>
    public void LoadVisibility(bool isVisible)
    {
        _isLoading = true;
        try
        {
            IsVisible = isVisible;
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnIsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(StateText));
        if (_isLoading)
        {
            return;
        }

        _onVisibilityChanged();
    }
}
