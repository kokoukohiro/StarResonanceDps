using System.Diagnostics;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public static class SkillCooldownStateStore
{
    private static readonly object Sync = new();
    private static readonly Dictionary<int, CooldownState> SelfCooldowns = [];

    private static long _selfUuid;
    private static int _accelerationPct;
    private static long _clientServerTimeDeltaMilliseconds;
    private static bool _hasClientServerTimeDelta;

    public static void Reset()
    {
        lock (Sync)
        {
            SelfCooldowns.Clear();
            _selfUuid = 0;
            _accelerationPct = 0;
            _clientServerTimeDeltaMilliseconds = 0;
            _hasClientServerTimeDelta = false;
        }
    }

    public static void SetSelfPlayer(long uuid)
    {
        if (uuid == 0)
        {
            return;
        }

        lock (Sync)
        {
            EnsureSelfPlayer(uuid);
        }
    }

    public static void UpdateServerTime(long clientMilliseconds, long serverMilliseconds)
    {
        if (clientMilliseconds <= 0 || serverMilliseconds <= 0)
        {
            return;
        }

        lock (Sync)
        {
            _clientServerTimeDeltaMilliseconds = clientMilliseconds - serverMilliseconds;
            _hasClientServerTimeDelta = true;
        }
    }

    public static void UpdateSelfCooldowns(long uuid, IEnumerable<SkillCD> cooldowns)
    {
        if (uuid == 0)
        {
            return;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            EnsureSelfPlayer(uuid);

            foreach (var cooldown in cooldowns)
            {
                if (!TryResolveSkillId(cooldown.SkillLevelId, out var skillId))
                {
                    continue;
                }

                if (cooldown.SkillCDType != ESkillCDType.EskillCdnormal)
                {
                    SelfCooldowns.Remove(skillId);
                    continue;
                }

                if (cooldown.BeginTime <= 0
                    || cooldown.Duration <= 0
                    || cooldown.ValidCDTime >= cooldown.Duration)
                {
                    SelfCooldowns.Remove(skillId);
                    continue;
                }

                if (SelfCooldowns.TryGetValue(skillId, out var existing)
                    && existing.BeginTimeMilliseconds == cooldown.BeginTime)
                {
                    existing.DurationMilliseconds = cooldown.Duration;
                    existing.ProgressMilliseconds = Math.Max(cooldown.ValidCDTime, 0);
                    existing.AnchorTimestamp = nowTimestamp;
                    continue;
                }

                double progressMilliseconds = Math.Max(cooldown.ValidCDTime, 0);
                if (_hasClientServerTimeDelta)
                {
                    var serverNowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        - _clientServerTimeDeltaMilliseconds;
                    var elapsedSinceBeginMilliseconds = Math.Max(
                        serverNowMilliseconds - cooldown.BeginTime,
                        0);

                    progressMilliseconds = cooldown.ValidCDTime == 0
                        ? elapsedSinceBeginMilliseconds * GetProgressRate()
                        : Math.Max(progressMilliseconds, elapsedSinceBeginMilliseconds);
                }

                if (progressMilliseconds >= cooldown.Duration)
                {
                    SelfCooldowns.Remove(skillId);
                    continue;
                }

                SelfCooldowns[skillId] = new CooldownState
                {
                    BeginTimeMilliseconds = cooldown.BeginTime,
                    DurationMilliseconds = cooldown.Duration,
                    ProgressMilliseconds = progressMilliseconds,
                    AnchorTimestamp = nowTimestamp
                };
            }
        }
    }

    public static void UpdateSelfAcceleration(long uuid, int accelerationPct)
    {
        if (uuid == 0)
        {
            return;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            EnsureSelfPlayer(uuid);

            foreach (var cooldown in SelfCooldowns.Values)
            {
                cooldown.ProgressMilliseconds = GetProgressMilliseconds(cooldown, nowTimestamp);
                cooldown.AnchorTimestamp = nowTimestamp;
            }

            _accelerationPct = Math.Max(accelerationPct, 0);
        }
    }

    public static double? GetSelfRemainingSeconds(long uuid, int skillId)
    {
        if (uuid == 0 || skillId <= 0)
        {
            return null;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            if (uuid != _selfUuid || !SelfCooldowns.TryGetValue(skillId, out var cooldown))
            {
                return null;
            }

            var remainingMilliseconds = cooldown.DurationMilliseconds
                - GetProgressMilliseconds(cooldown, nowTimestamp);
            if (remainingMilliseconds <= 0)
            {
                SelfCooldowns.Remove(skillId);
                return null;
            }

            return remainingMilliseconds / 1000d;
        }
    }

    private static void EnsureSelfPlayer(long uuid)
    {
        if (_selfUuid == uuid)
        {
            return;
        }

        SelfCooldowns.Clear();
        _selfUuid = uuid;
        _accelerationPct = 0;
    }

    private static double GetProgressMilliseconds(CooldownState cooldown, long nowTimestamp)
    {
        var elapsedMilliseconds = (nowTimestamp - cooldown.AnchorTimestamp)
            * 1000d
            / Stopwatch.Frequency;
        return cooldown.ProgressMilliseconds + elapsedMilliseconds * GetProgressRate();
    }

    private static double GetProgressRate()
    {
        return 1d + _accelerationPct / 10000d;
    }

    private static bool TryResolveSkillId(int skillLevelId, out int skillId)
    {
        skillId = 0;
        if (skillLevelId <= 0
            || !HelperMethods.DataTables.SkillFightLevels.Data.TryGetValue(
                skillLevelId.ToString(),
                out var skillFightLevel)
            || skillFightLevel.SkillId <= 0)
        {
            return false;
        }

        skillId = skillFightLevel.SkillId;
        return true;
    }

    private sealed class CooldownState
    {
        public long BeginTimeMilliseconds { get; init; }

        public int DurationMilliseconds { get; set; }

        public double ProgressMilliseconds { get; set; }

        public long AnchorTimestamp { get; set; }
    }
}
