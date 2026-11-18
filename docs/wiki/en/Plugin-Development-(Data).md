This page explains how to read game data with a plugin. What every plugin needs is described in [Plugin Development](Plugin-Development).

| `IPluginContext` member | Contents |
|---|---|
| `GameData` (`IPluginGameDataService`) | Values the app builds from the game's traffic (the connection and map, players, nearby enemies, buffs, battle statistics and records, and your own modules), the Aggregation settings, and events when they change |
| `Names` (`IPluginNameService`) | Looks up names from the IDs of skills, buffs, monsters, class specs, maps, and items |
| `Packets` (`IPluginPacketService`) | The game messages themselves |
| `CombatEvents` (`IPluginCombatEventService`) | The flow of combat events the app processes (damage, records of skills, damage taken, and the Timeline, buffs being applied and removed, and monsters' skills and 0 HP) |
| `History` (`IPluginHistoryService`) | Reads and opens saved battles (history), and saves the plugin's values with a battle ([Plugin Development (History)](Plugin-Development-(History))) |

## Game data (`GameData`)

The following example writes everyone's total damage to the log when a battle ends.

```csharp
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(MyPlugin.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "私のプラグイン")]
[assembly: PluginAuthor("YourName")]
[assembly: PluginVersion("1.0.0")]

namespace MyPlugin;

public sealed class Plugin : IStarResonancePlugin
{
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.GameData.EncounterClosed += OnEncounterClosed;
    }

    private void OnEncounterClosed(object? sender, PluginEncounterResult result)
    {
        foreach (PluginEncounterPlayer player in result.Totals.Players)
        {
            _context?.Logger.Info($"{player.CharacterId}: {player.Damage.Total}");
        }
    }

    public void Shutdown()
    {
        if (_context is not null)
        {
            _context.GameData.EncounterClosed -= OnEncounterClosed;
        }
    }
}
```

| Member | Contents | Event when it changes |
|---|---|---|
| `CurrentServer` | The game server currently being read (`Asia`, `China`, `Global`, or `SoutheastAsia` of `PluginGameServer`). `Unknown` if it is not known | None |
| `Session` | The state of the connection and map (`PluginSessionState`) | `SessionChanged` |
| `Players` | The list of players (`PluginPlayer`) | `PlayersChanged` |
| `Entities` | The list of nearby monsters (`PluginEntity`) | `EntitiesChanged` |
| `SelfModules` | Your owned modules and equipped slots (`PluginModuleInventory`). `null` if they have not arrived yet | `SelfModulesChanged` |
| `GetPlayerBuffs(characterId)` | The buffs and debuffs currently on that player (`PluginBuff`) | None (read again) |
| `GetEntityBuffs(entityUuid)` | The buffs and debuffs currently on that entity | None (read again) |
| `GetPlayer(characterId)` | That player's row (`PluginPlayer`). For a player not in `Players`, the last values after leaving the list, or the values received from a namecard or chat. `null` if there are none | None (read again) |
| `GetPlayerBuild(characterId)` | That player's build (`PluginPlayerBuild`; "Builds" below). `null` for a player whose `GetPlayer` is `null` | None (read again) |
| `GetEncounter()` | The statistics of everyone in the battle shown in "[DPS Meter](DPS-Meter)" (`PluginEncounterSnapshot`) | None (read again) |
| `GetSkillTotals(characterId, kind)` | That player's breakdown by skill in the same battle (`PluginSkillTotals`). `kind` is damage (`Damage`) or healing (`Healing`) | None (read again) |
| `GetShownEncounter()` | A battle reader (`IPluginEncounterRecord`) for the same battle as `GetEncounter`. `null` if there is no battle yet | `ShownEncounterChanged` |
| `GetLiveEncounter()` | A battle reader for the current battle. `null` if there is no battle yet | `EncounterStarted` |
| `AggregationSettings` | The values of "Aggregation" in Settings (`PluginAggregationSettings`) | `AggregationSettingsChanged` |

| Event | When it arrives |
|---|---|
| `EncounterStarted` | When the app creates a new battle |
| `EncounterClosed` | When a battle with records is closed. The statistics of the closed battle and everyone's breakdown by skill (`PluginEncounterResult`) are passed |
| `EncounterSaved` | When a battle is saved to the history. The battle's `EncounterKey` and its history ID (`HistoryId`) are passed (`PluginEncounterSaved`) |
| `MapEntered` | When you move to another map or channel |
| `LoggedOut` | When the game returns to the login screen |

- The values can be read from any thread. All events arrive on the UI thread.
- `SessionChanged`, `PlayersChanged`, `EntitiesChanged`, `SelfModulesChanged`, `ShownEncounterChanged`, and `AggregationSettingsChanged` combine changes within a short time into one and report the values at that time. The other five arrive every time they happen.
- The values are read-only copies and are not updated later. Read again to get new values. If you read two values one after another, they may be from different moments. A battle reader (`IPluginEncounterRecord`) is not a copy; it reads the values each time you call it ("Battle reader" below).
- `Players`, `Entities`, `SelfModules`, and the result of `EncounterClosed` are the same objects passed to every plugin. Do not cast `IReadOnlyList` and similar types to their original types to modify them.
- Event handlers run on the UI thread, the same thread as the app's screen. Heavy work also stops the widgets.
- An exception in an event handler stops the plugin.

### Connection and map (`Session`)

| Property | Contents |
|---|---|
| `SelfCharacterId` | Your character ID. `null` until it is known and after logging out |
| `SceneId` | The ID of the current map. Pass it to `Names.GetSceneName` |
| `SeasonId` | The ID of the current season |
| `DungeonState` | Outside a dungeon (`NotInDungeon`), inside (`InDungeon`), or after it ends (`Finished`) |
| `IsMidLaunch` | The app started reading the game's traffic partway through, and your information is not complete yet (such as when the app is started while the game is running). It becomes `false` when you change maps |
| `SelfTargetEntityUuid` | The UUID of your target enemy (the enemy "[Target Info](Target-Info)" shows). It is the enemy you last used a skill on; it becomes `null` when that enemy leaves your surroundings and comes back when you next use a skill on it |
| `DungeonFlowState`, `DungeonFlowChanges` | The dungeon's progress state (`PluginDungeonFlowState`) and the state changes since entering the map (`PluginDungeonFlowChange`: the state and the time it was received (UTC), oldest first). `null` and empty if none has been received |
| `DungeonTimer` | The game's dungeon timer (`PluginDungeonTimer`): the start time (Unix seconds on the server's clock, and the UTC time on this PC's clock), the time limit, whether it counts down, the total seconds it has been stopped, and the value it shows while stopped. `null` for dungeons that send no timer and outside dungeons |

- If the app is started while the game is running, your character ID and owned modules may not be known until you change maps.
- The game's lock-on and other players' targets are not available because the game does not send them.
- The progress states are the game's values and arrive in the order `Active` (entered the dungeon map), `Ready` (before the start), `Playing` (started), `End` (ended), `Settlement` (the result screen), and `Vote` (the vote). `None` is the game's initial value. A state that arrives again in a row is not added to the changes. They become empty when you change maps or log out.

### Players (`Players`)

- It includes yourself, your party members, the players around you, and the people with records in the battle shown in DPS Meter. It is the underlying list of "[Player List](Player-List)", without the Player List filter or sorting by name.
- The order is yourself, your party members (in party number order), people who are neither around you nor in your party, people in combat, and then everyone else.
- Names follow "Player name" in Settings. Hidden names are `*****`. A name may be empty when it is not known.

| Property | Contents |
|---|---|
| `CharacterId` | The character ID. It is the real value even when names are hidden |
| `Name` | The name |
| `ProfessionId` | The class ID. 0 if it is not known |
| `SpecState`, `SpecId` | The class spec state (`Unknown`, `NotEquipped` (Class I), `Specialized`, or `Transformation`). `SpecId` holds an ID only when it is `Specialized` (pass it to `Names.GetClassSpecName`) |
| `Level`, `AbilityScore`, `SeasonStrength` | The level, ability score, and season strength. 0 if they are not known |
| `CurrentHp`, `MaxHp`, `CurrentShield` | The amounts of HP and shield. The shield is known only for yourself and the people around you |
| `PartyNumber` | The party number. `null` outside the party |
| `IsSelf`, `IsNpc`, `IsPartyMember`, `IsNearby` | Whether this is you, a helper NPC, a party member, or someone around you |
| `IsInCombat`, `IsIncapacitated` | In combat, and defeated. The combat state is known only for yourself and the people around you |
| `PsychoscopeRootBuffId`, `IsPsychoscopeInactive` | The Deep-Slumber Psychoscope type (the ID of its root buff), and whether it is not enabled |
| `IsFromRecord` | The person appears only in the opened history of Aggregation. Always `true` in the rows of `GetPlayers` for a saved battle |

### Builds (`GetPlayerBuild`)

- The values come from the same sources as "[Talent Info](Talent-Info)", "[Deep-Slumber Psychoscope Info](Deep-Slumber-Psychoscope-Info)", "[Module Info](Module-Info)", and "[Equipment Info](Equipment-Info)". Each call makes a new snapshot.
- While the history of Aggregation is open, a player who appears only in that history has the values of that battle, and others have the current values (the same as the player widgets). Read the values of a saved battle with `GetPlayerBuild` of the battle reader.
- Each list is `null` if it has not been received, and empty if it was received empty. Comparing `PluginPlayerBuild` values with `==` does not compare the contents of the lists, so compare the contents yourself.

| `PluginPlayerBuild` property | Contents |
|---|---|
| `RootBuffIds` | The IDs of the root buffs on the player (talents, Deep-Slumber Psychoscope node effects and advanced node effects, factors, and module effects), in ascending order. `null` until all of the player's buffs have been received |
| `TalentNodeIds` | The IDs of the talent tree nodes taken for the current class, in ascending order. Only for yourself; `null` for other players |
| `LastUsedReplaceableSkills` | For the talents that replace a skill without adding a buff, a map from the original skill ID to the ID of the skill used last (the original or the replacement). `null` while none of them has been used |
| `Equipment` | The equipment (`PluginEquipmentItem`: the slot `Slot` and the equipment ID `EquipmentId`; use `Names.GetItemName` for the name). When the game's value could not be read, `IsReadable` is `false` and the list is empty |
| `SkillLevels` | The skill ID, level, and grade (`SkillId`, `Level`, and `RemodelLevel` of `PluginSkillLevel`). For yourself, the learned skills of the current class, the role skills, and the selected imagines; for other players, the list the game sends (imagines: only the two equipped); for party members who are not near you, the last list received from the party sync |
| `Factors` | The factors on the active type of the current season (the slot number `SlotId` and the item ID `ItemId`). Only for yourself; `null` for other players |
| `ModuleEffects` | The effects of the equipped modules (`PluginModuleEffect`). `LinkCount` is the sum over the equipped modules. Only for yourself; `null` for other players |

### Nearby enemies (`Entities`)

- It includes only nearby entities of the monster type. It is the underlying list of "[Entity List](Entity-List)", without the Entity List filter or sorting by name.
- The order is entities in combat first, then the rest, and within each, bosses, elites, normal enemies, and others.

| Property | Contents |
|---|---|
| `EntityUuid` | The UUID of the entity. Pass it to `GetEntityBuffs` |
| `MonsterId` | The monster ID (pass it to `Names.GetMonsterName`). For a monster that was around before the app started, it is `null` until the monster leaves your surroundings once and appears again |
| `CurrentHp`, `MaxHp` | HP |
| `IsInCombat` | The combat state |
| `Cast` | The skill being cast (`SkillId`, `StartedAtUtc`, and `TotalSeconds`). It is set when a skill with a cast time starts and remains until the skill changes next. It can remain after `TotalSeconds` has passed, so decide the end by time |
| `TargetCharacterId` | The character ID of the player being targeted. It is not necessarily the top of the aggro list |
| `HateRanking` | The aggro list (players only, highest first) |
| `Level` | The level (the same value as "[Entity List](Entity-List)"). 0 if it is not known |
| `CurrentShield` | The amount of shield |

### Buffs (`GetPlayerBuffs`, `GetEntityBuffs`)

- All the buffs currently applied are included. The filtering and grouping of what to show, as in "[Buff List](Buff-List)", are not applied.
- The lists are empty for people and enemies that are not around you. Everything is cleared when you change maps.
- Even when the history of Aggregation is open, these are the current values.

| Property | Contents |
|---|---|
| `BuffUuid` | The UUID of each buff |
| `BuffId` | The buff ID (pass it to `Names.GetBuffName`) |
| `Level`, `Layer` | The level and the layers (stack count) |
| `DurationSeconds` | The total duration (it grows when the buff is extended). `null` if there is no duration |
| `EndUtc` | The time the buff ends (UTC). `null` if there is no duration or the remaining time is not known |
| `IsRemainingUnknown` | The remaining time is not known |
| `CasterEntityUuid`, `CasterCharacterId` | The UUID of the entity that applied the buff (0 if not known), and its character ID if it was a player |

### Battle statistics (`GetEncounter`, `GetSkillTotals`)

- They read the battle shown in DPS Meter: the opened battle when the history of Aggregation is open, or the previous battle when it is shown because of "Hold Results Until Next Event" in Settings.
- If there is no battle yet, `GetEncounter` returns empty statistics whose `EncounterKey` is 0.
- `GetShownEncounter` reads the same battle through a battle reader, and `GetLiveEncounter` reads the current battle ("Battle reader" below).

| `PluginEncounterSnapshot` property | Contents |
|---|---|
| `IsHistory` | The battle was read from the saved history. For `GetEncounter`, the history of Aggregation is open |
| `StartUtc` | The time the battle clock started (UTC). `null` if it has not started yet. Usually the time of the first combat event; in a battle with a dungeon clock (`ClockOrigin`), the time the dungeon started |
| `Elapsed` | The elapsed time of the battle (excluding Auto Pause, and in a battle with a dungeon clock, the periods when the game's dungeon timer was stopped) |
| `IsClosed` | The battle has ended |
| `Players` | Players who have any damage, healing, or damage taken (`PluginEncounterPlayer`). Not filtered by party, and in no particular order |
| `EncounterKey` | A number (from 1) that identifies the battle while the app is running. 0 if there is no battle. The current battle keeps the same number after it is saved. A battle read from the history gets a new number each time it is read, so identify saved battles by `HistoryId`. When the app is restarted, the count starts again from 1 |
| `HistoryId` | The history ID ([Plugin Development (History)](Plugin-Development-(History))). `null` until the battle is saved, and for a battle that has no records and is not saved. Always `null` in the result of `EncounterClosed`; receive the ID with `EncounterSaved` |
| `LastCombatEventUtc` | The time (UTC) the last combat event (such as damage or healing) was received. `null` if there has not been one yet. The battle clock stops `Info.AutoPauseSeconds` seconds after this (it does not stop if that is 0; see "Battle values" below) |
| `IsAggregationStopped` | In the current battle, the app records nothing and does not run the battle clock ("Stop Aggregation in Non-Combat Areas" in Settings is on and you are in a non-combat area). `false` for a closed battle and for a battle read from the history |
| `ClockOrigin` | The origin of the battle clock (`PluginClockOrigin`): the first combat event, the time the start of the dungeon was received, or the start time of the game's dungeon timer. The first combat event unless a plugin has registered a dungeon clock ([Plugin Development (Battle Splits)](Plugin-Development-(Battle-Splits))) |
| `FirstCombatEventUtc` | The time (UTC) the first combat event was received. `null` if there has not been one yet. Healing, threat, and Resilience Break are recorded from this time. The same as `StartUtc` unless the battle has a dungeon clock |
| `BenchmarkFirstTargetEntityUuid` | When a benchmark aggregates only the first enemy attacked, the UUID of the first enemy you damaged. Only damage, threat, and Resilience Break against this enemy are counted. `null` until it is decided, in other battles, and in a battle read from the history |

- `PluginEncounterPlayer` has `CharacterId` and the totals (`PluginCombatTotals`) of damage (`Damage`), healing (`Healing`), and damage taken (`DamageTaken`).
- `PluginCombatTotals` has the total (`Total`), the per-second value (`PerSecond`, the same as DPS Meter), the per-second value over that player's active time (`ActivePerSecond`, the same as the skill details), the hit count (`Hits`), and the totals and counts of crits and lucky hits.
- `PluginSkillTotals` has the same rows as the skill details: the row key (`RowKey`), the row name (`Name`, `null` if there is none), the total, the per-second value, the hit count, the crit rate (`CritRate`), and the share of that player's total (`Share`). They are in order from the largest total.
- `EncounterStarted` arrives on a map change, at the start of a dungeon, at a split by "Auto Reset by Progress", on a manual reset, when a benchmark starts or stops, on logout, and at a split requested or ruled by a plugin ([Plugin Development (Battle Splits)](Plugin-Development-(Battle-Splits))). It can arrive several times for one map change.
- There is no event when the battle clock starts. Read `GetEncounter().StartUtc`.
- `EncounterClosed` arrives when a battle with records is closed and the next battle is created. It does not arrive for the last battle when the app is closed. A closed battle can be read with `GetEncounter` only while the meter is showing it, so keep the values you need from the result you receive right away. After the battle is saved, it can be read again with `LoadAsync` in [Plugin Development (History)](Plugin-Development-(History)), using the `HistoryId` of `EncounterSaved`.
- `EncounterSaved` arrives once each time the app saves a battle to the history, after `EncounterClosed` for the same battle and before the next `EncounterStarted`. A battle without records is not saved, so it does not arrive for such a battle. It does not arrive for the last battle when the app is closed either.
- `ShownEncounterChanged` arrives after the battle read by `GetEncounter` and `GetShownEncounter` changes: when a history entry is opened (by the user or with `OpenAsync`), when the app returns to the current battle (including automatically), when it starts showing a new battle, and when it stops showing the previous battle. It also arrives when the same history entry is opened again. It does not arrive when a new battle is created while the previous battle is being shown, or when a history entry could not be opened.

### Battle reader (`GetShownEncounter`, `GetLiveEncounter`)

- `GetShownEncounter` returns a battle reader (`IPluginEncounterRecord`) for the same battle as `GetEncounter`, and `GetLiveEncounter` returns one for the current battle. They return `null` if there is no battle yet.
- `GetLiveEncounter` returns the current battle even while the history of Aggregation is open or the previous battle is being shown.
- A battle reader keeps reading the battle it was created for. It reads the same battle even after the app moves on to the next battle.
- While you hold a battle reader, that battle stays in memory. Hold it only while you need it.
- It can be called from any thread. For the current battle, each call reads the values at that moment, so two calls one after another may return values from different moments. A battle read from the history does not change, except for the plugin values.
- Saved battles can also be read with the same battle reader (`LoadAsync` in [Plugin Development (History)](Plugin-Development-(History))).

| `IPluginEncounterRecord` member | Contents |
|---|---|
| `EncounterKey`, `HistoryId` | The battle number and the history ID (the same as in `PluginEncounterSnapshot` above). For the current battle, `HistoryId` is set when the battle is saved |
| `IsHistory` | A battle read from the saved history (a battle opened from the history of Aggregation, or a battle read with `LoadAsync`). `false` for the current battle, and for the previous battle while it is being shown |
| `Info` | Values such as the map, the server, and the benchmark (`PluginEncounterInfo`; "Battle values" below) |
| `GetTotals()` | Everyone's statistics (`PluginEncounterSnapshot`). They are the same values as DPS Meter, and for the battle shown in DPS Meter they are the same as `GetEncounter()` |
| `GetSkillTotals(characterId, kind)` | That player's breakdown by skill (the same rows as `GetSkillTotals` above). Empty for a player who has no totals in that battle |
| `ToCombatOffset(utc)` | Converts a time to the battle's elapsed time ("Battle elapsed time" below) |
| `GetPlayers()` | The players who were in a saved battle ("Players of a saved battle" below). `null` for the current battle |
| `GetMonsters()` | The monsters remaining in the battle ("Monsters of a battle" below) |
| `GetPlayerBuffIntervals(characterId)` | The intervals when buffs were on that player and when the player was around you ("Buff intervals" below) |
| `GetPlayerBuild(characterId)` | That player's build in a saved battle ("Builds" above). The values at the time of saving; `TalentNodeIds`, `Factors`, and `ModuleEffects` are only for the character that was logged in when the battle was saved, and `SkillLevels` is the list the game sent. `null` for the current battle and for a player not in `GetPlayers` |
| `GetSkillActivations(characterId, startIndex)` | That player's skill records (`PluginSkillActivation`), from the `startIndex`-th on ("Battle records" below) |
| `GetTakenHits(startIndex)` | The records of damage taken by players (`PluginTakenHit`), from the `startIndex`-th on ("Battle records" below) |
| `GetCombatLog(startIndex)` | The records of "[Timeline](Timeline)" (`PluginCombatLogRecord`), from the `startIndex`-th on ("Battle records" below) |
| `GetPerSecondTotals(characterId, kind)` | That player's damage or healing per second ("Per-second totals" below) |
| `GetPluginData(key)` | A value that this plugin saved with the battle. `null` if there is none ("Saving with a battle" in [Plugin Development (History)](Plugin-Development-(History))) |

### Battle values (`Info`)

| `PluginEncounterInfo` property | Contents |
|---|---|
| `CreatedUtc` | The time the app created the battle (UTC). The battle clock usually starts after that; in a battle with a dungeon clock it can start slightly earlier (`StartUtc`). The history list is ordered by this time, newest first |
| `SceneId`, `DungeonDifficulty` | The map ID (`null` if it is not known) and the dungeon difficulty (0 if there is none). Look up the name with `Names.GetSceneName(sceneId, dungeonDifficulty)` |
| `ChannelLine` | The channel number. `null` in places without channels |
| `Server` | The game server. The app writes it when it saves the battle, so for the current battle it is `Unknown` until the battle is saved. Read the current server with `CurrentServer` |
| `SeasonId` | The season ID. Like `Server`, it is `null` until the battle is saved. Read the current season with `Session.SeasonId` |
| `IsWipe` | The battle was closed on a wipe by "Auto Reset by Progress" in Settings |
| `BenchmarkSeconds` | For a benchmark battle, the benchmark duration (seconds). It is also set while waiting for the benchmark to start. 0 if it is not a benchmark |
| `BenchmarkFirstTargetOnly` | For a benchmark, whether only the first enemy attacked is aggregated. `false` if it is not a benchmark. The app does not save it, so it is `null` for a battle read from the history |
| `AutoPauseSeconds` | The number of seconds of "Auto Pause" in Settings when the app created the battle. The battle clock stops this many seconds after the last combat event. 0 when Auto Pause is set not to pause, for a benchmark battle, and for a battle with a dungeon clock |

- For the current battle, the channel, the difficulty, and `IsWipe` can change, so read `Info` again when you need it.

### Battle elapsed time (`ToCombatOffset`)

- Converts a time to the battle's elapsed time, as used on the horizontal axis of the app's graphs and in the Timeline. It is the time since `StartUtc` minus the periods before that time when the clock was stopped (Auto Pause, and in a battle with a dungeon clock, the periods when the game's dungeon timer was stopped).
- A time during a stop becomes the elapsed time at the moment the clock stopped.
- It returns `null` before the battle clock starts, and a negative value for a time before `StartUtc`.
- Pass a UTC time. It is not converted based on `DateTime.Kind`.
- For the current battle, the value for a time after the last combat event can change as the battle continues.

### Recording rules

These are the rules for what the app records in a battle. The statistics and the casts in `GetMonsters` are recorded under these rules.

- Nothing is recorded in a battle where `IsAggregationStopped` is `true`.
- Healing, threat, and Resilience Break are recorded only after the first combat event (from `FirstCombatEventUtc`).
- In a benchmark battle (`Info.BenchmarkSeconds` is greater than 0), only events from `StartUtc` until the benchmark duration has passed are recorded. Of those, only your own events and events that belong to no player (enemy casts, boss skill telegraphs, and enemy actions) are recorded.
- When `Info.BenchmarkFirstTargetOnly` is `true`, only damage, threat, and Resilience Break against the first enemy you damaged (`BenchmarkFirstTargetEntityUuid`) are counted. Threat and Resilience Break from before that enemy is decided are not counted.

### Players of a saved battle (`GetPlayers`)

- It returns the players only for a battle read from the history (`IsHistory` is `true`), and `null` for the current battle. Read the players of the current battle with `Players`.
- It includes people who have any damage, healing, threat, Resilience Break, or damage taken, or a Timeline record, in that battle. They are in no particular order.
- Each row is the same `PluginPlayer` as in `Players` ("Players" above). The values are only those saved with the battle (the values the app was showing when it saved the battle). The current party and the values the app holds now are not used, so a row can differ from the same person's row in `Players`.
- `IsFromRecord` is `true`, `CurrentHp` is the same as `MaxHp`, `CurrentShield` is 0, `PartyNumber` is `null`, and `IsPartyMember`, `IsNearby`, `IsInCombat`, and `IsIncapacitated` are `false`.
- `IsSelf` is the character logged in at the time of the call. Names follow "Player name" in Settings at the time of the call.

### Monsters of a battle (`GetMonsters`)

- For the current battle, these are the monsters around you, and, among the monsters that have left your surroundings, those that are bosses or appear in "[Timeline](Timeline)" or "[Damage Taken Log](Damage-Taken-Log)". Each call reads the values at that moment.
- For a battle read from the history, these are the monsters saved with the battle. The monsters saved are those with cast records, those that appear in enemy actions in the Timeline, and those that dealt damage to players. A boss without records is not saved.
- They are in no particular order.

| `PluginRecordedMonster` property | Contents |
|---|---|
| `EntityUuid` | The UUID of the entity. While the monster is around you, it is the same as `EntityUuid` in `Entities` |
| `MonsterId` | The monster ID (pass it to `Names.GetMonsterName`). `null` if it has not been received |
| `Hp`, `MaxHp` | The last received HP and maximum HP. For a battle read from the history, the values when the battle was saved. 0 if they have not been received |
| `Casts` | The monster's casts (`PluginRecordedCast`), in the order they were received. Skills with a cast time and skills that show a warning on the combat screen |

- `PluginRecordedCast` has the skill ID (`SkillId`, pass it to `Names.GetSkillName`), the time its start was received (`StartedAtUtc`, UTC), and whether it is a skill that shows a warning (`IsWarning`).
- Casts by a summoned entity that has no name of its own are recorded as casts of the monster that summoned it.
- Casts are recorded only while the app is recording under "Recording rules" above.

### Buff intervals (`GetPlayerBuffIntervals`)

- These are the intervals the app records for "[Buff Uptime](Buff-Uptime)". They cover the buffs shown in the Buff List (not debuffs), for players only. In a benchmark battle, only you are recorded, and nothing is recorded in a battle where `IsAggregationStopped` is `true`.
- It returns `null` when there is no record for that person and before the battle clock starts.
- The times are between `StartUtc` and the end of the battle clock. A buff that was on before the battle clock started begins at `StartUtc`. For the current battle, a buff that is still on extends to the current end of the battle clock.
- A buff whose removal was not received ends at the time its duration ran out.
- To match the horizontal axis of a graph, convert the times to the battle's elapsed time with `ToCombatOffset`.

| Property | Contents |
|---|---|
| `Buffs` | The intervals per buff ID (`PluginBuffInterval`), in the order the buffs were first applied in that battle |
| `PresentIntervals` | The intervals when that person's values were being received (when the person was around you), oldest first. Buffs outside these intervals cannot be known. When the person leaves your surroundings, the intervals of the buffs that were on also end there |
| `PluginBuffInterval.BuffId` | The buff ID (pass it to `Names.GetBuffName`). It is the ID as received; buffs that "Buff Uptime" combines into one row are not combined |
| `PluginBuffInterval.IsPermanent` | The buff had no duration when its last interval started (when it was applied while it was not on), that is, a permanent buff |
| `PluginBuffInterval.Intervals` | The intervals when one or more of the buff were on, oldest first and not overlapping |

- An interval (`PluginTimeInterval`) is a start (`StartUtc`) and an end (`EndUtc`) in UTC.

### Battle records (`GetSkillActivations`, `GetTakenHits`, `GetCombatLog`)

- These return what the app recorded in the battle, in the order it was recorded (oldest first), from the `startIndex`-th on. Passing the number you have read so far as `startIndex` reads only the new ones.
- If `startIndex` is negative or greater than the number of records at the time of the call, `ArgumentOutOfRangeException` is thrown. For `GetSkillActivations`, the number is per player.
- They are recorded under "Recording rules" above. `GetSkillActivations` also contains records from before the battle clock starts.
- A battle read from the history can be read in the same way.
- The time (`ArrivalTimeUtc`) is when it was received (UTC). For the end of a telegraph, it is the time the telegraph bar ended. To match the horizontal axis of a graph, convert it with `ToCombatOffset`.
- The records of damage taken and the Timeline records share a sequence number (`Sequence`). To list the two together, sort them by this number.

| Method | Contents of one record |
|---|---|
| `GetSkillActivations` | `PluginSkillActivation`: the skill ID (`SkillId`), the icon frame (`Frame`), and the time it was received. Your own are recorded when the game accepts the skill, and other players' when they start using it. Look up the name with `Names.GetSkillActivationName` |
| `GetTakenHits` | `PluginTakenHit`: the player who took the damage (`CharacterId`), the entity that dealt it (`Attacker`), what it came from (`Source` and `SourceId`), the value, the element, the type, and whether it incapacitated the player (`IsLethal`). Damage of 0 is not included. Damage from your own skills, falls, and players is included |
| `GetCombatLog` | Types derived from `PluginCombatLogRecord`: enemy casts (`PluginCastRecord`), boss skill telegraphs and their ends (`PluginAnnouncementRecord`), incapacitation without damage (`PluginPlayerDeathRecord`), enemy healing (`PluginEnemyHealRecord`), damage from enemies to enemies (`PluginEnemyDamageRecord`), and buffs applied by enemies (`PluginEnemyBuffRecord`) |

- `Frame` is the frame shown behind the skill icon in "[DPS Trend Graph](DPS-Trend-Graph)" and others (`Imagine`: Imagines, `Ultimate`: ultimate skills, both including the skills derived from them; `Standard`: other skills).
- An entity in a record (`PluginCombatParty`) has the UUID (`EntityUuid`), the kind (`Kind`: `Player`, `Monster`, `System`, or `Other`), the character ID for a player (`CharacterId`), and the monster ID for a monster (`MonsterId`, `null` if not received). `System` is a game mechanic that is neither a player nor a monster, shown as "System" in the Timeline.
- When it is known, the action of a summoned entity is recorded as an action of the player or monster that summoned it (the same as "Timeline").
- What the damage came from (`Source`) is a skill, a projectile, a buff, a fall, and so on, and `SourceId` is its ID. For damage from a buff or a summoned entity, the original skill (`BuffSourceSkillId`, `SummonSourceSkillId`) is also included.

### Per-second totals (`GetPerSecondTotals`)

- That player's damage (`Damage`) or healing (`Healing`) per second of the battle clock. The values are the same as "[DPS Trend Graph](DPS-Trend-Graph)" and "[HPS Trend Graph](HPS-Trend-Graph)".
- `Totals[i]` is the total from second i to second i+1 (including i+1) of the battle's elapsed time. Damage received late is also included in the second of its time.
- For the current battle, only complete seconds are included. For a closed battle (`IsClosed`), the last part of less than a second is also included. `Elapsed` is the battle's elapsed time when it was read.
- It returns `null` when that player is not in the battle and before the battle clock starts.
- Every second is counted again at each call. There is no change event, so read it again at a fixed interval.

### Your modules (`SelfModules`)

- `Modules` holds your owned modules (`PluginModuleItem`: `Uuid`, the item ID `ConfigId`, `Quality`, and the effects `Effects`), and `EquippedUuidBySlot` holds the equipped slot numbers and the `Uuid` of each module.
- An effect (`PluginModuleEffect`) has the effect ID (`EffectId`) and the link count (`LinkCount`, `null` if it has not arrived).
- `SelfModulesChanged` arrives every time you enter a map, even if the contents are the same.
- After logging out, and after changing "Network Adapter" or "Game Executable" in Settings, it is `null` until you next change maps.

### Aggregation settings (`AggregationSettings`)

- These are the values of "Aggregation" in Settings, as the user set them. When the settings are saved in Settings and a value changes, `AggregationSettingsChanged` arrives.

| `PluginAggregationSettings` property | Item in Settings |
|---|---|
| `StopInNonCombatAreas` | Stop Aggregation in Non-Combat Areas. When it is on, nothing is recorded and the battle clock does not run in non-combat areas (`IsAggregationStopped`) |
| `AutoPauseSeconds` | The number of seconds of Auto Pause. 0 when it does not pause. It takes effect from battles created after the change (the value for each battle is `Info.AutoPauseSeconds`) |
| `SplitOnProgress` | Auto Reset by Progress. A new battle is created when a dungeon starts, when you enter a boss room, and on a wipe. This stays the user's setting; read plugins' overrides from `EncounterControl.AppliedOverrides` |
| `KeepResultUntilNextEvent` | Hold Results Until Next Event. After a split by progress or a map change, the meters show the previous battle until records come into the new battle |
| `ReturnToLiveOnNextEvent` | Auto-exit History View. If something happens in the current battle while a history entry is open, the app returns to the current battle. It does not return while a plugin keeps the entry open with `KeepHistoryOpen` |
| `MaxSavedEncounters` | Max History Entries. 0 if there is no limit. When it is exceeded, the oldest battles are deleted first, together with their plugin values |
| `BenchmarkSeconds` | Benchmark Duration (seconds). It takes effect from the next benchmark (the value for a benchmark battle is `Info.BenchmarkSeconds`) |
| `BenchmarkFirstTargetOnly` | Measure Only the First Enemy Attacked. It takes effect from the next benchmark (the value for a benchmark battle is `Info.BenchmarkFirstTargetOnly`) |

## Looking up names (`Names`)

| Method | Name it looks up |
|---|---|
| `GetSkillName(skillId)` | Skill |
| `GetSkillActivationName(skillId)` | A used skill. The same name as the icons in "[DPS Trend Graph](DPS-Trend-Graph)" and others; if the skill has a meter row name, that name |
| `GetBuffName(buffId)` | Buff |
| `GetMonsterName(monsterId)` | Monster |
| `GetClassSpecName(specId)` | Class spec (`PluginPlayer.SpecId`) |
| `GetSceneName(sceneId, dungeonDifficulty)` | Map. If there is a difficulty name, "map name-difficulty name". `dungeonDifficulty` can be omitted |
| `GetItemName(itemId)` | Item (including equipment) |

- The names are in the app's display language. If there is no name in that language, the Chinese name is returned, and `null` if there is neither.
- Unlike the app's screens, internal names and internal IDs are not added. "Internal ID" and "Unnamed skills and buffs" in Settings do not apply either.
- There are no names for Class I or transformations, so `GetClassSpecName` returns `null` for them.
- There are no methods to look up the names of classes, Deep-Slumber Psychoscope types, seasons, or module effects.
- They can be called from any thread. When the display language changes, look the names up again in `Localization.CultureChanged`.

## Game messages (`Packets`)

Pass the types to receive to `ReadAsync`, and read the arriving messages with `await foreach`.

```csharp
private static async Task ReadMessagesAsync(IPluginContext context, IReadOnlyCollection<PluginPacketType> types)
{
    await foreach (PluginPacketMessage message in context.Packets.ReadAsync(types).ConfigureAwait(false))
    {
        context.Logger.Info($"{message.Kind} {message.ServiceId}/{message.MethodId}: {message.Payload.Length} bytes");
    }
}
```

- Start it in `Initialize` without awaiting it, as in `_ = ReadMessagesAsync(context, types);`.
- A type to receive (`PluginPacketType`) is a combination of the kind (`Kind`), the service ID, and the method ID. Pass one or more. Only messages that match all three arrive.
- The kinds are notifications from the server (`Notify`), calls from the game to the server (`Call`), and responses to calls (`Return`). The IDs of a `Return` are those of the call it answers.
- Messages that arrive after `await foreach` starts are passed in the order they arrive. Earlier ones are not passed. Leaving the loop stops receiving.
- The contents (`Payload`) are protobuf bytes. `Notify` and `Return` are decompressed, and `Return` stays in the form that wraps the response. `Call` is not decompressed.
- `Payload` is a copy made by the app, so you can keep it as long as you like.
- If the total of contents not yet taken, together with "Combat events" below, exceeds 64 MB for one plugin, the plugin is stopped and its row in "[Plugins](Plugins)" shows "Error" (the "Plugin Error" window appears, and the plugin is loaded again at the next startup). Narrow down the types you receive, and take what arrives right away.
- `Initialize` is called on the UI thread, so without `ConfigureAwait(false)` the body of the loop runs on the UI thread.
- When the app stops the plugin, the loop ends. If you did not pass `Stopping`, it ends without an exception; if you passed it, it ends with `OperationCanceledException`.
- This wiki does not describe the IDs and content formats of game messages.

## Combat events (`CombatEvents`)

Pass the kinds to receive to `ReadAsync`, and read the combat events the app processed with `await foreach`.

```csharp
private static async Task ReadCombatEventsAsync(IPluginContext context)
{
    PluginCombatEventKinds kinds = PluginCombatEventKinds.SkillActivation | PluginCombatEventKinds.MonsterHpZero;
    await foreach (PluginCombatEvent combatEvent in context.CombatEvents.ReadAsync(kinds).ConfigureAwait(false))
    {
        if (combatEvent is PluginSkillActivationEvent activation)
        {
            context.Logger.Info($"{activation.CharacterId}: {context.Names.GetSkillActivationName(activation.Record.SkillId)}");
        }
        else if (combatEvent is PluginMonsterHpZeroEvent { IsZero: true, MonsterId: { } monsterId })
        {
            context.Logger.Info($"{context.Names.GetMonsterName(monsterId)}: 0 HP");
        }
    }
}
```

| Kind | Type | When it comes |
|---|---|---|
| `Damage` | `PluginDamageEvent` | When a player or an entity a player summoned deals damage to an enemy and the app adds it to "[DPS Meter](DPS-Meter)" |
| `SkillActivation` | `PluginSkillActivationEvent` | When a skill record is added (the same record as `GetSkillActivations`) |
| `TakenHit` | `PluginTakenHitEvent` | When a record of damage taken is added (the same record as `GetTakenHits`) |
| `CombatLog` | `PluginCombatLogEvent` | When a Timeline record is added (the same record as `GetCombatLog`) |
| `PlayerBuff` | `PluginBuffEvent` | When a buff on a player is received as applied, changed, or removed |
| `MonsterBuff` | `PluginBuffEvent` | When a buff on a monster is received as applied, changed, or removed |
| `MonsterSkill` | `PluginMonsterSkillEvent` | When the skill a monster is using is received (when it starts and when it ends) |
| `MonsterHpZero` | `PluginMonsterHpZeroEvent` | When a monster's HP reaches 0 or comes back from 0 |

- Start it in `Initialize` without awaiting it, as in `_ = ReadCombatEventsAsync(context);`. `ConfigureAwait(false)` and how the loop ends when the app stops the plugin are the same as in "Game messages".
- Combine the kinds with `|` and pass one or more. `None` or a value that is not defined throws `ArgumentException`.
- Events from the time `await foreach` starts are passed in the order the app processed them. Earlier ones are not passed.
- The time (`ArrivalTimeUtc`) is when the message that carried the event was received (UTC). For the end of a telegraph, it is the time the telegraph bar ended.
- `SequenceNumber` is a sequence number shared with game messages (`Packets`). An event caused by a message has a later number than that message.
- The record kinds (`SkillActivation`, `TakenHit`, and `CombatLog`) have the `EncounterKey` of the battle they were recorded in and the record's index (`RecordIndex`). If the `EncounterKey` of `GetLiveEncounter()` is the same, pass `RecordIndex + 1` as `startIndex` to read the rest.
- `Damage` comes only for what is recorded under "Recording rules" above. For the other kinds, `IsRecorded` tells whether the app's current battle was recording under the recording rules at that time.
- `RowKey` of `PluginDamageEvent` is the same as a row of `GetSkillTotals` (`PluginSkillTotals.RowKey`). Damage with `IsImmune` is not added to the meters.
- `Kind` of `PluginBuffEvent` is applied (`Added`), a change in stacks or duration (`Changed`), removed (`Removed`), a buff that was on at that time (`Present`), or the end of that list (`AppearListEnd`).
  - `Present` comes in the list of buffs of an entity that comes around you, and when the game applies all of your buffs again after you move to another map. One `AppearListEnd` follows. A buff that was on before and is not in the list was removed while it could not be seen. When the game does not send a list for an entity that comes around, neither comes.
  - The buff ID, caster, and source (`SourceType`, `SourceConfigId`) of `Changed` and `Removed` are the values received when the buff was applied. For a buff whose application was not received (for example, one that was on before you entered the map), the ID is 0.
  - Values the received message did not carry (`Level`, `Layer`, `DurationMilliseconds`, and `ServerCreateTimeUtc`) are `null`.
- `PluginMonsterSkillEvent` comes with `SkillId` 0 when the monster stops using a skill. It also comes when the same skill is received again. A skill the monster was using when it came around does not come.
- `PluginMonsterHpZeroEvent` also comes for entities whose maximum HP is 0 (such as objects without an HP bar). A monster that had 0 HP when it came around comes with `IsOnAppear` set to `true`.
- While a dungeon shows its result or vote screen, the app does not process buffs, damage, and healing, so `Damage`, `TakenHit`, `PlayerBuff`, `MonsterBuff`, and the enemy healing, damage from enemies to enemies, and buffs applied by enemies of `CombatLog` do not come.
- If the events not yet taken, together with game messages, exceed 64 MB for one plugin, the plugin is stopped (the same as "Game messages" above). Narrow down the kinds you receive, and take what arrives right away.
- The values passed are read-only copies, and the same objects are passed to all plugins.

## Rules for reading data

- Remove the events you subscribed to in the content of a window or in the body and footer of a widget in `Dispose`. If you do not, the content of a closed window keeps receiving events and stays in memory.
- Remove the events you subscribed to in `Initialize` in `Shutdown`. The app also removes them before `Shutdown`, so your handlers are not called after the plugin is stopped.
- Buffs, battle statistics, `GetPlayer`, and `GetPlayerBuild` have no change events, so read them again with a timer. The moments buffs are applied and removed can be received with `CombatEvents`.

## What is not provided

- Detailed equipment values, attributes, skill slots, cooldowns, dungeon objectives and scores, chat, and so on are not provided. If you need them, read the game messages. Equipment slots and IDs, talents, and skill levels can be read with `GetPlayerBuild`, enemy casts and skills that show a warning on the combat screen with `GetMonsters` of the battle reader, and boss skill telegraphs with `GetCombatLog`.

## Samples

| Sample | What it shows |
|---|---|
| [SampleGameData](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleGameData) | Shows all the values and events of `GameData` in a window (for your build, the number of items of each `GetPlayerBuild` property; for the shown battle, the numbers of your skill records, records of damage taken, and Timeline records, and your per-second totals). Rereading every second (getting the battle readers again each time instead of holding them; battle records are read from where the last read stopped while the battle is the same), keeping the result of `EncounterClosed`, and removing events in `Dispose` |
| [SampleHistory](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleHistory) | Shows the statistics, `GetPlayers`, `GetMonsters`, `GetPlayerBuffIntervals`, and `GetPlayerBuild` of a battle read with `LoadAsync` ([Plugin Development (History)](Plugin-Development-(History))) |
| [SamplePackets](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SamplePackets) | A plugin without a window that reads game messages and writes their sizes to the log |
| [SampleCombatEvents](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleCombatEvents) | A plugin without a window that reads damage and 0 HP from `CombatEvents` and writes the player who dealt the most damage to a defeated monster to the log |
| [SampleNotification](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleNotification) | In `MapEntered`, looks up the map name from `Session.SceneId` with `GetSceneName` |
| [SampleMultiWidget](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleMultiWidget) | Builds the list of targets for a per-target widget from `Players` |
