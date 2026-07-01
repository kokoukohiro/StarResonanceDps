using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace StarResonanceDps.Core.Services;

internal sealed class GameProcessPortWatcher : IDisposable
{
    private const uint NoError = 0;
    private const uint ErrorInsufficientBuffer = 122;

    private readonly object _refreshSync = new();
    private readonly string[] _processNames;
    private readonly TimeSpan _refreshInterval;
    private readonly Action<Exception>? _onRefreshFailure;
    private GameProcessConnectionSnapshot _snapshot = GameProcessConnectionSnapshot.Empty;
    private Timer? _timer;
    private int _isDisposed;
    private int _isStarted;

    public GameProcessPortWatcher(
        IEnumerable<string> processNames,
        Action<Exception>? onRefreshFailure = null,
        TimeSpan? refreshInterval = null)
    {
        _processNames = NormalizeProcessNames(processNames)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _onRefreshFailure = onRefreshFailure;
        _refreshInterval = refreshInterval ?? TimeSpan.FromSeconds(2);
    }

    public event EventHandler<GameProcessConnectionSnapshotChangedEventArgs>? SnapshotChanged;

    public GameProcessConnectionSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public IReadOnlyCollection<string> SupportedProcessNames => _processNames;

    public void Start()
    {
        if (Interlocked.Exchange(ref _isStarted, 1) != 0)
        {
            return;
        }

        RefreshCore(publishEvenIfUnchanged: true);
        _timer = new Timer(Refresh, null, _refreshInterval, _refreshInterval);
    }

    public GameProcessConnectionSnapshot RefreshNow()
    {
        RefreshCore(publishEvenIfUnchanged: false);
        return Snapshot;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _timer?.Dispose();
        _timer = null;
    }

    public static string BuildCaptureFilter()
    {
        return "ip and tcp";
    }

    private void Refresh(object? state)
    {
        RefreshCore(publishEvenIfUnchanged: false);
    }

    private void RefreshCore(bool publishEvenIfUnchanged)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        try
        {
            var processIds = GetTargetProcessIds();
            var connections = GetTcpRows()
                .Where(row => processIds.Contains(unchecked((int)row.OwningProcessId)))
                .Select(CreateConnection)
                .Where(connection => connection is not null)
                .Select(connection => connection!.Value)
                .Distinct()
                .OrderBy(connection => connection.LocalAddress.ToString(), StringComparer.Ordinal)
                .ThenBy(connection => connection.LocalPort)
                .ThenBy(connection => connection.RemoteAddress.ToString(), StringComparer.Ordinal)
                .ThenBy(connection => connection.RemotePort)
                .ToArray();

            var localPorts = connections
                .Select(connection => checked((int)connection.LocalPort))
                .Distinct()
                .Order()
                .ToArray();

            var processIdSnapshot = processIds.Order().ToArray();
            var candidate = new GameProcessConnectionSnapshot(
                version: 0,
                capturedAtUtc: DateTime.UtcNow,
                targetProcessIds: processIdSnapshot,
                tcpPorts: localPorts,
                connections: connections);

            GameProcessConnectionSnapshot? publishedSnapshot = null;
            lock (_refreshSync)
            {
                var previous = _snapshot;
                if (!publishEvenIfUnchanged && previous.HasSameTopology(candidate))
                {
                    return;
                }

                publishedSnapshot = candidate.WithVersion(checked(previous.Version + 1));
                Volatile.Write(ref _snapshot, publishedSnapshot);
            }

            SnapshotChanged?.Invoke(
                this,
                new GameProcessConnectionSnapshotChangedEventArgs(publishedSnapshot));
        }
        catch (Exception exception)
        {
            _onRefreshFailure?.Invoke(exception);
        }
    }

    private HashSet<int> GetTargetProcessIds()
    {
        var processIds = new HashSet<int>();

        foreach (var processName in _processNames)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    using (process)
                    {
                        processIds.Add(process.Id);
                    }
                }
            }
            catch
            {
            }
        }

        return processIds;
    }

    private static GameTcpConnection? CreateConnection(MibTcpRowOwnerPid row)
    {
        var localPort = GetPort(row.LocalPort);
        var remotePort = GetPort(row.RemotePort);
        if (localPort == 0
            || remotePort == 0
            || row.RemoteAddress == 0)
        {
            return null;
        }

        return new GameTcpConnection(
            new IPAddress(unchecked((long)row.LocalAddress)),
            checked((ushort)localPort),
            new IPAddress(unchecked((long)row.RemoteAddress)),
            checked((ushort)remotePort));
    }

    private static string[] NormalizeProcessNames(IEnumerable<string> processNames)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var processName in processNames)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                continue;
            }

            var normalized = processName.Trim();
            if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^4];
            }

            result.Add(normalized);
        }

        return result.ToArray();
    }

    private static IEnumerable<MibTcpRowOwnerPid> GetTcpRows()
    {
        var bufferSize = 0;
        var result = GetExtendedTcpTable(
            IntPtr.Zero,
            ref bufferSize,
            true,
            (int)AddressFamily.InterNetwork,
            TcpTableClass.OwnerPidAll,
            0);

        if (result != ErrorInsufficientBuffer)
        {
            ThrowIfError(result, "GetExtendedTcpTable(size)");
        }

        var table = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedTcpTable(
                table,
                ref bufferSize,
                true,
                (int)AddressFamily.InterNetwork,
                TcpTableClass.OwnerPidAll,
                0);
            ThrowIfError(result, "GetExtendedTcpTable");

            var count = Marshal.ReadInt32(table);
            var row = IntPtr.Add(table, sizeof(uint));
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

            for (var index = 0; index < count; index++)
            {
                yield return Marshal.PtrToStructure<MibTcpRowOwnerPid>(row);
                row = IntPtr.Add(row, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(table);
        }
    }

    private static int GetPort(uint encodedPort)
    {
        var networkOrder = unchecked((ushort)(encodedPort & 0xFFFF));
        return unchecked((ushort)IPAddress.NetworkToHostOrder(unchecked((short)networkOrder)));
    }

    private static void ThrowIfError(uint result, string operation)
    {
        if (result == NoError)
        {
            return;
        }

        throw new Win32Exception(unchecked((int)result), $"{operation} failed with {result}.");
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int outputBufferLength,
        bool sort,
        int addressFamily,
        TcpTableClass tableClass,
        uint reserved);

    private enum AddressFamily
    {
        InterNetwork = 2
    }

    private enum TcpTableClass
    {
        OwnerPidAll = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddress;
        public uint LocalPort;
        public uint RemoteAddress;
        public uint RemotePort;
        public uint OwningProcessId;
    }
}

