using Newtonsoft.Json.Linq;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

internal sealed record PlayerDataSourceSnapshot(
    long CharacterId,
    string Name,
    int ProfessionId,
    int CombatPower,
    int SubProfessionId,
    int SeasonStrength,
    int Level,
    int SeasonLevel,
    long CurrentHp,
    long MaxHp,
    bool IsNpc,
    bool IsSpecAbilityUnequipped,
    PlayerEquipmentData? EquipmentData,
    bool IsLive,
    int SeasonTalentBuffId,
    bool IsSeasonTalentInactive);

internal static class PlayerDataSourceResolver
{
    public static PlayerDataSourceSnapshot Resolve(Entity entity, bool isSelf)
    {
        var characterId = entity.UID != 0
            ? entity.UID
            : Utils.UuidToEntityId(entity.UUID);

        return Resolve(characterId, entity, entity, isSelf);
    }

    public static PlayerDataSourceSnapshot Resolve(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        // PT補完は自分にも使う。<b>ただし最後の手段</b>で、空の項目しか埋めない。
        // ロード済みで起動すると自分の HP/最大HP は AttrHp/AttrMaxHp が来ず、
        // マップ移動まで 0 のままになる。PT側は最初から値を持っている。
        var party = PartyStateStore.Instance.Current;
        PartyMemberSupplement? partySupplement = null;
        if (party.TryGetSupplement(characterId, out var supplement))
        {
            partySupplement = supplement;
        }

        var nearbyName = nearbyEntity is { Name.Length: > 0 }
            ? nearbyEntity.Name
            : GetString(nearbyEntity, "AttrName");
        var nearbyProfessionId = nearbyEntity is { ProfessionId: > 0 }
            ? nearbyEntity.ProfessionId
            : GetInt(nearbyEntity, "AttrProfessionId");
        var nearbyCombatPower = nearbyEntity is { AbilityScore: > 0 }
            ? nearbyEntity.AbilityScore
            : GetInt(nearbyEntity, "AttrFightPoint");
        var nearbySeasonStrength = nearbyEntity is { SeasonStrength: > 0 }
            ? ToInt32(nearbyEntity.SeasonStrength)
            : GetFirstNonZeroInt(
                nearbyEntity,
                "AttrSeasonStrength",
                "AttrSeasonStrengthTotal",
                "AttrSeasonStrengthAdd",
                "AttrSeasonStrengthExAdd",
                "AttrSeasonStrengthPer",
                "AttrSeasonStrengthExPer");
        var nearbyLevel = nearbyEntity is { Level: > 0 }
            ? nearbyEntity.Level
            : GetInt(nearbyEntity, "AttrLevel");
        var nearbySeasonLevel = ToInt32(nearbyEntity?.SeasonLevel);
        if (nearbySeasonLevel <= 0)
        {
            nearbySeasonLevel = GetInt(nearbyEntity, "AttrSeasonLevel");
        }

        var metadataName = metadataEntity is { Name.Length: > 0 }
            ? metadataEntity.Name
            : GetString(metadataEntity, "AttrName");
        var metadataProfessionId = metadataEntity is { ProfessionId: > 0 }
            ? metadataEntity.ProfessionId
            : GetInt(metadataEntity, "AttrProfessionId");
        var metadataCombatPower = metadataEntity is { AbilityScore: > 0 }
            ? metadataEntity.AbilityScore
            : GetInt(metadataEntity, "AttrFightPoint");
        var metadataSeasonStrength = metadataEntity is { SeasonStrength: > 0 }
            ? ToInt32(metadataEntity.SeasonStrength)
            : GetFirstNonZeroInt(
                metadataEntity,
                "AttrSeasonStrength",
                "AttrSeasonStrengthTotal",
                "AttrSeasonStrengthAdd",
                "AttrSeasonStrengthExAdd",
                "AttrSeasonStrengthPer",
                "AttrSeasonStrengthExPer");
        var metadataLevel = metadataEntity is { Level: > 0 }
            ? metadataEntity.Level
            : GetInt(metadataEntity, "AttrLevel");
        var metadataSeasonLevel = ToInt32(metadataEntity?.SeasonLevel);
        if (metadataSeasonLevel <= 0)
        {
            metadataSeasonLevel = GetInt(metadataEntity, "AttrSeasonLevel");
        }

        // AOI外だと nearbyEntity が無い。エンカウンターに残るエンティティが最後に観測したHPを
        // 持っているので、そこから拾う。PT外はこれが唯一の経路。
        //
        // hasNearbyMaxHp は nearbyEntity 基準のままにしてある。PTメンバーでは
        // 下の social data(AOI外でもライブに届く)が引き続き勝つ。
        var hpEntity = nearbyEntity ?? metadataEntity;
        var currentHp = hpEntity?.Hp ?? 0;
        var maxHp = hpEntity?.MaxHp ?? 0;
        var hasNearbyMaxHp = nearbyEntity is not null
            && TryGetPositiveInt64(nearbyEntity.GetAttrKV("AttrMaxHp"), out _);
        if (!hasNearbyMaxHp
            && partySupplement is not null)
        {
            if (partySupplement.MaxHp > 0)
            {
                maxHp = partySupplement.MaxHp;
            }

            if (nearbyEntity is not null
                && TryGetNonNegativeInt64(nearbyEntity.GetAttrKV("AttrHp"), out var nearbyHp))
            {
                currentHp = nearbyHp;
            }
            else if (partySupplement.MaxHp > 0 || !partySupplement.IsNpc)
            {
                currentHp = Math.Max(partySupplement.CurrentHp, 0);
            }
        }

        return new PlayerDataSourceSnapshot(
            characterId,
            !string.IsNullOrEmpty(nearbyName)
                ? nearbyName
                : partySupplement is { Name.Length: > 0 }
                    ? partySupplement.Name
                    : metadataName,
            nearbyProfessionId > 0
                ? nearbyProfessionId
                : partySupplement is { ProfessionId: > 0 }
                    ? partySupplement.ProfessionId
                    : metadataProfessionId,
            nearbyCombatPower > 0
                ? nearbyCombatPower
                : partySupplement is { CombatPower: > 0 }
                    ? partySupplement.CombatPower
                    : metadataCombatPower,
            ResolveSubProfessionId(characterId, nearbyEntity, metadataEntity, isSelf),
            nearbySeasonStrength > 0
                ? nearbySeasonStrength
                : partySupplement is { SeasonStrength: > 0 }
                    ? partySupplement.SeasonStrength
                    : metadataSeasonStrength,
            nearbyLevel > 0
                ? nearbyLevel
                : partySupplement is { Level: > 0 }
                    ? partySupplement.Level
                    : metadataLevel,
            nearbySeasonLevel > 0
                ? nearbySeasonLevel
                : partySupplement is { SeasonLevel: > 0 }
                    ? partySupplement.SeasonLevel
                    : metadataSeasonLevel,
            currentHp,
            maxHp,
            // いまの供給(社交データ)が最優先。無ければ実体に焼き付いた印を見る。
            // 履歴とパーティ離脱後は焼き付けだけが残り、それが唯一の根拠になる。
            !isSelf && (partySupplement?.IsNpc == true || metadataEntity?.IsNpc == true),
            ResolveSpecAbilityUnequipped(characterId, nearbyEntity, metadataEntity, isSelf),
            GetEquipmentData(nearbyEntity)
                ?? partySupplement?.EquipmentData
                ?? GetEquipmentData(metadataEntity),
            // いま値が供給されているか(=ライブ)。キャッシュしか無い行は表示側で灰色にする。
            //
            // 判定はパーティを特別扱いしない。<see cref="PartyStateStore.PartyStateSnapshot.TryGetSupplement"/> は
            // 在籍中のメンバーにしか返さないので、PT外では自動的に「AOIにいるか」だけになる。
            //   nearbyEntity … AOIから届いている現在値
            //   partySupplement … TeamMemberFastSyncData がHPを供給する。AOIとは独立に届く
            //   metadataEntity … 最後の観測値が居残っているだけ。これしか無ければキャッシュ
            //
            // <b>自分だけは常にライブ。</b> 作り直した直後のエンカウンターには自分の
            // エンティティがまだ無く、導出だと一瞬だけ灰に落ちる。
            isSelf || nearbyEntity is not null || partySupplement is not null,
            ResolveSeasonTalentBuffId(characterId, nearbyEntity, metadataEntity, isSelf),
            ResolveSeasonTalentInactive(characterId, nearbyEntity, metadataEntity, isSelf));
    }

