using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerListEntry : ObservableObject
{
    private readonly ObservableCollection<PlayerImagineRoleSkillEntry> _imagineSkillEntries = [];
    private readonly ObservableCollection<PlayerImagineRoleSkillEntry> _roleSkillEntries = [];
    private IReadOnlyList<PlayerCooldownSkillSnapshot?> _imagineSkillSnapshots =
        Array.Empty<PlayerCooldownSkillSnapshot?>();
    private IReadOnlyList<PlayerCooldownSkillSnapshot?> _roleSkillSnapshots =
        Array.Empty<PlayerCooldownSkillSnapshot?>();
    private const int SelfRoleSlotCount = 4;

    private readonly HashSet<int> _trackedSkillIds = [];

    /// <summary>
    /// 他人のロールスキル表示設定。設定が変わると別インスタンスが渡ってくるので、
    /// 参照の一致で「設定が変わったか」を判定する。<b>自分には適用しない。</b>
    /// </summary>
    private IReadOnlyDictionary<string, bool>? _otherRoleSkillVisibility;
    private IReadOnlyDictionary<string, bool>? _appliedRoleSkillVisibility;
    private IReadOnlyList<PlayerCooldownSkillSnapshot?> _filteredRoleSkillSnapshots =
        Array.Empty<PlayerCooldownSkillSnapshot?>();
    private int _roleSlotCount;
    private long _skillEntityUuid;
    private int _skillProfessionId;
    private object? _skillSourceToken;
    private object? _roleFilterToken;
    private bool _skillLoadoutInitialized;

    // HPテキストは HP更新(Update)とデバフ更新(RefreshSkillDisplay)の
    // どちらからでも組み立て直す。呼ばれるタイミングが別なので値を控えておく。
    private long _currentHp;
    private long _maxHp;
    private long _currentShield;
    private int _healthValueDisplayModeIndex;

    /// <summary>蘇生不可デバフの残り秒。取れていなければ <c>null</c>。</summary>
    private double? _reviveBlockSeconds;

    private PlayerListEntry(long characterId)
    {
        CharacterId = characterId;
        FillSkillSlots(_imagineSkillEntries, 2, isImagine: true);
        // 自分はここで作った4枠のまま動かさない(装備スロットが分かるので空欄を出せる)。
        // 他人だけ RefreshSkillDisplay で取得できた数に合わせて増減させる。
        FillSkillSlots(_roleSkillEntries, SelfRoleSlotCount, isImagine: false);
        ImagineSkillEntries = new ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry>(_imagineSkillEntries);
        RoleSkillEntries = new ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry>(_roleSkillEntries);
    }

    public long CharacterId { get; }

    public ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry> ImagineSkillEntries { get; }

    public ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry> RoleSkillEntries { get; }

    [ObservableProperty]
    private string _professionKey = string.Empty;

    [ObservableProperty]
    private string _classSpecDisplayName = string.Empty;

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
    private bool _isNpc;

    [ObservableProperty]
    private bool _isPartyMember;

    [ObservableProperty]
    private string _partyNumberText = string.Empty;

    [ObservableProperty]
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    [ObservableProperty]
    private bool _isPlayerSelectionMenuOpen;

    /// <summary>自分の行か。</summary>
    [ObservableProperty]
    private bool _isSelf;

    /// <summary>自分の行を強調表示するか。設定「自分の表示」が強調表示のときだけ true。</summary>
    [ObservableProperty]
    private bool _isSelfHighlighted;

    public bool IsHealthFull => HealthRatio >= 1d;

    public static PlayerListEntry Create(
        PlayerRosterEntry player,
        MeterWidgetSettingsConfig settings,
        PlayerNameDisplayMode playerNameDisplayMode)
    {
        var entry = new PlayerListEntry(player.CharacterId);
        entry.Update(player, settings, playerNameDisplayMode);
        entry.RefreshSkillDisplay(refreshEffects: true);
        return entry;
    }

    public void Update(
        PlayerRosterEntry player,
        MeterWidgetSettingsConfig settings,
        PlayerNameDisplayMode playerNameDisplayMode)
    {
        IsSelf = player.IsSelf;
        IsSelfHighlighted = player.IsSelf
            && settings.SelfDisplayModeIndex == WidgetConfigDefaults.DefaultSelfDisplayModeIndex;
        _otherRoleSkillVisibility = settings.OtherRoleSkillVisibility;
        ProfessionKey = PlayerProfession.GetKey(player.ProfessionId);
        ClassSpecDisplayName = LocalizationManager.Instance.GetString($"ClassSpec_{player.ClassSpec}");
        IsNpc = player.IsNpc;
        IsPartyMember = player.IsPartyMember;
        PartyNumberText = player.IsPartyMember
            ? player.PartyNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "？"
            : string.Empty;

        DisplayName = PlayerInfoFormatFormatter.Format(
            player,
            settings.PlayerInfoFormatString,
            playerNameDisplayMode);

        HealthRatio = GetRatio(player.CurrentHp, player.MaxHp);
        UpdateShieldGeometry(player.CurrentHp, player.MaxHp, player.CurrentShield);
        _currentHp = player.CurrentHp;
        _maxHp = player.MaxHp;
        _currentShield = player.CurrentShield;
        _healthValueDisplayModeIndex = settings.HealthValueDisplayModeIndex;
        UpdateHealthText();

        var classColor = GetClassColor(settings, ProfessionKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
        }
    }

    public void RefreshSkillDisplay(bool refreshEffects)
    {
        var loadoutChanged = RefreshSkillLoadoutIfChanged();
        var filterChanged = !ReferenceEquals(_appliedRoleSkillVisibility, _otherRoleSkillVisibility);
        if (loadoutChanged || filterChanged)
        {
            ApplyRoleSkillVisibility();

            // 自分の枠は触らない。コンストラクタで作った4枠のままにする。
            if (!IsSelf)
            {
                SyncSkillSlotCount(
                    _roleSkillEntries,
                    _filteredRoleSkillSnapshots.Count,
                    isImagine: false);
            }
            UpdateSkillMetadata(_imagineSkillEntries, _imagineSkillSnapshots);
            UpdateSkillMetadata(_roleSkillEntries, _filteredRoleSkillSnapshots);
        }

        UpdateSkillCooldowns(
            _imagineSkillEntries,
            _imagineSkillSnapshots,
            _skillEntityUuid);
        UpdateSkillCooldowns(
            _roleSkillEntries,
            _filteredRoleSkillSnapshots,
            _skillEntityUuid);

        if (!refreshEffects)
        {
            return;
        }

        var effectsBySkillId = MeterSnapshotProvider.GetPlayerListSkillEffects(
            CharacterId,
            _skillEntityUuid,
            _trackedSkillIds);
        UpdateSkillEffects(_imagineSkillEntries, effectsBySkillId);
        UpdateSkillEffects(_roleSkillEntries, effectsBySkillId);

        // 蘇生不可はスキル枠のバッジには出さず、HPテキストのほうへ回す。
        _reviveBlockSeconds = MeterSnapshotProvider.TryGetReviveBlockSeconds(
            CharacterId,
            _skillEntityUuid,
            out var reviveBlockSeconds)
            ? reviveBlockSeconds
            : null;
        UpdateHealthText();
    }

    /// <summary>
    /// 他人のロールスキル枠に、表示オンのものだけを残す。
    ///
    /// <para>
    /// <b>自分は対象外。</b> 自分はアクションバーの枠をそのまま出すので、
    /// ここで間引くと枠の位置がゲームとずれる。
    /// </para>
    /// </summary>
    private void ApplyRoleSkillVisibility()
    {
        _appliedRoleSkillVisibility = _otherRoleSkillVisibility;

        if (IsSelf || _otherRoleSkillVisibility is null)
        {
            _filteredRoleSkillSnapshots = _roleSkillSnapshots;
            return;
        }

        var visible = new List<PlayerCooldownSkillSnapshot?>(_roleSkillSnapshots.Count);
        foreach (var snapshot in _roleSkillSnapshots)
        {
            if (snapshot is null)
            {
                continue;
            }

            var key = snapshot.SkillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // 設定に無いスキルは既定どおり表示する。設定は20件ぶんしか持たない。
            if (!_otherRoleSkillVisibility.TryGetValue(key, out var isVisible) || isVisible)
            {
                visible.Add(snapshot);
            }
        }

        _filteredRoleSkillSnapshots = visible;
    }

    private bool RefreshSkillLoadoutIfChanged()
    {
        if (!MeterSnapshotProvider.TryGetPlayerListImagineRoleSkillSourceState(
                CharacterId,
                _skillEntityUuid,
                out var entityUuid,
                out var professionId,
                out var skillSourceToken,
                out var roleFilterToken))
        {
            return ResetSkillLoadoutCache();
        }

        if (_skillLoadoutInitialized
            && entityUuid == _skillEntityUuid
            && professionId == _skillProfessionId
            && ReferenceEquals(skillSourceToken, _skillSourceToken)
            && ReferenceEquals(roleFilterToken, _roleFilterToken))
        {
            return false;
        }

        var snapshot = MeterSnapshotProvider.GetPlayerListImagineRoleSkills(
            CharacterId,
            entityUuid);
        _skillEntityUuid = snapshot.EntityUuid;
        _skillProfessionId = professionId;
        _skillSourceToken = skillSourceToken;
        _roleFilterToken = roleFilterToken;
        _imagineSkillSnapshots = snapshot.ImagineSkills;
        _roleSkillSnapshots = snapshot.RoleSkills;
        _roleSlotCount = snapshot.RoleSlotCount;
        RebuildTrackedSkillIds();
        _skillLoadoutInitialized = true;
        return true;
    }

    private bool ResetSkillLoadoutCache()
    {
        var changed = _skillLoadoutInitialized
            || _imagineSkillSnapshots.Count != 0
            || _roleSkillSnapshots.Count != 0;
        _skillEntityUuid = 0;
        _skillProfessionId = 0;
        _skillSourceToken = null;
        _roleFilterToken = null;
        _imagineSkillSnapshots = Array.Empty<PlayerCooldownSkillSnapshot?>();
        _roleSkillSnapshots = Array.Empty<PlayerCooldownSkillSnapshot?>();
        _roleSlotCount = 0;
        _trackedSkillIds.Clear();
        _skillLoadoutInitialized = false;
        return changed;
    }

    private void RebuildTrackedSkillIds()
    {
        _trackedSkillIds.Clear();
        foreach (var snapshot in _imagineSkillSnapshots)
        {
            if (snapshot is { SkillId: > 0 })
            {
                _trackedSkillIds.Add(snapshot.SkillId);
            }
        }

        foreach (var snapshot in _roleSkillSnapshots)
        {
            if (snapshot is { SkillId: > 0 })
            {
                _trackedSkillIds.Add(snapshot.SkillId);
            }
        }
    }

    private static void FillSkillSlots(
        ObservableCollection<PlayerImagineRoleSkillEntry> entries,
        int count,
        bool isImagine)
    {
        for (var index = 0; index < count; index++)
        {
            entries.Add(new PlayerImagineRoleSkillEntry(isImagine));
        }
    }

    /// <summary>
    /// 枠の数を <paramref name="count"/> に合わせる。増減は末尾で行い、
    /// 既存の枠のインスタンスは作り直さない(表示のちらつきを避けるため)。
    /// </summary>
    private static void SyncSkillSlotCount(
        ObservableCollection<PlayerImagineRoleSkillEntry> entries,
        int count,
        bool isImagine)
    {
        while (entries.Count > count)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        while (entries.Count < count)
        {
            entries.Add(new PlayerImagineRoleSkillEntry(isImagine));
        }
    }

    private static void UpdateSkillMetadata(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyList<PlayerCooldownSkillSnapshot?> snapshots)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].UpdateSkill(index < snapshots.Count ? snapshots[index] : null);
        }
    }

    private static void UpdateSkillCooldowns(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyList<PlayerCooldownSkillSnapshot?> snapshots,
        long entityUuid)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            var snapshot = index < snapshots.Count ? snapshots[index] : null;
            var cooldownState = snapshot is null || entityUuid == 0
                ? default
                : SkillCooldownTracker.Instance.GetDisplayState(
                    entityUuid,
                    snapshot.SkillId,
                    snapshot.CooldownSeconds,
                    snapshot.IsImagine,
                    snapshot.MaxCharges,
                    snapshot.ChargeCooldownSeconds);
            entries[index].UpdateCooldown(snapshot, cooldownState);
        }
    }

    private static void UpdateSkillEffects(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> effectsBySkillId)
    {
        foreach (var entry in entries)
        {
            if (entry.SkillId > 0
                && effectsBySkillId.TryGetValue(entry.SkillId, out var effects))
            {
                entry.UpdateEffects(effects.Buff, effects.Debuff);
            }
            else
            {
                entry.UpdateEffects(null, null);
            }
        }
    }

    partial void OnHealthRatioChanged(double value)
    {
        OnPropertyChanged(nameof(IsHealthFull));
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

    private static double GetRatio(long currentValue, long maxValue)
    {
        if (maxValue <= 0)
        {
            return 0d;
        }

        return Math.Clamp(currentValue / (double)maxValue, 0d, 1d);
    }

    /// <summary>
    /// HPバーのテキスト。<b>HPが優先。</b>
    ///
    /// <para>
    /// 死んでいて、かつ蘇生不可デバフの残り秒が取れているときだけ差し替える。
    /// 残り秒が取れないのはデバフの持続が分からない状態なので、
    /// そのまま出すと切れた後も表示が残る。その場合はHPのまま。
    /// </para>
    /// </summary>
    private void UpdateHealthText()
    {
        if (_currentHp <= 0 && _reviveBlockSeconds is { } seconds)
        {
            var roundedSeconds = Math.Max(0, (int)Math.Ceiling(seconds));
            HealthText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                LocalizationManager.Instance.GetString("PlayerList_ReviveBlocked"),
                roundedSeconds);
            return;
        }

        HealthText = FormatHealthText(
            _currentHp,
            _maxHp,
            _currentShield,
            _healthValueDisplayModeIndex);
    }

    private static string FormatHealthText(
        long currentHp,
        long maxHp,
        long currentShield,
        int displayModeIndex)
    {
        var shield = Math.Max(currentShield, 0L);
        return displayModeIndex == WidgetConfigDefaults.SeparateShieldHealthValueDisplayModeIndex
            ? $"{currentHp}({shield})/{maxHp}"
            : $"{AddSaturating(currentHp, shield)}/{maxHp}";
    }

    private static long AddSaturating(long value, long nonNegativeAddition)
    {
        return nonNegativeAddition > 0 && value > long.MaxValue - nonNegativeAddition
            ? long.MaxValue
            : value + nonNegativeAddition;
    }

    private static Color GetClassColor(MeterWidgetSettingsConfig classColors, string professionKey)
    {
        var palette = classColors.ClassColorPalettes.TryGetValue(professionKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.PlayerList, professionKey);
        var selectedIndex = classColors.ClassColorIndexes.TryGetValue(professionKey, out var index)
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
