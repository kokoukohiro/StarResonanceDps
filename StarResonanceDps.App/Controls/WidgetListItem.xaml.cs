using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views;

namespace StarResonanceDps.App.Controls;

public partial class WidgetListItem : UserControl
{
    private const double PlayerWindowCountBadgeHeight = 16d;
    private const double PlayerWindowCountBadgeHorizontalPadding = 4d;
    private const double PlayerWindowCountBadgeFontSize = 11d;

    private WidgetListItemViewModel? _playerWindowBadgeWidget;

    public WidgetListItem()
    {
        InitializeComponent();
        DataContextChanged += WidgetListItem_DataContextChanged;
        Loaded += WidgetListItem_Loaded;
        Unloaded += WidgetListItem_Unloaded;
    }

    private void WidgetListItem_Loaded(object sender, RoutedEventArgs e)
    {
        AttachPlayerWindowBadgeWidget(DataContext as WidgetListItemViewModel);
        UpdatePlayerWindowCountBadge();
    }

    private void WidgetListItem_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachPlayerWindowBadgeWidget();
    }

    private void WidgetListItem_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachPlayerWindowBadgeWidget(e.NewValue as WidgetListItemViewModel);
        UpdatePlayerWindowCountBadge();
    }

    private void AttachPlayerWindowBadgeWidget(WidgetListItemViewModel? widget)
    {
        if (ReferenceEquals(_playerWindowBadgeWidget, widget))
        {
            return;
        }

        DetachPlayerWindowBadgeWidget();
        _playerWindowBadgeWidget = widget;

        if (_playerWindowBadgeWidget is not null)
        {
            _playerWindowBadgeWidget.PropertyChanged += PlayerWindowBadgeWidget_PropertyChanged;
        }
    }

    private void DetachPlayerWindowBadgeWidget()
    {
        if (_playerWindowBadgeWidget is not null)
        {
            _playerWindowBadgeWidget.PropertyChanged -= PlayerWindowBadgeWidget_PropertyChanged;
            _playerWindowBadgeWidget = null;
        }
    }

    private void PlayerWindowBadgeWidget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetListItemViewModel.OpenPlayerWindowCount))
        {
            UpdatePlayerWindowCountBadge();
        }
    }

    private void UpdatePlayerWindowCountBadge()
    {
        if (_playerWindowBadgeWidget is not { ShowsPlayerWindowCountBadge: true })
        {
            PlayerWindowCountBadgePath.Data = Geometry.Empty;
            return;
        }

        var countText = _playerWindowBadgeWidget.OpenPlayerWindowCount.ToString(CultureInfo.CurrentCulture);
        var formattedText = new FormattedText(
            countText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            PlayerWindowCountBadgeFontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var badgeWidth = Math.Max(
            PlayerWindowCountBadgeHeight,
            formattedText.WidthIncludingTrailingWhitespace + (PlayerWindowCountBadgeHorizontalPadding * 2d));
        var badgeGeometry = new RectangleGeometry(
            new Rect(0d, 0d, badgeWidth, PlayerWindowCountBadgeHeight),
            PlayerWindowCountBadgeHeight / 2d,
            PlayerWindowCountBadgeHeight / 2d);
        var textGeometry = formattedText.BuildGeometry(new Point(
            (badgeWidth - formattedText.WidthIncludingTrailingWhitespace) / 2d,
            (PlayerWindowCountBadgeHeight - formattedText.Height) / 2d));
        var cutoutGeometry = Geometry.Combine(
            badgeGeometry,
            textGeometry,
            GeometryCombineMode.Exclude,
            Transform.Identity);

        if (cutoutGeometry.CanFreeze)
        {
            cutoutGeometry.Freeze();
        }

        PlayerWindowCountBadgePath.Data = cutoutGeometry;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WidgetListItemViewModel widget)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var settingsWindow = new WidgetSettingsWindow(widget);

        if (owner is not null)
        {
            const double leftOffset = 24;
            const double topOffset = 72;

            settingsWindow.Owner = owner;
            settingsWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            settingsWindow.Left = owner.Left + leftOffset;
            settingsWindow.Top = owner.Top + topOffset;
        }

        settingsWindow.ShowDialog();
    }
}
