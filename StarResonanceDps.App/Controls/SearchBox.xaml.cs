using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StarResonanceDps.App.Controls;

public partial class SearchBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(SearchBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder),
        typeof(string),
        typeof(SearchBox),
        new PropertyMetadata(string.Empty));

    public SearchBox()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    private void SearchBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsFromTextBox(source))
        {
            return;
        }

        PART_TextBox.Focus();
        e.Handled = true;
    }

    private static bool IsFromTextBox(DependencyObject source)
    {
        while (source is not null)
        {
            if (source is TextBox)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
