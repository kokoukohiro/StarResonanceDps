using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

public partial class WidgetSettingsWindow : Window
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

    private readonly WidgetListItemViewModel _widget;
    private readonly WidgetSettingsViewModel _viewModel;
    private bool _isSyncingExternalScrollBar;

    private WidgetSettingsViewModel ViewModel => _viewModel;

    public WidgetSettingsWindow(WidgetListItemViewModel widget)
    {
        _widget = widget;

        InitializeComponent();
        _viewModel = new WidgetSettingsViewModel(widget.Kind, widget.DisplayNameResourceKey);
        _viewModel.ThemePreviewChanged += _widget.ApplyTheme;
        _viewModel.MeterPreviewChanged += _widget.ApplyMeterSettingsPreview;
        _viewModel.MetricTimelinePreviewChanged += _widget.ApplyMetricTimelineSettingsPreview;
        _viewModel.BuffCardPreviewChanged += _widget.ApplyBuffCardSettingsPreview;
        _viewModel.TakenDamageLogPreviewChanged += _widget.ApplyTakenDamageLogSettingsPreview;
        _viewModel.BuffListPreviewChanged += _widget.ApplyBuffListSettingsPreview;
        _viewModel.ElementColorPreviewChanged += _widget.ApplyElementColorSettingsPreview;
        _viewModel.SkillDetailPreviewChanged += _widget.ApplySkillDetailSettingsPreview;
        _viewModel.PlayerStatusPreviewChanged += _widget.ApplyPlayerStatusSettingsPreview;
        DataContext = _viewModel;

        Loaded += WidgetSettingsWindow_Loaded;
        SourceInitialized += WidgetSettingsWindow_SourceInitialized;
    }

    private void WidgetSettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void WidgetSettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ThemePreviewChanged -= _widget.ApplyTheme;
        _viewModel.MeterPreviewChanged -= _widget.ApplyMeterSettingsPreview;
        _viewModel.MetricTimelinePreviewChanged -= _widget.ApplyMetricTimelineSettingsPreview;
        _viewModel.BuffCardPreviewChanged -= _widget.ApplyBuffCardSettingsPreview;
        _viewModel.TakenDamageLogPreviewChanged -= _widget.ApplyTakenDamageLogSettingsPreview;
        _viewModel.BuffListPreviewChanged -= _widget.ApplyBuffListSettingsPreview;
        _viewModel.ElementColorPreviewChanged -= _widget.ApplyElementColorSettingsPreview;
        _viewModel.SkillDetailPreviewChanged -= _widget.ApplySkillDetailSettingsPreview;
        _viewModel.PlayerStatusPreviewChanged -= _widget.ApplyPlayerStatusSettingsPreview;
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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

            ViewModel.RestoreSavedPreviews();
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

    private void WindowSettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(WindowSettingsHost);
    }

    private void DisplaySettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(DisplaySettingsHost);
    }

    private void PlayerStatusRowsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(PlayerStatusRowsHost);
    }

    private void OtherRoleSkillsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(OtherRoleSkillsHost);
    }

    private void ThemeNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ThemeSection);
    }

    private void ClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ClassColorsHost);
    }

    private void TakenDamageLogClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(TakenDamageLogClassColorsHost);
    }

    private void TakenDamageLogTextColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(TakenDamageLogTextColorsHost);
    }

    private void BuffListColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffListColorsHost);
    }

    private void ElementColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ElementColorsHost);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = ConfirmWindow.Show(
            this,
            "Confirm_ResetWidgetSettings_Title",
            "Confirm_ResetWidgetSettings_Message",
            "Confirm_ResetWidgetSettings_Detail");

        if (!confirmed)
        {
            return;
        }

        ViewModel.ResetToDefaults();
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

    private void SelectBackgroundImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{LocalizationManager.Instance.GetString("Settings_Theme_BackgroundImage_Filter")}|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
            CheckFileExists = true,
            Multiselect = false,
            FileName = ViewModel.BackgroundImagePath ?? string.Empty
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.SetBackgroundImagePath(dialog.FileName);
        }
    }


    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var config = ViewModel.SaveSettings();
        _widget.ApplyWidgetConfig(config);
        Close();
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
