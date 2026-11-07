using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleWidget.SampleWidgetPlugin), "kokoukohiro.sample-widget", PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Widget")]
[assembly: PluginDisplayName("ja-JP", "サンプル: ウィジェット")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]
[assembly: PluginIcon("Assets/icon.png")]

namespace SampleWidget;

/// <summary>
/// A plugin that adds two widgets to the app's widget list: one with everything a widget can have, and the smallest one.
/// A plugin without a window has no genre.
/// </summary>
public sealed class SampleWidgetPlugin : IStarResonancePlugin, IPluginWidgetProvider
{
    private const string FullWidgetId = "full";
    private const string MinimalWidgetId = "minimal";

    public void Initialize(IPluginContext context)
    {
    }

    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions()
    {
        return
        [
            new PluginWidgetDefinition
            {
                Id = FullWidgetId,
                Names = Texts("Sample widget", "サンプルのウィジェット"),
                DefaultWidth = 280,
                DefaultHeight = 240,
                HasFooter = true,
                ScrollsVertically = true,
                SettingSections =
                [
                    new PluginWidgetSettingSection
                    {
                        Names = Texts("Sample settings", "サンプルの設定"),
                        Icon = PluginResourceKeys.Icons.Sliders,
                        IconStyle = PluginResourceKeys.Icons.FillStyle,
                        Settings = FullWidgetBody.CreateSettings()
                    },
                    new PluginWidgetSettingSection
                    {
                        Names = Texts("Sample colors", "サンプルの色"),
                        Icon = PluginResourceKeys.Icons.Settings,
                        IconStyle = PluginResourceKeys.Icons.FillStyle,
                        Settings = FullWidgetBody.CreateColorSettings()
                    }
                ]
            },
            new PluginWidgetDefinition
            {
                Id = MinimalWidgetId,
                Names = Texts("Minimal widget", "最小のウィジェット"),
                DefaultWidth = 240,
                DefaultHeight = 80
            }
        ];
    }

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context)
    {
        return widgetId switch
        {
            FullWidgetId => CreateFullWidget(context),
            MinimalWidgetId => new PluginWidgetContent { Body = CreateWindowText("Only the body.") },
            _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, "Unknown widget.")
        };
    }

    public void Shutdown()
    {
    }

    private static PluginWidgetContent CreateFullWidget(IPluginWidgetContext context)
    {
        var clicks = 0;
        var footer = CreateWindowText("Clicked 0 times");
        var button = new PluginWidgetHeaderButton { Text = "Count" };
        button.Click += (_, _) =>
        {
            clicks++;
            footer.Text = $"Clicked {clicks} times";
        };

        return new PluginWidgetContent
        {
            Body = new FullWidgetBody(context),
            Footer = footer,
            HeaderButtons = [button]
        };
    }

    /// <summary>Text in the look of the app's widgets. The window's text is white with a shadow.</summary>
    internal static TextBlock CreateWindowText(string text)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(8, 4, 8, 4) };
        block.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Text.WidgetWindow);
        return block;
    }

    internal static IReadOnlyDictionary<string, string> Texts(string english, string japanese)
    {
        return new Dictionary<string, string> { ["en-US"] = english, ["ja-JP"] = japanese };
    }
}
