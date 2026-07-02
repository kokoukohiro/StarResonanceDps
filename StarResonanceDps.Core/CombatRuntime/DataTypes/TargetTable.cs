using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class TargetTable
    {
        public Dictionary<string, Target> Data = new();
    }

    public class Target
    {
        public string Id { get; set; } = null!;
        public int TargetType { get; set; }
        public int Num { get; set; }
        public int SceneId { get; set; }
        public List<int> Param { get; set; } = null!;
        public List<List<int>> TargetPos { get; set; } = null!;
        public bool IsTeamShare { get; set; }
        public string TargetDes { get; set; } = null!;
        public bool IsShowProgress { get; set; }
        public List<List<string>> SpVariable { get; set; } = null!;
        public List<List<string>> SpVariableLimit { get; set; } = null!;
        public List<string> SpVariableName { get; set; } = null!;
        public List<int> IsShowSpVariableProgress { get; set; } = null!;
        public List<string> ShowChange { get; set; } = null!;
    }
}
