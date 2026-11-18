With `IPluginContext.EncounterControl` (`IPluginEncounterControlService`), a plugin changes how the app splits battles and runs the battle clock. The app's screens (meters, graphs, Timeline, history) show the battles with the changed splits and clock.

## Splitting the current battle (`SplitAsync`)

```csharp
private static async Task SplitNowAsync(IPluginContext context)
{
    PluginEncounterSplitResult result = await context.EncounterControl.SplitAsync().ConfigureAwait(false);
    context.Logger.Info($"{result.Outcome} {result.ClosedEncounterHadRecords}");
}
```

- Closes the current battle and creates a new one, the same as a manual reset in "Aggregation".
- With `keepPreviousInMeter` set to `true`, the meters keep showing the previous battle until the new one has records, when "Hold Results Until Next Event" in Settings is on. With `false`, the new battle is shown at once.
- During a benchmark (also while it waits for the first event), nothing is done, and the task ends with `SkippedBenchmark`.
- The new battle arrives with `GameData.EncounterStarted`, and the closed battle with `GameData.EncounterClosed` when it had records. `ClosedEncounterHadRecords` tells whether the closed battle had records (a battle without records is not saved).

## Split rules (`SetRulesAsync`)

```csharp
private static async Task RegisterRulesAsync(IPluginContext context)
{
    PluginEncounterRulesResult result = await context.EncounterControl.SetRulesAsync(new PluginEncounterRules
    {
        IdleSplitSeconds = 30,
        DungeonClock = PluginDungeonClock.DungeonServerStart,
    }).ConfigureAwait(false);
    if (result.Status == PluginEncounterRulesStatus.Pending)
    {
        context.Logger.Warning($"Held by another plugin: {string.Join(", ", result.PendingItems)}");
    }
}
```

- Registers rules that the app checks while it processes the game's messages. If a plugin split after receiving an event, the records of the messages that arrived in between would go into the previous battle.
- Registering again replaces the previous rules, and `null` removes them. The app does not save the rules, so register them in `Initialize` from the plugin's own settings.

| Property | Contents |
|---|---|
| `IdleSplitSeconds` | Splits the current battle when no combat event arrives for this many seconds (1 to 86400). 0 does not split |
| `IntervalSplitSeconds` | Splits the current battle when this many seconds (1 to 86400) have passed since the first combat event. Stopped periods are counted. 0 does not split |
| `KeepPreviousInMeterOnConditionSplit` | Whether the two splits above keep the previous battle in the meters (the same as `keepPreviousInMeter` of `SplitAsync`) |
| `DungeonStartSplit`, `BossRoomSplit`, `WipeSplit` | Whether to split at the start of a dungeon, on entering a boss room, and on a wipe (`PluginAutoSplitMode`). `HostSetting` follows "Auto Reset by Progress" in Settings, `Split` splits, and `DoNotSplit` does not split |
| `DungeonClock` | The dungeon clock ("Dungeon clock" below). `None` does not use it |

- The idle and interval seconds are checked just before each message is processed and every second. The first combat event after a split goes into the new battle.
- The idle and interval seconds do not split a benchmark battle, a closed battle, a battle whose clock has not started, or a battle with a dungeon clock.
- The rules apply to battles created after registering. The idle and interval seconds also apply to the current battle.
- A value out of range throws `ArgumentOutOfRangeException`, and a combination that does not fit the dungeon clock (`DungeonStartSplit` set to `DoNotSplit`, or `BossRoomSplit` or `WipeSplit` set to `Split`) throws `ArgumentException`.

## Dungeon clock (`DungeonClock`)

- The clock of the battle created at the start of a dungeon runs from the start of the dungeon, and the whole dungeon is counted as one battle.
- Inside that battle, boss rooms, wipes, and the idle and interval seconds do not split, and Auto Pause is not used. The periods when the game's dungeon timer was stopped (from entering a boss room until the boss fight starts, after the boss is defeated, and so on) are excluded from the elapsed time.
- Choose one of two origins:
  - `DungeonStartArrival`: the time the start of the dungeon was received
  - `DungeonServerStart`: the start time of the game's dungeon timer (the origin of the time the game shows). The timer arrives shortly after the start of the dungeon, so the app starts with the received time and changes the origin when the timer arrives, if the battle has no records yet. For dungeons that send no timer, while the app has not received the server's time, and when records came first, the received time stays.
- Read the battle's origin from `GameData.GetEncounter().ClockOrigin`, and the game's dungeon timer from `GameData.Session.DungeonTimer` ([Plugin Development (Data)](Plugin-Development-(Data))).
- Battles created during a dungeon by a manual reset, `SplitAsync`, or the end of a benchmark are also counted from the start of the dungeon.

## Overlap with other plugins

- For `DungeonStartSplit`, `BossRoomSplit`, `WipeSplit`, and `DungeonClock`, each item uses the value of the plugin that registered it first. A plugin that holds the dungeon clock also holds the split at the start of a dungeon.
- For an item another plugin holds with a different value, `SetRulesAsync` ends with `Pending`, and the item goes into `PendingItems`. It is used when the holding plugin removes it or stops, and `RulesChanged` arrives.
- Read the overrides the app uses now from `AppliedOverrides`. "Auto Reset by Progress" in Settings shows the names of the plugins that override it.
- The idle and interval seconds of all plugins are used.

## How it runs

- The methods can be called from any thread. The app runs them on the thread that processes the game's messages, after the messages received before the call. While capture is stopped, it runs them on the UI thread.
- The continuation of a `Task` does not run on the app's thread. `RulesChanged` arrives on the UI thread.
- When the app stops a plugin, it removes the plugin's rules. After that, the methods do nothing and end with `Unavailable`.
