using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// バフ・デバフカード1枚の通知(効果時間切れ・薬剤・料理バフ2分以下)の判定。カードの1秒ごとの更新で、カードが見つけたバフを渡す。
/// カードを開いている間だけ動く(起動中のウィジェットだけが通知する)。
///
/// <para>
/// <b>効果時間切れ</b>: 前の更新で残りが1秒以下だったバフが、次の更新で消えていたら出す。
/// バフが消える通知に時間切れか解除かの理由は無いので、残り時間で決める。1秒はカードの更新間隔。
/// 残りがあるのに消えた(解除・死亡・マップ移動・周りから外れた)、持続の無いバフ、残りが分からないバフは出さない。
/// </para>
///
/// <para>
/// <b>薬剤・料理バフ2分以下</b>: 今のバフが料理・薬剤のとき、残りが2分を上から下回った更新で1回出す。2分を超えたら次も出す。
/// 開いた時点で既に2分以下なら出さない。
/// </para>
/// </summary>
public sealed class BuffCardNotificationTracker
{
    private const double ExpiryWindowSeconds = 1.0;
    private const double CuisinePotionLowSeconds = 120.0;

    private readonly Func<BuffCardWidgetSettingsConfig> _getSettings;

    private long? _lastTargetId;
    private bool _hasLastBuff;
    private double? _lastRemainingSeconds;
    private string _lastBuffName = string.Empty;
    private string _lastTargetName = string.Empty;
    private bool _isCuisinePotionArmed;

    public BuffCardNotificationTracker(Func<BuffCardWidgetSettingsConfig> getSettings)
    {
        _getSettings = getSettings;
    }

    /// <summary>対象が分からなくなった。前の観測を捨てる(通知しない)。</summary>
    public void Reset()
    {
        _lastTargetId = null;
        ResetBuff();
    }

    /// <param name="targetId">対象の ID。替わったら前の観測を捨てる(通知しない)。</param>
    /// <param name="snapshot">カードが見つけたバフ。消えていれば null。</param>
    /// <param name="targetName">カードに出している対象の名前。</param>
    public void Observe(long targetId, PlayerBuffSnapshot? snapshot, string targetName)
    {
        if (_lastTargetId != targetId)
        {
            ResetBuff();
            _lastTargetId = targetId;
        }

        if (snapshot is null)
        {
            if (_hasLastBuff && _lastRemainingSeconds is { } remaining && remaining <= ExpiryWindowSeconds)
            {
                Notify(
                    _getSettings().ExpiredNotificationFormatString,
                    WidgetNotificationTextFormatter.BuffCardExpiredDefaultKey,
                    _lastTargetName,
                    _lastBuffName);
            }

            ResetBuff();
            return;
        }

        var remainingSeconds = snapshot.IsRemainingUnknown ? null : snapshot.RemainingSeconds;
        var buffName = BuffDebuffCardContent.GetCardBuffName(snapshot.BaseId);

        if (CombatDataCatalog.GetBuffGroup(snapshot.BaseId) is not (BuffGroup.Cuisine or BuffGroup.Potion))
        {
            _isCuisinePotionArmed = false;
        }
        else if (remainingSeconds is { } cuisinePotionRemaining)
        {
            if (cuisinePotionRemaining > CuisinePotionLowSeconds)
            {
                _isCuisinePotionArmed = true;
            }
            else if (_isCuisinePotionArmed)
            {
                _isCuisinePotionArmed = false;
                Notify(
                    _getSettings().CuisinePotionLowNotificationFormatString,
                    WidgetNotificationTextFormatter.BuffCardCuisinePotionLowDefaultKey,
                    targetName,
                    buffName);
            }
        }

        _hasLastBuff = true;
        _lastRemainingSeconds = remainingSeconds;
        _lastBuffName = buffName;
        _lastTargetName = targetName;
    }

    private void ResetBuff()
    {
        _hasLastBuff = false;
        _lastRemainingSeconds = null;
        _lastBuffName = string.Empty;
        _lastTargetName = string.Empty;
        _isCuisinePotionArmed = false;
    }

    private static void Notify(string? savedFormat, string defaultKey, string targetName, string buffName)
    {
        var format = WidgetNotificationTextFormatter.ResolveFormat(savedFormat, defaultKey);
        if (WidgetNotificationTextFormatter.IsOff(format))
        {
            return;
        }

        NotificationService.Instance.Notify(WidgetNotificationTextFormatter.FormatBuffCard(format, targetName, buffName));
    }
}
