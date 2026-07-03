using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class ClassColorSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, ClassColorItemViewModel> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoading;

    public ClassColorSettingsViewModel(ClassColorSettingsConfig? settings)
    {
        var items = new ObservableCollection<ClassColorItemViewModel>();
        foreach (var key in AppConfigDefaults.ClassColorKeys)
        {
            var palette = new ColorPaletteViewModel(
                AppConfigDefaults.CreateDefaultClassColors(key),
                AppConfigDefaults.MaxPaletteColorCount);
            palette.PaletteChanged += Palette_PaletteChanged;

            var item = new ClassColorItemViewModel(key, palette);
            _itemsByKey.Add(key, item);
            items.Add(item);
        }

        Items = new ReadOnlyObservableCollection<ClassColorItemViewModel>(items);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        Load(settings);
    }

    public event EventHandler? SettingsChanged;

    public ReadOnlyObservableCollection<ClassColorItemViewModel> Items { get; }

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in Items)
        {
            item.Colors.PaletteChanged -= Palette_PaletteChanged;
        }
    }

    public ClassColorSettingsConfig CreateConfig()
    {
        var settings = new ClassColorSettingsConfig
        {
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in Items)
        {
            settings.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            settings.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        AppConfigDefaults.NormalizeClassColorSettings(settings);
        return settings;
    }

    public void Load(ClassColorSettingsConfig? settings)
    {
        var normalized = AppConfigDefaults.CloneNormalizedClassColorSettings(settings);

        _isLoading = true;
        try
        {
            foreach (var item in Items)
            {
                var colors = normalized.ClassColorPalettes.TryGetValue(item.Key, out var palette)
                    ? palette
                    : AppConfigDefaults.CreateDefaultClassColors(item.Key);
                var selectedIndex = normalized.ClassColorIndexes.TryGetValue(item.Key, out var index)
                    ? index
                    : AppConfigDefaults.MinClassColorIndex;
                item.Colors.Load(colors, selectedIndex);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void ResetToDefaults()
    {
        Load(AppConfigDefaults.CreateClassColorSettings());
        RaiseSettingsChanged();
    }

    public Color GetSelectedClassColor(string key)
    {
        return GetItem(key).Colors.SelectedColor;
    }

    public void ApplyClassColor(string key, Color color)
    {
        GetItem(key).Colors.AddOrSelect(color);
    }

    private ClassColorItemViewModel GetItem(string key)
    {
        return _itemsByKey.TryGetValue(key, out var item)
            ? item
            : _itemsByKey["Unknown"];
    }

    private void Palette_PaletteChanged(object? sender, EventArgs e)
    {
        RaiseSettingsChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshDisplayName();
        }
    }

    private void RaiseSettingsChanged()
    {
        if (!_isLoading)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed class ClassColorItemViewModel : ObservableObject
{
    public ClassColorItemViewModel(string key, ColorPaletteViewModel colors)
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
