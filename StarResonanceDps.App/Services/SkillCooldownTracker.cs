using System.Collections.Concurrent;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public sealed class SkillCooldownTracker
{
    private static readonly Lazy<SkillCooldownTracker> LazyInstance = new(() => new SkillCooldownTracker());

    private readonly ConcurrentDictionary<SkillActivationKey, DateTime> _lastActivations = new();
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

    public DateTime? GetLastActivationTime(long entityUuid, int skillId)
    {
        return _lastActivations.TryGetValue(new SkillActivationKey(entityUuid, skillId), out var activationTime)
            ? activationTime
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
        _lastActivations[new SkillActivationKey(e.CasterUuid, e.SkillId)] = e.ActivationDateTime;
    }

    private readonly record struct SkillActivationKey(long EntityUuid, int SkillId);
}
