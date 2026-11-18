using System.Globalization;
using System.Text;
using System.Windows;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleHistory.SampleHistoryPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: History")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 履歴")]
[assembly: PluginGenre("en-US", "Sample")]
[assembly: PluginGenre("ja-JP", "サンプル")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleHistory;

/// <summary>
/// A plugin that reads the app's saved history (<see cref="IPluginHistoryService"/>) and stores its own data with encounters:
/// how many times its window was opened during an encounter, and when the app saved the encounter.
/// </summary>
public sealed class SampleHistoryPlugin : IStarResonancePlugin, IPluginWindowProvider
{
    internal const string OpenedCountKey = "sample.opened-count";
    internal const string SavedAtKey = "sample.saved-at";

    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.GameData.EncounterSaved += GameData_EncounterSaved;
    }

    public FrameworkElement CreateContent()
    {
        var context = _context ?? throw new InvalidOperationException("The plugin has not been initialized.");
        RecordWindowOpened(context);
        return new HistoryView(context);
    }

    public void Shutdown()
    {
        if (_context is { } context)
        {
            context.GameData.EncounterSaved -= GameData_EncounterSaved;
        }

        _context = null;
    }

    /// <summary>Reads a number stored with <see cref="Encoding.UTF8"/>, or 0 when there is none.</summary>
    internal static int ReadCount(ReadOnlyMemory<byte>? data)
    {
        return ReadText(data) is { } text && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;
    }

    internal static string? ReadText(ReadOnlyMemory<byte>? data)
    {
        return data is { } bytes ? Encoding.UTF8.GetString(bytes.Span) : null;
    }

    /// <summary>
    /// An encounter that has not been saved yet takes data with its encounter key.
    /// When the app has saved it meanwhile, write with <see cref="IPluginHistoryService.SetSavedEncounterDataAsync"/> instead.
    /// </summary>
    private static void RecordWindowOpened(IPluginContext context)
    {
        var live = context.GameData.GetLiveEncounter();
        if (live is null)
        {
            return;
        }

        var count = ReadCount(live.GetPluginData(OpenedCountKey)) + 1;
        var data = Encoding.UTF8.GetBytes(count.ToString(CultureInfo.InvariantCulture));
        if (!context.History.TrySetEncounterData(live.EncounterKey, OpenedCountKey, data))
        {
            context.Logger.Info("The window opening was not stored: the encounter was saved meanwhile.");
        }
    }

    /// <summary>
    /// A closed encounter's final values are written after the app has saved it, with the number it was saved under.
    /// Data stored with <see cref="IPluginHistoryService.TrySetEncounterData"/> while the encounter closes may not be saved with it.
    /// </summary>
    private async void GameData_EncounterSaved(object? sender, PluginEncounterSaved saved)
    {
        var context = _context;
        if (context is null)
        {
            return;
        }

        var data = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        var status = await context.History.SetSavedEncounterDataAsync(saved.HistoryId, SavedAtKey, data);
        context.Logger.Info($"Stored the save time with history ID {saved.HistoryId}: {status}");
    }
}
