using System.Windows.Threading;
using StarResonanceDps.HotkeyHost;

namespace StarResonanceDps.App.Services;

/// <summary>
/// このプロセスの中でキーを受ける。補助(<see cref="HelperHotkeyRegistrar"/>)が使えないときに使う。
/// フック(<see cref="KeyboardHotkeyHook"/>)は専用のスレッドで掛ける。UI スレッドで掛けると、画面の処理が重いときに PC 全体のキー入力が遅れる。
/// 権限はこのアプリと同じなので、ゲームが管理者権限で動くと、ゲームが前面の間はキーが届かない。
/// </summary>
internal sealed class LocalHotkeyRegistrar : IHotkeyRegistrar
{
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;
    private readonly Dispatcher _hookDispatcher;
    private readonly KeyboardHotkeyHook _hook;

    public LocalHotkeyRegistrar()
    {
        Dispatcher? hookDispatcher = null;
        using (var started = new ManualResetEventSlim())
        {
            var thread = new Thread(() =>
            {
                hookDispatcher = Dispatcher.CurrentDispatcher;
                started.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "Hotkey.LocalHook"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            started.Wait();
        }

        _hookDispatcher = hookDispatcher!;
        _hook = _hookDispatcher.Invoke(() => new KeyboardHotkeyHook(Environment.ProcessId, OnPressed));
    }

    public event Action<int>? Pressed;

    public int Register(int id, uint modifiers, uint virtualKey)
    {
        return _hookDispatcher.Invoke(() => _hook.Register(id, modifiers, virtualKey));
    }

    public void Unregister(int id)
    {
        _hookDispatcher.Invoke(() => _hook.Unregister(id));
    }

    public void SetGameProcessNames(IReadOnlyList<string> names)
    {
        _hookDispatcher.Invoke(() => _hook.SetGameProcessNames(names));
    }

    public void Dispose()
    {
        _hookDispatcher.Invoke(_hook.Dispose);
        _hookDispatcher.InvokeShutdown();
    }

    // フックの中から呼ばれる。UI スレッドへ後から渡す。
    private void OnPressed(int id)
    {
        _uiDispatcher.InvokeAsync(() => Pressed?.Invoke(id));
    }
}
