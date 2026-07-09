using System.IO;
using System.Collections.ObjectModel;
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
    private readonly Dictionary<long, PlayerListEntry> _playerListEntriesByCharacterId = [];
    private WidgetThemeConfig _theme = WidgetConfigDefaults.CreateTheme();
    private MeterWidgetSettingsConfig _meter = WidgetConfigDefaults.CreateMeterSettings(WidgetKind.PlayerList);
    private MetricTimelineWidgetSettingsConfig _metricTimeline = WidgetConfigDefaults.CreateMetricTimelineSettings();
    private IReadOnlyList<PlayerRosterEntry> _playerRoster = Array.Empty<PlayerRosterEntry>();
    private long _playerListMapGeneration = -1;

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
    private int _openPlayerWindowCount;

    public ReadOnlyObservableCollection<PlayerListEntry> PlayerListEntries { get; }

    public bool IsPlayerList => Kind == WidgetKind.PlayerList;

    public bool IsPlayerInfo => Kind == WidgetKind.PlayerInfo;

    public bool IsPlayerStatus => Kind == WidgetKind.PlayerStatus;

    public bool IsPlayerEquipment => Kind == WidgetKind.PlayerEquipment;

    public bool IsPlayerWindowWidget => Kind is WidgetKind.PlayerInfo
        or WidgetKind.PlayerStatus
        or WidgetKind.PlayerEquipment
        or WidgetKind.DamageContribution
        or WidgetKind.DpsGraph
        or WidgetKind.HealingContribution
        or WidgetKind.HpsGraph;

    public bool HasOpenPlayerWindows => IsPlayerWindowWidget && OpenPlayerWindowCount > 0;

    public MeterWidgetSettingsConfig GetMeterSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedMeter(Kind, _meter);
    }

    public MetricTimelineWidgetSettingsConfig GetMetricTimelineSettingsSnapshot()
    {
        return WidgetConfigDefaults.CloneNormalizedMetricTimeline(_metricTimeline);
    }

    public string StateText => State == WidgetState.Running
        ? LocalizationManager.Instance.GetString("Widget_State_Running")
        : LocalizationManager.Instance.GetString("Widget_State_Stopped");

    public bool IsRunning => State == WidgetState.Running;

    public event Action<WidgetKind, long>? PlayerWindowRequested;

    public event EventHandler? PlayerWindowPresentationChanged;

    public event EventHandler? MeterSettingsChanged;

    public WidgetListItemViewModel()
    {
        PlayerListEntries = new ReadOnlyObservableCollection<PlayerListEntry>(_playerListEntries);
    }

    public void RefreshLocalizedText()
    {
        DisplayName = LocalizationManager.Instance.GetString(DisplayNameResourceKey);
        OnPropertyChanged(nameof(StateText));
        SynchronizePlayerListEntries(resetEntries: false);
        RaisePlayerWindowPresentationChanged();
    }

    public WidgetConfig CreateWidgetConfig()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            State = State,
            Theme = _theme.Clone(),
            Meter = WidgetConfigDefaults.SupportsMeterSettings(Kind)
                ? _meter.Clone()
                : null,
            MetricTimeline = WidgetConfigDefaults.SupportsMetricTimelineSettings(Kind)
                ? _metricTimeline.Clone()
                : null
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
        if (WidgetConfigDefaults.SupportsMeterSettings(Kind))
        {
            _meter = WidgetConfigDefaults.CloneNormalizedMeter(Kind, config.Meter);
        }

        if (WidgetConfigDefaults.SupportsMetricTimelineSettings(Kind))
        {
            _metricTimeline = WidgetConfigDefaults.CloneNormalizedMetricTimeline(config.MetricTimeline);
            RaisePlayerWindowPresentationChanged();
        }

        SynchronizePlayerListEntries(resetEntries: false);
        MeterSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyMeterSettingsPreview(MeterWidgetSettingsConfig meter)
    {
        if (!WidgetConfigDefaults.SupportsMeterSettings(Kind))
        {
            return;
        }

        _meter = WidgetConfigDefaults.CloneNormalizedMeter(Kind, meter);
        SynchronizePlayerListEntries(resetEntries: false);
        MeterSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyMetricTimelineSettingsPreview(MetricTimelineWidgetSettingsConfig metricTimeline)
    {
        if (!WidgetConfigDefaults.SupportsMetricTimelineSettings(Kind))
        {
            return;
        }

        _metricTimeline = WidgetConfigDefaults.CloneNormalizedMetricTimeline(metricTimeline);
        RaisePlayerWindowPresentationChanged();
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

    public void UpdatePlayerRoster(
        IReadOnlyList<PlayerRosterEntry> playerRoster,
        string mapName,
        long mapGeneration)
    {
        if (!IsPlayerList)
        {
            return;
        }

        MapName = mapName ?? string.Empty;
        _playerRoster = playerRoster;

        var resetEntries = _playerListMapGeneration != mapGeneration;
        _playerListMapGeneration = mapGeneration;
        SynchronizePlayerListEntries(resetEntries);
    }

    public void SetOpenPlayerWindowCount(int count)
    {
        OpenPlayerWindowCount = Math.Max(count, 0);
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
    private void RequestPlayerInfo(PlayerListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerInfo, player);
    }

    [RelayCommand]
    private void RequestPlayerStatus(PlayerListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerStatus, player);
    }

    [RelayCommand]
    private void RequestPlayerEquipment(PlayerListEntry? player)
    {
        RequestPlayerWindow(WidgetKind.PlayerEquipment, player);
    }

    partial void OnStateChanged(WidgetState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsRunning));
    }

    partial void OnOpenPlayerWindowCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasOpenPlayerWindows));
    }

    private void RequestPlayerWindow(WidgetKind kind, PlayerListEntry? player)
    {
        if (!IsPlayerList || player is null)
        {
            return;
        }

        PlayerWindowRequested?.Invoke(kind, player.CharacterId);
    }

    private void SynchronizePlayerListEntries(bool resetEntries)
    {
        if (!IsPlayerList)
        {
            return;
        }

        if (resetEntries)
        {
            foreach (var entry in _playerListEntries)
            {
                entry.IsPlayerSelectionMenuOpen = false;
            }

            _playerListEntriesByCharacterId.Clear();
            _playerListEntries.Clear();
        }
        else
        {
            var activeCharacterIds = _playerRoster
                .Select(entry => entry.CharacterId)
                .ToHashSet();

            for (var index = _playerListEntries.Count - 1; index >= 0; index--)
            {
                var entry = _playerListEntries[index];
                if (activeCharacterIds.Contains(entry.CharacterId))
                {
                    continue;
                }

                entry.IsPlayerSelectionMenuOpen = false;
                _playerListEntriesByCharacterId.Remove(entry.CharacterId);
                _playerListEntries.RemoveAt(index);
            }
        }

        var globalSettings = ConfigManager.Instance.GetSettingsSnapshot();
        var playerNameDisplayMode = (PlayerNameDisplayMode)globalSettings.PlayerNameDisplayModeIndex;

        for (var targetIndex = 0; targetIndex < _playerRoster.Count; targetIndex++)
        {
            var player = _playerRoster[targetIndex];
            if (!_playerListEntriesByCharacterId.TryGetValue(player.CharacterId, out var entry))
            {
                entry = PlayerListEntry.Create(player, _meter, playerNameDisplayMode);
                _playerListEntriesByCharacterId.Add(player.CharacterId, entry);
                _playerListEntries.Insert(targetIndex, entry);
                continue;
            }

            entry.Update(player, _meter, playerNameDisplayMode);

            if (_playerListEntries[targetIndex].CharacterId == player.CharacterId)
            {
                continue;
            }

            var currentIndex = FindPlayerListEntryIndex(player.CharacterId, targetIndex + 1);
            if (currentIndex >= 0)
            {
                _playerListEntries.Move(currentIndex, targetIndex);
            }
        }
    }

    private int FindPlayerListEntryIndex(long characterId, int startIndex)
    {
        for (var index = startIndex; index < _playerListEntries.Count; index++)
        {
            if (_playerListEntries[index].CharacterId == characterId)
            {
                return index;
            }
        }

        return -1;
    }

    private void RaisePlayerWindowPresentationChanged()
    {
        if (IsPlayerWindowWidget)
        {
            PlayerWindowPresentationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
