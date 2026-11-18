namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Reads one encounter: the encounter the host's meters show (<see cref="IPluginGameDataService.GetShownEncounter"/>),
/// the current live encounter (<see cref="IPluginGameDataService.GetLiveEncounter"/>),
/// or a saved encounter read without opening it (<see cref="IPluginHistoryService.LoadAsync"/>). The host implements it.
///
/// <para>
/// The object keeps reading the encounter it was created for, even after the host has moved on to another encounter.
/// While the plugin keeps the object, the encounter stays in memory; keep it only while it is needed.
/// </para>
///
/// <para>
/// Members can be called from any thread. For a live encounter, each call reads the values at that moment,
/// so two calls can read different moments. An encounter read from the saved history does not change,
/// except for the plugin's data (<see cref="GetPluginData"/>).
/// </para>
/// </summary>
public interface IPluginEncounterRecord
{
    /// <summary>
    /// The number that identifies the encounter in this run of the app (<see cref="PluginEncounterSnapshot.EncounterKey"/>).
    /// </summary>
    long EncounterKey { get; }

    /// <summary>
    /// The number of the encounter in the saved history (<see cref="PluginEncounterSnapshot.HistoryId"/>), or <c>null</c> while it has not been saved.
    /// A live encounter gets its number when the host saves it (<see cref="IPluginGameDataService.EncounterSaved"/>).
    /// </summary>
    ulong? HistoryId { get; }

    /// <summary>
    /// The encounter was read from the saved history: the history encounter opened in the host,
    /// or an encounter read with <see cref="IPluginHistoryService.LoadAsync"/>.
    /// <c>false</c> for the current encounter and for the previous encounter the meters keep showing.
    /// </summary>
    bool IsHistory { get; }

    /// <summary>
    /// The scene, server, benchmark and other values of the encounter.
    /// For a live encounter, read it again when needed: the channel, the difficulty and <see cref="PluginEncounterInfo.IsWipe"/> can change.
    /// </summary>
    PluginEncounterInfo Info { get; }

    /// <summary>
    /// The totals of every player in the encounter, with the same numbers as the host's meters
    /// (for the encounter the meters show, the same as <see cref="IPluginGameDataService.GetEncounter"/>).
    /// </summary>
    PluginEncounterSnapshot GetTotals();

    /// <summary>
    /// A player's skill breakdown in the encounter, largest first (the same rows as <see cref="IPluginGameDataService.GetSkillTotals"/>).
    /// Empty when the player has no total of that kind in the encounter.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for a value that is not a <see cref="PluginCombatKind"/>.
    /// </summary>
    IReadOnlyList<PluginSkillTotals> GetSkillTotals(long characterId, PluginCombatKind kind);

    /// <summary>
    /// Converts a time to the encounter's combat time, the horizontal axis of the host's graphs and timeline:
    /// the time since <see cref="PluginEncounterSnapshot.StartUtc"/>, minus the intervals in which the combat clock was paused before it
    /// (the general setting "Auto Pause", and with a dungeon clock, the stops of the game's dungeon timer).
    /// A time inside a pause gives the combat time at which the pause started.
    /// Returns <c>null</c> before the combat clock starts, and a negative value for a time before <see cref="PluginEncounterSnapshot.StartUtc"/>.
    /// Pass a UTC time; <see cref="DateTime.Kind"/> is not converted.
    /// For a live encounter, the value for a time after the last combat event can change when combat resumes.
    /// </summary>
    TimeSpan? ToCombatOffset(DateTime utc);

    /// <summary>
    /// The players of a saved history encounter: those with damage, healing, hate, break, damage taken or timeline records in it,
    /// in no particular order. <c>null</c> for a live encounter; read <see cref="IPluginGameDataService.Players"/> instead.
    ///
    /// <para>
    /// Each row has only the values saved with the encounter, which are what the host showed when it saved the encounter.
    /// The current party and the values the host keeps for the current session are not used,
    /// so a row can differ from the same player's row in <see cref="IPluginGameDataService.Players"/>.
    /// <see cref="PluginPlayer.IsFromRecord"/> is <c>true</c>; <see cref="PluginPlayer.CurrentHp"/> equals <see cref="PluginPlayer.MaxHp"/>;
    /// <see cref="PluginPlayer.CurrentShield"/> is 0; <see cref="PluginPlayer.PartyNumber"/> is <c>null</c>;
    /// <see cref="PluginPlayer.IsPartyMember"/>, <see cref="PluginPlayer.IsNearby"/>, <see cref="PluginPlayer.IsInCombat"/>
    /// and <see cref="PluginPlayer.IsIncapacitated"/> are <c>false</c>.
    /// <see cref="PluginPlayer.IsSelf"/> marks the character logged in at the time of the call,
    /// and names are hidden by the host's display settings at the time of the call.
    /// </para>
    /// </summary>
    IReadOnlyList<PluginPlayer>? GetPlayers();

