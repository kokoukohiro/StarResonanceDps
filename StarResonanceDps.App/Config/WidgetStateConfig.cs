using System.Text.Json.Serialization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.Config;

public sealed class WidgetStateDocument
{
    public int SchemaVersion { get; set; } = WidgetConfigDefaults.CurrentSchemaVersion;

    public Dictionary<string, WidgetConfig> Widgets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WidgetConfig
{
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; }

    public WidgetState? State { get; set; }
    public WidgetThemeConfig Theme { get; set; } = WidgetConfigDefaults.CreateTheme();
    public WidgetWindowConfig Window { get; set; } = new();
    public MeterWidgetSettingsConfig? Meter { get; set; }
    public MetricTimelineWidgetSettingsConfig? MetricTimeline { get; set; }
    public BuffCardWidgetSettingsConfig? BuffCard { get; set; }

    /// <summary>
    /// 前回開いていたウィンドウの対象一覧。プレイヤー用ウィンドウのみ持つ。
    ///
    /// <para>
    /// これが無いと、状態が Running のウィジェットは再起動のたびに
    /// <b>自分の窓1枚</b>として作り直される(対象の指定が失われる)。
    /// </para>
    /// </summary>
    public List<WidgetOpenTargetConfig>? OpenTargets { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    public WidgetConfig Clone()
    {
        return new WidgetConfig
        {
            IsFavorite = IsFavorite,
            IsPinned = IsPinned,
            State = State,
            Theme = Theme?.Clone() ?? WidgetConfigDefaults.CreateTheme(),
            Window = Window?.Clone() ?? new WidgetWindowConfig(),
            Meter = Meter?.Clone(),
            MetricTimeline = MetricTimeline?.Clone(),
            BuffCard = BuffCard?.Clone(),
            OpenTargets = OpenTargets?.Select(target => target.Clone()).ToList(),
            ExtensionData = ExtensionData is null
                ? null
                : new Dictionary<string, object>(ExtensionData, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class WidgetThemeConfig
{
    public int WindowColorIndex { get; set; }
    public int WindowOpacity { get; set; } = 50;
    public List<string> WindowColors { get; set; } = WidgetConfigDefaults.CreateDefaultWindowColors();
    public string? BackgroundImagePath { get; set; }
    public string? BackgroundImageAverageColor { get; set; }
    public string? BackgroundImageAverageColorSourcePath { get; set; }

    /// <summary>
    /// ピン留め中にヘッダーを隠すか。隠している間はドラッグ領域も消える。
    /// <b>保存キーは旧名のまま</b>(変えると保存済みの設定が失われる)。
    /// </summary>
    public bool HideHeaderWhenInactive { get; set; } = true;

    /// <summary>ピン留め中にフッターを隠すか。保存キーは旧名のまま。</summary>
    public bool HideFooterWhenInactive { get; set; }

    public WidgetThemeConfig Clone()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = WindowColorIndex,
            WindowOpacity = WindowOpacity,
            WindowColors = WindowColors is null ? WidgetConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors],
            BackgroundImagePath = BackgroundImagePath,
            BackgroundImageAverageColor = BackgroundImageAverageColor,
            BackgroundImageAverageColorSourcePath = BackgroundImageAverageColorSourcePath,
            HideHeaderWhenInactive = HideHeaderWhenInactive,
            HideFooterWhenInactive = HideFooterWhenInactive
        };
    }
}

public sealed class WidgetWindowConfig
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    public WidgetWindowConfig Clone()
    {
        return new WidgetWindowConfig
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height
        };
    }
}

public sealed class MetricTimelineWidgetSettingsConfig
{
    public int AggregationIntervalSeconds { get; set; } = WidgetConfigDefaults.DefaultMetricTimelineAggregationIntervalSeconds;

    public MetricTimelineWidgetSettingsConfig Clone()
    {
        return new MetricTimelineWidgetSettingsConfig
        {
            AggregationIntervalSeconds = AggregationIntervalSeconds
        };
    }
}

/// <summary>
/// 開いていたウィンドウ1枚ぶんの対象。
///
/// <para>
/// プレイヤーとモンスターの両方を1つの型で表す。<see cref="EntityId"/> が 0 以外なら
/// モンスター、そうでなければプレイヤー。
/// </para>
/// </summary>
public sealed class WidgetOpenTargetConfig
{
    /// <summary>
    /// プレイヤーのID。<c>null</c> は「自分」。
    /// <b>解決後の自分のIDではなく、窓を開いたときの指定をそのまま保存する。</b>
    /// 解決後の値を保存すると、キャラを変えたときに前のキャラの窓として復元される。
    /// </summary>
    public long? CharacterId { get; set; }

    /// <summary>
    /// モンスターの種別ID(<c>MonsterTable</c> のキー)。0 ならプレイヤーの窓。
    /// 実体IDは再起動で消えるので、種別で捕まえ直す。
    /// </summary>
    public long EntityId { get; set; }

