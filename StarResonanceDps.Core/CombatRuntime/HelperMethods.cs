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
        public static ItemTable Items = new ItemTable();
        public static EquipTable Equips = new EquipTable();
        public static EquipBreakThroughTable EquipBreakThroughs = new EquipBreakThroughTable();
        public static TempAttrTable TempAttrs = new TempAttrTable();
        public static CookCuisineTable CookCuisines = new CookCuisineTable();
    }
}
