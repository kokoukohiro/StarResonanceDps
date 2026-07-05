using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

public sealed partial class MeterWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly WidgetKind _kind;
    private readonly Dictionary<string, MeterClassColorItemViewModel> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private MeterWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private double _classColorOpacity = WidgetConfigDefaults.MaxClassColorOpacity;

    public MeterWidgetSettingsViewModel(WidgetKind kind, MeterWidgetSettingsConfig? config)
    {
        _kind = kind;
        var items = new ObservableCollection<MeterClassColorItemViewModel>();
        foreach (var key in WidgetConfigDefaults.ClassColorKeys)
        {
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(_kind, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors);
            _itemsByKey.Add(key, item);
            items.Add(item);
        }

        Items = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(items);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedMeter(_kind, config);
        Load(_lastSaved);
    }

    public event Action<MeterWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> Items { get; }

    public bool UsesMeterClassColorIconBackground => WidgetConfigDefaults.UsesMeterClassColorOpacity(_kind);

    public bool ShowsPlayerListProfessionIcons => !UsesMeterClassColorIconBackground;

    public bool HasClassColorOpacity => UsesMeterClassColorIconBackground;

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in Items)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    public MeterWidgetSettingsConfig CreateConfig()
    {
        var config = new MeterWidgetSettingsConfig
        {
            ClassColorOpacity = Math.Clamp(
                (int)Math.Round(ClassColorOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorOpacity,
                WidgetConfigDefaults.MaxClassColorOpacity),
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in Items)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        WidgetConfigDefaults.NormalizeMeter(_kind, config);
        return config;
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateMeterSettings(_kind));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(MeterWidgetSettingsConfig config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMeter(_kind, config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public Color GetSelectedClassColor(string key)
    {
        return GetItem(key).Colors.SelectedColor;
    }

    public void ApplyClassColor(string key, Color color)
    {
        GetItem(key).Colors.AddOrSelect(color);
    }

    private void Load(MeterWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedMeter(_kind, config);

        _isLoading = true;
        try
        {
            foreach (var item in Items)
            {
                var colors = normalized.ClassColorPalettes.TryGetValue(item.Key, out var palette)
                    ? palette
                    : WidgetConfigDefaults.CreateDefaultClassColors(_kind, item.Key);
                var selectedIndex = normalized.ClassColorIndexes.TryGetValue(item.Key, out var index)
                    ? index
                    : WidgetConfigDefaults.MinClassColorIndex;
                item.Colors.Load(colors, selectedIndex);
            }

            ClassColorOpacity = normalized.ClassColorOpacity;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private MeterClassColorItemViewModel GetItem(string key)
    {
        return _itemsByKey.TryGetValue(key, out var item)
            ? item
            : _itemsByKey["Unknown"];
    }

    private static bool SettingsEqual(MeterWidgetSettingsConfig left, MeterWidgetSettingsConfig right)
    {
        if (left.ClassColorOpacity != right.ClassColorOpacity)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.ClassColorKeys)
        {
            if (!left.ClassColorIndexes.TryGetValue(key, out var leftIndex)
                || !right.ClassColorIndexes.TryGetValue(key, out var rightIndex)
                || leftIndex != rightIndex)
            {
                return false;
            }

            if (!left.ClassColorPalettes.TryGetValue(key, out var leftColors)
                || !right.ClassColorPalettes.TryGetValue(key, out var rightColors)
                || !leftColors.SequenceEqual(rightColors, StringComparer.OrdinalIgnoreCase))
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

    partial void OnClassColorOpacityChanged(double value)
    {
        NotifyChanged();
    }
}

public sealed class MeterClassColorItemViewModel : ObservableObject
{
    public MeterClassColorItemViewModel(string key, ColorPaletteViewModel colors)
    {
        Key = key;
        Colors = colors;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public string DisplayName => LocalizationManager.Instance.GetString($"Classes_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