    /// <summary>
    /// The monsters the encounter keeps, in no particular order.
    /// For a live encounter: the monsters around the player, and the monsters that left the surroundings but are bosses
    /// or appear in the host's Timeline or Damage Taken Log; each call reads them at that moment.
    /// For an encounter read from the saved history: the monsters saved with it, which are those with a recorded cast,
    /// those in the Timeline's enemy actions, and the attackers in the players' damage taken. A boss without records is not saved.
    /// </summary>
    IReadOnlyList<PluginRecordedMonster> GetMonsters();

    /// <summary>
    /// When each buff was on the player and when the player was around in the encounter (<see cref="PluginBuffIntervals"/>).
    /// These are the records the host's Buff Uptime widget reads, read with the same rules as the widget:
    /// buffs shown in the buff list (not debuffs), for players only; in a benchmark encounter, only the player the host recorded in it
    /// (the player who was logged in during the benchmark); nothing in an encounter in which the host stops aggregation
    /// (<see cref="PluginAggregationSettings.StopInNonCombatAreas"/>).
    /// Returns <c>null</c> when the host has no record for the player or the combat clock has not started.
    /// For a live encounter, buffs that are still on extend to the current end of the combat clock.
    /// </summary>
    PluginBuffIntervals? GetPlayerBuffIntervals(long characterId);

    /// <summary>
    /// The build of a player saved with a history encounter, or <c>null</c> for the current encounter
    /// (read <see cref="IPluginGameDataService.GetPlayerBuild"/>) and for a player not in <see cref="GetPlayers"/>.
    /// The values are the ones saved: <see cref="PluginPlayerBuild.TalentNodeIds"/>, <see cref="PluginPlayerBuild.Factors"/> and
    /// <see cref="PluginPlayerBuild.ModuleEffects"/> only for the player who was logged in when the encounter was saved,
    /// and <see cref="PluginPlayerBuild.SkillLevels"/> is the list the game sent for each player (also for that player).
    /// </summary>
    PluginPlayerBuild? GetPlayerBuild(long characterId);

    /// <summary>
    /// The skill activations of a player recorded in the encounter, from <paramref name="startIndex"/> on, oldest first
    /// (the records the host's graphs show as icons). The local player's activations are recorded when the game accepts the skill,
    /// other players' when they start using it; activations before the combat clock starts are included.
    /// Pass the number of activations read so far as <paramref name="startIndex"/> to read only the new ones.
    /// Throws <see cref="ArgumentOutOfRangeException"/> when <paramref name="startIndex"/> is negative or greater than
    /// the number of the player's activations at the time of the call.
    /// </summary>
    IReadOnlyList<PluginSkillActivation> GetSkillActivations(long characterId, int startIndex = 0);

    /// <summary>
    /// The damage players took in the encounter (the records of the host's Damage Taken Log), from <paramref name="startIndex"/> on, oldest first.
    /// Damage of 0 is not recorded; self-inflicted damage, falls and damage from players are.
    /// Throws <see cref="ArgumentOutOfRangeException"/> when <paramref name="startIndex"/> is negative or greater than the number of records at the time of the call.
    /// </summary>
    IReadOnlyList<PluginTakenHit> GetTakenHits(int startIndex = 0);

    /// <summary>
    /// The Timeline records of the encounter, from <paramref name="startIndex"/> on, oldest first.
    /// <see cref="PluginCombatLogRecord.Sequence"/> is shared with <see cref="GetTakenHits"/>; sort by it to merge the two.
    /// Throws <see cref="ArgumentOutOfRangeException"/> when <paramref name="startIndex"/> is negative or greater than the number of records at the time of the call.
    /// </summary>
    IReadOnlyList<PluginCombatLogRecord> GetCombatLog(int startIndex = 0);

    /// <summary>
    /// A player's damage or healing per second of combat time, the same values as the host's DPS and HPS Trend Graphs with a 1-second interval.
    /// <c>null</c> when the player is not in the encounter or the combat clock has not started. Each call counts all the seconds again.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for a value that is not a <see cref="PluginCombatKind"/>.
    /// </summary>
    PluginPerSecondTotals? GetPerSecondTotals(long characterId, PluginCombatKind kind);

    /// <summary>
    /// The data this plugin stored with the encounter under <paramref name="key"/>
    /// (<see cref="IPluginHistoryService.TrySetEncounterData"/> and <see cref="IPluginHistoryService.SetSavedEncounterDataAsync"/>),
    /// or <c>null</c> when there is none. Keys are matched without case. Only this plugin's data can be read.
    /// Throws <see cref="ArgumentException"/> when <paramref name="key"/> does not follow the key rules of <see cref="IPluginHistoryService"/>.
    /// </summary>
    ReadOnlyMemory<byte>? GetPluginData(string key);
}
