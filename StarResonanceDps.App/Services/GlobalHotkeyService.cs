using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Serilog;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>登録できなかったホットキー。<see cref="ErrorCode"/> は Windows のエラー番号。</summary>
public sealed record HotkeyRegistrationFailure(HotkeyAction Action, HotkeyBindingConfig Binding, int ErrorCode)
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    /// <summary>ほかのアプリ(または Windows)が同じキーを登録している。</summary>
    public bool IsInUse => ErrorCode == ErrorHotkeyAlreadyRegistered;
}

/// <summary>
/// アプリ全体のホットキー。<c>RegisterHotKey</c> で登録し、見えないメッセージ専用の窓で受ける。
///
/// <para>
/// 登録したキーはこのアプリが受け取り、ほかのアプリ(ゲームを含む)には届かない。
/// 押しっぱなしの連打は数えない(<c>MOD_NOREPEAT</c>)。
/// </para>
///
/// <para>
/// 全体設定でキーを受け付けている間は、全部を一時的に外す(<see cref="Suspend"/> / <see cref="Resume"/>)。
/// 登録したままだと、押したキーがホットキーとして動いて欄に届かない。
/// </para>
///
/// <para>
/// 呼ぶのは UI スレッドだけ。押されたことの通知(<see cref="Pressed"/>)も UI スレッドで上がる。
/// </para>
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private static readonly Lazy<GlobalHotkeyService> LazyInstance = new(() => new GlobalHotkeyService());

    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    /// <summary>試しの登録に使う番号。アプリが使える番号の上限(0xBFFF)。操作の番号(1〜)とぶつからない。</summary>
    private const int TestHotkeyId = 0xBFFF;

    /// <summary>メッセージ専用の窓を作るときの親(HWND_MESSAGE)。</summary>
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private readonly HashSet<HotkeyAction> _registeredActions = [];
    private HotkeySettingsConfig _bindings = new();
    private HwndSource? _source;
    private bool _isSuspended;

    private GlobalHotkeyService()
    {
    }

    public static GlobalHotkeyService Instance => LazyInstance.Value;

    /// <summary>登録したキーが押された。</summary>
    public event Action<HotkeyAction>? Pressed;

    /// <summary>登録の状態が変わった(登録・解除・一時停止・再開)。</summary>
    public event EventHandler? RegistrationChanged;

    public bool IsRegistered(HotkeyAction action)
    {
        return _registeredActions.Contains(action);
    }

    public HotkeyBindingConfig GetBinding(HotkeyAction action)
    {
        return _bindings.Get(action);
    }

    /// <summary>割り当てを入れ替えて登録し直す。一時停止中なら控えるだけで、再開したときに登録する。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> Apply(HotkeySettingsConfig bindings)
    {
        _bindings = bindings.Clone();
        UnregisterAll();
        IReadOnlyList<HotkeyRegistrationFailure> failures = _isSuspended
            ? Array.Empty<HotkeyRegistrationFailure>()
            : RegisterAll();
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
        return failures;
    }

    public void Suspend()
    {
        if (_isSuspended)
        {
            return;
        }

        _isSuspended = true;
        UnregisterAll();
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<HotkeyRegistrationFailure> Resume()
    {
        if (!_isSuspended)
        {
            return [];
        }

        _isSuspended = false;
        var failures = RegisterAll();
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
        return failures;
    }

    /// <summary>
    /// そのキーを登録できるか試す。できたらすぐ外す。できなければ Windows のエラー番号を返す。
    /// 自分の登録とぶつからないよう、一時停止中に呼ぶこと。
    /// </summary>
    public int? TestRegistration(HotkeyBindingConfig binding)
    {
        var handle = EnsureHandle();
        if (!RegisterHotKey(handle, TestHotkeyId, ToNativeModifiers(binding.Modifiers), ToVirtualKey(binding.Key)))
        {
            var error = Marshal.GetLastWin32Error();
            Log.Warning("Hotkey {Hotkey} cannot be registered: Win32 error {Error}", HotkeyText.Format(binding), error);
            return error;
        }

        UnregisterHotKey(handle, TestHotkeyId);
        return null;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }

    private List<HotkeyRegistrationFailure> RegisterAll()
    {
        var failures = new List<HotkeyRegistrationFailure>();
        var handle = EnsureHandle();

        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var binding = _bindings.Get(action);
            if (!binding.IsAssigned)
            {
                continue;
            }

            if (RegisterHotKey(handle, ToHotkeyId(action), ToNativeModifiers(binding.Modifiers), ToVirtualKey(binding.Key)))
            {
                _registeredActions.Add(action);
                continue;
            }

            var error = Marshal.GetLastWin32Error();
            Log.Warning(
                "Failed to register hotkey {Action} ({Hotkey}): Win32 error {Error}",
                action,
                HotkeyText.Format(binding),
                error);
            failures.Add(new HotkeyRegistrationFailure(action, binding.Clone(), error));
        }

        return failures;
    }

    private void UnregisterAll()
    {
        if (_source is null)
        {
            _registeredActions.Clear();
            return;
        }

        foreach (var action in _registeredActions)
        {
            UnregisterHotKey(_source.Handle, ToHotkeyId(action));
        }

        _registeredActions.Clear();
    }

    private IntPtr EnsureHandle()
    {
        if (_source is null)
        {
            _source = new HwndSource(new HwndSourceParameters("StarResonanceDpsHotkeys")
            {
                ParentWindow = MessageOnlyParent,
                WindowStyle = 0
            });
            _source.AddHook(WndProc);
        }

        return _source.Handle;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        var id = wParam.ToInt32();
        foreach (var action in _registeredActions)
        {
            if (ToHotkeyId(action) != id)
            {
                continue;
            }

            handled = true;
            Pressed?.Invoke(action);
            break;
        }

        return IntPtr.Zero;
    }

    private static int ToHotkeyId(HotkeyAction action)
    {
        return (int)action + 1;
    }

    private static uint ToVirtualKey(Key key)
    {
        return (uint)KeyInterop.VirtualKeyFromKey(key);
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        var native = ModNoRepeat;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            native |= ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            native |= ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            native |= ModShift;
        }

        return native;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
