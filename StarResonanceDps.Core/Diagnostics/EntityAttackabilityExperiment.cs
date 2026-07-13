using System.Globalization;
using System.Text;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.Core.Diagnostics;

public sealed class EntityAttackabilityExperiment
{
    private const string AttrTargetUuid = "AttrTargetUuid";
    private const string AttrCanBeHit = "AttrCanBeHit";
    private const string AttrCanLessenHp = "AttrCanLessenHp";
    private const string AttrCanIntoCombat = "AttrCanIntoCombat";
    private const string AttrCanBeHatredTarget = "AttrCanBeHatredTarget";

    private static readonly string[] ObservedAttributeNames =
    [
        AttrCanBeHit,
        AttrCanLessenHp,
        AttrCanIntoCombat,
        AttrCanBeHatredTarget
    ];

    private static readonly Lazy<EntityAttackabilityExperiment> LazyInstance =
        new(() => new EntityAttackabilityExperiment());

    private readonly object _sync = new();
    private readonly NearbyEntityStore _nearbyEntityStore = NearbyEntityStore.Instance;
    private readonly Dictionary<long, EntityObservation> _observations = [];

    private Encounter? _subscribedEncounter;
    private DateTimeOffset _startedAt;
    private string _startMapName = string.Empty;
    private long _startMapGeneration;
    private string _endMapName = string.Empty;
    private long _endMapGeneration;
    private long _lastTargetUuid;
    private bool _isRunning;

    private EntityAttackabilityExperiment()
    {
    }

    public static EntityAttackabilityExperiment Instance => LazyInstance.Value;

