using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class EquipBreakThroughTable
    {
        public Dictionary<string, EquipBreakThrough> Data = new();
    }

    public class EquipBreakThrough
    {
        public int Id { get; set; }
        public int EquipId { get; set; }
        public int BreakThroughTime { get; set; }
        public int EquipGs { get; set; }
        public List<int> BasicAttrLibId { get; set; } = null!;
        public List<int> AdvancedAttrLibId { get; set; } = null!;
        public List<int> QualityChildAttrLibId { get; set; } = null!;
        public List<List<int>> Condition { get; set; } = null!;
        public List<List<int>> Consume { get; set; } = null!;
        public List<float> ModelPos { get; set; } = null!;
        public List<float> ModelRot { get; set; } = null!;
        public float TimelineScale { get; set; }
        public List<float> ModelScale { get; set; } = null!;
        public float SsprHeight { get; set; }
        public int TimelineId { get; set; }
    }
}
