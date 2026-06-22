using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PluginListItemViewModel : ViewModelBase
{
    public PluginKind? Kind { get; init; }

    public int OriginalIndex { get; init; }

    public string DisplayNameResourceKey { get; init; } = string.Empty;

    public bool IsAddItem { get; init; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    public void RefreshLocalizedText()
    {
        DisplayName = IsAddItem
            ? string.Empty
            : LocalizationManager.Instance.GetString(DisplayNameResourceKey);
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open()
    {
        PluginWindowManager.Instance.Open(this);
    }

    private bool CanOpen()
    {
        return !IsAddItem && Kind is not null;
    }
}
