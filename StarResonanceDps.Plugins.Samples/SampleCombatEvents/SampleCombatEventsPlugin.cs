using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleCombatEvents.SampleCombatEventsPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Combat events")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 戦闘のできごと")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleCombatEvents;

/// <summary>
/// A plugin that runs in the background: it has no window, no widget and no notification.
/// It reads damage and monsters reaching 0 HP from the app's combat events, adds up each player's damage to each enemy,
/// and when a recorded enemy reaches 0 HP, writes the player who dealt it the most damage to the app's log.
/// </summary>
public sealed class SampleCombatEventsPlugin : IStarResonancePlugin
{
    // Read only the kinds the plugin needs: the app stops a plugin that leaves more than 64 MB unread
    // (game messages and combat events together).
    private const PluginCombatEventKinds Kinds = PluginCombatEventKinds.Damage | PluginCombatEventKinds.MonsterHpZero;

    public void Initialize(IPluginContext context)
    {
        _ = ReadCombatEventsAsync(context);
    }

    public void Shutdown()
    {
    }

    /// <summary>
    /// The loop ends by itself when the app stops the plugin. Its body runs on a background thread (ConfigureAwait(false)),
    /// one event at a time, so the dictionaries need no lock.
    /// </summary>
    private static async Task ReadCombatEventsAsync(IPluginContext context)
    {
        // Encounter key, then enemy entity UUID, then character ID to damage.
        var damageByEncounter = new Dictionary<long, Dictionary<long, Dictionary<long, long>>>();

        await foreach (var combatEvent in context.CombatEvents.ReadAsync(Kinds).ConfigureAwait(false))
        {
            switch (combatEvent)
            {
                // The meters do not count damage to an immune enemy.
                case PluginDamageEvent { IsImmune: false } damage:
                    AddDamage(damageByEncounter, damage);
                    break;

                case PluginMonsterHpZeroEvent { IsZero: true, IsRecorded: true, IsOnAppear: false } defeated:
                    WriteTopAttacker(context, damageByEncounter, defeated);
                    break;
            }
        }
    }

    private static void AddDamage(Dictionary<long, Dictionary<long, Dictionary<long, long>>> damageByEncounter, PluginDamageEvent damage)
    {
        // Keep only the current encounter: a new key means the app started a new one.
        if (!damageByEncounter.TryGetValue(damage.EncounterKey, out var damageByEnemy))
        {
            damageByEncounter.Clear();
            damageByEnemy = [];
            damageByEncounter.Add(damage.EncounterKey, damageByEnemy);
        }

        if (!damageByEnemy.TryGetValue(damage.TargetEntityUuid, out var damageByPlayer))
        {
            damageByPlayer = [];
            damageByEnemy.Add(damage.TargetEntityUuid, damageByPlayer);
        }

        damageByPlayer[damage.AttackerCharacterId] = damageByPlayer.GetValueOrDefault(damage.AttackerCharacterId) + damage.Value;
    }

    private static void WriteTopAttacker(
        IPluginContext context,
        Dictionary<long, Dictionary<long, Dictionary<long, long>>> damageByEncounter,
        PluginMonsterHpZeroEvent defeated)
    {
        var damageByPlayer = damageByEncounter.Values
            .Select(damageByEnemy => damageByEnemy.GetValueOrDefault(defeated.EntityUuid))
            .FirstOrDefault(players => players is not null);
        if (damageByPlayer is null || damageByPlayer.Count == 0)
        {
            return;
        }

        var top = damageByPlayer.MaxBy(pair => pair.Value);
        var name = defeated.MonsterId is { } monsterId
            ? context.Names.GetMonsterName(monsterId) ?? $"Monster {monsterId}"
            : "Unknown monster";
        context.Logger.Info($"{name} defeated. Top damage: character {top.Key}, {top.Value}.");
    }
}
