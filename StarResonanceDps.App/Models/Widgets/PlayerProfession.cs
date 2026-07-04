namespace StarResonanceDps.App.Models.Widgets;

public static class PlayerProfession
{
    public static string GetKey(int professionId)
    {
        return professionId switch
        {
            1 => "Stormblade",
            2 => "FrostMage",
            3 => "FlameBerserker",
            4 => "WindKnight",
            5 => "VerdantOracle",
            9 => "HeavyGuardian",
            11 => "Marksman",
            12 => "ShieldKnight",
            13 => "SoulMusician",
            _ => "Unknown"
        };
    }
}
