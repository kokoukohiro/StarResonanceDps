namespace StarResonanceDps.Core.Combat;

internal sealed class GameMapStateMachine
{
    private const int DungeonStateNone = 0;
    private const int DungeonStatePlaying = 3;
    private const int DungeonStateEnd = 4;

    private readonly Queue<int> _dungeonStateHistory = [];
    private readonly GameCombatStore _combatStore;

    public GameMapStateMachine(GameCombatStore combatStore)
    {
        _combatStore = combatStore;
    }

    public void StartNewMap()
    {
        _dungeonStateHistory.Clear();
        _combatStore.StartNewMap();
    }

    public void RecordDungeonState(int state, DateTimeOffset occurredAtUtc)
    {
        _dungeonStateHistory.Enqueue(state);
        _combatStore.SetDungeonState(state);

        if (state == DungeonStateNone)
        {
            _combatStore.StartNewDungeonEntry(force: false, occurredAtUtc);
            return;
        }

        if (state == DungeonStatePlaying)
        {
            _combatStore.StartNewDungeonEntry(_combatStore.HasRecordedCombatStatistics(), occurredAtUtc);
            return;
        }

        if (state == DungeonStateEnd)
        {
            _combatStore.CompleteDungeonEntry(occurredAtUtc);
        }
    }
}
