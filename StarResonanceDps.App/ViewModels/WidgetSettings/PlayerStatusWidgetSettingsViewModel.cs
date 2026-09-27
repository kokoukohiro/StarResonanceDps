using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// ステータス詳細の表示設定、行ごとのオン/オフ、行ごとのテキストカラー。
/// 行の一覧の作りは「他人のロールスキル」(<see cref="MeterWidgetSettingsViewModel"/>)、
/// テキストカラーは被ダメログ(<see cref="TakenDamageLogWidgetSettingsViewModel"/>)と同じ。
/// </summary>
public sealed partial class PlayerStatusWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<PlayerStatusRowItemViewModel> _rows = [];
    private readonly ObservableCollection<PlayerStatusTextColorItemViewModel> _textColors = [];
    private readonly Dictionary<string, PlayerStatusTextColorItemViewModel> _textColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private PlayerStatusWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private bool _hideInactiveStatusEffects = WidgetConfigDefaults.DefaultHideInactiveStatusEffects;

    public PlayerStatusWidgetSettingsViewModel(PlayerStatusWidgetSettingsConfig? config)
    {
        var attrIds = PlayerStatusEntry.SettingRowAttrIds;
        for (var index = 0; index < attrIds.Count; index++)
        {
            _rows.Add(new PlayerStatusRowItemViewModel(
                attrIds[index],
                index == attrIds.Count - 1,
                OnRowVisibilityChanged));
        }

        Rows = new ReadOnlyObservableCollection<PlayerStatusRowItemViewModel>(_rows);

        for (var index = 0; index < attrIds.Count; index++)
        {
            var attrId = attrIds[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultPlayerStatusTextColors(attrId),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += TextColors_PaletteChanged;

            var item = new PlayerStatusTextColorItemViewModel(attrId, colors, index == attrIds.Count - 1);
            _textColorItemsByKey.Add(item.Key, item);
            _textColors.Add(item);
        }

        TextColors = new ReadOnlyObservableCollection<PlayerStatusTextColorItemViewModel>(_textColors);

        Load(WidgetConfigDefaults.CloneNormalizedPlayerStatus(config));
        _lastSaved = CreateConfig();

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<PlayerStatusWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<PlayerStatusRowItemViewModel> Rows { get; }

    public ReadOnlyObservableCollection<PlayerStatusTextColorItemViewModel> TextColors { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString("Settings_Section_Display_Title");

    public string RowSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_PlayerStatusRows_Title");

    public string TextColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_PlayerStatusTextColors_Title");

    /// <summary>スイッチの右に出す状態の文言。作りはウィンドウの表示設定と同じ。</summary>
    public string HideInactiveStatusEffectsStateText => LocalizationManager.Instance.GetString(
        HideInactiveStatusEffects ? "Settings_Switch_On" : "Settings_Switch_Off");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in _textColors)
        {
            item.Colors.PaletteChanged -= TextColors_PaletteChanged;
        }
    }

    public PlayerStatusWidgetSettingsConfig CreateConfig()
    {
        var config = new PlayerStatusWidgetSettingsConfig
        {
            HideInactiveStatusEffects = HideInactiveStatusEffects,
            RowVisibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var row in _rows)
        {
            config.RowVisibility[row.Key] = row.IsVisible;
        }

        foreach (var item in _textColors)
        {
            config.TextColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.TextColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return config;
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreatePlayerStatusSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(PlayerStatusWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedPlayerStatus(config);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>色選択ウィンドウの初期値。宛先は行番号。</summary>
    public Color GetSelectedTextColor(string key)
    {
        return _textColorItemsByKey.TryGetValue(key, out var item)
            ? item.Colors.SelectedColor
            : Colors.White;
    }

    public void ApplyTextColor(string key, Color color)
    {
        if (_textColorItemsByKey.TryGetValue(key, out var item))
        {
            item.Colors.AddOrSelect(color);
        }
    }



    partial void OnHideInactiveStatusEffectsChanged(bool value)
    {
        OnPropertyChanged(nameof(HideInactiveStatusEffectsStateText));
        NotifyChanged();
    }

    private void Load(PlayerStatusWidgetSettingsConfig config)
    {
        _isLoading = true;
        try
        {
            HideInactiveStatusEffects = config.HideInactiveStatusEffects
                ?? WidgetConfigDefaults.DefaultHideInactiveStatusEffects;

            foreach (var row in _rows)
            {
                row.LoadVisibility(
                    config.RowVisibility is not null
                    && config.RowVisibility.TryGetValue(row.Key, out var visible)
                        ? visible
                        : PlayerStatusEntry.IsRowVisibleByDefault(row.AttrId));
            }

            foreach (var item in _textColors)
            {
                var palette = config.TextColorPalettes is not null
                    && config.TextColorPalettes.TryGetValue(item.Key, out var saved)
                    && saved is { Count: > 0 }
                        ? saved
                        : WidgetConfigDefaults.CreateDefaultPlayerStatusTextColors(item.AttrId);
                var index = config.TextColorIndexes is not null
                    && config.TextColorIndexes.TryGetValue(item.Key, out var savedIndex)
                        ? savedIndex
                        : WidgetConfigDefaults.DefaultPlayerStatusTextColorIndex;

                item.Colors.Load(palette, index);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }




    private void TextColors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    /// <summary>スイッチを触った瞬間に呼ぶ。プレビュー反映と「未保存あり」の判定を更新する。</summary>
    private void OnRowVisibilityChanged()
    {
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var row in _rows)
        {
            row.RefreshMetadata();
        }

        foreach (var item in _textColors)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(RowSectionTitle));
        OnPropertyChanged(nameof(TextColorSectionTitle));
        OnPropertyChanged(nameof(HideInactiveStatusEffectsStateText));
    }

    private static bool SettingsEqual(
        PlayerStatusWidgetSettingsConfig left,
        PlayerStatusWidgetSettingsConfig right)
    {
        if (left.HideInactiveStatusEffects != right.HideInactiveStatusEffects)
        {
            return false;
        }

        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            var key = attrId.ToString(CultureInfo.InvariantCulture);
            if (left.RowVisibility is null
                || right.RowVisibility is null
                || !left.RowVisibility.TryGetValue(key, out var leftVisible)
                || !right.RowVisibility.TryGetValue(key, out var rightVisible)
                || leftVisible != rightVisible)
            {
                return false;
            }
        }

        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            var key = attrId.ToString(CultureInfo.InvariantCulture);
            if (left.TextColorIndexes is null
                || right.TextColorIndexes is null
                || left.TextColorPalettes is null
                || right.TextColorPalettes is null
                || !left.TextColorIndexes.TryGetValue(key, out var leftIndex)
                || !right.TextColorIndexes.TryGetValue(key, out var rightIndex)
                || leftIndex != rightIndex
                || !left.TextColorPalettes.TryGetValue(key, out var leftPalette)
                || !right.TextColorPalettes.TryGetValue(key, out var rightPalette)
                || !leftPalette.SequenceEqual(rightPalette, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
