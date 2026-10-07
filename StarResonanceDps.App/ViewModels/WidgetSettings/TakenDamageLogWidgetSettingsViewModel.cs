using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 被ダメログの表示設定。HP数値の出し方、フィルター、クラスアイコンの色(クラスカラー)、通知の文章(予兆技)。
/// クラスカラーの形はプレイヤーリストと同じで、フィルターと不透明度は持たない。
/// </summary>
public sealed partial class TakenDamageLogWidgetSettingsViewModel : ObservableObject, IDisposable
{
    /// <summary>予兆技の通知の文章に差し込める項目。名前はログの行と同じ名前、スキル名は予告の名前があればそれ。</summary>
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] TelegraphedSkillFormatFieldDefinitions =
    [
        ("Name", "Settings_EntityInfo_Field_Name", "{Name}"),
        ("SkillName", "Metric_SkillName", "{SkillName}")
    ];

    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TakenDamageLogTextColorItemViewModel> _textColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
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

        var textItems = new ObservableCollection<TakenDamageLogTextColorItemViewModel>();
        var textColorKeys = WidgetConfigDefaults.TakenDamageLogTextColorKeys;
        for (var index = 0; index < textColorKeys.Length; index++)
        {
            var key = textColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultTextColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += TextColors_PaletteChanged;

            var item = new TakenDamageLogTextColorItemViewModel(key, colors, index == textColorKeys.Length - 1);
            _textColorItemsByKey.Add(key, item);
            textItems.Add(item);
        }

        TextColorItems = new ReadOnlyObservableCollection<TakenDamageLogTextColorItemViewModel>(textItems);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        TelegraphedSkillNotification = new WidgetNotificationTextItemViewModel(
            "Settings_TakenDamageLogNotification_TelegraphedSkill",
            WidgetNotificationTextFormatter.TelegraphedSkillDefaultKey,
            TelegraphedSkillFormatFieldDefinitions,
            WidgetNotificationTextFormatter.FormatTelegraphedSkillPreview);
        TelegraphedSkillNotification.Changed += NotificationText_Changed;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedTakenDamageLog(config);
        Load(_lastSaved);
    }

    public event Action<TakenDamageLogWidgetSettingsConfig>? PreviewChanged;

    /// <summary>通知の文章「予兆技」。</summary>
    public WidgetNotificationTextItemViewModel TelegraphedSkillNotification { get; }

    /// <summary>クラスカラーの行。職ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    /// <summary>テキストカラーの行。行ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<TakenDamageLogTextColorItemViewModel> TextColorItems { get; }

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_IconColors_Title");

    public string TextColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_TakenDamageLogTextColors_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        TelegraphedSkillNotification.Changed -= NotificationText_Changed;

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= ClassColors_PaletteChanged;
        }

        foreach (var item in TextColorItems)
        {
            item.Colors.PaletteChanged -= TextColors_PaletteChanged;
        }
    }

    public TakenDamageLogWidgetSettingsConfig CreateConfig()
    {
        var config = new TakenDamageLogWidgetSettingsConfig
        {
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            AttackerFilterIndex = AttackerFilterIndex,
            TelegraphedSkillNotificationFormatString = TelegraphedSkillNotification.CreateSavedValue(),
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in ClassColorItems)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        foreach (var item in TextColorItems)
        {
            config.TextColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.TextColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
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

    public Color GetSelectedTextColor(string key)
    {
        return _textColorItemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyTextColor(string key, Color color)
    {
        _textColorItemsByKey[key].Colors.AddOrSelect(color);
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

            foreach (var item in TextColorItems)
            {
                item.Colors.Load(normalized.TextColorPalettes[item.Key], normalized.TextColorIndexes[item.Key]);
            }

            HealthValueDisplayModeIndex = normalized.HealthValueDisplayModeIndex;
            AttackerFilterIndex = normalized.AttackerFilterIndex;
            TelegraphedSkillNotification.Load(normalized.TelegraphedSkillNotificationFormatString);
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
            || left.AttackerFilterIndex != right.AttackerFilterIndex
            || !string.Equals(left.TelegraphedSkillNotificationFormatString, right.TelegraphedSkillNotificationFormatString, StringComparison.Ordinal))
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

        foreach (var key in WidgetConfigDefaults.TakenDamageLogTextColorKeys)
        {
            if (left.TextColorIndexes[key] != right.TextColorIndexes[key]
                || !left.TextColorPalettes[key].SequenceEqual(right.TextColorPalettes[key], StringComparer.OrdinalIgnoreCase))
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

    private void TextColors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in ClassColorItems)
        {
            item.RefreshDisplayName();
        }

        foreach (var item in TextColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(ClassColorSectionTitle));
        OnPropertyChanged(nameof(TextColorSectionTitle));
        TelegraphedSkillNotification.RefreshLocalizedText();
    }

    private void NotificationText_Changed(object? sender, EventArgs e)
    {
        NotifyChanged();
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

/// <summary>
/// テキストカラーの1行。行の作りはクラスカラー(<see cref="MeterClassColorItemViewModel"/>)と同じで、
/// ダメージの属性の行だけ左にアイコンを出す。
/// </summary>
public sealed class TakenDamageLogTextColorItemViewModel : ObservableObject
{
    public TakenDamageLogTextColorItemViewModel(string key, ColorPaletteViewModel colors, bool isLast)
    {
        Key = key;
        Colors = colors;
        IsLast = isLast;
        IsDamageProperty = WidgetConfigDefaults.IsDamagePropertyKey(key);
        if (IsDamageProperty)
        {
            var mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.DamageProperty.{key}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            IconMask = mask;
        }
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public bool IsLast { get; }

    /// <summary>ダメージの属性の行。左にアイコンを出す。</summary>
    public bool IsDamageProperty { get; }

    /// <summary>属性アイコンの形。その行の選択色をこの形で抜く(クラスアイコンと同じ染め方)。</summary>
    public Brush? IconMask { get; }

    /// <summary>属性の行は属性名、それ以外は被ダメログ専用の項目名。</summary>
    public string DisplayName => IsDamageProperty
        ? LocalizationManager.Instance.GetString($"DamageProperty_{Key}")
        : LocalizationManager.Instance.GetString($"Settings_TakenDamageLogTextColor_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
