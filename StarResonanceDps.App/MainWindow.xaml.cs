using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views;
#if DEBUG
using StarResonanceDps.App.DebugTools;
#endif

namespace StarResonanceDps.App;

public partial class MainWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const double ResizeBorderThickness = 8.0;

    private static readonly bool IsInDesignMode =
        DesignerProperties.GetIsInDesignMode(new DependencyObject());

    public MainWindow()
    {
        InitializeComponent();

        if (!IsInDesignMode)
        {
            DataContext = new MainViewModel();
        }

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
#if DEBUG
        PreviewKeyDown += MainWindow_PreviewKeyDown;
#endif
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RestoreRunningWidgetWindows();

            // ホットキーの補助が使えなかったこと・登録できなかったホットキーはここで知らせる
            // (メッセージの親にこの窓が要るので、開いた後に登録する)。
            GlobalHotkeyService.Instance.HelperUnavailable += HotkeyService_HelperUnavailable;
            HotkeyRegistrationFailureMessage.Show(this, viewModel.ApplyHotkeys());

            // 読み上げに使う VOICEVOX のスタイルを、最初の通知より前に初期化しておく(返事は待たない)。
            VoicevoxStyleInitializer.Start();

            // 読み上げの声が使えなければ知らせる(通知方式が読み上げでなければ何もしない)。
            var settings = ConfigManager.Instance.AppConfig.Settings;
            await NotificationCheckMessage.CheckSpeechVoiceAsync(this, settings.NotificationMethodIndex, settings.SpeechVoiceIndex);
        }
    }

    private void HotkeyService_HelperUnavailable(IReadOnlyList<HotkeyRegistrationFailure> failures)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            this,
            localization.GetString("Hotkey_Error_Title"),
            localization.GetString("Hotkey_HelperUnavailable_Message"),
            localization.GetString("Hotkey_HelperUnavailable_Detail"));
        HotkeyRegistrationFailureMessage.Show(this, failures);
    }

#if DEBUG
    // Ctrl+Shift+D でデバッグ用の確認画面(DebugTools、Debug の構成でだけビルドされる)を開く。
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.D || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        e.Handled = true;
        new MessagePreviewWindow { Owner = this }.Show();
    }
#endif

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest || ResizeMode == ResizeMode.NoResize || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        Point cursor = PointFromScreen(GetScreenPoint(lParam));
        double width = ActualWidth;
        double height = ActualHeight;

        bool left = cursor.X >= 0 && cursor.X < ResizeBorderThickness;
        bool right = cursor.X <= width && cursor.X > width - ResizeBorderThickness;
        bool top = cursor.Y >= 0 && cursor.Y < ResizeBorderThickness;
        bool bottom = cursor.Y <= height && cursor.Y > height - ResizeBorderThickness;

        int hitTest = HtClient;

        if (top && left)
        {
            hitTest = HtTopLeft;
        }
        else if (top && right)
        {
            hitTest = HtTopRight;
        }
        else if (bottom && left)
        {
            hitTest = HtBottomLeft;
        }
        else if (bottom && right)
        {
            hitTest = HtBottomRight;
        }
        else if (left)
        {
            hitTest = HtLeft;
        }
        else if (right)
        {
            hitTest = HtRight;
        }
        else if (top)
        {
            hitTest = HtTop;
        }
        else if (bottom)
        {
            hitTest = HtBottom;
        }

        if (hitTest == HtClient)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private static Point GetScreenPoint(IntPtr lParam)
    {
        long value = lParam.ToInt64();
        int x = unchecked((short)(value & 0xFFFF));
        int y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }
}
