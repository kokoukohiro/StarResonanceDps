using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleMultiWidget.SampleMultiWidgetPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Per-target widget")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 相手ごとのウィジェット")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleMultiWidget;

/// <summary>
/// A plugin that adds widgets that open one window per target: one with a fixed list of targets, and one with the nearby players.
/// The header buttons open windows from code: the other slot of this plugin's widget, and the app's Player Info.
/// A plugin without a window has no genre.
/// </summary>
public sealed class SampleMultiWidgetPlugin : IStarResonancePlugin, IPluginMultiWidgetProvider
{
    private const string SlotsWidgetId = "slots";
    private const string PlayersWidgetId = "players";

    private IPluginContext? _plugin;
    private IPluginGameDataService? _gameData;

    public void Initialize(IPluginContext context)
    {
        _plugin = context;
        _gameData = context.GameData;
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
                DefaultHeight = 80,
                // The players are live data. When the app started reading the game connection in the middle, the circle is red until a map move,
                // also on the empty window that waits for a player.
                StatusRule = PluginWidgetStatusRule.Live
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
        return widgetId switch
        {
            SlotsWidgetId => CreateSlotWidget(targetId),
            PlayersWidgetId => CreatePlayerWidget(targetId),
            _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, "Unknown widget.")
        };
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

    private PluginWidgetContent CreateSlotWidget(string targetId)
    {
        // Opens the window of the other slot of this widget (OpenAsync with a target ID).
        var otherId = targetId == "a" ? "b" : "a";
        var button = new PluginWidgetHeaderButton { Text = otherId == "a" ? "Slot A" : "Slot B" };
        button.Click += (_, _) => _ = _plugin?.Widgets.OpenAsync(SlotsWidgetId, otherId);
        return new PluginWidgetContent { Body = CreateWindowText($"Target: {targetId}"), HeaderButtons = [button] };
    }

    private PluginWidgetContent CreatePlayerWidget(string targetId)
    {
        var body = CreateWindowText($"Target: {targetId}");
        if (!long.TryParse(targetId, NumberStyles.None, CultureInfo.InvariantCulture, out var characterId))
        {
            return new PluginWidgetContent { Body = body };
        }

        // Opens the app's Player Info for the player of this window (the target ID is the character ID).
        var infoButton = new PluginWidgetHeaderButton { Text = "Info" };
        infoButton.Click += (_, _) => _ = _plugin?.Widgets.OpenHostForPlayerAsync(PluginHostWidgetKind.PlayerInfo, characterId);
        return new PluginWidgetContent { Body = body, HeaderButtons = [infoButton] };
    }

    /// <summary>Text in the look of the app's widgets. The window's text is white with a shadow.</summary>
    private static TextBlock CreateWindowText(string text)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(8, 4, 8, 4) };
        block.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Text.WidgetWindow);
        return block;
    }

    private static IReadOnlyDictionary<string, string> Texts(string english, string japanese)
    {
        return new Dictionary<string, string> { ["en-US"] = english, ["ja-JP"] = japanese };
    }
}
