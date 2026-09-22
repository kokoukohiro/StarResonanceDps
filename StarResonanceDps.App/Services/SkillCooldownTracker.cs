using System.Collections.Concurrent;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public readonly record struct SkillCooldownDisplayState(
    int? AvailableCharges,
    double? CooldownRemainingSeconds);

public sealed class SkillCooldownTracker
{
    private const int LowerCdBuffBaseId = 2110034;
    private const double LowerCdAdditionalProgressRate = 0.5d;

    private static readonly Lazy<SkillCooldownTracker> LazyInstance = new(() => new SkillCooldownTracker());

    private readonly ConcurrentDictionary<SkillActivationKey, SkillActivationHistory> _activationHistories = new();
    private readonly ConcurrentDictionary<long, CooldownResetSchedule> _resetSchedules = new();
    private readonly object _subscriptionSync = new();

    private Encounter? _subscribedEncounter;
    private bool _isInitialized;

    private SkillCooldownTracker()
    {
    }

    public static SkillCooldownTracker Instance => LazyInstance.Value;

    public void Initialize()
    {
        lock (_subscriptionSync)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            EncounterManager.EncounterStart += EncounterManager_EncounterStart;
            SkillCooldownStateStore.CooldownsResetByGame += SkillCooldownStateStore_CooldownsResetByGame;
            SkillCooldownStateStore.CooldownsResetForPlayer += SkillCooldownStateStore_CooldownsResetForPlayer;
            AttachToCurrentEncounter();
        }
    }

    public void Shutdown()
    {
        lock (_subscriptionSync)
        {
            if (!_isInitialized)
            {
                return;
            }

            EncounterManager.EncounterStart -= EncounterManager_EncounterStart;
            SkillCooldownStateStore.CooldownsResetByGame -= SkillCooldownStateStore_CooldownsResetByGame;
            SkillCooldownStateStore.CooldownsResetForPlayer -= SkillCooldownStateStore_CooldownsResetForPlayer;
            if (_subscribedEncounter is not null)
            {
                _subscribedEncounter.SkillActivated -= Encounter_SkillActivated;
                _subscribedEncounter = null;
            }

            _isInitialized = false;
            _activationHistories.Clear();
            _resetSchedules.Clear();
        }
    }

    public SkillCooldownDisplayState GetDisplayState(
        long entityUuid,
        int skillId,
        double estimatedCooldownSeconds,
        bool isImagine,
        int maxCharges,
        double chargeCooldownSeconds)
    {
        var key = new SkillActivationKey(entityUuid, skillId);
        if (maxCharges > 1 && chargeCooldownSeconds > 0)
        {
            _activationHistories.TryGetValue(key, out var chargeHistory);
            var chargeState = IsSelf(entityUuid)
                ? GetSelfChargeState(entityUuid, skillId, maxCharges)
                : GetEstimatedChargeState(
                    entityUuid,
                    ExcludeBeforeReset(entityUuid, chargeHistory?.GetSnapshot() ?? []),
                    maxCharges,
                    chargeCooldownSeconds,
                    isImagine);
            double? cooldownSeconds = chargeState.AvailableCharges == 0
                && chargeState.NextChargeRemainingSeconds > 0
                    ? chargeState.NextChargeRemainingSeconds
                    : null;

            return new SkillCooldownDisplayState(
                chargeState.AvailableCharges,
                cooldownSeconds);
        }

        var remainingSeconds = IsSelf(entityUuid)
            ? SkillCooldownStateStore.GetSelfRemainingSeconds(entityUuid, skillId)
            : GetEstimatedRemainingSeconds(
                entityUuid,
                key,
                estimatedCooldownSeconds,
                isImagine);
        return new SkillCooldownDisplayState(null, remainingSeconds);
    }

    private double? GetEstimatedRemainingSeconds(
        long entityUuid,
        SkillActivationKey key,
        double estimatedCooldownSeconds,
        bool isImagine)
    {
        if (estimatedCooldownSeconds <= 0
            || !_activationHistories.TryGetValue(key, out var history)
            || history.GetLatest() is not { } activation
            || GetResetCutoff(entityUuid) is { } cutoff && activation.ActivationDateTime < cutoff)
        {
            return null;
        }

        var elapsedSeconds = Math.Max(
            DateTime.Now.Subtract(activation.ActivationDateTime).TotalSeconds,
            0d);
        if (isImagine)
        {
            elapsedSeconds += GetLowerCdOverlapSeconds(
                    entityUuid,
                    activation.Encounter,
                    activation.EncounterTime,
                    activation.Encounter?.GetDuration() ?? activation.EncounterTime)
                * LowerCdAdditionalProgressRate;
        }

        var remainingSeconds = estimatedCooldownSeconds - elapsedSeconds;
        return remainingSeconds > 0
            ? remainingSeconds
            : null;
    }

    /// <summary>
    /// 起動直後の状態へ戻したときに、他プレイヤーの推測値の元になる発動履歴を全員分破棄する。
    /// 自分の残CDはサーバ真値なのでここでは何もしない。
    /// </summary>
    private void SkillCooldownStateStore_CooldownsResetByGame()
    {
        _activationHistories.Clear();
        _resetSchedules.Clear();
    }

    /// <summary>
    /// ゲームがその人のクールダウンをリセットする時刻を控える。消えるのが付与より後のバフがあるので、履歴はここでは消さず、
    /// 読むときに「過ぎたリセットのうち最後の時刻」より前の発動を無視する(<see cref="GetResetCutoff"/>)。
    /// </summary>
    private void SkillCooldownStateStore_CooldownsResetForPlayer(long entityUuid, DateTime resetAt)
    {
        _resetSchedules.GetOrAdd(entityUuid, static _ => new CooldownResetSchedule()).Add(resetAt);
    }

    /// <summary>その人のクールダウンが最後にリセットされた時刻(いまより前のもの)。無ければ null。</summary>
    private DateTime? GetResetCutoff(long entityUuid)
    {
        return _resetSchedules.TryGetValue(entityUuid, out var schedule)
            ? schedule.GetLatestPassed(DateTime.Now)
            : null;
    }

    private void EncounterManager_EncounterStart(EncounterStartEventArgs e)
    {
        lock (_subscriptionSync)
        {
            AttachToCurrentEncounter();
        }
    }

    private void AttachToCurrentEncounter()
    {
        var currentEncounter = EncounterManager.Current;
        if (ReferenceEquals(_subscribedEncounter, currentEncounter))
        {
            return;
        }

        if (_subscribedEncounter is not null)
        {
            _subscribedEncounter.SkillActivated -= Encounter_SkillActivated;
        }

        _subscribedEncounter = currentEncounter;
        _subscribedEncounter.SkillActivated += Encounter_SkillActivated;
    }

    private void Encounter_SkillActivated(object sender, SkillActivatedEventArgs e)
    {
        var encounter = sender as Encounter;
        var activation = new SkillActivationState(
            e.ActivationDateTime,
            encounter,
            encounter?.GetDuration() ?? TimeSpan.Zero);
        var maxCharges = CombatDataCatalog.GetSkillMaxCharges(e.SkillId);
        var rawChargeCooldownSeconds = maxCharges > 1
            ? CombatDataCatalog.GetSkillChargeCooldownSeconds(e.SkillId, 0)
            : 0d;
        var history = _activationHistories.GetOrAdd(
            new SkillActivationKey(e.CasterUuid, e.SkillId),
            static _ => new SkillActivationHistory());
        history.Record(activation, maxCharges, rawChargeCooldownSeconds);
    }

    /// <summary>最後に過ぎたリセットより前の発動を除く。</summary>
    private IReadOnlyList<SkillActivationState> ExcludeBeforeReset(long entityUuid, IReadOnlyList<SkillActivationState> activations)
    {
        return GetResetCutoff(entityUuid) is { } cutoff
            ? activations.Where(activation => activation.ActivationDateTime >= cutoff).ToArray()
            : activations;
    }

    private static bool IsSelf(long entityUuid)
    {
        return entityUuid == MessageManager.currentUserUuid
            || entityUuid == AppState.PlayerUUID;
    }

    private static ChargeCooldownState GetSelfChargeState(
        long entityUuid,
        int skillId,
        int maxCharges)
    {
        var state = SkillCooldownStateStore.GetSelfChargeCooldownState(
            entityUuid,
            skillId,
            maxCharges);
        return new ChargeCooldownState(
            state.AvailableCharges,
            state.NextChargeRemainingSeconds,
            state.FullRecoveryRemainingSeconds);
    }

    private static ChargeCooldownState GetEstimatedChargeState(
        long entityUuid,
        IReadOnlyList<SkillActivationState> activationHistory,
        int maxCharges,
        double chargeCooldownSeconds,
        bool isImagine)
    {
        if (activationHistory.Count == 0)
        {
            return ChargeCooldownState.FullyRecovered(maxCharges);
        }

        var latestEncounter = activationHistory[^1].Encounter;
        var activations = activationHistory
            .Where(activation => ReferenceEquals(activation.Encounter, latestEncounter))
            .OrderBy(activation => activation.ActivationDateTime)
            .ToArray();
        if (activations.Length == 0)
        {
            return ChargeCooldownState.FullyRecovered(maxCharges);
        }

        var origin = activations[0];
        var activationProgressTimes = activations
            .Select(activation => GetEffectiveElapsedSeconds(
                entityUuid,
                origin,
                activation.ActivationDateTime,
                activation.EncounterTime,
                isImagine))
            .ToArray();
        var currentEncounterTime = latestEncounter?.GetDuration() ?? origin.EncounterTime;
        var currentProgressTime = GetEffectiveElapsedSeconds(
            entityUuid,
            origin,
            DateTime.Now,
            currentEncounterTime,
            isImagine);

        return CalculateChargeState(
            activationProgressTimes,
            currentProgressTime,
            maxCharges,
            chargeCooldownSeconds);
    }

    private static ChargeCooldownState CalculateChargeState(
        IReadOnlyList<double> activationProgressTimes,
        double currentProgressTime,
        int maxCharges,
        double chargeCooldownSeconds)
    {
        if (maxCharges <= 1 || chargeCooldownSeconds <= 0)
        {
            return default;
        }

        var availableCharges = maxCharges;
        var missingCharges = 0;
        var nextRecoveryProgressTime = 0d;

        void RecoverThrough(double progressTime)
        {
            while (missingCharges > 0 && nextRecoveryProgressTime <= progressTime)
            {
                missingCharges--;
                availableCharges++;
                if (missingCharges > 0)
                {
                    nextRecoveryProgressTime += chargeCooldownSeconds;
                }
                else
                {
                    nextRecoveryProgressTime = 0d;
                }
            }
        }

        foreach (var activationProgressTime in activationProgressTimes)
        {
            RecoverThrough(activationProgressTime);
            if (availableCharges <= 0)
            {
                continue;
            }

            availableCharges--;
            missingCharges++;
            if (missingCharges == 1)
            {
                nextRecoveryProgressTime = activationProgressTime + chargeCooldownSeconds;
            }
        }

        RecoverThrough(currentProgressTime);
        if (missingCharges == 0)
        {
            return ChargeCooldownState.FullyRecovered(maxCharges);
        }

        var nextChargeRemainingSeconds = Math.Max(
            nextRecoveryProgressTime - currentProgressTime,
            0d);
        var fullRecoveryRemainingSeconds = nextChargeRemainingSeconds
            + chargeCooldownSeconds * (missingCharges - 1);
        return new ChargeCooldownState(
            availableCharges,
            nextChargeRemainingSeconds,
            fullRecoveryRemainingSeconds);
    }

    private static double GetEffectiveElapsedSeconds(
        long entityUuid,
        SkillActivationState origin,
        DateTime targetDateTime,
        TimeSpan targetEncounterTime,
        bool isImagine)
    {
        var elapsedSeconds = Math.Max(
            targetDateTime.Subtract(origin.ActivationDateTime).TotalSeconds,
            0d);
        if (!isImagine)
        {
            return elapsedSeconds;
        }

        return elapsedSeconds
            + GetLowerCdOverlapSeconds(
                entityUuid,
                origin.Encounter,
                origin.EncounterTime,
                targetEncounterTime)
            * LowerCdAdditionalProgressRate;
    }

    private static double GetLowerCdOverlapSeconds(
        long entityUuid,
        Encounter? encounter,
        TimeSpan rangeStart,
        TimeSpan rangeEnd)
    {
        if (encounter is null
            || rangeEnd <= rangeStart
            || !encounter.Entities.TryGetValue(entityUuid, out var entity))
        {
            return 0d;
        }

        var intervals = new List<LowerCdInterval>();
        foreach (var buffEvent in entity.BuffEvents.Values)
        {
            if (buffEvent.BaseId != LowerCdBuffBaseId
                || buffEvent.Duration <= 0)
            {
                continue;
            }

            var intervalStart = buffEvent.EventAddTime;
            var scheduledEnd = intervalStart + TimeSpan.FromMilliseconds(buffEvent.Duration);
            var intervalEnd = buffEvent.EventRemoveTime > TimeSpan.Zero
                && buffEvent.EventRemoveTime < scheduledEnd
                    ? buffEvent.EventRemoveTime
                    : scheduledEnd;
            var clippedStart = intervalStart > rangeStart
                ? intervalStart
                : rangeStart;
            var clippedEnd = intervalEnd < rangeEnd
                ? intervalEnd
                : rangeEnd;

            if (clippedEnd > clippedStart)
            {
                intervals.Add(new LowerCdInterval(clippedStart, clippedEnd));
            }
        }

        if (intervals.Count == 0)
        {
            return 0d;
        }

        intervals.Sort(static (left, right) => left.Start.CompareTo(right.Start));

        var total = TimeSpan.Zero;
        var mergedStart = intervals[0].Start;
        var mergedEnd = intervals[0].End;
        for (var index = 1; index < intervals.Count; index++)
        {
            var interval = intervals[index];
            if (interval.Start <= mergedEnd)
            {
                if (interval.End > mergedEnd)
                {
                    mergedEnd = interval.End;
                }

                continue;
            }

            total += mergedEnd - mergedStart;
            mergedStart = interval.Start;
            mergedEnd = interval.End;
        }

        total += mergedEnd - mergedStart;
        return total.TotalSeconds;
    }

    private readonly record struct SkillActivationKey(long EntityUuid, int SkillId);

    private readonly record struct SkillActivationState(
        DateTime ActivationDateTime,
        Encounter? Encounter,
        TimeSpan EncounterTime);

    private readonly record struct LowerCdInterval(TimeSpan Start, TimeSpan End);

    /// <summary>
    /// 1人ぶんのクールダウンのリセットの時刻。まだ来ていない時刻(付与より後に消えるバフ)も持つ。
    /// </summary>
    private sealed class CooldownResetSchedule
    {
        private readonly object _sync = new();
        private readonly List<DateTime> _times = [];

        public void Add(DateTime resetAt)
        {
            lock (_sync)
            {
                _times.Add(resetAt);
            }
        }

        /// <summary>
        /// <paramref name="now"/> までに過ぎたリセットのうち最後の時刻。無ければ null。
        /// それより前の時刻はもう使わないので捨て、まだ来ていない時刻は残す。
        /// </summary>
        public DateTime? GetLatestPassed(DateTime now)
        {
            lock (_sync)
            {
                DateTime? latest = null;
                foreach (var time in _times)
                {
                    if (time <= now && (latest is null || time > latest))
                    {
                        latest = time;
                    }
                }

                if (latest is { } kept)
                {
                    _times.RemoveAll(time => time < kept);
                }

                return latest;
            }
        }
    }

    private readonly record struct ChargeCooldownState(
        int AvailableCharges,
        double NextChargeRemainingSeconds,
        double FullRecoveryRemainingSeconds)
    {
        public static ChargeCooldownState FullyRecovered(int maxCharges)
        {
            return new ChargeCooldownState(Math.Max(maxCharges, 0), 0d, 0d);
        }
    }

    private sealed class SkillActivationHistory
    {
        private readonly object _sync = new();
        private readonly List<SkillActivationState> _activations = [];

        public void Record(
            SkillActivationState activation,
            int maxCharges,
            double rawChargeCooldownSeconds)
        {
            lock (_sync)
            {
                if (maxCharges <= 1 || rawChargeCooldownSeconds <= 0)
                {
                    _activations.Clear();
                    _activations.Add(activation);
                    return;
                }

                if (_activations.Count > 0
                    && !ReferenceEquals(_activations[^1].Encounter, activation.Encounter))
                {
                    _activations.Clear();
                }
                else if (AreAllChargesRecovered(
                    activation.ActivationDateTime,
                    maxCharges,
                    rawChargeCooldownSeconds))
                {
                    _activations.Clear();
                }

                _activations.Add(activation);
            }
        }

        private bool AreAllChargesRecovered(
            DateTime targetDateTime,
            int maxCharges,
            double chargeCooldownSeconds)
        {
            if (_activations.Count == 0)
            {
                return true;
            }

            var activations = _activations
                .OrderBy(activation => activation.ActivationDateTime)
                .ToArray();
            var originDateTime = activations[0].ActivationDateTime;
            var activationProgressTimes = activations
                .Select(activation => Math.Max(
                    activation.ActivationDateTime.Subtract(originDateTime).TotalSeconds,
                    0d))
                .ToArray();
            var currentProgressTime = Math.Max(
                targetDateTime.Subtract(originDateTime).TotalSeconds,
                0d);

            return CalculateChargeState(
                activationProgressTimes,
                currentProgressTime,
                maxCharges,
                chargeCooldownSeconds).FullRecoveryRemainingSeconds <= 0;
        }

        public SkillActivationState? GetLatest()
        {
            lock (_sync)
            {
                if (_activations.Count == 0)
                {
                    return null;
                }

                var latest = _activations[0];
                for (var index = 1; index < _activations.Count; index++)
                {
                    if (_activations[index].ActivationDateTime > latest.ActivationDateTime)
                    {
                        latest = _activations[index];
                    }
                }

                return latest;
            }
        }

        public SkillActivationState[] GetSnapshot()
        {
            lock (_sync)
            {
                return [.. _activations];
            }
        }
    }
}
