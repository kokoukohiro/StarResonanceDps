namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One monster an encounter keeps (<see cref="IPluginEncounterRecord.GetMonsters"/>).
/// </summary>
/// <param name="EntityUuid">The entity UUID. While the monster is around, it is the same as <see cref="PluginEntity.EntityUuid"/>.</param>
/// <param name="MonsterId">
/// The monster ID, or <c>null</c> when the host did not receive it. Use <see cref="IPluginNameService.GetMonsterName"/> for its name.
/// </param>
/// <param name="Hp">
/// The last HP the host received, or 0 when it did not receive it; for an encounter read from the saved history, the value when it was saved.
/// </param>
/// <param name="MaxHp">
/// The last maximum HP the host received, or 0 when it did not receive it; for an encounter read from the saved history, the value when it was saved.
/// </param>
/// <param name="Casts">
/// The monster's casts the host recorded, in the order they arrived: skills with a cast bar and skills the game warns about on the combat screen.
/// A cast of a summoned entity without a name of its own is recorded on its summoner.
/// Casts are recorded only while the host records events (<see cref="PluginEncounterInfo"/>).
/// </param>
public sealed record PluginRecordedMonster(
    long EntityUuid,
    int? MonsterId,
    long Hp,
    long MaxHp,
    IReadOnlyList<PluginRecordedCast> Casts);

/// <summary>
/// One cast an encounter recorded (<see cref="PluginRecordedMonster.Casts"/>).
/// </summary>
/// <param name="SkillId">The skill ID. Use <see cref="IPluginNameService.GetSkillName"/> for its name.</param>
/// <param name="StartedAtUtc">When the host received the start (UTC).</param>
/// <param name="IsWarning">A skill the game warns about on the combat screen.</param>
public sealed record PluginRecordedCast(int SkillId, DateTime StartedAtUtc, bool IsWarning);
