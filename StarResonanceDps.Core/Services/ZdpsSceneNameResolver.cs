using System.Text.Json;

namespace StarResonanceDps.Core.Services;

internal static class ZdpsSceneNameResolver
{
    private const string ResourceName = "StarResonanceDps.Core.Data.zdps_scene_names.json";

    private static readonly Lazy<SceneNameCatalog> Catalog = new(LoadCatalog);

    public static string Resolve(uint levelMapId)
    {
        if (levelMapId == 0)
        {
            return string.Empty;
        }

        var catalog = Catalog.Value;
        return catalog.DungeonOverrides.TryGetValue(levelMapId, out var dungeonName)
            ? dungeonName
            : catalog.SceneNames.TryGetValue(levelMapId, out var sceneName)
                ? sceneName
                : string.Empty;
    }

    private static SceneNameCatalog LoadCatalog()
    {
        using var stream = typeof(ZdpsSceneNameResolver).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return SceneNameCatalog.Empty;
        }

        using var document = JsonDocument.Parse(stream);
        return new SceneNameCatalog(
            ReadNameMap(document.RootElement, "scenes"),
            ReadNameMap(document.RootElement, "dungeonOverrides"));
    }

    private static IReadOnlyDictionary<uint, string> ReadNameMap(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var mapElement)
            || mapElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<uint, string>();
        }

        var names = new Dictionary<uint, string>();
        foreach (var property in mapElement.EnumerateObject())
        {
            if (uint.TryParse(property.Name, out var mapId)
                && property.Value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                names[mapId] = property.Value.GetString()!;
            }
        }

        return names;
    }

    private sealed record SceneNameCatalog(
        IReadOnlyDictionary<uint, string> SceneNames,
        IReadOnlyDictionary<uint, string> DungeonOverrides)
    {
        public static SceneNameCatalog Empty { get; } = new(
            new Dictionary<uint, string>(),
            new Dictionary<uint, string>());
    }
}
