using System.Net;
using System.Threading;
using System.Threading.Channels;
using PacketDotNet;
using SharpPcap;
using StarResonanceDps.Core.Combat;
using StarResonanceDps.Core.Logging;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class PlayerRosterCaptureService : IDisposable
{
    private static readonly string[] GameProcessNames =
    [
        "star.exe",
        "BPSR_STEAM.exe",
        "BPSR_EPIC.exe",
        "BPSR.exe",
        "StarSEA.exe",
        "StarASIA.exe",
        "StarSEA_STEAM.exe",
        "StarASIA_STEAM.exe"
    ];

    private static readonly Lazy<PlayerRosterCaptureService> LazyInstance = new(() => new PlayerRosterCaptureService());

    private readonly object _sync = new();
    private readonly NetworkAdapterSession _networkAdapterSession = NetworkAdapterSession.Instance;
    private readonly PlayerRosterStore _playerRosterStore = PlayerRosterStore.Instance;
    private readonly PacketDiagnosticLogStore _diagnosticLog = PacketDiagnosticLogStore.Instance;
    private CaptureRun? _activeRun;
    private long _captureGeneration;
    private bool _isStarted;

    private PlayerRosterCaptureService()
    {
    }

    public static PlayerRosterCaptureService Instance => LazyInstance.Value;

    public void Start()
    {
        lock (_sync)
        {
            if (_isStarted)
            {
                return;
            }

            _isStarted = true;
            _networkAdapterSession.SelectedAdapterChanged += NetworkAdapterSession_SelectedAdapterChanged;
        }

        _diagnosticLog.Information("Capture", "Player-roster packet capture service started.");
        RestartCapture();
    }

    public void Stop()
    {
        CaptureRun? run;
        lock (_sync)
        {
            if (!_isStarted)
            {
                return;
            }

            _isStarted = false;
            _networkAdapterSession.SelectedAdapterChanged -= NetworkAdapterSession_SelectedAdapterChanged;
            run = _activeRun;
            _activeRun = null;
        }

        run?.Dispose();
        _diagnosticLog.Information("Capture", "Game packet capture service stopped.");
    }

    public void Dispose()
    {
        Stop();
    }

    private void NetworkAdapterSession_SelectedAdapterChanged(object? sender, EventArgs e)
    {
        RestartCapture();
    }

    private void RestartCapture()
    {
        CaptureRun? previousRun;
        long generation;
        lock (_sync)
        {
            if (!_isStarted)
            {
                return;
            }

            generation = ++_captureGeneration;
            previousRun = _activeRun;
            _activeRun = null;
        }

        previousRun?.Dispose();

        var selectedAdapter = _networkAdapterSession.SelectedAdapter;
        if (selectedAdapter is null)
        {
            _diagnosticLog.Warning("Capture", "Packet capture was not started because no capture adapter is selected.");
            return;
        }

        _diagnosticLog.Information("Capture", $"Starting packet capture on {selectedAdapter.DisplayName}.");
        var nextRun = TryCreateCaptureRun(selectedAdapter.DeviceName);
        if (nextRun is null)
        {
            return;
        }

        lock (_sync)
        {
            if (!_isStarted || _captureGeneration != generation)
            {
                nextRun.Dispose();
                return;
            }

            _activeRun = nextRun;
        }
    }

    private CaptureRun? TryCreateCaptureRun(string deviceName)
    {
        CaptureRun? run = null;
        try
        {
            var device = CaptureDeviceList.Instance
                .FirstOrDefault(candidate => string.Equals(candidate.Name, deviceName, StringComparison.Ordinal));
            if (device is not ILiveDevice liveDevice)
            {
                _diagnosticLog.Error(
                    "Capture",
                    $"The selected capture device was not found or cannot capture live traffic: {deviceName}.");
                return null;
            }

            run = new CaptureRun(liveDevice, _playerRosterStore, _diagnosticLog);
            run.Start();
            return run;
        }
        catch (Exception exception)
        {
            run?.Dispose();
            _diagnosticLog.Error("Capture", $"Failed to start packet capture for device {deviceName}.", exception);
            return null;
        }
    }

    private sealed class CaptureRun : IDisposable
    {
        private static readonly TimeSpan DiagnosticInterval = TimeSpan.FromSeconds(5);
        private const int MaximumNotifyTypeSamples = 16;

        private readonly ILiveDevice _device;
        private readonly PlayerRosterStore _playerRosterStore;
        private readonly PacketDiagnosticLogStore _diagnosticLog;
        private readonly GameProcessPortWatcher _gameConnectionWatcher;
        private readonly GameNotificationProcessor _notificationProcessor;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Channel<TcpSegment> _segments = Channel.CreateUnbounded<TcpSegment>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        private readonly Timer _diagnosticTimer;
        private readonly Task _processorTask;
        private readonly HashSet<NotifyTypeKey> _sampledNotifyTypes = [];

        private string? _appliedFilter;
        private int _resetRequested;
        private int _activeFlowCount;
        private int _lastRosterSize;
        private long _capturedPacketCount;
        private long _ipv4TcpPacketCount;
        private long _payloadSegmentCount;
        private long _queuedSegmentCount;
        private long _queueDropCount;
        private long _packetFailureCount;
        private long _gameConnectionSegmentCount;
        private long _ignoredConnectionSegmentCount;
        private long _decodedNotifyCount;
        private long _enterSceneCount;
        private long _syncNearEntitiesCount;
        private long _syncContainerDataCount;
        private long _syncContainerDirtyDataCount;
        private long _syncDungeonDataCount;
        private long _syncDungeonDirtyDataCount;
        private long _syncNearDeltaInfoCount;
        private long _syncToMeDeltaInfoCount;
        private long _combatEventCount;
        private long _rosterUpdateCount;
        private long _mapChangeCount;
        private long _noCharacterEntityMessageCount;
        private long _rosterParseFailureCount;
        private long _protocolFailureCount;
        private long _gapRecoveryCount;
        private bool _isDisposed;

        public CaptureRun(
            ILiveDevice device,
            PlayerRosterStore playerRosterStore,
            PacketDiagnosticLogStore diagnosticLog)
        {
            _device = device;
            _playerRosterStore = playerRosterStore;
            _diagnosticLog = diagnosticLog;
            _gameConnectionWatcher = new GameProcessPortWatcher(GameProcessNames, OnGameConnectionWatcherFailure);
            _notificationProcessor = new GameNotificationProcessor(GameCombatStore.Instance);
            _diagnosticTimer = new Timer(WriteActivitySummary, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _processorTask = Task.Run(ProcessSegmentsAsync);
        }

        public void Start()
        {
            _gameConnectionWatcher.SnapshotChanged += GameConnectionWatcher_SnapshotChanged;
            _gameConnectionWatcher.Start();

            _diagnosticLog.Information("Capture", $"Opening capture device: {GetDeviceDisplayName()}.");
            _device.Open(new DeviceConfiguration
            {
                Mode = DeviceModes.Promiscuous,
                Immediate = true,
                ReadTimeout = 1000,
                BufferSize = 4 * 1024 * 1024
            });

            ApplyCaptureFilter();
            _device.OnPacketArrival += Device_OnPacketArrival;
            _device.StartCapture();
            _diagnosticTimer.Change(DiagnosticInterval, DiagnosticInterval);

            _diagnosticLog.Information(
                "Capture",
                "Packet capture started. TCP traffic is matched to supported game-process connections before protocol decoding.");
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _diagnosticTimer.Dispose();
            _gameConnectionWatcher.SnapshotChanged -= GameConnectionWatcher_SnapshotChanged;
            _gameConnectionWatcher.Dispose();

            try
            {
                _device.OnPacketArrival -= Device_OnPacketArrival;
                _device.StopCapture();
            }
            catch (Exception exception)
            {
                _diagnosticLog.Warning("Capture", "Stopping packet capture failed.", exception);
            }

            try
            {
                _device.Close();
            }
            catch (Exception exception)
            {
                _diagnosticLog.Warning("Capture", "Closing the capture device failed.", exception);
            }

            _segments.Writer.TryComplete();
            _cancellation.Cancel();
            try
            {
                _processorTask.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _diagnosticLog.Warning("TCP", "Waiting for the TCP segment processor to stop failed.", exception);
            }

            _cancellation.Dispose();
            _diagnosticLog.Information("Capture", $"Packet capture stopped: {GetDeviceDisplayName()}.");
        }

        private void GameConnectionWatcher_SnapshotChanged(
            object? sender,
            GameProcessConnectionSnapshotChangedEventArgs e)
        {
            var snapshot = e.Snapshot;
            _diagnosticLog.Information(
                "Process",
                $"Game connection scan updated. Processes={snapshot.TargetProcessCount}, Connections={snapshot.ConnectionCount}, TcpPorts={FormatPorts(snapshot.TcpPorts)}, SupportedProcesses={string.Join(",", _gameConnectionWatcher.SupportedProcessNames)}.");

            if (snapshot.ConnectionCount != 0)
            {
                return;
            }

            Interlocked.Exchange(ref _resetRequested, 1);
            _diagnosticLog.Information(
                "PlayerRoster",
                "No active TCP connection belongs to a supported game process. The last player roster and map are retained until newer game data arrives.");
        }

        private void ApplyCaptureFilter()
        {
            var filter = GameProcessPortWatcher.BuildCaptureFilter();
            try
            {
                _device.Filter = filter;
                _appliedFilter = filter;
                _diagnosticLog.Information(
                    "Capture",
                    $"Capture filter applied. Filter={filter}.");
            }
            catch (Exception exception)
            {
                _diagnosticLog.Error("Capture", $"Applying capture filter failed: {filter}.", exception);
            }
        }

        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            Interlocked.Increment(ref _capturedPacketCount);

            try
            {
                var rawPacket = e.GetPacket();
                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
                var ipv4 = packet.Extract<IPv4Packet>();
                var tcp = packet.Extract<TcpPacket>();
                if (ipv4 is null || tcp is null)
                {
                    return;
                }

                Interlocked.Increment(ref _ipv4TcpPacketCount);
                var payload = tcp.PayloadData;
                var payloadBytes = payload is null ? Array.Empty<byte>() : payload.ToArray();
                if (payloadBytes.Length > 0)
                {
                    Interlocked.Increment(ref _payloadSegmentCount);
                }

                var segment = new TcpSegment(
                    new TcpFlowKey(
                        ipv4.SourceAddress,
                        tcp.SourcePort,
                        ipv4.DestinationAddress,
                        tcp.DestinationPort),
                    tcp.SequenceNumber,
                    tcp.Synchronize,
                    tcp.Finished || tcp.Reset,
                    payloadBytes);

                if (_segments.Writer.TryWrite(segment))
                {
                    Interlocked.Increment(ref _queuedSegmentCount);
                }
                else
                {
                    Interlocked.Increment(ref _queueDropCount);
                }
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref _packetFailureCount);
                _diagnosticLog.Warning("Capture", "Packet parsing failed.", exception);
            }
        }

        private async Task ProcessSegmentsAsync()
        {
            var flows = new Dictionary<TcpFlowKey, TcpFlowProcessor>();
            var connectionMatches = new Dictionary<TcpFlowKey, bool>();
            var connectionSnapshotVersion = long.MinValue;
            var lastFlowCleanup = DateTime.UtcNow;

            try
            {
                await foreach (var segment in _segments.Reader.ReadAllAsync(_cancellation.Token))
                {
                    if (Interlocked.Exchange(ref _resetRequested, 0) != 0)
                    {
                        DisposeFlows(flows);
                        flows.Clear();
                        connectionMatches.Clear();
                        connectionSnapshotVersion = long.MinValue;
                        Volatile.Write(ref _activeFlowCount, 0);
                    }

                    var snapshot = _gameConnectionWatcher.Snapshot;
                    if (connectionSnapshotVersion != snapshot.Version)
                    {
                        connectionMatches.Clear();
                        connectionSnapshotVersion = snapshot.Version;
                    }

                    if (!connectionMatches.TryGetValue(segment.Flow, out var isGameConnection))
                    {
                        isGameConnection = snapshot.Matches(
                            segment.Flow.SourceAddress,
                            segment.Flow.SourcePort,
                            segment.Flow.DestinationAddress,
                            segment.Flow.DestinationPort);

                        if (!isGameConnection)
                        {
                            snapshot = _gameConnectionWatcher.RefreshNow();
                            if (connectionSnapshotVersion != snapshot.Version)
                            {
                                connectionMatches.Clear();
                                connectionSnapshotVersion = snapshot.Version;
                            }

                            isGameConnection = snapshot.Matches(
                                segment.Flow.SourceAddress,
                                segment.Flow.SourcePort,
                                segment.Flow.DestinationAddress,
                                segment.Flow.DestinationPort);
                        }

                        connectionMatches[segment.Flow] = isGameConnection;
                    }

                    if (!isGameConnection)
                    {
                        Interlocked.Increment(ref _ignoredConnectionSegmentCount);
                        if (segment.IsTerminal)
                        {
                            connectionMatches.Remove(segment.Flow);
                        }

                        continue;
                    }

                    Interlocked.Increment(ref _gameConnectionSegmentCount);

                    var now = DateTime.UtcNow;
                    if (now - lastFlowCleanup >= TimeSpan.FromSeconds(30))
                    {
                        RemoveInactiveFlows(flows, connectionMatches, now);
                        Volatile.Write(ref _activeFlowCount, flows.Count);
                        lastFlowCleanup = now;
                    }

                    if (!flows.TryGetValue(segment.Flow, out var flow))
                    {
                        flow = new TcpFlowProcessor(HandleNotify, HandleProtocolFailure, HandleGapRecovery);
                        flows.Add(segment.Flow, flow);
                        Volatile.Write(ref _activeFlowCount, flows.Count);
                    }

                    flow.AddSegment(segment, now);
                    if (segment.IsTerminal)
                    {
                        flow.Dispose();
                        flows.Remove(segment.Flow);
                        connectionMatches.Remove(segment.Flow);
                        Volatile.Write(ref _activeFlowCount, flows.Count);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _diagnosticLog.Error("TCP", "TCP segment processing stopped because of an unhandled error.", exception);
            }
            finally
            {
                DisposeFlows(flows);
                Volatile.Write(ref _activeFlowCount, 0);
            }
        }

        private void HandleNotify(ulong serviceId, uint methodId, byte[] payload)
        {
            Interlocked.Increment(ref _decodedNotifyCount);
            var notifyType = new NotifyTypeKey(serviceId, methodId);
            if (_sampledNotifyTypes.Count < MaximumNotifyTypeSamples && _sampledNotifyTypes.Add(notifyType))
            {
                _diagnosticLog.Information(
                    "Protocol",
                    $"Notify observed. ServiceId={serviceId}, MethodId={methodId}, PayloadBytes={payload.Length}.");
            }

            var outcome = _notificationProcessor.Process(
                serviceId,
                methodId,
                payload,
                DateTimeOffset.UtcNow);
            if (outcome.Result == GameNotificationProcessResult.Ignored)
            {
                return;
            }

            switch (outcome.Source)
            {
                case "EnterScene":
                    Interlocked.Increment(ref _enterSceneCount);
                    break;

                case "SyncNearEntities":
                    Interlocked.Increment(ref _syncNearEntitiesCount);
                    break;

                case "SyncContainerData":
                    Interlocked.Increment(ref _syncContainerDataCount);
                    Interlocked.Increment(ref _mapChangeCount);
                    break;

                case "SyncContainerDirtyData":
                    Interlocked.Increment(ref _syncContainerDirtyDataCount);
                    break;

                case "SyncDungeonData":
                    Interlocked.Increment(ref _syncDungeonDataCount);
                    break;

                case "SyncDungeonDirtyData":
                    Interlocked.Increment(ref _syncDungeonDirtyDataCount);
                    break;

                case "SyncNearDeltaInfo":
                    Interlocked.Increment(ref _syncNearDeltaInfoCount);
                    break;

                case "SyncToMeDeltaInfo":
                    Interlocked.Increment(ref _syncToMeDeltaInfoCount);
                    break;
            }

            Volatile.Write(ref _lastRosterSize, outcome.VisibleRosterSize);
            Interlocked.Add(ref _combatEventCount, outcome.CombatEventCount);

            if (outcome.Result == GameNotificationProcessResult.InvalidPayload)
            {
                Interlocked.Increment(ref _rosterParseFailureCount);
                _diagnosticLog.Warning(
                    "Game",
                    $"{outcome.Source} payload could not be decoded. PayloadBytes={payload.Length}.");
                return;
            }

            if (outcome.Source == "SyncContainerData")
            {
                var current = _playerRosterStore.Current;
                _diagnosticLog.Information(
                    "PlayerRoster",
                    $"Game context was replaced. MapName={current.MapName}, Players={outcome.VisibleRosterSize}.");
            }

            if (outcome.VisibleRosterSize > 0)
            {
                Interlocked.Increment(ref _rosterUpdateCount);
            }
        }

        private void HandleProtocolFailure(string message, Exception? exception)
        {
            Interlocked.Increment(ref _protocolFailureCount);
            _diagnosticLog.Warning("Protocol", message, exception);
        }

        private void HandleGapRecovery(uint expectedSequence, uint resumedSequence)
        {
            Interlocked.Increment(ref _gapRecoveryCount);
            _diagnosticLog.Warning(
                "TCP",
                $"TCP stream gap recovery advanced sequence from {expectedSequence} to {resumedSequence}.");
        }

        private void OnGameConnectionWatcherFailure(Exception exception)
        {
            _diagnosticLog.Warning("Process", "Inspecting supported game-process TCP connections failed.", exception);
        }

        private void WriteActivitySummary(object? state)
        {
            if (_isDisposed)
            {
                return;
            }

            var snapshot = _gameConnectionWatcher.Snapshot;
            var capturedPackets = Interlocked.Exchange(ref _capturedPacketCount, 0);
            var ipv4TcpPackets = Interlocked.Exchange(ref _ipv4TcpPacketCount, 0);
            var payloadSegments = Interlocked.Exchange(ref _payloadSegmentCount, 0);
            var queuedSegments = Interlocked.Exchange(ref _queuedSegmentCount, 0);
            var queueDrops = Interlocked.Exchange(ref _queueDropCount, 0);
            var packetFailures = Interlocked.Exchange(ref _packetFailureCount, 0);
            var gameConnectionSegments = Interlocked.Exchange(ref _gameConnectionSegmentCount, 0);
            var ignoredConnectionSegments = Interlocked.Exchange(ref _ignoredConnectionSegmentCount, 0);
            var decodedNotifies = Interlocked.Exchange(ref _decodedNotifyCount, 0);
            var enterScene = Interlocked.Exchange(ref _enterSceneCount, 0);
            var syncNearEntities = Interlocked.Exchange(ref _syncNearEntitiesCount, 0);
            var syncContainerData = Interlocked.Exchange(ref _syncContainerDataCount, 0);
            var syncContainerDirtyData = Interlocked.Exchange(ref _syncContainerDirtyDataCount, 0);
            var syncDungeonData = Interlocked.Exchange(ref _syncDungeonDataCount, 0);
            var syncDungeonDirtyData = Interlocked.Exchange(ref _syncDungeonDirtyDataCount, 0);
            var syncNearDeltaInfo = Interlocked.Exchange(ref _syncNearDeltaInfoCount, 0);
            var syncToMeDeltaInfo = Interlocked.Exchange(ref _syncToMeDeltaInfoCount, 0);
            var combatEvents = Interlocked.Exchange(ref _combatEventCount, 0);
            var rosterUpdates = Interlocked.Exchange(ref _rosterUpdateCount, 0);
            var mapChanges = Interlocked.Exchange(ref _mapChangeCount, 0);
            var noCharacterEntityMessages = Interlocked.Exchange(ref _noCharacterEntityMessageCount, 0);
            var rosterParseFailures = Interlocked.Exchange(ref _rosterParseFailureCount, 0);
            var protocolFailures = Interlocked.Exchange(ref _protocolFailureCount, 0);
            var gapRecoveries = Interlocked.Exchange(ref _gapRecoveryCount, 0);

            _diagnosticLog.Information(
                "Capture",
                $"Activity ({DiagnosticInterval.TotalSeconds:0}s): Filter={_appliedFilter ?? "None"}, Captured={capturedPackets}, IPv4Tcp={ipv4TcpPackets}, PayloadSegments={payloadSegments}, GameSegments={gameConnectionSegments}, IgnoredSegments={ignoredConnectionSegments}, Queued={queuedSegments}, QueueDrops={queueDrops}, GameProcesses={snapshot.TargetProcessCount}, GameConnections={snapshot.ConnectionCount}, GamePorts={FormatPorts(snapshot.TcpPorts)}, ActiveFlows={Volatile.Read(ref _activeFlowCount)}, Notify={decodedNotifies}, EnterScene={enterScene}, SyncNearEntities={syncNearEntities}, SyncContainerData={syncContainerData}, SyncContainerDirtyData={syncContainerDirtyData}, SyncDungeonData={syncDungeonData}, SyncDungeonDirtyData={syncDungeonDirtyData}, SyncNearDeltaInfo={syncNearDeltaInfo}, SyncToMeDeltaInfo={syncToMeDeltaInfo}, CombatEvents={combatEvents}, RosterUpdates={rosterUpdates}, MapChanges={mapChanges}, NoCharacterEntities={noCharacterEntityMessages}, ParserFailures={rosterParseFailures}, ProtocolFailures={protocolFailures}, TcpGapRecoveries={gapRecoveries}, RosterSize={Volatile.Read(ref _lastRosterSize)}, PacketFailures={packetFailures}.");
        }

        private string GetDeviceDisplayName()
        {
            return string.IsNullOrWhiteSpace(_device.Description)
                ? _device.Name
                : _device.Description;
        }

        private static string FormatPorts(IReadOnlyCollection<int> ports)
        {
            return ports.Count == 0
                ? "None"
                : string.Join(",", ports.Order());
        }

        private static void RemoveInactiveFlows(
            Dictionary<TcpFlowKey, TcpFlowProcessor> flows,
            Dictionary<TcpFlowKey, bool> connectionMatches,
            DateTime now)
        {
            foreach (var key in flows
                .Where(pair => now - pair.Value.LastSegmentAtUtc >= TimeSpan.FromSeconds(60))
                .Select(pair => pair.Key)
                .ToArray())
            {
                flows[key].Dispose();
                flows.Remove(key);
                connectionMatches.Remove(key);
            }
        }

        private static void DisposeFlows(Dictionary<TcpFlowKey, TcpFlowProcessor> flows)
        {
            foreach (var flow in flows.Values)
            {
                flow.Dispose();
            }
        }

        private readonly record struct NotifyTypeKey(ulong ServiceId, uint MethodId);
    }

    private sealed class TcpFlowProcessor : IDisposable
    {
        private readonly TcpFlowReassembler _reassembler;
        private readonly GameProtocolDecoder _decoder;
        private readonly Action<byte[]> _appendDecoderData;

        public TcpFlowProcessor(
            Action<ulong, uint, byte[]> onNotify,
            Action<string, Exception?> onProtocolFailure,
            Action<uint, uint> onGapRecovery)
        {
            _reassembler = new TcpFlowReassembler(onGapRecovery);
            _decoder = new GameProtocolDecoder(onNotify, onProtocolFailure);
            _appendDecoderData = data => _decoder.Append(data);
        }

        public DateTime LastSegmentAtUtc { get; private set; } = DateTime.UtcNow;

        public void AddSegment(TcpSegment segment, DateTime receivedAtUtc)
        {
            LastSegmentAtUtc = receivedAtUtc;
            _reassembler.AddSegment(segment.SequenceNumber, segment.IsSynchronize, segment.Payload, _appendDecoderData);
        }

        public void Dispose()
        {
            _decoder.Dispose();
            _reassembler.Reset();
        }
    }

    private readonly record struct TcpFlowKey(
        IPAddress SourceAddress,
        ushort SourcePort,
        IPAddress DestinationAddress,
        ushort DestinationPort);

    private readonly record struct TcpSegment(
        TcpFlowKey Flow,
        uint SequenceNumber,
        bool IsSynchronize,
        bool IsTerminal,
        byte[] Payload);
}