    /// <summary>
    /// 最後に分かっていた対象の名前。<b>復元直後にタイトルを正しく出すために持つ。</b>
    /// これが無いと、対象がAOIに現れるまでタイトルがウィジェット名だけになる。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 実際に観測したプレイヤーのUID。<b>表示用</b>で、対象の指定ではない。
    ///
    /// <para>
    /// 自分の窓は <see cref="CharacterId"/> が <c>null</c>(＝「自分」という指定)なので、
    /// これが無いとタイトルに出すUIDが分からない。かといって <see cref="CharacterId"/> を
    /// 観測値で埋めてしまうと、キャラを変えたときに<b>前のキャラ固定の窓として復元される</b>。
    /// 指定と観測値は別に持つ。
    /// </para>
    /// </summary>
    public long? ResolvedCharacterId { get; set; }

    /// <summary>バフ・デバフカード専用。<c>PlayerBuffListKind</c> の値。</summary>
    public int? BuffListKind { get; set; }

    /// <summary>バフ・デバフカード専用。まとまりを追う窓では <c>null</c>。</summary>
    public string? BuffKey { get; set; }

    /// <summary>
    /// バフ・デバフカード専用。最後に分かっていたバフ名。<b>復元直後のタイトルに使う。</b>
    ///
    /// <para>
    /// 個別のバフを追うカードは、そのバフが失効しているとタイトルを組み直せない。
    /// まとまり(料理・薬剤)を追うカードは名前が静的なので、これが無くても出る。
    /// </para>
    /// </summary>
    public string? BuffName { get; set; }

    /// <summary>
    /// バフ・デバフカード専用。<c>BuffGroup</c> の値。0(None)なら個別のバフを追う窓。
    /// </summary>
    public int? BuffGroup { get; set; }

    /// <summary>
    /// この窓の位置と大きさ。<b>ウィンドウ1枚ごと</b>に持つ。
    ///
    /// <para>
    /// <c>WidgetConfig.Window</c> は種別ごとに1組しか無いため、同じウィジェットを複数開くと
    /// 最後に動かした窓が全部を上書きし、次の起動で全員が同じ位置に出る。
    /// <c>null</c> のときは従来どおり種別の位置とカスケードを使う。
    /// </para>
    /// </summary>
    public WidgetWindowConfig? Window { get; set; }

    public bool IsEntity => EntityId != 0;

    public WidgetOpenTargetConfig Clone()
    {
        return new WidgetOpenTargetConfig
        {
            CharacterId = CharacterId,
            EntityId = EntityId,
            Name = Name,
            ResolvedCharacterId = ResolvedCharacterId,
            BuffListKind = BuffListKind,
            BuffKey = BuffKey,
            BuffName = BuffName,
            BuffGroup = BuffGroup,
            Window = Window?.Clone()
        };
    }
}

public sealed class BuffCardWidgetSettingsConfig
{
    public string? BuffInfoFormatString { get; set; }

