using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// バフ/デバフリスト(とそこから開くカード)に出すバフの条件。リスト(<c>MeterSnapshotProvider</c>)と
/// 常設の空欄検知(<c>Diagnostics.BlankBuffNameProbe</c>)が同じ判定を使う。
///
/// <para>
/// <b>モジュール効果のバフは、モジュール効果の出し方(<see cref="GetModuleBuffDisplay"/>)だけで決める</b>(ゲームの条件も持続も見ない)。
/// それ以外は2種類。ゲーム内のバフバーと同じ条件で出るもの(<see cref="IsShownByGameRule"/>)と、
/// それで落ちるもののうち足して出すもの(<see cref="IsAddedBuff"/>)。<b>足すほうは由来(付与元)で絞らない。</b>
/// 持続なし(0)と永続(負)は、ゲーム内のバフバーと同じ条件で出るなら出し(残り時間は出さない)、足すほうでは出さない。
/// </para>
///
/// <para>
/// リストの設定「モジュール効果」(すべて表示 / 非表示 / 常駐効果のみ非表示)はアプリの一覧が当てる。ここでは出し方を行に渡すだけ。
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

    /// <summary>ゲーム内のバフバーと同じ条件(出す優先度で、アイコンがある)で出すバフか。</summary>
    public static bool IsShownByGameRule(BuffEvent buffEvent)
        => !IsHiddenFromGameBuffBar(buffEvent)
           && buffEvent.BaseId > 0
           && !string.IsNullOrWhiteSpace(CombatDataCatalog.GetBuffOwnIconName(buffEvent.BaseId));

    /// <summary>
    /// リストに出すバフか(バフ側かデバフ側かは見ない)。モジュール効果のバフは出し方が <see cref="ModuleBuffDisplay.Hidden"/> 以外で、
    /// 付く相手が <see cref="IsAddedBuffBearer"/> のもの。それ以外は、ゲーム内のバフバーと同じ条件で出るものか、足して出すもの。
    /// </summary>
    public static bool IsListed(BuffEvent buffEvent, Entity bearer)
    {
        var moduleDisplay = GetModuleBuffDisplay(buffEvent);
        if (moduleDisplay != ModuleBuffDisplay.None)
        {
            return moduleDisplay != ModuleBuffDisplay.Hidden && IsAddedBuffBearer(bearer);
        }

        return IsShownByGameRule(buffEvent) || IsAddedBuff(buffEvent, bearer);
    }

    /// <summary>
    /// <see cref="IsShownByGameRule"/> で落ちるバフ(モジュール効果のバフを除く)のうち、足して出すものか。
    /// 持続が <see cref="AddedBuffDurationThresholdMilliseconds"/> を超え、付く相手が <see cref="IsAddedBuffBearer"/> のもの。
    /// 持続なし・永続は出さない(時間の無い状態の印や数)。アイコンが無ければ空欄で出す。
    /// </summary>
    private static bool IsAddedBuff(BuffEvent buffEvent, Entity bearer)
        => !IsShownByGameRule(buffEvent)
           && buffEvent.Duration > AddedBuffDurationThresholdMilliseconds
           && IsAddedBuffBearer(bearer);

    /// <summary>
    /// モジュール効果のバフなら、そのモジュール効果の番号(<c>ModEffectTable.EffectID</c>)。違えば 0。
    /// 番号で引ける(根のバフか、手修正 <c>ModuleBuffOverrides.json</c> の <c>Effect</c>)ならその効果、
    /// 引けなければ付与元をたどった由来(<see cref="BuffEvent.Origin"/>)がモジュール効果ならその効果(手修正にまだ無い子のバフ)。
    /// </summary>
    public static int GetModuleEffectId(BuffEvent buffEvent)
        => CombatDataCatalog.GetModEffectId(buffEvent.BaseId, buffEvent.Origin?.Landing ?? SourceLanding.None);

    /// <summary>
    /// モジュール効果のバフの出し方。手修正(<c>ModuleBuffOverrides.json</c>)に書かれたバフはその値、
    /// 書かれていないモジュール効果のバフ(<see cref="GetModuleEffectId"/> が 0 でない)は <see cref="ModuleBuffDisplay.Shown"/>、
    /// モジュール効果のバフでなければ <see cref="ModuleBuffDisplay.None"/>。
    /// </summary>
    public static ModuleBuffDisplay GetModuleBuffDisplay(BuffEvent buffEvent)
    {
        if (buffEvent.BaseId > 0 && CombatDataCatalog.TryGetModuleBuffDisplay(buffEvent.BaseId, out var display))
        {
            return display;
        }

        return GetModuleEffectId(buffEvent) > 0 ? ModuleBuffDisplay.Shown : ModuleBuffDisplay.None;
    }

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
