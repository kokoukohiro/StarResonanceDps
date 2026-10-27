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
    private bool _hasBreakGauge;

    [ObservableProperty]
    private double _breakRatio;

    [ObservableProperty]
    private string _breakText = string.Empty;

    [ObservableProperty]
    private bool _isInvulnerable;

    [ObservableProperty]
    private bool _isBreakLocked;

    [ObservableProperty]
    private EntityCampRelation _campRelation = EntityCampRelation.NonHostile;

    /// <summary>名前の左に戦闘状態の印を出すか。設定「戦闘状態」が表示で、いま戦闘状態のとき。</summary>
    [ObservableProperty]
    private bool _showsCombatState;

    [ObservableProperty]
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    [ObservableProperty]
    private bool _isEntitySelectionMenuOpen;

    public bool UsesFriendlyHealthBar => CampRelation == EntityCampRelation.Friendly;

    /// <summary>
    /// HP バーの右のヘイトの枠(5つ)。狙っている相手がいれば [0] がその相手で [1]〜[4] がヘイト1〜4位、
    /// いなければ [0]〜[3] がヘイト1〜4位(詰めて並べる)。
    /// 枠は作ったまま使い回し、行を当て直すたびに値だけ入れ替える。
    /// </summary>
    public IReadOnlyList<EntityHateSlot> HateSlots { get; } =
        Enumerable.Range(0, NearbyEntityHateRanking.MaxCount + 1).Select(_ => new EntityHateSlot()).ToArray();

    public static EntityListEntry Create(
        NearbyEntityEntry entity,
        MeterWidgetSettingsConfig settings,
        EntityHatePlayerResolver hatePlayers)
    {
        var entry = new EntityListEntry(entity.EntityUuid);
        entry.Update(entity, settings, hatePlayers);
        return entry;
    }

    public void Update(
        NearbyEntityEntry entity,
        MeterWidgetSettingsConfig settings,
        EntityHatePlayerResolver hatePlayers)
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
        CampRelation = entity.CampRelation;
        ShowsCombatState = entity.IsInCombat
            && settings.CombatStateDisplayModeIndex == WidgetConfigDefaults.CombatStateDisplayShowIndex;

        var classColor = GetClassColor(settings, ClassificationKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
        }

        UpdateHateSlots(entity, hatePlayers);
    }

    /// <summary>
    /// ヘイトの枠を当て直す。一覧(プレイヤーだけ)の無い敵は5つとも隠す。
    /// 狙っている相手がプレイヤーなら左端をその相手にし、続けてヘイト1〜4位。プレイヤーでない・いないときは左端からヘイト1〜4位を詰めて並べる。
    /// 並べるものが無い枠は隠す(場所は残す)。
    /// </summary>
    private void UpdateHateSlots(NearbyEntityEntry entity, EntityHatePlayerResolver hatePlayers)
    {
        var ranking = entity.HateRanking;
        var hasTarget = ranking.Count > 0 && entity.TargetCharacterId != 0;
        var firstRankSlot = 0;
        if (hasTarget)
        {
            HateSlots[0].ShowTarget(hatePlayers.Resolve(entity.TargetCharacterId));
            firstRankSlot = 1;
        }

        for (var slotIndex = firstRankSlot; slotIndex < HateSlots.Count; slotIndex++)
        {
            var rankIndex = slotIndex - firstRankSlot;
            if (rankIndex < ranking.Count)
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
