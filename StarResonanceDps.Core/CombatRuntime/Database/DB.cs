using StarResonanceDps.Core.CombatRuntime.Database;
using StarResonanceDps.Core.CombatRuntime.Database.Migrations;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace StarResonanceDps.Core.CombatRuntime
{
    public class DB
    {
        public const string DbFileName = "CombatHistory.db";
        public static string DbFilePath = Path.Combine(Utils.DATA_DIR_NAME, DbFileName);
        public static MigrationStatus MigrationStatus = new MigrationStatus();

        private static SqliteConnection DbConn = null!;
        private static ILogger Log = null!;
        private static ZstdSharp.Compressor Compressor = new ZstdSharp.Compressor();
        private static ZstdSharp.Decompressor Decompressor = new ZstdSharp.Decompressor();
        private static object DBLock = new object();
        private static readonly RuntimeSerializationBinder SerializationBinder = new();
        private static readonly List<BaseMigration> Migrations = [
                new SkillStatsMigration()
            ];

        public static void Init()
        {
            SQLitePCL.Batteries_V2.Init();
            Log = Log ?? Serilog.Log.Logger.ForContext<DB>();
            if (DbConn != null)
            {
                DbConn.Close();
                DbConn.Dispose();
            }

            var useFileDb = Settings.Instance.UseDatabaseForEncounterHistory;
            DbConn = new SqliteConnection($"Data Source={(useFileDb ? DbFilePath : ":memory:")}");
            DbConn.Open();

            DBSchema.CreateTables(DbConn);
        }

        public static void CloseAndSave()
        {
            if (DbConn != null)
            {
                lock (DBLock)
                {
                    DbConn.Close();
                }
            }
        }

        public static ulong GetNextEncounterId()
        {
            const string sql = "SELECT COALESCE(MAX(EncounterId), 0) + 1 FROM Encounters";
            return DbConn.QuerySingle<ulong>(sql);
        }

        public static ulong GetNumEncounters()
        {
            const string sql = "SELECT COUNT(*) FROM Encounters";
            return DbConn.QuerySingle<ulong>(sql);
        }

        public static ulong InsertEncounter(Encounter encounter)
        {
            using var transaction = DbConn.BeginTransaction();
            var result = InsertEncounter(encounter, transaction);
            transaction.Commit();

            return result;
        }

        public static ulong InsertEncounter(Encounter encounter, SqliteTransaction tx)
        {
            var sw = Stopwatch.StartNew();
            lock (DBLock)
            {
                using var encMs = new MemoryStream();
                ProtoBuf.Serializer.Serialize(encMs, encounter.ExData);
                encMs.Flush();
                encounter.ExDataBlob = Compressor.Wrap(encMs.ToArray()).ToArray();

                var encounterId = DbConn.QuerySingle<ulong>(DBSchema.Encounter.Insert, encounter, tx);
                encounter.EncounterId = encounterId;
                var entityBlob = CreateEntityBlobForEncounter(encounter);

                Log.Information($"Enounter's entityBlob.Data.Length = {entityBlob.Data.Length}");
                DbConn.Execute(DBSchema.Entities.Insert, entityBlob, tx);

                GC.Collect(2);

                sw.Stop();
                Log.Information("Saving encounter {encounterId} to DB took: {duration}", encounterId, sw.Elapsed);

                return encounterId;
            }
        }

        public static EntityBlobTable CreateEntityBlobForEncounter(Encounter encounter)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var compStream = new ZstdSharp.CompressionStream(memoryStream))
                {
                    using (var streamWriter = new StreamWriter(compStream, Encoding.UTF8, 1024, true))
                    {
                        using (var writer = new JsonTextWriter(streamWriter))
                        {
                            JsonSerializer serializer = new JsonSerializer()
                            {
                                Formatting = Formatting.None,
                                TypeNameHandling = TypeNameHandling.All,
                            };
                            lock (encounter.Entities)
                            {
                                serializer.Serialize(writer, encounter.Entities);
                            }
                            writer.Flush();
                        }
                        streamWriter.Flush();
                    }
                    compStream.Flush();
                }

                var entityBlob = new EntityBlobTable();
                entityBlob.EncounterId = encounter.EncounterId;
                entityBlob.Data = memoryStream.ToArray();

                return entityBlob;
            }
        }

        public static void UpdateEncounterWipeState(ulong encounterId, bool isWipe)
        {
            lock (DBLock)
            {
                DbConn.Execute(DBSchema.Encounter.UpdateIsWipe, new { EncounterId = encounterId, IsWipe = isWipe });
            }

            Log.Information("Updated {encounterId} IsWipe to: {isWipe}", encounterId, isWipe);
        }

        public static Encounter? LoadEncounter(ulong encounterId)
        {
            var sw = Stopwatch.StartNew();
            var encounter = DbConn.QuerySingleOrDefault<Encounter>(DBSchema.Encounter.SelectById, new { EncounterId = encounterId });

            if (encounter == null)
            {
                Log.Warning("Encounter {encounterId} not found in database", encounterId);
                return null;
            }

            var decompressedEncEx = Decompressor.Unwrap(encounter.ExDataBlob);
            ProtoBuf.Serializer.Deserialize<EncounterExData>(decompressedEncEx, encounter.ExData);
            encounter.ExDataBlob = null!;

            var entityBlob = DbConn.QuerySingleOrDefault<EntityBlobTable>(DBSchema.Entities.SelectByEncounterId, new { EncounterId = encounterId });
            if (entityBlob?.Data != null)
            {
                using (var memStream = new MemoryStream(entityBlob.Data))
                {
                    using (var decompStream = new ZstdSharp.DecompressionStream(memStream))
                    {
                        using (var streamReader = new StreamReader(decompStream))
                        {
                            using (JsonTextReader reader = new JsonTextReader(streamReader))
                            {
                                JsonSerializer serializer = new JsonSerializer()
                                {
                                    ContractResolver = new PrivateResolver(),
                                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                                    TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
                                    SerializationBinder = SerializationBinder
                                };
                                serializer.Converters.Add(new DictionaryObjectConverter());

                                encounter.Entities = serializer.Deserialize<ConcurrentDictionary<long, Entity>>(reader)!;
                            }
                        }
                    }
                }

                GC.Collect(2);
            }
            else
            {
                Log.Warning("Entities data not found for encounter {encounterId}", encounterId);
                encounter.Entities = new ConcurrentDictionary<long, Entity>();
            }

            sw.Stop();
            Log.Information("Loading encounter {encounterId} from DB took: {duration}", encounterId, sw.Elapsed);

            return encounter;
        }

        public static List<Encounter> LoadEncounterSummaries()
        {
            var encounters = DbConn.Query<Encounter>(DBSchema.Encounter.SelectAll).ToList();
            foreach (var encounter in encounters)
            {
                var decompressedEncEx = Decompressor.Unwrap(encounter.ExDataBlob);
                ProtoBuf.Serializer.Deserialize<EncounterExData>(decompressedEncEx, encounter.ExData);
                encounter.ExDataBlob = null!;
            }
            return encounters;
        }

        public static EncounterExData? GetEncounterExDataForBattle(int battleId)
        {
            try
            {
                var encounter = DB.DbConn.QueryFirst<Encounter>(DBSchema.Encounter.SelectOneByBattleId, new { BattleId = battleId });
                var decompressedEncEx = Decompressor.Unwrap(encounter.ExDataBlob);
                ProtoBuf.Serializer.Deserialize<EncounterExData>(decompressedEncEx, encounter.ExData);
                encounter.ExDataBlob = null!;

                return encounter.ExData;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting GetEncounterExDataForBattle by {battleId}", battleId);
            }

            return null;
        }

        public static DBCleanUpResults ClearOldEncounters(int olderThanDays)
        {
            lock (DBLock)
            {
                var date = DateTime.Now.AddDays(olderThanDays * -1);
                var results = new DBCleanUpResults();
                results.EncountersDeleted = DbConn.Execute(DBSchema.Encounter.RemoveEncountersOlderThan, new { Date = date });
                results.EntitiesCachesDeleted = DbConn.Execute(DBSchema.Entities.DeleteEntitiesCachesWithNoEncounters);
                results.BattlesDeleted = DbConn.Execute(DBSchema.Battles.DeleteBattlesWithNoEncounters);

                DbConn.Execute("VACUUM");

                Log.Information("Cleaned up {EncountersDeleted} encounters and {BattlesDeleted} battles, with {EntitesCachesDeleted} cachedEntities",
                        results.EncountersDeleted, results.BattlesDeleted, results.EntitiesCachesDeleted);

                return results;
            }
        }

        public static void DeleteEncounter(ulong encounterId)
        {
            lock (DBLock)
            {
                var tx = DbConn.BeginTransaction();
                DbConn.Execute(DBSchema.Encounter.RemoveEncounter, new { EncounterId = encounterId }, tx);
                DbConn.Execute(DBSchema.Entities.RemoveByEncounterId, new { EncounterId = encounterId }, tx);
                tx.Commit();

                Log.Information("Deleted Encounter: {encounterId}", encounterId);
            }
        }

        public static int GetNextBattleId()
        {
            const string sql = "SELECT COALESCE(MAX(BattleId), 0) + 1 FROM Battles";
            return DbConn.QuerySingle<int>(sql);
        }

        public static int StartBattle(uint sceneId, string sceneName)
        {
            var battle = new Battle()
            {
                SceneId = sceneId,
                SceneName = sceneName ?? "",
                StartTime = DateTime.Now
            };

            lock (DBLock)
            {
                var battleId = DbConn.QuerySingle<int>(DBSchema.Battles.Insert, battle);

                return battleId;
            }
        }

        public static void UpdateBattleInfo(int battleId, uint sceneId, string sceneName)
        {
            var battle = new Battle()
            {
                BattleId = battleId,
                SceneId = sceneId,
                SceneName = sceneName ?? ""
            };

            lock (DBLock)
            {
                DbConn.Execute(DBSchema.Battles.Update, battle);
            }
        }

        public static void UpdateBattleEnd(int battleId)
        {
            var battle = new Battle()
            {
                BattleId = battleId,
                EndTime = DateTime.Now
            };

            lock (DBLock)
            {
                DbConn.Execute(DBSchema.Battles.UpdateEndTime, battle);
            }
        }

        public static List<Battle> LoadBattles()
        {
            var battles = DbConn.Query<Battle>(DBSchema.Battles.SelectAll).ToList();
            return battles;
        }

        public static List<Encounter> LoadEncountersForBattleId(int battleId)
        {
            var encountersSum = DbConn.Query<Encounter>(DBSchema.Encounter.SelectByBattleId, new { BattleId = battleId });
            var encounters = new List<Encounter>(encountersSum.Count());

            foreach (var encounter in encountersSum)
            {
                var encounterFull = LoadEncounter(encounter.EncounterId);
                encounters.Add(encounterFull!);
            }

            return encounters;
        }






        public static bool CheckIfMigrationsNeeded()
        {
            var dbData = DbConn.Query<DbData>(DBSchema.DbData.Select).First();
            var migrationsToRun = Migrations.Where(x => x.MinVersion >= dbData.Version).ToList();

            return migrationsToRun.Count > 0;
        }

        public static void CheckAndRunMigrations()
        {
            var dbData = DbConn.Query<DbData>(DBSchema.DbData.Select).First();
            var migrationsToRun = Migrations.Where(x => x.MinVersion >= dbData.Version).ToList();

            if (migrationsToRun.Count() > 0)
            {
                Log.Information("{NumMigrations} Migrations to run", migrationsToRun.Count());
                MigrationStatus.State = MigrationStatusState.Running;
                MigrationStatus.TotalMigrationsNeeded = migrationsToRun.Count();
                MigrationStatus.CurrentMigrationNum = 0;

                foreach (var migration in migrationsToRun)
                {
                    MigrationStatus.CurrentMigrationNum++;
                    MigrationStatus.CurrentMigration = migration;
                    Log.Information("Starting migration {Num}, {Name}, {Description}", MigrationStatus.CurrentMigrationNum, migration.Name, migration.Description);
                    var tx = DbConn.BeginTransaction();
                    var result = migration.RunMigration(DbConn, tx);
                    if (!result)
                    {
                        Log.Error("Error running migration: {Name}", migration.Name);
                        MigrationStatus.State = MigrationStatusState.Error;
                        MigrationStatus.ErrorMsg = migration.ErrorMsg;
                        tx.Rollback();
                    }
                    else
                    {
                        DbConn.Execute(DBSchema.DbData.Update, new { Version = migration.NewVersion }, tx);
                        tx.Commit();
                        Log.Information("Migration {Num} {Name} Ran, Updated to version {Version}", MigrationStatus.CurrentMigrationNum, migration.Name, migration.NewVersion);
                    }
                }

                Log.Information("Migrations ran, cleaning up.");
                MigrationStatus.State = MigrationStatusState.CleanUp;
                DbConn.Execute("VACUUM");
            }

            MigrationStatus.State = MigrationStatusState.Done;
        }
    }

    public class DBCleanUpResults
    {
        public int EncountersDeleted { get; set; } = 0;
        public int BattlesDeleted { get; set; } = 0;
        public int EntitiesCachesDeleted { get; set; } = 0;
    }
}
