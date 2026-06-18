using System.IO;
using System.Text.Json;
using StarResonanceDps.Core.Models;

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
    private readonly object _syncRoot = new();
    private WidgetStateDocument _document;

    private WidgetStateManager()
    {
        _statePath = Path.Combine(AppContext.BaseDirectory, "widget-state.json");
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
            WidgetConfigDefaults.Normalize(config);
            SaveCore();
        }
    }

    public void SaveWidgetTheme(WidgetKind kind, WidgetThemeConfig theme)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.Theme = WidgetConfigDefaults.CloneNormalizedTheme(theme);
            WidgetConfigDefaults.Normalize(config);
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
        if (!File.Exists(_statePath))
        {
            return CreateDefaultDocument();
        }

        try
        {
            var json = File.ReadAllText(_statePath);
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
        document.SchemaVersion = document.SchemaVersion <= 0 ? 1 : document.SchemaVersion;
        document.Widgets ??= new Dictionary<string, WidgetConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (WidgetKind kind in Enum.GetValues<WidgetKind>())
        {
            var key = WidgetConfigDefaults.GetKey(kind);
            if (!document.Widgets.TryGetValue(key, out var config))
            {
                document.Widgets[key] = WidgetConfigDefaults.Create(kind);
                continue;
            }

            WidgetConfigDefaults.Normalize(config);
        }
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
