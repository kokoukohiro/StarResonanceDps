using System.Windows.Input;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>登録できなかったホットキー。<see cref="ErrorCode"/> は Windows のエラー番号。</summary>
public sealed record HotkeyRegistrationFailure(HotkeyAction Action, HotkeyBindingConfig Binding, int ErrorCode)
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    /// <summary>ほかのアプリ(または Windows)が同じキーを登録している。</summary>
    public bool IsInUse => ErrorCode == ErrorHotkeyAlreadyRegistered;
}

/// <summary>
/// アプリ全体のホットキー。割り当てを決め、キーを受けるのは補助(<see cref="HelperHotkeyRegistrar"/>)に任せる。
///
/// <para>
/// 割り当てたキーは、ゲームかこのアプリが前面のときだけ受け取り、ゲームには渡さない。
/// それ以外のアプリが前面なら何もせず、キーはそのアプリに届く(受け方は <see cref="StarResonanceDps.HotkeyHost.KeyboardHotkeyHook"/>)。
/// ゲームとして扱うのは、今の「ゲームの種類」の設定から決まるプロセス名(キャプチャと同じ)。
/// ほかのアプリが同じキーを登録していれば割り当てない(失敗として返す)。押しっぱなしの繰り返しは数えない。
/// </para>
///
/// <para>
/// アプリは通常権限で動き、管理者権限で動くのはキーを受ける補助だけ。
/// ゲームが管理者権限で動くと、通常権限ではゲームが前面の間キーを受けられないため。
/// 補助は割り当てのあるキーを初めて足すときに起動する(割り当てが無ければ起動しない)。
/// 起動できない・途中で使えなくなったときは、このプロセスの中で受けるようにし(<see cref="LocalHotkeyRegistrar"/>)、
/// <see cref="HelperUnavailable"/> で知らせる。その回の実行中は補助に戻らない。
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

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    /// <summary>試しの登録に使う番号。アプリが使える番号の上限(0xBFFF)。操作の番号(1〜)とぶつからない。</summary>
    private const int TestHotkeyId = 0xBFFF;

    private readonly HashSet<HotkeyAction> _registeredActions = [];
    private HotkeySettingsConfig _bindings = new();
    private IReadOnlyList<string> _gameProcessNames = [];
    private IHotkeyRegistrar? _registrar;
    private bool _isHelperUnavailable;
    private bool _isSuspended;

    private GlobalHotkeyService()
    {
    }

    public static GlobalHotkeyService Instance => LazyInstance.Value;

    /// <summary>登録したキーが押された。</summary>
    public event Action<HotkeyAction>? Pressed;

    /// <summary>登録の状態が変わった(登録・解除・一時停止・再開)。</summary>
    public event EventHandler? RegistrationChanged;

    /// <summary>
    /// 補助が使えず、このプロセスの中で登録するようにした。一覧はそのとき登録し直せなかったキー
    /// (呼び出しの戻り値で返す場面では空)。UI スレッドで後から上がる。
    /// </summary>
    public event Action<IReadOnlyList<HotkeyRegistrationFailure>>? HelperUnavailable;

    public bool IsRegistered(HotkeyAction action)
    {
        return _registeredActions.Contains(action);
    }

    public HotkeyBindingConfig GetBinding(HotkeyAction action)
    {
        return _bindings.Get(action);
    }

    /// <summary>
    /// 割り当てを入れ替えて登録し直す。一時停止中なら控えるだけで、再開したときに登録する。
    /// ゲームのプロセス名もここで今の設定から取り直す(全体設定の保存では、ゲームの種類がこれより先に反映される)。
    /// </summary>
    public IReadOnlyList<HotkeyRegistrationFailure> Apply(HotkeySettingsConfig bindings)
    {
        _bindings = bindings.Clone();
        _gameProcessNames = Utils.GameCapturePreferenceToExeNames(CombatRuntimeSettings.GameCapturePreference);
        var failures = RegisterAgain();
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
        try
        {
            UnregisterAll();
        }
        catch (HotkeyHelperException exception)
        {
            NotifyHelperUnavailable(FallBackToLocal(exception));
        }

        RegistrationChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<HotkeyRegistrationFailure> Resume()
    {
        if (!_isSuspended)
        {
            return [];
        }

        _isSuspended = false;
        var failures = RegisterAgain();
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
        return failures;
    }

    /// <summary>
    /// そのキーを割り当てられるか(ほかのアプリが同じキーを登録していないか)試す。できたらすぐ外す。
    /// できなければ Windows のエラー番号を返す。
    /// </summary>
    public int? TestRegistration(HotkeyBindingConfig binding)
    {
        try
        {
            return Test(binding);
        }
        catch (HotkeyHelperException exception)
        {
            var failures = FallBackToLocal(exception);
            RegistrationChanged?.Invoke(this, EventArgs.Empty);
            NotifyHelperUnavailable(failures);
            return Test(binding);
        }
    }

    /// <summary>登録を全部外す。補助は接続を閉じると終わり、補助が登録したキーもそれで外れる。</summary>
    public void Dispose()
    {
        _registeredActions.Clear();
        DisposeRegistrar();
    }

    // 全部外して、止めていなければ全部登録し直す。補助が途中で使えなくなったら、このプロセスの中で登録し直す。
    private IReadOnlyList<HotkeyRegistrationFailure> RegisterAgain()
    {
        try
        {
            UnregisterAll();
            _registrar?.SetGameProcessNames(_gameProcessNames);
            if (_isSuspended)
            {
                return Array.Empty<HotkeyRegistrationFailure>();
            }

            return RegisterAll();
        }
        catch (HotkeyHelperException exception)
        {
            var failures = FallBackToLocal(exception);
            NotifyHelperUnavailable(Array.Empty<HotkeyRegistrationFailure>());
            return failures;
        }
    }

    private List<HotkeyRegistrationFailure> RegisterAll()
    {
        var failures = new List<HotkeyRegistrationFailure>();

        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var binding = _bindings.Get(action);
            if (!binding.IsAssigned)
            {
                continue;
            }

            var error = EnsureRegistrar().Register(
                ToHotkeyId(action),
                ToNativeModifiers(binding.Modifiers),
                ToVirtualKey(binding.Key));
            if (error == 0)
            {
                _registeredActions.Add(action);
                continue;
            }

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
        if (_registrar is not null)
        {
            foreach (var action in _registeredActions)
            {
                _registrar.Unregister(ToHotkeyId(action));
            }
        }

        _registeredActions.Clear();
    }

    private int? Test(HotkeyBindingConfig binding)
    {
        var registrar = EnsureRegistrar();
        var error = registrar.Register(TestHotkeyId, ToNativeModifiers(binding.Modifiers), ToVirtualKey(binding.Key));
        if (error != 0)
        {
            Log.Warning("Hotkey {Hotkey} cannot be registered: Win32 error {Error}", HotkeyText.Format(binding), error);
            return error;
        }

        registrar.Unregister(TestHotkeyId);
        return null;
    }

    // 登録の役を用意する。最初は補助を起動し、起動できなければ(その回の実行中はずっと)このプロセスの中で登録する。
    private IHotkeyRegistrar EnsureRegistrar()
    {
        if (_registrar is not null)
        {
            return _registrar;
        }

        if (!_isHelperUnavailable)
        {
            HelperHotkeyRegistrar? helper = null;
            try
            {
                helper = HelperHotkeyRegistrar.Start();
            }
            catch (HotkeyHelperException exception)
            {
                if (exception.WasDeclined)
                {
                    Log.Warning(exception, "Hotkey helper was not started (elevation was declined); registering hotkeys in this process");
                }
                else
                {
                    Log.Error(exception, "Could not start the hotkey helper; registering hotkeys in this process");
                }

                _isHelperUnavailable = true;
                NotifyHelperUnavailable(Array.Empty<HotkeyRegistrationFailure>());
            }

            if (helper is not null)
            {
                helper.Pressed += Registrar_Pressed;
                helper.Lost += Helper_Lost;
                _registrar = helper;
                // ここで補助が切れたら、呼び出し元がこのプロセスの中へ切り替える(_registrar に入れてから渡すため)。
                helper.SetGameProcessNames(_gameProcessNames);
                return helper;
            }
        }

        var local = new LocalHotkeyRegistrar();
        local.Pressed += Registrar_Pressed;
        _registrar = local;
        local.SetGameProcessNames(_gameProcessNames);
        return local;
    }

    // 補助が使えなくなった。このプロセスの中へ切り替え、止めていなければ今の割り当てを全部登録し直す。
    private List<HotkeyRegistrationFailure> FallBackToLocal(HotkeyHelperException exception)
    {
        int? exitCode = null;
        if (_registrar is HelperHotkeyRegistrar helper)
        {
            helper.Pressed -= Registrar_Pressed;
            helper.Lost -= Helper_Lost;
            exitCode = helper.DisposeAndGetExitCode();
            _registrar = null;
        }

        Log.Error(exception, "Lost the hotkey helper (exit code {ExitCode}); registering hotkeys in this process", exitCode);
        _registeredActions.Clear();
        _isHelperUnavailable = true;

        if (_isSuspended)
        {
            return [];
        }

        return RegisterAll();
    }

    // 命令の合間に補助が切れた(補助が落ちたなど)。
    private void Helper_Lost(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _registrar) || sender is not HelperHotkeyRegistrar helper)
        {
            return;
        }

        var failures = FallBackToLocal(new HotkeyHelperException("The hotkey helper closed the connection", helper.ReadFailure));
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
        NotifyHelperUnavailable(failures);
    }

    private void Registrar_Pressed(int id)
    {
        foreach (var action in _registeredActions)
        {
            if (ToHotkeyId(action) != id)
            {
                continue;
            }

            Pressed?.Invoke(action);
            break;
        }
    }

    // 登録の途中で知らせの窓を出さないよう、後から上げる。
    private void NotifyHelperUnavailable(IReadOnlyList<HotkeyRegistrationFailure> failures)
    {
        Dispatcher.CurrentDispatcher.InvokeAsync(() => HelperUnavailable?.Invoke(failures));
    }

    private void DisposeRegistrar()
    {
        if (_registrar is null)
        {
            return;
        }

        _registrar.Pressed -= Registrar_Pressed;
        if (_registrar is HelperHotkeyRegistrar helper)
        {
            helper.Lost -= Helper_Lost;
        }

        _registrar.Dispose();
        _registrar = null;
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
}
