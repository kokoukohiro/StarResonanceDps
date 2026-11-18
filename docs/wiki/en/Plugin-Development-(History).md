This page explains how to read the history (saved battles) in "[Aggregation](Aggregation)" with a plugin and how to save a plugin's values together with a battle. What every plugin needs is described in [Plugin Development](Plugin-Development), and how to read the values of a battle you have read is described in "Battle reader" in [Plugin Development (Data)](Plugin-Development-(Data)).

## History (`History`)

Use `IPluginContext.History` (`IPluginHistoryService`). The following example writes the save time into a battle when the battle is saved to the history, then reads the battle again and writes everyone's total damage and the written value to the log.

```csharp
using System.Text;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(MyPlugin.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "私のプラグイン")]
[assembly: PluginAuthor("YourName")]
[assembly: PluginVersion("1.0.0")]

namespace MyPlugin;

public sealed class Plugin : IStarResonancePlugin
{
    private const string NoteKey = "my.note";

    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.GameData.EncounterSaved += OnEncounterSaved;
    }

    private async void OnEncounterSaved(object? sender, PluginEncounterSaved saved)
    {
        IPluginContext? context = _context;
        if (context is null)
        {
            return;
        }

        byte[] note = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O"));
        PluginHistoryStatus status = await context.History.SetSavedEncounterDataAsync(saved.HistoryId, NoteKey, note);
        if (status != PluginHistoryStatus.Succeeded)
        {
            context.Logger.Warning($"{saved.HistoryId}: {status}");
            return;
        }

        PluginHistoryLoadResult loaded = await context.History.LoadAsync(saved.HistoryId);
        IPluginEncounterRecord? encounter = loaded.Encounter;
        if (encounter is null)
        {
            context.Logger.Warning($"{saved.HistoryId}: {loaded.Status}");
            return;
        }

        foreach (PluginEncounterPlayer player in encounter.GetTotals().Players)
        {
            context.Logger.Info($"{player.CharacterId}: {player.Damage.Total}");
        }

        if (encounter.GetPluginData(NoteKey) is ReadOnlyMemory<byte> stored)
        {
            context.Logger.Info(Encoding.UTF8.GetString(stored.Span));
        }
    }

    public void Shutdown()
    {
        if (_context is not null)
        {
            _context.GameData.EncounterSaved -= OnEncounterSaved;
        }
    }
}
```

| Member | Contents |
|---|---|
| `GetEntriesAsync()` | The list of saved battles ("List" below) |
| `LoadAsync(historyId)` | Reads a saved battle without opening it ("Reading without opening" below) |
| `OpenAsync(historyId)` | Opens a saved battle in the app ("Opening in the app" below) |
| `ReturnToLiveAsync()` | Returns the app to the current battle ("Opening in the app" below) |
| `KeepHistoryOpen()` | Keeps an opened history entry from returning to the current battle automatically ("Keeping a history entry open" below) |
| `HasOpenFailed`, `OpenFailedChanged` | Whether the battle the app last tried to open could not be read, and an event when this changes ("When a battle cannot be opened" below) |
| `TrySetEncounterData(encounterKey, key, data)` | Puts a plugin value into a battle that has not been saved yet ("Saving with a battle" below) |
| `SetSavedEncounterDataAsync(historyId, key, data)` | Rewrites a plugin value of a saved battle ("Saving with a battle" below) |

- They can be called from any thread. `OpenFailedChanged` arrives on the UI thread.
- `GetEntriesAsync`, `LoadAsync`, and `SetSavedEncounterDataAsync` read and write the history on a background thread. For each plugin, they run one at a time in order, and each call waits until the previous one finishes.
- The `cancellationToken` of these three can be omitted. It is checked before reading or writing (and also after reading for `GetEntriesAsync` and `LoadAsync`), and if it has been canceled, the task ends with `OperationCanceledException`. Reading and writing do not stop partway.
- A saved battle is specified by its history ID (`HistoryId`), and a battle before it is saved by its `EncounterKey` ("Battle statistics" in [Plugin Development (Data)](Plugin-Development-(Data))).
- After the plugin is stopped (including inside `Shutdown`), every method does nothing. The methods that return a task end with `Unavailable` (`ReturnToLiveAsync` simply completes), `TrySetEncounterData` returns `false`, and `KeepHistoryOpen` returns an `IDisposable` that keeps nothing. A history entry requested with `OpenAsync` before the plugin stopped and not yet opened is not opened.
- After the app starts closing, `GetEntriesAsync`, `LoadAsync`, `OpenAsync`, `ReturnToLiveAsync`, and `SetSavedEncounterDataAsync` also do nothing in the same way.

