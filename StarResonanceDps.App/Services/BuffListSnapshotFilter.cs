using StarResonanceDps.App.Config;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>
/// バフ・デバフ一覧に出す行を、一覧の設定で絞る。プレイヤーから開いた一覧とエンティティリストから開いた一覧の両方が使う。
/// 絞るのは、非表示リスト(右クリックで足したバフ)と、設定「モジュール効果」(行の <see cref="PlayerBuffSnapshot.IsModuleEffect"/>)・
/// 「装備効果」(行の <see cref="PlayerBuffSnapshot.IsEquipmentEffect"/>)。
/// 常駐効果はどちらも永続 <see cref="PlayerBuffSnapshot.IsPermanent"/> のもの。
/// </summary>
public static class BuffListSnapshotFilter
{
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
    {
        if (settings.HiddenBuffIds.Contains(snapshot.BaseId))
        {
            return false;
        }

        if (snapshot.IsModuleEffect)
        {
            return settings.ModuleEffectDisplayModeIndex switch
            {
                WidgetConfigDefaults.HideModuleEffectDisplayModeIndex => false,
                WidgetConfigDefaults.HideResidentModuleEffectDisplayModeIndex => !snapshot.IsPermanent,
                _ => true
            };
        }

        if (snapshot.IsEquipmentEffect)
        {
            return settings.EquipmentEffectDisplayModeIndex switch
            {
                WidgetConfigDefaults.HideEquipmentEffectDisplayModeIndex => false,
                WidgetConfigDefaults.HideResidentEquipmentEffectDisplayModeIndex => !snapshot.IsPermanent,
                _ => true
            };
        }

        return true;
    }
}
