using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフ・デバフカードの表示設定。中身は表示テキストの書式ひとつ。
///
/// <para>
/// 倍率(<see cref="BuffCardWidgetSettingsConfig.Scales"/>)は<b>この画面では触らない</b>。
/// ウィンドウのヘッダーボタンが直接書き込むので、ここで作り直すと開いているカードの
/// 倍率を巻き戻してしまう。読み込んだ値をそのまま持ち回す。
/// </para>
/// </summary>
public sealed partial class BuffCardWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] FormatFieldDefinitions =
    [
        ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
        ("Name", "Settings_BuffInfo_Field_Name", "{Name}"),
        ("Level", "Settings_BuffInfo_Field_Level", "{Level}"),
        ("NewLine", "Settings_BuffInfo_Field_NewLine", "{NewLine}")
    ];

    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableFormatFields = [];
    private BuffCardWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _buffInfoFormatString = WidgetConfigDefaults.DefaultBuffInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    [ObservableProperty]
    private string _customizationTitle = string.Empty;

    public BuffCardWidgetSettingsViewModel(BuffCardWidgetSettingsConfig? config)
    {
        AvailableFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableFormatFields);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        RebuildFormatFields();
        RefreshLocalizedText();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffCard(config);
        Load(_lastSaved);
    }

    public event Action<BuffCardWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableFormatFields { get; }

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    public BuffCardWidgetSettingsConfig CreateConfig()
    {
        var config = _lastSaved.Clone();
        config.BuffInfoFormatString = BuffInfoFormatString ?? string.Empty;
        return WidgetConfigDefaults.CloneNormalizedBuffCard(config);
    }

    public void ResetToDefaults()
    {
        var defaults = WidgetConfigDefaults.CreateBuffCardSettings();
        defaults.Scales = new Dictionary<string, int>(_lastSaved.Scales, StringComparer.OrdinalIgnoreCase);
        Load(defaults);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(BuffCardWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffCard(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(BuffCardWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedBuffCard(config);

        _isLoading = true;
        try
        {
            BuffInfoFormatString = normalized.BuffInfoFormatString ?? string.Empty;
        }
        finally
        {
            _isLoading = false;
        }

        RefreshFormatPreview();
    }

    private void RebuildFormatFields()
    {
        var selectedKey = SelectedFormatField?.Key;

        _availableFormatFields.Clear();
        foreach (var definition in FormatFieldDefinitions)
        {
            _availableFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedFormatField = _availableFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedKey, StringComparison.Ordinal))
            ?? _availableFormatFields.FirstOrDefault();
    }

    private void RefreshLocalizedText()
    {
        CustomizationTitle = LocalizationManager.Instance.GetString("Settings_BuffInfo_Customization");
    }

    private void RefreshFormatPreview()
    {
        // 改行はプレビューでは1行に潰す。設定欄の高さが変わらないようにするため。
        FormatPreview = BuffInfoFormatFormatter
            .FormatPreview(BuffInfoFormatString)
            .Replace(Environment.NewLine, " ", StringComparison.Ordinal);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildFormatFields();
        RefreshLocalizedText();
        RefreshFormatPreview();
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private static bool SettingsEqual(
        BuffCardWidgetSettingsConfig left,
        BuffCardWidgetSettingsConfig right)
    {
        return string.Equals(
            left.BuffInfoFormatString,
            right.BuffInfoFormatString,
            StringComparison.Ordinal);
    }

    partial void OnBuffInfoFormatStringChanged(string value)
    {
        RefreshFormatPreview();

        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }
}
