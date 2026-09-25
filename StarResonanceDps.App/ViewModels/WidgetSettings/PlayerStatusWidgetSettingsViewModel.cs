using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    /// <summary>行を足せるか。上限に達していたら押せない。</summary>
    public bool CanAddTextColorRow => _textColors.Count < WidgetConfigDefaults.MaxPlayerStatusTextColorRows;

    /// <summary>最後の行を消せるか。下限に達していたら押せない。</summary>
    public bool CanRemoveTextColorRow => _textColors.Count > WidgetConfigDefaults.MinPlayerStatusTextColorRows;

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
            TextColors = []
        };

        foreach (var row in _rows)
        {
            config.RowVisibility[row.Key] = row.IsVisible;
        }

        foreach (var item in _textColors)
        {
            config.TextColors.Add(new PlayerStatusTextColorConfig
            {
                SelectedIndex = item.Colors.SelectedIndex,
                Palette = [.. item.Colors.GetHexColors()]
            });
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
        return FindTextColorItem(key)?.Colors.SelectedColor ?? Colors.White;
    }

    public void ApplyTextColor(string key, Color color)
    {
        FindTextColorItem(key)?.Colors.AddOrSelect(color);
    }

    /// <summary>次の行を足す。色は純白1色から始める。</summary>
    [RelayCommand]
    private void AddTextColorRow()
    {
        if (!CanAddTextColorRow)
        {
            return;
        }

        AppendTextColorRow(WidgetConfigDefaults.CreatePlayerStatusTextColorRow());
        RaiseTextColorRowCountChanged();
        NotifyChanged();
    }

    /// <summary>最後の行を消す。</summary>
    [RelayCommand]
    private void RemoveTextColorRow()
    {
        if (!CanRemoveTextColorRow)
        {
            return;
        }

        var item = _textColors[^1];
        item.Colors.PaletteChanged -= TextColors_PaletteChanged;
        _textColors.RemoveAt(_textColors.Count - 1);

        RaiseTextColorRowCountChanged();
        NotifyChanged();
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

            // 行数が変わるので、作り直して入れ直す。
            foreach (var item in _textColors)
            {
                item.Colors.PaletteChanged -= TextColors_PaletteChanged;
            }

            _textColors.Clear();
            foreach (var row in config.TextColors ?? WidgetConfigDefaults.CreateDefaultPlayerStatusTextColors())
            {
                AppendTextColorRow(row);
            }
        }
        finally
        {
            _isLoading = false;
        }

        RaiseTextColorRowCountChanged();
    }

    private void AppendTextColorRow(PlayerStatusTextColorConfig row)
    {
        var colors = new ColorPaletteViewModel(row.Palette, WidgetConfigDefaults.MaxPaletteColorCount);
        colors.Load(row.Palette, row.SelectedIndex);
        colors.PaletteChanged += TextColors_PaletteChanged;
        _textColors.Add(new PlayerStatusTextColorItemViewModel(_textColors.Count + 1, colors));
    }

    private PlayerStatusTextColorItemViewModel? FindTextColorItem(string key)
    {
        foreach (var item in _textColors)
        {
            if (string.Equals(item.Key, key, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private void RaiseTextColorRowCountChanged()
    {
        OnPropertyChanged(nameof(CanAddTextColorRow));
        OnPropertyChanged(nameof(CanRemoveTextColorRow));
        AddTextColorRowCommand.NotifyCanExecuteChanged();
        RemoveTextColorRowCommand.NotifyCanExecuteChanged();
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

        var leftColors = left.TextColors;
        var rightColors = right.TextColors;
        if (leftColors is null || rightColors is null || leftColors.Count != rightColors.Count)
        {
            return false;
        }

        for (var index = 0; index < leftColors.Count; index++)
        {
            if (leftColors[index].SelectedIndex != rightColors[index].SelectedIndex
                || !leftColors[index].Palette.SequenceEqual(rightColors[index].Palette, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
