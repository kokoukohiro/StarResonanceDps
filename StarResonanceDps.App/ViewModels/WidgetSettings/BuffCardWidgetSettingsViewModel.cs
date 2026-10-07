using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフ・デバフカードの設定。表示テキストの書式と、通知テキスト(効果時間切れ・薬剤・料理バフ2分以下)の書式。
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

    /// <summary>通知テキストに差し込める項目。名前とバフ名だけ(料理・薬剤のバフ名は「料理」「薬剤」の1語)。</summary>
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] NotificationFormatFieldDefinitions =
    [
        ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
        ("Name", "Settings_BuffInfo_Field_Name", "{Name}")
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

        ExpiredNotification = new WidgetNotificationTextItemViewModel(
            "Settings_BuffCardNotification_Expired",
            WidgetNotificationTextFormatter.BuffCardExpiredDefaultKey,
            NotificationFormatFieldDefinitions,
            WidgetNotificationTextFormatter.FormatBuffCardExpiredPreview);
        CuisinePotionLowNotification = new WidgetNotificationTextItemViewModel(
            "Settings_BuffCardNotification_CuisinePotionLow",
            WidgetNotificationTextFormatter.BuffCardCuisinePotionLowDefaultKey,
            NotificationFormatFieldDefinitions,
            WidgetNotificationTextFormatter.FormatBuffCardCuisinePotionLowPreview);
        ExpiredNotification.Changed += NotificationText_Changed;
        CuisinePotionLowNotification.Changed += NotificationText_Changed;

        RebuildFormatFields();
        RefreshLocalizedText();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffCard(config);
        Load(_lastSaved);
    }

    public event Action<BuffCardWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableFormatFields { get; }

    /// <summary>通知テキスト「効果時間切れ」。</summary>
    public WidgetNotificationTextItemViewModel ExpiredNotification { get; }

    /// <summary>通知テキスト「薬剤・料理バフ2分以下」。</summary>
    public WidgetNotificationTextItemViewModel CuisinePotionLowNotification { get; }

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        ExpiredNotification.Changed -= NotificationText_Changed;
        CuisinePotionLowNotification.Changed -= NotificationText_Changed;
    }

    public BuffCardWidgetSettingsConfig CreateConfig()
    {
        var config = _lastSaved.Clone();
        config.BuffInfoFormatString = BuffInfoFormatString ?? string.Empty;
        config.ExpiredNotificationFormatString = ExpiredNotification.CreateSavedValue();
        config.CuisinePotionLowNotificationFormatString = CuisinePotionLowNotification.CreateSavedValue();
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
            ExpiredNotification.Load(normalized.ExpiredNotificationFormatString);
            CuisinePotionLowNotification.Load(normalized.CuisinePotionLowNotificationFormatString);
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
        ExpiredNotification.RefreshLocalizedText();
        CuisinePotionLowNotification.RefreshLocalizedText();
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private static bool SettingsEqual(
        BuffCardWidgetSettingsConfig left,
        BuffCardWidgetSettingsConfig right)
    {
        return string.Equals(left.BuffInfoFormatString, right.BuffInfoFormatString, StringComparison.Ordinal)
            && string.Equals(left.ExpiredNotificationFormatString, right.ExpiredNotificationFormatString, StringComparison.Ordinal)
            && string.Equals(left.CuisinePotionLowNotificationFormatString, right.CuisinePotionLowNotificationFormatString, StringComparison.Ordinal);
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

    private void NotificationText_Changed(object? sender, EventArgs e)
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
/// ウィジェットの通知の文章の1行(書式の入力欄・プレビュー・差し込む項目)。バフ・デバフカードと被ダメログが使う。
///
/// <para>
/// 保存する値は、null が既定のまま(表示言語の既定の文。言語を変えると入力欄も替わる)、空が通知しない、それ以外は書き換えた文。
/// 入力欄の文が今の言語の既定の文と同じなら、既定のままとして保存する。空白だけの文も空欄として扱う。
/// </para>
/// </summary>
public sealed partial class WidgetNotificationTextItemViewModel : ObservableObject
{
    private readonly string _labelResourceKey;
    private readonly string _defaultTextResourceKey;
    private readonly (string Key, string LabelResourceKey, string Placeholder)[] _fieldDefinitions;
    private readonly Func<string, string> _formatPreview;
    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableFormatFields = [];
    private bool _isLoading;

    /// <summary>入力欄の文が今の言語の既定の文か。言語を変えたときに、入力欄を新しい言語の既定の文へ替えるかを決める。</summary>
    private bool _usesDefaultText = true;

    [ObservableProperty]
    private string _formatString = string.Empty;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedFormatField;

    [ObservableProperty]
    private string _preview = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    public WidgetNotificationTextItemViewModel(
        string labelResourceKey,
        string defaultTextResourceKey,
        (string Key, string LabelResourceKey, string Placeholder)[] fieldDefinitions,
        Func<string, string> formatPreview)
    {
        _labelResourceKey = labelResourceKey;
        _defaultTextResourceKey = defaultTextResourceKey;
        _fieldDefinitions = fieldDefinitions;
        _formatPreview = formatPreview;
        AvailableFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableFormatFields);
        RefreshLocalizedText();
    }

    /// <summary>入力欄の文が変わった。読み込みと、言語を変えたときの既定の文の差し替えでは上げない。</summary>
    public event EventHandler? Changed;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableFormatFields { get; }

    private string DefaultText => LocalizationManager.Instance.GetString(_defaultTextResourceKey);

    /// <summary>保存する値。今の言語の既定の文なら null。</summary>
    public string? CreateSavedValue()
    {
        return string.Equals(FormatString, DefaultText, StringComparison.Ordinal)
            ? null
            : FormatString;
    }

    public void Load(string? savedValue)
    {
        _isLoading = true;
        try
        {
            FormatString = savedValue ?? DefaultText;
        }
        finally
        {
            _isLoading = false;
        }

        RefreshPreview();
    }

    /// <summary>行の名前・差し込む項目・プレビューを作り直す。既定の文のままなら、入力欄を今の言語の既定の文にする。</summary>
    public void RefreshLocalizedText()
    {
        Label = LocalizationManager.Instance.GetString(_labelResourceKey);
        RebuildFormatFields();

        if (_usesDefaultText)
        {
            _isLoading = true;
            try
            {
                FormatString = DefaultText;
            }
            finally
            {
                _isLoading = false;
            }
        }

        RefreshPreview();
    }

    private void RebuildFormatFields()
    {
        var selectedKey = SelectedFormatField?.Key;

        _availableFormatFields.Clear();
        foreach (var definition in _fieldDefinitions)
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

    private void RefreshPreview()
    {
        // 改行はプレビューでは1行に潰す(設定欄の高さを変えないため)。空欄は通知しないので、そう出す。
        Preview = string.IsNullOrWhiteSpace(FormatString)
            ? LocalizationManager.Instance.GetString("NotificationMethod_None")
            : _formatPreview(FormatString).Replace(Environment.NewLine, " ", StringComparison.Ordinal);
    }

    partial void OnFormatStringChanged(string value)
    {
        _usesDefaultText = string.Equals(value, DefaultText, StringComparison.Ordinal);
        RefreshPreview();

        if (!_isLoading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
