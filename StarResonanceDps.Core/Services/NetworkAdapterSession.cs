using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using SharpPcap;
using StarResonanceDps.Core.Logging;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class NetworkAdapterSession
{
    private const string RouteProbeAddress = "8.8.8.8";

    private static readonly Lazy<NetworkAdapterSession> LazyInstance = new(() => new NetworkAdapterSession());

    private readonly PacketDiagnosticLogStore _diagnosticLog = PacketDiagnosticLogStore.Instance;
    private IReadOnlyList<NetworkAdapterInfo> _availableAdapters = [];

    private NetworkAdapterSession()
    {
    }

    public static NetworkAdapterSession Instance => LazyInstance.Value;

    public IReadOnlyList<NetworkAdapterInfo> AvailableAdapters => _availableAdapters;

    public NetworkAdapterInfo? SelectedAdapter { get; private set; }

    public bool IsInitialized { get; private set; }

    public bool HasAutomaticSelection { get; private set; }

    public event EventHandler? SelectedAdapterChanged;

    public void Initialize()
    {
        if (IsInitialized)
        {
            return;
        }

        _availableAdapters = GetNetworkAdapters();
        _diagnosticLog.Information(
            "Adapter",
            $"Capture-device enumeration completed. Candidates={_availableAdapters.Count}.");

        SelectedAdapter = FindAutomaticSelection(_availableAdapters);
        HasAutomaticSelection = SelectedAdapter is not null;
        if (SelectedAdapter is null)
        {
            _diagnosticLog.Warning(
                "Adapter",
                "Automatic capture-adapter selection did not find a route-matched capture device.");
        }
        else
        {
            _diagnosticLog.Information(
                "Adapter",
                $"Automatically selected capture adapter: {SelectedAdapter.DisplayName}.");
        }

        IsInitialized = true;
    }

    public bool SelectAdapter(NetworkAdapterInfo? adapter)
    {
        if (!IsInitialized)
        {
            Initialize();
        }

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

        if (string.Equals(SelectedAdapter?.DeviceName, selected.DeviceName, StringComparison.Ordinal))
        {
            return true;
        }

        SelectedAdapter = selected;
        _diagnosticLog.Information("Adapter", $"Selected capture adapter: {selected.DisplayName}.");
        SelectedAdapterChanged?.Invoke(this, EventArgs.Empty);
        return true;
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

    private static NetworkAdapterInfo? FindAutomaticSelection(
        IReadOnlyList<NetworkAdapterInfo> captureAdapters)
    {
        var routeInterfaceIndex = TryGetRouteInterfaceIndex();
        if (routeInterfaceIndex is null)
        {
            return null;
        }

        NetworkInterface? routeInterface;
        try
        {
            routeInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(networkInterface => GetIpv4InterfaceIndex(networkInterface) == routeInterfaceIndex);
        }
        catch
        {
            return null;
        }

        if (routeInterface is null)
        {
            return null;
        }

        NetworkAdapterInfo? selected = null;
        var bestScore = 0;

        foreach (var captureAdapter in captureAdapters)
        {
            var score = 0;

            if (captureAdapter.Description.Contains(routeInterface.Name, StringComparison.OrdinalIgnoreCase))
            {
                score += 2;
            }

            if (captureAdapter.Description.Contains(routeInterface.Description, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            selected = captureAdapter;
        }

        return selected;
    }

    private static int? GetIpv4InterfaceIndex(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface.GetIPProperties().GetIPv4Properties().Index;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetRouteInterfaceIndex()
    {
        try
        {
            var addressBytes = IPAddress.Parse(RouteProbeAddress).GetAddressBytes();
            var destinationAddress = BitConverter.ToUInt32(addressBytes, 0);

            return GetBestInterface(destinationAddress, out var interfaceIndex) == 0
                ? checked((int)interfaceIndex)
                : null;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetBestInterface(uint destinationAddress, out uint interfaceIndex);
}
