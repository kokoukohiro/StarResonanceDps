using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class PlayerRosterPresentationStore
{
    /// <summary>名前を伏せるときの表示。<b>UIDもまとめてこれ1つに置き換える。</b></summary>
    public const string HiddenPlayerName = "*****";

    private static readonly Lazy<PlayerRosterPresentationStore> LazyInstance = new(() => new PlayerRosterPresentationStore());

    private readonly object _sync = new();
    private readonly PlayerRosterStore _sourceStore = PlayerRosterStore.Instance;

    private IReadOnlyList<PlayerRosterEntry> _snapshot = Array.AsReadOnly(Array.Empty<PlayerRosterEntry>());
    private string _mapName = string.Empty;
    private long _mapGeneration;
    private PlayerNameDisplayMode _nameDisplayMode = PlayerNameDisplayMode.Show;

    private PlayerRosterPresentationStore()
    {
        _sourceStore.RosterChanged += SourceStore_RosterChanged;

        var current = _sourceStore.Current;
        _snapshot = CreatePresentationSnapshot(current.Entries, _nameDisplayMode);
        _mapName = current.MapName;
        _mapGeneration = current.MapGeneration;
    }

    public static PlayerRosterPresentationStore Instance => LazyInstance.Value;

    public event EventHandler<PlayerRosterChangedEventArgs>? RosterChanged;

    public PlayerRosterSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return new PlayerRosterSnapshot(_snapshot, _mapName, _mapGeneration);
            }
        }
    }

    /// <summary>
    /// いまの名前の表示方法。ウィンドウのタイトルなど、
    /// ロスターの投影を通らない表示から使う。
    ///
    /// <para>
    /// 設定のプレビュー適用もここを通るので、<b>プレイヤー一覧と同じ瞬間に切り替わる</b>。
    /// </para>
    /// </summary>
    public PlayerNameDisplayMode NameDisplayMode
    {
        get
        {
            lock (_sync)
            {
                return _nameDisplayMode;
            }
        }
    }

    public void SetNameDisplayMode(PlayerNameDisplayMode mode)
    {
        mode = NormalizeNameDisplayMode(mode);

        PlayerRosterSnapshot changedSnapshot;
        lock (_sync)
        {
            if (_nameDisplayMode == mode)
            {
                return;
            }

            _nameDisplayMode = mode;

            var current = _sourceStore.Current;
            _snapshot = CreatePresentationSnapshot(current.Entries, _nameDisplayMode);
            _mapName = current.MapName;
            _mapGeneration = current.MapGeneration;
            changedSnapshot = new PlayerRosterSnapshot(_snapshot, _mapName, _mapGeneration);
        }

        RosterChanged?.Invoke(this, new PlayerRosterChangedEventArgs(changedSnapshot));
    }

    private void SourceStore_RosterChanged(object? sender, PlayerRosterChangedEventArgs e)
    {
        PlayerRosterSnapshot changedSnapshot;
        lock (_sync)
        {
            _snapshot = CreatePresentationSnapshot(e.Snapshot, _nameDisplayMode);
            _mapName = e.MapName;
            _mapGeneration = e.MapGeneration;
            changedSnapshot = new PlayerRosterSnapshot(_snapshot, _mapName, _mapGeneration);
        }

        RosterChanged?.Invoke(this, new PlayerRosterChangedEventArgs(changedSnapshot));
    }

    private static IReadOnlyList<PlayerRosterEntry> CreatePresentationSnapshot(
        IReadOnlyList<PlayerRosterEntry> source,
        PlayerNameDisplayMode nameDisplayMode)
    {
        var entries = source
            .Select(entry => entry with
            {
                Name = ShouldHideName(entry, nameDisplayMode)
                    ? HiddenPlayerName
                    : entry.Name ?? string.Empty
            })
            .ToArray();

        return Array.AsReadOnly(entries);
    }

    private static bool ShouldHideName(PlayerRosterEntry entry, PlayerNameDisplayMode nameDisplayMode)
    {
        return nameDisplayMode switch
        {
            PlayerNameDisplayMode.Hide => true,
            PlayerNameDisplayMode.HideOthers => !entry.IsSelf,
            _ => false
        };
    }

    private static PlayerNameDisplayMode NormalizeNameDisplayMode(PlayerNameDisplayMode mode)
    {
        return mode switch
        {
            PlayerNameDisplayMode.Show => PlayerNameDisplayMode.Show,
            PlayerNameDisplayMode.Hide => PlayerNameDisplayMode.Hide,
            PlayerNameDisplayMode.HideOthers => PlayerNameDisplayMode.HideOthers,
            _ => PlayerNameDisplayMode.Show
        };
    }
}
