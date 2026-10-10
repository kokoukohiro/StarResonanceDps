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

        public static bool MousePassthrough { get; set; } = false;

        public static bool IsUpdateAvailable { get; set; } = false;

        public static bool IsChatEnabled = true;

        public static long PartyTeamId = 0;

        public static Encounter? OpenedHistoricalEncounter = null;

        public static void LoadDataTables()
        {
            System.Diagnostics.Stopwatch loadTime = new();
            loadTime.Start();

            LoadRawTable<Monster>("MonsterTable.json", table => HelperMethods.DataTables.Monsters.Data = table);
            LoadRawTable<Skill>("SkillTable.json", table => HelperMethods.DataTables.Skills.Data = table);
            LoadRawTable<SkillFightLevel>("SkillFightLevelTable.json", table => HelperMethods.DataTables.SkillFightLevels.Data = table);
            LoadRawTable<Buff>("BuffTable.json", table => HelperMethods.DataTables.Buffs.Data = table);
            // CombatDataCatalog.Load が技のアイコンの枠を作るのに使う(変身クラスの技の番号)。
            LoadRawTable<ProfessionSystem>("ProfessionSystemTable.json", table => HelperMethods.DataTables.ProfessionSystems.Data = table);

            LoadBuffOverridesTable();
            LoadSkillOverridesTable();
            CombatDataCatalog.Load();

            LoadRawTable<SceneEventDungeonConfig>("SceneEventDuneonConfigTable.json", table => HelperMethods.DataTables.SceneEventDungeonConfigs.Data = table);
            LoadRawTable<TempAttr>("TempAttrTable.json", table => HelperMethods.DataTables.TempAttrs.Data = table);

            LoadCookCuisineTable();
            LoadFightAttrTable();

            var startupTime = loadTime.Elapsed.TotalSeconds;
            Serilog.Log.Debug($"Took {Math.Round(startupTime, 4)}s to load DataTables.");

            loadTime.Stop();
        }

        /// <summary>
        /// <c>Data/Raw</c> の生テーブルを1つ読み、<paramref name="assign"/> で置き場へ入れる。
        /// 無ければ、その表を引く機能が空のまま動くので、黙って飛ばさずエラーを出す。
        /// </summary>
        private static void LoadRawTable<T>(string fileName, Action<Dictionary<string, T>> assign)
        {
            string path = Path.Combine(CombatRuntimePaths.RawTableDirectory, fileName);
            if (!File.Exists(path))
            {
                Log.Error("{FileName} is missing. Lookups that use this table will be empty path={Path}", fileName, path);
                return;
            }

            assign(JsonConvert.DeserializeObject<Dictionary<string, T>>(File.ReadAllText(path))!);
            Log.Information("Loaded {FileName}", fileName);
        }

        /// <summary>
        /// 料理の表を読み、回復の料理が付けるバフと1回の回復量を引けるようにする。
        /// 表が無いと料理の回復を HPS に数えられないので、黙って空にせずエラーを出す。
        /// </summary>
        private static void LoadCookCuisineTable()
        {
            string cookCuisineTableFile = Path.Combine(CombatRuntimePaths.RawTableDirectory, "CookCuisineTable.json");
            if (!File.Exists(cookCuisineTableFile))
            {
                Log.Error("CookCuisineTable.json is missing. Cuisine healing cannot be counted in HPS path={Path}", cookCuisineTableFile);
                return;
            }

            var cookCuisines = JsonConvert.DeserializeObject<Dictionary<string, CookCuisine>>(File.ReadAllText(cookCuisineTableFile))!;
            HelperMethods.DataTables.CookCuisines.Data = cookCuisines;

            var amounts = new Dictionary<int, int>();
            var conflicts = new HashSet<int>();
            foreach (var cuisine in cookCuisines.Values)
            {
                if (cuisine.Description != CookCuisineTable.HealthRegenDescriptionId)
                {
                    continue;
                }

                foreach (var buffPar in cuisine.BuffPar)
                {
                    if (buffPar.Count < 2)
                    {
                        Log.Error("CookCuisineTable.json: healing cuisine {Id} has too few BuffPar entries ({Count})", cuisine.Id, buffPar.Count);
                        continue;
                    }

                    if (amounts.TryGetValue(buffPar[0], out var existing) && existing != buffPar[1])
                    {
                        conflicts.Add(buffPar[0]);
                    }

                    amounts[buffPar[0]] = buffPar[1];
                }
            }

            foreach (var buffId in conflicts)
            {
                Log.Error("CookCuisineTable.json: healing buff {BuffId} has rows with different amounts. This buff is not counted", buffId);
                amounts.Remove(buffId);
            }

            HelperMethods.DataTables.CookCuisines.RegenAmountsByBuffId = amounts.ToFrozenDictionary();
            Log.Information("Loaded CookCuisineTable.json");
        }

        /// <summary>
        /// 能力値の表を読み、能力値の属性番号を引けるようにする。
        /// 表が無いと入場のときに残った能力値を 0 に戻せないので、黙って空にせずエラーを出す。
        /// </summary>
        private static void LoadFightAttrTable()
        {
            string fightAttrTableFile = Path.Combine(CombatRuntimePaths.RawTableDirectory, "FightAttrTable.json");
            if (!File.Exists(fightAttrTableFile))
            {
                Log.Error("FightAttrTable.json is missing. Stats left over from the previous scene are not reset on EnterScene path={Path}", fightAttrTableFile);
                return;
            }

            var fightAttrs = JsonConvert.DeserializeObject<Dictionary<string, FightAttr>>(File.ReadAllText(fightAttrTableFile))!;
            HelperMethods.DataTables.FightAttrs.Data = fightAttrs;

            var statAttrIds = new HashSet<int>();
            foreach (var fightAttr in fightAttrs.Values)
            {
                if (!fightAttr.IsClass)
                {
                    continue;
                }

                foreach (var attrId in new[]
                {
                    fightAttr.Id,
                    fightAttr.AttrFinal,
                    fightAttr.AttrTotal,
                    fightAttr.AttrAdd,
                    fightAttr.AttrExAdd,
                    fightAttr.AttrPer,
                    fightAttr.AttrExPer,
                })
                {
                    if (attrId != 0)
                    {
                        statAttrIds.Add(attrId);
                    }
                }
            }

            HelperMethods.DataTables.FightAttrs.StatAttrIds = statAttrIds.ToFrozenSet();
            Log.Information("Loaded FightAttrTable.json");
        }

        /// <summary>
        /// 生の <c>SkillTable</c> が持たない <c>SkillLevelGroup</c> を補う。スキル枠のバッジの帰属が、
        /// 付与元の技から装備中のイマジン・ロールスキルへ辿るのに使う。
        /// 推移グラフの技のアイコンの枠(<c>CombatDataCatalog.GetSkillIconFrame</c>)も、この寄せ先で決まる
        /// (枠の番号を持たない技は <c>SkillLevelGroup</c> の先の技の枠になる)。
        ///
        /// <para>
        /// 書くのは <c>SkillLevelGroup</c> だけ。表に無いキーは、<c>BuffOverrides</c> と同じく行を作ってから当てる。
        /// 作る行は ID と <c>SkillLevelGroup</c> だけを持ち、文字列の項目は空にする。
        /// ファイルが無ければエラーログを出して上書き無しのまま続け、<c>SkillLevelGroup</c> が書かれていない項目は当てない。
        /// </para>
        /// </summary>
        public static void LoadSkillOverridesTable()
        {
            const string relativePath = "Overrides/SkillOverrides.json";
            var overridePath = Path.Combine(CombatRuntimePaths.OverridesDirectory, "SkillOverrides.json");
            if (!File.Exists(overridePath))
            {
                Log.Error("Failed to load {OverridePath}", relativePath);
                return;
            }

            var overrides = JsonConvert.DeserializeObject<Dictionary<string, SkillLevelGroupOverride>>(File.ReadAllText(overridePath))
                ?? throw new InvalidDataException($"{relativePath} が空です。");

            foreach (var (key, value) in overrides)
            {
                if (value.SkillLevelGroup is not > 0)
                {
                    continue;
                }

                if (!HelperMethods.DataTables.Skills.Data.TryGetValue(key, out var skill))
                {
                    skill = new Skill
                    {
                        Id = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture),
                        Icon = string.Empty,
                        Name = string.Empty,
                        Desc = string.Empty,
                        NameDesign = string.Empty,
                    };
                    HelperMethods.DataTables.Skills.Data.Add(key, skill);
                }

                skill.SkillLevelGroup = value.SkillLevelGroup.Value;
            }

            Log.Information("Loaded {OverridePath}", relativePath);
        }

        private sealed class SkillLevelGroupOverride
        {
            public int? SkillLevelGroup { get; set; }
        }

        public static void LoadBuffOverridesTable()
        {
            LoadBuffOverrideFile(Path.Combine("Overrides", "BuffOverrides.json"));
        }

        private static void LoadBuffOverrideFile(string relativePath)
        {
            var overridePath = Path.Combine(CombatRuntimePaths.DataDirectory, relativePath);
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
