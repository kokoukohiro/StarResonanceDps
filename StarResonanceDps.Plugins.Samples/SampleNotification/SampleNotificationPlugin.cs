using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleNotification.SampleNotificationPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Notification")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 通知")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleNotification;

/// <summary>
/// A plugin that adds two items to the app's notification tab: one with a field and its own settings, and the smallest one.
/// The user chooses how each notification is delivered; the plugin only decides when to send it.
/// </summary>
public sealed class SampleNotificationPlugin : IStarResonancePlugin, IPluginNotificationProvider
{
    private const string MapEnteredId = "map-entered";
    private const string EncounterStartedId = "encounter-started";

    private const string SkipSameMapId = "skip-same-map";
    private const string NameFormatId = "name-format";
    private const string MaxPerLaunchId = "max-per-launch";
    private const string PrefixId = "prefix";
    private const string QuietSecondsId = "quiet-seconds";

    private const string NameOnlyOptionId = "name-only";
    private const string NameAndIdOptionId = "name-and-id";

    private IPluginContext? _context;
    private int? _lastSceneId;
    private long _sentCount;
    private DateTime _lastSentUtc = DateTime.MinValue;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.GameData.MapEntered += GameData_MapEntered;
        context.GameData.EncounterStarted += GameData_EncounterStarted;
    }

    public IReadOnlyList<PluginNotificationDefinition> GetNotificationDefinitions()
    {
        return
        [
            new PluginNotificationDefinition
            {
                Id = MapEnteredId,
                Names = Texts("Entered a map", "マップ移動"),
                DefaultTexts = Texts("Entered {MapName}.", "{MapName}に入りました。"),
                Fields =
                [
                    new PluginNotificationField
                    {
                        Key = "MapName",
                        Names = Texts("Map name", "マップ名"),
                        SampleValues = Texts("Asterleeds", "アステルリーズ")
                    }
                ],
                Settings =
                [
                    new PluginSwitchSetting
                    {
                        Id = SkipSameMapId,
                        Names = Texts("Skip the same map", "同じマップは通知しない"),
                        DefaultValue = false
                    },
                    new PluginChoiceSetting
                    {
                        Id = NameFormatId,
                        Names = Texts("Map name", "マップ名"),
                        Options =
                        [
                            new PluginChoiceOption { Id = NameOnlyOptionId, Names = Texts("Name only", "名前だけ") },
                            new PluginChoiceOption { Id = NameAndIdOptionId, Names = Texts("Name and ID", "名前と番号") }
                        ],
                        DefaultOptionId = NameOnlyOptionId
                    },
                    new PluginNumberSetting
                    {
                        Id = MaxPerLaunchId,
                        Names = Texts("Limit per launch", "起動ごとの上限"),
                        Notes = Texts("Note: 0 means no limit", "※0で上限なし"),
                        Minimum = 0,
                        Maximum = 1000,
                        DefaultValue = 0
                    },
                    new PluginTextSetting
                    {
                        Id = PrefixId,
                        Names = Texts("Text before the name", "名前の前の文字")
                    },
                    new PluginSliderSetting
                    {
                        Id = QuietSecondsId,
                        Names = Texts("Seconds between notifications", "通知の間隔(秒)"),
                        Minimum = 0,
                        Maximum = 60,
                        DefaultValue = 0,
                        Step = 5
                    }
                ]
            },
            new PluginNotificationDefinition
            {
                Id = EncounterStartedId,
                Names = Texts("New encounter", "新しい戦闘"),
                DefaultTexts = Texts("A new encounter started.", "新しい戦闘が始まりました。")
            }
        ];
    }

    public void Shutdown()
    {
        if (_context is null)
        {
            return;
        }

        _context.GameData.MapEntered -= GameData_MapEntered;
        _context.GameData.EncounterStarted -= GameData_EncounterStarted;
        _context = null;
    }

    private void GameData_MapEntered(object? sender, EventArgs e)
    {
        if (_context is not { } context || !context.Notifications.IsEnabled(MapEnteredId))
        {
            return;
        }

        var settings = context.Notifications.GetSettings(MapEnteredId);
        var sceneId = context.GameData.Session.SceneId;
        var now = DateTime.UtcNow;

        var isSameMap = sceneId == _lastSceneId;
        _lastSceneId = sceneId;
        if (isSameMap && settings.GetSwitch(SkipSameMapId))
        {
            return;
        }

        var limit = settings.GetNumber(MaxPerLaunchId);
        if ((limit > 0 && _sentCount >= limit)
            || (now - _lastSentUtc).TotalSeconds < settings.GetSlider(QuietSecondsId))
        {
            return;
        }

        var name = sceneId is { } id ? context.Names.GetSceneName(id) : null;
        if (settings.GetChoice(NameFormatId) == NameAndIdOptionId)
        {
            name = $"{name} ({sceneId})";
        }

        _sentCount++;
        _lastSentUtc = now;
        context.Notifications.Notify(MapEnteredId, new Dictionary<string, string>
        {
            ["MapName"] = settings.GetText(PrefixId) + name
        });
    }

    private void GameData_EncounterStarted(object? sender, EventArgs e)
    {
        _context?.Notifications.Notify(EncounterStartedId);
    }

    private static IReadOnlyDictionary<string, string> Texts(string english, string japanese)
    {
        return new Dictionary<string, string> { ["en-US"] = english, ["ja-JP"] = japanese };
    }
}