internal sealed class GameProcessConnectionSnapshot
{
    public static GameProcessConnectionSnapshot Empty { get; } = new(
        version: 0,
        capturedAtUtc: DateTime.MinValue,
        targetProcessIds: Array.Empty<int>(),
        tcpPorts: Array.Empty<int>(),
        connections: Array.Empty<GameTcpConnection>());

    public GameProcessConnectionSnapshot(
        long version,
        DateTime capturedAtUtc,
        IReadOnlyList<int> targetProcessIds,
        IReadOnlyList<int> tcpPorts,
        IReadOnlyList<GameTcpConnection> connections)
    {
        Version = version;
        CapturedAtUtc = capturedAtUtc;
        TargetProcessIds = targetProcessIds;
        TcpPorts = tcpPorts;
        Connections = connections;
    }

    public long Version { get; }

    public DateTime CapturedAtUtc { get; }

    public IReadOnlyList<int> TargetProcessIds { get; }

    public IReadOnlyList<int> TcpPorts { get; }

    public IReadOnlyList<GameTcpConnection> Connections { get; }

    public int TargetProcessCount => TargetProcessIds.Count;

    public int ConnectionCount => Connections.Count;

    public bool Matches(IPAddress sourceAddress, ushort sourcePort, IPAddress destinationAddress, ushort destinationPort)
    {
        foreach (var connection in Connections)
        {
            if (connection.Matches(sourceAddress, sourcePort, destinationAddress, destinationPort))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasSameTopology(GameProcessConnectionSnapshot other)
    {
        return TargetProcessIds.SequenceEqual(other.TargetProcessIds)
            && TcpPorts.SequenceEqual(other.TcpPorts)
            && Connections.SequenceEqual(other.Connections);
    }

    public GameProcessConnectionSnapshot WithVersion(long version)
    {
        return new GameProcessConnectionSnapshot(
            version,
            CapturedAtUtc,
            TargetProcessIds,
            TcpPorts,
            Connections);
    }
}

internal readonly record struct GameTcpConnection(
    IPAddress LocalAddress,
    ushort LocalPort,
    IPAddress RemoteAddress,
    ushort RemotePort)
{
    public bool Matches(IPAddress sourceAddress, ushort sourcePort, IPAddress destinationAddress, ushort destinationPort)
    {
        return (LocalAddress.Equals(sourceAddress)
                && LocalPort == sourcePort
                && RemoteAddress.Equals(destinationAddress)
                && RemotePort == destinationPort)
            || (LocalAddress.Equals(destinationAddress)
                && LocalPort == destinationPort
                && RemoteAddress.Equals(sourceAddress)
                && RemotePort == sourcePort);
    }
}

internal sealed class GameProcessConnectionSnapshotChangedEventArgs(GameProcessConnectionSnapshot snapshot) : EventArgs
{
    public GameProcessConnectionSnapshot Snapshot { get; } = snapshot;
}
