namespace StarResonanceDps.PluginSdk;

/// <summary>
/// When buffs were on a player in an encounter, and when the player was around (<see cref="IPluginEncounterRecord.GetPlayerBuffIntervals"/>).
/// Times are UTC and lie between <see cref="PluginEncounterSnapshot.StartUtc"/> and the end of the combat clock;
/// a buff applied before the combat clock started begins at <see cref="PluginEncounterSnapshot.StartUtc"/>.
/// A buff whose removal the host did not receive ends when its duration ran out.
/// Use <see cref="IPluginEncounterRecord.ToCombatOffset"/> to place the times on the host's combat time.
/// </summary>
/// <param name="Buffs">One entry for each buff ID, in the order the buffs were first applied in the encounter.</param>
/// <param name="PresentIntervals">
/// When the host received the player's values (the player was around), oldest first. Buffs are unknown outside these intervals:
/// the buffs on the player end when the player leaves the surroundings.
/// </param>
public sealed record PluginBuffIntervals(
    IReadOnlyList<PluginBuffInterval> Buffs,
    IReadOnlyList<PluginTimeInterval> PresentIntervals);

/// <summary>
/// When one buff was on a player (<see cref="PluginBuffIntervals.Buffs"/>).
/// </summary>
/// <param name="BuffId">
/// The buff ID as received; buffs that the host's Buff Uptime widget shows as one row are not combined.
/// Use <see cref="IPluginNameService.GetBuffName"/> for its name.
/// </param>
/// <param name="IsPermanent">
/// The instance that started the last interval (when the buff went from off to on) had no duration (a buff that stays on).
/// </param>
/// <param name="Intervals">When at least one instance of the buff was on, oldest first. The intervals do not overlap.</param>
public sealed record PluginBuffInterval(int BuffId, bool IsPermanent, IReadOnlyList<PluginTimeInterval> Intervals);

/// <summary>
/// A time interval.
/// </summary>
/// <param name="StartUtc">The start (UTC).</param>
/// <param name="EndUtc">The end (UTC), after the start.</param>
public readonly record struct PluginTimeInterval(DateTime StartUtc, DateTime EndUtc);