    /// <summary>
    /// 有効化しているシーズンタレントの型(根ノードのバフID)を解決する。特化(<see cref="ResolveSubProfessionId"/>)と同じ順で、
    /// 実体 → 控え(パーティ → メーター)。自分は控えを見ない。変身中でも型は変わらないので、変身の関門は置かない。
    /// </summary>
    private static int ResolveSeasonTalentBuffId(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        if (nearbyEntity is { SeasonTalentBuffId: > 0 })
        {
            return nearbyEntity.SeasonTalentBuffId;
        }

        if (metadataEntity is { SeasonTalentBuffId: > 0 })
        {
            return metadataEntity.SeasonTalentBuffId;
        }

        if (isSelf)
        {
            return 0;
        }

        return TryResolveCachedSeasonTalent(characterId, out var cached, out _) ? cached : 0;
    }

    /// <summary>
    /// 控えからシーズンタレントの型を補完する。<b>型と無効の印を必ず同じ出所から返す</b>(特化の <see cref="TryResolveCachedSpec"/> と同じ理由)。
    /// </summary>
    private static bool TryResolveCachedSeasonTalent(
        long characterId,
        out int rootBuffId,
        out bool isInactive)
    {
        var partyCache = PartyMemberCache.Instance;
        if (partyCache.TryGetSeasonTalent(characterId, out rootBuffId))
        {
            isInactive = false;
            return true;
        }

        if (partyCache.IsSeasonTalentInactive(characterId))
        {
            rootBuffId = 0;
            isInactive = true;
            return true;
        }

        var meterCache = MeterPlayerSpecCache.Instance;
        if (meterCache.TryGetSeasonTalent(characterId, out rootBuffId))
        {
            isInactive = false;
            return true;
        }

        if (meterCache.IsSeasonTalentInactive(characterId))
        {
            rootBuffId = 0;
            isInactive = true;
            return true;
        }

        rootBuffId = 0;
        isInactive = false;
        return false;
    }

