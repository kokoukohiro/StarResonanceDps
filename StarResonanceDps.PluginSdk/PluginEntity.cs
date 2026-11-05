namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One entity around the player (the same list as the Entity List widget).
/// </summary>
/// <param name="EntityUuid">The entity UUID. Use it to identify the entity and to read its buffs.</param>
/// <param name="MonsterId">
/// The monster ID, or <c>null</c> when the host has not received it.
/// Use <see cref="IPluginNameService.GetMonsterName"/> for its name.
/// </param>
/// <param name="IsInCombat">The entity is in the combat state.</param>
/// <param name="Cast">The cast the entity started, or <c>null</c> when it is not casting or the start time is unknown.</param>
/// <param name="TargetCharacterId">
/// The character ID of the player the entity targets, or <c>null</c> when the target is not a player or there is none.
/// It is not always the first player of <see cref="HateRanking"/>.
/// </param>
/// <param name="HateRanking">The players on the entity's hate list, highest first.</param>
public sealed record PluginEntity(
    long EntityUuid,
    int? MonsterId,
    long CurrentHp,
    long MaxHp,
    bool IsInCombat,
    PluginEntityCast? Cast,
    long? TargetCharacterId,
    IReadOnlyList<PluginHateRank> HateRanking);

/// <summary>
/// A cast an entity started (a skill with a cast bar).
/// </summary>
/// <param name="SkillId">The skill ID. Use <see cref="IPluginNameService.GetSkillName"/> for its name.</param>
/// <param name="StartedAtUtc">When the host received the start.</param>
/// <param name="TotalSeconds">The cast time of the skill. The skill can last longer than this.</param>
public sealed record PluginEntityCast(int SkillId, DateTime StartedAtUtc, double TotalSeconds);

/// <summary>
/// One player on an entity's hate list.
/// </summary>
public sealed record PluginHateRank(long CharacterId, long HateValue);
