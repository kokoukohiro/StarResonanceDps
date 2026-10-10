using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// プレイヤーリストの通知「HP低下」の判定。リストの行を組み直すたびに、表示している人(フィルターに従う、自分も含む)を渡す。
/// プレイヤーリストを開いている間だけ動く(起動中のウィジェットだけが通知する)。
///
/// <para>
/// HP が最大HPの15%を超えていたか 0(倒れた)だった人が15%以下(0より大きい)になったら、その時刻を控え、15%以下が3秒続いたら1回出す。
/// 3秒の間に15%を超えるか 0 になった(倒れた)ら出さない(すぐ回復した人と、通知しても間に合わない人)。15%を超えたら次も出す。
/// 15%を超えたところから 0 になった(即死)ときは出さない。0 から15%以下で復帰したとき(見え始めた時点で 0 だった人も)も、3秒続いたら出す。
/// 見え始めた時点で既に15%以下(0より大きい)の人と、最大HP が分からない人は出さない(下がったところを見ていないため)。
/// バリアは数えない。リストから外れた人の状態は捨てる。
/// </para>
///
/// <para>
/// 3秒は時計で数える。HP が15%以下のまま変わらないと名簿が届かず、組み直しを待つと判定の機会が来ないため。
/// 判定に使うのは最後に届いた名簿の HP。
/// </para>
/// </summary>
public sealed class PlayerListHealthNotificationTracker
{
    /// <summary>最大HPに対する割合(%)。ゲーム内で画面の周辺が赤くなるのと同じ割合。</summary>
    private const long ThresholdPercent = 15;

    /// <summary>15%以下が続いたら出すまでの時間。</summary>
    private const long NotifyDelayMilliseconds = 3000;

    /// <summary>控えた人の経過を確かめる間隔。プレイヤーリストの更新と同じ間隔。</summary>
    private const int CheckIntervalMilliseconds = 250;

    private readonly Func<MeterWidgetSettingsConfig> _getSettings;

    /// <summary>人ごとの「次に15%以下(0より大きい)になったら控えるか」(15%を超えていたか 0 だった)。鍵は CharacterId。</summary>
    private readonly Dictionary<long, bool> _isArmedByCharacterId = [];

    /// <summary>
    /// 15%以下になって、まだ出していない人。15%以下に入った時刻(<see cref="Environment.TickCount64"/>)と、出すときの名前
    /// (最後に届いた名簿の値)。鍵は CharacterId。
    /// </summary>
    private readonly Dictionary<long, (long Since, string Name)> _lowSinceByCharacterId = [];

    private readonly DispatcherTimer _checkTimer;

    public PlayerListHealthNotificationTracker(Func<MeterWidgetSettingsConfig> getSettings)
    {
        _getSettings = getSettings;
        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CheckIntervalMilliseconds)
        };
        _checkTimer.Tick += CheckTimer_Tick;
    }

    /// <summary>窓を閉じた・行を作り直した。今までの観測を捨てる(通知しない)。</summary>
    public void Reset()
    {
        _isArmedByCharacterId.Clear();
        _lowSinceByCharacterId.Clear();
        _checkTimer.Stop();
    }

    public void Observe(IReadOnlyList<PlayerRosterEntry> visibleRoster, PlayerNameDisplayMode nameDisplayMode)
    {
        var visibleCharacterIds = new HashSet<long>();
        var now = Environment.TickCount64;
        foreach (var player in visibleRoster)
        {
            visibleCharacterIds.Add(player.CharacterId);
            if (player.MaxHp <= 0)
            {
                _isArmedByCharacterId.Remove(player.CharacterId);
                _lowSinceByCharacterId.Remove(player.CharacterId);
                continue;
            }

            var isDown = player.CurrentHp <= 0;
            var isAboveThreshold = player.CurrentHp * 100 > player.MaxHp * ThresholdPercent;
            if (isAboveThreshold || isDown)
            {
                _lowSinceByCharacterId.Remove(player.CharacterId);
            }
            else if (_lowSinceByCharacterId.TryGetValue(player.CharacterId, out var pending))
            {
                _lowSinceByCharacterId[player.CharacterId] = (pending.Since, GetDisplayName(player, nameDisplayMode));
            }
            else if (_isArmedByCharacterId.TryGetValue(player.CharacterId, out var wasArmed) && wasArmed)
            {
                _lowSinceByCharacterId[player.CharacterId] = (now, GetDisplayName(player, nameDisplayMode));
            }

            _isArmedByCharacterId[player.CharacterId] = isAboveThreshold || isDown;
        }

        foreach (var characterId in _isArmedByCharacterId.Keys.Where(id => !visibleCharacterIds.Contains(id)).ToArray())
        {
            _isArmedByCharacterId.Remove(characterId);
        }

        foreach (var characterId in _lowSinceByCharacterId.Keys.Where(id => !visibleCharacterIds.Contains(id)).ToArray())
        {
            _lowSinceByCharacterId.Remove(characterId);
        }

        UpdateCheckTimer();
    }

    /// <summary>15%以下に入ってから3秒たった人に出す(最後に届いた名簿で、まだ15%以下のまま)。</summary>
    private void CheckTimer_Tick(object? sender, EventArgs e)
    {
        var now = Environment.TickCount64;
        foreach (var (characterId, pending) in _lowSinceByCharacterId.ToArray())
        {
            if (now - pending.Since < NotifyDelayMilliseconds)
            {
                continue;
            }

            _lowSinceByCharacterId.Remove(characterId);
            Notify(pending.Name);
        }

        UpdateCheckTimer();
    }

    /// <summary>控えた人が居る間だけ時計を動かす。</summary>
    private void UpdateCheckTimer()
    {
        if (_lowSinceByCharacterId.Count == 0)
        {
            _checkTimer.Stop();
            return;
        }

        if (!_checkTimer.IsEnabled)
        {
            _checkTimer.Start();
        }
    }

    private static string GetDisplayName(PlayerRosterEntry player, PlayerNameDisplayMode nameDisplayMode)
    {
        return PlayerInfoFormatFormatter.GetDisplayName(
            player.Name,
            player.CharacterId,
            player.IsSelf,
            player.IsNpc,
            player.ProfessionId,
            nameDisplayMode);
    }

    private void Notify(string name)
    {
        var format = WidgetNotificationTextFormatter.ResolveFormat(
            _getSettings().HealthLowNotificationFormatString,
            WidgetNotificationTextFormatter.HealthLowDefaultKey);
        if (WidgetNotificationTextFormatter.IsOff(format))
        {
            return;
        }

        NotificationService.Instance.Notify(WidgetNotificationTextFormatter.FormatHealthLow(format, name));
    }
}
