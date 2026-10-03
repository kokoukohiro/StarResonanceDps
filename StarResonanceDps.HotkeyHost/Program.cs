using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;

namespace StarResonanceDps.HotkeyHost;

/// <summary>
/// ホットキーを受ける補助。管理者権限で動き、本体(通常権限)が開いた名前付きパイプにつなぐ。
/// 割り当ての判断は本体がし、ここは言われた割り当てをキーボードのフック(<see cref="KeyboardHotkeyHook"/>)で受けて、押されたら知らせる。
/// パイプが閉じたら(本体が終わったら)終わる。
/// ログは書かない。失敗は終了コード(<see cref="HotkeyHostProtocol"/>)とエラー番号の返事で本体に渡る。
/// </summary>
internal static class Program
{
    private const int ConnectTimeoutMilliseconds = 10000;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2
            || !uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var serverProcessId))
        {
            return HotkeyHostProtocol.ExitInvalidArguments;
        }

        // CurrentUserOnly は付けない。付けるとサーバーの持ち主を自分のトークンの既定の持ち主と比べ、
        // 管理者権限のトークン(既定の持ち主は Administrators)では通常権限の本体のパイプにつながらない。
        // つなぎ先は下でサーバーのプロセスIDを見て確かめる。
        using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            pipe.Connect(ConnectTimeoutMilliseconds);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return HotkeyHostProtocol.ExitConnectFailed;
        }

        // 同じ名前のパイプを先に作った別のプロセスにつながっていないか。
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var actualServerProcessId)
            || actualServerProcessId != serverProcessId)
        {
            return HotkeyHostProtocol.ExitUnexpectedServer;
        }

        // 本体の窓が前面のときもホットキーを受けるので、本体のプロセスIDを渡す。
        return new HotkeyHostSession(pipe, (int)serverProcessId).Run();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
}

/// <summary>
/// つながった後の受け答え。命令はスレッドで読み、フックと割り当ては Dispatcher のスレッドで扱う(フックはこのスレッドで呼ばれる)。
/// パイプは非同期で開くこと(同期のハンドルでは、読み取りの待ちが書き込みを止める)。
/// </summary>
internal sealed class HotkeyHostSession
{
    private readonly NamedPipeClientStream _pipe;
    private readonly StreamWriter _writer;
    private readonly object _writeLock = new();
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly KeyboardHotkeyHook _hook;
    private readonly List<string> _gameProcessNames = [];
    private int _exitCode;

    public HotkeyHostSession(NamedPipeClientStream pipe, int ownerProcessId)
    {
        _pipe = pipe;
        _writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
        _hook = new KeyboardHotkeyHook(ownerProcessId, OnPressed);
    }

    public int Run()
    {
        new Thread(ReadCommands)
        {
            IsBackground = true,
            Name = "HotkeyHost.Reader"
        }.Start();

        Dispatcher.Run();

        _hook.Dispose();
        return _exitCode;
    }

    private void ReadCommands()
    {
        using var reader = new StreamReader(_pipe, new UTF8Encoding(false), false, 256, leaveOpen: true);
        try
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var command = line;
                var error = _dispatcher.Invoke(() => Execute(command));
                if (error is null)
                {
                    _exitCode = HotkeyHostProtocol.ExitInvalidCommand;
                    break;
                }

                Write($"{HotkeyHostProtocol.Reply} {error.Value.ToString(CultureInfo.InvariantCulture)}");
            }
        }
        catch (IOException)
        {
            // 本体との接続が切れた(本体が落ちたとき)。閉じたときと同じく終わる。
        }

        _dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
    }

    /// <summary>命令を実行し、Windows のエラー番号(0 は成功)を返す。読めない命令なら null。</summary>
    private int? Execute(string line)
    {
        if (line == HotkeyHostProtocol.ClearGameProcessNames)
        {
            _gameProcessNames.Clear();
            _hook.SetGameProcessNames(_gameProcessNames);
            return 0;
        }

        var addGameProcessNamePrefix = HotkeyHostProtocol.AddGameProcessName + " ";
        if (line.StartsWith(addGameProcessNamePrefix, StringComparison.Ordinal))
        {
            _gameProcessNames.Add(line[addGameProcessNamePrefix.Length..]);
            _hook.SetGameProcessNames(_gameProcessNames);
            return 0;
        }

        var parts = line.Split(' ');
        if (parts[0] == HotkeyHostProtocol.Register
            && parts.Length == 4
            && TryParseNumber(parts[1], out var registerId)
            && TryParseNumber(parts[2], out var modifiers)
            && TryParseNumber(parts[3], out var virtualKey))
        {
            return _hook.Register((int)registerId, modifiers, virtualKey);
        }

        if (parts[0] == HotkeyHostProtocol.Unregister
            && parts.Length == 2
            && TryParseNumber(parts[1], out var unregisterId))
        {
            _hook.Unregister((int)unregisterId);
            return 0;
        }

        return null;
    }

    // フックの中から呼ばれる。書き込みはフックを抜けてから行う。
    private void OnPressed(int id)
    {
        _dispatcher.InvokeAsync(() => Write($"{HotkeyHostProtocol.Pressed} {id.ToString(CultureInfo.InvariantCulture)}"));
    }

    private void Write(string line)
    {
        try
        {
            lock (_writeLock)
            {
                _writer.WriteLine(line);
            }
        }
        catch (IOException)
        {
            // 本体との接続が切れた。読み取りの側も同じく終わるが、こちらからも止める。
            _dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        }
    }

    private static bool TryParseNumber(string text, out uint value)
    {
        return uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