    /// <summary>
    /// 倍率(%)。キーは <c>{対象ID}:{バフキー}</c>。
    ///
    /// <para>
    /// 対象IDはプレイヤーなら <c>CharacterId</c>、モンスターなら
    /// <c>NearbyEntityEntry.EntityId</c>(= <c>MonsterTable</c> のキー = <b>種別</b>)。
    /// どちらも再起動をまたいで同じ値になる。
    /// </para>
    /// </summary>
    public Dictionary<string, int> Scales { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public BuffCardWidgetSettingsConfig Clone()
    {
        return new BuffCardWidgetSettingsConfig
        {
            BuffInfoFormatString = BuffInfoFormatString,
            Scales = Scales is null
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(Scales, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class MeterWidgetSettingsConfig
{
    public string? PlayerInfoFormatString { get; set; }

    public int HealthValueDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    public int PartyDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultPartyDisplayModeIndex;

    /// <summary>自分の行の見せ方。0=強調表示 / 1=通常表示。</summary>
    public int SelfDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

    public int ClassColorOpacity { get; set; } = WidgetConfigDefaults.MaxClassColorOpacity;

    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes();

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.PlayerList);

    /// <summary>
    /// 他人のロールスキルを表示するか。キーはスキルID。
    /// オンにしたものだけを他人のプレイヤーリストに出す。<b>自分は対象外。</b>
    /// </summary>
    public Dictionary<string, bool> OtherRoleSkillVisibility { get; set; } =
        WidgetConfigDefaults.CreateDefaultOtherRoleSkillVisibility();

    /// <summary>
    /// クラスカラーにフィルター(レンズ)を掛けるか。
    /// 掛かるのはウィジェットの表示だけで、<b>設定画面の色見本は素のまま</b>。
    /// </summary>
    /// <remarks>
    /// <c>null</c> は「設定されていない」。ウィジェット種別ごとの既定は
    /// <see cref="WidgetConfigDefaults.NormalizeMeter"/> で埋める。
    /// bool のままだと、この設定が無かった頃のファイルと「明示的にオフ」を区別できない。
    /// </remarks>
    public bool? ClassColorFilterEnabled { get; set; }

    /// <summary>フィルター色のパレット。クラスカラーと同じく最大5枠。</summary>
    public List<string>? ClassColorFilterColors { get; set; }

    public int ClassColorFilterColorIndex { get; set; }

    /// <summary>フィルター色をどれだけ反映するか(0〜100)。レンズの濃さ。</summary>
    public int ClassColorFilterStrength { get; set; } =
        WidgetConfigDefaults.DefaultClassColorFilterStrength;

    public MeterWidgetSettingsConfig Clone()
    {
        return new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString,
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            PartyDisplayModeIndex = PartyDisplayModeIndex,
            SelfDisplayModeIndex = SelfDisplayModeIndex,
            ClassColorOpacity = ClassColorOpacity,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes()
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.PlayerList)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase),
            OtherRoleSkillVisibility = OtherRoleSkillVisibility is null
                ? WidgetConfigDefaults.CreateDefaultOtherRoleSkillVisibility()
                : new Dictionary<string, bool>(OtherRoleSkillVisibility, StringComparer.OrdinalIgnoreCase),
            ClassColorFilterEnabled = ClassColorFilterEnabled,
            ClassColorFilterColors = ClassColorFilterColors is null ? null : [.. ClassColorFilterColors],
            ClassColorFilterColorIndex = ClassColorFilterColorIndex,
            ClassColorFilterStrength = ClassColorFilterStrength
        };
    }
}

public static class WidgetConfigDefaults
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxPaletteColorCount = 5;
    public const int MinColorIndex = 0;
    public const int MinWindowOpacity = 0;
    public const int MaxWindowOpacity = 100;
    public const int MinClassColorIndex = 0;
    public const int MinClassColorOpacity = 0;
    public const int MaxClassColorOpacity = 100;
    public const int DefaultMetricTimelineAggregationIntervalSeconds = 10;
    public const int DefaultHealthValueDisplayModeIndex = 0;
    public const int SeparateShieldHealthValueDisplayModeIndex = 1;
    public const int DefaultPartyDisplayModeIndex = 0;
    public const int MaxPartyDisplayModeIndex = 3;
    public const int DefaultSelfDisplayModeIndex = 0;
    public const int MaxSelfDisplayModeIndex = 1;
    public const int MinClassColorFilterStrength = 0;
    public const int MaxClassColorFilterStrength = 100;
    public const int DefaultClassColorFilterStrength = 50;
    public const string DefaultEntityInfoFormatString = "Lv.{Level} {Name}";
    public const string DefaultMeterPlayerInfoFormatString = "{Name} - {Spec} ({PowerLevel}-{SeasonStrength})";
    public const string DefaultPlayerListPlayerInfoFormatString = "{Name}({PowerLevel}-{SeasonStrength})";
    public const string DefaultBuffInfoFormatString = "{BuffName}({Name})";

    public const int MinBuffCardScale = 100;
    public const int MaxBuffCardScale = 1000;
    public const int DefaultBuffCardScale = 200;
    public const int BuffCardScaleStep = 25;

    private const double PlayerListInitialWindowWidth = 360d;
    private const double PlayerListInitialWindowHeight = 400d;
    private const double PlayerInfoInitialWindowWidth = 360d;
    private const double PlayerInfoInitialWindowHeight = 200d;
    private const double PlayerStatusInitialWindowWidth = 400d;
    private const double PlayerStatusInitialWindowHeight = 230d;
    private const double PlayerEquipmentInitialWindowWidth = 400d;
    private const double PlayerEquipmentInitialWindowHeight = 230d;
    private const double PlayerBuffListInitialWindowWidth = 360d;
    // 1行1件になったので、初回に開いた時点で数行ぶんが見える高さにする(1行34px)。
    private const double PlayerBuffListInitialWindowHeight = 240d;
    private const double BuffDebuffCardInitialWindowWidth = 260d;
    private const double BuffDebuffCardInitialWindowHeight = 260d;
    private const double MetricContributionInitialWindowWidth = 980d;
    private const double MetricContributionInitialWindowHeight = 360d;
    private const double MetricSummaryInitialWindowWidth = 720d;
    private const double MetricSummaryInitialWindowHeight = 180d;
    private const double MetricTimelineInitialWindowWidth = 980d;
    private const double MetricTimelineInitialWindowHeight = 420d;

    public static IReadOnlyList<int> MetricTimelineAggregationIntervals { get; } = [10, 5, 3, 2, 1];

    /// <summary>
    /// 設定に並べるロールスキル。全20種。
    ///
    /// <para>
    /// 3021〜3028 は全職務共通で、レベルを4段階持つ(<c>SkillFightLevelTable</c> で確認)。
    /// 残り12件は職務専用で、ゲームのスキル表で <c>SlotPositionId</c> に 21〜24 を持つもの。
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> OtherRoleSkillIds { get; } =
    [
        3021, 3022, 3023, 3024, 3025, 3026, 3027, 3028,
        3611, 3612, 3613, 3614,
        3011, 3012, 3013, 3014,
        3311, 3312, 3313, 3314
    ];

    /// <summary>
    /// 既定でオンにするロールスキル。
    ///
    /// <para>
    /// 3021 Thunderfall Grasp / 3027 Blessing of Life / 3028 Guardian's Boundary /
    /// 3312 Renewal Prayer の4件。
    /// </para>
    /// </summary>
    private static readonly int[] DefaultVisibleOtherRoleSkillIds = [3021, 3027, 3028, 3312];

