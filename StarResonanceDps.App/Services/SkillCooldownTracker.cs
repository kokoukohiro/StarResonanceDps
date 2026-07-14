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
            if (_subscribedEncounter is not null)
            {
                _subscribedEncounter.SkillActivated -= Encounter_SkillActivated;
                _subscribedEncounter = null;
            }

            _isInitialized = false;
            _activationHistories.Clear();
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
                    chargeHistory?.GetSnapshot() ?? [],
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
            || history.GetLatest() is not { } activation)
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
