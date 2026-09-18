using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>被ダメログの表示設定。HP数値の出し方。</summary>
public sealed partial class TakenDamageLogWidgetSettingsViewModel : ObservableObject
{
    private TakenDamageLogWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _healthValueDisplayModeIndex = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    public TakenDamageLogWidgetSettingsViewModel(TakenDamageLogWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedTakenDamageLog(config);
        Load(_lastSaved);
    }

    public event Action<TakenDamageLogWidgetSettingsConfig>? PreviewChanged;

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public TakenDamageLogWidgetSettingsConfig CreateConfig()
    {
        return WidgetConfigDefaults.CloneNormalizedTakenDamageLog(new TakenDamageLogWidgetSettingsConfig
        {
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex
        });
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
            HealthValueDisplayModeIndex = normalized.HealthValueDisplayModeIndex;
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
        return left.HealthValueDisplayModeIndex == right.HealthValueDisplayModeIndex;
    }

    partial void OnHealthValueDisplayModeIndexChanged(int value)
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }
}