    /// <summary>既定でオンかどうか。</summary>
    public static bool IsOtherRoleSkillVisibleByDefault(int skillId)
    {
        return Array.IndexOf(DefaultVisibleOtherRoleSkillIds, skillId) >= 0;
    }

    /// <summary>既定は上の4件だけオン。残りはオフ。</summary>
    public static Dictionary<string, bool> CreateDefaultOtherRoleSkillVisibility()
    {
        var result = new Dictionary<string, bool>(
            OtherRoleSkillIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var skillId in OtherRoleSkillIds)
        {
            result[skillId.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                IsOtherRoleSkillVisibleByDefault(skillId);
        }

        return result;
    }

    private static readonly string[] DefaultWindowColorHexes =
    [
        "#1F1F1F",
        "#FCFCFC"
    ];

    public static readonly string[] ClassColorKeys =
    [
        "ShieldKnight",
        "HeavyGuardian",
        "VerdantOracle",
        "SoulMusician",
        "FlameBerserker",
        "Stormblade",
        "FrostMage",
        "WindKnight",
        "Marksman",
        "Transformation",
        "Unknown"
    ];

    public static readonly string[] EntityClassColorKeys =
    [
        "Monster",
        "Elite",
        "Boss",
        "Unknown"
    ];

    private static readonly Dictionary<string, string[]> PlayerListDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShieldKnight"] = ["#68A6CD", "#0F68B3"],
        ["HeavyGuardian"] = ["#68A6CD", "#08A0DC"],
        ["VerdantOracle"] = ["#83C49A", "#32BF0F"],
        ["SoulMusician"] = ["#83C49A", "#1F9F0E"],
        ["FlameBerserker"] = ["#DB8787", "#B33000"],
        ["Stormblade"] = ["#DB8787", "#6B39DE"],
        ["FrostMage"] = ["#DB8787", "#5C82E1"],
        ["WindKnight"] = ["#DB8787", "#11B5B2"],
        ["Marksman"] = ["#DB8787", "#D4D116"],
        ["Transformation"] = ["#FFFFFF", "#B06BE8"],
        ["Unknown"] = ["#FFFFFF", "#A8A8A8"]
    };

