using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>
/// バフ・デバフ一覧の行の名前を、一覧の表示設定「バフ名」の書式と「付与者プレイヤー名の表示」で組む。
/// プレイヤーから開いた一覧とエンティティリストから開いた一覧の両方が使う。内部ID注記は書式を当てた後ろに付ける。
/// </summary>
public static class BuffListRowName
{
    /// <param name="targetName">対象者名(<c>{Name}</c>)。</param>
    /// <param name="level">対象者のレベル(<c>{Level}</c>)。</param>
    public static string Format(
        BuffListWidgetSettingsConfig settings,
        PlayerBuffSnapshot snapshot,
        string targetName,
        int level)
    {
        var casterName = ShowsCaster(settings, snapshot)
            ? BuffDebuffCardContent.GetCasterName(snapshot.Caster)
            : string.Empty;
        var name = BuffInfoFormatFormatter.Format(
            snapshot.NameWithoutInternalId,
            targetName,
            casterName,
            level,
            settings.BuffInfoFormatString);
        return CombatDataCatalog.AppendBuffInternalId(name, snapshot.BaseId);
    }

    /// <summary>「他者付与のみ」は、付与者が対象者本人(本人の召喚物を含む)なら出さない。</summary>
    private static bool ShowsCaster(BuffListWidgetSettingsConfig settings, PlayerBuffSnapshot snapshot)
        => settings.CasterNameDisplayModeIndex switch
        {
            WidgetConfigDefaults.CasterNameDisplayHideIndex => false,
            WidgetConfigDefaults.CasterNameDisplayAppliedByOthersIndex => !snapshot.IsCasterTarget,
            _ => true,
        };
}
