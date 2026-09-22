using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerBuffListWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly PlayerBuffListKind _kind;

    /// <summary>行をクリックしたときにバフ・デバフカードを開く経路。</summary>
    private readonly Action<long, PlayerBuffListKind, string, int>? _openCard;

    private readonly ObservableCollection<PlayerBuffEntry> _entries = [];
    private readonly DispatcherTimer _refreshTimer;
    private BuffListWidgetSettingsConfig _settings;
    private bool _isDisposed;

    /// <summary>行のゲージの塗り。設定が変わったら作り直す。</summary>
    [ObservableProperty]
    private Brush _gaugeBrush = Brushes.Transparent;

    [ObservableProperty]
    private string _noDataText = string.Empty;

    [ObservableProperty]
    private bool _hasEntries;

    public PlayerBuffListWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        PlayerBuffListKind kind,
        Action<long, PlayerBuffListKind, string, int>? openCard = null)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _openCard = openCard;
        _settings = playerWidget.GetBuffListSettingsSnapshot();
        GaugeBrush = BuffListGaugeBrush.Create(_settings);
        Entries = new ReadOnlyObservableCollection<PlayerBuffEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        playerWidget.BuffListSettingsChanged += Widget_BuffListSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<PlayerBuffEntry> Entries { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        PlayerWidget.BuffListSettingsChanged -= Widget_BuffListSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>表示設定(保存かプレビュー)が変わった。色を作り直し、ゲージの長さも当て直す。</summary>
    private void Widget_BuffListSettingsChanged(object? sender, EventArgs e)
    {
        _settings = PlayerWidget.GetBuffListSettingsSnapshot();
        GaugeBrush = BuffListGaugeBrush.Create(_settings);

        var gaugeLengthSeconds = BuffListGaugeBrush.GetLengthSeconds(_settings);
        foreach (var entry in _entries)
        {
            entry.UpdateBar(gaugeLengthSeconds);
        }
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        Refresh();
    }

    [RelayCommand]
    private void OpenCard(PlayerBuffEntry? entry)
    {
        if (entry is null || SelectedCharacterId is not { } characterId)
        {
            return;
        }

        _openCard?.Invoke(characterId, _kind, entry.Key, entry.BaseId);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        if (SelectedCharacterId is not { } characterId)
        {
            SynchronizeEntries(Array.Empty<PlayerBuffSnapshot>());
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId);
        }

        SynchronizeEntries(MeterSnapshotProvider.GetPlayerBuffs(characterId, _kind));
    }

    private void SynchronizeEntries(IReadOnlyList<PlayerBuffSnapshot> snapshots)
    {
        var snapshotKeys = snapshots
            .Select(snapshot => snapshot.Key)
            .ToHashSet(StringComparer.Ordinal);

        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            if (snapshotKeys.Contains(_entries[index].Key))
            {
                continue;
            }

            _entries.RemoveAt(index);
        }

        for (var targetIndex = 0; targetIndex < snapshots.Count; targetIndex++)
        {
            var snapshot = snapshots[targetIndex];
            var existingIndex = FindEntryIndex(snapshot.Key);
            if (existingIndex < 0)
            {
                _entries.Insert(targetIndex, new PlayerBuffEntry(snapshot));
                continue;
            }

            var entry = _entries[existingIndex];
            entry.Update(snapshot);
            if (existingIndex != targetIndex)
            {
                _entries.Move(existingIndex, targetIndex);
            }
        }

        // 行を入れ替えた後にまとめて計算する。作った直後の行にも同じ長さが当たる。
        var gaugeLengthSeconds = BuffListGaugeBrush.GetLengthSeconds(_settings);
        foreach (var entry in _entries)
        {
            entry.UpdateBar(gaugeLengthSeconds);
        }
    }

    private int FindEntryIndex(string key)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (string.Equals(_entries[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
