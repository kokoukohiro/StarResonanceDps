using StarResonanceDps.Core.Models;
using static StarResonanceDps.Core.CombatRuntime.DataTypes.Enums.Professions;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// アビリティ情報・潜在心相晶情報・モジュール情報の1行(根のバフ1つ)。値で比べる(中身が同じ行は差し替えない)。
/// </summary>
/// <param name="Name">名前(内部ID注記を含む)。</param>
/// <param name="IconPath">絵のファイル。分からない(型が分からない上級・ファイルが無い)ときは null。</param>
/// <param name="Kind">根のバフの種類。絵の大きさ・位置・影・背景を切り替える。</param>
/// <param name="TalentRole">タレントの役割。タレントの絵の背景の色を選ぶ。タレント以外は None。</param>
public sealed record PlayerRootBuffEntry(
    string Name,
    string? IconPath,
    RootBuffKind Kind,
    ERoleType TalentRole)
{
    /// <summary>絵が分からないか。はてなを出す。</summary>
    public bool ShowsUnknownIcon => IconPath is null;

    /// <summary>モジュール効果の絵か。バフ一覧と同じく、絵を小さくして左へずらす。</summary>
    public bool IsModuleEffectIcon => Kind == RootBuffKind.ModuleEffect;

    /// <summary>因子の絵か。バフ一覧と同じく、絵を左へずらす。</summary>
    public bool IsFactorIcon => Kind == RootBuffKind.Factor;

    /// <summary>心相晶のノード・上級の絵か。白い絵なので影を付ける。</summary>
    public bool IsSeasonTalentIcon => Kind is RootBuffKind.SeasonTalentNode or RootBuffKind.SeasonTalentAdvanced;

    /// <summary>タレントの絵か。役割の背景の上に小さく置く。</summary>
    public bool IsTalentIcon => Kind == RootBuffKind.Talent;

    /// <summary>タレントの背景が防御(青)か。</summary>
    public bool IsTankTalent => TalentRole == ERoleType.Tank;

    /// <summary>タレントの背景が支援(緑)か。</summary>
    public bool IsHealerTalent => TalentRole == ERoleType.Healer;

    /// <summary>タレントの背景が攻撃(赤)か。</summary>
    public bool IsDpsTalent => TalentRole == ERoleType.DPS;
}
