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

    [ObservableProperty]
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    [ObservableProperty]
    private bool _isEntitySelectionMenuOpen;

    public bool UsesFriendlyHealthBar => CampRelation == EntityCampRelation.Friendly;

    public static EntityListEntry Create(
        NearbyEntityEntry entity,
        MeterWidgetSettingsConfig settings)
    {
        var entry = new EntityListEntry(entity.EntityUuid);
        entry.Update(entity, settings);
        return entry;
    }

    public void Update(
        NearbyEntityEntry entity,
        MeterWidgetSettingsConfig settings)
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

        var classColor = GetClassColor(settings, ClassificationKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
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

    private static Color GetClassColor(MeterWidgetSettingsConfig classColors, string classificationKey)
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
