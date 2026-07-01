using System.Text.Json;

namespace StarResonanceDps.Core.Services;

internal static class GameSceneNameResolver
{
    private const string SceneResourceName = "StarResonanceDps.Core.Data.SceneTable.json";
    private const string DungeonResourceName = "StarResonanceDps.Core.Data.DungeonsTable.json";

    private static readonly Lazy<SceneNameCatalog> Catalog = new(LoadCatalog);

    public static string Resolve(uint levelMapId)
    {
        if (levelMapId == 0)
        {
            return string.Empty;
        }

        var catalog = Catalog.Value;
        if (catalog.Dungeons.TryGetValue(levelMapId, out var dungeon)
            && dungeon.PlayType == 17)
        {
            return dungeon.Name;
        }

        return catalog.Scenes.TryGetValue(levelMapId, out var scene)
            ? scene
            : string.Empty;
    }

    private static SceneNameCatalog LoadCatalog()
    {
        return new SceneNameCatalog(
            ReadScenes(SceneResourceName),
            ReadDungeons(DungeonResourceName));
    }

    private static IReadOnlyDictionary<uint, string> ReadScenes(string resourceName)
    {
        using var stream = typeof(GameSceneNameResolver).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new Dictionary<uint, string>();
        }

        using var document = JsonDocument.Parse(stream);
        var result = new Dictionary<uint, string>();
        foreach (var item in document.RootElement.EnumerateObject())
        {
            if (!uint.TryParse(item.Name, out var id)
                || item.Value.ValueKind != JsonValueKind.Object
                || !item.Value.TryGetProperty("Name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameElement.GetString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                result[id] = name;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<uint, DungeonNameEntry> ReadDungeons(string resourceName)
    {
        using var stream = typeof(GameSceneNameResolver).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new Dictionary<uint, DungeonNameEntry>();
        }

        using var document = JsonDocument.Parse(stream);
        var result = new Dictionary<uint, DungeonNameEntry>();
        foreach (var item in document.RootElement.EnumerateObject())
        {
            if (!uint.TryParse(item.Name, out var id)
                || item.Value.ValueKind != JsonValueKind.Object
                || !item.Value.TryGetProperty("Name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String
                || !item.Value.TryGetProperty("PlayType", out var playTypeElement)
                || playTypeElement.ValueKind != JsonValueKind.Number
                || !playTypeElement.TryGetInt32(out var playType))
            {
                continue;
            }

            var name = nameElement.GetString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                result[id] = new DungeonNameEntry(name, playType);
            }
        }

        return result;
    }

    private sealed record SceneNameCatalog(
        IReadOnlyDictionary<uint, string> Scenes,
        IReadOnlyDictionary<uint, DungeonNameEntry> Dungeons);

    private sealed record DungeonNameEntry(string Name, int PlayType);
}
