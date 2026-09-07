using StarResonanceDps.Core.CombatRuntime.DataTypes;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class AppState
    {
        public static long PlayerUUID { get; set; }
        public static long PlayerUID { get; set; }
        public static string AccountId { get; set; } = null!;
        public static string PlayerName { get; set; } = null!;
        public static int ProfessionId { get; set; }

        public static int PlayerMeterPlacement { get; set; }

        public static ulong PlayerTotalMeterValue { get; set; }
        public static double PlayerMeterValuePerSecond { get; set; }

        public static bool IsBenchmarkMode { get; set; }
        public static int BenchmarkTime { get; set; }
        public static bool HasBenchmarkBegun { get; set; }
        internal static bool IsBenchmarkCompleting { get; set; }
        public static bool IsBenchmarkCompleted { get; set; }
        public static DateTime? BenchmarkCompletionTime { get; set; }
        public static bool BenchmarkSingleTarget { get; set; }
        public static long BenchmarkSingleTargetUUID { get; set; }

        public static bool IsEncounterSavingPaused { get; set; } = false;
        public static bool WasEncounterSavingPaused { get; set; } = false;

        public static bool MousePassthrough { get; set; } = false;

        public static bool IsUpdateAvailable { get; set; } = false;

        public static bool IsChatEnabled = true;

        public static long PartyTeamId = 0;

        public static Encounter? ActiveEncounter = null;
        public static Encounter? OpenedHistoricalEncounter = null;

        public static void LoadDataTables()
        {
            System.Diagnostics.Stopwatch loadTime = new();
            loadTime.Start();

            string monsterTableFile = Path.Combine(Utils.DATA_DIR_NAME, "MonsterTable.json");
            if (File.Exists(monsterTableFile))
            {
                var monsters = JsonConvert.DeserializeObject<Dictionary<string, Monster>>(File.ReadAllText(monsterTableFile))!;
                HelperMethods.DataTables.Monsters.Data = monsters;
                Log.Information("Loaded MonsterTable.json");
            }

            string skillTableFile = Path.Combine(Utils.DATA_DIR_NAME, "SkillTable.json");
            if (File.Exists(skillTableFile))
            {
                var skills = JsonConvert.DeserializeObject<Dictionary<string, Skill>>(File.ReadAllText(skillTableFile))!;
                HelperMethods.DataTables.Skills.Data = skills;
                Log.Information("Loaded SkillTable.json");
            }





            string skillFightLevelTableFile = Path.Combine(Utils.DATA_DIR_NAME, "SkillFightLevelTable.json");
            if (File.Exists(skillTableFile))
            {
                var skillFightLevels = JsonConvert.DeserializeObject<Dictionary<string, SkillFightLevel>>(File.ReadAllText(skillFightLevelTableFile))!;
                HelperMethods.DataTables.SkillFightLevels.Data = skillFightLevels;
                Log.Information("Loaded SkillFightLevelTable.json");
            }




            string buffTableFile = Path.Combine(Utils.DATA_DIR_NAME, "BuffTable.json");
            if (File.Exists(buffTableFile))
            {
                var buffs = JsonConvert.DeserializeObject<Dictionary<string, Buff>>(File.ReadAllText(buffTableFile))!;
                HelperMethods.DataTables.Buffs.Data = buffs;
                Log.Information("Loaded BuffTable.json");
            }

            LoadBuffOverridesTable();
            CombatDataCatalog.Load();

            string sceneEventDungeonConfigTableFile = Path.Combine(Utils.DATA_DIR_NAME, "SceneEventDuneonConfigTable.json");
            if (File.Exists(sceneEventDungeonConfigTableFile))
            {
                var sceneEventDungeonConfigs = JsonConvert.DeserializeObject<Dictionary<string, SceneEventDungeonConfig>>(File.ReadAllText(sceneEventDungeonConfigTableFile))!;
                HelperMethods.DataTables.SceneEventDungeonConfigs.Data = sceneEventDungeonConfigs;
                Log.Information("Loaded SceneEventDuneonConfigTable.json");
            }


            string itemTableFile = Path.Combine(Utils.DATA_DIR_NAME, "ItemTable.json");
            if (File.Exists(itemTableFile))
            {
                var items = JsonConvert.DeserializeObject<Dictionary<string, Item>>(File.ReadAllText(itemTableFile))!;
                HelperMethods.DataTables.Items.Data = items;
                Log.Information("Loaded ItemTable.json");
            }

            string equipTableFile = Path.Combine(Utils.DATA_DIR_NAME, "EquipTable.json");
            if (File.Exists(equipTableFile))
            {
                var equips = JsonConvert.DeserializeObject<Dictionary<string, Equip>>(File.ReadAllText(equipTableFile))!;
                HelperMethods.DataTables.Equips.Data = equips;
                Log.Information("Loaded EquipTable.json");
            }





            string equipBreakThroughTableFile = Path.Combine(Utils.DATA_DIR_NAME, "EquipBreakThroughTable.json");
            if (File.Exists(equipBreakThroughTableFile))
            {
                var equipBreakThroughs = JsonConvert.DeserializeObject<Dictionary<string, EquipBreakThrough>>(File.ReadAllText(equipBreakThroughTableFile))!;
                HelperMethods.DataTables.EquipBreakThroughs.Data = equipBreakThroughs;
                Log.Information("Loaded EquipBreakThroughTable.json");
            }


            string tempAttrTableFile = Path.Combine(Utils.DATA_DIR_NAME, "TempAttrTable.json");
            if (File.Exists(tempAttrTableFile))
            {
                var tempAttrs = JsonConvert.DeserializeObject<Dictionary<string, TempAttr>>(File.ReadAllText(tempAttrTableFile))!;
                HelperMethods.DataTables.TempAttrs.Data = tempAttrs;
                Log.Information("Loaded TempAttrTable.json");
            }




            var startupTime = loadTime.Elapsed.TotalSeconds;
            Serilog.Log.Debug($"Took {Math.Round(startupTime, 4)}s to load DataTables.");

            loadTime.Stop();
        }

        public static void LoadBuffOverridesTable()
        {
            LoadBuffOverrideFile(Path.Combine("Overrides", "BuffOverrides.json"));
        }

        private static void LoadBuffOverrideFile(string relativePath)
        {
            var overridePath = Path.Combine(Utils.DATA_DIR_NAME, relativePath);
            if (!File.Exists(overridePath))
            {
                Log.Error("Failed to load {OverridePath}", relativePath);
                return;
            }

            var overrides = JsonConvert.DeserializeObject<Dictionary<string, Buff>>(File.ReadAllText(overridePath))
                ?? new Dictionary<string, Buff>();
            foreach (var item in overrides)
            {
                if (!HelperMethods.DataTables.Buffs.Data.TryGetValue(item.Key, out var buff))
                {
                    buff = new Buff
                    {
                        Id = string.IsNullOrWhiteSpace(item.Value.Id) ? item.Key : item.Value.Id,
                        Icon = string.Empty,
                        ShowHUDIcon = string.Empty,
                        Name = string.Empty,
                        Desc = string.Empty,
                        NameDesign = string.Empty
                    };
                    HelperMethods.DataTables.Buffs.Data.Add(item.Key, buff);
                }

                if (!string.IsNullOrWhiteSpace(item.Value.Icon))
                {
                    buff.Icon = item.Value.Icon == "-" ? string.Empty : item.Value.Icon;
                }

                if (!string.IsNullOrWhiteSpace(item.Value.ShowHUDIcon))
                {
                    buff.ShowHUDIcon = item.Value.ShowHUDIcon == "-" ? string.Empty : item.Value.ShowHUDIcon;
                }

                if (item.Value.BuffType.HasValue)
                {
                    buff.BuffType = item.Value.BuffType.Value;
                }

                if (item.Value.BuffPriority.HasValue)
                {
                    buff.BuffPriority = item.Value.BuffPriority.Value;
                }
            }

            Log.Information("Loaded {OverridePath}", relativePath);
        }
    }
}
