using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Zproto.World.Types;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class EquipEnchantTable
    {
        public Dictionary<string, EquipEnchant> Data = new();
    }

    public class EquipEnchant
    {
        public int Id { get; set; }
        public int EnchantId { get; set; }
        public int EnchantType { get; set; }
        public List<int> EnchantItemList { get; set; } = null!;
        public List<int> RecommendedGem { get; set; } = null!;
    }
}
