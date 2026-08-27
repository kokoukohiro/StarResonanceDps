using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

public sealed partial class MeterWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] PlayerInfoFormatFieldDefinitions =
    [
        ("Name", "Settings_PlayerInfo_Field_Name", "{Name}"),
        ("Spec", "Settings_PlayerInfo_Field_Class", "{Spec}"),
        ("PowerLevel", "Settings_PlayerInfo_Field_AbilityScore", "{PowerLevel}"),
        ("SeasonStrength", "Settings_PlayerInfo_Field_SeasonStrength", "{SeasonStrength}"),
        ("SeasonLevel", "Settings_PlayerInfo_Field_SeasonLevel", "{SeasonLevel}"),
        ("Uid", "Settings_PlayerInfo_Field_PlayerUid", "{Uid}")
    ];

    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] EntityInfoFormatFieldDefinitions =
    [
        ("Name", "Settings_EntityInfo_Field_Name", "{Name}"),
        ("Level", "Settings_EntityInfo_Field_Level", "{Level}")
    ];

    private readonly WidgetKind _kind;
    private readonly Dictionary<string, MeterClassColorItemViewModel> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availablePlayerInfoFormatFields = [];
    private readonly ObservableCollection<OtherRoleSkillItemViewModel> _otherRoleSkills = [];
    private MeterWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _playerInfoFormatString = WidgetConfigDefaults.DefaultMeterPlayerInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedPlayerInfoFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    [ObservableProperty]
    private int _healthValueDisplayModeIndex = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    [ObservableProperty]
    private int _partyDisplayModeIndex = WidgetConfigDefaults.DefaultPartyDisplayModeIndex;

    [ObservableProperty]
    private double _classColorOpacity = WidgetConfigDefaults.MaxClassColorOpacity;

    public MeterWidgetSettingsViewModel(WidgetKind kind, MeterWidgetSettingsConfig? config)
    {
        _kind = kind;
        var items = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(_kind);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(_kind, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _itemsByKey.Add(key, item);
            items.Add(item);
        }

        Items = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(items);

        var roleSkillIds = WidgetConfigDefaults.OtherRoleSkillIds;
        for (var index = 0; index < roleSkillIds.Count; index++)
        {
            _otherRoleSkills.Add(new OtherRoleSkillItemViewModel(
                roleSkillIds[index],
                index == roleSkillIds.Count - 1,
                OnOtherRoleSkillVisibilityChanged));
        }

        OtherRoleSkills = new ReadOnlyObservableCollection<OtherRoleSkillItemViewModel>(_otherRoleSkills);
        AvailablePlayerInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availablePlayerInfoFormatFields);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        RebuildPlayerInfoFormatFields();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMeter(_kind, config);
        Load(_lastSaved);
    }

    public ReadOnlyObservableCollection<OtherRoleSkillItemViewModel> OtherRoleSkills { get; }

    /// <summary>「他人のロールスキル」設定を出すのはプレイヤーリストだけ。</summary>
    public bool ShowsOtherRoleSkillSettings => _kind == WidgetKind.PlayerList;

    public event Action<MeterWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> Items { get; }

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailablePlayerInfoFormatFields { get; }

    public string PlayerInfoCustomizationTitle => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.EntityList
            ? "Settings_EntityInfo_Customization"
            : "Settings_PlayerInfo_Customization");

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.EntityList
            ? "Settings_Section_IconColors_Title"
            : "Settings_Section_ClassColors_Title");

    public bool UsesMeterClassColorIconBackground => WidgetConfigDefaults.UsesMeterClassColorOpacity(_kind);

    public bool ShowsPlayerListProfessionIcons => !UsesMeterClassColorIconBackground;

    public bool HasClassColorOpacity => UsesMeterClassColorIconBackground;

    public bool ShowsHealthValueSettings => _kind is WidgetKind.PlayerList or WidgetKind.EntityList;

    public bool ShowsPartyDisplaySettings => _kind is WidgetKind.PlayerList or WidgetKind.DpsMeter or WidgetKind.HpsMeter;

    public bool HasAdditionalDisplaySettings => ShowsHealthValueSettings || ShowsPartyDisplaySettings;

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in Items)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    [RelayCommand]
    private void AddPlayerInfoFormatField()
    {
        if (SelectedPlayerInfoFormatField is null)
        {
            return;
        }

        PlayerInfoFormatString += SelectedPlayerInfoFormatField.Placeholder;
    }

    public MeterWidgetSettingsConfig CreateConfig()
    {
        var config = new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString ?? string.Empty,
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            PartyDisplayModeIndex = PartyDisplayModeIndex,
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

        config.OtherRoleSkillVisibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in _otherRoleSkills)
        {
            config.OtherRoleSkillVisibility[skill.Key] = skill.IsVisible;
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

            foreach (var skill in _otherRoleSkills)
            {
                skill.LoadVisibility(
                    !normalized.OtherRoleSkillVisibility.TryGetValue(skill.Key, out var visible)
                    || visible);
            }

            PlayerInfoFormatString = normalized.PlayerInfoFormatString ?? string.Empty;
            HealthValueDisplayModeIndex = normalized.HealthValueDisplayModeIndex;
            PartyDisplayModeIndex = normalized.PartyDisplayModeIndex;
            ClassColorOpacity = normalized.ClassColorOpacity;
        }
        finally
        {
            _isLoading = false;
        }

        RefreshFormatPreview();
    }

    private MeterClassColorItemViewModel GetItem(string key)
    {
        return _itemsByKey.TryGetValue(key, out var item)
            ? item
            : _itemsByKey["Unknown"];
    }

    private bool SettingsEqual(MeterWidgetSettingsConfig left, MeterWidgetSettingsConfig right)
    {
        if (!string.Equals(left.PlayerInfoFormatString, right.PlayerInfoFormatString, StringComparison.Ordinal)
            || left.HealthValueDisplayModeIndex != right.HealthValueDisplayModeIndex
            || left.PartyDisplayModeIndex != right.PartyDisplayModeIndex
            || left.ClassColorOpacity != right.ClassColorOpacity)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(_kind))
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

        foreach (var skillId in WidgetConfigDefaults.OtherRoleSkillIds)
        {
            var key = skillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!left.OtherRoleSkillVisibility.TryGetValue(key, out var leftVisible)
                || !right.OtherRoleSkillVisibility.TryGetValue(key, out var rightVisible)
                || leftVisible != rightVisible)
            {
                return false;
            }
        }

        return true;
    }

    private void RebuildPlayerInfoFormatFields()
    {
        var selectedKey = SelectedPlayerInfoFormatField?.Key;

        _availablePlayerInfoFormatFields.Clear();
        var definitions = _kind == WidgetKind.EntityList
            ? EntityInfoFormatFieldDefinitions
            : PlayerInfoFormatFieldDefinitions;
        foreach (var definition in definitions)
        {
            _availablePlayerInfoFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedPlayerInfoFormatField = _availablePlayerInfoFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedKey, StringComparison.Ordinal))
            ?? _availablePlayerInfoFormatFields.FirstOrDefault();
    }

    private void RefreshFormatPreview()
    {
        FormatPreview = _kind == WidgetKind.EntityList
            ? EntityInfoFormatFormatter.FormatPreview(PlayerInfoFormatString)
            : PlayerInfoFormatFormatter.FormatPreview(PlayerInfoFormatString);
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    /// <summary>
    /// スイッチを触った瞬間に呼ぶ。既存の色設定と同じ経路で、
    /// プレビュー反映と「未保存あり」の判定を更新する。
    /// </summary>
    private void OnOtherRoleSkillVisibilityChanged()
    {
        NotifyChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshDisplayName();
        }

        foreach (var skill in _otherRoleSkills)
        {
            skill.RefreshMetadata();
        }

        OnPropertyChanged(nameof(PlayerInfoCustomizationTitle));
        OnPropertyChanged(nameof(ClassColorSectionTitle));
        RebuildPlayerInfoFormatFields();
        RefreshFormatPreview();
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

    partial void OnPlayerInfoFormatStringChanged(string value)
    {
        var normalized = NormalizeFormatString(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            PlayerInfoFormatString = normalized;
            return;
        }

        RefreshFormatPreview();
        NotifyChanged();
    }

    partial void OnHealthValueDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnPartyDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnClassColorOpacityChanged(double value)
    {
        NotifyChanged();
    }

    private static string NormalizeFormatString(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}

public sealed class MeterPlayerInfoFormatField
{
    public MeterPlayerInfoFormatField(string key, string displayName, string placeholder)
    {
        Key = key;
        DisplayName = displayName;
        Placeholder = placeholder;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string Placeholder { get; }

    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class MeterClassColorItemViewModel : ObservableObject
{
    public MeterClassColorItemViewModel(string key, ColorPaletteViewModel colors, bool isLast)
    {
        Key = key;
        Colors = colors;
        IsLast = isLast;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public bool IsLast { get; }

    public string DisplayName => LocalizationManager.Instance.GetString($"Classes_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
