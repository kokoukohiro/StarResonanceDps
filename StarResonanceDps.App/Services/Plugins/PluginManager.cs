using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

public sealed class PluginManager
{
    private readonly string _pluginsDirectory;
    private readonly Dictionary<string, PluginRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

    private bool _hasDiscoveredPlugins;

    private PluginManager()
    {
        _pluginsDirectory = AppDataPaths.PluginsDirectory;
    }

    public static PluginManager Instance { get; } = new();

    public IReadOnlyList<PluginInfo> DiscoverPlugins()
    {
        if (_hasDiscoveredPlugins)
        {
            return GetDiscoveredPlugins();
        }

        _hasDiscoveredPlugins = true;

        if (!Directory.Exists(_pluginsDirectory))
        {
            return [];
        }

        foreach (var assemblyPath in Directory.EnumerateFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            TryRegisterPlugin(assemblyPath);
        }

        return GetDiscoveredPlugins();
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
                WritePluginError(loadedPlugin.Registration.Info.Id, "Plugin shutdown failed.", exception);
            }
        }

        _loadedPlugins.Clear();
    }

    private IReadOnlyList<PluginInfo> GetDiscoveredPlugins()
    {
        return _registrations.Values
            .OrderBy(registration => registration.Info.Id, StringComparer.OrdinalIgnoreCase)
            .Select(registration => registration.Info)
            .ToArray();
    }

    private void TryRegisterPlugin(string assemblyPath)
    {
        try
        {
            var fullAssemblyPath = Path.GetFullPath(assemblyPath);
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullAssemblyPath);
            var registration = CreateRegistration(assembly);

            if (!_registrations.TryAdd(registration.Info.Id, registration))
            {
                throw new InvalidOperationException($"A plugin with id '{registration.Info.Id}' is already registered.");
            }
        }
        catch (Exception exception)
        {
            var pluginFileName = Path.GetFileNameWithoutExtension(assemblyPath) ?? "unknown";
            WritePluginError(pluginFileName, "Plugin DLL registration failed.", exception);
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
            if (Activator.CreateInstance(registration.EntryType) is not IStarResonancePlugin plugin)
            {
                throw new InvalidOperationException("The plugin entry type could not be instantiated.");
            }

            var logger = new PluginDebugLogger(registration.Info.Id);
            var settingsStore = new PluginSettingsStore(_pluginsDirectory, logger);
            var messages = new PluginMessageService();
            var context = new PluginHostContext(registration.Info.Id, settingsStore, messages, PluginLocalizationService.Instance, logger);

            plugin.Initialize(context);

            loadedPlugin = new LoadedPlugin(registration, plugin);
            _loadedPlugins.Add(registration.Info.Id, loadedPlugin);
            return true;
        }
        catch (Exception exception)
        {
            WritePluginError(pluginId, "Plugin loading or initialization failed.", exception);
            loadedPlugin = null!;
            return false;
        }
    }

    private static PluginRegistration CreateRegistration(Assembly assembly)
    {
        var registrationAttribute = assembly.GetCustomAttribute<PluginRegistrationAttribute>()
            ?? throw new InvalidOperationException("The plugin registration attribute is missing.");

        var pluginId = registrationAttribute.Id?.Trim() ?? string.Empty;
        if (!IsValidPluginId(pluginId))
        {
            throw new InvalidOperationException("Plugin id is missing or contains unsupported characters.");
        }

        if (registrationAttribute.ApiVersion != PluginSdkVersion.Current)
        {
            throw new InvalidOperationException($"Unsupported plugin API version '{registrationAttribute.ApiVersion}'.");
        }

        var entryType = registrationAttribute.EntryType
            ?? throw new InvalidOperationException("The plugin entry type is missing.");

        if (!ReferenceEquals(entryType.Assembly, assembly))
        {
            throw new InvalidOperationException("The plugin entry type must be declared in the plugin DLL itself.");
        }

        if (!entryType.IsClass
            || entryType.IsAbstract
            || !entryType.IsPublic
            || entryType.ContainsGenericParameters)
        {
            throw new InvalidOperationException("The plugin entry type must be a public, non-abstract, non-generic class.");
        }

        if (!typeof(IStarResonancePlugin).IsAssignableFrom(entryType))
        {
            throw new InvalidOperationException($"'{entryType.FullName}' does not implement IStarResonancePlugin.");
        }

        if (entryType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new InvalidOperationException("The plugin entry type must have a public parameterless constructor.");
        }

        var displayNames = ReadDisplayNames(assembly);
        var info = new PluginInfo(pluginId, displayNames);
        return new PluginRegistration(info, entryType);
    }

    private static IReadOnlyDictionary<string, string> ReadDisplayNames(Assembly assembly)
    {
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var displayNameAttribute in assembly.GetCustomAttributes<PluginDisplayNameAttribute>())
        {
            var cultureName = displayNameAttribute.CultureName?.Trim() ?? string.Empty;
            var displayName = displayNameAttribute.DisplayName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(cultureName) || string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            if (!displayNames.TryAdd(cultureName, displayName))
            {
                throw new InvalidOperationException($"The plugin contains duplicate display names for culture '{cultureName}'.");
            }
        }

        if (displayNames.Count == 0)
        {
            throw new InvalidOperationException("At least one localized plugin display name is required.");
        }

        return new ReadOnlyDictionary<string, string>(displayNames);
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

    private sealed record PluginRegistration(PluginInfo Info, Type EntryType);

    private sealed record LoadedPlugin(PluginRegistration Registration, IStarResonancePlugin Instance);
}
