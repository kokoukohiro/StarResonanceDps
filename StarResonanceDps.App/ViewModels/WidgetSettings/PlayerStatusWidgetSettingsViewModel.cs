using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// ステータス詳細の表示設定と、行ごとのオン/オフ。
/// 行の一覧の作りは「他人のロールスキル」(<see cref="MeterWidgetSettingsViewModel"/>)と同じ。
/// </summary>
public sealed partial class PlayerStatusWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<PlayerStatusRowItemViewModel> _rows = [];
    private PlayerStatusWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private bool _hideInactiveStatusEffects = WidgetConfigDefaults.DefaultHideInactiveStatusEffects;

    public PlayerStatusWidgetSettingsViewModel(PlayerStatusWidgetSettingsConfig? config)
    {
        var attrIds = PlayerStatusEntry.SettingRowAttrIds;
        for (var index = 0; index < attrIds.Count; index++)
        {
            _rows.Add(new PlayerStatusRowItemViewModel(
                attrIds[index],
                index == attrIds.Count - 1,
                OnRowVisibilityChanged));
        }

        Rows = new ReadOnlyObservableCollection<PlayerStatusRowItemViewModel>(_rows);

        Load(WidgetConfigDefaults.CloneNormalizedPlayerStatus(config));
        _lastSaved = CreateConfig();

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<PlayerStatusWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<PlayerStatusRowItemViewModel> Rows { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString("Settings_Section_Display_Title");

    public string RowSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_PlayerStatusRows_Title");

    /// <summary>スイッチの右に出す状態の文言。作りはウィンドウの表示設定と同じ。</summary>
    public string HideInactiveStatusEffectsStateText => LocalizationManager.Instance.GetString(
        HideInactiveStatusEffects ? "Settings_Switch_On" : "Settings_Switch_Off");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    public PlayerStatusWidgetSettingsConfig CreateConfig()
    {
        var config = new PlayerStatusWidgetSettingsConfig
        {
            HideInactiveStatusEffects = HideInactiveStatusEffects,
            RowVisibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var row in _rows)
        {
            config.RowVisibility[row.Key] = row.IsVisible;
        }

        return config;
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreatePlayerStatusSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(PlayerStatusWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedPlayerStatus(config);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnHideInactiveStatusEffectsChanged(bool value)
    {
        OnPropertyChanged(nameof(HideInactiveStatusEffectsStateText));
        NotifyChanged();
    }

    private void Load(PlayerStatusWidgetSettingsConfig config)
    {
        _isLoading = true;
        try
        {
            HideInactiveStatusEffects = config.HideInactiveStatusEffects
                ?? WidgetConfigDefaults.DefaultHideInactiveStatusEffects;

            foreach (var row in _rows)
            {
                row.LoadVisibility(
                    config.RowVisibility is not null
                    && config.RowVisibility.TryGetValue(row.Key, out var visible)
                        ? visible
                        : PlayerStatusEntry.IsRowVisibleByDefault(row.AttrId));
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>スイッチを触った瞬間に呼ぶ。プレビュー反映と「未保存あり」の判定を更新する。</summary>
    private void OnRowVisibilityChanged()
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

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var row in _rows)
        {
            row.RefreshMetadata();
        }

        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(RowSectionTitle));
        OnPropertyChanged(nameof(HideInactiveStatusEffectsStateText));
    }

    private static bool SettingsEqual(
        PlayerStatusWidgetSettingsConfig left,
        PlayerStatusWidgetSettingsConfig right)
    {
        if (left.HideInactiveStatusEffects != right.HideInactiveStatusEffects)
        {
            return false;
        }

        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            var key = attrId.ToString(CultureInfo.InvariantCulture);
            if (left.RowVisibility is null
                || right.RowVisibility is null
                || !left.RowVisibility.TryGetValue(key, out var leftVisible)
                || !right.RowVisibility.TryGetValue(key, out var rightVisible)
                || leftVisible != rightVisible)
            {
                return false;
            }
        }

        return true;
    }
}
