using System.Diagnostics;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public readonly record struct SelfChargeCooldownState(
    int AvailableCharges,
    double NextChargeRemainingSeconds,
    double FullRecoveryRemainingSeconds);

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

                switch (cooldown.SkillCDType)
                {
                    case ESkillCDType.EskillCdnormal:
                        UpdateNormalCooldown(skillId, cooldown, nowTimestamp);
                        break;
                    case ESkillCDType.EskillCdcharge:
                        UpdateChargeCooldown(skillId, cooldown, nowTimestamp);
                        break;
                    default:
                        SelfCooldowns.Remove(skillId);
                        break;
                }
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
            if (uuid != _selfUuid
                || !SelfCooldowns.TryGetValue(skillId, out var cooldown)
                || cooldown.Type != ESkillCDType.EskillCdnormal)
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

    public static SelfChargeCooldownState GetSelfChargeCooldownState(
        long uuid,
        int skillId,
        int fallbackMaxCharges)
    {
        var normalizedFallbackCharges = Math.Max(fallbackMaxCharges, 0);
        if (uuid == 0 || skillId <= 0 || normalizedFallbackCharges <= 1)
        {
            return default;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();

        lock (Sync)
        {
            if (uuid != _selfUuid
                || !SelfCooldowns.TryGetValue(skillId, out var cooldown)
                || cooldown.Type != ESkillCDType.EskillCdcharge
                || cooldown.DurationMilliseconds <= 0
                || cooldown.MaxCharges <= 1)
            {
                return new SelfChargeCooldownState(
                    normalizedFallbackCharges,
                    0d,
                    0d);
            }

            var maxCharges = cooldown.MaxCharges;
            var durationMilliseconds = cooldown.DurationMilliseconds;
            var totalDurationMilliseconds = (double)durationMilliseconds * maxCharges;
            var progressMilliseconds = Math.Clamp(
                GetProgressMilliseconds(cooldown, nowTimestamp),
                0d,
                totalDurationMilliseconds);

            if (progressMilliseconds >= totalDurationMilliseconds)
            {
                SelfCooldowns.Remove(skillId);
                return new SelfChargeCooldownState(
                    maxCharges,
                    0d,
                    0d);
            }

            var availableCharges = Math.Clamp(
                (int)Math.Floor(progressMilliseconds / durationMilliseconds),
                0,
                maxCharges);
            var partialProgressMilliseconds = progressMilliseconds
                - availableCharges * durationMilliseconds;
            var nextChargeRemainingMilliseconds = availableCharges >= maxCharges
                ? 0d
                : Math.Max(durationMilliseconds - partialProgressMilliseconds, 0d);
            var fullRecoveryRemainingMilliseconds = Math.Max(
                totalDurationMilliseconds - progressMilliseconds,
                0d);

            return new SelfChargeCooldownState(
                availableCharges,
                nextChargeRemainingMilliseconds / 1000d,
                fullRecoveryRemainingMilliseconds / 1000d);
        }
    }

    private static void UpdateNormalCooldown(
        int skillId,
        SkillCD cooldown,
        long nowTimestamp)
    {
        if (cooldown.BeginTime <= 0
            || cooldown.Duration <= 0
            || cooldown.ValidCDTime >= cooldown.Duration)
        {
            SelfCooldowns.Remove(skillId);
            return;
        }

        if (SelfCooldowns.TryGetValue(skillId, out var existing)
            && existing.Type == ESkillCDType.EskillCdnormal
            && existing.BeginTimeMilliseconds == cooldown.BeginTime)
        {
            existing.DurationMilliseconds = cooldown.Duration;
            existing.ProgressMilliseconds = Math.Max(cooldown.ValidCDTime, 0);
            existing.AnchorTimestamp = nowTimestamp;
            return;
        }

        var progressMilliseconds = ResolveInitialProgressMilliseconds(cooldown);
        if (progressMilliseconds >= cooldown.Duration)
        {
            SelfCooldowns.Remove(skillId);
            return;
        }

        SelfCooldowns[skillId] = new CooldownState
        {
            Type = ESkillCDType.EskillCdnormal,
            BeginTimeMilliseconds = cooldown.BeginTime,
            DurationMilliseconds = cooldown.Duration,
            MaxCharges = 1,
            ProgressMilliseconds = progressMilliseconds,
            AnchorTimestamp = nowTimestamp
        };
    }

    private static void UpdateChargeCooldown(
        int skillId,
        SkillCD cooldown,
        long nowTimestamp)
    {
        var maxCharges = CombatDataCatalog.GetSkillMaxCharges(skillId);
        if (cooldown.BeginTime <= 0
            || cooldown.Duration <= 0
            || maxCharges <= 1)
        {
            SelfCooldowns.Remove(skillId);
            return;
        }

        var totalDurationMilliseconds = (double)cooldown.Duration * maxCharges;
        var packetProgressMilliseconds = Math.Clamp(
            (double)Math.Max(cooldown.ValidCDTime, 0),
            0d,
            totalDurationMilliseconds);

        if (SelfCooldowns.TryGetValue(skillId, out var existing)
            && existing.Type == ESkillCDType.EskillCdcharge
            && existing.BeginTimeMilliseconds == cooldown.BeginTime)
        {
            existing.DurationMilliseconds = cooldown.Duration;
            existing.MaxCharges = maxCharges;
            existing.ProgressMilliseconds = Math.Max(
                packetProgressMilliseconds,
                Math.Min(
                    GetProgressMilliseconds(existing, nowTimestamp),
                    totalDurationMilliseconds));
            existing.AnchorTimestamp = nowTimestamp;
            return;
        }

        var progressMilliseconds = ResolveInitialProgressMilliseconds(cooldown);
        progressMilliseconds = Math.Clamp(
            progressMilliseconds,
            0d,
            totalDurationMilliseconds);
        if (progressMilliseconds >= totalDurationMilliseconds)
        {
            SelfCooldowns.Remove(skillId);
            return;
        }

        SelfCooldowns[skillId] = new CooldownState
        {
            Type = ESkillCDType.EskillCdcharge,
            BeginTimeMilliseconds = cooldown.BeginTime,
            DurationMilliseconds = cooldown.Duration,
            MaxCharges = maxCharges,
            ProgressMilliseconds = progressMilliseconds,
            AnchorTimestamp = nowTimestamp
        };
    }

    private static double ResolveInitialProgressMilliseconds(SkillCD cooldown)
    {
        double progressMilliseconds = Math.Max(cooldown.ValidCDTime, 0);
        if (!_hasClientServerTimeDelta)
        {
            return progressMilliseconds;
        }

        var serverNowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            - _clientServerTimeDeltaMilliseconds;
        var elapsedSinceBeginMilliseconds = Math.Max(
            serverNowMilliseconds - cooldown.BeginTime,
            0);

        return cooldown.ValidCDTime == 0
            ? elapsedSinceBeginMilliseconds * GetProgressRate()
            : Math.Max(progressMilliseconds, elapsedSinceBeginMilliseconds);
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
        public ESkillCDType Type { get; init; }

        public long BeginTimeMilliseconds { get; init; }

        public int DurationMilliseconds { get; set; }

        public int MaxCharges { get; set; }

        public double ProgressMilliseconds { get; set; }

        public long AnchorTimestamp { get; set; }
    }
}
