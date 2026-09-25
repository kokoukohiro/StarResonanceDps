using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// ステータス詳細のテキストカラーの1行。作りは被ダメログのテキストカラー
/// (<see cref="TakenDamageLogTextColorItemViewModel"/>)と同じで、<b>アイコンは持たない</b>。
///
/// <para>
/// 行数が可変なので、鍵は固定の名前ではなく行番号。<see cref="RowNumber"/> は 1 始まりで、
/// 行を足したり消したりすると <c>PlayerStatusWidgetSettingsViewModel</c> が振り直す。
/// </para>
/// </summary>
public sealed partial class PlayerStatusTextColorItemViewModel : ObservableObject
{
    public PlayerStatusTextColorItemViewModel(int rowNumber, ColorPaletteViewModel colors)
    {
        RowNumber = rowNumber;
        Colors = colors;
    }

    public ColorPaletteViewModel Colors { get; }

    /// <summary>1 始まりの行番号。色の選択肢のグループ名と、色選択ウィンドウの宛先を兼ねる。</summary>
    [ObservableProperty]
    private int _rowNumber;

    public string Key => RowNumber.ToString(CultureInfo.InvariantCulture);

    public string DisplayName => string.Format(
        CultureInfo.CurrentCulture,
        LocalizationManager.Instance.GetString("Settings_PlayerStatus_TextColorRow_Value"),
        RowNumber);

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnRowNumberChanged(int value)
    {
        OnPropertyChanged(nameof(Key));
        OnPropertyChanged(nameof(DisplayName));
    }
}
