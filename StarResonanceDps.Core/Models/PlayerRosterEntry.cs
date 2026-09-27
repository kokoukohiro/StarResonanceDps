namespace StarResonanceDps.Core.Models;

public sealed record PlayerRosterEntry(
    long CharacterId,
    string Name,
    int ProfessionId,
    int CombatPower = 0,
    int SeasonStrength = 0,
    long CurrentHp = 0,
    long MaxHp = 0,
    PlayerClassSpec ClassSpec = PlayerClassSpec.Unknown,
    bool IsSelf = false,
    PlayerCombatAttributes CombatAttributes = default,
    int SubProfessionId = 0,
    int Level = 0,
    int SeasonLevel = 0,
    PlayerEquipmentData? EquipmentData = null,
    bool IsNpc = false,
    long CurrentShield = 0,
    bool IsPartyMember = false,
    int? PartyNumber = null,
    bool IsLive = true,
    // 自分の実体に届いている属性の全部(番号順)。他人には能力値が届かないので自分だけ入る。
    IReadOnlyList<PlayerAttributeEntry>? Attributes = null,
    // 有効化しているシーズンタレントの型の根ノードのバフID(0 = 不明か無効)。名前は表示時に CombatDataCatalog.GetSeasonTalentName で引く。
    int SeasonTalentBuffId = 0,
    // シーズンタレントの型が無効(どの型も有効化していない)と確定しているか。
    bool IsSeasonTalentInactive = false);
