using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフ・デバフ一覧の表示設定。行のゲージの色(左端と右端)と、満タンになる残り時間、一覧に出さないバフ。
/// 色の行の作りはクラスカラーと同じ。
/// </summary>
public sealed partial class BuffListWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, BuffListGaugeColorItemViewModel> _gaugeColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<HiddenBuffItemViewModel> _hiddenBuffItems = [];
    private readonly WidgetKind _kind;
    private BuffListWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _gaugeLengthIndex;

    /// <summary>ゲージの色の不透明度(0〜100)。UI がスライダーなので double で持つ(メーターのクラスカラーと同じ)。</summary>
    [ObservableProperty]
    private double _gaugeColorOpacity = WidgetConfigDefaults.MaxClassColorOpacity;

    public BuffListWidgetSettingsViewModel(WidgetKind kind, BuffListWidgetSettingsConfig? config)
    {
        _kind = kind;

        var items = new ObservableCollection<BuffListGaugeColorItemViewModel>();
        var keys = WidgetConfigDefaults.BuffListGaugeColorKeys;
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultGaugeColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += GaugeColors_PaletteChanged;

            // 節の最後の行は不透明度なので、色の行はどれも下の線を引く。
            var item = new BuffListGaugeColorItemViewModel(key, colors, isLast: false);
            _gaugeColorItemsByKey.Add(key, item);
            items.Add(item);
        }

        GaugeColorItems = new ReadOnlyObservableCollection<BuffListGaugeColorItemViewModel>(items);
        HiddenBuffItems = new ReadOnlyObservableCollection<HiddenBuffItemViewModel>(_hiddenBuffItems);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffList(kind, config);
        Load(_lastSaved);
    }

    public event Action<BuffListWidgetSettingsConfig>? PreviewChanged;

    /// <summary>ゲージの色の行。左端と右端の2行。</summary>
    public ReadOnlyObservableCollection<BuffListGaugeColorItemViewModel> GaugeColorItems { get; }

    /// <summary>節の見出し。バフ一覧なら「バフカラー」、デバフ一覧なら「デバフカラー」。</summary>
    public string GaugeColorSectionTitle => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.DebuffList
            ? "Settings_Section_DebuffListColors_Title"
            : "Settings_Section_BuffListColors_Title");

    /// <summary>不透明度の行名。見出しと同じく種別で「バフカラー」「デバフカラー」に分かれる。</summary>
    public string GaugeColorOpacityLabel => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.DebuffList
            ? "Settings_DebuffListColors_Opacity"
            : "Settings_BuffListColors_Opacity");

    /// <summary>非表示のバフの行。並びは非表示にした順。</summary>
    public ReadOnlyObservableCollection<HiddenBuffItemViewModel> HiddenBuffItems { get; }

    /// <summary>非表示のバフが1つでもあるか。無いときは枠ごと出さない。</summary>
    public bool HasHiddenBuffs => _hiddenBuffItems.Count > 0;

    /// <summary>節の見出し(「非表示リスト」)。今は種別で同じ文言だが、鍵はバフ一覧とデバフ一覧で分けてある。</summary>
    public string HiddenBuffSectionTitle => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.DebuffList
            ? "Settings_Section_HiddenDebuffs_Title"
            : "Settings_Section_HiddenBuffs_Title");

    /// <summary>見出しの下の説明。右クリックで非表示にできることを書く。</summary>
    public string HiddenBuffSectionDescription => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.DebuffList
            ? "Settings_Section_HiddenDebuffs_Description"
            : "Settings_Section_HiddenBuffs_Description");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in GaugeColorItems)
        {
            item.Colors.PaletteChanged -= GaugeColors_PaletteChanged;
        }
    }

    public BuffListWidgetSettingsConfig CreateConfig()
    {
        var config = new BuffListWidgetSettingsConfig
        {
            GaugeLengthIndex = GaugeLengthIndex,
            GaugeColorOpacity = Math.Clamp(
                (int)Math.Round(GaugeColorOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorOpacity,
                WidgetConfigDefaults.MaxClassColorOpacity),
            GaugeColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            GaugeColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            HiddenBuffIds = [.. _hiddenBuffItems.Select(item => item.BaseId)]
        };

        foreach (var item in GaugeColorItems)
        {
            config.GaugeColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.GaugeColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedBuffList(_kind, config);
    }

    public Color GetSelectedGaugeColor(string key)
    {
        return _gaugeColorItemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyGaugeColor(string key, Color color)
    {
        _gaugeColorItemsByKey[key].Colors.AddOrSelect(color);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateBuffListSettings(_kind));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(BuffListWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffList(_kind, config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、一覧の窓の右クリックでバフが非表示になった。その分は既に保存されているので、
    /// 行と保存済みの値の両方に足す(「保存」で消えず、未保存の印も付かない)。プレビューは鳴らさない
    /// (ウィジェット側は既に非表示にしている)。
    /// </summary>
    public void AddHiddenBuffFromWidget(int baseId)
    {
        if (baseId <= 0)
        {
            return;
        }

        if (!_lastSaved.HiddenBuffIds.Contains(baseId))
        {
            _lastSaved.HiddenBuffIds.Add(baseId);
        }

        if (_hiddenBuffItems.All(item => item.BaseId != baseId))
        {
            _hiddenBuffItems.Add(new HiddenBuffItemViewModel(baseId, RemoveHiddenBuff));
            OnHiddenBuffItemsChanged();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(BuffListWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedBuffList(_kind, config);

        _isLoading = true;
        try
        {
            foreach (var item in GaugeColorItems)
            {
                item.Colors.Load(normalized.GaugeColorPalettes[item.Key], normalized.GaugeColorIndexes[item.Key]);
            }

            GaugeLengthIndex = normalized.GaugeLengthIndex;
            GaugeColorOpacity = normalized.GaugeColorOpacity;

            _hiddenBuffItems.Clear();
            foreach (var baseId in normalized.HiddenBuffIds)
            {
                _hiddenBuffItems.Add(new HiddenBuffItemViewModel(baseId, RemoveHiddenBuff));
            }

            OnHiddenBuffItemsChanged();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RemoveHiddenBuff(HiddenBuffItemViewModel item)
    {
        if (!_hiddenBuffItems.Remove(item))
        {
            return;
        }

        OnHiddenBuffItemsChanged();
        NotifyChanged();
    }

    /// <summary>行が増減したら、最終行の印と枠の有無を当て直す。</summary>
    private void OnHiddenBuffItemsChanged()
    {
        for (var index = 0; index < _hiddenBuffItems.Count; index++)
        {
            _hiddenBuffItems[index].IsLast = index == _hiddenBuffItems.Count - 1;
        }

        OnPropertyChanged(nameof(HasHiddenBuffs));
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private static bool SettingsEqual(BuffListWidgetSettingsConfig left, BuffListWidgetSettingsConfig right)
    {
        if (left.GaugeLengthIndex != right.GaugeLengthIndex
            || left.GaugeColorOpacity != right.GaugeColorOpacity
            || !left.HiddenBuffIds.SequenceEqual(right.HiddenBuffIds))
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.BuffListGaugeColorKeys)
        {
            if (left.GaugeColorIndexes[key] != right.GaugeColorIndexes[key]
                || !left.GaugeColorPalettes[key].SequenceEqual(right.GaugeColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void GaugeColors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in GaugeColorItems)
        {
            item.RefreshDisplayName();
        }

        foreach (var item in _hiddenBuffItems)
        {
            item.RefreshMetadata();
        }

        OnPropertyChanged(nameof(GaugeColorSectionTitle));
        OnPropertyChanged(nameof(GaugeColorOpacityLabel));
        OnPropertyChanged(nameof(HiddenBuffSectionTitle));
        OnPropertyChanged(nameof(HiddenBuffSectionDescription));
    }

    partial void OnGaugeLengthIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnGaugeColorOpacityChanged(double value)
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
}

/// <summary>ゲージの色の1行。作りはクラスカラーの行と同じ。</summary>
public sealed class BuffListGaugeColorItemViewModel : ObservableObject
{
    public BuffListGaugeColorItemViewModel(string key, ColorPaletteViewModel colors, bool isLast)
    {
        Key = key;
        Colors = colors;
        IsLast = isLast;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public bool IsLast { get; }

    public string DisplayName => LocalizationManager.Instance.GetString($"Settings_BuffListGauge_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
