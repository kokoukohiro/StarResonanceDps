namespace StarResonanceDps.Core.CombatRuntime
{
    public class EntityCache
    {
        public static EntityCache Instance = new();

        public EntityCacheFile Cache = new();
        private static readonly string LegacyFilePath = Path.Combine(Utils.DATA_DIR_NAME, "EntityCache.json");

        public void Initialize()
        {
            Cache = new();

            try
            {
                File.Delete(LegacyFilePath);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"Error deleting legacy EntityCache file:\n{ex.Message}\nStack Trace:\n{ex.StackTrace}");
            }
        }

        public EntityCacheLine? Get(long uuid)
        {
            if (Cache.Lines.TryGetValue(uuid, out var outLine))
            {
                return outLine;
            }

            return null;
        }

        public EntityCacheLine GetOrCreate(long uuid)
        {
            if (Cache.Lines.TryGetValue(uuid, out var outLine))
            {
                return outLine;
            }

            var newEntityCacheLine = new EntityCacheLine() { UUID = uuid, UID = Utils.UuidToEntityId(uuid) };
            Cache.Lines.TryAdd(uuid, newEntityCacheLine);

            return newEntityCacheLine;
        }

        public void Set(EntityCacheLine item)
        {
            if (Cache != null)
            {
                Cache.Lines[item.UUID] = item;
            }
        }

        public void SetName(long uuid, string name)
        {
            if (Cache != null)
            {
                if (Cache.Lines.TryGetValue(uuid, out var item))
                {
                    item.Name = name;
                }
                else
                {
                    Cache.Lines.TryAdd(uuid, new EntityCacheLine() { UUID = uuid, UID = Utils.UuidToEntityId(uuid), Name = name });
                }
            }
        }

        public void PortToDB()
        {
            DB.UpdateEntityCacheLines(Cache.Lines.Values);
        }
    }

    public class EntityCacheLine
    {
        public long UUID { get; set; }
        public long UID { get; set; }
        public string Name { get; set; } = "";
        public int Level { get; set; } = 0;
        public int AbilityScore { get; set; } = 0;
        public int ProfessionId { get; set; } = 0;
        public int SubProfessionId { get; set; } = 0;
        public int SeasonLevel { get; set; } = 0;
        public int SeasonStrength { get; set; } = 0;
    }

    public class EntityCacheFile
    {
        public System.Collections.Concurrent.ConcurrentDictionary<long, EntityCacheLine> Lines { get; set; } = [];
    }
}
