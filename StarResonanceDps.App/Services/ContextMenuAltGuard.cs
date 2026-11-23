// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 右クリックのメニュー(<see cref="ContextMenu"/>)を、開いたときから押し続けている Alt で閉じさせない(notes/218)。
/// WPF のメニューは届いた Alt のキーで自分を閉じる(押しっぱなしの繰り返しも、前面が替わった直後の最初の Alt も)。
/// ゲームでは Alt を押したままマウスを使うので、開いたときに Alt を押していたメニューは、その Alt を離すまで Alt のキーを処理済みにする。
/// 離した後に改めて Alt を押せば、今までどおり閉じる。アプリの全部のメニュー(プラグインのも)に効くよう、起動時に型の受け手を1回だけ登録する。
/// </summary>
internal static class ContextMenuAltGuard
{
    private const int VkMenu = 0x12;

    // 今開いているメニューのうち、開いたときに Alt を押していたもの。Alt を離したときと閉じたときに外す。
    private static readonly HashSet<ContextMenu> AltHeldMenus = [];

    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler(OnOpened));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.ClosedEvent, new RoutedEventHandler(OnClosed));
        EventManager.RegisterClassHandler(typeof(ContextMenu), UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
        EventManager.RegisterClassHandler(typeof(ContextMenu), UIElement.PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp));
    }

    private static void OnOpened(object sender, RoutedEventArgs e)
    {
        // 前面が替わった直後は、この窓の側のキーの状態がまだ追いついていないので、キーボードの今の状態を読む。
        if (sender is ContextMenu menu && (GetAsyncKeyState(VkMenu) & 0x8000) != 0)
        {
            AltHeldMenus.Add(menu);
        }
    }

    private static void OnClosed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            AltHeldMenus.Remove(menu);
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is ContextMenu menu && IsAltKey(e) && AltHeldMenus.Contains(menu))
        {
            e.Handled = true;
        }
    }

    private static void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        // 開く前から押していた Alt を離したキーも、メニューへは渡さない(この Alt はメニューの操作ではない)。
        if (sender is ContextMenu menu && IsAltKey(e) && AltHeldMenus.Remove(menu))
        {
            e.Handled = true;
        }
    }

    private static bool IsAltKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key is Key.LeftAlt or Key.RightAlt;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
