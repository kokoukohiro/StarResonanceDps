using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views;

public partial class SettingsWindow : Window
{
    private const int WmNcHitTest = 0x0084;

    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const double ResizeBorderThickness = 8.0;

    private bool _isSyncingExternalScrollBar;

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += SettingsWindow_Loaded;
        SourceInitialized += SettingsWindow_SourceInitialized;
        PreviewMouseDown += SettingsWindow_PreviewMouseDown;
        Deactivated += SettingsWindow_Deactivated;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
        QueueNpcapWarning();
    }


    private void QueueNpcapWarning()
    {
        Dispatcher.BeginInvoke((Action)ShowNpcapWarningIfNeeded, DispatcherPriority.ContextIdle);
    }

    private void ShowNpcapWarningIfNeeded()
    {
        var version = NpcapVersionProbe.GetVersion();
        string? message = null;
        string? detail = null;
        var localization = LocalizationManager.Instance;

        if (version == new Version())
        {
            message = localization.GetString("Confirm_NpcapMissing_Message");
            detail = localization.GetString("Confirm_NpcapMissing_Detail");
        }
        else if (version < new Version(1, 86))
        {
            message = localization.GetString("Confirm_NpcapOutdated_Message");
            detail = localization.Format("Confirm_NpcapOutdated_Detail", version);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var confirmed = ConfirmWindow.ShowText(
            this,
            localization.GetString("Confirm_NpcapUpdate_Title"),
            message,
            detail ?? string.Empty);

        if (!confirmed)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://npcap.com/",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.Dispose();
        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        CommitGameCaptureCustomExeNameEdit();

        if (ViewModel.HasUnsavedChanges)
        {
            var confirmed = ConfirmWindow.Show(
                this,
                "Confirm_DiscardUnsaved_Title",
                "Confirm_DiscardUnsaved_Message",
                "Confirm_DiscardUnsaved_Detail");

            if (!confirmed)
            {
                e.Cancel = true;
                return;
            }

            HotkeyRegistrationFailureMessage.Show(this, ViewModel.RestoreSavedSettingsPreview());
        }

        base.OnClosing(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest || ResizeMode == ResizeMode.NoResize || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        var cursor = PointFromScreen(GetScreenPoint(lParam));
        var width = ActualWidth;
        var height = ActualHeight;

        var left = cursor.X >= 0 && cursor.X < ResizeBorderThickness;
        var right = cursor.X <= width && cursor.X > width - ResizeBorderThickness;
        var top = cursor.Y >= 0 && cursor.Y < ResizeBorderThickness;
        var bottom = cursor.Y <= height && cursor.Y > height - ResizeBorderThickness;

        var hitTest = 0;

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

        if (hitTest == 0)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private static Point GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }

    private void BasicNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BasicSection);
    }

    private void DisplayNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(DisplaySection);
    }

    private void AggregationNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(AggregationSection);
    }

    private void HotkeyNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(HotkeySection);
    }

    /// <summary>ホットキーの欄をクリックした。その行でキーの受付を始める。</summary>
    private void HotkeyBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { DataContext: HotkeyItemViewModel item } box)
        {
            return;
        }

        // 読み取り専用の欄の文字を選択させない。
        e.Handled = true;
        box.Focus();
        ViewModel.BeginHotkeyCapture(item);
    }

    /// <summary>
    /// 受付中に押されたキーを割り当てる。どのキーも割り当ての対象(Esc・Delete も)。
    /// 修飾キーだけを押したときは、組み合わせの途中なので待つ。
    /// </summary>
    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: HotkeyItemViewModel { IsCapturing: true } })
        {
            return;
        }

        e.Handled = true;

        // Alt との組み合わせと F10 は System、IME を通ったキーは ImeProcessed として届く。
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key
        };

        if (key == Key.None || HotkeyBindingConfig.IsModifierKey(key))
        {
            return;
        }

        var failures = ViewModel.CompleteHotkeyCapture(key, Keyboard.Modifiers);
        Keyboard.ClearFocus();
        HotkeyRegistrationFailureMessage.Show(this, failures);
    }

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: HotkeyItemViewModel { IsCapturing: true } })
        {
            HotkeyRegistrationFailureMessage.Show(this, ViewModel.CancelHotkeyCapture());
        }
    }

    /// <summary>受付中に欄の外をクリックしたら取り消す。ほかの行の欄なら、その行の受付に移る(欄の処理に任せる)。</summary>
    private void SettingsWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.IsCapturingHotkey || IsWithinHotkeyBox(e.OriginalSource as DependencyObject))
        {
            return;
        }

        Keyboard.ClearFocus();
        HotkeyRegistrationFailureMessage.Show(this, ViewModel.CancelHotkeyCapture());
    }

    /// <summary>ほかのアプリをクリックしたときも欄の外として取り消す(受付の間はホットキーを外しているため)。</summary>
    private void SettingsWindow_Deactivated(object? sender, EventArgs e)
    {
        if (ViewModel.IsCapturingHotkey)
        {
            Keyboard.ClearFocus();
            HotkeyRegistrationFailureMessage.Show(this, ViewModel.CancelHotkeyCapture());
        }
    }

    private static bool IsWithinHotkeyBox(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is TextBox { DataContext: HotkeyItemViewModel })
            {
                return true;
            }

            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    private void ThemeNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ThemeSection);
    }


    private void UpdateNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(UpdateSection);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = ConfirmWindow.Show(
            this,
            "Confirm_ResetSettings_Title",
            "Confirm_ResetSettings_Message",
            "Confirm_ResetSettings_Detail");

        if (!confirmed)
        {
            return;
        }

        HotkeyRegistrationFailureMessage.Show(this, ViewModel.ResetToDefaults());
    }


    private void ColorOptionRadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ColorOptionViewModel option })
        {
            ViewModel.ApplyWindowColor(option.Color);
        }
    }

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var window = new ColorPickerWindow(ViewModel.GetSelectedWindowColor());

        if (owner is not null)
        {
            window.Owner = owner;
        }

        OwnerModalWindow.Show(window, owner, () =>
        {
            if (window.IsConfirmed)
            {
                ViewModel.ApplyWindowColor(window.SelectedColor);
            }
        });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        CommitGameCaptureCustomExeNameEdit();
        var hotkeyFailures = ViewModel.SaveSettings();
        HotkeyRegistrationFailureMessage.Show(this, hotkeyFailures);
        Close();
    }

    private void GameCaptureCustomExeNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }

        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CommitGameCaptureCustomExeNameEdit()
    {
        GameCaptureCustomExeNameTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void ScrollToSection(FrameworkElement target)
    {
        if (ContentScrollViewer.Content is not FrameworkElement content)
        {
            target.BringIntoView();
            QueueUpdateExternalScrollBar();
            return;
        }

        var point = target.TransformToVisual(content).Transform(new Point(0, 0));
        ContentScrollViewer.ScrollToVerticalOffset(Math.Max(point.Y, 0));
        QueueUpdateExternalScrollBar();
    }

    private void ContentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar(ContentScrollViewer, SettingsExternalScrollBar);
    }

    private void ContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void SettingsExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingExternalScrollBar || !IsLoaded)
        {
            return;
        }

        ContentScrollViewer.ScrollToVerticalOffset(e.NewValue);
    }

    private void QueueUpdateExternalScrollBar()
    {
        Dispatcher.BeginInvoke(
            () => UpdateExternalScrollBar(ContentScrollViewer, SettingsExternalScrollBar),
            DispatcherPriority.Loaded);
    }

    private void UpdateExternalScrollBar(ScrollViewer scrollViewer, ScrollBar scrollBar)
    {
        _isSyncingExternalScrollBar = true;

        try
        {
            var maximum = Math.Max(scrollViewer.ScrollableHeight, 0);
            scrollBar.Maximum = maximum;
            scrollBar.ViewportSize = Math.Max(scrollViewer.ViewportHeight, 0);
            scrollBar.LargeChange = Math.Max(scrollViewer.ViewportHeight * 0.9, 1);
            scrollBar.SmallChange = 48;
            scrollBar.Value = Math.Min(scrollViewer.VerticalOffset, maximum);
            var isScrollBarVisible = maximum > 0;
            scrollBar.Visibility = isScrollBarVisible ? Visibility.Visible : Visibility.Collapsed;

            SettingsExternalScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(8);
        }
        finally
        {
            _isSyncingExternalScrollBar = false;
        }
    }
}