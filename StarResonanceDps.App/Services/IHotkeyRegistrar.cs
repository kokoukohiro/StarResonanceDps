namespace StarResonanceDps.App.Services;

/// <summary>
/// ホットキーを受ける役。<see cref="GlobalHotkeyService"/> が割り当てを決め、キーを受けるのはこれに任せる。
/// 番号・修飾(<c>MOD_*</c>)・仮想キーは <c>RegisterHotKey</c> と同じ値。
/// 受けるのはゲームかこのアプリが前面のときだけ(<see cref="StarResonanceDps.HotkeyHost.KeyboardHotkeyHook"/>)。
/// </summary>
internal interface IHotkeyRegistrar : IDisposable
{
    /// <summary>キーが押された。番号は割り当てたときのもの。UI スレッドで上がる。</summary>
    event Action<int>? Pressed;

    /// <summary>割り当てを足す。足せたら 0、足せなければ Windows のエラー番号(ほかのアプリが同じキーを登録しているときなど)。</summary>
    int Register(int id, uint modifiers, uint virtualKey);

    void Unregister(int id);

    /// <summary>ゲームとして扱うプロセス名(拡張子なし)。</summary>
    void SetGameProcessNames(IReadOnlyList<string> names);
}
