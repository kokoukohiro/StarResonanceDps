using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PluginListItemViewModel : ViewModelBase
{
    public string PluginId { get; init; } = string.Empty;

    public int OriginalIndex { get; init; }

    public Dictionary<string, string> DisplayNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsAddItem { get; init; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    public void RefreshLocalizedText()
    {
        if (IsAddItem)
        {
            DisplayName = string.Empty;
            return;
        }

        var culture = LocalizationManager.Instance.CurrentCulture;
        if (TryGetDisplayName(culture.Name, out var displayName)
            || TryGetDisplayName(culture.TwoLetterISOLanguageName, out displayName)
            || TryGetDisplayName("en-US", out displayName))
        {
            DisplayName = displayName!;
            return;
        }

        DisplayName = DisplayNames.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? PluginId;
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open()
    {
        PluginManager.Instance.Open(this);
    }

    private bool CanOpen()
    {
        return !IsAddItem && !string.IsNullOrWhiteSpace(PluginId);
    }

    private bool TryGetDisplayName(string cultureName, out string? displayName)
    {
        if (DisplayNames.TryGetValue(cultureName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            displayName = value;
            return true;
        }

        displayName = null;
        return false;
    }
}
