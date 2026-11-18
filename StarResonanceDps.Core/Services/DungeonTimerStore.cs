// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// ゲームのダンジョンの時計の控え(<c>DungeonSyncData.timerInfo</c>)。
///
/// <para>
/// 全体の同期で丸ごと置き換え、差分では届いた項目だけを重ねる。種類が Null か開始が0なら「時計なし」。
/// 時計はダンジョンの開始の通知とは別の差分で、少し後に届く(届かないダンジョンもある)。
/// </para>
///
/// <para>
/// 止まったかは、止まった時点の値(<c>pauseTime</c>)が0以外になった・0に戻ったで見分ける。止まった区間の端はその差分の到着時刻(UTC)で、
/// ほかの記録と同じ時刻の基準になる。区間は開始時刻が変わったら捨てる。ダンジョンの時計の回(<see cref="Encounter.ClockStartUtc"/>)が経過から引く。
/// </para>
///
/// <para>
/// 差分を重ねて持つので、マップ移動・キャプチャの停止・ログアウト・ダンジョンの状態 Null で消す。終わりの後はゲームが時計を空にする。
/// </para>
/// </summary>
public sealed class DungeonTimerStore
{
    private static readonly Lazy<DungeonTimerStore> LazyInstance = new(() => new DungeonTimerStore());

    private readonly object _sync = new();

    private int _type;
    private int _startTime;
    private int _dungeonTimes;
    private int _direction;
    private int _pauseTime;
    private int _pauseTotalTime;

    /// <summary>終わった止まった区間(古い順)。差し替えて持つので、読んだ一覧は後から変わらない。</summary>
    private DungeonTimerPause[] _pauses = [];

    private DateTime? _pausedSinceUtc;

    private DungeonTimerStore()
    {
    }

    public static DungeonTimerStore Instance => LazyInstance.Value;

    /// <summary>控えが変わったとき。パケットのスレッドで上がる。</summary>
    public event EventHandler? Changed;

    /// <summary>ダンジョンの全体の同期の時計を当てる(丸ごと置き換え)。</summary>
    public void ApplyFull(Zproto.DungeonTimerInfo? timer, DateTime arrivalUtc)
    {
        Apply(
            arrivalUtc,
            () =>
            {
                _type = timer is null ? 0 : (int)timer.Type;
                _startTime = timer?.StartTime ?? 0;
                _dungeonTimes = timer?.DungeonTimes ?? 0;
                _direction = timer is null ? 0 : (int)timer.Direction;
                _pauseTime = timer?.PauseTime ?? 0;
                _pauseTotalTime = timer?.PauseTotalTime ?? 0;
            });
    }

    /// <summary>ダンジョンの差分の時計を当てる(届いた項目だけ)。</summary>
    public void ApplyDirty(Protocols.Game.Binary.DungeonTimerInfo? timer, DateTime arrivalUtc)
    {
        if (timer is null)
        {
            return;
        }

        Apply(
            arrivalUtc,
            () =>
            {
                _type = timer.TimerType is { } type ? (int)type : _type;
                _startTime = timer.StartTime ?? _startTime;
                _dungeonTimes = timer.DungeonTimes ?? _dungeonTimes;
                _direction = timer.Direction is { } direction ? (int)direction : _direction;
                _pauseTime = timer.PauseTime ?? _pauseTime;
                _pauseTotalTime = timer.PauseTotalTime ?? _pauseTotalTime;
            });
    }

    /// <summary>今の時計の値。時計なしなら null。</summary>
    public DungeonTimerSnapshot? Current
    {
        get
        {
            lock (_sync)
            {
                return HasTimerLocked()
                    ? new DungeonTimerSnapshot(
                        _startTime,
                        _dungeonTimes,
                        _direction == (int)Zproto.EDungeonTimerDirection.DungeonTimerDirectionDown,
                        _pauseTotalTime,
                        _pauseTime > 0 ? _pauseTime : null,
                        _pauses,
                        _pausedSinceUtc)
                    : null;
            }
        }
    }

    /// <summary>
    /// 開始時刻(サーバの Unix 秒)を、この端末の時計の UTC に直す(<see cref="SkillCooldownStateStore.TryGetClientServerDeltaMilliseconds"/> の差を足す)。
    /// サーバの時刻をまだ受け取っていなければ false。
    /// </summary>
    public static bool TryToLocalUtc(long startServerUnixSeconds, out DateTime startUtc)
    {
        if (!SkillCooldownStateStore.TryGetClientServerDeltaMilliseconds(out var deltaMilliseconds))
        {
            startUtc = default;
            return false;
        }

        startUtc = DateTimeOffset.FromUnixTimeMilliseconds(startServerUnixSeconds * 1000L + deltaMilliseconds).UtcDateTime;
        return true;
    }

    public void Clear()
    {
        bool changed;
        lock (_sync)
        {
            changed = HasTimerLocked() || _pauses.Length > 0 || _pausedSinceUtc != null;
            _type = 0;
            _startTime = 0;
            _dungeonTimes = 0;
            _direction = 0;
            _pauseTime = 0;
            _pauseTotalTime = 0;
            _pauses = [];
            _pausedSinceUtc = null;
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Apply(DateTime arrivalUtc, Action write)
    {
        bool changed;
        lock (_sync)
        {
            var before = (_type, _startTime, _dungeonTimes, _direction, _pauseTime, _pauseTotalTime);
            write();
            changed = before != (_type, _startTime, _dungeonTimes, _direction, _pauseTime, _pauseTotalTime);

            if (!HasTimerLocked() || before._startTime != _startTime)
            {
                _pauses = [];
                _pausedSinceUtc = null;
            }

            if (HasTimerLocked())
            {
                if (_pauseTime > 0 && _pausedSinceUtc is null)
                {
                    _pausedSinceUtc = arrivalUtc;
                }
                else if (_pauseTime <= 0 && _pausedSinceUtc is { } pausedSince)
                {
                    if (arrivalUtc > pausedSince)
                    {
                        _pauses = [.. _pauses, new DungeonTimerPause(pausedSince, arrivalUtc)];
                    }

                    _pausedSinceUtc = null;
                }
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool HasTimerLocked()
    {
        return _type != (int)Zproto.EDungeonTimerType.DungeonTimerTypeNull && _startTime > 0;
    }
}

/// <summary>ゲームのダンジョンの時計の値(<see cref="DungeonTimerStore.Current"/>)。</summary>
/// <param name="StartServerUnixSeconds">ゲームが送った開始時刻(サーバの Unix 秒)。</param>
/// <param name="LimitSeconds">制限時間(秒)。</param>
/// <param name="CountsDown">制限時間まで減っていくか。</param>
/// <param name="PausedTotalSeconds">止まっていた秒の合計(終わった停止だけ)。</param>
/// <param name="PausedAtSeconds">止まっている間の、止まった時点の値(秒)。動いている間は null。</param>
/// <param name="Pauses">終わった止まった区間(到着時刻、UTC、古い順)。</param>
/// <param name="PausedSinceUtc">今止まっていれば、止まった差分の到着時刻(UTC)。</param>
public sealed record DungeonTimerSnapshot(
    long StartServerUnixSeconds,
    int LimitSeconds,
    bool CountsDown,
    int PausedTotalSeconds,
    int? PausedAtSeconds,
    IReadOnlyList<DungeonTimerPause> Pauses,
    DateTime? PausedSinceUtc);

/// <summary>ゲームのダンジョンの時計が止まっていた区間(止まった差分と動いた差分の到着時刻、UTC)。</summary>
public readonly record struct DungeonTimerPause(DateTime StartUtc, DateTime EndUtc);