| `PluginHistoryStatus` | How it ends |
|---|---|
| `Succeeded` | Done |
| `NotFound` | There is no saved battle with that ID (for example, it was deleted because of "Max History Entries" in Settings) |
| `Failed` | The history could not be read or written. The reason is written to the app's log |
| `Unavailable` | Nothing was done because the plugin has been stopped or the app has started closing |

### List (`GetEntriesAsync`)

```csharp
private static async Task LogHistoryAsync(IPluginContext context)
{
    PluginHistoryListResult list = await context.History.GetEntriesAsync().ConfigureAwait(false);
    foreach (PluginHistoryEntry entry in list.Entries)
    {
        string? sceneName = entry.Info.SceneId is int sceneId
            ? context.Names.GetSceneName(sceneId, entry.Info.DungeonDifficulty)
            : null;
        context.Logger.Info($"{entry.HistoryId}: {entry.Info.CreatedUtc:O} {sceneName} {entry.Elapsed}");
    }
}
```

- Returns the saved battles, newest first, like the history in "Aggregation". It does not open them, so the app's display does not change.
- The result (`PluginHistoryListResult`) has items in `Entries` only when `Status` is `Succeeded`.
- When the app saves a battle and when it closes, it deletes the old battles beyond "Max History Entries" in Settings.

| `PluginHistoryEntry` property | Contents |
|---|---|
| `HistoryId` | The history ID. Pass it to `LoadAsync`, `OpenAsync`, and `SetSavedEncounterDataAsync` |
| `Info` | Values such as the map, the server, and the benchmark (`PluginEncounterInfo`; "Battle values" in [Plugin Development (Data)](Plugin-Development-(Data))). `BenchmarkFirstTargetOnly` is `null` |
| `StartUtc` | The time the battle clock started (UTC). `null` for a battle whose clock never started |
| `Elapsed` | The elapsed time of the battle (excluding Auto Pause) |

### Reading without opening (`LoadAsync`)

- Reads one saved battle. It does not open the battle, so the widgets, "Aggregation", and `GetEncounter` do not change.
- `Encounter` of the result (`PluginHistoryLoadResult`) is the battle reader (`IPluginEncounterRecord`) for the battle that was read. It is `null` if `Status` is not `Succeeded`.
- How to use a battle reader is described in "Battle reader" in [Plugin Development (Data)](Plugin-Development-(Data)). `IsHistory` is `true`, and the players who were in that battle can be read with `GetPlayers`.
- A battle that has been read does not change later. Values written with `SetSavedEncounterDataAsync` after reading are not included either, so read the battle again when you need them.
- `EncounterKey` is a new number each time the battle is read. Compare `HistoryId` to tell whether two are the same battle.
- While you hold the battle reader, the battle stays in memory. Hold it only while you need it.

### Opening in the app (`OpenAsync`, `ReturnToLiveAsync`)

