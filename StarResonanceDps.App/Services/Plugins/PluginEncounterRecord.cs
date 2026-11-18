// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインに渡す回1つの読み口。作ったときの回を持ち、本体が別の回へ移った後もその回を読む。
///
/// <para>
/// 実行中の回は読むたびに今の値を写す。DB から読んだ回は値が変わらないので、Info・参加者の行(伏せる前)・敵は最初に読んだときに作って持つ。
/// 参加者の行の <see cref="PlayerRosterEntry.IsSelf"/> と名前の伏せ方は、呼ぶたびにその時点のログイン中のキャラと表示方法で当て、
/// どちらも前の呼び出しと同じなら前に写した一覧を返す。持ち物は読み口と一緒に手放す。
/// </para>
///
/// <para>
/// プラグインの値は、そのプラグインの鍵(アセンブリ名)の分だけを読む。どのスレッドからも呼べる。
/// </para>
/// </summary>
internal sealed class PluginEncounterRecord : IPluginEncounterRecord
{
    private readonly Encounter _encounter;
    private readonly string _pluginKey;
    private readonly object _gate = new();

    private PluginEncounterInfo? _historyInfo;
    private IReadOnlyList<PlayerRosterEntry>? _recordedPlayers;
    private IReadOnlyList<PluginPlayer>? _players;
    private long _playersSelfCharacterId;
    private PlayerNameDisplayMode _playersNameDisplayMode;
    private IReadOnlyList<PluginRecordedMonster>? _historyMonsters;

    public PluginEncounterRecord(Encounter encounter, string pluginKey)
    {
        _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        _pluginKey = pluginKey ?? throw new ArgumentNullException(nameof(pluginKey));
    }

    public long EncounterKey => _encounter.RuntimeKey;

    /// <summary>保存の印を先に読む(印より前に保存の番号が書いてある)。実行中の回は保存した後に番号が付く。</summary>
    public ulong? HistoryId => _encounter.IsPersisted ? _encounter.EncounterId : null;

    public bool IsHistory => _encounter.IsFromHistory;

    public PluginEncounterInfo Info
    {
        get
        {
            if (!_encounter.IsFromHistory)
            {
                return PluginGameDataSource.ToPluginInfo(_encounter);
            }

            lock (_gate)
            {
                return _historyInfo ??= PluginGameDataSource.ToPluginInfo(_encounter);
            }
        }
    }

    public PluginEncounterSnapshot GetTotals()
    {
        return PluginGameDataSource.ToPluginEncounter(MeterSnapshotProvider.GetEncounterTotals(_encounter));
    }

    public IReadOnlyList<PluginSkillTotals> GetSkillTotals(long characterId, PluginCombatKind kind)
    {
        var meterKind = PluginGameDataSource.ToMeterKind(kind);
        return PluginGameDataSource.ToPluginSkills(MeterSnapshotProvider.GetPlayerSkillTable(_encounter, meterKind, characterId));
    }

    public TimeSpan? ToCombatOffset(DateTime utc)
    {
        return _encounter.ToCombatOffset(utc);
    }

    /// <summary>
    /// DB から読んだ回の参加者。保存した値だけで作った行(今のパーティと控えを重ねない)を最初の呼び出しで持ち、
    /// 呼ぶたびにログイン中のキャラ(<see cref="AppState.PlayerUID"/>)と名前の表示方法で写し直す。実行中の回は null。
    /// </summary>
    public IReadOnlyList<PluginPlayer>? GetPlayers()
    {
        if (!_encounter.IsFromHistory)
        {
            return null;
        }

        var selfCharacterId = AppState.PlayerUID;
        var nameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;
        lock (_gate)
        {
            _recordedPlayers ??= MeterSnapshotProvider.GetRecordedPlayers(_encounter);
            if (_players is null
                || _playersSelfCharacterId != selfCharacterId
                || _playersNameDisplayMode != nameDisplayMode)
            {
                _players = _recordedPlayers
                    .Select(entry => PluginGameDataSource.ToPluginPlayer(PlayerRosterPresentationStore.ApplyNameDisplay(
                        entry with { IsSelf = selfCharacterId != 0 && entry.CharacterId == selfCharacterId },
                        nameDisplayMode)))
                    .ToArray();
                _playersSelfCharacterId = selfCharacterId;
                _playersNameDisplayMode = nameDisplayMode;
            }

            return _players;
        }
    }

