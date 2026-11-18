// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグイン1つに渡す、保存した回(集計タブの履歴)の口。読み書きと選択は Core(<see cref="EncounterHistoryProvider"/>・<see cref="EncounterManager"/>)が行い、
/// ここはプラグインの鍵(アセンブリ名)で値を分け、止めたプラグインの要求を受けない。
///
/// <para>
/// 一覧・回の読み込み・保存した回への書き込みは、プラグインごとに1つずつ順番に走らせる(<see cref="_queue"/>)。
/// 止めた後(<see cref="Release"/>)とアプリを閉じた後は何もしない。止めるときは、このプラグインの保持(<see cref="KeepHistoryOpen"/>)を全部外し、
/// 開く要求(<see cref="OpenAsync"/>)がまだ最新なら取り下げる。順番待ちは捨てない(待っている呼び出しは、順番を取った後に止めた印を見て終わる)。
/// </para>
/// </summary>
internal sealed class PluginHistoryService : IPluginHistoryService
{
    private readonly PluginCallGuard _guard;
    private readonly string _pluginKey;
    private readonly PluginGameDataSource _source;
    private readonly SemaphoreSlim _queue = new(1, 1);
    private readonly object _gate = new();

    /// <summary>このプラグインが取って、まだ外していない保持。止めるときに全部外す。</summary>
    private readonly HashSet<KeepHandle> _keeps = [];

    /// <summary>このプラグインが最後に出した開く要求の番号(<see cref="EncounterHistoryProvider.SelectAsync(ulong, out long)"/>)。止めるときの取り下げに使う。</summary>
    private long _openRequest;

    private volatile bool _isReleased;

