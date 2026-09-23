using System.Text.Json.Serialization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.Models;

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
    public TakenDamageLogWidgetSettingsConfig? TakenDamageLog { get; set; }
    public BuffListWidgetSettingsConfig? BuffList { get; set; }
    public ElementColorWidgetSettingsConfig? ElementColor { get; set; }

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
            TakenDamageLog = TakenDamageLog?.Clone(),
            BuffList = BuffList?.Clone(),
            ElementColor = ElementColor?.Clone(),
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

public sealed class TakenDamageLogWidgetSettingsConfig
{
    /// <summary>
    /// 被弾行の HP の出し方。0=バリア量加算表示 / 1=バリア量個別表示。
    /// プレイヤーリスト・エンティティリストと同じ規則。
    /// </summary>
    public int HealthValueDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    /// <summary>
    /// フィルター。0=すべて表示 / 1=自傷・フレンドリーファイア以外(加害者がプレイヤーの被弾を出さない)。
    /// 以前のフィルター(撤去済み)の <c>FilterIndex</c> とは別の名前にしてある。古い保存の値を拾わないため。
    /// </summary>
    public int AttackerFilterIndex { get; set; } = WidgetConfigDefaults.DefaultTakenDamageLogAttackerFilterIndex;

    /// <summary>
    /// クラスアイコンの色(クラスカラー)。形はプレイヤーリストのクラスカラーと同じで、被ダメログ専用に持つ。
    /// 既定の色はプレイヤーリストと同じ。フィルターと不透明度は持たない。
    /// </summary>
    public Dictionary<string, int> ClassColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.TakenDamageLog);

    public Dictionary<string, List<string>> ClassColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.TakenDamageLog);

    /// <summary>
    /// 行の文字の色(テキストカラー)。形はクラスカラーと同じで、鍵は <see cref="WidgetConfigDefaults.TakenDamageLogTextColorKeys"/>。
    /// </summary>
    public Dictionary<string, int> TextColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultTextColorIndexes();

    public Dictionary<string, List<string>> TextColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultTextColorPalettes();

    public TakenDamageLogWidgetSettingsConfig Clone()
    {
        return new TakenDamageLogWidgetSettingsConfig
        {
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            AttackerFilterIndex = AttackerFilterIndex,
            ClassColorIndexes = ClassColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorIndexes(WidgetKind.TakenDamageLog)
                : new Dictionary<string, int>(ClassColorIndexes, StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = ClassColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultClassColorPalettes(WidgetKind.TakenDamageLog)
                : ClassColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = TextColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultTextColorIndexes()
                : new Dictionary<string, int>(TextColorIndexes, StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = TextColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultTextColorPalettes()
                : TextColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// バフ・デバフ一覧の設定。行に出すゲージの色と長さ。
///
/// <para>
/// 色は左端と右端の2つで、間はグラデーションになる(形はクラスカラーと同じ「色見本＋選んでいる枠」)。
/// 既定の長さは種別で違う(バフは短く、デバフは長い)ので、作るときに <see cref="WidgetKind"/> を渡す。
/// </para>
/// </summary>
public sealed class BuffListWidgetSettingsConfig
{
    /// <summary>
    /// 選んでいる枠。<b>既定が種別で違う</b>(バフは1枠目、デバフは2枠目)ので、
    /// <see cref="WidgetKind"/> を知らないここでは入れない。空のまま
    /// <see cref="WidgetConfigDefaults.CloneNormalizedBuffList"/> を通ると、そこで種別に応じて埋まる。
    /// </summary>
    public Dictionary<string, int> GaugeColorIndexes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<string>> GaugeColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultGaugeColorPalettes();

    /// <summary>
    /// ゲージの色の不透明度(0〜100)。作りはメーターのクラスカラーと同じで、
    /// 表示に使う色のアルファへ掛ける(設定画面の色見本には掛けない)。
    /// </summary>
    public int GaugeColorOpacity { get; set; } = WidgetConfigDefaults.MaxClassColorOpacity;

    /// <summary>ゲージが満タンになる残り時間。0=10秒 / 1=20秒 / 2=30秒。これ以上は満タンで頭打ち。</summary>
    public int GaugeLengthIndex { get; set; } = WidgetConfigDefaults.DefaultBuffListGaugeLengthIndex;

    public BuffListWidgetSettingsConfig Clone()
    {
        return new BuffListWidgetSettingsConfig
        {
            GaugeLengthIndex = GaugeLengthIndex,
            GaugeColorOpacity = GaugeColorOpacity,
            GaugeColorIndexes = GaugeColorIndexes is null
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(GaugeColorIndexes, StringComparer.OrdinalIgnoreCase),
            GaugeColorPalettes = GaugeColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultGaugeColorPalettes()
                : GaugeColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// 開いていたウィンドウ1枚ぶんの対象。
///
/// <para>
/// プレイヤーとエンティティリストの実体(モンスター)の両方を1つの型で表す。
/// <see cref="EntityId"/> が 0 以外なら実体、そうでなければプレイヤー。
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
    /// 実体の種別ID(<c>AttrId</c>)。0 ならプレイヤーの窓。
    /// 実体IDは再起動で消えるので、種別で捕まえ直す。
    /// </summary>
    public long EntityId { get; set; }

    /// <summary>
    /// 実体の種類(<c>EEntityType</c> の値)。<see cref="EntityId"/> がどの表の番号かを決めるので、
    /// 捕まえ直すときは種類と番号の両方が一致する個体だけを捕まえる。無い保存は捕まえない。
    /// </summary>
    public int? EntityType { get; set; }

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
            EntityType = EntityType,
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

    /// <summary>エンティティリストのフィルター。0=すべて表示 / 1=オブジェクト以外(<c>EntityDisplayMode</c>)。</summary>
    public int EntityDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultEntityDisplayModeIndex;

    /// <summary>自分の行の見せ方。0=強調表示 / 1=通常表示。</summary>
    public int SelfDisplayModeIndex { get; set; } = WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

    /// <summary>
    /// 一覧の並び替え。0=発見順 / 1=名前順。既定はプレイヤーリストが発見順、エンティティリストが名前順。
    /// <b>群(プレイヤーリストの 自分→パーティ→灰色→ライブ、エンティティリストの ボス→精鋭→普通)の順は変えない。</b>
    /// この設定が決めるのは、その群の中の並びだけ。使うのはプレイヤーリストとエンティティリストだけ。
    /// </summary>
    public int ListSortModeIndex { get; set; } = WidgetConfigDefaults.FirstSeenListSortModeIndex;

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
            EntityDisplayModeIndex = EntityDisplayModeIndex,
            SelfDisplayModeIndex = SelfDisplayModeIndex,
            ListSortModeIndex = ListSortModeIndex,
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

/// <summary>
/// スキル詳細の属性カラー。作りはメーターのクラスカラーと同じで、行が9属性になる。
///
/// <para>
/// ウィジェットが実際に使う色は<b>その行に出た属性の割合で混ぜたもの</b>で、そこへフィルターと
/// 不透明度が掛かる。<b>設定画面の色見本にはどちらも掛けない</b>(クラスカラーと同じ)。
/// </para>
/// </summary>
public sealed class ElementColorWidgetSettingsConfig
{
    public int ColorOpacity { get; set; } = WidgetConfigDefaults.MaxClassColorOpacity;

    public Dictionary<string, int> ColorIndexes { get; set; } = WidgetConfigDefaults.CreateDefaultElementColorIndexes();

    public Dictionary<string, List<string>> ColorPalettes { get; set; } = WidgetConfigDefaults.CreateDefaultElementColorPalettes();

    /// <remarks>
    /// <c>null</c> は「設定されていない」。ウィジェット種別ごとの既定は
    /// <see cref="WidgetConfigDefaults.NormalizeElementColor"/> で埋める。
    /// </remarks>
    public bool? FilterEnabled { get; set; }

    /// <summary>フィルター色のパレット。クラスカラーと同じく最大5枠。</summary>
    public List<string>? FilterColors { get; set; }

    public int FilterColorIndex { get; set; }

    /// <summary>フィルター色をどれだけ反映するか(0〜100)。レンズの濃さ。</summary>
    public int FilterStrength { get; set; } = WidgetConfigDefaults.DefaultClassColorFilterStrength;

    public ElementColorWidgetSettingsConfig Clone()
    {
        return new ElementColorWidgetSettingsConfig
        {
            ColorOpacity = ColorOpacity,
            ColorIndexes = ColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultElementColorIndexes()
                : new Dictionary<string, int>(ColorIndexes, StringComparer.OrdinalIgnoreCase),
            ColorPalettes = ColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultElementColorPalettes()
                : ColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase),
            FilterEnabled = FilterEnabled,
            FilterColors = FilterColors is null ? null : [.. FilterColors],
            FilterColorIndex = FilterColorIndex,
            FilterStrength = FilterStrength
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
    public const int MinEntityDisplayModeIndex = (int)EntityDisplayMode.All;
    public const int MaxEntityDisplayModeIndex = (int)EntityDisplayMode.HideObjects;
    public const int DefaultEntityDisplayModeIndex = (int)EntityDisplayMode.HideObjects;
    public const int DefaultSelfDisplayModeIndex = 0;
    public const int MaxSelfDisplayModeIndex = 1;

    /// <summary>一覧の並び替え: 発見順(群の中は初めて現れた順のまま)。</summary>
    public const int FirstSeenListSortModeIndex = 0;

    /// <summary>一覧の並び替え: 名前順(群の中を画面に出している名前で並べる)。</summary>
    public const int NameListSortModeIndex = 1;
    public const int DefaultTakenDamageLogAttackerFilterIndex = 0;
    public const int NoSelfOrFriendlyFireTakenDamageLogAttackerFilterIndex = 1;
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


    private const double PlayerListInitialWindowWidth = 400d;
    private const double PlayerListInitialWindowHeight = 440d;
    private const double MeterInitialWindowWidth = 400d;
    private const double MeterInitialWindowHeight = 440d;
    private const double TakenDamageLogInitialWindowWidth = 400d;
    private const double TakenDamageLogInitialWindowHeight = 440d;
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
    private const double MetricContributionInitialWindowWidth = 580d;
    private const double MetricContributionInitialWindowHeight = 440d;
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

    /// <summary>ゲージの色の鍵。左端と右端の2つで、間はグラデーションになる。</summary>
    public static readonly string[] BuffListGaugeColorKeys =
    [
        "GaugeStart",
        "GaugeEnd"
    ];

    public const int MinBuffListGaugeLengthIndex = 0;

    /// <summary>ゲージが満タンになる残り時間。添字は 0=10秒 / 1=20秒 / 2=30秒。</summary>
    public static readonly int[] BuffListGaugeLengthSeconds = [10, 20, 30];

    /// <summary>バフ一覧の既定。短いバフを見るので10秒。</summary>
    public const int DefaultBuffListGaugeLengthIndex = 0;

    /// <summary>デバフ一覧の既定。長いデバフを見るので30秒。</summary>
    public const int DefaultDebuffListGaugeLengthIndex = 2;

    /// <summary>
    /// ダメージの属性の鍵。<c>EDamageProperty</c> の並び(属性ID 0〜8)。
    /// 表示名は <c>DamageProperty_*</c>、アイコンは <c>Icon.DamageProperty.*</c> で引く。
    /// </summary>
    public static readonly string[] DamagePropertyKeys =
    [
        "General",
        "Fire",
        "Water",
        "Electricity",
        "Wood",
        "Wind",
        "Rock",
        "Light",
        "Dark"
    ];

    /// <summary>被ダメログのテキストカラーの鍵。属性の9行はこの並びのまま真ん中に入る。</summary>
    public static readonly string[] TakenDamageLogTextColorKeys =
    [
        "EntityName",
        "SkillName",
        "PlayerName",
        .. DamagePropertyKeys,
        "HpValue",
        "ShieldValue",
        "Death"
    ];

    /// <summary>テキストカラーの鍵のうち、ダメージの属性のもの。行の左にアイコンを出す。</summary>
    public static bool IsDamagePropertyKey(string key)
    {
        return DamagePropertyKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

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

    /// <summary>
    /// ゲージの既定色。左から右へこの2色でグラデーションになる。
    ///
    /// <para>
    /// 見本は2枠で、<b>1枠目がバフの青緑、2枠目がデバフの紫</b>。2枠目は1枠目の R と G を入れ替えたもので、
    /// 彩度と明度はそのままに色相だけ反対側へ回る。どちらを選んだ状態で始めるかは
    /// <see cref="GetDefaultGaugeColorIndex"/> が種別で決める。
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string[]> BuffListDefaultGaugeColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GaugeStart"] = ["#5688A4", "#8856A4"],
        ["GaugeEnd"] = ["#56C8C1", "#C856C1"]
    };

    /// <summary>
    /// 被ダメログのテキストカラーの既定色。属性は属性らしい色、それ以外は白と差し色の2枠。
    /// </summary>
    private static readonly Dictionary<string, string[]> TakenDamageLogDefaultTextColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EntityName"] = ["#FFFFFF", "#FFD98A"],
        ["SkillName"] = ["#FFFFFF", "#D9866C"],
        ["PlayerName"] = ["#FFFFFF", "#9FD1FF"],
        ["General"] = ["#FFFFFF", "#D0E1E9"],
        ["Fire"] = ["#FFFFFF", "#FF8100"],
        ["Water"] = ["#FFFFFF", "#9DFFEF"],
        ["Electricity"] = ["#FFFFFF", "#AE86FF"],
        ["Wood"] = ["#FFFFFF", "#CEF700"],
        ["Wind"] = ["#FFFFFF", "#00EFFF"],
        ["Rock"] = ["#FFFFFF", "#F7C600"],
        ["Light"] = ["#FFFFFF", "#F5EFB3"],
        ["Dark"] = ["#FFFFFF", "#734FE1"],
        ["HpValue"] = ["#FFFFFF", "#81E5C6"],
        ["ShieldValue"] = ["#FFFFFF", "#DCDCDC"],
        ["Death"] = ["#FFFFFF", "#FF8080"]
    };

    /// <summary>
    /// スキル詳細の属性カラーの既定色。<b>被ダメログの属性テキストカラーと同じ2枠</b>で、
    /// 既定で選ばれるのも同じく2枠目の差し色。
    /// </summary>
    private static readonly Dictionary<string, string[]> ElementDefaultColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["General"] = ["#FFFFFF", "#D0E1E9"],
        ["Fire"] = ["#FFFFFF", "#FF8100"],
        ["Water"] = ["#FFFFFF", "#9DFFEF"],
        ["Electricity"] = ["#FFFFFF", "#AE86FF"],
        ["Wood"] = ["#FFFFFF", "#CEF700"],
        ["Wind"] = ["#FFFFFF", "#00EFFF"],
        ["Rock"] = ["#FFFFFF", "#F7C600"],
        ["Light"] = ["#FFFFFF", "#F5EFB3"],
        ["Dark"] = ["#FFFFFF", "#734FE1"]
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

    public static bool SupportsTakenDamageLogSettings(WidgetKind kind)
    {
        return kind is WidgetKind.TakenDamageLog;
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
            MetricTimeline = SupportsMetricTimelineSettings(kind) ? CreateMetricTimelineSettings() : null,
            TakenDamageLog = SupportsTakenDamageLogSettings(kind) ? CreateTakenDamageLogSettings() : null,
            BuffList = SupportsBuffListSettings(kind) ? CreateBuffListSettings(kind) : null,
            ElementColor = SupportsElementColorSettings(kind) ? CreateElementColorSettings(kind) : null
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
                Width = MeterInitialWindowWidth,
                Height = MeterInitialWindowHeight
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
            WidgetKind.DamageContribution or WidgetKind.HealingContribution => new WidgetWindowConfig
            {
                Width = MetricContributionInitialWindowWidth,
                Height = MetricContributionInitialWindowHeight
            },
            WidgetKind.TakenDamageLog => new WidgetWindowConfig
            {
                Width = TakenDamageLogInitialWindowWidth,
                Height = TakenDamageLogInitialWindowHeight
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

    public static TakenDamageLogWidgetSettingsConfig CreateTakenDamageLogSettings()
    {
        return new TakenDamageLogWidgetSettingsConfig
        {
            HealthValueDisplayModeIndex = DefaultHealthValueDisplayModeIndex,
            AttackerFilterIndex = DefaultTakenDamageLogAttackerFilterIndex,
            ClassColorIndexes = CreateDefaultClassColorIndexes(WidgetKind.TakenDamageLog),
            ClassColorPalettes = CreateDefaultClassColorPalettes(WidgetKind.TakenDamageLog),
            TextColorIndexes = CreateDefaultTextColorIndexes(),
            TextColorPalettes = CreateDefaultTextColorPalettes()
        };
    }

    /// <summary>ゲージの長さの既定は種別で違う。バフは10秒、デバフは30秒。</summary>
    public static BuffListWidgetSettingsConfig CreateBuffListSettings(WidgetKind kind)
    {
        return new BuffListWidgetSettingsConfig
        {
            GaugeLengthIndex = kind == WidgetKind.DebuffList
                ? DefaultDebuffListGaugeLengthIndex
                : DefaultBuffListGaugeLengthIndex,
            GaugeColorIndexes = CreateDefaultGaugeColorIndexes(kind),
            GaugeColorPalettes = CreateDefaultGaugeColorPalettes(),
            GaugeColorOpacity = MaxClassColorOpacity
        };
    }

    public static bool SupportsBuffListSettings(WidgetKind kind)
    {
        return kind is WidgetKind.BuffList or WidgetKind.DebuffList;
    }

    public static BuffListWidgetSettingsConfig CloneNormalizedBuffList(WidgetKind kind, BuffListWidgetSettingsConfig? buffList)
    {
        var normalized = (buffList ?? CreateBuffListSettings(kind)).Clone();
        NormalizeBuffList(kind, normalized);
        return normalized;
    }

    /// <summary>
    /// 保存値を表示できる形に直す。<b>選んでいる枠が入っていないときの既定は種別で違う</b>ので
    /// <see cref="WidgetKind"/> を受け取る(<see cref="GetDefaultGaugeColorIndex"/>)。
    /// </summary>
    public static void NormalizeBuffList(WidgetKind kind, BuffListWidgetSettingsConfig buffList)
    {
        buffList.GaugeLengthIndex = Math.Clamp(
            buffList.GaugeLengthIndex,
            MinBuffListGaugeLengthIndex,
            BuffListGaugeLengthSeconds.Length - 1);

        buffList.GaugeColorOpacity = Math.Clamp(
            buffList.GaugeColorOpacity,
            MinClassColorOpacity,
            MaxClassColorOpacity);

        buffList.GaugeColorIndexes ??= CreateDefaultGaugeColorIndexes(kind);
        buffList.GaugeColorPalettes ??= CreateDefaultGaugeColorPalettes();

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in BuffListGaugeColorKeys)
        {
            var defaultColors = CreateDefaultGaugeColors(key);
            var sourceColors = buffList.GaugeColorPalettes.TryGetValue(key, out var colors) ? colors : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = buffList.GaugeColorIndexes.TryGetValue(key, out var index)
                ? index
                : GetDefaultGaugeColorIndex(kind);
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        buffList.GaugeColorIndexes = normalizedIndexes;
        buffList.GaugeColorPalettes = normalizedPalettes;
    }

    public static MeterWidgetSettingsConfig CreateMeterSettings(WidgetKind kind)
    {
        return new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = GetDefaultPlayerInfoFormatString(kind),
            HealthValueDisplayModeIndex = DefaultHealthValueDisplayModeIndex,
            PartyDisplayModeIndex = DefaultPartyDisplayModeIndex,
            EntityDisplayModeIndex = DefaultEntityDisplayModeIndex,
            ListSortModeIndex = GetDefaultListSortModeIndex(kind),
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

    /// <summary>
    /// 一覧の並び替えの既定。エンティティリストは名前順、それ以外(プレイヤーリスト)は発見順。
    /// </summary>
    public static int GetDefaultListSortModeIndex(WidgetKind kind)
    {
        return kind == WidgetKind.EntityList
            ? NameListSortModeIndex
            : FirstSeenListSortModeIndex;
    }

    /// <summary>並び替えの設定を出すウィジェット。</summary>
    public static bool UsesListSort(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerList or WidgetKind.EntityList;
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
    /// テキストカラーで最初に選ばれている枠。エンティティ名とプレイヤー名だけ1枠目(白)で、
    /// 残りは2枠目の差し色から始める。
    /// </summary>
    public static int GetDefaultTextColorIndex(string key)
    {
        return string.Equals(key, "EntityName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "PlayerName", StringComparison.OrdinalIgnoreCase)
            ? MinClassColorIndex
            : MinClassColorIndex + 1;
    }

    /// <summary>
    /// ゲージの色で最初に選ばれている枠。<b>デバフ一覧だけ2枠目(紫)</b>で、残りは1枠目(青緑)から始める。
    /// 見本そのものは両方で同じ(<see cref="BuffListDefaultGaugeColorHexes"/>)。
    /// </summary>
    public static int GetDefaultGaugeColorIndex(WidgetKind kind)
    {
        return kind == WidgetKind.DebuffList
            ? MinClassColorIndex + 1
            : MinClassColorIndex;
    }

    public static Dictionary<string, int> CreateDefaultGaugeColorIndexes(WidgetKind kind)
    {
        return BuffListGaugeColorKeys
            .ToDictionary(key => key, _ => GetDefaultGaugeColorIndex(kind), StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultGaugeColorPalettes()
    {
        return BuffListGaugeColorKeys.ToDictionary(
            key => key,
            CreateDefaultGaugeColors,
            StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> CreateDefaultGaugeColors(string key)
    {
        return BuffListDefaultGaugeColorHexes.TryGetValue(key, out var colors)
            ? [.. colors]
            : ["#5688A4"];
    }

    public static Dictionary<string, int> CreateDefaultTextColorIndexes()
    {
        return TakenDamageLogTextColorKeys
            .ToDictionary(key => key, GetDefaultTextColorIndex, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultTextColorPalettes()
    {
        return TakenDamageLogTextColorKeys.ToDictionary(
            key => key,
            CreateDefaultTextColors,
            StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> CreateDefaultTextColors(string key)
    {
        return TakenDamageLogDefaultTextColorHexes.TryGetValue(key, out var colors)
            ? [.. colors]
            : ["#FFFFFF", "#A8A8A8"];
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

    /// <summary>属性カラーの節を持つウィジェット。スキル詳細の2種だけ。</summary>
    public static bool SupportsElementColorSettings(WidgetKind kind)
    {
        return kind is WidgetKind.DamageContribution or WidgetKind.HealingContribution;
    }

    /// <summary>
    /// 属性カラーの既定を引くときの読み替え先(ユーザー決定)。
    /// スキル詳細(与ダメ)は DPS メーター、スキル詳細(ヒール)は HPS メーターに合わせる。
    /// </summary>
    private static WidgetKind GetElementColorDefaultSource(WidgetKind kind)
    {
        return kind == WidgetKind.HealingContribution
            ? WidgetKind.HpsMeter
            : WidgetKind.DpsMeter;
    }

    /// <summary>属性カラーのフィルター色の既定パレット。読み替え先のメーターと同じ色。</summary>
    public static List<string> CreateDefaultElementFilterColors(WidgetKind kind)
    {
        return CreateDefaultClassColorFilterColors(GetElementColorDefaultSource(kind));
    }

    public static ElementColorWidgetSettingsConfig CreateElementColorSettings(WidgetKind kind)
    {
        var source = GetElementColorDefaultSource(kind);

        return new ElementColorWidgetSettingsConfig
        {
            ColorOpacity = MaxClassColorOpacity,
            ColorIndexes = CreateDefaultElementColorIndexes(),
            ColorPalettes = CreateDefaultElementColorPalettes(),
            FilterEnabled = IsClassColorFilterEnabledByDefault(source),
            FilterColors = CreateDefaultClassColorFilterColors(source),
            FilterStrength = DefaultClassColorFilterStrength
        };
    }

    /// <summary>属性カラーで最初に選ばれている枠。被ダメログのテキストカラーと同じく全部2枠目の差し色。</summary>
    public static Dictionary<string, int> CreateDefaultElementColorIndexes()
    {
        return DamagePropertyKeys
            .ToDictionary(key => key, _ => MinClassColorIndex + 1, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, List<string>> CreateDefaultElementColorPalettes()
    {
        return DamagePropertyKeys.ToDictionary(
            key => key,
            CreateDefaultElementColors,
            StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> CreateDefaultElementColors(string key)
    {
        return ElementDefaultColorHexes.TryGetValue(key, out var colors)
            ? [.. colors]
            : [.. ElementDefaultColorHexes["General"]];
    }

    public static ElementColorWidgetSettingsConfig CloneNormalizedElementColor(
        WidgetKind kind,
        ElementColorWidgetSettingsConfig? elementColor)
    {
        var normalized = (elementColor ?? CreateElementColorSettings(kind)).Clone();
        NormalizeElementColor(kind, normalized);
        return normalized;
    }

    public static void NormalizeElementColor(WidgetKind kind, ElementColorWidgetSettingsConfig elementColor)
    {
        var source = GetElementColorDefaultSource(kind);

        elementColor.ColorOpacity = Math.Clamp(
            elementColor.ColorOpacity,
            MinClassColorOpacity,
            MaxClassColorOpacity);
        elementColor.FilterStrength = Math.Clamp(
            elementColor.FilterStrength,
            MinClassColorFilterStrength,
            MaxClassColorFilterStrength);
        elementColor.FilterEnabled ??= IsClassColorFilterEnabledByDefault(source);

        elementColor.ColorIndexes ??= CreateDefaultElementColorIndexes();
        elementColor.ColorPalettes ??= CreateDefaultElementColorPalettes();

        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in DamagePropertyKeys)
        {
            var defaultColors = CreateDefaultElementColors(key);
            var sourceColors = elementColor.ColorPalettes.TryGetValue(key, out var colors) ? colors : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = elementColor.ColorIndexes.TryGetValue(key, out var index)
                ? index
                : MinClassColorIndex + 1;
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        elementColor.ColorIndexes = normalizedIndexes;
        elementColor.ColorPalettes = normalizedPalettes;

        var filterDefaults = CreateDefaultClassColorFilterColors(source);
        elementColor.FilterColors = NormalizeColorList(
            elementColor.FilterColors ?? filterDefaults,
            filterDefaults,
            MaxPaletteColorCount);
        elementColor.FilterColorIndex = Math.Clamp(
            elementColor.FilterColorIndex,
            MinClassColorIndex,
            elementColor.FilterColors.Count - 1);
    }

    public static List<string> CreateDefaultClassColors(WidgetKind kind, string key)
    {
        var source = kind switch
        {
            // 被ダメログのクラスアイコンは、プレイヤーリストと同じ既定の色で始める。
            WidgetKind.PlayerList or WidgetKind.TakenDamageLog => PlayerListDefaultClassColorHexes,
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

    public static TakenDamageLogWidgetSettingsConfig CloneNormalizedTakenDamageLog(TakenDamageLogWidgetSettingsConfig? takenDamageLog)
    {
        var normalized = (takenDamageLog ?? CreateTakenDamageLogSettings()).Clone();
        NormalizeTakenDamageLog(normalized);
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
        config.TakenDamageLog = SupportsTakenDamageLogSettings(kind)
            ? CloneNormalizedTakenDamageLog(config.TakenDamageLog)
            : null;
        config.BuffList = SupportsBuffListSettings(kind)
            ? CloneNormalizedBuffList(kind, config.BuffList)
            : null;
        config.ElementColor = SupportsElementColorSettings(kind)
            ? CloneNormalizedElementColor(kind, config.ElementColor)
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
                config.Window.Width ??= MeterInitialWindowWidth;
                config.Window.Height ??= MeterInitialWindowHeight;
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
                config.Window.Width ??= MetricContributionInitialWindowWidth;
                config.Window.Height ??= MetricContributionInitialWindowHeight;
                break;
            case WidgetKind.TakenDamageLog:
                config.Window.Width ??= TakenDamageLogInitialWindowWidth;
                config.Window.Height ??= TakenDamageLogInitialWindowHeight;
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

    public static void NormalizeTakenDamageLog(TakenDamageLogWidgetSettingsConfig takenDamageLog)
    {
        takenDamageLog.HealthValueDisplayModeIndex = Math.Clamp(
            takenDamageLog.HealthValueDisplayModeIndex,
            DefaultHealthValueDisplayModeIndex,
            SeparateShieldHealthValueDisplayModeIndex);
        takenDamageLog.AttackerFilterIndex = Math.Clamp(
            takenDamageLog.AttackerFilterIndex,
            DefaultTakenDamageLogAttackerFilterIndex,
            NoSelfOrFriendlyFireTakenDamageLogAttackerFilterIndex);

        takenDamageLog.ClassColorIndexes ??= CreateDefaultClassColorIndexes(WidgetKind.TakenDamageLog);
        takenDamageLog.ClassColorPalettes ??= CreateDefaultClassColorPalettes(WidgetKind.TakenDamageLog);
        (takenDamageLog.ClassColorIndexes, takenDamageLog.ClassColorPalettes) = NormalizeClassColors(
            WidgetKind.TakenDamageLog,
            takenDamageLog.ClassColorIndexes,
            takenDamageLog.ClassColorPalettes);

        takenDamageLog.TextColorIndexes ??= CreateDefaultTextColorIndexes();
        takenDamageLog.TextColorPalettes ??= CreateDefaultTextColorPalettes();
        (takenDamageLog.TextColorIndexes, takenDamageLog.TextColorPalettes) = NormalizeTextColors(
            takenDamageLog.TextColorIndexes,
            takenDamageLog.TextColorPalettes);
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
        meter.EntityDisplayModeIndex = Math.Clamp(
            meter.EntityDisplayModeIndex,
            MinEntityDisplayModeIndex,
            MaxEntityDisplayModeIndex);
        meter.SelfDisplayModeIndex = Math.Clamp(
            meter.SelfDisplayModeIndex,
            DefaultSelfDisplayModeIndex,
            MaxSelfDisplayModeIndex);
        meter.ListSortModeIndex = Math.Clamp(
            meter.ListSortModeIndex,
            FirstSeenListSortModeIndex,
            NameListSortModeIndex);

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

        (meter.ClassColorIndexes, meter.ClassColorPalettes) = NormalizeClassColors(
            kind,
            meter.ClassColorIndexes,
            meter.ClassColorPalettes);
    }

    /// <summary>
    /// クラスカラーの色の一覧と選んでいる枠を、<paramref name="kind"/> の職の並びと既定値で揃える。
    /// メーター系の設定と被ダメログが使う。
    /// </summary>
    private static (Dictionary<string, int> Indexes, Dictionary<string, List<string>> Palettes) NormalizeClassColors(
        WidgetKind kind,
        Dictionary<string, int> indexes,
        Dictionary<string, List<string>> palettes)
    {
        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in GetClassColorKeys(kind))
        {
            var defaultColors = CreateDefaultClassColors(kind, key);
            var sourceColors = palettes.TryGetValue(key, out var colors)
                ? colors
                : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = indexes.TryGetValue(key, out var index)
                ? index
                : GetDefaultClassColorIndex(kind, key);
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        return (normalizedIndexes, normalizedPalettes);
    }

    /// <summary>
    /// 被ダメログのテキストカラーを、鍵の並びと既定値で揃える。クラスカラーと同じ規則。
    /// </summary>
    private static (Dictionary<string, int> Indexes, Dictionary<string, List<string>> Palettes) NormalizeTextColors(
        Dictionary<string, int> indexes,
        Dictionary<string, List<string>> palettes)
    {
        var normalizedIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in TakenDamageLogTextColorKeys)
        {
            var defaultColors = CreateDefaultTextColors(key);
            var sourceColors = palettes.TryGetValue(key, out var colors)
                ? colors
                : defaultColors;
            var palette = NormalizeColorList(sourceColors, defaultColors, MaxPaletteColorCount);
            normalizedPalettes[key] = palette;

            var selectedIndex = indexes.TryGetValue(key, out var index)
                ? index
                : GetDefaultTextColorIndex(key);
            normalizedIndexes[key] = Math.Clamp(selectedIndex, MinClassColorIndex, palette.Count - 1);
        }

        return (normalizedIndexes, normalizedPalettes);
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
