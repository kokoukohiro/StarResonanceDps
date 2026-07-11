using System.Collections.Concurrent;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public sealed class SkillCooldownTracker
{
    private const int LowerCdBuffBaseId = 2110034;
    private const double LowerCdAdditionalProgressRate = 0.5d;

    private static readonly Lazy<SkillCooldownTracker> LazyInstance = new(() => new SkillCooldownTracker());

    private readonly ConcurrentDictionary<SkillActivationKey, SkillActivationState> _lastActivations = new();
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
            _lastActivations.Clear();
        }
    }

    public double? GetRemainingSeconds(
        long entityUuid,
        int skillId,
        double estimatedCooldownSeconds,
        bool isImagine)
    {
        if (entityUuid == MessageManager.currentUserUuid
            || entityUuid == AppState.PlayerUUID)
        {
            return SkillCooldownStateStore.GetSelfRemainingSeconds(entityUuid, skillId);
        }

        if (estimatedCooldownSeconds <= 0
            || !_lastActivations.TryGetValue(
                new SkillActivationKey(entityUuid, skillId),
                out var activation))
        {
            return null;
        }

        var elapsedSeconds = DateTime.Now.Subtract(activation.ActivationDateTime).TotalSeconds;
        if (isImagine)
        {
            elapsedSeconds += GetLowerCdOverlapSeconds(entityUuid, activation)
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
        var encounterTime = encounter?.GetDuration() ?? TimeSpan.Zero;
        _lastActivations[new SkillActivationKey(e.CasterUuid, e.SkillId)] = new SkillActivationState(
            e.ActivationDateTime,
            encounter,
            encounterTime);
    }

    private static double GetLowerCdOverlapSeconds(
        long entityUuid,
        SkillActivationState activation)
    {
        var encounter = activation.Encounter;
        if (encounter is null
            || !encounter.Entities.TryGetValue(entityUuid, out var entity))
        {
            return 0d;
        }

        var rangeStart = activation.EncounterTime;
        var rangeEnd = encounter.GetDuration();
        if (rangeEnd <= rangeStart)
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
}
