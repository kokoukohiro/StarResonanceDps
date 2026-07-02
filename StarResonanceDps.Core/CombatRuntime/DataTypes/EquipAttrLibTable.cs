using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class EquipAttrLibTable
    {
        public Dictionary<string, EquipAttrLib> Data = new();
    }

    public class EquipAttrLib
    {
        public int Id { get; set; }
        public int AttrLibId { get; set; }
        public List<List<int>> AttrEffect { get; set; } = null!;
        public List<List<string>> AttrEffectKey { get; set; } = null!;
        public List<List<int>> AttrEffectConfig { get; set; } = null!;
        public List<int> AllowPart { get; set; } = null!;
        public List<List<int>> FightValue { get; set; } = null!;
        public int ColorType { get; set; }
    }
}
