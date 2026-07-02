using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class DungeonsTable
    {
        public Dictionary<string, Dungeons> Data = new();
    }

    public class Dungeons
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string DungeonTypeName { get; set; } = null!;
        public int FunctionID { get; set; }
        public int PlayType { get; set; }
        public int SceneID { get; set; }
        public List<List<int>> Condition { get; set; } = null!;
        public List<int> LimitedNum { get; set; } = null!;
        public int TeamType { get; set; }
        public List<List<int>> SingleAiCondition { get; set; } = null!;
        public List<int> SingleModeLimitNum { get; set; } = null!;
        public int SingleAiMode { get; set; }
        public int SingleModeDungeonId { get; set; }
        public int SingleAwardCounterId { get; set; }
        public bool IgnoreDungeonCheck { get; set; }
        public int KickTime { get; set; }
        public int EndTime { get; set; }
        public int MonsterGS { get; set; }
        public List<int> MonsterLv { get; set; } = null!;
        public List<int> Season { get; set; } = null!;
        public List<int> SeasonLv { get; set; } = null!;
        public List<int> SeasonRank { get; set; } = null!;
        public bool IsLoadRank { get; set; }
        public int ShowResultHudType { get; set; }
        public Vector3 ResultCurscenePos { get; set; }
        public List<int> ExploreConfig { get; set; } = null!;
        public List<List<int>> ExploreAward { get; set; } = null!;
        public int HideQuest { get; set; }
        public List<List<int>> DungeonTarget { get; set; } = null!;
        public List<int> FirstPassAward { get; set; } = null!;
        public List<int> PassAward { get; set; } = null!;
        public int CountLimit { get; set; }
        public int ExtraAward { get; set; }
        public string FailTexture { get; set; } = null!;
        public int DisableTransport { get; set; }
        public int ActiveStateTime { get; set; }
        public int ReadyStateTime { get; set; }
        public int PlayingStateTime { get; set; }
        public int SettlementStateTime { get; set; }
        public int ExitTransferType { get; set; }
        public List<int> Affix { get; set; } = null!;
        public int AffixPool { get; set; }
        public bool AttrGrowRangeBuff { get; set; }
        public int DungeonsAttrGrowRange { get; set; }
        public int DeathReleaseTime { get; set; }
        public int RecommendFightValue { get; set; }
        public int AssessId { get; set; }
        public int AffixEntityAttrId { get; set; }
        public List<int> AssitNumber { get; set; } = null!;
        public bool IsShowFakeAttr { get; set; }
        public bool CanRide { get; set; }
        public bool IsDpsTrackerOn { get; set; }
        public int CanSummoned { get; set; }
    }
}
