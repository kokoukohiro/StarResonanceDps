namespace StarResonanceDps.Core.CombatRuntime.Database.Migrations
{
    public class MigrationStatus
    {
        public MigrationStatusState State { get; set; } = MigrationStatusState.NotStarted;
        public BaseMigration? CurrentMigration { get; set; }
        public int TotalMigrationsNeeded { get; set; } = 0;
        public int CurrentMigrationNum { get; set; } = 0;
        public string ErrorMsg = "";
    }

    public enum MigrationStatusState
    {
        NotStarted,
        Running,
        CleanUp,
        Done,
        Error,
    }
}
