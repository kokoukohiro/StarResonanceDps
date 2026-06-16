using System.IO;
using System.Text.Json;

namespace StarResonanceDps.App.Config;

public sealed class ConfigManager
{
    private static readonly Lazy<ConfigManager> LazyInstance = new(() => new ConfigManager());
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configPath;

    private ConfigManager()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        AppConfig = LoadAppConfig();
    }

    public static ConfigManager Instance => LazyInstance.Value;

    public AppConfig AppConfig { get; }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var root = new AppSettingsRoot
        {
            Config = AppConfig
        };

        var json = JsonSerializer.Serialize(root, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    private AppConfig LoadAppConfig()
    {
        if (!File.Exists(_configPath))
        {
            return new AppConfig();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var root = JsonSerializer.Deserialize<AppSettingsRoot>(json, JsonOptions);
            return root?.Config ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    private sealed class AppSettingsRoot
    {
        public AppConfig? Config { get; set; }
    }
}
