namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// バフ/デバフリスト(とそこから開くカード)に出すバフの条件。リスト(<c>MeterSnapshotProvider</c>)が使う。
///
/// <para>
/// 出すのはゲーム内のバフバーと同じ条件で出るもの(<see cref="IsShownByGameRule"/>)。持続なし(0)と永続(負)も出す(残り時間は出さない)。
/// </para>
///
/// <para>
/// <b>出すか出さないかを個別に決めるのは、手修正 <c>BuffOverrides.json</c> の優先度(<c>BuffPriority</c>)とアイコン。</b>
/// 読み込みが表に重ねた値で判定する(0 なら出さない、0 以外でアイコンがあれば出す)。
/// 手修正の <c>FoldInto</c> で畳んだバフは、読み込みが畳み先の優先度・絵・種別を写すので、畳み先と同じ判定になる。
/// </para>
///
/// <para>
/// リストの設定「モジュール効果」「装備効果」「因子効果」(どれも出すか出さないかのスイッチ)はアプリの一覧が当てる。
/// ここではモジュール効果のバフか(<see cref="GetModuleEffectId"/>)、装備効果(セット効果・武器効果)のバフか(<see cref="IsEquipmentEffect"/>)、
/// 因子のバフか(<see cref="IsFactorEffect"/>)を行に渡すだけ。どれもバフの番号だけで決め、誰が付けたかでは変わらない。
/// </para>
///
/// <para>
/// バフ側かデバフ側かは表の種別(<c>BuffType</c>、手修正 <c>BuffOverrides.json</c> 込み)で決め、ここでは見ない。
/// </para>
/// </summary>
public static class BuffDisplayRules
{
    /// <summary>
    /// ゲーム内のバフバーが出さないバフか。
    ///
    /// <para>
    /// ゲーム内のバフ表示は <c>BuffPriority</c> が <c>NotShow</c> のバフを出さない。
    /// 自分のバーもボスHPバーのバーも同じ条件で、対象エンティティが違うだけ。
    /// </para>
    ///
    /// <para>
    /// <b>アイコンの有無で判定しない。</b> 計数・マーカー・移動アクションの有効化など、
    /// アイコンを持っていてもゲームが出さないバフが多数ある。
    /// </para>
    /// </summary>
    public static bool IsHiddenFromGameBuffBar(BuffEvent buffEvent)
        => buffEvent.BuffPriority == DataTypes.Enum.EBuffPriority.NotShow;

    /// <summary>
    /// リストに出すバフか(バフ側かデバフ側かは見ない)。ゲーム内のバフバーと同じ条件(出す優先度で、アイコンがある)。
    /// アイコンはバフの絵(<see cref="CombatDataCatalog.GetBuffOwnIconName"/>、手修正を重ねた値)で見る。
    /// 手修正で優先度を0以外にしても、絵の無いバフは出ない。
    /// </summary>
    public static bool IsShownByGameRule(BuffEvent buffEvent)
        => !IsHiddenFromGameBuffBar(buffEvent)
           && buffEvent.BaseId > 0
           && !string.IsNullOrWhiteSpace(CombatDataCatalog.GetBuffOwnIconName(buffEvent.BaseId));

    /// <summary>
    /// モジュール効果のバフなら、そのモジュール効果の番号(<c>ModEffectTable.EffectID</c>)。違えば 0。
    /// 根のバフか根の子のバフかを、バフの番号だけで決める(<see cref="CombatDataCatalog.GetModEffectId"/>)。
    /// </summary>
    public static int GetModuleEffectId(BuffEvent buffEvent)
        => CombatDataCatalog.GetModEffectId(buffEvent.BaseId);

    /// <summary>
    /// 装備効果(セット効果・武器効果)のバフか。根のバフか根の子のバフかを、バフの番号だけで決める(<see cref="CombatDataCatalog.IsEquipmentEffectBuff"/>)。
    /// </summary>
    public static bool IsEquipmentEffect(BuffEvent buffEvent)
        => CombatDataCatalog.IsEquipmentEffectBuff(buffEvent.BaseId);

    /// <summary>
    /// 因子(シーズンタレントの因子)のバフか。バフの番号だけで決める(<see cref="CombatDataCatalog.IsFactorBuff"/>)。
    /// </summary>
    public static bool IsFactorEffect(BuffEvent buffEvent)
        => CombatDataCatalog.IsFactorBuff(buffEvent.BaseId);
}
