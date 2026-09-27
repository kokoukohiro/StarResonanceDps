using System.Globalization;
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
    public SkillDetailWidgetSettingsConfig? SkillDetail { get; set; }

    public PlayerStatusWidgetSettingsConfig? PlayerStatus { get; set; }

    /// <summary>
    /// ステータス詳細の行の並び(<c>PlayerStatusEntry.OrderUnitAttrIds</c> の番号)。
    ///
    /// <para>
    /// <b>設定ウィンドウは触らない。</b>あちらは開いた時点の設定を丸ごと書き戻すので、
    /// ウィジェット側で動かした並びを巻き戻してしまう。ウィンドウの位置と同じく、
    /// 掴んで動かした瞬間にウィジェットから直接保存する。
    /// </para>
    /// </summary>
    public List<int>? PlayerStatusRowOrder { get; set; }

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
            SkillDetail = SkillDetail?.Clone(),
            PlayerStatus = PlayerStatus?.Clone(),
            PlayerStatusRowOrder = PlayerStatusRowOrder is null ? null : [.. PlayerStatusRowOrder],
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

    /// <summary>プレイヤーリストにシーズン心相晶の型の絵を出すか。使うのはプレイヤーリストだけ。</summary>
    public bool ShowSeasonTalent { get; set; } = true;

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
            ShowSeasonTalent = ShowSeasonTalent,
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
/// <summary>スキル詳細の表示設定。いまは行名の書式だけ。</summary>
public sealed class SkillDetailWidgetSettingsConfig
{
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultSkillInfoFormatString"/>。</remarks>
    public string? SkillInfoFormatString { get; set; }

    public SkillDetailWidgetSettingsConfig Clone()
    {
        return new SkillDetailWidgetSettingsConfig
        {
            SkillInfoFormatString = SkillInfoFormatString
        };
    }
}

public sealed class PlayerStatusWidgetSettingsConfig
{
    /// <remarks><c>null</c> は「設定されていない」。既定は <see cref="WidgetConfigDefaults.DefaultHideInactiveStatusEffects"/>。</remarks>
    public bool? HideInactiveStatusEffects { get; set; }

    /// <summary>行ごとに出すか。鍵は属性の番号。</summary>
    public Dictionary<string, bool> RowVisibility { get; set; } =
        WidgetConfigDefaults.CreateDefaultPlayerStatusRowVisibility();

    /// <summary>
    /// 行の文字とアイコンの色。形はクラスカラーと同じで、<b>鍵は属性の番号</b>
    /// (<c>PlayerStatusEntry.SettingRowAttrIds</c>)。
    /// </summary>
    public Dictionary<string, int> TextColorIndexes { get; set; } =
        WidgetConfigDefaults.CreateDefaultPlayerStatusTextColorIndexes();

    public Dictionary<string, List<string>> TextColorPalettes { get; set; } =
        WidgetConfigDefaults.CreateDefaultPlayerStatusTextColorPalettes();

