using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 被ダメログの表示設定。HP数値の出し方、フィルター、クラスアイコンの色(クラスカラー)。
/// クラスカラーの形はプレイヤーリストと同じで、フィルターと不透明度は持たない。
/// </summary>
public sealed partial class TakenDamageLogWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private TakenDamageLogWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _healthValueDisplayModeIndex = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    [ObservableProperty]
    private int _attackerFilterIndex = WidgetConfigDefaults.DefaultTakenDamageLogAttackerFilterIndex;

    public TakenDamageLogWidgetSettingsViewModel(TakenDamageLogWidgetSettingsConfig? config)
    {
        var items = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(WidgetKind.TakenDamageLog);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.TakenDamageLog, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += ClassColors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _classColorItemsByKey.Add(key, item);
            items.Add(item);
        }

        ClassColorItems = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(items);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedTakenDamageLog(config);
        Load(_lastSaved);
    }

    public event Action<TakenDamageLogWidgetSettingsConfig>? PreviewChanged;

    /// <summary>クラスカラーの行。職ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_ClassColors_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= ClassColors_PaletteChanged;
        }
    }

    public TakenDamageLogWidgetSettingsConfig CreateConfig()
    {
        var config = new TakenDamageLogWidgetSettingsConfig
        {
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            AttackerFilterIndex = AttackerFilterIndex,
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in ClassColorItems)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedTakenDamageLog(config);
    }

    public Color GetSelectedClassColor(string key)
    {
        return _classColorItemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyClassColor(string key, Color color)
    {
        _classColorItemsByKey[key].Colors.AddOrSelect(color);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateTakenDamageLogSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(TakenDamageLogWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedTakenDamageLog(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(TakenDamageLogWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTakenDamageLog(config);

        _isLoading = true;
        try
        {
            foreach (var item in ClassColorItems)
            {
                item.Colors.Load(normalized.ClassColorPalettes[item.Key], normalized.ClassColorIndexes[item.Key]);
            }

            HealthValueDisplayModeIndex = normalized.HealthValueDisplayModeIndex;
            AttackerFilterIndex = normalized.AttackerFilterIndex;
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

    private static bool SettingsEqual(
        TakenDamageLogWidgetSettingsConfig left,
        TakenDamageLogWidgetSettingsConfig right)
    {
        if (left.HealthValueDisplayModeIndex != right.HealthValueDisplayModeIndex
            || left.AttackerFilterIndex != right.AttackerFilterIndex)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(WidgetKind.TakenDamageLog))
        {
            if (left.ClassColorIndexes[key] != right.ClassColorIndexes[key]
                || !left.ClassColorPalettes[key].SequenceEqual(right.ClassColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void ClassColors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in ClassColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(ClassColorSectionTitle));
    }

    partial void OnHealthValueDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnAttackerFilterIndexChanged(int value)
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
