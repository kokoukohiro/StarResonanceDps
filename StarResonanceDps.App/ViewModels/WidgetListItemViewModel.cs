using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public partial class WidgetListItemViewModel : ViewModelBase
{
    private readonly ObservableCollection<PlayerListEntry> _playerListEntries = [];
    private IReadOnlyList<PlayerRosterEntry> _playerRoster = Array.Empty<PlayerRosterEntry>();
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private WidgetThemeConfig _theme = WidgetConfigDefaults.CreateTheme();
    private long? _selectedPlayerCharacterId;

    public WidgetKind Kind { get; init; }

    public int OriginalIndex { get; init; }

    public string DisplayNameResourceKey { get; init; } = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private WidgetState _state;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private ThemeColorPalette _themePalette = ThemeColorPalette.Create(Color.FromRgb(0x0B, 0x16, 0x24));

    [ObservableProperty]
    private WidgetWindowThemePalette _widgetWindowPalette = WidgetWindowThemePalette.Create(Color.FromRgb(0x0B, 0x16, 0x24), 50);

    [ObservableProperty]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private bool _hasBackgroundImage;

    [ObservableProperty]
    private string _mapName = string.Empty;

    [ObservableProperty]
    private PlayerDetailEntry? _selectedPlayerDetail;

    public ReadOnlyObservableCollection<PlayerListEntry> PlayerListEntries { get; }

    public bool IsPlayerList => Kind == WidgetKind.PlayerInfoDebug;

    public bool IsPlayerDetail => Kind == WidgetKind.PlayerDetail;

    public string StateText => State == WidgetState.Running
        ? LocalizationManager.Instance.GetString("Widget_State_Running")
        : LocalizationManager.Instance.GetString("Widget_State_Stopped");

    public bool IsRunning => State == WidgetState.Running;

    public event Action<long>? PlayerDetailRequested;

    public WidgetListItemViewModel()
    {
        PlayerListEntries = new ReadOnlyObservableCollection<PlayerListEntry>(_playerListEntries);
    }

    public void RefreshLocalizedText()
    {
        DisplayName = LocalizationManager.Instance.GetString(DisplayNameResourceKey);
        OnPropertyChanged(nameof(StateText));
        RefreshPlayerListEntries();
        RefreshPlayerDetail();
    }

    public WidgetConfig CreateWidgetConfig()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            State = State,
            Theme = _theme.Clone()
        };
    }

    public void ApplyWidgetConfig(WidgetConfig config)
    {
        WidgetConfigDefaults.Normalize(Kind, config);

        IsFavorite = config.IsFavorite;
        IsPinned = config.IsPinned;

        if (config.State is { } state)
        {
            State = state;
        }

        ApplyTheme(config.Theme);
        RefreshPlayerListEntries();
        RefreshPlayerDetail();
    }

    public void ApplyTheme(WidgetThemeConfig theme)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTheme(theme);

        _theme = normalized.Clone();

        var selectedHex = normalized.WindowColors[
            Math.Clamp(normalized.WindowColorIndex, 0, normalized.WindowColors.Count - 1)];

        if (!ColorUtilities.TryParseHex(selectedHex, out var windowSurface))
        {
            windowSurface = Color.FromRgb(0x0B, 0x16, 0x24);
        }

        var hasBackgroundImage = !string.IsNullOrWhiteSpace(normalized.BackgroundImagePath)
            && File.Exists(normalized.BackgroundImagePath);
        Color? backgroundImageAverageColor = null;
        if (hasBackgroundImage
            && string.Equals(
                normalized.BackgroundImageAverageColorSourcePath,
                normalized.BackgroundImagePath,
                StringComparison.OrdinalIgnoreCase)
            && ColorUtilities.TryParseHex(normalized.BackgroundImageAverageColor, out var parsedAverageColor))
        {
            backgroundImageAverageColor = parsedAverageColor;
        }

        ThemePalette = ThemeColorPalette.Create(windowSurface);
        WidgetWindowPalette = WidgetWindowThemePalette.Create(
            windowSurface,
            normalized.WindowOpacity,
            backgroundImageAverageColor);

        HasBackgroundImage = hasBackgroundImage;
        BackgroundImagePath = hasBackgroundImage
            ? normalized.BackgroundImagePath
            : null;
    }

    public void UpdatePlayerRoster(IReadOnlyList<PlayerRosterEntry> playerRoster, string mapName)
    {
        if (!IsPlayerList && !IsPlayerDetail)
        {
            return;
        }

        MapName = mapName ?? string.Empty;
        _playerRoster = playerRoster
            .Select(entry => new PlayerRosterEntry(
                entry.CharacterId,
                entry.Name,
                entry.ProfessionId,
                entry.CombatPower,
                entry.SeasonStrength,
                entry.CurrentHp,
                entry.MaxHp,
                entry.ClassSpec,
                entry.IsSelf,
                entry.CombatAttributes))
            .ToArray();
        RefreshPlayerListEntries();
        RefreshPlayerDetail();
    }

    public void SelectPlayer(long characterId)
    {
        if (!IsPlayerDetail)
        {
            return;
        }

        _selectedPlayerCharacterId = characterId;
        RefreshPlayerDetail();
    }

    public void ResetSelectedPlayer()
    {
        if (!IsPlayerDetail)
        {
            return;
        }

        _selectedPlayerCharacterId = null;
        RefreshPlayerDetail();
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    private void ToggleRunning()
    {
        State = State == WidgetState.Running
            ? WidgetState.Stopped
            : WidgetState.Running;
    }

    [RelayCommand]
    private void RequestPlayerDetail(PlayerListEntry? player)
    {
        if (!IsPlayerList || player is null)
        {
            return;
        }

        PlayerDetailRequested?.Invoke(player.CharacterId);
    }

    partial void OnStateChanged(WidgetState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsRunning));
    }

    private void RefreshPlayerListEntries()
    {
        if (!IsPlayerList)
        {
            return;
        }

        var classColors = _configManager.GetSettingsSnapshot().ClassColors;
        var nextEntries = _playerRoster
            .Select(entry => PlayerListEntry.Create(entry, classColors))
            .ToArray();

        _playerListEntries.Clear();
        foreach (var entry in nextEntries)
        {
            _playerListEntries.Add(entry);
        }
    }

    private void RefreshPlayerDetail()
    {
        if (!IsPlayerDetail)
        {
            return;
        }

        var selectedPlayer = _selectedPlayerCharacterId is { } characterId
            ? _playerRoster.FirstOrDefault(entry => entry.CharacterId == characterId)
            : _playerRoster.FirstOrDefault(entry => entry.IsSelf);

        if (selectedPlayer is null && _selectedPlayerCharacterId.HasValue)
        {
            _selectedPlayerCharacterId = null;
            selectedPlayer = _playerRoster.FirstOrDefault(entry => entry.IsSelf);
        }

        SelectedPlayerDetail = selectedPlayer is null
            ? null
            : PlayerDetailEntry.Create(selectedPlayer);
    }
}
