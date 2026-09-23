using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// スキル詳細の属性カラー。作りはメーターのクラスカラーと同じで、行が9属性になる。
///
/// <para>
/// ウィジェットの行のバーは、その行に出た属性の割合でこの色を混ぜたものを使う。
/// フィルターと不透明度が掛かるのもバーだけで、<b>ここの色見本は素のまま</b>。
/// </para>
/// </summary>
public sealed partial class ElementColorWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, ElementColorItemViewModel> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly WidgetKind _kind;
    private ElementColorWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private double _colorOpacity = WidgetConfigDefaults.MaxClassColorOpacity;

    [ObservableProperty]
    private bool _filterEnabled;

    [ObservableProperty]
    private double _filterStrength = WidgetConfigDefaults.DefaultClassColorFilterStrength;

    public ElementColorWidgetSettingsViewModel(WidgetKind kind, ElementColorWidgetSettingsConfig? config)
    {
        _kind = kind;

        var items = new ObservableCollection<ElementColorItemViewModel>();
        foreach (var key in WidgetConfigDefaults.DamagePropertyKeys)
        {
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultElementColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new ElementColorItemViewModel(key, colors);
            _itemsByKey.Add(key, item);
            items.Add(item);
        }

        Items = new ReadOnlyObservableCollection<ElementColorItemViewModel>(items);

        FilterColors = new ColorPaletteViewModel(
            WidgetConfigDefaults.CreateDefaultElementFilterColors(kind),
            WidgetConfigDefaults.MaxPaletteColorCount);
        FilterColors.PaletteChanged += Colors_PaletteChanged;

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedElementColor(_kind, config);
        Load(_lastSaved);
    }

    public event Action<ElementColorWidgetSettingsConfig>? PreviewChanged;

    /// <summary>属性の行。9件で、並びは属性ID順。</summary>
    public ReadOnlyObservableCollection<ElementColorItemViewModel> Items { get; }

    /// <summary>フィルター色のパレット。クラスカラーと同じ枠を使う。</summary>
    public ColorPaletteViewModel FilterColors { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString("Settings_Section_ElementColors_Title");

    /// <summary>フィルターの色と強さを出すか。スイッチがオフのときは隠す。</summary>
    public bool ShowsFilterOptions => FilterEnabled;

    /// <summary>スイッチの右に出す ON / OFF。</summary>
    public string FilterStateText => LocalizationManager.Instance.GetString(
        FilterEnabled ? "Settings_Switch_On" : "Settings_Switch_Off");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in Items)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }

        FilterColors.PaletteChanged -= Colors_PaletteChanged;
    }

    public ElementColorWidgetSettingsConfig CreateConfig()
    {
        var config = new ElementColorWidgetSettingsConfig
        {
            ColorOpacity = Math.Clamp(
                (int)Math.Round(ColorOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorOpacity,
                WidgetConfigDefaults.MaxClassColorOpacity),
            FilterEnabled = FilterEnabled,
            FilterColors = [.. FilterColors.GetHexColors()],
            FilterColorIndex = FilterColors.SelectedIndex,
            FilterStrength = Math.Clamp(
                (int)Math.Round(FilterStrength, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorFilterStrength,
                WidgetConfigDefaults.MaxClassColorFilterStrength),
            ColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in Items)
        {
            config.ColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        WidgetConfigDefaults.NormalizeElementColor(_kind, config);
        return config;
    }

    public Color GetSelectedColor(string key)
    {
        return _itemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyColor(string key, Color color)
    {
        _itemsByKey[key].Colors.AddOrSelect(color);
    }

    public Color GetSelectedFilterColor()
    {
        return FilterColors.SelectedColor;
    }

    public void ApplyFilterColor(Color color)
    {
        FilterColors.AddOrSelect(color);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateElementColorSettings(_kind));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(ElementColorWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedElementColor(_kind, config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(ElementColorWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedElementColor(_kind, config);

        _isLoading = true;
        try
        {
            foreach (var item in Items)
            {
                item.Colors.Load(normalized.ColorPalettes[item.Key], normalized.ColorIndexes[item.Key]);
            }

            ColorOpacity = normalized.ColorOpacity;
            FilterColors.Load(normalized.FilterColors, normalized.FilterColorIndex);
            FilterEnabled = normalized.FilterEnabled ?? false;
            FilterStrength = normalized.FilterStrength;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private static bool SettingsEqual(ElementColorWidgetSettingsConfig left, ElementColorWidgetSettingsConfig right)
    {
        if (left.ColorOpacity != right.ColorOpacity
            || left.FilterEnabled != right.FilterEnabled
            || left.FilterColorIndex != right.FilterColorIndex
            || left.FilterStrength != right.FilterStrength)
        {
            return false;
        }

        var leftFilterColors = left.FilterColors ?? [];
        var rightFilterColors = right.FilterColors ?? [];
        if (!leftFilterColors.SequenceEqual(rightFilterColors, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.DamagePropertyKeys)
        {
            if (left.ColorIndexes[key] != right.ColorIndexes[key]
                || !left.ColorPalettes[key].SequenceEqual(right.ColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(FilterStateText));
    }

    partial void OnColorOpacityChanged(double value)
    {
        NotifyChanged();
    }

    partial void OnFilterEnabledChanged(bool value)
    {
        // オフの間は「フィルターカラー」「フィルターの強さ」の行ごと消す。
        OnPropertyChanged(nameof(ShowsFilterOptions));
        OnPropertyChanged(nameof(FilterStateText));
        NotifyChanged();
    }

    partial void OnFilterStrengthChanged(double value)
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

/// <summary>属性カラーの1行。作りはメーターのクラスカラーの行と同じで、アイコンは白で染めて影を付ける。</summary>
public sealed class ElementColorItemViewModel : ObservableObject
{
    public ElementColorItemViewModel(string key, ColorPaletteViewModel colors)
    {
        Key = key;
        Colors = colors;

        var mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.DamageProperty.{key}"))
        {
            Stretch = Stretch.Uniform
        };
        mask.Freeze();
        IconMask = mask;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    /// <summary>属性アイコンの形。塗りをこの形で抜く。</summary>
    public Brush IconMask { get; }

    /// <summary>行名は属性名。その属性だけで出来た行の色、という意味。</summary>
    public string DisplayName => LocalizationManager.Instance.GetString($"DamageProperty_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
