using System.IO;
using System.Text.Json;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.Config;

public sealed class WidgetStateManager
{
    private static readonly Lazy<WidgetStateManager> LazyInstance = new(() => new WidgetStateManager());
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _statePath;
    private readonly string _legacyStatePath;
    private readonly object _syncRoot = new();
    private WidgetStateDocument _document;

    private WidgetStateManager()
    {
        _statePath = AppDataPaths.WidgetStatePath;
        _legacyStatePath = AppDataPaths.GetLegacyWidgetStatePath();
        _document = Load();
    }

    public static WidgetStateManager Instance => LazyInstance.Value;

    public WidgetConfig GetWidgetSnapshot(WidgetKind kind)
    {
        lock (_syncRoot)
        {
            var key = WidgetConfigDefaults.GetKey(kind);
            if (!_document.Widgets.TryGetValue(key, out var config))
            {
                config = WidgetConfigDefaults.Create(kind);
                _document.Widgets[key] = config;
            }

            return WidgetConfigDefaults.CloneNormalized(kind, config);
        }
    }

    public void SaveWidget(WidgetKind kind, WidgetConfig config)
    {
        lock (_syncRoot)
        {
            var key = WidgetConfigDefaults.GetKey(kind);
            _document.Widgets[key] = WidgetConfigDefaults.CloneNormalized(kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetFlags(WidgetKind kind, bool isFavorite, bool isPinned)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.IsFavorite = isFavorite;
            config.IsPinned = isPinned;
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetState(WidgetKind kind, WidgetState state)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.State = state;
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetTheme(WidgetKind kind, WidgetThemeConfig theme)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.Theme = WidgetConfigDefaults.CloneNormalizedTheme(theme);
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetWindowBounds(WidgetKind kind, double x, double y, double width, double height)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.Window = new WidgetWindowConfig
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            };
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    private WidgetConfig GetOrCreateWidgetConfig(WidgetKind kind)
    {
        var key = WidgetConfigDefaults.GetKey(kind);
        if (_document.Widgets.TryGetValue(key, out var config))
        {
            return config;
        }

        config = WidgetConfigDefaults.Create(kind);
        _document.Widgets[key] = config;
        return config;
    }

    private WidgetStateDocument Load()
    {
        var loadPath = GetReadableStatePath();
        if (loadPath is null)
        {
            return CreateDefaultDocument();
        }

        try
        {
            var json = File.ReadAllText(loadPath);
            var document = JsonSerializer.Deserialize<WidgetStateDocument>(json, JsonOptions) ?? CreateDefaultDocument();
            Normalize(document);
            return document;
        }
        catch
        {
            return CreateDefaultDocument();
        }
    }

    private static WidgetStateDocument CreateDefaultDocument()
    {
        var document = new WidgetStateDocument();
        Normalize(document);
        return document;
    }

    private static void Normalize(WidgetStateDocument document)
    {
        var sourceSchemaVersion = document.SchemaVersion <= 0 ? 1 : document.SchemaVersion;
        document.Widgets ??= new Dictionary<string, WidgetConfig>(StringComparer.OrdinalIgnoreCase);
        MigratePlayerStatusWidgetConfig(document.Widgets);

        foreach (WidgetKind kind in Enum.GetValues<WidgetKind>())
        {
            var key = WidgetConfigDefaults.GetKey(kind);
            if (!document.Widgets.TryGetValue(key, out var config))
            {
                document.Widgets[key] = WidgetConfigDefaults.Create(kind);
                continue;
            }

            if (sourceSchemaVersion < 3)
            {
                WidgetConfigDefaults.MigrateVersion1Defaults(config);
            }

            if (sourceSchemaVersion < WidgetConfigDefaults.CurrentSchemaVersion)
            {
                WidgetConfigDefaults.RemoveObsoleteSettings(config);
            }

            WidgetConfigDefaults.Normalize(kind, config);
        }

        document.SchemaVersion = WidgetConfigDefaults.CurrentSchemaVersion;
    }

    private static void MigratePlayerStatusWidgetConfig(Dictionary<string, WidgetConfig> widgets)
    {
        const string legacyKey = "PlayerDetail";
        var statusKey = WidgetConfigDefaults.GetKey(WidgetKind.PlayerStatus);

        if (widgets.TryGetValue(legacyKey, out var legacyConfig)
            && !widgets.ContainsKey(statusKey))
        {
            widgets[statusKey] = legacyConfig;
        }

        widgets.Remove(legacyKey);
    }

    private string? GetReadableStatePath()
    {
        if (File.Exists(_statePath))
        {
            return _statePath;
        }

        return File.Exists(_legacyStatePath)
            ? _legacyStatePath
            : null;
    }

    private void SaveCore()
    {
        Normalize(_document);

        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_document, JsonOptions);
        File.WriteAllText(_statePath, json);
    }
}
