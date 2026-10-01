using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class EntityBuffListWidgetViewModel
    : ViewModelBase, IEntityWidgetWindowViewModel, IBuffListWindowViewModel, IDisposable
{
    private readonly WidgetListItemViewModel _widget;
    private readonly EntityWindowTarget _target;
    private readonly PlayerBuffListKind _kind;

    /// <summary>行をクリックしたときにバフ・デバフカードを開く経路。</summary>
    private readonly Action<EntityWindowTarget, PlayerBuffListKind, string, int>? _openCard;

    private readonly ObservableCollection<PlayerBuffEntry> _entries = [];
    private readonly DispatcherTimer _refreshTimer;
    private BuffListWidgetSettingsConfig _settings;
    private bool _isDisposed;

    /// <summary>行のゲージの塗り。設定はプレイヤーから開いた一覧と同じウィジェットのものを使う。</summary>
    [ObservableProperty]
    private Brush _gaugeBrush = Brushes.Transparent;

    [ObservableProperty]
    private string _headerText = string.Empty;

    public EntityBuffListWidgetViewModel(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        PlayerBuffListKind kind,
        Action<EntityWindowTarget, PlayerBuffListKind, string, int>? openCard = null)
    {
        _widget = widget;
        _target = target;
        _kind = kind;
        _openCard = openCard;
        _settings = widget.GetBuffListSettingsSnapshot();
        GaugeBrush = BuffListGaugeBrush.Create(_settings);
        Entries = new ReadOnlyObservableCollection<PlayerBuffEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _target.Changed += Target_Changed;
        _widget.BuffListSettingsChanged += Widget_BuffListSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        RefreshPresentation();
        Refresh();
        _refreshTimer.Start();
    }

    public long EntityId => _target.EntityId;

    public Zproto.EEntityType? EntityType => _target.EntityType;

    public long EntityUuid => _target.EntityUuid;

    public bool IsEntityAcquired => _target.IsAcquired;

    public string TargetName => _target.Name;

    public ReadOnlyObservableCollection<PlayerBuffEntry> Entries { get; }

    public WidgetListItemViewModel Widget => _widget;

    public string CardMenuText => LocalizationManager.Instance.GetString(
        _kind == PlayerBuffListKind.Debuff
            ? "BuffList_Menu_DebuffCard"
            : "BuffList_Menu_BuffCard");

    public string HideMenuText => LocalizationManager.Instance.GetString(
        _kind == PlayerBuffListKind.Debuff
            ? "BuffList_Menu_HideDebuff"
            : "BuffList_Menu_HideBuff");

    public bool RepresentsEntity(long entityUuid)
    {
        return _target.Represents(entityUuid);
    }

    [RelayCommand]
    private void OpenCard(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _openCard?.Invoke(_target, _kind, entry.Key, entry.BaseId);
    }

    [RelayCommand]
    private void HideBuff(PlayerBuffEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _widget.HideBuffInList(entry.BaseId);
    }

    public bool TryApplyEntity(EntityListEntry entity)
    {
        return _target.TryApply(entity);
    }

    public void ReleaseEntity()
    {
        _target.Release();
    }

    public void RefreshPresentation()
    {
        HeaderText = string.IsNullOrWhiteSpace(_target.DisplayName)
            ? _widget.DisplayName
            : $"{_widget.DisplayName} - {_target.DisplayName}";
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _target.Changed -= Target_Changed;
        _target.Dispose();
        _widget.BuffListSettingsChanged -= Widget_BuffListSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>
    /// 表示設定(保存かプレビュー)が変わった。色を作り直し、行も作り直す
    /// (非表示のバフを効かせる。ゲージの長さもそこで当て直る)。
    /// </summary>
    private void Widget_BuffListSettingsChanged(object? sender, EventArgs e)
    {
        _settings = _widget.GetBuffListSettingsSnapshot();
        GaugeBrush = BuffListGaugeBrush.Create(_settings);
        Refresh();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Target_Changed(object? sender, EventArgs e)
    {
        RefreshPresentation();
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CardMenuText));
        OnPropertyChanged(nameof(HideMenuText));
        RefreshPresentation();
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        SynchronizeEntries(ExcludeHiddenBuffs(MeterSnapshotProvider.GetEntityBuffs(EntityUuid, _kind)));
    }

    /// <summary>設定の非表示の一覧にあるバフを除く。</summary>
    private IReadOnlyList<PlayerBuffSnapshot> ExcludeHiddenBuffs(IReadOnlyList<PlayerBuffSnapshot> snapshots)
    {
        if (_settings.HiddenBuffIds.Count == 0)
        {
            return snapshots;
        }

        return [.. snapshots.Where(snapshot => !_settings.HiddenBuffIds.Contains(snapshot.BaseId))];
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

            // 行が消えるならメニューも閉じる(消えた行に開いたまま残らないように)。
            _entries[index].IsMenuOpen = false;
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
