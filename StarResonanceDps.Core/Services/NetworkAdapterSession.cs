using SharpPcap;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Logging;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class NetworkAdapterSession
{
    private static readonly Lazy<NetworkAdapterSession> LazyInstance = new(() => new NetworkAdapterSession());

    private readonly PacketDiagnosticLogStore _diagnosticLog = PacketDiagnosticLogStore.Instance;
    private IReadOnlyList<NetworkAdapterInfo> _availableAdapters = [];
    private string _activeNetCaptureDeviceName = Settings.AutomaticNetCaptureDeviceName;
    private EGameCapturePreference _activeGameCapturePreference = EGameCapturePreference.Auto;
    private string _activeGameCaptureCustomExeName = string.Empty;

    private NetworkAdapterSession()
    {
    }

    public static NetworkAdapterSession Instance => LazyInstance.Value;

    public IReadOnlyList<NetworkAdapterInfo> AvailableAdapters => _availableAdapters;

    public NetworkAdapterInfo? SelectedAdapter { get; private set; }

    public bool IsInitialized { get; private set; }

    public event EventHandler? SelectedAdapterChanged;

    public void Initialize()
    {
        _availableAdapters = GetNetworkAdapters();
        NormalizePersistedAdapterSelection();
        _activeNetCaptureDeviceName = Settings.Instance.NetCaptureDeviceName;
        _activeGameCapturePreference = Settings.Instance.GameCapturePreference;
        _activeGameCaptureCustomExeName = NormalizeGameCaptureCustomExeName(Settings.Instance.GameCaptureCustomExeName);
        SelectedAdapter = FindSelectedAdapter(_activeNetCaptureDeviceName);
        ApplyRuntimeCaptureSettings();
        IsInitialized = true;
    }

    public bool SelectAdapter(NetworkAdapterInfo? adapter)
    {
        EnsureInitialized();

        if (adapter is null)
        {
            _diagnosticLog.Warning("Adapter", "Ignoring an empty capture-adapter selection.");
            return false;
        }

        var selected = _availableAdapters.FirstOrDefault(candidate =>
            string.Equals(candidate.DeviceName, adapter.DeviceName, StringComparison.Ordinal));

        if (selected is null)
        {
            _diagnosticLog.Warning(
                "Adapter",
                $"Ignoring a capture-adapter selection that is not in the current device list: {adapter.DisplayName}.");
            return false;
        }

        ApplyCaptureSettings(selected.DeviceName, _activeGameCapturePreference, _activeGameCaptureCustomExeName);
        return true;
    }

    public void ApplyGameCaptureSettings(EGameCapturePreference preference, string customExeName)
    {
        ApplyCaptureSettings(_activeNetCaptureDeviceName, preference, customExeName);
    }

    public void PreviewCaptureSettings(
        string networkAdapterDeviceName,
        EGameCapturePreference preference,
        string customExeName)
    {
        ApplyCaptureSettingsCore(networkAdapterDeviceName, preference, customExeName, persist: false);
    }

    public void ApplyCaptureSettings(
        string networkAdapterDeviceName,
        EGameCapturePreference preference,
        string customExeName)
    {
        ApplyCaptureSettingsCore(networkAdapterDeviceName, preference, customExeName, persist: true);
    }

    private void ApplyCaptureSettingsCore(
        string networkAdapterDeviceName,
        EGameCapturePreference preference,
        string customExeName,
        bool persist)
    {
        EnsureInitialized();

        var normalizedDeviceName = NormalizePersistedDeviceName(networkAdapterDeviceName);
        if (!Settings.IsAutomaticNetCaptureDeviceName(normalizedDeviceName)
            && FindPersistedSelection(_availableAdapters, normalizedDeviceName) is null)
        {
            _diagnosticLog.Warning(
                "Adapter",
                $"Falling back to automatic capture-adapter selection because the selected device is not in the current device list: {normalizedDeviceName}.");
            normalizedDeviceName = Settings.AutomaticNetCaptureDeviceName;
        }

        var normalizedCustomExeName = NormalizeGameCaptureCustomExeName(customExeName);
        var adapterChanged = !string.Equals(_activeNetCaptureDeviceName, normalizedDeviceName, StringComparison.Ordinal);
        var gameCaptureChanged = _activeGameCapturePreference != preference
            || !string.Equals(_activeGameCaptureCustomExeName, normalizedCustomExeName, StringComparison.Ordinal);

        if (!adapterChanged && !gameCaptureChanged)
        {
            if (persist)
            {
                PersistActiveCaptureSettings();
            }

            return;
        }

        _activeNetCaptureDeviceName = normalizedDeviceName;
        _activeGameCapturePreference = preference;
        _activeGameCaptureCustomExeName = normalizedCustomExeName;
        SelectedAdapter = FindSelectedAdapter(normalizedDeviceName);

        if (persist)
        {
            PersistActiveCaptureSettings();
        }

        RestartCapture();

        if (adapterChanged)
        {
            _diagnosticLog.Information("Adapter", $"Selected capture adapter: {SelectedAdapter?.DisplayName ?? "Auto"}.");
            SelectedAdapterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            Initialize();
        }
    }

    private void NormalizePersistedAdapterSelection()
    {
        var deviceName = NormalizePersistedDeviceName(Settings.Instance.NetCaptureDeviceName);
        if (Settings.IsAutomaticNetCaptureDeviceName(deviceName))
        {
            Settings.Instance.NetCaptureDeviceName = Settings.AutomaticNetCaptureDeviceName;
            return;
        }

        if (FindPersistedSelection(_availableAdapters, deviceName) is not null)
        {
            Settings.Instance.NetCaptureDeviceName = deviceName;
            return;
        }

        Settings.Instance.NetCaptureDeviceName = Settings.AutomaticNetCaptureDeviceName;
        Settings.Save();
        _diagnosticLog.Warning(
            "Adapter",
            $"Falling back to automatic capture-adapter selection because the persisted device is not in the current device list: {deviceName}.");
    }

    private NetworkAdapterInfo? FindSelectedAdapter(string persistedDeviceName)
    {
        var selected = FindPersistedSelection(_availableAdapters, persistedDeviceName);
        if (selected is not null)
        {
            return selected;
        }

        var runtimeDeviceName = ResolveRuntimeCaptureDeviceName(string.Empty);
        return FindPersistedSelection(_availableAdapters, runtimeDeviceName)
            ?? FindFirstAdapter(_availableAdapters);
    }

    private static NetworkAdapterInfo? FindPersistedSelection(
        IReadOnlyList<NetworkAdapterInfo> captureAdapters,
        string deviceName)
    {
        if (Settings.IsAutomaticNetCaptureDeviceName(deviceName))
        {
            return null;
        }

        return captureAdapters.FirstOrDefault(adapter =>
            string.Equals(adapter.DeviceName, deviceName, StringComparison.Ordinal));
    }

    private static NetworkAdapterInfo? FindFirstAdapter(IReadOnlyList<NetworkAdapterInfo> captureAdapters)
    {
        return captureAdapters.Count == 0
            ? null
            : captureAdapters[0];
    }

    private static string NormalizePersistedDeviceName(string? deviceName)
    {
        return Settings.IsAutomaticNetCaptureDeviceName(deviceName)
            ? Settings.AutomaticNetCaptureDeviceName
            : deviceName!.Trim();
    }

    private static string NormalizeGameCaptureCustomExeName(string? customExeName)
    {
        return Path.GetFileNameWithoutExtension(customExeName ?? string.Empty);
    }

    private void PersistActiveCaptureSettings()
    {
        Settings.Instance.NetCaptureDeviceName = _activeNetCaptureDeviceName;
        Settings.Instance.GameCapturePreference = _activeGameCapturePreference;
        Settings.Instance.GameCaptureCustomExeName = _activeGameCaptureCustomExeName;
        Settings.Save();
    }

    private void ApplyRuntimeCaptureSettings()
    {
        MessageManager.NetCaptureDeviceName = ResolveRuntimeCaptureDeviceName(_activeNetCaptureDeviceName);
        MessageManager.GameCapturePreference = _activeGameCapturePreference;
        MessageManager.GameCaptureCustomExeName = _activeGameCaptureCustomExeName;
    }

    private static string ResolveRuntimeCaptureDeviceName(string persistedDeviceName)
    {
        if (!Settings.IsAutomaticNetCaptureDeviceName(persistedDeviceName))
        {
            return persistedDeviceName;
        }

        return MessageManager.TryFindBestNetworkDevice()?.Name ?? string.Empty;
    }

    private IReadOnlyList<NetworkAdapterInfo> GetNetworkAdapters()
    {
        try
        {
            return CaptureDeviceList.Instance
                .Select(device => new NetworkAdapterInfo(
                    device.Name,
                    device.Description ?? device.Name))
                .ToList();
        }
        catch (Exception exception)
        {
            _diagnosticLog.Error("Adapter", "Capture-device enumeration failed.", exception);
            return [];
        }
    }

    private void RestartCapture()
    {
        MessageManager.StopCapturing();
        ApplyRuntimeCaptureSettings();

        if (!string.IsNullOrWhiteSpace(MessageManager.NetCaptureDeviceName))
        {
            MessageManager.InitializeCapturing();
        }
    }
}
