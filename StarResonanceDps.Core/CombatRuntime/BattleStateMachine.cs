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
        public static DateTime? DeferredEncounterEndFinalTime { get; private set; } = null;
        public static EncounterEndFinalData? DeferredEncounterEndFinalData { get; private set; } = null;
        static readonly object BenchmarkCompletionSync = new();
        static System.Threading.Timer? BenchmarkCompletionTimer;

        public static void StartNewMap()
        {
            Log.Information($"{DateTime.Now} - BattleStateMachine.StartNewMap");
            DeferredEncounterEndFinalTime = null;

            DungeonStateHistory.Clear();

            EncounterManager.StartNewMap();
            EncounterManager.EnterDungeon(true, EncounterStartReason.Force);

            // バフはエンカウンター境界では消さないが、マップ移動では持ち越さない。
            Services.ActiveBuffStore.Instance.Clear();
            Services.BuffInstanceIndex.Instance.Clear();
            Services.SummonSourceIndex.Instance.Clear();
            Services.SourceLandingResolver.Instance.Clear();
            Services.NearbyMonsterIndex.Instance.Clear();

        }

        /// <summary>
        /// ダンジョンの履歴・予約・前回の値を起動時の値に戻す(<see cref="StartNewMap"/> の前半と同じ消去)。ログアウト(ExitGame)で使う。
        /// エンカウンターと battle 行には触らない。
        /// </summary>
        internal static void ResetDungeonStateToStartup()
        {
            DeferredEncounterEndFinalTime = null;

            DungeonStateHistory.Clear();
        }

        /// <summary>
        /// 終わりの控え(前のエンカウンター)を起動時の値(null)に戻す。
        /// <b>エンカウンターを作り直した後に呼ぶこと。</b> 作り直しの中で前のエンカウンター用に入れ直され、その場で消化される。
        /// </summary>
        internal static void ClearEncounterEndFinalData()
        {
            DeferredEncounterEndFinalTime = null;
            DeferredEncounterEndFinalData = null;
        }

        public static void DungeonStateHistoryAdd(EDungeonState dungeonState)
        {
            DungeonStateHistory.Enqueue(new KeyValuePair<EDungeonState, DateTime>(dungeonState, DateTime.Now));
            Log.Information($"{DateTime.Now} - BattleStateMachine.DungeonStateHistoryAdd: {dungeonState}");


            if (dungeonState == EDungeonState.DungeonStateNull)
            {

                EncounterManager.EnterDungeon();
                PlayerRosterProjection.ResetNearbyPlayers();

            }
            else if (dungeonState == EDungeonState.DungeonStatePlaying)
            {
                // 開始までの待ち時間の記録を別の戦闘に分けるのはフェーズ分割の一部なので、無効なら区切らずに続ける。
                // 記録が無ければ区切りではなく開始時刻の付け直し(EnterDungeon の中)なので、設定によらず行う。
                if (!EncounterManager.Current.HasStatsBeenRecorded())
                {
                    EncounterManager.EnterDungeon();
                }
                else if (CombatRuntimeSettings.SplitEncountersOnNewPhases)
                {
                    EncounterManager.EnterDungeon(true, EncounterStartReason.Force);
                }
            }
            else if (dungeonState == EDungeonState.DungeonStateEnd)
            {

                EncounterManager.StopEncounter(true, EncounterStartReason.DungeonStateEnd);

            }

        }

        /// <summary>
        /// ダンジョンの目標が届いたことをログに残すだけ。区切りの判定には使わない
        /// (ボス部屋の入場はリセット系バフで見る。<see cref="Managers.MessageManager"/> の差分の処理)。
        /// </summary>
        public static void LogDungeonTarget(DungeonTargetData dungeonTargetData)
        {
            Log.Information($"{DateTime.Now} - BattleStateMachine.LogDungeonTarget: TargetId={dungeonTargetData.TargetId}, Complete={dungeonTargetData.Complete}, Nums={dungeonTargetData.Nums}");
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

            if (DeferredEncounterEndFinalTime.HasValue && DateTime.Now.CompareTo(DeferredEncounterEndFinalTime) >= 0)
            {
                DeferredEncounterEndFinalTime = null;

                EncounterManager.SignalEncounterEndFinal(DeferredEncounterEndFinalData!);

            }
        }

        /// <summary>
        /// ダンジョンが進行中(Playing)か。ボス部屋の入場の判定で使う。
        /// エンカウンターが持つ状態は区切りで消えるので、履歴の最後で見る。
        /// </summary>
        public static bool IsDungeonPlaying()
        {
            return !DungeonStateHistory.IsEmpty && DungeonStateHistory.Last().Key == EDungeonState.DungeonStatePlaying;
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
