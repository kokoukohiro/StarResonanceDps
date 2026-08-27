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
            // 変身クラス。ゲーム側の ProfessionTable.ProfessionIcon が
            // 8/14/15 とも profession_horizontal_hud00 で同じなので、1つのキーに束ねる。
            8 or 14 or 15 => "Transformation",
            _ => "Unknown"
        };
    }
}