    public IReadOnlyList<PluginRecordedMonster> GetMonsters()
    {
        if (!_encounter.IsFromHistory)
        {
            return PluginGameDataSource.ToPluginMonsters(MeterSnapshotProvider.GetRecordedMonsters(_encounter));
        }

        lock (_gate)
        {
            return _historyMonsters ??= PluginGameDataSource.ToPluginMonsters(MeterSnapshotProvider.GetRecordedMonsters(_encounter));
        }
    }

    public PluginBuffIntervals? GetPlayerBuffIntervals(long characterId)
    {
        return PluginGameDataSource.ToPluginBuffIntervals(MeterSnapshotProvider.GetPlayerBuffIntervals(_encounter, characterId));
    }

    /// <summary>
    /// DB から読んだ回のその人のビルド。<see cref="GetPlayers"/> と同じ保存した行と、回に保存した技の一覧から作る。
    /// 因子とモジュール効果は行に保存した値(<see cref="PlayerRosterEntry.RecordedSelf"/>、保存したときにログインしていたキャラの行だけが持つ)。
    /// 実行中の回と、回にいない人は null。
    /// </summary>
    public PluginPlayerBuild? GetPlayerBuild(long characterId)
    {
        if (!_encounter.IsFromHistory)
        {
            return null;
        }

        PlayerRosterEntry? row;
        lock (_gate)
        {
            _recordedPlayers ??= MeterSnapshotProvider.GetRecordedPlayers(_encounter);
            row = _recordedPlayers.FirstOrDefault(entry => entry.CharacterId == characterId);
        }

        return row is null
            ? null
            : PluginGameDataSource.ToPluginBuild(
                row,
                MeterSnapshotProvider.GetRecordedSkillLevels(_encounter, characterId),
                row.RecordedSelf?.Factors,
                row.RecordedSelf?.ModuleEffects);
    }

    public IReadOnlyList<PluginSkillActivation> GetSkillActivations(long characterId, int startIndex = 0)
    {
        var activations = MeterSnapshotProvider.GetPlayerSkillActivationRecords(_encounter, characterId, startIndex);
        var records = new PluginSkillActivation[activations.Length];
        for (var index = 0; index < activations.Length; index++)
        {
            records[index] = PluginCombatEventConverter.ToSkillActivation(activations[index]);
        }

        return records;
    }

    public IReadOnlyList<PluginTakenHit> GetTakenHits(int startIndex = 0)
    {
        var observations = MeterSnapshotProvider.GetTakenHitObservations(_encounter, startIndex);
        var records = new PluginTakenHit[observations.Length];
        for (var index = 0; index < observations.Length; index++)
        {
            records[index] = PluginCombatEventConverter.ToTakenHit(observations[index]);
        }

        return records;
    }

    public IReadOnlyList<PluginCombatLogRecord> GetCombatLog(int startIndex = 0)
    {
        var observations = MeterSnapshotProvider.GetCombatLogObservations(_encounter, startIndex);
        var records = new PluginCombatLogRecord[observations.Length];
        for (var index = 0; index < observations.Length; index++)
        {
            records[index] = PluginCombatEventConverter.ToCombatLogRecord(observations[index]);
        }

        return records;
    }

    public PluginPerSecondTotals? GetPerSecondTotals(long characterId, PluginCombatKind kind)
    {
        var meterKind = PluginGameDataSource.ToMeterKind(kind);
        return MeterSnapshotProvider.GetPlayerPerSecondTotals(_encounter, meterKind, characterId) is { } totals
            ? new PluginPerSecondTotals(totals.Elapsed, totals.IsClosed, totals.Totals)
            : null;
    }

    /// <summary>配列は書き込みのたびに別のものに替わり、持っている配列は書き換えないので、写さずに渡す。</summary>
    public ReadOnlyMemory<byte>? GetPluginData(string key)
    {
        PluginHistoryService.ThrowIfInvalidDataKey(key, nameof(key));
        return _encounter.PluginData.Get(_pluginKey, key) is { } data
            ? new ReadOnlyMemory<byte>(data)
            : null;
    }
}
