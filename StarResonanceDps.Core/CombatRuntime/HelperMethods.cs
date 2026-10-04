using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.Core.CombatRuntime;

public class HelperMethods
{
    public static class DataTables
    {
        public static MonsterTable Monsters = new MonsterTable();
        public static SkillTable Skills = new SkillTable();
        public static BuffTable Buffs = new BuffTable();
        public static SkillFightLevelTable SkillFightLevels = new SkillFightLevelTable();
        public static SceneEventDungeonConfigTable SceneEventDungeonConfigs = new SceneEventDungeonConfigTable();
        public static TempAttrTable TempAttrs = new TempAttrTable();
        public static ProfessionSystemTable ProfessionSystems = new ProfessionSystemTable();
        public static CookCuisineTable CookCuisines = new CookCuisineTable();
        public static FightAttrTable FightAttrs = new FightAttrTable();
    }
}