    public PlayerStatusWidgetSettingsConfig Clone()
    {
        return new PlayerStatusWidgetSettingsConfig
        {
            HideInactiveStatusEffects = HideInactiveStatusEffects,
            RowVisibility = RowVisibility is null
                ? WidgetConfigDefaults.CreateDefaultPlayerStatusRowVisibility()
                : new Dictionary<string, bool>(RowVisibility, StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = TextColorIndexes is null
                ? WidgetConfigDefaults.CreateDefaultPlayerStatusTextColorIndexes()
                : new Dictionary<string, int>(TextColorIndexes, StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = TextColorPalettes is null
                ? WidgetConfigDefaults.CreateDefaultPlayerStatusTextColorPalettes()
                : TextColorPalettes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value is null ? new List<string>() : new List<string>(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };
    }
}


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
    public const string DefaultMeterPlayerInfoFormatString = "{Name}[{Psych}] - {Spec} ({PowerLevel}-{SeasonStrength})";
    public const string DefaultSkillInfoFormatString = "{SkillName} - {Type} ({Hits}hits-CRT{CritRate})";

    /// <summary>ステータス詳細で、値が 0 の属性効果の行を隠すか。</summary>
    public const bool DefaultHideInactiveStatusEffects = true;
    public const string DefaultPlayerListPlayerInfoFormatString = "{Name}({PowerLevel}-{SeasonStrength})";
    public const string DefaultBuffInfoFormatString = "{BuffName}({Name})";

    public const int MinBuffCardScale = 100;
    public const int MaxBuffCardScale = 1000;
    public const int DefaultBuffCardScale = 200;
    public const int BuffCardScaleStep = 25;


    private const double PlayerListInitialWindowWidth = 457d;
    private const double PlayerListInitialWindowHeight = 460d;
    private const double MeterInitialWindowWidth = 440d;
    private const double MeterInitialWindowHeight = 460d;
    private const double TakenDamageLogInitialWindowWidth = 440d;
    private const double TakenDamageLogInitialWindowHeight = 460d;
    private const double PlayerInfoInitialWindowWidth = 360d;
    private const double PlayerInfoInitialWindowHeight = 400d;
    private const double PlayerStatusInitialWindowWidth = 360d;
    private const double PlayerStatusInitialWindowHeight = 400d;
    private const double PlayerEquipmentInitialWindowWidth = 400d;
    private const double PlayerEquipmentInitialWindowHeight = 230d;
    private const double PlayerBuffListInitialWindowWidth = 360d;
    private const double PlayerBuffListInitialWindowHeight = 400d;
    private const double BuffDebuffCardInitialWindowWidth = 260d;
    private const double BuffDebuffCardInitialWindowHeight = 260d;
    private const double MetricContributionInitialWindowWidth = 520d;
    private const double MetricContributionInitialWindowHeight = 460d;
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

    /// <summary>プレイヤーリストのアイコンカラーの鍵。クラスの鍵の後ろに、シーズン心相晶の絵の10件と不明。</summary>
    public static readonly string[] PlayerListClassColorKeys =
        [.. ClassColorKeys, .. StarResonanceDps.App.Services.SeasonTalentIcons.ColorKeys];

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

    /// <summary>プレイヤーリストのシーズン心相晶の既定色。鍵は SeasonTalent.(絵の名前)。</summary>
    private static readonly Dictionary<string, string[]> PlayerListDefaultSeasonTalentColorHexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SeasonTalent.s2talent01_01"] = ["#FFFFFF", "#F5EABF"],
        ["SeasonTalent.s2talent02_01"] = ["#FFFFFF", "#F5C1A8"],
        ["SeasonTalent.s2talent03_01"] = ["#FFFFFF", "#F5CECE"],
        ["SeasonTalent.s2talent04_01"] = ["#FFFFFF", "#F5CEEA"],
        ["SeasonTalent.s2talent05_01"] = ["#FFFFFF", "#C2E9F5"],
        ["SeasonTalent.s2talent06_01"] = ["#FFFFFF", "#DFD2F5"],
        ["SeasonTalent.s2talent07_01"] = ["#FFFFFF", "#C8F5EB"],
        ["SeasonTalent.s2talent08_01"] = ["#FFFFFF", "#CCF5DD"],
        ["SeasonTalent.s2talent051_01"] = ["#FFFFFF", "#C2F5F1"],
        ["SeasonTalent.s2talent054_01"] = ["#FFFFFF", "#F5D8F2"],
        ["SeasonTalent.Unknown"] = ["#FFFFFF", "#EEE9F2"]
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
        ["Water"] = ["#FFFFFF", "#9EECFF"],
        ["Electricity"] = ["#FFFFFF", "#8787FF"],
        ["Wood"] = ["#FFFFFF", "#CEF700"],
        ["Wind"] = ["#FFFFFF", "#80FFCE"],
        ["Rock"] = ["#FFFFFF", "#F7C600"],
        ["Light"] = ["#FFFFFF", "#F5EFB3"],
        ["Dark"] = ["#FFFFFF", "#8C70E0"],
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
        ["General"] = ["#A4C2D0", "#A8A8A8"],
        ["Fire"] = ["#F4501B", "#CC6333"],
        ["Water"] = ["#66CDE7", "#4494A9"],
        ["Electricity"] = ["#6B6BEB", "#826DD5"],
        ["Wood"] = ["#94E727", "#519F44"],
        ["Wind"] = ["#70DAB2", "#4A997A"],
        ["Rock"] = ["#997951", "#9C714F"],
        ["Light"] = ["#F9ED73", "#F5CF64"],
        ["Dark"] = ["#B36AF4", "#9D66AA"]
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

    /// <summary>
    /// テキストカラーの見本。<b>一覧の全項目ぶんを1件ずつ並べる</b>(並びは「ステータス」の一覧と同じ)。
    /// </summary>
    private static readonly Dictionary<int, string[]> PlayerStatusDefaultTextColorHexes = new()
    {
        [11440] = ["#FFFFFF", "#C3A7FF"],    // シーズン強度
        [11320] = ["#FFFFFF", "#FFB3B3"],    // 最大HP
        [12790] = ["#FFFFFF", "#FBE8C3"],    // 物理増強
        [12800] = ["#FFFFFF", "#DFC8FA"],    // 魔法増強
        [13000] = ["#FFFFFF", "#D6E8F0"],    // 全属性ボーナス
        [13010] = ["#FFFFFF", "#FFB38A"],    // 火属性ボーナス
        [13020] = ["#FFFFFF", "#B5ECFA"],    // 氷属性ボーナス
        [13030] = ["#FFFFFF", "#C8F49A"],    // 森属性ボーナス
        [13040] = ["#FFFFFF", "#B5B0FF"],    // 雷属性ボーナス
        [13050] = ["#FFFFFF", "#A8F2D7"],    // 風属性ボーナス
        [13060] = ["#FFFFFF", "#F7D98A"],    // 岩属性ボーナス
        [13070] = ["#FFFFFF", "#F7F0C9"],    // 光属性ボーナス
        [13080] = ["#FFFFFF", "#C8B4F2"],    // 闇属性ボーナス
        [13200] = ["#FFFFFF", "#D6E8F0"],    // 全属性軽減
        [13210] = ["#FFFFFF", "#FFB38A"],    // 火属性軽減
        [13220] = ["#FFFFFF", "#B5ECFA"],    // 氷属性軽減
        [13230] = ["#FFFFFF", "#C8F49A"],    // 森属性軽減
        [13240] = ["#FFFFFF", "#B5B0FF"],    // 雷属性軽減
        [13250] = ["#FFFFFF", "#A8F2D7"],    // 風属性軽減
        [13260] = ["#FFFFFF", "#F7D98A"],    // 岩属性軽減
        [13270] = ["#FFFFFF", "#F7F0C9"],    // 光属性軽減
        [13280] = ["#FFFFFF", "#C8B4F2"],    // 闇属性軽減

        [11330] = ["#FFFFFF", "#FBE8C3"],    // 物理攻撃力
        [11340] = ["#FFFFFF", "#DFC8FA"],    // 魔法攻撃力
        [11010] = ["#FFFFFF", "#F6D3A8"],    // 筋力
        [11020] = ["#FFFFFF", "#DCC1FA"],    // 知力
        [11030] = ["#FFFFFF", "#A2E3F5"],    // 敏捷
        [11040] = ["#FFFFFF", "#B3F4C6"],    // 耐久力

        [11110] = ["#FFFFFF", "#FABFCB"],    // 会心
        [12510] = ["#FFFFFF", "#F8B6C7"],    // 会心ダメージ
        [11120] = ["#FFFFFF", "#A2E3F5"],    // ファスト
        [11130] = ["#FFFFFF", "#B3F4C6"],    // 幸運
        [12530] = ["#FFFFFF", "#C1F8D2"],    // 幸運の一撃のダメージ倍率
        [11140] = ["#FFFFFF", "#DFC8FA"],    // 器用さ
        [11150] = ["#FFFFFF", "#FBF2B9"],    // 万能
        [11840] = ["#FFFFFF", "#F7EFBE"],    // 万能増強
        [11850] = ["#FFFFFF", "#F3EDC2"],    // 万能軽減
        [11170] = ["#FFFFFF", "#CDC6FA"],    // レジスト
        [12540] = ["#FFFFFF", "#DCC1FA"],    // レジストダメージ軽減

        [11720] = ["#FFFFFF", "#7EEBFF"],    // 攻撃速度
        [11730] = ["#FFFFFF", "#8BCBFF"],    // 詠唱速度
        [11960] = ["#FFFFFF", "#A7B6FF"],    // リキャスト加速
        [11830] = ["#FFFFFF", "#FFD77A"],    // ブレイク効率

        [11350] = ["#FFFFFF", "#AEDAFA"],    // 物理防御力
        [11360] = ["#FFFFFF", "#CDC6FA"],    // 魔法防御力
        [11370] = ["#FFFFFF", "#F4E8B9"],    // 物理防御力無視
        [11380] = ["#FFFFFF", "#D9C7FA"],    // 魔法防御力無視
        [12560] = ["#FFFFFF", "#BFE9FB"],    // 物理軽減
        [12580] = ["#FFFFFF", "#DCC1FA"],    // 魔法軽減

        [11410] = ["#FFFFFF", "#FFE0A8"],    // 精錬物攻
        [11430] = ["#FFFFFF", "#D8C0FA"],    // 精錬魔攻
        [11420] = ["#FFFFFF", "#BBDFF5"],    // 精錬防御力

        [12670] = ["#FFFFFF", "#FFB9AD"],    // ダメージボーナス
        [12680] = ["#FFFFFF", "#B8DFF5"],    // ダメージ軽減
        [12590] = ["#FFFFFF", "#FFC7A8"],    // 近距離ダメージボーナス
        [12610] = ["#FFFFFF", "#A9E7F5"],    // 遠距離ダメージボーナス
        [12630] = ["#FFFFFF", "#FFD69A"],    // 対ボスダメージボーナス

        [11460] = ["#FFFFFF", "#C3A7FF"],    // シーズン防御力
        [11470] = ["#FFFFFF", "#D0B6FA"],    // シーズン防御力無視
        [12690] = ["#FFFFFF", "#FFB3D0"],    // シーズンダメージボーナス
        [12700] = ["#FFFFFF", "#C6C4FA"],    // シーズンダメージ軽減

        [11500] = ["#FFFFFF", "#D6E8F0"],    // 全属性攻撃力
        [11510] = ["#FFFFFF", "#FFB38A"],    // 火属性攻撃力
        [11520] = ["#FFFFFF", "#B5ECFA"],    // 氷属性攻撃力
        [11530] = ["#FFFFFF", "#C8F49A"],    // 森属性攻撃力
        [11540] = ["#FFFFFF", "#B5B0FF"],    // 雷属性攻撃力
        [11550] = ["#FFFFFF", "#A8F2D7"],    // 風属性攻撃力
        [11560] = ["#FFFFFF", "#F7D98A"],    // 岩属性攻撃力
        [11570] = ["#FFFFFF", "#F7F0C9"],    // 光属性攻撃力
        [11580] = ["#FFFFFF", "#C8B4F2"],    // 闇属性攻撃力

        [11790] = ["#FFFFFF", "#9EF0C1"],    // 回復力
        [11800] = ["#FFFFFF", "#B8F2D0"],    // 被回復力
        [12740] = ["#FFFFFF", "#C1F8D2"],    // 会心回復
        [12720] = ["#FFFFFF", "#C1F8D2"],    // 幸運の一撃回復の倍率
        [11810] = ["#FFFFFF", "#A9E8F5"],    // バリア強度
        [11820] = ["#FFFFFF", "#C3DFF5"],    // 被バリア強度

        [11880] = ["#FFFFFF", "#F3B8D5"],    // 抑圧ダメージ
        [11890] = ["#FFFFFF", "#CDBEFA"],    // 抑圧ダメージ軽減
        [12730] = ["#FFFFFF", "#FFCAA8"],    // 臣獣ダメージ
        [11990] = ["#FFFFFF", "#A5ECF3"],    // 臣獣の攻撃速度

        [10200] = ["#FFFFFF", "#A2E3F5"],    // 移動速度
        [20020] = ["#FFFFFF", "#F5D6A8"],    // 最大スタミナ
        [20120] = ["#FFFFFF", "#B3F4C6"],    // 戦闘時のスタミナ回復率
    };

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

    /// <summary>スキル詳細の表示設定(行名の書式)を持つ種別か。</summary>
    public static bool SupportsSkillDetailSettings(WidgetKind kind)
    {
        return kind is WidgetKind.DamageContribution or WidgetKind.HealingContribution;
    }

    /// <summary>ステータス詳細の表示設定を持つ種別か。</summary>
    public static bool SupportsPlayerStatusSettings(WidgetKind kind)
    {
        return kind is WidgetKind.PlayerStatus;
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
            ElementColor = SupportsElementColorSettings(kind) ? CreateElementColorSettings(kind) : null,
            SkillDetail = SupportsSkillDetailSettings(kind) ? CreateSkillDetailSettings() : null,
            PlayerStatus = SupportsPlayerStatusSettings(kind) ? CreatePlayerStatusSettings() : null
        };
    }

    /// <summary>ステータス詳細の表示設定の既定。</summary>
    public static PlayerStatusWidgetSettingsConfig CreatePlayerStatusSettings()
    {
        return new PlayerStatusWidgetSettingsConfig
        {
            HideInactiveStatusEffects = DefaultHideInactiveStatusEffects,
            RowVisibility = CreateDefaultPlayerStatusRowVisibility(),
            TextColorIndexes = CreateDefaultPlayerStatusTextColorIndexes(),
            TextColorPalettes = CreateDefaultPlayerStatusTextColorPalettes()
        };
    }

    /// <summary>
    /// テキストカラーの見本。1枠目は白で、2枠目に色を置く(クラスカラーと同じ2色)。
    /// <b>表は一覧の全項目を持つ</b>ので、引けない番号は表の書き忘れ。埋めずに落とす。
    /// </summary>
    public static List<string> CreateDefaultPlayerStatusTextColors(int attrId)
    {
        return [.. PlayerStatusDefaultTextColorHexes[attrId]];
    }

    /// <summary>テキストカラーで最初に選ばれている枠。</summary>
    public const int DefaultPlayerStatusTextColorIndex = 1;

    public static Dictionary<string, int> CreateDefaultPlayerStatusTextColorIndexes()
    {
        var indexes = new Dictionary<string, int>(
            PlayerStatusEntry.SettingRowAttrIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            indexes[attrId.ToString(CultureInfo.InvariantCulture)] = DefaultPlayerStatusTextColorIndex;
        }

        return indexes;
    }

    public static Dictionary<string, List<string>> CreateDefaultPlayerStatusTextColorPalettes()
    {
        var palettes = new Dictionary<string, List<string>>(
            PlayerStatusEntry.SettingRowAttrIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            palettes[attrId.ToString(CultureInfo.InvariantCulture)] = CreateDefaultPlayerStatusTextColors(attrId);
        }

        return palettes;
    }

    /// <summary>
    /// 行の並びを正規化する。<b>表に無い番号は捨て、足りない番号は既定の位置へ補う</b>ので、
    /// 表が増減しても保存値が取り残されない。
    /// </summary>
    public static List<int> NormalizePlayerStatusRowOrder(IReadOnlyList<int>? order)
    {
        var units = PlayerStatusEntry.OrderUnitAttrIds;
        var known = new HashSet<int>(units);
        var result = new List<int>(units.Count);
        var seen = new HashSet<int>();

        if (order is not null)
        {
            foreach (var unit in order)
            {
                if (known.Contains(unit) && seen.Add(unit))
                {
                    result.Add(unit);
                }
            }
        }

        foreach (var unit in units)
        {
            if (seen.Add(unit))
            {
                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>行ごとのオン/オフの既定。一覧の全件を埋める。</summary>
    public static Dictionary<string, bool> CreateDefaultPlayerStatusRowVisibility()
    {
        var visibility = new Dictionary<string, bool>(
            PlayerStatusEntry.SettingRowAttrIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            visibility[attrId.ToString(CultureInfo.InvariantCulture)] =
                PlayerStatusEntry.IsRowVisibleByDefault(attrId);
        }

        return visibility;
    }

    /// <summary>保存値を正規化して写す。設定が無ければ既定を入れる。</summary>
    public static PlayerStatusWidgetSettingsConfig CloneNormalizedPlayerStatus(
        PlayerStatusWidgetSettingsConfig? playerStatus)
    {
        var normalized = (playerStatus ?? CreatePlayerStatusSettings()).Clone();
        normalized.HideInactiveStatusEffects ??= DefaultHideInactiveStatusEffects;

        // 一覧に無い鍵は捨て、足りない鍵は既定で埋める。表が変わっても保存値が取り残されない。
        var rowVisibility = new Dictionary<string, bool>(
            PlayerStatusEntry.SettingRowAttrIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            var key = attrId.ToString(CultureInfo.InvariantCulture);
            rowVisibility[key] = normalized.RowVisibility is not null
                && normalized.RowVisibility.TryGetValue(key, out var visible)
                    ? visible
                    : PlayerStatusEntry.IsRowVisibleByDefault(attrId);
        }

        normalized.RowVisibility = rowVisibility;

        // 一覧に無い鍵は捨て、足りない鍵は既定で埋める。選択はパレットの範囲に収める。
        var textIndexes = new Dictionary<string, int>(
            PlayerStatusEntry.SettingRowAttrIds.Count,
            StringComparer.OrdinalIgnoreCase);
        var textPalettes = new Dictionary<string, List<string>>(
            PlayerStatusEntry.SettingRowAttrIds.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var attrId in PlayerStatusEntry.SettingRowAttrIds)
        {
            var key = attrId.ToString(CultureInfo.InvariantCulture);
            var palette = normalized.TextColorPalettes is not null
                && normalized.TextColorPalettes.TryGetValue(key, out var saved)
                && saved is { Count: > 0 }
                    ? new List<string>(saved)
                    : CreateDefaultPlayerStatusTextColors(attrId);

            var index = normalized.TextColorIndexes is not null
                && normalized.TextColorIndexes.TryGetValue(key, out var savedIndex)
                    ? savedIndex
                    : DefaultPlayerStatusTextColorIndex;

            textPalettes[key] = palette;
            textIndexes[key] = Math.Clamp(index, 0, palette.Count - 1);
        }

        normalized.TextColorIndexes = textIndexes;
        normalized.TextColorPalettes = textPalettes;
        return normalized;
    }

    /// <summary>スキル詳細の表示設定の既定。</summary>
    public static SkillDetailWidgetSettingsConfig CreateSkillDetailSettings()
    {
        return new SkillDetailWidgetSettingsConfig
        {
            SkillInfoFormatString = DefaultSkillInfoFormatString
        };
    }

    /// <summary>保存値を正規化して写す。書式が無ければ既定を入れる。</summary>
    public static SkillDetailWidgetSettingsConfig CloneNormalizedSkillDetail(SkillDetailWidgetSettingsConfig? skillDetail)
    {
        var normalized = (skillDetail ?? CreateSkillDetailSettings()).Clone();
        normalized.SkillInfoFormatString ??= DefaultSkillInfoFormatString;
        return normalized;
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
        return kind switch
        {
            WidgetKind.EntityList => EntityClassColorKeys,
            WidgetKind.PlayerList => PlayerListClassColorKeys,
            _ => ClassColorKeys
        };
    }

    /// <summary>
    /// クラスカラーで最初に選ばれている枠。HPSのビートパフォーマーと、プレイヤーリストのシーズン心相晶(不明を含む)だけ2枠目。
    /// </summary>
    public static int GetDefaultClassColorIndex(WidgetKind kind, string key)
    {
        return (kind == WidgetKind.HpsMeter
                && string.Equals(key, "SoulMusician", StringComparison.OrdinalIgnoreCase))
            || (kind == WidgetKind.PlayerList
                && PlayerListDefaultSeasonTalentColorHexes.ContainsKey(key))
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

    /// <summary>属性カラーで最初に選ばれている枠。9属性とも1枠目(白)。</summary>
    public static Dictionary<string, int> CreateDefaultElementColorIndexes()
    {
        return DamagePropertyKeys
            .ToDictionary(key => key, _ => MinClassColorIndex, StringComparer.OrdinalIgnoreCase);
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
        if (kind == WidgetKind.PlayerList
            && PlayerListDefaultSeasonTalentColorHexes.TryGetValue(key, out var seasonTalentColors))
        {
            return [.. seasonTalentColors];
        }

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
        config.PlayerStatusRowOrder = kind == WidgetKind.PlayerStatus
            ? NormalizePlayerStatusRowOrder(config.PlayerStatusRowOrder)
            : null;

        config.PlayerStatus = SupportsPlayerStatusSettings(kind)
            ? CloneNormalizedPlayerStatus(config.PlayerStatus)
            : null;
        config.SkillDetail = SupportsSkillDetailSettings(kind)
            ? CloneNormalizedSkillDetail(config.SkillDetail)
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
