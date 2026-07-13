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
    int CurrentStamina = 0,
    int MaxStamina = 0);
