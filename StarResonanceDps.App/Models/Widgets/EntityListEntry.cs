using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using Zproto;
using MonsterClassification = StarResonanceDps.Core.CombatRuntime.EMonsterType;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class EntityListEntry : ObservableObject
{
    private const string UnknownScoreText = "??";

    private EntityListEntry(long entityUuid)
    {
        EntityUuid = entityUuid;
    }

    public long EntityUuid { get; }

    /// <summary>
    /// 種別ID(<c>AttrId</c>。モンスターなら <c>MonsterTable</c> のキー)。
    /// <b>再起動をまたいでも同じ値</b>。<c>EntityUuid</c> は実体ごとの値で別物。
    /// どの表の番号かは <see cref="EntityType"/> で決まるので、比べるときは種類と組で比べる。
    /// </summary>
    public long EntityId { get; private set; }

    /// <summary>実体の種類。<see cref="EntityId"/> がどの表の番号かを決める。</summary>
    public EEntityType EntityType { get; private set; }

    /// <summary>
    /// 書式を通していない素の名前。表に名前が無く HP バーが見える実体は「敵」「味方」。
    /// 種別ID(<c>AttrId</c>)が分からない実体は「未知の敵」(陣営が味方と分かっていれば「味方」)。
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    public int Level { get; private set; }

    /// <summary>種別ID(<c>AttrId</c>)が分からない(アプリの起動前から居た)実体か。名前は「未知の敵」か「味方」になる。</summary>
    public bool IsTypeUnknown => EntityId == 0;

    [ObservableProperty]
    private string _classificationKey = "Unknown";

    [ObservableProperty]
    private string _classificationDisplayName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private double _healthRatio;

    [ObservableProperty]
    private double _shieldVisibleRatio;

    [ObservableProperty]
    private double _shieldOverflowRatio;

    [ObservableProperty]
    private double _shieldOverflowStartRatio = 1d;

    [ObservableProperty]
    private string _healthText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSecondGauge))]
    private bool _hasBreakGauge;

    [ObservableProperty]
    private double _breakRatio;

    [ObservableProperty]
    private string _breakText = string.Empty;

    /// <summary>詠唱ゲージを出しているか。詠唱バーの全体秒数が過ぎたら、技が続いていても出さない。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSecondGauge))]
    private bool _isCasting;

    /// <summary>詠唱ゲージの満ちた割合(経過 / 全体)。詠唱の始まりと 0.1 秒ごとの更新で張り直し、その間はアニメーションで満ちる。</summary>
    [ObservableProperty]
    private double _castRatio;

    /// <summary>詠唱ゲージが満タンになるまでの秒数(全体 − 経過)。詠唱していなければ 0。</summary>
    [ObservableProperty]
    private double _castFillSeconds;

    /// <summary>詠唱ゲージの文字「経過/全体」。</summary>
    [ObservableProperty]
    private string _castText = string.Empty;

    /// <summary>当てている詠唱。行の詠唱が変わったときだけゲージを張り直すために持つ。</summary>
    private NearbyEntityCast? _cast;

    /// <summary>満ちた後の満タンと「全体/全体」を出したか。出した次の更新で詠唱ゲージを外す。</summary>
    private bool _isCastCompleteShown;

    /// <summary>HP バーの下にもう1本ゲージを出すか(名前・HP・ゲージの3段)。詠唱中はブレイクゲージの代わりに詠唱ゲージを出す。</summary>
    public bool ShowsSecondGauge => HasBreakGauge || IsCasting;

    [ObservableProperty]
    private bool _isInvulnerable;

    [ObservableProperty]
    private bool _isBreakLocked;

    [ObservableProperty]
    private EntityCampRelation _campRelation = EntityCampRelation.NonHostile;

    /// <summary>名前の左に戦闘状態の印を出すか。設定「戦闘状態」が表示で、いま戦闘状態のとき。</summary>
    [ObservableProperty]
    private bool _showsCombatState;

    /// <summary>アイコンに重ねる、倒すと入る得点(「○○pt」)。今の場所が得点を求めていない・得点の無い敵・HP バーの無い敵は空。</summary>
    [ObservableProperty]
    private string _scoreText = string.Empty;

    [ObservableProperty]
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    [ObservableProperty]
    private bool _isEntitySelectionMenuOpen;

    public bool UsesFriendlyHealthBar => CampRelation == EntityCampRelation.Friendly;

    /// <summary>
    /// HP バーの右のヘイトの枠(6つ)。狙っている相手がいて表示設定「狙われるプレイヤー」が表示なら [0] がその相手で [1] からヘイト1位、
    /// そうでなければ [0] からヘイト1位(詰めて並べる)。ヘイトは表示設定「ヘイト値」の人数(1〜5人)まで。
    /// 枠は作ったまま使い回し、行を当て直すたびに値だけ入れ替える。
    /// </summary>
    public IReadOnlyList<EntityHateSlot> HateSlots { get; } =
        Enumerable.Range(0, NearbyEntityHateRanking.MaxCount + 1).Select(_ => new EntityHateSlot()).ToArray();

    /// <param name="showsScore">今の場所が得点を求めているか(<see cref="Core.Services.DungeonScoreSnapshot.IsScoreRequested"/>)。</param>
    public static EntityListEntry Create(
        NearbyEntityEntry entity,
        MeterWidgetSettingsConfig settings,
        EntityHatePlayerResolver hatePlayers,
        bool showsScore)
    {
        var entry = new EntityListEntry(entity.EntityUuid);
        entry.Update(entity, settings, hatePlayers, showsScore);
        return entry;
    }

    /// <param name="showsScore">今の場所が得点を求めているか(<see cref="Core.Services.DungeonScoreSnapshot.IsScoreRequested"/>)。</param>
    public void Update(
        NearbyEntityEntry entity,
        MeterWidgetSettingsConfig settings,
        EntityHatePlayerResolver hatePlayers,
        bool showsScore)
    {
        EntityId = entity.EntityId;
        EntityType = entity.EntityType;

        // 名前は<b>ここで</b>言語別に引く。Core の投影側で解決すると、
        // 投影した時点の言語で焼き付いて言語切替に追従しなくなる。
        // 言語を切り替えると WidgetListItemViewModel が全エントリに Update を掛け直すので、
        // ここを通していれば自動で入れ替わる。
        Name = ResolveName(entity);
        Level = entity.Level;
        ClassificationKey = GetClassificationKey(entity);
        ClassificationDisplayName = LocalizationManager.Instance.GetString($"Classes_{ClassificationKey}");
        DisplayName = EntityInfoFormatFormatter.Format(Name, entity.Level, settings.PlayerInfoFormatString);
        HealthRatio = GetRatio(entity.CurrentHp, entity.MaxHp);
        UpdateShieldGeometry(entity.CurrentHp, entity.MaxHp, entity.CurrentShield);
        HealthText = FormatHealthText(
            entity.CurrentHp,
            entity.MaxHp,
            entity.CurrentShield,
            settings.HealthValueDisplayModeIndex);
        HasBreakGauge = entity.MaxBreak > 0;
        BreakRatio = HasBreakGauge ? GetRatio(entity.CurrentBreak, entity.MaxBreak) : 0d;
        BreakText = HasBreakGauge ? FormatValuePair(entity.CurrentBreak, entity.MaxBreak) : string.Empty;
        IsInvulnerable = entity.IsInvulnerable;
        IsBreakLocked = entity.IsBreakLocked;
        if (_cast != entity.Cast)
        {
            _cast = entity.Cast;
            StartCast(DateTime.UtcNow);
        }

        CampRelation = entity.CampRelation;
        ShowsCombatState = entity.IsInCombat
            && settings.CombatStateDisplayModeIndex == WidgetConfigDefaults.CombatStateDisplayShowIndex;
        var score = GetDisplayedScore(entity, showsScore);
        ScoreText = score > 0 ? FormatScore(score) : string.Empty;

        var classColor = GetClassColor(settings, ClassificationKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
        }

        UpdateHateSlots(entity, settings, hatePlayers);
    }

    /// <summary>
    /// ヘイトの枠を当て直す。一覧(プレイヤーだけ)の無い敵は全部隠す。
    /// 狙っている相手がプレイヤーで「狙われるプレイヤー」が表示なら左端をその相手にし、続けてヘイト1位から。
    /// そうでなければ左端からヘイト1位を詰めて並べる。ヘイトは「ヘイト値」の人数まで。並べるものが無い枠は隠す(場所は残す)。
    /// </summary>
    private void UpdateHateSlots(NearbyEntityEntry entity, MeterWidgetSettingsConfig settings, EntityHatePlayerResolver hatePlayers)
    {
        var ranking = entity.HateRanking;
        var hasTarget = ranking.Count > 0
            && entity.TargetCharacterId != 0
            && settings.TargetedPlayerDisplayModeIndex == WidgetConfigDefaults.TargetedPlayerDisplayShowIndex;
        var rankCount = Math.Min(ranking.Count, WidgetConfigDefaults.GetHateRankDisplayCount(settings.HateRankDisplayModeIndex));
        var firstRankSlot = 0;
        if (hasTarget)
        {
            HateSlots[0].ShowTarget(hatePlayers.Resolve(entity.TargetCharacterId));
            firstRankSlot = 1;
        }

        for (var slotIndex = firstRankSlot; slotIndex < HateSlots.Count; slotIndex++)
        {
            var rankIndex = slotIndex - firstRankSlot;
            if (rankIndex < rankCount)
            {
                var rank = ranking[rankIndex];
                HateSlots[slotIndex].ShowRank(hatePlayers.Resolve(rank.CharacterId), rank.HateValue);
            }
            else
            {
                HateSlots[slotIndex].Hide();
            }
        }
    }

    /// <summary>
    /// 行に出す名前。<b>並び替え(名前順)もこれを使う</b>ので、行を作る前にも引けるように分けてある。
    /// 名前は表示言語で決まるので、ここで引く(Core の投影で解決すると言語切替に追従しない)。
    /// </summary>
    public static string ResolveName(NearbyEntityEntry entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return entity.EntityId == 0
            ? GetUnknownLabel(entity)
            : CombatDataCatalog.GetEntityName(entity.EntityType, entity.EntityId, GetUnnamedLabel(entity));
    }

    /// <summary>
    /// 名前の無い実体に出す名前。HP バーが見える実体だけで、HP バーの色と同じ判定で決める
    /// (緑=自分と同じ陣営なら「味方」、赤なら「敵」。陣営がまだ分からない間も赤なので「敵」)。
    /// HP バーが見えない実体は名前が無ければ一覧に載らないので、付けない。
    /// </summary>
    private static string? GetUnnamedLabel(NearbyEntityEntry entity)
    {
        if (!entity.HasHpBar)
        {
            return null;
        }

        return LocalizationManager.Instance.GetString(
            entity.CampRelation == EntityCampRelation.Friendly
                ? "EntityList_UnnamedAlly"
                : "EntityList_UnnamedEnemy");
    }

    /// <summary>
    /// 種別ID(<c>AttrId</c>)が分からない実体の名前。<c>AttrId</c> は出現の通知でしか届かないので、
    /// アプリの起動前から居た実体がこうなる。HP バーの色と同じ判定で、味方と分かっていれば「味方」、それ以外は「未知の敵」。
    /// </summary>
    private static string GetUnknownLabel(NearbyEntityEntry entity)
    {
        return LocalizationManager.Instance.GetString(
            entity.CampRelation == EntityCampRelation.Friendly
                ? "EntityList_UnnamedAlly"
                : "EntityList_UnknownEnemy");
    }

    partial void OnCampRelationChanged(EntityCampRelation value)
    {
        OnPropertyChanged(nameof(UsesFriendlyHealthBar));
    }

    /// <summary>
    /// 詠唱の文字とゲージを今の時刻で当て直す(エンティティリストの 0.1 秒ごとの更新)。
    /// ゲージも張り直すのは、詠唱の途中で行の部品ができた(窓を開いた・スクロールで見えた)ときに、
    /// アニメーションが前に張った割合から始まって文字より遅れるため。
    /// 全体秒数が過ぎた後の1回は満タンと「全体/全体」を出し、その次の更新で詠唱ゲージを外す(最後の表示を更新の当たり方で変えない)。
    /// </summary>
    public void RefreshCast(DateTime utcNow)
    {
        if (!IsCasting || _cast is not { } cast)
        {
            return;
        }

        if (_isCastCompleteShown)
        {
            EndCast();
            return;
        }

        var elapsedSeconds = GetCastElapsedSeconds(cast, utcNow);
        if (elapsedSeconds >= cast.TotalSeconds)
        {
            var totalText = RoundToHundredths(cast.TotalSeconds);
            _isCastCompleteShown = true;
            CastFillSeconds = 0d;
            CastRatio = 1d;
            CastText = FormatCastText(totalText, totalText);
            return;
        }

        ApplyCast(cast, elapsedSeconds);
    }

    /// <summary>窓を開いたときに、今の時刻で詠唱を当て直す。全体秒数が過ぎていれば、満タンの表示を挟まずに外す。</summary>
    public void ReapplyCast(DateTime utcNow)
    {
        StartCast(utcNow);
    }

    /// <summary>行の詠唱が変わった。ゲージを張り、あいだはアニメーションで満ちる。</summary>
    private void StartCast(DateTime utcNow)
    {
        _isCastCompleteShown = false;
        if (_cast is not { } cast)
        {
            EndCast();
            return;
        }

        var elapsedSeconds = GetCastElapsedSeconds(cast, utcNow);
        if (elapsedSeconds >= cast.TotalSeconds)
        {
            EndCast();
            return;
        }

        // 前の詠唱と同じ値でもアニメーションを張り直すよう、満ちる秒数を先に 0 へ戻す。
        CastFillSeconds = 0d;
        ApplyCast(cast, elapsedSeconds);
        IsCasting = true;
    }

    private void ApplyCast(NearbyEntityCast cast, double elapsedSeconds)
    {
        CastRatio = elapsedSeconds / cast.TotalSeconds;
        CastFillSeconds = cast.TotalSeconds - elapsedSeconds;
        CastText = FormatCastText(FloorToHundredths(elapsedSeconds), RoundToHundredths(cast.TotalSeconds));
    }

    private void EndCast()
    {
        _isCastCompleteShown = false;
        IsCasting = false;
        CastFillSeconds = 0d;
        CastRatio = 0d;
        CastText = string.Empty;
    }

    private static double GetCastElapsedSeconds(NearbyEntityCast cast, DateTime utcNow)
    {
        return Math.Max((utcNow - cast.StartedAtUtc).TotalSeconds, 0d);
    }

    /// <summary>「経過/全体」を小数2桁で。単位の s は4言語とも同じ。</summary>
    private static string FormatCastText(decimal elapsedSeconds, decimal totalSeconds)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{elapsedSeconds:0.00}s/{totalSeconds:0.00}s");
    }

    /// <summary>経過の秒は切り捨て。四捨五入した全体の秒を超えない。</summary>
    private static decimal FloorToHundredths(double seconds)
    {
        return decimal.Floor((decimal)seconds * 100m) / 100m;
    }

    private static decimal RoundToHundredths(double seconds)
    {
        return decimal.Round((decimal)seconds, 2, MidpointRounding.AwayFromZero);
    }

    private void UpdateShieldGeometry(long currentHp, long maxHp, long currentShield)
    {
        if (maxHp <= 0)
        {
            ShieldVisibleRatio = 0d;
            ShieldOverflowRatio = 0d;
            ShieldOverflowStartRatio = 1d;
            return;
        }

        var displayedHp = Math.Clamp(currentHp, 0L, maxHp);
        var shield = Math.Max(currentShield, 0L);
        var shieldGaugeCapacity = (double)maxHp * 2d;
        var availableShieldCapacity = (maxHp - displayedHp) * 2d;
        var foldsEntireShield = shield > availableShieldCapacity;

        var visibleShield = foldsEntireShield
            ? 0d
            : shield;

        var overflowShield = foldsEntireShield
            ? Math.Min(shield, shieldGaugeCapacity)
            : 0d;

        ShieldVisibleRatio = visibleShield / shieldGaugeCapacity;
        ShieldOverflowRatio = overflowShield / shieldGaugeCapacity;
        ShieldOverflowStartRatio = 1d - ShieldOverflowRatio;
    }

    private static string GetClassificationKey(NearbyEntityEntry entity)
    {
        if (entity.EntityType == EEntityType.EntMonster)
        {
            return entity.MonsterType switch
            {
                MonsterClassification.Boss => "Boss",
                MonsterClassification.Elite => "Elite",
                MonsterClassification.Monster => "Monster",
                _ => "Unknown"
            };
        }

        return "Unknown";
    }

    private static double GetRatio(long currentValue, long maxValue)
    {
        if (maxValue <= 0)
        {
            return 0d;
        }

        return Math.Clamp(currentValue / (double)maxValue, 0d, 1d);
    }

    private static string FormatHealthText(
        long currentHp,
        long maxHp,
        long currentShield,
        int displayModeIndex)
    {
        var shield = Math.Max(currentShield, 0L);
        // バリア量個別表示でも、バリアが無いときは括弧を出さない。
        return displayModeIndex == WidgetConfigDefaults.SeparateShieldHealthValueDisplayModeIndex
            ? shield > 0
                ? $"{currentHp}({shield})/{maxHp}"
                : $"{currentHp}/{maxHp}"
            : $"{AddSaturating(currentHp, shield)}/{maxHp}";
    }

    private static long AddSaturating(long value, long nonNegativeAddition)
    {
        return nonNegativeAddition > 0 && value > long.MaxValue - nonNegativeAddition
            ? long.MaxValue
            : value + nonNegativeAddition;
    }

    private static string FormatValuePair(long currentValue, long maxValue)
    {
        return $"{currentValue}/{maxValue}";
    }

    /// <summary>得点の文字(「○○pt」)。アイコンとフッターで使う。分からなければ「??pt」。</summary>
    internal static string FormatScore(int? score)
    {
        return LocalizationManager.Instance.Format(
            "EntityList_ScoreFormat",
            score?.ToString(CultureInfo.InvariantCulture) ?? UnknownScoreText);
    }

    /// <summary>
    /// アイコンに出し、フッターの交戦中に数える得点。今の場所が得点を求めている間の、HP バーのある敵だけ
    /// (表示設定に関係なく、フィルター「オブジェクト以外」と同じ条件)。それ以外は 0。アイコンとフッターで同じ決まりにする。
    /// </summary>
    /// <param name="isScoreRequested">今の場所が得点を求めているか(<see cref="Core.Services.DungeonScoreSnapshot.IsScoreRequested"/>)。</param>
    internal static int GetDisplayedScore(NearbyEntityEntry entity, bool isScoreRequested)
    {
        return isScoreRequested && entity.HasHpBar ? entity.Score : 0;
    }

    /// <summary>アイコンカラーで選んでいる色。敵の種類の行と、ヘイトの枠のプレイヤーのクラスの行で使う。</summary>
    internal static Color GetClassColor(MeterWidgetSettingsConfig classColors, string classificationKey)
    {
        var palette = classColors.ClassColorPalettes.TryGetValue(classificationKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.EntityList, classificationKey);
        var selectedIndex = classColors.ClassColorIndexes.TryGetValue(classificationKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? "#A8A8A8"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        return ColorUtilities.TryParseHex(selectedColor, out var color)
            ? color
            : Color.FromRgb(0xA8, 0xA8, 0xA8);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