    public PluginHistoryService(PluginCallGuard guard, string pluginKey, PluginGameDataSource source)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _pluginKey = pluginKey ?? throw new ArgumentNullException(nameof(pluginKey));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.HistoryOpenFailedChanged += Source_HistoryOpenFailedChanged;
    }

    public bool HasOpenFailed => EncounterHistoryProvider.HasLoadFailed;

    public event EventHandler? OpenFailedChanged;

    private bool IsUnavailable => _isReleased || EncounterHistoryProvider.IsShutDown;

    public Task<PluginHistoryListResult> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        return RunQueuedAsync(
            async () =>
            {
                var read = await EncounterHistoryProvider.LoadSummariesDetachedAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (read.Status != HistoryReadStatus.Succeeded)
                {
                    return new PluginHistoryListResult(ToPluginStatus(read.Status), Array.Empty<PluginHistoryEntry>());
                }

                var summaries = read.Value ?? throw new InvalidOperationException("The history list was read without a value.");
                var entries = summaries
                    .Select(encounter => new PluginHistoryEntry(
                        encounter.EncounterId,
                        PluginGameDataSource.ToPluginInfo(encounter),
                        encounter.ClockStartUtc is { } start ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : null,
                        encounter.ReadCombatClock().Elapsed))
                    .ToArray();
                return new PluginHistoryListResult(PluginHistoryStatus.Succeeded, entries);
            },
            new PluginHistoryListResult(PluginHistoryStatus.Unavailable, Array.Empty<PluginHistoryEntry>()),
            cancellationToken);
    }

    public Task<PluginHistoryLoadResult> LoadAsync(ulong historyId, CancellationToken cancellationToken = default)
    {
        return RunQueuedAsync(
            async () =>
            {
                var read = await EncounterHistoryProvider.LoadDetachedAsync(historyId).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (read.Status != HistoryReadStatus.Succeeded)
                {
                    return new PluginHistoryLoadResult(ToPluginStatus(read.Status), null);
                }

                var encounter = read.Value ?? throw new InvalidOperationException($"Encounter {historyId} was read without a value.");
                return new PluginHistoryLoadResult(PluginHistoryStatus.Succeeded, new PluginEncounterRecord(encounter, _pluginKey));
            },
            new PluginHistoryLoadResult(PluginHistoryStatus.Unavailable, null),
            cancellationToken);
    }

    /// <summary>
    /// 選ぶのは集計タブと同じ口(<see cref="EncounterHistoryProvider.SelectAsync(ulong, out long)"/>)で、順番待ちには入れない。
    /// 出した要求の番号を控え、止めたときにまだ最新なら取り下げる。要求を出すのと控えるのは、止める処理と同じロックの中で行う。
    /// </summary>
    public Task<PluginHistoryOpenResult> OpenAsync(ulong historyId)
    {
        Task<HistorySelectResult> load;
        lock (_gate)
        {
            if (IsUnavailable)
            {
                return Task.FromResult(PluginHistoryOpenResult.Unavailable);
            }

            load = EncounterHistoryProvider.SelectAsync(historyId, out var request);
            if (request == 0)
            {
                // アプリを閉じている間は要求を出さない。
                return Task.FromResult(PluginHistoryOpenResult.Unavailable);
            }

            _openRequest = request;
        }

        return ToPluginOpenResultAsync(load);
    }

    public Task ReturnToLiveAsync()
    {
        return IsUnavailable
            ? Task.CompletedTask
            : EncounterHistoryProvider.SelectLiveAsync();
    }

    public IDisposable KeepHistoryOpen()
    {
        lock (_gate)
        {
            if (_isReleased)
            {
                return NoKeep.Instance;
            }

            var handle = new KeepHandle(this, EncounterHistoryProvider.KeepOpen());
            _keeps.Add(handle);
            return handle;
        }
    }

    public bool TrySetEncounterData(long encounterKey, string key, ReadOnlyMemory<byte> data)
    {
        ThrowIfInvalidDataKey(key, nameof(key));
        ThrowIfTooLong(data, nameof(data));
        if (_isReleased || !EncounterManager.TryGetUnsavedEncounter(encounterKey, out var encounter))
        {
            return false;
        }

        var result = encounter.PluginData.TrySet(
            _pluginKey,
            key,
            data.ToArray(),
            PluginEncounterDataLimits.MaxKeys,
            PluginEncounterDataLimits.MaxTotalBytes);
        return result switch
        {
            EncounterPluginDataSetResult.Stored => true,
            EncounterPluginDataSetResult.Sealed => false,
            EncounterPluginDataSetResult.TooManyKeys => throw CreateTooManyKeysException(),
            EncounterPluginDataSetResult.TooLarge => throw CreateTooLargeException(),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown result of storing plugin data."),
        };
    }

    /// <summary>引数は呼んだ時点で確かめて写す(順番待ちの間にプラグインが配列を書き換えても、呼んだときの値を書く)。</summary>
    public Task<PluginHistoryStatus> SetSavedEncounterDataAsync(
        ulong historyId,
        string key,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        ThrowIfInvalidDataKey(key, nameof(key));
        ThrowIfTooLong(data, nameof(data));
        var copy = data.ToArray();
        return RunQueuedAsync(
            async () =>
            {
                var status = await EncounterHistoryProvider.SetSavedPluginDataAsync(
                    historyId,
                    _pluginKey,
                    key,
                    copy,
                    PluginEncounterDataLimits.MaxKeys,
                    PluginEncounterDataLimits.MaxTotalBytes).ConfigureAwait(false);
                return status switch
                {
                    SavedPluginDataStatus.Stored => PluginHistoryStatus.Succeeded,
                    SavedPluginDataStatus.NotFound => PluginHistoryStatus.NotFound,
                    SavedPluginDataStatus.Failed => PluginHistoryStatus.Failed,
                    SavedPluginDataStatus.Unavailable => PluginHistoryStatus.Unavailable,
                    SavedPluginDataStatus.TooManyKeys => throw CreateTooManyKeysException(),
                    SavedPluginDataStatus.TooLarge => throw CreateTooLargeException(),
                    _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown result of writing saved plugin data."),
                };
            },
            PluginHistoryStatus.Unavailable,
            cancellationToken);
    }

    /// <summary>
    /// プラグインを止めた・終えたときに呼ぶ。以後の呼び出しを受けず、本体の知らせから外し、開く要求がまだ最新なら取り下げ、保持を全部外す。
    /// </summary>
    public void Release()
    {
        KeepHandle[] keeps;
        lock (_gate)
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            EncounterHistoryProvider.WithdrawSelection(_openRequest);
            _openRequest = 0;
            keeps = _keeps.ToArray();
            _keeps.Clear();
        }

        _source.HistoryOpenFailedChanged -= Source_HistoryOpenFailedChanged;
        foreach (var keep in keeps)
        {
            keep.Dispose();
        }
    }

    /// <summary>回に残す値のキーの決まり(<see cref="PluginIdRules"/>)。合わなければ <see cref="ArgumentException"/>。</summary>
    internal static void ThrowIfInvalidDataKey(string? key, string paramName)
    {
        if (!PluginIdRules.IsValid(key))
        {
            throw new ArgumentException($"The key '{key}' must be 1 to 128 ASCII letters, digits, '.', '-' or '_'.", paramName);
        }
    }

    private static void ThrowIfTooLong(ReadOnlyMemory<byte> data, string paramName)
    {
        if (data.Length > PluginEncounterDataLimits.MaxTotalBytes)
        {
            throw new ArgumentException(
                $"The data is {data.Length} bytes, longer than the limit of {PluginEncounterDataLimits.MaxTotalBytes} bytes.",
                paramName);
        }
    }

    private static InvalidOperationException CreateTooManyKeysException()
    {
        return new InvalidOperationException(
            $"The plugin's data for the encounter would exceed {PluginEncounterDataLimits.MaxKeys} keys.");
    }

    private static InvalidOperationException CreateTooLargeException()
    {
        return new InvalidOperationException(
            $"The plugin's data for the encounter would exceed {PluginEncounterDataLimits.MaxTotalBytes} bytes in total.");
    }

    /// <summary>
    /// 順番待ちを取ってから走らせる。止めた後とアプリを閉じた後は走らせず <paramref name="unavailable"/>。
    /// 取り消しは待つ間と走らせる前に見る(待つ間に取り消されると <see cref="OperationCanceledException"/>)。
    /// </summary>
    private async Task<T> RunQueuedAsync<T>(Func<Task<T>> work, T unavailable, CancellationToken cancellationToken)
    {
        if (IsUnavailable)
        {
            return unavailable;
        }

        await _queue.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsUnavailable)
            {
                return unavailable;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await work().ConfigureAwait(false);
        }
        finally
        {
            _queue.Release();
        }
    }

    private static async Task<PluginHistoryOpenResult> ToPluginOpenResultAsync(Task<HistorySelectResult> load)
    {
        var result = await load.ConfigureAwait(false);
        return result switch
        {
            HistorySelectResult.Opened => PluginHistoryOpenResult.Opened,
            HistorySelectResult.NotFound => PluginHistoryOpenResult.NotFound,
            HistorySelectResult.Superseded => PluginHistoryOpenResult.Superseded,
            HistorySelectResult.Failed => PluginHistoryOpenResult.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown result of opening a history encounter."),
        };
    }

    private static PluginHistoryStatus ToPluginStatus(HistoryReadStatus status)
    {
        return status switch
        {
            HistoryReadStatus.Succeeded => PluginHistoryStatus.Succeeded,
            HistoryReadStatus.NotFound => PluginHistoryStatus.NotFound,
            HistoryReadStatus.Failed => PluginHistoryStatus.Failed,
            HistoryReadStatus.Unavailable => PluginHistoryStatus.Unavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown result of reading the history."),
        };
    }

    private void Source_HistoryOpenFailedChanged(object? sender, EventArgs e)
    {
        _guard.TryInvoke("History open failure notification", () => OpenFailedChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>本体の保持(<see cref="EncounterHistoryProvider.KeepOpen"/>)を包む。外すのは1回だけ(プラグインの Dispose と、止めたときの一括のどちらが先でも)。</summary>
    private sealed class KeepHandle : IDisposable
    {
        private readonly PluginHistoryService _owner;
        private readonly IDisposable _hostKeep;
        private int _isDisposed;

        public KeepHandle(PluginHistoryService owner, IDisposable hostKeep)
        {
            _owner = owner;
            _hostKeep = hostKeep;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            lock (_owner._gate)
            {
                _owner._keeps.Remove(this);
            }

            _hostKeep.Dispose();
        }
    }

    /// <summary>止めた後に返す、何も保持しない物。</summary>
    private sealed class NoKeep : IDisposable
    {
        public static readonly NoKeep Instance = new();

        public void Dispose()
        {
        }
    }
}