- `OpenAsync` opens a saved battle, the same as clicking a history row in "Aggregation". Widgets such as the DPS Meter and `GetEncounter` then show that battle ("[Aggregation](Aggregation)").
- The battle is read on a background thread, and the task ends with `Opened` when the display has been scheduled. When the battle is shown, `GameData.ShownEncounterChanged` arrives.
- If another battle or the current battle is chosen while the battle is being read, the task ends with `Superseded`. If one is chosen after `Opened` but before the battle is shown, the battle is not shown either. Check whether it is shown with `GameData.GetShownEncounter()`.
- If "Auto-exit History View" in Settings is on, the app returns to the current battle when something happens in the current battle and when the aggregation is split. To keep it from returning, use `KeepHistoryOpen`.
- `ReturnToLiveAsync` returns to the current battle, the same as clicking the open history row in "Aggregation" again. The task ends after the app has returned. If the current battle is already shown, nothing changes.
- If an exception occurs in the app while returning, the task ends with that exception (it is also written to the app's log).

| `PluginHistoryOpenResult` | How it ends |
|---|---|
| `Opened` | The battle was read and its display was scheduled |
| `NotFound` | There is no saved battle with that ID. The display does not change |
| `Superseded` | Another battle or the current battle was chosen while the battle was being read. It is not shown |
| `Failed` | The battle could not be read (the reason is in the app's log). The display does not change |
| `Unavailable` | Nothing was done because the plugin has been stopped or the app has started closing |

### Keeping a history entry open (`KeepHistoryOpen`)

- Until you `Dispose` the returned `IDisposable`, the app does not return to the current battle when something happens in the current battle or when the aggregation is split, even if "Auto-exit History View" in Settings is on.
- The app still returns to the current battle on `ReturnToLiveAsync`, when the open history row in "Aggregation" is clicked again, and on logout.
- Each call returns a new `IDisposable`. If you `Dispose` the same one twice, the second time does nothing.
- When the `IDisposable` objects of all plugins have been disposed, the app returns to the current battle according to the setting, starting from the next event.
- When a plugin is stopped, the app disposes of that plugin's objects.

### When a battle cannot be opened (`HasOpenFailed`)

- `HasOpenFailed` is `true` when the app could not read the battle it last tried to open, including a battle chosen in "Aggregation" (an exception, or there is no battle with that ID). The display stays as it was.
- It becomes `false` when a battle is opened and when the app returns to the current battle. `LoadAsync` does not change it.
- When it changes, `OpenFailedChanged` arrives. Changes within a short time are combined into one.

## Saving with a battle

You can save a plugin's values as bytes together with a battle and read them again later from the history.

| Method | Battle it writes to | Result |
|---|---|---|
| `TrySetEncounterData(encounterKey, key, data)` | A battle that has not been saved yet (the current battle, or a closed battle before it is saved). `encounterKey` is the battle's `EncounterKey` | `true` if the value was put in |
| `SetSavedEncounterDataAsync(historyId, key, data)` | A saved battle. `historyId` is the history ID | `PluginHistoryStatus` |

- Values are held per key. A key is 1 to 128 characters of letters, digits, ".", "-", and "_", and is not case-sensitive.
- A plugin can read and write only its own values. Read them with `GetPluginData(key)` of the battle reader, which returns `null` if there is no value.
- Each plugin can save up to 32 keys (`PluginEncounterDataLimits.MaxKeys`) and 4 MiB in total (`PluginEncounterDataLimits.MaxTotalBytes`) in one battle.
- Writing an empty value removes that key. The app copies the value, so changing it after passing it does not change the saved value.
- If the key does not follow the rules or the value exceeds 4 MiB, `ArgumentException` occurs when you call the method. `GetPluginData` also throws `ArgumentException` if the key does not follow the rules.
- If the number of keys or the total would exceed the limit, nothing is written and `InvalidOperationException` occurs (for `SetSavedEncounterDataAsync`, the task ends with this exception).
- The values are deleted together with the battle ("Max History Entries" in Settings). They remain when the plugin is deleted, and they can be read again if a plugin with the same assembly name is installed. If you change the assembly name, the previous values cannot be read.

### Battles not yet saved (`TrySetEncounterData`)

- If you put a value into the current battle (the `EncounterKey` of `GameData.GetLiveEncounter()`) or another battle that has not been saved yet, the app saves it together with the battle when it saves the battle.
- A value cannot be put into a battle after it is saved, and `false` is returned. After `GameData.EncounterSaved`, write with `SetSavedEncounterDataAsync`.
- A value put in inside a `GameData.EncounterClosed` handler is not necessarily saved with the battle. Write the final values of a closed battle with `SetSavedEncounterDataAsync` after you receive `EncounterSaved`.
- A battle without records is not saved, and the values put into it are discarded with it.
- When the app closes, it saves the last battle before stopping the plugins, so `TrySetEncounterData` returns `false` inside `Shutdown`.

### Saved battles (`SetSavedEncounterDataAsync`)

- Replaces the value of that key in the saved battle.
- The task ends with `NotFound` if there is no battle with that ID, and with `Failed` if the value cannot be written (the reason is in the app's log).
- The new value is included in the battle open in "Aggregation" and in the previous battle while it is being shown. It is not included in the battle readers of battles read earlier with `LoadAsync` or of battles the app no longer shows.
- If the same battle is opened while the value is being written, the opened battle may not include the new value. The history has it, so you can read it again with `LoadAsync`.

## Samples

| Sample | What it shows |
|---|---|
| [SampleHistory](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleHistory) | Shows in a window the list of history entries, and the statistics, players, monsters, buff intervals, and plugin values of a battle read with `LoadAsync`. Opening a battle in the app and returning to the current battle, `KeepHistoryOpen` while the window is open, `TrySetEncounterData` on the current battle, and `SetSavedEncounterDataAsync` after `EncounterSaved` |
