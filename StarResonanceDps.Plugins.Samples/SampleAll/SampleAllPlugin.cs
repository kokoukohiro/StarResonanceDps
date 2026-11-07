using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleAll.SampleAllPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Everything")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 全部")]
[assembly: PluginDisplayName("ko-KR", "샘플: 전부")]
[assembly: PluginDisplayName("zh-CN", "示例: 全部")]
[assembly: PluginGenre("en-US", "Sample")]
[assembly: PluginGenre("ja-JP", "サンプル")]
[assembly: PluginGenre("ko-KR", "샘플")]
[assembly: PluginGenre("zh-CN", "示例")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleAll;

/// <summary>
/// One plugin with a window, a widget and a notification item.
/// The window and the widget each have a button that sends the notification.
/// </summary>
public sealed class SampleAllPlugin : IStarResonancePlugin, IPluginWindowProvider, IPluginWidgetProvider, IPluginNotificationProvider
{
    private const string WidgetId = "widget";
    private const string NotificationId = "button-pressed";

    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
    }

    public FrameworkElement CreateContent()
    {
        var button = new Button { Content = "Send the notification", Width = 200, Margin = new Thickness(24, 8, 24, 24), HorizontalAlignment = HorizontalAlignment.Left };
        button.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Buttons.Text);
        button.Click += (_, _) => SendNotification("window");
        return button;
    }

    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions()
    {
        return
        [
            new PluginWidgetDefinition
            {
                Id = WidgetId,
                Names = Texts("Sample widget", "サンプルのウィジェット"),
                DefaultWidth = 240,
                DefaultHeight = 80
            }
        ];
    }

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context)
    {
        var text = new TextBlock { Text = "Press the button in the title bar.", Margin = new Thickness(8, 4, 8, 4) };
        text.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Text.WidgetWindow);

        var button = new PluginWidgetHeaderButton { Text = "Send" };
        button.Click += (_, _) => SendNotification("widget");

        return new PluginWidgetContent { Body = text, HeaderButtons = [button] };
    }

    public IReadOnlyList<PluginNotificationDefinition> GetNotificationDefinitions()
    {
        return
        [
            new PluginNotificationDefinition
            {
                Id = NotificationId,
                Names = Texts("Button pressed", "ボタンの押下"),
                DefaultTexts = Texts("The button in the {Place} was pressed.", "{Place}のボタンが押されました。"),
                Fields =
                [
                    new PluginNotificationField
                    {
                        Key = "Place",
                        Names = Texts("Place", "場所"),
                        SampleValues = Texts("window", "window")
                    }
                ]
            }
        ];
    }

    public void Shutdown()
    {
        _context = null;
    }

    private void SendNotification(string place)
    {
        _context?.Notifications.Notify(NotificationId, new Dictionary<string, string> { ["Place"] = place });
    }

    private static IReadOnlyDictionary<string, string> Texts(string english, string japanese)
    {
        return new Dictionary<string, string> { ["en-US"] = english, ["ja-JP"] = japanese };
    }
}
