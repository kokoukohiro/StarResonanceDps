using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// プレイヤーリストの通知「HP低下」の判定。リストの行を組み直すたびに、表示している人(フィルターに従う、自分も含む)を渡す。
/// プレイヤーリストを開いている間だけ動く(起動中のウィジェットだけが通知する)。
///
/// <para>
/// HP が最大HPの15%を超えていたか 0(倒れた)だった人が、15%以下(0より大きい)になった組み直しで1回出す。15%を超えたら次も出す。
/// 15%を超えたところから 0 になった(即死)ときは出さない。0 から15%以下で復帰したとき(見え始めた時点で 0 だった人も)は出す。
/// 見え始めた時点で既に15%以下(0より大きい)の人と、最大HP が分からない人は出さない(下がったところを見ていないため)。
/// バリアは数えない。リストから外れた人の状態は捨てる。
/// </para>
/// </summary>
public sealed class PlayerListHealthNotificationTracker
{
    /// <summary>最大HPに対する割合(%)。ゲーム内で画面の周辺が赤くなるのと同じ割合。</summary>
    private const long ThresholdPercent = 15;

    private readonly Func<MeterWidgetSettingsConfig> _getSettings;

    /// <summary>人ごとの「次に15%以下(0より大きい)になったら出すか」(15%を超えていたか 0 だった)。鍵は CharacterId。</summary>
    private readonly Dictionary<long, bool> _isArmedByCharacterId = [];

    public PlayerListHealthNotificationTracker(Func<MeterWidgetSettingsConfig> getSettings)
    {
        _getSettings = getSettings;
    }

    /// <summary>窓を閉じた・行を作り直した。今までの観測を捨てる(通知しない)。</summary>
    public void Reset()
    {
        _isArmedByCharacterId.Clear();
    }

    public void Observe(IReadOnlyList<PlayerRosterEntry> visibleRoster, PlayerNameDisplayMode nameDisplayMode)
    {
        var visibleCharacterIds = new HashSet<long>();
        foreach (var player in visibleRoster)
        {
            visibleCharacterIds.Add(player.CharacterId);
            if (player.MaxHp <= 0)
            {
                _isArmedByCharacterId.Remove(player.CharacterId);
                continue;
            }

            var isDown = player.CurrentHp <= 0;
            var isAboveThreshold = player.CurrentHp * 100 > player.MaxHp * ThresholdPercent;
            if (!isAboveThreshold
                && !isDown
                && _isArmedByCharacterId.TryGetValue(player.CharacterId, out var wasArmed)
                && wasArmed)
            {
                Notify(PlayerInfoFormatFormatter.GetDisplayName(
                    player.Name,
                    player.CharacterId,
                    player.IsSelf,
                    player.IsNpc,
                    player.ProfessionId,
                    nameDisplayMode));
            }

            _isArmedByCharacterId[player.CharacterId] = isAboveThreshold || isDown;
        }

        foreach (var characterId in _isArmedByCharacterId.Keys.Where(id => !visibleCharacterIds.Contains(id)).ToArray())
        {
            _isArmedByCharacterId.Remove(characterId);
        }
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
