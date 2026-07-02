using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class DummyTable
    {
        public Dictionary<string, Dummy> Data = new();
    }

    public class Dummy
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int SkillId { get; set; }
        public float WalkSpeed { get; set; }
        public float RunSpeed { get; set; }
        public List<int> SkillIds { get; set; } = null!;
        public int AITableReference { get; set; }
        public bool IsInitiative { get; set; }
        public bool IsFlying { get; set; }
        public string BirthEffect { get; set; } = null!;
        public string DeadEffect { get; set; } = null!;
        public string BirthAudio { get; set; } = null!;
        public string DeadAudio { get; set; } = null!;
        public List<int> Tags { get; set; } = null!;
        public bool IsNotGround { get; set; }
        public bool IsStatic { get; set; }
        public int SummonUpperLimit { get; set; }
        public int DefaultState { get; set; }
    }
}
