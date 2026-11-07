using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleWidget.SampleWidgetPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Widget")]
[assembly: PluginDisplayName("ja-JP", "サンプル: ウィジェット")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]
[assembly: PluginIcon("Assets/icon.png")]

namespace SampleWidget;

/// <summary>
/// A plugin that adds widgets to the app's widget list. Two open one window: one with everything a widget can have, and the smallest one.
/// Two open one window per target: one with a fixed list of targets, and one with the nearby players.
/// A plugin without a window has no genre.
/// </summary>
public sealed class SampleWidgetPlugin : IStarResonancePlugin, IPluginWidgetProvider, IPluginMultiWidgetProvider
{
    private const string FullWidgetId = "full";
    private const string MinimalWidgetId = "minimal";
    private const string SlotsWidgetId = "slots";
    private const string PlayersWidgetId = "players";

    private IPluginGameDataService? _gameData;

    public void Initialize(IPluginContext context)
    {
        _gameData = context.GameData;
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

    public IReadOnlyList<PluginWidgetDefinition> GetMultiWidgetDefinitions()
    {
        return
        [
            new PluginWidgetDefinition
            {
                Id = SlotsWidgetId,
                Names = Texts("Sample slots", "サンプルの枠"),
                DefaultWidth = 240,
                DefaultHeight = 80
            },
            new PluginWidgetDefinition
            {
                Id = PlayersWidgetId,
                Names = Texts("Sample players", "サンプルのプレイヤー"),
                DefaultWidth = 240,
                DefaultHeight = 80
            }
        ];
    }

    public IReadOnlyList<PluginWidgetTarget> GetWidgetTargets(string widgetId)
    {
        return widgetId switch
        {
            SlotsWidgetId => CreateSlotTargets(),
            PlayersWidgetId => CreatePlayerTargets(),
            _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, "Unknown widget.")
        };
    }

    public PluginWidgetContent CreateWidget(string widgetId, string targetId, IPluginWidgetContext context)
    {
        return new PluginWidgetContent { Body = CreateWindowText($"Target: {targetId}") };
    }

    public void Shutdown()
    {
    }

    private static IReadOnlyList<PluginWidgetTarget> CreateSlotTargets()
    {
        return
        [
            new PluginWidgetTarget { Id = "a", Names = Texts("Slot A", "枠A") },
            new PluginWidgetTarget { Id = "b", Names = Texts("Slot B", "枠B") }
        ];
    }

    /// <summary>
    /// The list is empty until the app knows a player; meanwhile the start button opens an empty window, which gets its content when a target appears.
    /// IsDefault makes the card select the local player as soon as they are known.
    /// A target needs a name, and a player's name can be unknown for a moment, so the ID stands in for it.
    /// </summary>
    private IReadOnlyList<PluginWidgetTarget> CreatePlayerTargets()
    {
        var players = _gameData?.Players ?? [];
        return
        [
            .. players
                .Where(player => player.CharacterId != 0 && (player.IsSelf || player.IsNearby))
                .Select(player =>
                {
                    var id = player.CharacterId.ToString(CultureInfo.InvariantCulture);
                    var name = string.IsNullOrWhiteSpace(player.Name) ? id : player.Name;
                    return new PluginWidgetTarget
                    {
                        Id = id,
                        Names = Texts(name, name),
                        IsDefault = player.IsSelf
                    };
                })
        ];
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
