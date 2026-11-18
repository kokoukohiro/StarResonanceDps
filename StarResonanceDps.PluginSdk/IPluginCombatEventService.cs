namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Reads the combat events the host processes: damage dealt to enemies, the records the host adds to an encounter
/// (skill activations, damage taken and Timeline records), buff changes, monster skills and monsters reaching 0 HP.
/// The host implements it.
/// </summary>
public interface IPluginCombatEventService
{
    /// <summary>
    /// Returns the events of the given kinds, in the order the host processed them, starting from the first event
    /// after the enumeration starts. The plugin receives events only while the enumeration is running:
    /// it starts receiving when <c>await foreach</c> begins and stops when the loop exits.
    /// When the host stops the plugin, the enumeration ends without an exception, or with <see cref="OperationCanceledException"/>
    /// when <see cref="IPluginContext.Stopping"/> is passed as <paramref name="cancellationToken"/>.
    /// Throws <see cref="ArgumentException"/> when <paramref name="kinds"/> is <see cref="PluginCombatEventKinds.None"/>
    /// or contains a value that is not defined.
    /// </summary>
    /// <param name="kinds">The kinds of events to read, combined with <c>|</c>.</param>
    /// <param name="cancellationToken">
    /// When canceled, the enumeration ends with <see cref="OperationCanceledException"/> after the events already received.
    /// </param>
    /// <remarks>
    /// Events that have not been read yet are kept for the plugin and counted together with the game messages of
    /// <see cref="IPluginPacketService"/>: when they exceed 64 MB for one plugin, the host stops the plugin.
    /// Read only the kinds the plugin needs.
    /// While a dungeon shows its result or vote screen, the host does not process buffs, damage and healing:
    /// <see cref="PluginCombatEventKinds.Damage"/>, <see cref="PluginCombatEventKinds.TakenHit"/>, <see cref="PluginCombatEventKinds.PlayerBuff"/>,
    /// <see cref="PluginCombatEventKinds.MonsterBuff"/>, and the enemy heal, enemy damage and enemy buff records of
    /// <see cref="PluginCombatEventKinds.CombatLog"/> do not come then.
    /// <see cref="IStarResonancePlugin.Initialize"/> runs on the UI thread; start the loop with <c>ConfigureAwait(false)</c>
    /// so that its body does not run on the UI thread.
    /// </remarks>
    IAsyncEnumerable<PluginCombatEvent> ReadAsync(
        PluginCombatEventKinds kinds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The kinds of combat events for <see cref="IPluginCombatEventService.ReadAsync"/>. Combine them with <c>|</c>.
/// </summary>
[Flags]
public enum PluginCombatEventKinds
{
    /// <summary>No events.</summary>
    None = 0,

    /// <summary><see cref="PluginDamageEvent"/>: damage that players and their summons dealt to enemies, when the host adds it to the meters.</summary>
    Damage = 1 << 0,

    /// <summary><see cref="PluginSkillActivationEvent"/>: a player's skill activation the host recorded (<see cref="IPluginEncounterRecord.GetSkillActivations"/>).</summary>
    SkillActivation = 1 << 1,

    /// <summary><see cref="PluginTakenHitEvent"/>: damage a player took that the host recorded (<see cref="IPluginEncounterRecord.GetTakenHits"/>).</summary>
    TakenHit = 1 << 2,

    /// <summary><see cref="PluginCombatLogEvent"/>: a Timeline record the host added (<see cref="IPluginEncounterRecord.GetCombatLog"/>).</summary>
    CombatLog = 1 << 3,

    /// <summary><see cref="PluginBuffEvent"/> for a buff on a player.</summary>
    PlayerBuff = 1 << 4,

    /// <summary><see cref="PluginBuffEvent"/> for a buff on a monster.</summary>
    MonsterBuff = 1 << 5,

    /// <summary><see cref="PluginMonsterSkillEvent"/>: a monster's current skill was received.</summary>
    MonsterSkill = 1 << 6,

    /// <summary><see cref="PluginMonsterHpZeroEvent"/>: a monster's HP reached 0 or came back from 0.</summary>
    MonsterHpZero = 1 << 7
}