    /// <summary>
    /// シーズンタレントの型が無効と確定しているか。全バフスナップショットを受け取った上で根ノードのバフが無かったときだけ true
    /// (<see cref="Entity.IsSeasonTalentInactive"/>)。
    /// </summary>
    private static bool ResolveSeasonTalentInactive(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        if (nearbyEntity?.IsSeasonTalentInactive == true
            || metadataEntity?.IsSeasonTalentInactive == true)
        {
            return true;
        }

        return !isSelf
            && TryResolveCachedSeasonTalent(characterId, out _, out var cachedInactive)
            && cachedInactive;
    }

    /// <summary>
    /// 職業特化を解決する。
    ///
    /// <para>
    /// 真値は特化マーカーバフの観測結果で、エンティティが持っている値。相手がAOI外に出ると
    /// 観測できなくなるので、自分以外のパーティメンバーだけキャッシュから補完する
    /// (イマジン/ロールスキルと同じ扱い)。自分はキャッシュを見ない。
    /// </para>
    /// </summary>
    private static int ResolveSubProfessionId(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        // 変身クラス(8/14/15)は特化を持たない。キャッシュも見ない。
        // Entity 側の関門だけでは、変身前の特化がキャッシュから補完されて残ってしまう。
        var professionId = nearbyEntity is { ProfessionId: > 0 }
            ? nearbyEntity.ProfessionId
            : metadataEntity?.ProfessionId ?? 0;
        if (Models.PlayerClassSpecResolver.TryResolveTransformation(professionId, out _))
        {
            return 0;
        }

        if (nearbyEntity is { SubProfessionId: > 0 })
        {
            return nearbyEntity.SubProfessionId;
        }

        if (metadataEntity is { SubProfessionId: > 0 })
        {
            return metadataEntity.SubProfessionId;
        }

        if (isSelf)
        {
            return 0;
        }

        return TryResolveCachedSpec(characterId, out var cached, out _) ? cached : 0;
    }

