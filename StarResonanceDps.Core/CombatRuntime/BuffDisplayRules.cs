using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// バフ/デバフリスト(とそこから開くカード)に出すバフの条件。リスト(<c>MeterSnapshotProvider</c>)と
/// 常設の空欄検知(<c>Diagnostics.BlankBuffNameProbe</c>)が同じ判定を使う。
///
/// <para>
/// 2種類。ゲーム内のバフバーと同じ条件で出るもの(<see cref="IsShownByGameRule"/>)と、
/// それで落ちるもののうち足して出すもの(<see cref="IsAddedBuff"/>)。<b>足すほうは由来(付与元)で絞らない。</b>
/// 持続なし(0)と永続(負)は、ゲーム内のバフバーと同じ条件で出るなら出し(残り時間は出さない)、足すほうでは出さない。
/// </para>
///
/// <para>
/// <b>出すか出さないかを個別に決めるのは、手修正 <c>BuffOverrides.json</c> の優先度(<c>BuffPriority</c>)とアイコン。</b>
/// 優先度を書いたバフは足すほうに当たらず、ゲームの条件だけで決まる(0 なら出さない、0 以外でアイコンがあれば出す)。
/// モジュール効果のバフもこの規則で、名前と絵も番号ごとの手修正に書く。
/// </para>
///
/// <para>
/// リストの設定「モジュール効果」「装備効果」(どちらも すべて表示 / 非表示 / 常駐効果のみ非表示)はアプリの一覧が当てる。
/// ここではモジュール効果のバフか(<see cref="GetModuleEffectId"/>)、装備効果(セット効果・武器効果)のバフか(<see cref="IsEquipmentEffect"/>)を
/// 行に渡すだけ。どちらもバフの番号だけで決め、誰が付けたかでは変わらない。常駐は永続(持続が負)のもの。
/// </para>
///
/// <para>
/// バフ側かデバフ側かは表の種別(<c>BuffType</c>、手修正 <c>BuffOverrides.json</c> 込み)で決め、ここでは見ない。
/// </para>
/// </summary>
public static class BuffDisplayRules
{
    /// <summary>
    /// 足して出すバフの持続の境目(ミリ秒)。<b>これを超えるものだけ出す</b>(ちょうど1秒も出さない)。
    /// 持続なし(0)と永続(負)は出さない。
    /// </summary>
    public const int AddedBuffDurationThresholdMilliseconds = 1000;

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
    /// ゲーム内のバフバーと同じ条件(出す優先度で、アイコンがある)で出すバフか。
    /// アイコンはバフの絵(<see cref="CombatDataCatalog.GetBuffOwnIconName"/>、手修正を重ねた値)で見る。
    /// 手修正で優先度を0以外にしても、絵の無いバフは出ない。
    /// </summary>
    public static bool IsShownByGameRule(BuffEvent buffEvent)
        => !IsHiddenFromGameBuffBar(buffEvent)
           && buffEvent.BaseId > 0
           && !string.IsNullOrWhiteSpace(CombatDataCatalog.GetBuffOwnIconName(buffEvent.BaseId));

    /// <summary>
    /// リストに出すバフか(バフ側かデバフ側かは見ない)。ゲーム内のバフバーと同じ条件で出るものか、足して出すもの。
    /// </summary>
    public static bool IsListed(BuffEvent buffEvent, Entity bearer)
        => IsShownByGameRule(buffEvent) || IsAddedBuff(buffEvent, bearer);

    /// <summary>
    /// <see cref="IsShownByGameRule"/> で落ちるバフのうち、足して出すものか。
    /// 持続が <see cref="AddedBuffDurationThresholdMilliseconds"/> を超え、付く相手が <see cref="IsAddedBuffBearer"/> のもの。
    /// 持続なし・永続は出さない(時間の無い状態の印や数)。アイコンが無ければ空欄で出す。
    /// 手修正 <c>BuffOverrides.json</c> が優先度を書いたバフ(<see cref="CombatDataCatalog.HasBuffPriorityOverride"/>)は足さない。
    /// </summary>
    private static bool IsAddedBuff(BuffEvent buffEvent, Entity bearer)
        => !IsShownByGameRule(buffEvent)
           && !CombatDataCatalog.HasBuffPriorityOverride(buffEvent.BaseId)
           && buffEvent.Duration > AddedBuffDurationThresholdMilliseconds
           && IsAddedBuffBearer(bearer);

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

    /// <summary>どちらかのリスト(バフ・デバフ)に出るバフか。空欄検知が使う。</summary>
    public static bool IsDisplayed(BuffEvent buffEvent, Entity bearer)
        => buffEvent.BuffType is DataTypes.Enum.EBuffType.Debuff
               or DataTypes.Enum.EBuffType.Gain
               or DataTypes.Enum.EBuffType.GainRecovery
           && IsListed(buffEvent, bearer);

    /// <summary>
    /// 足して出すバフの付く相手か。プレイヤーか、モンスター。モンスターは召喚者
    /// (<c>AttrSummonerId</c>、無ければ <c>AttrTopSummonerId</c>)がプレイヤーなら外す(プレイヤーの召喚体)。
    /// 召喚の印(UUID のビット)では決めない。印の付いた本物の敵がいる。
    /// </summary>
    private static bool IsAddedBuffBearer(Entity bearer)
    {
        var entityType = Utils.UuidToEntityType(bearer.UUID);
        if (entityType == (long)EEntityType.EntChar)
        {
            return true;
        }

        if (entityType != (long)EEntityType.EntMonster)
        {
            return false;
        }

        var summonerUuid = bearer.GetAttrKV("AttrSummonerId") as long? ?? 0;
        if (summonerUuid == 0)
        {
            summonerUuid = bearer.GetAttrKV("AttrTopSummonerId") as long? ?? 0;
        }

        return summonerUuid == 0 || Utils.UuidToEntityType(summonerUuid) != (long)EEntityType.EntChar;
    }
}
