using System;
using System.ComponentModel;
using System.Globalization;
using System.Media;
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

    /// <summary>空だった VOICEVOX のリストを、選択肢ができたので開き直している(開き直しでは問い合わせない)。</summary>
    private bool _isReopeningVoicevoxDropDown;

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsWindow()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(BenchmarkDurationTextBox, BenchmarkDurationTextBox_Pasting);
        Loaded += SettingsWindow_Loaded;
        SourceInitialized += SettingsWindow_SourceInitialized;
        PreviewMouseDown += SettingsWindow_PreviewMouseDown;
        Deactivated += SettingsWindow_Deactivated;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SpeechVoiceCheckRequested += ViewModel_SpeechVoiceCheckRequested;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ShowBenchmarkDuration();
        QueueUpdateExternalScrollBar();
        QueueNpcapWarning();

        // スタイルの行を出すかは、エンジンの一覧で話者のスタイルの数を見ないと決まらない。
        // つながらなくても知らせない(知らせるのは読み上げで選んだときと起動時。失敗は VoicevoxClient がログに書く)。
        if (ViewModel.IsVoicevoxSelected)
        {
            await ViewModel.RefreshVoicevoxSpeakersAsync();
        }
    }

    /// <summary>
    /// 通知方式を読み上げにした・読み上げ方式で声を選んだ・Windows の音声のまま言語を変えた。声を確かめて、使えなければ知らせる。
    /// VOICEVOX のときは、確かめるのを兼ねて話者の一覧を取る。
    /// </summary>
    private async void ViewModel_SpeechVoiceCheckRequested(object? sender, EventArgs e)
    {
        if (ViewModel.IsVoicevoxSelected)
        {
            NotificationCheckMessage.ShowVoicevoxFailure(this, await ViewModel.RefreshVoicevoxSpeakersAsync());
            return;
        }

        await NotificationCheckMessage.CheckSpeechVoiceAsync(this, ViewModel.NotificationMethodIndex, ViewModel.SpeechVoiceIndex);
    }

    /// <summary>
    /// 話者かスタイルのリストを開いた。エンジンに一覧を問い合わせて作り直す。
    /// つながらなくても知らせない(読み上げで VOICEVOX を選んだときと起動時に知らせている。失敗は VoicevoxClient がログに書く)。
    /// 選択肢が1つも無いときは空のリストを出さずに閉じ、問い合わせで選択肢ができたら開き直す。
    /// </summary>
    private async void VoicevoxComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        var comboBox = (ComboBox)sender!;
        if (_isReopeningVoicevoxDropDown)
        {
            _isReopeningVoicevoxDropDown = false;
            return;
        }

        var wasEmpty = comboBox.Items.Count == 0;
        if (wasEmpty)
        {
            comboBox.IsDropDownOpen = false;
        }

        await ViewModel.RefreshVoicevoxSpeakersAsync();

        if (wasEmpty && comboBox.Items.Count > 0 && comboBox.IsVisible)
        {
            _isReopeningVoicevoxDropDown = true;
            comboBox.IsDropDownOpen = true;
        }
    }

    private void VoicevoxDownloadLink_Click(object sender, RoutedEventArgs e)
    {
        ExternalLinkOpener.Open(VoicevoxClient.DownloadPageUrl);
    }

    /// <summary>利用規約のリンクを押した。選んである話者の規約の本文をエンジンから取って出す。</summary>
    private async void VoicevoxPolicyLink_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasVoicevoxSpeaker)
        {
            return;
        }

        await NotificationCheckMessage.ShowVoicevoxPolicyAsync(this, ViewModel.VoicevoxSpeakerUuid, ViewModel.VoicevoxSpeakerName);
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

        ExternalLinkOpener.Open("https://npcap.com/");
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
        ViewModel.SpeechVoiceCheckRequested -= ViewModel_SpeechVoiceCheckRequested;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.Dispose();
        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        CommitGameCaptureCustomExeNameEdit();
        CommitBenchmarkDurationEdit();

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

    private void NotificationNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(NotificationSection);
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
        CommitBenchmarkDurationEdit();
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

    // --- 通知音量のスライドバー ---
    // 値を設定へ渡す(保存前プレビュー・未保存の印・通知の音量に効く)のは、つまみを手放したときと、
    // ドラッグ以外の操作(クリック・キー・ホイール)で値が変わったときだけ。ドラッグ中は打つ前の値のまま。

    private bool _isDraggingNotificationVolume;

    private void NotificationVolumeSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDraggingNotificationVolume = true;
    }

    private void NotificationVolumeSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDraggingNotificationVolume = false;
        CommitNotificationVolume((Slider)sender);
    }

    private void NotificationVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDraggingNotificationVolume)
        {
            CommitNotificationVolume((Slider)sender);
        }
    }

    private static void CommitNotificationVolume(Slider slider)
    {
        slider.GetBindingExpression(RangeBase.ValueProperty)?.UpdateSource();
    }

    // --- 計測時間の入力欄 ---
    // 文字はバインドせずここで書く。確定(Enter・フォーカスが外れたとき・保存と閉じるとき)のたびに今の値で書き直す
    // (範囲の外を端の値にしたとき、値が変わらなくても欄を直すため)。

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 初期化など、欄の外から値が変わったとき。
        if (e.PropertyName == nameof(SettingsViewModel.BenchmarkDurationSeconds))
        {
            ShowBenchmarkDuration();
        }
    }

    private void ShowBenchmarkDuration()
    {
        BenchmarkDurationTextBox.Text = ViewModel.BenchmarkDurationSeconds.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>欄の文字を確定する。空なら打つ前の値に戻して警告音を鳴らす。</summary>
    private void CommitBenchmarkDurationEdit()
    {
        if (!ViewModel.TryCommitBenchmarkDurationText(BenchmarkDurationTextBox.Text))
        {
            PlayInvalidInputSound();
        }

        ShowBenchmarkDuration();
    }

    /// <summary>数字以外の文字は入れない。文字を伴わない入力(Ctrl との組み合わせなど)は止めない。</summary>
    private void BenchmarkDurationTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && !e.Text.All(char.IsAsciiDigit))
        {
            e.Handled = true;
            PlayInvalidInputSound();
        }
    }

    private void BenchmarkDurationTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text)
            || e.DataObject.GetData(DataFormats.Text) is not string pastedText
            || pastedText.Length == 0
            || !pastedText.All(char.IsAsciiDigit))
        {
            e.CancelCommand();
            PlayInvalidInputSound();
        }
    }

    private void BenchmarkDurationTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // スペースは文字の入力(PreviewTextInput)を通らないので、ここで止める。
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            PlayInvalidInputSound();
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitBenchmarkDurationEdit();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void BenchmarkDurationTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitBenchmarkDurationEdit();
    }

    private static void PlayInvalidInputSound()
    {
        SystemSounds.Beep.Play();
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