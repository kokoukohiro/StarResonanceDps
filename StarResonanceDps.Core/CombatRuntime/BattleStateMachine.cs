using StarResonanceDps.Core.CombatRuntime.DataTypes;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class BattleStateMachine
    {
        public static ConcurrentQueue<KeyValuePair<EDungeonState, DateTime>> DungeonStateHistory { get; private set; } = new();
        public static ConcurrentQueue<KeyValuePair<DungeonTargetData, DateTime>> DungeonTargetDataHistory { get; private set; } = new();
        public static ConcurrentQueue<KeyValuePair<DungeonVar, DateTime>> DungeonVarHistory { get; private set; } = new();
        public static DateTime? DeferredEncounterStartTime { get; private set; } = null;
        public static EncounterStartReason DeferredEncounterStartReason { get; private set; } = EncounterStartReason.None;
        public static DateTime? DeferredEncounterEndFinalTime { get; private set; } = null;
        public static EncounterEndFinalData? DeferredEncounterEndFinalData { get; private set; } = null;
        static KeyValuePair<DungeonTargetData, DateTime>? PreviousDungeonTargetData = null;
        static KeyValuePair<DungeonVar, DateTime>? PreviousDungeonVar = null;
        static bool NewEncounterOnNextEncounterEnd = false;
        static readonly object BenchmarkCompletionSync = new();
        static System.Threading.Timer? BenchmarkCompletionTimer;

        public static void StartNewMap()
        {
            Log.Information($"{DateTime.Now} - BattleStateMachine.StartNewMap");
            PreviousDungeonTargetData = null;
            DeferredEncounterStartTime = null;
            DeferredEncounterStartReason = EncounterStartReason.None;
            DeferredEncounterEndFinalTime = null;

            DungeonVarHistory.Clear();
            DungeonTargetDataHistory.Clear();
            DungeonStateHistory.Clear();

            PreviousDungeonVar = null;
            NewEncounterOnNextEncounterEnd = false;

            EncounterManager.StartNewMap();
            EncounterManager.EnterDungeon(true, EncounterStartReason.Force);

            // マップが変わるとゲーム側のクールダウンはリセットされる。
            SkillCooldownStateStore.NotifyCooldownsResetByGame();

            // バフはエンカウンター境界では消さないが、マップ移動では持ち越さない。
            Services.ActiveBuffStore.Instance.Clear();

            if (!Settings.Instance.PersistEncounterSavingPauseStateBetweenMaps)
            {
                AppState.IsEncounterSavingPaused = false;
            }
        }

        public static void DungeonStateHistoryAdd(EDungeonState dungeonState)
        {
            DungeonStateHistory.Enqueue(new KeyValuePair<EDungeonState, DateTime>(dungeonState, DateTime.Now));
            Log.Information($"{DateTime.Now} - BattleStateMachine.DungeonStateHistoryAdd: {dungeonState}");

            if (dungeonState != EDungeonState.DungeonStateNull)
            {
                AppState.IsEncounterSavingPaused = false;
            }

            if (dungeonState == EDungeonState.DungeonStateNull)
            {

                EncounterManager.EnterDungeon();
                PlayerRosterProjection.ResetNearbyPlayers();

                if (Settings.Instance.PersistEncounterSavingPauseStateBetweenMaps)
                {
                    AppState.IsEncounterSavingPaused = AppState.WasEncounterSavingPaused;
                }
            }
            else if (dungeonState == EDungeonState.DungeonStatePlaying)
            {
                // ダンジョン開始でゲーム側のクールダウンはリセットされる。
                // 自分の残CDはサーバ真値なので勝手に戻るが、他プレイヤーの推測値は明示的に捨てる。
                SkillCooldownStateStore.NotifyCooldownsResetByGame();

                if (EncounterManager.Current.HasStatsBeenRecorded())
                {

                    EncounterManager.EnterDungeon(true, EncounterStartReason.Force);
                }
                else
                {
                    EncounterManager.EnterDungeon();
                }
            }
            else if (dungeonState == EDungeonState.DungeonStateEnd)
            {

                EncounterManager.StopEncounter(true, EncounterStartReason.DungeonStateEnd);

            }

        }

        public static void DungeonTargetDataHistoryAdd(DungeonTargetData dungeonTargetData)
        {

            if (DungeonTargetDataHistory.Count > 300)
            {
                DungeonTargetDataHistory.TryDequeue(out _);
            }

            var newDungeonTargetData = new KeyValuePair<DungeonTargetData, DateTime>(dungeonTargetData, DateTime.Now);

            DungeonTargetDataHistory.Enqueue(newDungeonTargetData);
            Log.Information($"{DateTime.Now} - BattleStateMachine.DungeonTargetDataHistoryAdd: TargetId={dungeonTargetData.TargetId}, Complete={dungeonTargetData.Complete}, Nums={dungeonTargetData.Nums}");

            if (DungeonTargetDataHistory.Count > 2 && dungeonTargetData.Complete == 0 && dungeonTargetData.Nums == 0)
            {

                var firstObjective = DungeonTargetDataHistory.First();
                if (firstObjective.Key != null && PreviousDungeonTargetData != null)
                {
                    if (firstObjective.Key.TargetId != 0 && PreviousDungeonTargetData.Value.Key.TargetId != 0 && PreviousDungeonTargetData.Value.Key.TargetId != firstObjective.Key.TargetId && firstObjective.Key.TargetId == dungeonTargetData.TargetId)
                    {
                        PreviousDungeonTargetData = newDungeonTargetData;
                        Log.Information($"{DateTime.Now} - BattleStateMachine.DungeonTargetDataHistoryAdd: RestartCheckHit!");

                        EncounterManager.StopEncounter();
                        EncounterManager.EnterDungeon(false, EncounterStartReason.Restart);
                        return;
                    }
                }
            }

            if (PreviousDungeonTargetData != null)
            {
                if (PreviousDungeonTargetData.Value.Key.Complete == 0 && dungeonTargetData.Complete == 0 && PreviousDungeonTargetData.Value.Key.TargetId == dungeonTargetData.TargetId)
                {

                    PreviousDungeonTargetData = newDungeonTargetData;
                    return;
                }
            }

            PreviousDungeonTargetData = newDungeonTargetData;

            if (Settings.Instance.SplitEncountersOnNewPhases)
            {
                if (dungeonTargetData.Complete == 0 && dungeonTargetData.Nums == 0)
                {

                    Log.Debug("DungeonTargetDataHistoryAdd - Deferring a New Objective EncounterStart");
                    DeferredEncounterStartReason = EncounterStartReason.NewObjective;
                    DeferredEncounterStartTime = DateTime.Now.AddSeconds(1);
                }
                else if (dungeonTargetData.Complete == 1 && dungeonTargetData.Nums > 0)
                {

                    EncounterManager.StopEncounter();

                    if (NewEncounterOnNextEncounterEnd)
                    {
                        NewEncounterOnNextEncounterEnd = false;

                        Log.Debug("DungeonTargetDataHistoryAdd - Objective Complete is requesting an EncounterStart as New Objective");

                        DeferredEncounterStartReason = EncounterStartReason.NewObjective;
                        DeferredEncounterStartTime = DateTime.Now.AddSeconds(1);
                    }
                }
            }
        }

        public static void DungeonVarHistoryAdd(DungeonVar dungeonVar)
        {

            if (DungeonVarHistory.Count > 300)
            {
                DungeonVarHistory.TryDequeue(out _);
            }

            var newDungeonVar = new KeyValuePair<DungeonVar, DateTime>(dungeonVar, DateTime.Now);

            DungeonVarHistory.Enqueue(newDungeonVar);

            foreach (var dungeonVarData in dungeonVar.DungeonVarData)
            {

                if (dungeonVarData.Name == "IsFinishTarget" && dungeonVarData.Value == 1)
                {
                    bool previousWasFinish = false;
                    if (PreviousDungeonVar != null)
                    {
                        foreach (var item in PreviousDungeonVar.Value.Key.DungeonVarData)
                        {
                            if (item.Name == "IsFinishTarget" && item.Value == 1)
                            {
                                previousWasFinish = true;
                            }
                        }
                    }

                    if (!previousWasFinish)
                    {
                        Log.Debug($"{DateTime.Now} - BattleStateMachine.DungeonVarHistoryAdd: IsFinishTarget == 1 for the first time. Next EncounterEnd will request EncounterStart");

                        NewEncounterOnNextEncounterEnd = true;
                    }
                }
            }

            PreviousDungeonVar = newDungeonVar;
        }

        public static void SetDeferredEncounterEndFinalData(DateTime dateTime, EncounterEndFinalData data)
        {
            if (DeferredEncounterEndFinalData != null && DeferredEncounterEndFinalData.EncounterId == data.EncounterId)
            {

                if (DeferredEncounterEndFinalTime == null)
                {

                    Log.Debug("SetDeferredEncounterEndFinalData - Encounter has already signaled the final end");
                    return;
                }
                else if (dateTime.CompareTo(DeferredEncounterEndFinalTime) >= 0)
                {

                    Log.Debug("SetDeferredEncounterEndFinalData - New time is not sooner than already set callback");
                    return;
                }
            }

            DeferredEncounterEndFinalTime = dateTime;
            DeferredEncounterEndFinalData = data;
        }

        public static void StartBenchmarkCompletionTimer()
        {
            lock (BenchmarkCompletionSync)
            {
                BenchmarkCompletionTimer?.Dispose();
                BenchmarkCompletionTimer = null;

                if (!AppState.IsBenchmarkMode
                    || !AppState.HasBenchmarkBegun
                    || AppState.IsBenchmarkCompleting
                    || AppState.IsBenchmarkCompleted)
                {
                    return;
                }

                var completionTime = EncounterManager.Current.StartTime.AddSeconds(AppState.BenchmarkTime);
                var dueTime = completionTime - DateTime.Now;
                if (dueTime < TimeSpan.Zero)
                {
                    dueTime = TimeSpan.Zero;
                }

                BenchmarkCompletionTimer = new System.Threading.Timer(
                    static _ => CompleteBenchmarkIfElapsed(DateTime.Now),
                    null,
                    dueTime,
                    System.Threading.Timeout.InfiniteTimeSpan);
            }
        }

        public static void CancelBenchmarkCompletionTimer()
        {
            lock (BenchmarkCompletionSync)
            {
                BenchmarkCompletionTimer?.Dispose();
                BenchmarkCompletionTimer = null;
                AppState.IsBenchmarkCompleting = false;
            }
        }

        public static void CompleteBenchmarkIfElapsed(DateTime currentTime)
        {
            if (!AppState.IsBenchmarkMode
                || !AppState.HasBenchmarkBegun
                || AppState.IsBenchmarkCompleting
                || AppState.IsBenchmarkCompleted)
            {
                return;
            }

            lock (BenchmarkCompletionSync)
            {
                if (!AppState.IsBenchmarkMode
                    || !AppState.HasBenchmarkBegun
                    || AppState.IsBenchmarkCompleting
                    || AppState.IsBenchmarkCompleted)
                {
                    return;
                }

                var completionTime = EncounterManager.Current.StartTime.AddSeconds(AppState.BenchmarkTime);
                if (currentTime < completionTime)
                {
                    BenchmarkCompletionTimer?.Change(
                        completionTime - currentTime,
                        System.Threading.Timeout.InfiniteTimeSpan);
                    return;
                }

                AppState.IsBenchmarkCompleting = true;
                BenchmarkCompletionTimer?.Dispose();
                BenchmarkCompletionTimer = null;

                try
                {
                    EncounterManager.FreezeCurrentBenchmarkMetrics(completionTime);
                    AppState.BenchmarkCompletionTime = completionTime;
                    AppState.IsBenchmarkCompleted = true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to complete benchmark");
                }
                finally
                {
                    AppState.IsBenchmarkCompleting = false;
                }
            }
        }

        public static void CheckDeferredCalls()
        {
            CompleteBenchmarkIfElapsed(DateTime.Now);

            if (DeferredEncounterStartTime.HasValue && DateTime.Now.CompareTo(DeferredEncounterStartTime) >= 0)
            {
                DeferredEncounterStartTime = null;

                EncounterManager.EnterDungeon(false, DeferredEncounterStartReason);

                DeferredEncounterStartReason = EncounterStartReason.None;
            }

            if (DeferredEncounterEndFinalTime.HasValue && DateTime.Now.CompareTo(DeferredEncounterEndFinalTime) >= 0)
            {
                DeferredEncounterEndFinalTime = null;

                EncounterManager.SignalEncounterEndFinal(DeferredEncounterEndFinalData!);

            }
        }

        public static bool IsInOpenWorld()
        {
            if (DungeonStateHistory.IsEmpty)
            {
                return true;
            }
            else if (DungeonStateHistory.Last().Key == EDungeonState.DungeonStateNull)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class EncounterEndFinalData
    {
        public ulong EncounterId;
        public int BattleId;
        public EncounterStartReason Reason;
        public Encounter? Encounter;
    }
}
