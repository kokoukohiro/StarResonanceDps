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
    bool IsSeasonTalentInactive = false,
    // シーズンランクの段階(SeasonRankTable.RankToLevel、属性 AttrRankLevel)。段階 0 があるので、属性が届いていなければ null。
    int? SeasonRankLevel = null,
    // いま周りの実体から値が届いているか(自分を含む)。周りに見えている人は自分と同じ場所にいる。
    bool IsNearby = false,
    // パーティ情報のいる場所。マップ番号とチャンネル番号、0 は分からない(パーティ外も 0)。
    int PartySceneId = 0,
    int PartyLineId = 0);
