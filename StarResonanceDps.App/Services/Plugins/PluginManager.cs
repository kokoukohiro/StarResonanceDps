using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

public sealed class PluginManager
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly string _pluginsDirectory;
    private readonly string _pluginDataDirectory;
    private readonly Dictionary<string, PluginRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

    private bool _hasDiscoveredPlugins;

    private PluginManager()
    {
        _pluginsDirectory = Path.Combine(AppDataPaths.BaseDirectory, "Plugins");
        _pluginDataDirectory = AppDataPaths.PluginDataDirectory;
    }

    public static PluginManager Instance { get; } = new();

    public IReadOnlyList<PluginManifest> DiscoverPlugins()
    {
        if (_hasDiscoveredPlugins)
        {
            return GetDiscoveredManifests();
        }

        _hasDiscoveredPlugins = true;

        if (!Directory.Exists(_pluginsDirectory))
        {
            return [];
        }

        foreach (var pluginDirectory in Directory.EnumerateDirectories(_pluginsDirectory)
                     .OrderBy(directory => Path.GetFileName(directory) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            TryRegisterPlugin(pluginDirectory);
        }

        return GetDiscoveredManifests();
    }

    public void Open(PluginListItemViewModel pluginItem)
    {
        ArgumentNullException.ThrowIfNull(pluginItem);

        if (pluginItem.IsAddItem || string.IsNullOrWhiteSpace(pluginItem.PluginId))
        {
            return;
        }

        if (PluginWindowManager.Instance.ActivateExisting(pluginItem.PluginId))
        {
            return;
        }

        if (!TryGetOrLoadPlugin(pluginItem.PluginId, out var loadedPlugin))
        {
            return;
        }

        try
        {
            var content = loadedPlugin.Instance.CreateContent();
            if (content is null)
            {
                throw new InvalidOperationException("CreateContent returned null.");
            }

            PluginWindowManager.Instance.Open(pluginItem, content);
        }
        catch (Exception exception)
        {
            WritePluginError(pluginItem.PluginId, "Failed to create plugin window content.", exception);
        }
    }

    public void ShutdownAll()
    {
        foreach (var loadedPlugin in _loadedPlugins.Values.ToArray())
        {
            try
            {
                loadedPlugin.Instance.Shutdown();
            }
            catch (Exception exception)
            {
                WritePluginError(loadedPlugin.Registration.Manifest.Id, "Plugin shutdown failed.", exception);
            }
        }

        _loadedPlugins.Clear();
    }

    private IReadOnlyList<PluginManifest> GetDiscoveredManifests()
    {
        return _registrations.Values
            .OrderBy(registration => registration.Manifest.Id, StringComparer.OrdinalIgnoreCase)
            .Select(registration => registration.Manifest)
            .ToArray();
    }

    private void TryRegisterPlugin(string pluginDirectory)
    {
        var manifestPath = Path.Combine(pluginDirectory, "plugin.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), ManifestJsonOptions)
                ?? throw new InvalidOperationException("The manifest is empty.");

            NormalizeAndValidateManifest(manifest, pluginDirectory);

            if (_registrations.ContainsKey(manifest.Id))
            {
                throw new InvalidOperationException($"A plugin with id '{manifest.Id}' is already registered.");
            }

            var assemblyPath = GetAssemblyPathWithinPluginDirectory(pluginDirectory, manifest.EntryAssembly);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("The manifest entry assembly was not found.", assemblyPath);
            }

            _registrations.Add(manifest.Id, new PluginRegistration(manifest, pluginDirectory, assemblyPath));
        }
        catch (Exception exception)
        {
            WritePluginError(Path.GetFileName(pluginDirectory), "Plugin manifest registration failed.", exception);
        }
    }

    private bool TryGetOrLoadPlugin(string pluginId, out LoadedPlugin loadedPlugin)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var existingPlugin))
        {
            loadedPlugin = existingPlugin;
            return true;
        }

        DiscoverPlugins();

        if (!_registrations.TryGetValue(pluginId, out var registration))
        {
            WritePluginError(pluginId, "The requested plugin is not registered.", null);
            loadedPlugin = null!;
            return false;
        }

        try
        {
            var loadContext = new PluginLoadContext(registration.AssemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(registration.AssemblyPath);
            var entryType = assembly.GetType(registration.Manifest.EntryType, throwOnError: true, ignoreCase: false)
                ?? throw new InvalidOperationException("The manifest entry type could not be resolved.");

            if (!typeof(IStarResonancePlugin).IsAssignableFrom(entryType))
            {
                throw new InvalidOperationException($"'{registration.Manifest.EntryType}' does not implement IStarResonancePlugin.");
            }

            if (Activator.CreateInstance(entryType) is not IStarResonancePlugin plugin)
            {
                throw new InvalidOperationException("The plugin entry type could not be instantiated.");
            }

            var descriptor = plugin.Descriptor
                ?? throw new InvalidOperationException("The plugin descriptor is missing.");

            if (!string.Equals(descriptor.Id, registration.Manifest.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The plugin descriptor id does not match plugin.json.");
            }

            if (descriptor.ApiVersion != PluginSdkVersion.Current
                || registration.Manifest.ApiVersion != PluginSdkVersion.Current)
            {
                throw new InvalidOperationException($"Unsupported plugin API version. Host supports {PluginSdkVersion.Current}.");
            }

            var logger = new PluginDebugLogger(registration.Manifest.Id);
            var pluginDataDirectory = Path.Combine(_pluginDataDirectory, registration.Manifest.Id);
            var settingsStore = new PluginSettingsStore(pluginDataDirectory, logger);
            var context = new PluginHostContext(registration.Manifest.Id, pluginDataDirectory, settingsStore, logger);

            plugin.Initialize(context);

            loadedPlugin = new LoadedPlugin(registration, loadContext, plugin);
            _loadedPlugins.Add(registration.Manifest.Id, loadedPlugin);
            return true;
        }
        catch (Exception exception)
        {
            WritePluginError(pluginId, "Plugin loading or initialization failed.", exception);
            loadedPlugin = null!;
            return false;
        }
    }

    private static void NormalizeAndValidateManifest(PluginManifest manifest, string pluginDirectory)
    {
        manifest.Id = manifest.Id?.Trim() ?? string.Empty;
        manifest.EntryAssembly = manifest.EntryAssembly?.Trim() ?? string.Empty;
        manifest.EntryType = manifest.EntryType?.Trim() ?? string.Empty;

        if (!IsValidPluginId(manifest.Id))
        {
            throw new InvalidOperationException("Plugin id is missing or contains unsupported characters.");
        }

        if (manifest.ApiVersion != PluginSdkVersion.Current)
        {
            throw new InvalidOperationException($"Unsupported plugin API version '{manifest.ApiVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly)
            || Path.IsPathRooted(manifest.EntryAssembly))
        {
            throw new InvalidOperationException("EntryAssembly must be a relative file path.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryType))
        {
            throw new InvalidOperationException("EntryType is required.");
        }

        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (manifest.DisplayNames is not null)
        {
            foreach (var (cultureName, displayName) in manifest.DisplayNames)
            {
                if (!string.IsNullOrWhiteSpace(cultureName)
                    && !string.IsNullOrWhiteSpace(displayName))
                {
                    displayNames[cultureName.Trim()] = displayName.Trim();
                }
            }
        }

        if (displayNames.Count == 0)
        {
            throw new InvalidOperationException("At least one localized display name is required.");
        }

        manifest.DisplayNames = displayNames;

        _ = GetAssemblyPathWithinPluginDirectory(pluginDirectory, manifest.EntryAssembly);
    }

    private static string GetAssemblyPathWithinPluginDirectory(string pluginDirectory, string relativeAssemblyPath)
    {
        var normalizedDirectory = Path.GetFullPath(pluginDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directoryPrefix = normalizedDirectory + Path.DirectorySeparatorChar;
        var assemblyPath = Path.GetFullPath(Path.Combine(normalizedDirectory, relativeAssemblyPath));

        if (!assemblyPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The entry assembly must stay inside its plugin directory.");
        }

        return assemblyPath;
    }

    private static bool IsValidPluginId(string pluginId)
    {
        return !string.IsNullOrWhiteSpace(pluginId)
            && pluginId.Length <= 128
            && pluginId.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '.' or '-' or '_');
    }

    private static void WritePluginError(string pluginId, string message, Exception? exception)
    {
        var suffix = exception is null ? string.Empty : $"{Environment.NewLine}{exception}";
        Debug.WriteLine($"[ERROR] [Plugin:{pluginId}] {message}{suffix}");
    }

    private sealed record PluginRegistration(PluginManifest Manifest, string DirectoryPath, string AssemblyPath);

    private sealed record LoadedPlugin(PluginRegistration Registration, PluginLoadContext LoadContext, IStarResonancePlugin Instance);
}
