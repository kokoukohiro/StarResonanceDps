namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 推移グラフの横軸の下に出す技のアイコン1つ。値は整形済みで、作るのは ViewModel。
/// </summary>
/// <param name="Seconds">技の開始の、戦闘の時計の経過(秒)。</param>
/// <param name="IconPath">技のアイコンのファイル(<c>CombatIconResolver.ResolveSkillIcon</c>)。無ければ null で、クラス不明のアイコンで出す。</param>
/// <param name="Name">TIPS に出す技の名前。名前が無ければ「不明」。</param>
public sealed record MetricTimelineSkillMarker(double Seconds, string? IconPath, string Name);
