using StarResonanceDps.App.Config;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>
/// バフ・デバフ一覧に出す行を、一覧の設定で絞る。プレイヤーから開いた一覧とエンティティリストから開いた一覧の両方が使う。
/// 絞るのは非表示リスト(右クリックで足したバフ)だけ。
/// </summary>
public static class BuffListSnapshotFilter
{
    /// <summary>
    /// プレイヤーのバフ(デバフ)一覧の行。一覧の窓と同じ作り方(設定の「同種バフの表示」で取り、非表示リストで絞る)。
    /// 一覧の窓・メイン画面のカードの選択リスト・バフ・デバフカードの「指定なし」が同じ行を見るように、ここ1か所で作る。
    /// </summary>
    public static IReadOnlyList<PlayerBuffSnapshot> GetPlayerRows(
        BuffListWidgetSettingsConfig settings,
        long characterId,
        PlayerBuffListKind kind)
    {
        return Apply(
            settings,
            MeterSnapshotProvider.GetPlayerBuffs(
                characterId,
                kind,
                WidgetConfigDefaults.GroupsSameBuffs(settings, isEntityTarget: false)));
    }

    /// <summary>落とす行が無ければ、受け取った一覧をそのまま返す(新しい一覧を作らない)。</summary>
    public static IReadOnlyList<PlayerBuffSnapshot> Apply(
        BuffListWidgetSettingsConfig settings,
        IReadOnlyList<PlayerBuffSnapshot> snapshots)
    {
        var dropsAny = false;
        for (var index = 0; index < snapshots.Count; index++)
        {
            if (!IsShown(settings, snapshots[index]))
            {
                dropsAny = true;
                break;
            }
        }

        return dropsAny
            ? [.. snapshots.Where(snapshot => IsShown(settings, snapshot))]
            : snapshots;
    }

    private static bool IsShown(BuffListWidgetSettingsConfig settings, PlayerBuffSnapshot snapshot)
        => !settings.HiddenBuffIds.Contains(snapshot.BaseId);
}
