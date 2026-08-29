using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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
            EnableWindow(ownerHandle, false);
            window.Closed += (_, _) =>
            {
                EnableWindow(ownerHandle, true);

                // 破棄の後に戻すので、そのままだと別のアプリへフォーカスが飛ぶことがある。
                // オーナーへ明示的に返す。
                owner!.Activate();
            };
        }

        if (onClosed is not null)
        {
            window.Closed += (_, _) => onClosed();
        }

        window.Show();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);
}
