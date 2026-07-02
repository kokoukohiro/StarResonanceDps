using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class EquipAttrSchoolLibTable
    {
        public Dictionary<string, EquipAttrSchoolLib> Data = new();
    }

    public class EquipAttrSchoolLib
    {
        public int Id { get; set; }
        public int AttrLibId { get; set; }
        public List<int> TalentSchoolId { get; set; } = null!;
        public int SchoolNumber { get; set; }
        public List<List<int>> AttrEffect { get; set; } = null!;
        public List<List<string>> AttrEffectKey { get; set; } = null!;
        public List<List<int>> AttrEffectConfig { get; set; } = null!;
        public List<int> AllowPart { get; set; } = null!;
        public List<List<int>> FightValue { get; set; } = null!;
        public int ColorType { get; set; }

        public EquipAttrLib ToEquipAttrLib()
        {
            EquipAttrLib equipAttrLib = new()
            {
                Id = Id,
                AttrLibId = AttrLibId,
                AttrEffect = AttrEffect,
                AttrEffectKey = AttrEffectKey,
                AttrEffectConfig = AttrEffectConfig,
                AllowPart = AllowPart,
                FightValue = FightValue,
                ColorType = ColorType
            };

            return equipAttrLib;
        }
    }
}
