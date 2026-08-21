using System.Globalization;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public sealed class PlayerStatusEntry
{
    private PlayerStatusEntry(
        string maxHpText,
        string attackText,
        string primaryStatText,
        string enduranceText,
        string armorText,
        string criticalText,
        string hasteText,
        string luckText,
        string masteryText,
        string versatilityText,
        string blockText)
    {
        MaxHpText = maxHpText;
        AttackText = attackText;
        PrimaryStatText = primaryStatText;
        EnduranceText = enduranceText;
        ArmorText = armorText;
        CriticalText = criticalText;
        HasteText = hasteText;
        LuckText = luckText;
        MasteryText = masteryText;
        VersatilityText = versatilityText;
        BlockText = blockText;
    }

    public string MaxHpText { get; }

    public string AttackText { get; }

    public string PrimaryStatText { get; }

    public string EnduranceText { get; }

    public string ArmorText { get; }

    public string CriticalText { get; }

    public string HasteText { get; }

    public string LuckText { get; }

    public string MasteryText { get; }

    public string VersatilityText { get; }

    public string BlockText { get; }

    public static PlayerStatusEntry Create(PlayerRosterEntry player)
    {
        var attributes = player.CombatAttributes;
        var localization = LocalizationManager.Instance;
        var (primaryStatKey, primaryStatValue) = GetPrimaryStat(player.ProfessionId, attributes);
        var isMagicalProfession = player.ProfessionId is 2 or 5 or 13;

        return new PlayerStatusEntry(
            $"{localization.GetString("PlayerStatus_MaxHp")}: {FormatInteger(player.MaxHp)}",
            $"{(isMagicalProfession ? "MATK" : localization.GetString("PlayerStatus_PhysicalAttack"))}: {FormatInteger(isMagicalProfession ? attributes.MagicalAttack : attributes.PhysicalAttack)}",
            $"{localization.GetString(primaryStatKey)}: {FormatInteger(primaryStatValue)}",
            $"{localization.GetString("PlayerStatus_Endurance")}: {FormatInteger(attributes.Endurance)}",
            $"{localization.GetString("PlayerStatus_Armor")}: {FormatInteger(attributes.Armor)}",
            $"{localization.GetString("PlayerStatus_Crit")}: {FormatPercent(attributes.CriticalPercent / 100d)}% ({FormatInteger(attributes.Critical)})",
            $"{localization.GetString("PlayerStatus_Haste")}: {FormatPercent(attributes.HastePercent / 100d)}% ({FormatInteger(attributes.Haste)})",
            $"{localization.GetString("PlayerStatus_Luck")}: {FormatPercent(attributes.LuckPercent / 100d)}% ({FormatInteger(attributes.Luck)})",
            $"{localization.GetString("PlayerStatus_Mastery")}: {FormatPercent(attributes.MasteryPercent / 100d)}% ({FormatInteger(attributes.Mastery)})",
            $"{localization.GetString("PlayerStatus_Versatility")}: {FormatPercent(attributes.VersatilityPercent / 100d)}% ({FormatInteger(attributes.Versatility)})",
            $"{localization.GetString("PlayerStatus_Block")}: {FormatPercent(attributes.BlockPercent / 100d)}%");
    }

    private static (string LabelKey, int Value) GetPrimaryStat(int professionId, PlayerCombatAttributes attributes)
    {
        return professionId switch
        {
            1 or 11 => ("PlayerStatus_Agility", attributes.Dexterity),
            2 or 5 or 13 => ("PlayerStatus_Intellect", attributes.Intelligence),
            _ => ("PlayerStatus_Strength", attributes.Strength)
        };
    }

    private static string FormatInteger(long value)
    {
        return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }
}