    public event EventHandler? StateChanged;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _isRunning;
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_isRunning)
            {
                return;
            }

            _observations.Clear();
            _startedAt = DateTimeOffset.Now;
            var snapshot = _nearbyEntityStore.Current;
            _startMapName = snapshot.MapName;
            _startMapGeneration = snapshot.MapGeneration;
            _endMapName = snapshot.MapName;
            _endMapGeneration = snapshot.MapGeneration;
            _lastTargetUuid = 0;
            _isRunning = true;

            EncounterManager.EncounterStart += EncounterManager_EncounterStart;
            _nearbyEntityStore.EntitiesChanged += NearbyEntityStore_EntitiesChanged;
            AttachToCurrentEncounterNoLock();
            CaptureSnapshotNoLock(snapshot.Entries);
            CaptureCurrentTargetNoLock();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        string report;

        lock (_sync)
        {
            if (!_isRunning)
            {
                return;
            }

            var finalSnapshot = _nearbyEntityStore.Current;
            _endMapName = finalSnapshot.MapName;
            _endMapGeneration = finalSnapshot.MapGeneration;
            CaptureSnapshotNoLock(finalSnapshot.Entries);
            CaptureCurrentTargetNoLock();
            report = BuildReportNoLock(DateTimeOffset.Now);

            EncounterManager.EncounterStart -= EncounterManager_EncounterStart;
            _nearbyEntityStore.EntitiesChanged -= NearbyEntityStore_EntitiesChanged;
            if (_subscribedEncounter is not null)
            {
                _subscribedEncounter.AttributeUpdated -= Encounter_AttributeUpdated;
                _subscribedEncounter = null;
            }

            _isRunning = false;
        }

        StarResonanceDps.Core.Logging.PacketDiagnosticLogStore.Instance.AppendDisplayText(report);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EncounterManager_EncounterStart(EncounterStartEventArgs e)
    {
        lock (_sync)
        {
            if (!_isRunning)
            {
                return;
            }

            AttachToCurrentEncounterNoLock();
            CaptureSnapshotNoLock(_nearbyEntityStore.Current.Entries);
            CaptureCurrentTargetNoLock();
        }
    }

    private void NearbyEntityStore_EntitiesChanged(object? sender, NearbyEntitiesChangedEventArgs e)
    {
        lock (_sync)
        {
            if (!_isRunning)
            {
                return;
            }

            _endMapName = e.MapName;
            _endMapGeneration = e.MapGeneration;

            foreach (var observation in _observations.Values)
            {
                observation.IsCurrentlyNearby = false;
            }

            CaptureSnapshotNoLock(e.Snapshot);
        }
    }

    private void Encounter_AttributeUpdated(object sender, AttributeUpdatedEventArgs e)
    {
        lock (_sync)
        {
            if (!_isRunning)
            {
                return;
            }

            if (string.Equals(e.AttributeName, AttrTargetUuid, StringComparison.Ordinal)
                && IsSelfEntity(e.EntityUuid))
            {
                ObserveTargetNoLock(ToInt64(e.AttributeValue));
                return;
            }

            if (!ObservedAttributeNames.Contains(e.AttributeName, StringComparer.Ordinal))
            {
                return;
            }

            if (!_observations.TryGetValue(e.EntityUuid, out var observation))
            {
                var currentNearbyEntity = _nearbyEntityStore.Current.Entries
                    .FirstOrDefault(entry => entry.EntityUuid == e.EntityUuid);
                if (currentNearbyEntity is null)
                {
                    return;
                }

                observation = GetOrCreateObservationNoLock(currentNearbyEntity);
            }

            observation.ObserveAttribute(
                e.AttributeName,
                e.AttributeValue,
                GetElapsedNoLock());
        }
    }

    private void AttachToCurrentEncounterNoLock()
    {
        var currentEncounter = EncounterManager.Current;
        if (ReferenceEquals(_subscribedEncounter, currentEncounter))
        {
            return;
        }

        if (_subscribedEncounter is not null)
        {
            _subscribedEncounter.AttributeUpdated -= Encounter_AttributeUpdated;
        }

        _subscribedEncounter = currentEncounter;
        if (_subscribedEncounter is not null)
        {
            _subscribedEncounter.AttributeUpdated += Encounter_AttributeUpdated;
        }
    }

    private void CaptureSnapshotNoLock(IReadOnlyList<NearbyEntityEntry> entries)
    {
        foreach (var entry in entries)
        {
            var observation = GetOrCreateObservationNoLock(entry);
            observation.IsCurrentlyNearby = true;
            observation.LastSeenElapsed = GetElapsedNoLock();
            CaptureCurrentAttributesNoLock(observation);
        }
    }

    private EntityObservation GetOrCreateObservationNoLock(NearbyEntityEntry entry)
    {
        if (!_observations.TryGetValue(entry.EntityUuid, out var observation))
        {
            observation = new EntityObservation(
                entry.EntityUuid,
                entry.EntityId,
                entry.Name,
                entry.EntityType.ToString(),
                entry.MonsterType.ToString(),
                entry.Level,
                GetElapsedNoLock());
            _observations.Add(entry.EntityUuid, observation);
        }
        else
        {
            observation.UpdateMetadata(
                entry.EntityId,
                entry.Name,
                entry.EntityType.ToString(),
                entry.MonsterType.ToString(),
                entry.Level);
        }

        return observation;
    }

    private EntityObservation GetOrCreateObservationNoLock(long entityUuid)
    {
        if (_observations.TryGetValue(entityUuid, out var observation))
        {
            return observation;
        }

        var encounter = EncounterManager.Current;
        if (encounter is not null
            && encounter.Entities.TryGetValue(entityUuid, out var entity))
        {
            observation = new EntityObservation(
                entityUuid,
                entity.UID,
                entity.Name ?? string.Empty,
                entity.EntityType.ToString(),
                entity.MonsterType.ToString(),
                entity.Level,
                GetElapsedNoLock());
        }
        else
        {
            observation = new EntityObservation(
                entityUuid,
                0,
                string.Empty,
                "Unknown",
                "Unknown",
                0,
                GetElapsedNoLock());
        }

        _observations.Add(entityUuid, observation);
        CaptureCurrentAttributesNoLock(observation);
        return observation;
    }

    private void CaptureCurrentAttributesNoLock(EntityObservation observation)
    {
        var encounter = EncounterManager.Current;
        if (encounter is null
            || !encounter.Entities.TryGetValue(observation.EntityUuid, out var entity))
        {
            return;
        }

        observation.UpdateMetadata(
            entity.UID,
            entity.Name ?? string.Empty,
            entity.EntityType.ToString(),
            entity.MonsterType.ToString(),
            entity.Level);

        var elapsed = GetElapsedNoLock();
        foreach (var attributeName in ObservedAttributeNames)
        {
            var value = entity.GetAttrKV(attributeName);
            if (value is not null)
            {
                observation.ObserveAttribute(attributeName, value, elapsed);
            }
        }
    }

    private void CaptureCurrentTargetNoLock()
    {
        var selfUuid = GetSelfUuid();
        var encounter = EncounterManager.Current;
        if (selfUuid == 0
            || encounter is null
            || !encounter.Entities.TryGetValue(selfUuid, out var selfEntity))
        {
            return;
        }

        var targetValue = selfEntity.GetAttrKV(AttrTargetUuid);
        if (targetValue is not null)
        {
            ObserveTargetNoLock(ToInt64(targetValue));
        }
    }

    private void ObserveTargetNoLock(long targetUuid)
    {
        if (_lastTargetUuid == targetUuid)
        {
            return;
        }

        _lastTargetUuid = targetUuid;
        if (targetUuid == 0)
        {
            return;
        }

        var observation = GetOrCreateObservationNoLock(targetUuid);
        observation.ObserveSelection(GetElapsedNoLock());
    }

    private string BuildReportNoLock(DateTimeOffset endedAt)
    {
        var duration = endedAt - _startedAt;
        var text = new StringBuilder();
        text.AppendLine("===== ENTITY ATTACKABILITY EXPERIMENT BEGIN =====");
        text.Append("StartedAt=").AppendLine(_startedAt.ToString("O", CultureInfo.InvariantCulture));
        text.Append("EndedAt=").AppendLine(endedAt.ToString("O", CultureInfo.InvariantCulture));
        text.Append("DurationSeconds=").AppendLine(duration.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        text.Append("StartMapName=").AppendLine(string.IsNullOrWhiteSpace(_startMapName) ? "<unknown>" : _startMapName);
        text.Append("StartMapGeneration=").AppendLine(_startMapGeneration.ToString(CultureInfo.InvariantCulture));
        text.Append("EndMapName=").AppendLine(string.IsNullOrWhiteSpace(_endMapName) ? "<unknown>" : _endMapName);
        text.Append("EndMapGeneration=").AppendLine(_endMapGeneration.ToString(CultureInfo.InvariantCulture));
        text.Append("MapChangedDuringExperiment=").AppendLine(_startMapGeneration == _endMapGeneration ? "NO" : "YES");
        text.Append("EntityCount=").AppendLine(_observations.Count.ToString(CultureInfo.InvariantCulture));
        text.AppendLine("SelectionSource=local player AttrTargetUuid (attribute id 450)");
        text.AppendLine("ObservedAttributes=AttrCanBeHit(720), AttrCanLessenHP(721), AttrCanIntoCombat(722), AttrCanBeHatredTarget(724)");
        text.AppendLine("SelectionObserved=YES means the entity UUID appeared in the local player's AttrTargetUuid during the experiment.");
        text.AppendLine("SelectionObserved=NO is not proof that the entity is unselectable; it only means no selection was observed.");
        text.AppendLine("An absent attribute is reported as UNKNOWN and is never treated as false.");

        foreach (var observation in _observations.Values
                     .OrderByDescending(item => item.SelectionObserved)
                     .ThenBy(item => item.Name, StringComparer.Ordinal)
                     .ThenBy(item => item.EntityUuid))
        {
            text.AppendLine();
            text.Append("EntityUuid=").AppendLine(observation.EntityUuid.ToString(CultureInfo.InvariantCulture));
            text.Append("EntityId=").AppendLine(observation.EntityId.ToString(CultureInfo.InvariantCulture));
            text.Append("Name=").AppendLine(string.IsNullOrWhiteSpace(observation.Name) ? "<unknown>" : observation.Name);
            text.Append("EntityType=").AppendLine(observation.EntityType);
            text.Append("MonsterType=").AppendLine(observation.MonsterType);
            text.Append("Level=").AppendLine(observation.Level.ToString(CultureInfo.InvariantCulture));
            text.Append("CurrentlyNearbyAtStop=").AppendLine(observation.IsCurrentlyNearby ? "YES" : "NO");
            text.Append("FirstSeenSeconds=").AppendLine(observation.FirstSeenElapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            text.Append("LastSeenSeconds=").AppendLine(observation.LastSeenElapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            text.Append("SelectionObserved=").AppendLine(observation.SelectionObserved ? "YES" : "NO");
            text.Append("SelectionTimesSeconds=").AppendLine(observation.FormatSelectionTimes());

            foreach (var attributeName in ObservedAttributeNames)
            {
                text.Append(attributeName)
                    .Append('=')
                    .AppendLine(observation.FormatAttribute(attributeName));
            }
        }

        text.AppendLine("===== ENTITY ATTACKABILITY EXPERIMENT END =====");
        return text.ToString().TrimEnd();
    }

    private TimeSpan GetElapsedNoLock()
    {
        return DateTimeOffset.Now - _startedAt;
    }

    private static bool IsSelfEntity(long entityUuid)
    {
        var selfUuid = GetSelfUuid();
        return selfUuid != 0 && entityUuid == selfUuid;
    }

    private static long GetSelfUuid()
    {
        return MessageManager.currentUserUuid != 0
            ? MessageManager.currentUserUuid
            : AppState.PlayerUUID;
    }

    private static long ToInt64(object? value)
    {
        return value switch
        {
            long integer => integer,
            int integer => integer,
            ulong integer => integer > long.MaxValue ? long.MaxValue : (long)integer,
            uint integer => integer,
            short integer => integer,
            ushort integer => integer,
            byte integer => integer,
            sbyte integer => integer,
            _ => 0
        };
    }

    private sealed class EntityObservation
    {
        private readonly Dictionary<string, AttributeObservation> _attributes = new(StringComparer.Ordinal);
        private readonly List<TimeSpan> _selectionTimes = [];

        public EntityObservation(
            long entityUuid,
            long entityId,
            string name,
            string entityType,
            string monsterType,
            int level,
            TimeSpan firstSeenElapsed)
        {
            EntityUuid = entityUuid;
            EntityId = entityId;
            Name = name;
            EntityType = entityType;
            MonsterType = monsterType;
            Level = level;
            FirstSeenElapsed = firstSeenElapsed;
            LastSeenElapsed = firstSeenElapsed;
        }

        public long EntityUuid { get; }

        public long EntityId { get; private set; }

        public string Name { get; private set; }

        public string EntityType { get; private set; }

        public string MonsterType { get; private set; }

        public int Level { get; private set; }

        public TimeSpan FirstSeenElapsed { get; }

        public TimeSpan LastSeenElapsed { get; set; }

        public bool IsCurrentlyNearby { get; set; }

        public bool SelectionObserved => _selectionTimes.Count > 0;

        public void UpdateMetadata(
            long entityId,
            string name,
            string entityType,
            string monsterType,
            int level)
        {
            if (entityId != 0)
            {
                EntityId = entityId;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                Name = name;
            }

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                EntityType = entityType;
            }

            if (!string.IsNullOrWhiteSpace(monsterType))
            {
                MonsterType = monsterType;
            }

            if (level != 0)
            {
                Level = level;
            }
        }

        public void ObserveSelection(TimeSpan elapsed)
        {
            _selectionTimes.Add(elapsed);
            LastSeenElapsed = elapsed;
        }

        public void ObserveAttribute(string attributeName, object value, TimeSpan elapsed)
        {
            if (!_attributes.TryGetValue(attributeName, out var observation))
            {
                observation = new AttributeObservation();
                _attributes.Add(attributeName, observation);
            }

            observation.Observe(value, elapsed);
            LastSeenElapsed = elapsed;
        }

        public string FormatSelectionTimes()
        {
            if (_selectionTimes.Count == 0)
            {
                return "<none>";
            }

            return string.Join(
                ",",
                _selectionTimes.Select(time => time.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)));
        }

        public string FormatAttribute(string attributeName)
        {
            return _attributes.TryGetValue(attributeName, out var observation)
                ? observation.Format()
                : "UNKNOWN";
        }
    }

    private sealed class AttributeObservation
    {
        private readonly List<AttributeTransition> _transitions = [];
        private string? _lastValue;

        public void Observe(object value, TimeSpan elapsed)
        {
            var formattedValue = FormatValue(value);
            if (string.Equals(_lastValue, formattedValue, StringComparison.Ordinal))
            {
                return;
            }

            _lastValue = formattedValue;
            _transitions.Add(new AttributeTransition(elapsed, formattedValue));
        }

        public string Format()
        {
            if (_transitions.Count == 0)
            {
                return "UNKNOWN";
            }

            return string.Join(
                " -> ",
                _transitions.Select(transition =>
                    $"{transition.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)}s:{transition.Value}"));
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                bool boolean => boolean ? "1(TRUE)" : "0(FALSE)",
                byte integer => FormatInteger(integer),
                sbyte integer => FormatInteger(integer),
                short integer => FormatInteger(integer),
                ushort integer => FormatInteger(integer),
                int integer => FormatInteger(integer),
                uint integer => FormatInteger(integer),
                long integer => FormatInteger(integer),
                ulong integer => FormatInteger(integer),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>"
            };
        }

        private static string FormatInteger<T>(T value)
            where T : struct, IFormattable
        {
            var numericText = value.ToString(null, CultureInfo.InvariantCulture);
            return numericText switch
            {
                "0" => "0(FALSE)",
                "1" => "1(TRUE)",
                _ => numericText
            };
        }
    }

    private sealed record AttributeTransition(TimeSpan Elapsed, string Value);
}