    /// <summary>
    /// 保持している特化から補完する。<b>特化IDと未装着フラグを必ず同じソースから返す。</b>
    ///
    /// <para>
    /// 別々に引くと、片方がパーティのキャッシュ・もう片方がメーターのキャッシュに当たって
    /// 「特化はXだが未装着」という成立しない組み合わせが出る。
    /// </para>
    ///
    /// <para>
    /// 順序はパーティ(<see cref="PartyMemberCache"/>)が先。在籍中はそちらのほうが
    /// 更新機会が多く、パーティを抜けるまで生きる。次に
    /// <see cref="MeterPlayerSpecCache"/>(AOI外でもメーターに残る人ぶん)。
    /// </para>
    /// </summary>
    private static bool TryResolveCachedSpec(
        long characterId,
        out int subProfessionId,
        out bool isSpecAbilityUnequipped)
    {
        var partyCache = PartyMemberCache.Instance;
        if (partyCache.TryGetSubProfession(characterId, out subProfessionId))
        {
            isSpecAbilityUnequipped = false;
            return true;
        }

        if (partyCache.IsSpecAbilityUnequipped(characterId))
        {
            subProfessionId = 0;
            isSpecAbilityUnequipped = true;
            return true;
        }

        var meterCache = MeterPlayerSpecCache.Instance;
        if (meterCache.TryGetSubProfession(characterId, out subProfessionId))
        {
            isSpecAbilityUnequipped = false;
            return true;
        }

        if (meterCache.IsSpecAbilityUnequipped(characterId))
        {
            subProfessionId = 0;
            isSpecAbilityUnequipped = true;
            return true;
        }

        subProfessionId = 0;
        isSpecAbilityUnequipped = false;
        return false;
    }

    /// <summary>
    /// 特化アビリティ未装着が確定しているか。
    ///
    /// <para>
    /// 「マーカーバフが無い」だけでは未装着と言えない。単に観測していないだけの可能性がある。
    /// 全バフスナップショットを受け取った上でマーカーが1つも無かったときだけ true になる
    /// (<see cref="Entity.IsSpecAbilityUnequipped"/>)。
    /// </para>
    /// </summary>
    private static bool ResolveSpecAbilityUnequipped(
        long characterId,
        Entity? nearbyEntity,
        Entity? metadataEntity,
        bool isSelf)
    {
        if (nearbyEntity?.IsSpecAbilityUnequipped == true
            || metadataEntity?.IsSpecAbilityUnequipped == true)
        {
            return true;
        }

        return !isSelf
            && TryResolveCachedSpec(characterId, out _, out var cachedUnequipped)
            && cachedUnequipped;
    }

    public static int GetInt(Entity? entity, string key)
    {
        return entity is null ? 0 : ToInt32(entity.GetAttrKV(key));
    }

    private static PlayerEquipmentData? GetEquipmentData(Entity? entity)
    {
        var rawData = entity?.GetAttrKV("AttrEquipData");
        if (rawData is null)
        {
            return null;
        }

        if (rawData is JArray serializedItems)
        {
            var parsedItems = serializedItems.ToObject<List<EquipNine>>();
            return parsedItems is null
                ? PlayerEquipmentData.Invalid
                : CreateEquipmentData(parsedItems);
        }

        if (rawData is IEnumerable<EquipNine> items)
        {
            return CreateEquipmentData(items);
        }

        return PlayerEquipmentData.Invalid;
    }

    private static PlayerEquipmentData CreateEquipmentData(IEnumerable<EquipNine> items)
    {
        return PlayerEquipmentData.Create(
            items.Select(item => new PlayerEquipmentItem(item.Slot, item.EquipID)));
    }

    private static int GetFirstNonZeroInt(Entity? entity, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetInt(entity, key);
            if (value != 0)
            {
                return value;
            }
        }

        return 0;
    }

    private static string GetString(Entity? entity, string key)
    {
        return entity?.GetAttrKV(key) as string ?? string.Empty;
    }

    private static bool TryGetPositiveInt64(object? value, out long result)
    {
        if (TryGetNonNegativeInt64(value, out result) && result > 0)
        {
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryGetNonNegativeInt64(object? value, out long result)
    {
        switch (value)
        {
            case long integer when integer >= 0:
                result = integer;
                return true;
            case int integer when integer >= 0:
                result = integer;
                return true;
            case uint integer:
                result = integer;
                return true;
            case ulong integer when integer <= long.MaxValue:
                result = (long)integer;
                return true;
            case short integer when integer >= 0:
                result = integer;
                return true;
            case ushort integer:
                result = integer;
                return true;
            case byte integer:
                result = integer;
                return true;
            case sbyte integer when integer >= 0:
                result = integer;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static int ToInt32(object? value)
    {
        return value switch
        {
            int integer => integer,
            long integer => ClampToInt32(integer),
            uint integer => integer > int.MaxValue ? int.MaxValue : (int)integer,
            ulong integer => integer > int.MaxValue ? int.MaxValue : (int)integer,
            short integer => integer,
            ushort integer => integer,
            byte integer => integer,
            sbyte integer => integer,
            _ => 0
        };
    }

    private static int ClampToInt32(long value)
    {
        return value switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)value
        };
    }
}
