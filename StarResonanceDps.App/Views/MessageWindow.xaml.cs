using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public static void Show(Window? owner, string title, string message)
    {
        Show(owner, title, message, (string?)null);
    }

    public static void Show(Window? owner, string title, string message, string? detail)
    {
        var window = Create(owner, title, message);
        window.DetailText.Text = detail ?? string.Empty;
        window.DetailText.Visibility = string.IsNullOrWhiteSpace(detail)
            ? Visibility.Collapsed
            : Visibility.Visible;

        // ShowDialog はアプリのウィンドウを全部止めるので、ウィジェットも操作できなくなる。止めるのはオーナーだけにする。
        OwnerModalWindow.ShowAndWait(window, owner);
    }

    /// <summary>詳細の欄にリンクを含めて出す。リンクは <c>Hyperlink.Inline</c> で描き、押したら開く。</summary>
    public static void Show(Window? owner, string title, string message, IReadOnlyList<MessageDetailPart> detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var window = Create(owner, title, message);
        var linkStyle = (Style)window.FindResource("Hyperlink.Inline");
        foreach (var part in detail)
        {
            if (part.Url is null)
            {
                AddText(window.DetailText.Inlines, part.Text);
                continue;
            }

            var url = part.Url;
            var link = new Hyperlink { Style = linkStyle };
            AddText(link.Inlines, part.Text);
            link.Click += (_, _) => ExternalLinkOpener.Open(url);
            window.DetailText.Inlines.Add(link);
        }

        window.DetailText.Visibility = detail.Any(part => !string.IsNullOrWhiteSpace(part.Text))
            ? Visibility.Visible
            : Visibility.Collapsed;

        // ShowDialog はアプリのウィンドウを全部止めるので、ウィジェットも操作できなくなる。止めるのはオーナーだけにする。
        OwnerModalWindow.ShowAndWait(window, owner);
    }

    private static MessageWindow Create(Window? owner, string title, string message)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        var window = new MessageWindow
        {
            Owner = owner,
            WindowStartupLocation = owner is null
                ? System.Windows.WindowStartupLocation.CenterScreen
                : System.Windows.WindowStartupLocation.CenterOwner
        };

        window.HeaderText.Text = title;
        window.MessageText.Text = message;
        return window;
    }

    /// <summary>改行を <see cref="LineBreak"/> にして足す。</summary>
    private static void AddText(InlineCollection inlines, string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            if (lines[i].Length > 0)
            {
                inlines.Add(new Run(lines[i]));
            }
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    // DialogResult は ShowDialog で表示した窓にしか設定できないので、閉じるだけにする(結果は使わない)。
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