    private static readonly Dictionary<string, string[]> EntityListDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Monster"] = ["#FFFFFF", "#FFB15C"],
        ["Elite"] = ["#FFFFFF", "#C490FF"],
        ["Boss"] = ["#FFFFFF", "#FF6A6A"],
        ["Unknown"] = ["#FFFFFF", "#A8A8A8"]
    };

    private static readonly Dictionary<string, string[]> MeterDefaultClassColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShieldKnight"] = ["#0F68B3", "#08406F"],
        ["HeavyGuardian"] = ["#08A0DC", "#056482"],
        ["VerdantOracle"] = ["#32BF0F", "#1D7410"],
        ["SoulMusician"] = ["#1F9F0E", "#145F0A"],
        ["FlameBerserker"] = ["#B33000", "#6F1F00"],
        ["Stormblade"] = ["#6B39DE", "#3F2485"],
        ["FrostMage"] = ["#5C82E1", "#355094"],
        ["WindKnight"] = ["#11B5B2", "#0A6E6C"],
        ["Marksman"] = ["#D4D116", "#8A8810"],
        ["Transformation"] = ["#B06BE8", "#6E3A9C"],
        ["Unknown"] = ["#A8A8A8", "#707070"]
    };

    private static readonly Dictionary<string, string[]> HpsMeterDefaultClassColorHexes = MeterDefaultClassColorHexes;

    private static readonly HashSet<string> LegacyWidgetWindowColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        "#2297F4",
        "#7C5CFF",
        "#9FD14A",
        "#FF9F2E",
        "#F05284",
        "#000000",
        "#FFFFFF"
    };

    public static bool SupportsMeterSettings(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerList
            or WidgetKind.EntityList
            or WidgetKind.DpsMeter
            or WidgetKind.HpsMeter;
    }

    public static bool UsesMeterClassColorOpacity(WidgetKind kind)
    {
        return kind is WidgetKind.DpsMeter or WidgetKind.HpsMeter;
    }

    public static bool SupportsMetricTimelineSettings(WidgetKind kind)
    {
        return kind is WidgetKind.DpsGraph or WidgetKind.HpsGraph;
    }

    public static bool SupportsBuffCardSettings(WidgetKind kind)
    {
        return kind is WidgetKind.BuffDebuffCard;
    }

    /// <summary>
    /// 開いていた窓の対象を保存する種別か。プレイヤー用ウィンドウだけが持つ。
    /// <see cref="WidgetKind.PlayerStatus"/> は常に自分1枚なので対象外。
    /// </summary>
    public static bool SupportsOpenTargets(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerInfo
            or WidgetKind.PlayerEquipment
            or WidgetKind.BuffList
            or WidgetKind.DebuffList
            or WidgetKind.BuffDebuffCard
            or WidgetKind.DamageContribution
            or WidgetKind.DamageSummary
            or WidgetKind.DpsGraph
            or WidgetKind.HealingContribution
            or WidgetKind.HealingSummary
            or WidgetKind.HpsGraph;
    }

    public static void MigrateVersion1Defaults(WidgetConfig config)
    {
        config.Theme ??= CreateTheme();

        if (UsesLegacyWindowColorPalette(config.Theme.WindowColors))
        {
            config.Theme.WindowColors = CreateDefaultWindowColors();
            config.Theme.WindowColorIndex = MinColorIndex;
        }
    }

    public static void MigratePlayerListFormatDefault(WidgetConfig config)
    {
        if (config.Meter is not { } meter
            || !string.Equals(
                meter.PlayerInfoFormatString,
                DefaultMeterPlayerInfoFormatString,
                StringComparison.Ordinal))
        {
            return;
        }

        meter.PlayerInfoFormatString = DefaultPlayerListPlayerInfoFormatString;
    }

    private static bool UsesLegacyWindowColorPalette(IEnumerable<string>? colors)
    {
        if (colors is null)
        {
            return false;
        }

        var normalized = new List<string>();
        foreach (var color in colors)
        {
            if (!TryNormalizeHexColor(color, out var value))
            {
                return false;
            }

            normalized.Add(value);
        }

        return normalized.Count == MaxPaletteColorCount
            && normalized.All(LegacyWidgetWindowColorHexes.Contains);
    }

    public static WidgetConfig Create(WidgetKind kind)
    {
        return new WidgetConfig
        {
            IsFavorite = false,
            IsPinned = false,
            State = WidgetState.Stopped,
            Theme = CreateTheme(),
            Window = CreateDefaultWindowConfig(kind),
            Meter = SupportsMeterSettings(kind) ? CreateMeterSettings(kind) : null,
            MetricTimeline = SupportsMetricTimelineSettings(kind) ? CreateMetricTimelineSettings() : null
        };
    }

    private static WidgetWindowConfig CreateDefaultWindowConfig(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.PlayerList or WidgetKind.EntityList => new WidgetWindowConfig
            {
                Width = PlayerListInitialWindowWidth,
                Height = PlayerListInitialWindowHeight
            },
            WidgetKind.DpsMeter or WidgetKind.HpsMeter => new WidgetWindowConfig
            {
                Width = PlayerListInitialWindowWidth,
                Height = PlayerListInitialWindowHeight
            },
            WidgetKind.PlayerInfo => new WidgetWindowConfig
            {
                Width = PlayerInfoInitialWindowWidth,
                Height = PlayerInfoInitialWindowHeight
            },
            WidgetKind.PlayerStatus => new WidgetWindowConfig
            {
                Width = PlayerStatusInitialWindowWidth,
                Height = PlayerStatusInitialWindowHeight
            },
            WidgetKind.PlayerEquipment => new WidgetWindowConfig
            {
                Width = PlayerEquipmentInitialWindowWidth,
                Height = PlayerEquipmentInitialWindowHeight
            },
            WidgetKind.BuffList or WidgetKind.DebuffList => new WidgetWindowConfig
            {
                Width = PlayerBuffListInitialWindowWidth,
                Height = PlayerBuffListInitialWindowHeight
            },
            WidgetKind.BuffDebuffCard => new WidgetWindowConfig
            {
                Width = BuffDebuffCardInitialWindowWidth,
                Height = BuffDebuffCardInitialWindowHeight
            },
            WidgetKind.DamageContribution or WidgetKind.HealingContribution or WidgetKind.TakenDamageLog => new WidgetWindowConfig
            {
                Width = MetricContributionInitialWindowWidth,
                Height = MetricContributionInitialWindowHeight
            },
            WidgetKind.DamageSummary or WidgetKind.HealingSummary => new WidgetWindowConfig
            {
                Width = MetricSummaryInitialWindowWidth,
                Height = MetricSummaryInitialWindowHeight
            },
            WidgetKind.DpsGraph or WidgetKind.HpsGraph => new WidgetWindowConfig
            {
                Width = MetricTimelineInitialWindowWidth,
                Height = MetricTimelineInitialWindowHeight
            },
            _ => new WidgetWindowConfig()
        };
    }

    public static WidgetThemeConfig CreateTheme()
    {
        return new WidgetThemeConfig
        {
            WindowColorIndex = 0,
            WindowOpacity = 50,
            WindowColors = CreateDefaultWindowColors(),
            HideHeaderWhenInactive = true
        };
    }

    public static MetricTimelineWidgetSettingsConfig CreateMetricTimelineSettings()
    {
        return new MetricTimelineWidgetSettingsConfig
        {
            AggregationIntervalSeconds = DefaultMetricTimelineAggregationIntervalSeconds
        };
    }

    public static BuffCardWidgetSettingsConfig CreateBuffCardSettings()
    {
        return new BuffCardWidgetSettingsConfig
        {
            BuffInfoFormatString = DefaultBuffInfoFormatString
        };
    }

    public static MeterWidgetSettingsConfig CreateMeterSettings(WidgetKind kind)
    {
        return new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = GetDefaultPlayerInfoFormatString(kind),
            HealthValueDisplayModeIndex = DefaultHealthValueDisplayModeIndex,
            PartyDisplayModeIndex = DefaultPartyDisplayModeIndex,
            ClassColorOpacity = MaxClassColorOpacity,
            ClassColorFilterEnabled = IsClassColorFilterEnabledByDefault(kind),
            ClassColorFilterColors = CreateDefaultClassColorFilterColors(kind),
            ClassColorFilterStrength = DefaultClassColorFilterStrength,
            ClassColorIndexes = CreateDefaultClassColorIndexes(kind),
            ClassColorPalettes = CreateDefaultClassColorPalettes(kind)
        };
    }

    public static List<string> CreateDefaultWindowColors()
    {
        return [.. DefaultWindowColorHexes];
    }

    public static string GetDefaultPlayerInfoFormatString(WidgetKind kind)
    {
        return kind switch
        {
            WidgetKind.EntityList => DefaultEntityInfoFormatString,
            WidgetKind.PlayerList => DefaultPlayerListPlayerInfoFormatString,
            _ => DefaultMeterPlayerInfoFormatString
        };
    }

    public static IReadOnlyList<string> GetClassColorKeys(WidgetKind kind)
    {
        return kind == WidgetKind.EntityList
            ? EntityClassColorKeys
            : ClassColorKeys;
    }

    /// <summary>
    /// クラスカラーで最初に選ばれている枠。HPSのビートパフォーマーだけ2枠目。
    /// </summary>
    public static int GetDefaultClassColorIndex(WidgetKind kind, string key)
    {
        return kind == WidgetKind.HpsMeter
            && string.Equals(key, "SoulMusician", StringComparison.OrdinalIgnoreCase)
            ? MinClassColorIndex + 1
            : MinClassColorIndex;
    }

    public static Dictionary<string, int> CreateDefaultClassColorIndexes(WidgetKind kind = WidgetKind.PlayerList)
    {
        return GetClassColorKeys(kind)
            .ToDictionary(key => key, key => GetDefaultClassColorIndex(kind, key), StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultClassColorPalettes(WidgetKind kind)
    {
        return GetClassColorKeys(kind).ToDictionary(
            key => key,
            key => CreateDefaultClassColors(kind, key),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// フッターを持つウィジェット。持たないウィジェットでは「ピン留め中フッターを隠す」を出さない。
    ///
    /// <para>
    /// <b>実際にフッターを作っているのは <c>WidgetWindowManager.CreateWidgetWindowComposition</c>。</b>
    /// 増減させたらここも合わせること。
    /// </para>
    /// </summary>
    public static bool HasFooter(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerList
            or WidgetKind.EntityList
            or WidgetKind.DpsMeter
            or WidgetKind.HpsMeter;
    }

    /// <summary>クラスカラーのフィルターを持つウィジェット。</summary>
    public static bool UsesClassColorFilter(WidgetKind kind)
    {
        return kind is WidgetKind.DpsMeter or WidgetKind.HpsMeter;
    }

    /// <summary>フィルターの既定の有効/無効。HPSだけ既定で有効。</summary>
    public static bool IsClassColorFilterEnabledByDefault(WidgetKind kind)
    {
        return kind == WidgetKind.HpsMeter;
    }

    /// <summary>フィルター色の既定パレット。DPSは赤系、HPSは緑系。</summary>
    public static List<string> CreateDefaultClassColorFilterColors(WidgetKind kind)
    {
        return kind == WidgetKind.HpsMeter
            ? ["#43D978", "#227A20"]
            : ["#B33000", "#6F1F00"];
    }

    public static List<string> CreateDefaultClassColors(WidgetKind kind, string key)
    {
        var source = kind switch
        {
            WidgetKind.PlayerList => PlayerListDefaultClassColorHexes,
            WidgetKind.EntityList => EntityListDefaultClassColorHexes,
            WidgetKind.HpsMeter => HpsMeterDefaultClassColorHexes,
            _ => MeterDefaultClassColorHexes
        };

        return source.TryGetValue(key, out var colors)
            ? [.. colors]
            : ["#A8A8A8", "#707070"];
    }

    public static WidgetConfig CloneNormalized(WidgetKind kind, WidgetConfig? config)
    {
        var normalized = (config ?? Create(kind)).Clone();
        Normalize(kind, normalized);
        return normalized;
    }

    public static WidgetThemeConfig CloneNormalizedTheme(WidgetThemeConfig? theme)
    {
        var normalized = (theme ?? CreateTheme()).Clone();
        NormalizeTheme(normalized);
        return normalized;
    }

    public static MeterWidgetSettingsConfig CloneNormalizedMeter(WidgetKind kind, MeterWidgetSettingsConfig? meter)
    {
        var normalized = (meter ?? CreateMeterSettings(kind)).Clone();
        NormalizeMeter(kind, normalized);
        return normalized;
    }

    public static MetricTimelineWidgetSettingsConfig CloneNormalizedMetricTimeline(MetricTimelineWidgetSettingsConfig? metricTimeline)
    {
        var normalized = (metricTimeline ?? CreateMetricTimelineSettings()).Clone();
        NormalizeMetricTimeline(normalized);
        return normalized;
    }

    public static BuffCardWidgetSettingsConfig CloneNormalizedBuffCard(BuffCardWidgetSettingsConfig? buffCard)
    {
        var normalized = (buffCard ?? CreateBuffCardSettings()).Clone();
        NormalizeBuffCard(normalized);
        return normalized;
    }

    public static void Normalize(WidgetConfig config)
    {
        config.Theme ??= CreateTheme();
        config.Window ??= new WidgetWindowConfig();

        if (config.State is { } state
            && state is not WidgetState.Stopped
            && state is not WidgetState.Running)
        {
            config.State = WidgetState.Stopped;
        }

        NormalizeTheme(config.Theme);
    }

    public static void Normalize(WidgetKind kind, WidgetConfig config)
    {
        Normalize(config);
        config.Meter = SupportsMeterSettings(kind)
            ? CloneNormalizedMeter(kind, config.Meter)
            : null;
        config.MetricTimeline = SupportsMetricTimelineSettings(kind)
            ? CloneNormalizedMetricTimeline(config.MetricTimeline)
            : null;
        config.BuffCard = SupportsBuffCardSettings(kind)
            ? CloneNormalizedBuffCard(config.BuffCard)
            : null;
        config.OpenTargets = SupportsOpenTargets(kind)
            ? config.OpenTargets
            : null;

        switch (kind)
        {
            case WidgetKind.PlayerList:
            case WidgetKind.EntityList:
                config.Window.Width ??= PlayerListInitialWindowWidth;
                config.Window.Height ??= PlayerListInitialWindowHeight;
                break;

            case WidgetKind.DpsMeter:
            case WidgetKind.HpsMeter:
                config.Window.Width ??= PlayerListInitialWindowWidth;
                config.Window.Height ??= PlayerListInitialWindowHeight;
                break;

            case WidgetKind.PlayerInfo:
                config.Window.Width ??= PlayerInfoInitialWindowWidth;
                config.Window.Height ??= PlayerInfoInitialWindowHeight;
                break;

            case WidgetKind.PlayerStatus:
                config.Window.Width ??= PlayerStatusInitialWindowWidth;
                config.Window.Height ??= PlayerStatusInitialWindowHeight;
                break;

            case WidgetKind.PlayerEquipment:
                config.Window.Width ??= PlayerEquipmentInitialWindowWidth;
                config.Window.Height ??= PlayerEquipmentInitialWindowHeight;
                break;
            case WidgetKind.BuffList:
            case WidgetKind.DebuffList:
                config.Window.Width ??= PlayerBuffListInitialWindowWidth;
                config.Window.Height ??= PlayerBuffListInitialWindowHeight;
                break;
            case WidgetKind.BuffDebuffCard:
                config.Window.Width ??= BuffDebuffCardInitialWindowWidth;
                config.Window.Height ??= BuffDebuffCardInitialWindowHeight;
                break;
            case WidgetKind.DamageContribution:
            case WidgetKind.HealingContribution:
            case WidgetKind.TakenDamageLog:
                config.Window.Width ??= MetricContributionInitialWindowWidth;
                config.Window.Height ??= MetricContributionInitialWindowHeight;
                break;
            case WidgetKind.DamageSummary:
            case WidgetKind.HealingSummary:
                config.Window.Width ??= MetricSummaryInitialWindowWidth;
                config.Window.Height ??= MetricSummaryInitialWindowHeight;
                break;
            case WidgetKind.DpsGraph:
            case WidgetKind.HpsGraph:
                config.Window.Width ??= MetricTimelineInitialWindowWidth;
                config.Window.Height ??= MetricTimelineInitialWindowHeight;
                break;
        }
    }

    public static void NormalizeTheme(WidgetThemeConfig theme)
    {
        theme.WindowColors = NormalizeColorList(theme.WindowColors, DefaultWindowColorHexes, MaxPaletteColorCount);
        theme.WindowColorIndex = Math.Clamp(theme.WindowColorIndex, MinColorIndex, theme.WindowColors.Count - 1);
        theme.WindowOpacity = Math.Clamp(theme.WindowOpacity, MinWindowOpacity, MaxWindowOpacity);
        theme.BackgroundImagePath = string.IsNullOrWhiteSpace(theme.BackgroundImagePath)
            ? null
            : theme.BackgroundImagePath.Trim();

        if (theme.BackgroundImagePath is null)
        {
            theme.BackgroundImageAverageColor = null;
            theme.BackgroundImageAverageColorSourcePath = null;
            return;
        }

        theme.BackgroundImageAverageColor = TryNormalizeHexColor(theme.BackgroundImageAverageColor, out var averageColor)
            ? averageColor
            : null;
        theme.BackgroundImageAverageColorSourcePath = string.IsNullOrWhiteSpace(theme.BackgroundImageAverageColorSourcePath)
            ? null
            : theme.BackgroundImageAverageColorSourcePath.Trim();
    }

    public static void NormalizeMetricTimeline(MetricTimelineWidgetSettingsConfig metricTimeline)
    {
        if (!MetricTimelineAggregationIntervals.Contains(metricTimeline.AggregationIntervalSeconds))
        {
            metricTimeline.AggregationIntervalSeconds = DefaultMetricTimelineAggregationIntervalSeconds;
        }
    }

    public static void NormalizeBuffCard(BuffCardWidgetSettingsConfig buffCard)
    {
        buffCard.BuffInfoFormatString ??= DefaultBuffInfoFormatString;
        buffCard.Scales ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in buffCard.Scales.Keys.ToList())
        {
            buffCard.Scales[key] = ClampBuffCardScale(buffCard.Scales[key]);
        }
    }

    public static int ClampBuffCardScale(int scale)
    {
        var stepped = (int)Math.Round(
            (double)scale / BuffCardScaleStep,
            MidpointRounding.AwayFromZero) * BuffCardScaleStep;

        return Math.Clamp(stepped, MinBuffCardScale, MaxBuffCardScale);
    }

    public static void NormalizeMeter(WidgetKind kind, MeterWidgetSettingsConfig meter)
    {
        meter.PlayerInfoFormatString ??= GetDefaultPlayerInfoFormatString(kind);
        meter.HealthValueDisplayModeIndex = Math.Clamp(
            meter.HealthValueDisplayModeIndex,
            DefaultHealthValueDisplayModeIndex,
            SeparateShieldHealthValueDisplayModeIndex);
        meter.PartyDisplayModeIndex = Math.Clamp(
            meter.PartyDisplayModeIndex,
            DefaultPartyDisplayModeIndex,
            MaxPartyDisplayModeIndex);
        meter.SelfDisplayModeIndex = Math.Clamp(
            meter.SelfDisplayModeIndex,
            DefaultSelfDisplayModeIndex,
            MaxSelfDisplayModeIndex);

        // クラスカラーのフィルター。持たないウィジェットでは常に無効に倒す。
        var filterDefaults = CreateDefaultClassColorFilterColors(kind);
        meter.ClassColorFilterEnabled = UsesClassColorFilter(kind)
            && (meter.ClassColorFilterEnabled ?? IsClassColorFilterEnabledByDefault(kind));
        meter.ClassColorFilterColors = NormalizeColorList(
            meter.ClassColorFilterColors ?? filterDefaults,
            filterDefaults,
            MaxPaletteColorCount);
        meter.ClassColorFilterColorIndex = Math.Clamp(
            meter.ClassColorFilterColorIndex,
            MinClassColorIndex,
            meter.ClassColorFilterColors.Count - 1);
        meter.ClassColorFilterStrength = Math.Clamp(
            meter.ClassColorFilterStrength,
            MinClassColorFilterStrength,
            MaxClassColorFilterStrength);
        meter.ClassColorOpacity = UsesMeterClassColorOpacity(kind)
            ? Math.Clamp(meter.ClassColorOpacity, MinClassColorOpacity, MaxClassColorOpacity)
            : MaxClassColorOpacity;
        meter.ClassColorIndexes ??= CreateDefaultClassColorIndexes(kind);
        meter.ClassColorPalettes ??= CreateDefaultClassColorPalettes(kind);

        // 20件ぶんのキーを必ず揃える。設定に無いスキルはそのスキルの既定値で埋める。
        var normalizedRoleSkills = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var skillId in OtherRoleSkillIds)
        {
            var key = skillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            normalizedRoleSkills[key] =
                meter.OtherRoleSkillVisibility is not null
                && meter.OtherRoleSkillVisibility.TryGetValue(key, out var visible)
                    ? visible
                    : IsOtherRoleSkillVisibleByDefault(skillId);
        }

        meter.OtherRoleSkillVisibility = normalizedRoleSkills;

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in GetClassColorKeys(kind))
        {
            var defaultColors = CreateDefaultClassColors(kind, key);
            var sourceColors = meter.ClassColorPalettes.TryGetValue(key, out var colors)
                ? colors
                : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = meter.ClassColorIndexes.TryGetValue(key, out var index)
                ? index
                : GetDefaultClassColorIndex(kind, key);
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        meter.ClassColorIndexes = normalizedIndexes;
        meter.ClassColorPalettes = normalizedPalettes;
    }

    public static string GetKey(WidgetKind kind)
    {
        return kind.ToString();
    }

    private static List<string> NormalizeColorList(IEnumerable<string>? colors, IEnumerable<string> fallback, int maxCount)
    {
        var result = new List<string>();

        if (colors is not null)
        {
            foreach (var color in colors)
            {
                if (!TryNormalizeHexColor(color, out var normalized)
                    || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }
        }

        if (result.Count == 0)
        {
            foreach (var color in fallback)
            {
                if (!TryNormalizeHexColor(color, out var normalized)
                    || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }
        }

        if (result.Count == 0)
        {
            result.Add("#FFFFFF");
        }

        return result;
    }

    private static bool TryNormalizeHexColor(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        normalized = "#" + text.ToUpperInvariant();
        return true;
    }
}
