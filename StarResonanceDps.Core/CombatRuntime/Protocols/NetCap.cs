using System.Buffers;
using Microsoft.Extensions.ObjectPool;
using PacketDotNet;
using Serilog;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using ZstdSharp;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public class NetCap
{
    private NetCapConfig Config = null!;
    public ICaptureDevice CaptureDevice = null!;
    public TcpReassembler TcpReassempler = null!;

    private CancellationTokenSource CancelTokenSrc = new();
    public ObjectPool<RawPacket> RawPacketPool = ObjectPool.Create(new DefaultPooledObjectPolicy<RawPacket>());
    public ConcurrentQueue<RawPacket> RawPacketQueue = new();
    private Task PacketParseTask = null!;
    private byte[] DecompressionScratchBuffer = new byte[1024 * 1024];
    private Decompressor _decompressor = new();
    private Dictionary<NotifyId, Action<ReadOnlySpan<byte>, ExtraPacketData>> NotifyHandlers = new();
    private Dictionary<ProxyId, Action<ReadOnlySpan<byte>, uint, ExtraPacketData>> ProxyHandlers = new();
    private Dictionary<ProxyId, Action<ReadOnlySpan<byte>, uint, ExtraPacketData>> ProxyReturnHandlers = new();
    private ConcurrentDictionary<uint, ProxyId> ProxyReturnsDictionary = new();
    public ulong NumSeenPackets = 0;
    public DateTime LastPacketSeenAt = DateTime.MinValue;
    public int NumConnectionReaders = 0;
    public ConcurrentDictionary<ConnectionId, bool> ConnectionFilters = new();
    public ConcurrentBag<string> ImportantLogMsgs = [];
    public ulong NumGameMessagesSeen = 0;
    public ulong NumGameMessagesDequeued = 0;

    private static readonly TimeSpan FailureLogInterval = TimeSpan.FromMinutes(1);
    private readonly object FailureLogSync = new();
    private readonly Dictionary<string, (DateTime LastLogged, int Suppressed)> FailureLogState = new(StringComparer.Ordinal);

    private bool IsDebugCaptureFileMode = false;
    private string DebugCaptureFile = "";
    private DateTime LastDebugCapturePacketTime = DateTime.MinValue;

    public void Init(NetCapConfig config)
    {
        Config = config;
    }

    public void Start()
    {
        ProxyReturnsDictionary.Clear();

        if (!string.IsNullOrEmpty(DebugCaptureFile) && IsDebugCaptureFileMode)
        {
            CaptureDevice = new CaptureFileReaderDevice(DebugCaptureFile);
            CaptureDevice.Open();
        }
        else
        {
            CaptureDevice = GetCaptureDevice();
            CaptureDevice.Open(DeviceModes.Promiscuous, 100);
        }

        PacketParseTask = Task.Factory.StartNew(ParsePacketsLoop, CancelTokenSrc.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        TcpReassempler = new TcpReassembler();
        TcpReassempler.OnNewConnection += OnNewConnection;

        CaptureDevice.Filter = "tcp and not portrange 0-1000";
        CaptureDevice.OnPacketArrival += DeviceOnOnPacketArrival;
        CaptureDevice.StartCapture();

        Log.Information("Capture device started");
    }

    public void RegisterNotifyHandler(ulong serviceId, uint methodId, Action<ReadOnlySpan<byte>, ExtraPacketData> handler)
    {
        NotifyHandlers.Add(new NotifyId(serviceId, methodId), handler);
    }

    /// <summary>
    /// 通知を処理してよいかの関門。<c>null</c> なら全部通す。
    /// ログイン画面にいる間、ゲームの状態を変える通知を落とすために使う(<c>MessageManager.ShouldDispatchNotify</c>)。
    /// </summary>
    public Func<ulong, uint, bool>? NotifyGate { get; set; }

    /// <summary>要求・応答を処理してよいかの関門。<c>null</c> なら全部通す。</summary>
    public Func<uint, uint, bool>? ProxyGate { get; set; }

    public void RegisterProxyHandler(uint serviceId, uint methodId, Action<ReadOnlySpan<byte>, uint, ExtraPacketData> handler)
    {
        ProxyHandlers.Add(new ProxyId(serviceId, methodId), handler);
    }

    public void RegisterProxyReturnHandler(uint serviceId, uint methodId, Action<ReadOnlySpan<byte>, uint, ExtraPacketData> handler)
    {
        ProxyReturnHandlers.Add(new ProxyId(serviceId, methodId), handler);
    }

    public void RegisterMatchNotifyHandler(ServiceMethods.MatchNtf methodId, Action<ReadOnlySpan<byte>, ExtraPacketData> handler)
    {
        NotifyHandlers.Add(new NotifyId((ulong)EServiceId.MatchNtf, (uint)methodId), handler);
    }

    public void RegisterWorldNotifyHandler(ServiceMethods.WorldNtf methodId, Action<ReadOnlySpan<byte>, ExtraPacketData> handler)
    {
        NotifyHandlers.Add(new NotifyId((ulong)EServiceId.WorldNtf, (uint)methodId), handler);
    }

    private void DeviceOnOnPacketArrival(object sender, PacketCapture e)
    {
        var rawPacket = e.GetPacket();

        if (IsDebugCaptureFileMode)
        {
            if (LastDebugCapturePacketTime == DateTime.MinValue)
            {
                LastDebugCapturePacketTime = rawPacket.Timeval.Date;
            }
            else
            {
                TimeSpan timeDiff = rawPacket.Timeval.Date.Subtract(LastDebugCapturePacketTime);
                if (timeDiff > TimeSpan.Zero)
                {
                    System.Threading.Thread.Sleep(timeDiff);
                }

                LastDebugCapturePacketTime = rawPacket.Timeval.Date;
            }
        }

        var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

        var ipv4 = packet?.Extract<IPv4Packet>();
        if (ipv4 == null)
            return;

        var tcpPacket = packet?.Extract<TcpPacket>();
        if (tcpPacket == null)
            return;

        NumSeenPackets++;
        LastPacketSeenAt = DateTime.Now;

        if (tcpPacket.DestinationPort <= 1000 || tcpPacket.SourcePort <= 1000)
            return;

        if (IsDebugCaptureFileMode) {
            TcpReassempler.AddPacket(ipv4, tcpPacket, rawPacket.Timeval);
            return;
        }

        var connId = new ConnectionId(ipv4.SourceAddress.ToString(), tcpPacket.SourcePort, ipv4.DestinationAddress.ToString(), tcpPacket.DestinationPort);
        if (!ConnectionFilters.TryGetValue(connId, out var allowed))
        {
            if (IsFromGame(ipv4, tcpPacket)) {
                ConnectionFilters.TryAdd(connId, true);
            }
            else {
                ConnectionFilters.TryAdd(connId, false);
                return;
            }
        }

        if (!allowed)
            return;

        TcpReassempler.AddPacket(ipv4, tcpPacket, rawPacket.Timeval);
    }

    private void OnNewConnection(TcpReassembler.TcpConnection conn)
    {
        var task = Task.Factory.StartNew(async () =>
        {
            NumConnectionReaders++;
            try
            {
                while (conn.IsAlive && !CancelTokenSrc.IsCancellationRequested && !conn.CancelTokenSrc.IsCancellationRequested)
                {
                    var buff = await conn.Pipe.Reader.ReadAtLeastAsync(6);
                    if (buff.IsCompleted || buff.IsCanceled)
                        break;

                    Span<byte> header = new byte[6];
                    buff.Buffer.Slice(0, 6).CopyTo(header);
                    var len = BinaryPrimitives.ReadUInt32BigEndian(header);
                    var rawMsgType = BinaryPrimitives.ReadInt16BigEndian(header[4..]);
                    var msgType = (rawMsgType & 0x7FFF);
                    conn.Pipe.Reader.AdvanceTo(buff.Buffer.Start);

                    var msgBuff = await conn.Pipe.Reader.ReadAtLeastAsync((int)len);
                    if (msgBuff.IsCompleted || msgBuff.IsCanceled)
                        break;

                    var rawPacket = RawPacketPool.Get();
                    rawPacket.Set((int)len);
                    rawPacket.LastPacketTime = conn.LastPacketTime;
                    msgBuff.Buffer.Slice(0, len).CopyTo(rawPacket.Data.AsSpan()[..(int)len]);
                    RawPacketQueue.Enqueue(rawPacket);
                    conn.Pipe.Reader.AdvanceTo(msgBuff.Buffer.GetPosition(len));
                    NumGameMessagesSeen++;
                }
            }
            catch (Exception ex)
            {
                // この接続の読み取りはここで終わる。黙って終わらせない。
                LogFailureAndContinue($"接続の読み取り {conn.EndPoint}", ex);
            }

            NumConnectionReaders--;
            Log.Logger.Information($"{conn.EndPoint} finished reading");
        }, CancelTokenSrc.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private void ParsePacketsLoop()
    {
        while (!CancelTokenSrc.IsCancellationRequested)
        {
            if (RawPacketQueue.TryDequeue(out var rawPacket))
            {
                try
                {
                    ParsePacket(rawPacket.Data[..rawPacket.Len], rawPacket.LastPacketTime);
                }
                catch (Exception ex)
                {
                    // 最後の砦。処理の中で拾えなかった例外(解析そのものの失敗など)。
                    // ここで投げ直すとこのループごと終わり、以後のパケットが一切処理されなくなる。
                    LogFailureAndContinue("パケットの解析", ex);
                }
                finally
                {
                    rawPacket.Return();
                    RawPacketPool.Return(rawPacket);
                    NumGameMessagesDequeued++;
                }
            }
            else
            {
                Task.Delay(10).Wait();
            }
        }
    }

    private void ParsePacket(ReadOnlySpan<byte> data, DateTime lastPacketTime)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            var msgData = data[offset..];
            if (data.Length < 6)
            {
                return;
            }

            var len = BinaryPrimitives.ReadUInt32BigEndian(msgData);
            var rawMsgType = BinaryPrimitives.ReadInt16BigEndian(msgData[4..]);
            var isCompressed = (rawMsgType & 0x8000) != 0;
            var msgType = (MsgTypeId)(rawMsgType & 0x7FFF);
            var msgPayload = msgData.Slice(6, (int)len - 6);
            offset += (int)len;

            switch (msgType)
            {
                case MsgTypeId.Notify:
                    ParseNotify(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.FrameDown:
                    ParseFrameDown(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.Call:
                    ParseCall(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.Return:
                    ParseReturn(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.FrameUp:
                    ParseFrameUp(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.None:
                case MsgTypeId.Echo:
                case MsgTypeId.UNK1:
                case MsgTypeId.UNK2:
                    break;
                default:
                    Log.Information("Got an unknown message type: {msgType}", msgType);
                    break;
            }
        }
    }

    private void ParseFrameDown(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        var seqNum = BinaryPrimitives.ReadUInt32BigEndian(data);

        if (isCompressed)
        {
            var decompressed = Decompress(data[4..]);
            if (!decompressed.IsEmpty)
            {
                ParsePacket(decompressed, lastPacketTime);
            }
        }
        else
        {
            ParsePacket(data[4..], lastPacketTime);
        }
    }

    private void ParseNotify(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        var serviceUuid = BinaryPrimitives.ReadUInt64BigEndian(data);
        var stubId = BinaryPrimitives.ReadUInt32BigEndian(data[8..]);
        var methodId = BinaryPrimitives.ReadUInt32BigEndian(data[12..]);

        var msgData = data[16..];
        if (isCompressed)
        {
            msgData = Decompress(msgData);

            if (msgData.IsEmpty)
            {
                Log.Logger.Warning("Error decompressing data for {serviceUuid}, {stubId}, {methodId}", serviceUuid, stubId, methodId);
                return;
            }
        }

        if (!Enum.IsDefined(typeof(EServiceId), serviceUuid))
        {
            Log.Logger.Information($"Unknown ServiceId = {serviceUuid} MethodId = {methodId}");
        }

        var id = new NotifyId(serviceUuid, methodId);
        var hasNotifyHandler = NotifyHandlers.TryGetValue(id, out var handler);
        var isNotifyAllowed = NotifyGate is null || NotifyGate(serviceUuid, methodId);
        if (hasNotifyHandler && isNotifyAllowed)
        {
            var extraData = new ExtraPacketData(lastPacketTime);
            try
            {
                handler!(msgData, extraData);
            }
            catch (Exception ex)
            {
                LogFailureAndContinue($"通知 service={serviceUuid} method={methodId}", ex);
            }
        }

    }


    private void ParseCall(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        if (data.Length < 20)
        {
            return;
        }

        var proxyServiceId = BinaryPrimitives.ReadUInt64BigEndian(data);
        var returnUid = BinaryPrimitives.ReadUInt32BigEndian(data[12..]);
        var proxyMethodId = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        var msgData = data[20..];

        var id = new ProxyId((uint)proxyServiceId, proxyMethodId);
        ProxyReturnsDictionary.AddOrUpdate(returnUid, id, (_, _) => id);
        DispatchProxyCall(id, msgData, returnUid, lastPacketTime);
    }

    private void ParseFrameUp(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        if (data.Length < 4)
        {
            return;
        }

        var offset = 4;
        while (offset < data.Length)
        {
            if (data.Length - offset < 18)
            {
                return;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            if (length < 18
                || length > int.MaxValue
                || length > data.Length - offset)
            {
                return;
            }

            var endPos = offset + (int)length;
            if (endPos > data.Length)
            {
                return;
            }

            offset += 4;
            var flags = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            offset += 2;
            offset += 4;
            var proxyServiceId = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            offset += 4;

            uint returnUid;
            uint proxyMethodId;
            if (flags == 2)
            {
                if (endPos - offset < 8)
                {
                    return;
                }

                returnUid = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                offset += 4;
                proxyMethodId = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                offset += 4;
            }
            else
            {
                if (endPos - offset < 12)
                {
                    return;
                }

                offset += 4;
                returnUid = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                offset += 4;
                proxyMethodId = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                offset += 4;
            }

            var msgData = data[offset..endPos];
            var id = new ProxyId(proxyServiceId, proxyMethodId);
            if (flags != 2)
            {
                ProxyReturnsDictionary.AddOrUpdate(returnUid, id, (_, _) => id);
            }

            DispatchProxyCall(id, msgData, returnUid, lastPacketTime);
            offset = endPos;
        }
    }

    private void ParseReturn(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        if (data.Length < 12)
        {
            return;
        }

        var returnUid = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        var msgData = data[12..];
        if (isCompressed)
        {
            msgData = Decompress(msgData);
            if (msgData.IsEmpty)
            {
                return;
            }
        }

        var protoStart = 0;
        var searchLength = Math.Min(msgData.Length, 4);
        for (var index = 0; index < searchLength; index++)
        {
            if (msgData[index] == 0x0A)
            {
                protoStart = index;
            }
        }

        if (!ProxyReturnsDictionary.TryRemove(returnUid, out var id))
        {
            return;
        }

        var hasReturnHandler = ProxyReturnHandlers.TryGetValue(id, out var handler);
        var isReturnAllowed = ProxyGate is null || ProxyGate(id.ServiceId, id.MethodId);
        var finalData = msgData[protoStart..];
        var extraData = new ExtraPacketData(lastPacketTime);
        if (hasReturnHandler && isReturnAllowed)
        {
            try
            {
                handler!(finalData, returnUid, extraData);
            }
            catch (Exception ex)
            {
                LogFailureAndContinue($"応答 service={id.ServiceId} method={id.MethodId}", ex);
            }
        }
    }

    private void DispatchProxyCall(
        ProxyId id,
        ReadOnlySpan<byte> data,
        uint returnUid,
        DateTime lastPacketTime)
    {
        var extraData = new ExtraPacketData(lastPacketTime);
        var hasProxyHandler = ProxyHandlers.TryGetValue(id, out var handler);
        var isProxyAllowed = ProxyGate is null || ProxyGate(id.ServiceId, id.MethodId);
        if (hasProxyHandler && isProxyAllowed)
        {
            try
            {
                handler!(data, returnUid, extraData);
            }
            catch (Exception ex)
            {
                LogFailureAndContinue($"要求 service={id.ServiceId} method={id.MethodId}", ex);
            }
        }
    }

    /// <summary>
    /// 処理の途中で出た例外を、見える形で書く。<b>その1件だけ捨てて、処理は続ける。</b>
    ///
    /// <para>
    /// 投げ直すとパケット処理のタスクがそこで終わり、以後の通知が一切処理されなくなる。
    /// 表示が更新されなくなるのに、ログには何も残らない(実際にそうなっていた)。
    /// </para>
    ///
    /// <para>
    /// 握り潰しにはしない。<c>Log.Error</c> はアプリのログ画面(<c>ManagerLogSink</c>)に出る。
    /// ログが埋まらないよう、同じ場所・同じ種類の例外は最初の1件の後は <see cref="FailureLogInterval"/> に1件だけ書き、
    /// その間の件数を添える。
    /// </para>
    /// </summary>
    private void LogFailureAndContinue(string where, Exception exception)
    {
        var key = where + "|" + exception.GetType().FullName;
        int suppressed;
        lock (FailureLogSync)
        {
            var now = DateTime.Now;
            if (FailureLogState.TryGetValue(key, out var state))
            {
                if (now - state.LastLogged < FailureLogInterval)
                {
                    FailureLogState[key] = (state.LastLogged, state.Suppressed + 1);
                    return;
                }

                suppressed = state.Suppressed;
            }
            else
            {
                suppressed = 0;
            }

            FailureLogState[key] = (now, 0);
        }

        if (suppressed > 0)
        {
            Log.Error(exception, "Exception in {Where}. Dropping this one and continuing ({Suppressed} identical exceptions since the last log)", where, suppressed);
        }
        else
        {
            Log.Error(exception, "Exception in {Where}. Dropping this one and continuing", where);
        }
    }

    private ReadOnlySpan<byte> Decompress(ReadOnlySpan<byte> data)
    {
        try
        {
            var decompressedLen = _decompressor.Unwrap(data, DecompressionScratchBuffer.AsSpan());
            return DecompressionScratchBuffer.AsSpan()[..decompressedLen];
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error decompressing data of Len: {Len}, DecompressionScratchBuffer Size: {ScratchSize}", data.Length, DecompressionScratchBuffer.Length);
            return [];
        }
    }

    private bool IsFromGame(IPv4Packet ip, TcpPacket tcp)
    {
        var sw = Stopwatch.StartNew();
        var conns = Utils.GetTCPConnectionsForExe(Config.ExeNames);
        var isGameConnection = conns.Any((x =>
            (x.LocalAddress == ip.SourceAddress.ToString() && x.LocalPort == tcp.SourcePort) ||
            (x.RemoteAddress == ip.SourceAddress.ToString() && x.RemotePort == tcp.SourcePort) ||
            (x.LocalAddress == ip.DestinationAddress.ToString() && x.LocalPort == tcp.DestinationPort) ||
            (x.RemoteAddress == ip.DestinationAddress.ToString() && x.RemotePort == tcp.DestinationPort)));

        sw.Stop();
        Log.Logger.Debug($"Checking {ip.SourceAddress}:{tcp.SourcePort} > {ip.DestinationAddress}:{tcp.DestinationPort} is game connection: {isGameConnection}, took {sw.ElapsedMilliseconds}ms");

        return isGameConnection;
    }

    public void Stop()
    {
        CancelTokenSrc.Cancel();

        if (CaptureDevice != null)
        {
            CaptureDevice.StopCapture();
            CaptureDevice.Close();
            ConnectionFilters.Clear();

            Log.Information("Capture device stopped");
        }
    }

    public void PrintCaptureDevices()
    {
        var devices = CaptureDeviceList.Instance;
        foreach (var liveDevice in devices)
        {
            var dev = (LibPcapLiveDevice)liveDevice;
            Log.Information("Device: {DeviceName}, {FriendlyName}", dev.Name, dev.Interface?.FriendlyName);
        }
    }

    private ICaptureDevice GetCaptureDevice()
    {
        var devices = CaptureDeviceList.Instance;

        try
        {
            foreach (var liveDevice in devices)
            {
                var dev = (LibPcapLiveDevice)liveDevice;
                if (dev.Name == Config.CaptureDeviceName)
                {
                    Log.Information("Matched capture device: {DeviceName}, {FriendlyName}", dev.Name, dev.Interface?.FriendlyName);
                    return dev;
                }
            }

            Log.Information("No matched capture device, trying to find Ethernet");
            var ethernet = devices.FirstOrDefault(x => ((LibPcapLiveDevice)x).Interface?.FriendlyName == "Ethernet");
            if (ethernet != null)
            {
                Log.Information("Found Ethernet named capture device, using it: {DeviceName}, {FriendlyName}", ethernet.Name, ((LibPcapLiveDevice)ethernet).Interface?.FriendlyName);
                return ethernet;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error getting capture device");
            throw;
        }

        var device = devices[0];
        Log.Information("No matched capture device, using first found: {DeviceName}, {FriendlyName}", device.Name, ((LibPcapLiveDevice)device).Interface?.FriendlyName);
        return device;
    }

    public string GetFilterString(IEnumerable<TcpHelper.TcpRow> conns)
    {
        var connLines = conns.DistinctBy(x => x.RemoteAddress).Select(x => $"(tcp and src host {x.RemoteAddress} or dst host {x.RemoteAddress})");
        var filterStr = string.Join(" or ", connLines);
        return filterStr;
    }
}

public class PendingConnState(IPAddress addr)
{
    public IPAddress IPAddress { get; set; } = addr;
    public DateTime FirstSeenAt { get; set; } = DateTime.Now;
    public bool? IsGameConnection = null;
}
