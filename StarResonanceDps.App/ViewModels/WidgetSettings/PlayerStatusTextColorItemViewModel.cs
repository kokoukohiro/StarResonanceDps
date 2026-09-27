using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// ステータス詳細のテキストカラーの1行。作りはプレイヤーリストのクラスカラー
/// (<see cref="MeterClassColorItemViewModel"/>)と同じで、左のアイコンを<b>選んだ色で塗る</b>。
/// 項目と並びは「ステータス」の一覧(<c>PlayerStatusEntry.SettingRowAttrIds</c>)と同じ。
/// </summary>
public sealed class PlayerStatusTextColorItemViewModel : ObservableObject
{
    public PlayerStatusTextColorItemViewModel(int attrId, ColorPaletteViewModel colors, bool isLast)
    {
        AttrId = attrId;
        Colors = colors;
        IsLast = isLast;
        Key = attrId.ToString(CultureInfo.InvariantCulture);
        IconMask = PlayerStatusEntry.GetRowIconMask(attrId);
    }

    public int AttrId { get; }

    public ColorPaletteViewModel Colors { get; }

    /// <summary>最終行。区切り線を消して二重にならないようにする。</summary>
    public bool IsLast { get; }

    public string Key { get; }

    /// <summary>行のアイコンの形。選んだ色をこの形で抜く(クラスアイコンと同じ染め方)。</summary>
    public Brush? IconMask { get; }

    public string DisplayName => LocalizationManager.Instance.GetString(PlayerStatusEntry.GetRowNameKey(AttrId));

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
