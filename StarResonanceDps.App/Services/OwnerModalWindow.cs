using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace StarResonanceDps.App.Services;

/// <summary>
/// オーナーだけを止めるモーダル表示。
///
/// <para>
/// <see cref="Window.ShowDialog"/> は<b>アプリケーション モーダル</b>で、アプリ内の全ウィンドウの
/// 入力を止めてしまう。設定ウィンドウを開いている間もウィジェットは操作したいので、
/// 止める範囲をオーナー1枚に限定する。
/// </para>
///
/// <para>
/// 無効化に <c>IsEnabled</c> を使わないのは、WPF の無効状態が視覚ツリー全体に伝わって
/// <b>オーナーの中身が灰色に沈む</b>ため(このプロジェクトのスタイルは <c>IsEnabled=False</c> で
/// 不透明度を落とす)。<c>EnableWindow</c> は入力だけを止めるので見た目が変わらない。
/// モーダルダイアログが内部でやっているのと同じこと。
/// </para>
/// </summary>
public static class OwnerModalWindow
{
    /// <param name="onClosed">
    /// 閉じた後に走らせる処理。<c>ShowDialog</c> と違って呼び出しは待たないので、
    /// 結果を使う続きはここに渡す。
    /// </param>
    public static void Show(Window window, Window? owner, Action? onClosed = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        var ownerHandle = owner is null
            ? IntPtr.Zero
            : new WindowInteropHelper(owner).Handle;

        if (ownerHandle != IntPtr.Zero)
        {
            // <b>閉じると決まった瞬間(窓が消える前)にオーナーを戻す。</b>ShowDialog が閉じるときと同じ順番。
            // 消えた後に戻すと、消える瞬間はオーナーが止まったままで選ばれず、Windows が別のアプリを前面にする。
            // 前面を移すのは閉じる窓が前面だったときだけ(別のアプリへ切り替えてから閉じたときは奪わない)。
            // Closing が届くのは閉じると決まったときだけという前提(各窓は取り消すとき base.OnClosing を呼ばずに戻る)。
            window.Closing += (_, e) =>
            {
                if (e.Cancel)
                {
                    return;
                }

                var wasActive = window.IsActive;
                EnableWindow(ownerHandle, true);
                if (wasActive)
                {
                    owner!.Activate();
                }
            };

            // 閉じ始めを通らずに閉じたとき(オーナーが閉じる・アプリの終了で、取り消しを無視して閉じられた)の戻し。
            // 止めたオーナーは、実際に閉じるたびに必ず戻す。
            window.Closed += (_, _) => EnableWindow(ownerHandle, true);
        }

        if (onClosed is not null)
        {
            window.Closed += (_, _) => onClosed();
        }

        window.Show();

        // <b>止めるのは表示の後。</b>アクティブなまま無効化すると、無効なウィンドウは
        // アクティブでいられないので、Windows がアクティブ化を次のウィンドウ(下にある
        // 別アプリ)へ渡してしまい、オーナーがその後ろへ回る。
        if (ownerHandle != IntPtr.Zero)
        {
            EnableWindow(ownerHandle, false);
        }
    }

    /// <summary>
    /// <see cref="Show"/> と同じくオーナーだけを止めて表示し、閉じるまで呼び出しを待つ。
    ///
    /// <para>
    /// 結果をその場で使う呼び出し(<c>Window.OnClosing</c> の中で閉じるかを決める確認など)用。
    /// 待つ間は <see cref="Dispatcher.PushFrame"/> でメッセージを回すので、<see cref="Window.ShowDialog"/> と違って
    /// オーナー以外のウィンドウ(ウィジェット)は動き続ける。
    /// </para>
    /// </summary>
    public static void ShowAndWait(Window window, Window? owner)
    {
        var frame = new DispatcherFrame();
        Show(window, owner, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);
}
