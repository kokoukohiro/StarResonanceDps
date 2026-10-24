using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Notifications;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.ViewModels.Notifications;

/// <summary>
/// 通知タブの項目の文章(入力欄・プレビュー・差し込む項目)。
///
/// <para>
/// 保存する値は、null が既定のまま(表示言語の既定の文。言語を変えると入力欄も替わる)、それ以外は書き換えた文。
/// 入力欄の文が今の言語の既定の文と同じとき、空欄(空白だけを含む)のときは既定のままとして保存する。
/// 空欄のプレビューは、通知に使われる既定の文で出す。
/// </para>
/// </summary>
public sealed partial class NotificationTextViewModel : ObservableObject
{
    private readonly NotificationKind _kind;
    private readonly (string Key, string LabelResourceKey, string Placeholder)[] _fieldDefinitions;
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

    public NotificationTextViewModel(
        NotificationKind kind,
        (string Key, string LabelResourceKey, string Placeholder)[] fieldDefinitions)
    {
        _kind = kind;
        _fieldDefinitions = fieldDefinitions;
        AvailableFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableFormatFields);
        RefreshLocalizedText();
    }

    /// <summary>入力欄の文が変わった。読み込みと、言語を変えたときの既定の文の差し替えでは上げない。</summary>
    public event EventHandler? Changed;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableFormatFields { get; }

    private string DefaultText => LocalizationManager.Instance.GetString(NotificationTextFormatter.GetDefaultKey(_kind));

    /// <summary>保存する値。今の言語の既定の文か空欄なら null。</summary>
    public string? CreateSavedValue()
    {
        return string.IsNullOrWhiteSpace(FormatString) || string.Equals(FormatString, DefaultText, StringComparison.Ordinal)
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

    /// <summary>差し込む項目とプレビューを作り直す。既定の文のままなら、入力欄を今の言語の既定の文にする。</summary>
    public void RefreshLocalizedText()
    {
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
        // 改行はそのまま出す(通知と同じ文を見せる)。空欄は既定の文で通知するので、既定の文で出す。
        var format = string.IsNullOrWhiteSpace(FormatString) ? DefaultText : FormatString;
        Preview = NotificationTextFormatter.FormatPreview(_kind, format);
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
