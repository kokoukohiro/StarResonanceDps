using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// ダメージ・回復イベントの発生源を、メーターの行に使うIDへ畳む。
///
/// <para>
/// イベントの <c>OwnerId</c> は <c>DamageSource</c> によって中身が変わる。
/// スキルID・弾ID・バフIDのどれかで、そのままではメーターの行にならない。
/// </para>
///
/// <list type="number">
///   <item>対応表 — 同じ行に属するIDを、その行の最若IDへ寄せる</item>
///   <item>見出し表 — キーがあればそのIDで出す。無ければ畳まない</item>
/// </list>
///
/// <para>
/// <b>畳み先を決めているのはゲーム内メーターの見出し表(<c>RecountTable</c>)だけ。</b>
/// 表は <c>DamageAttrTable.TypeEnum</c> を<b>生のまま</b>鍵にしてあるので、弾IDも直接載っている。
/// 実測(ログ188本 109万イベント)で <b>99.91%</b> が表に直接当たり、
/// 残る0.09%(18種960件)は弾の親を持たないモンスタースキルで、名前もプレースホルダ。
/// </para>
///
/// <para>
/// <b>弾ID規則(<c>SkillFightLevelTable</c> の親スキルへ登る)は撤去した。</b>
/// 表が弾IDを直接持つので寄与が0になるうえ、規則の答えはゲームの表と18件中16件で食い違う。
/// <c>230401</c> を <c>场地标记01</c> へ、<c>120401</c> を <c>场地标记01</c> へ畳んでいた。
/// </para>
///
/// <para>
/// <b>実行時の情報からは畳まない。</b> 以前はバフ実体の <c>FightSourceInfo</c>・
/// コンボの <c>NextSkillId</c>・召喚体の <c>AttrId</c>・ダメージ属性の式も辿っていたが、
/// ゲーム内の実挙動と食い違うことが実測で分かった(2026-09-06)。
/// </para>
/// </summary>
public static class SkillSourceResolver
{
    /// <summary>畳んだ結果。</summary>
    /// <param name="Id">メーターの行に使うID。</param>
    /// <param name="IsBuffSource">
    /// true ならバフとして届いた発生源。<b>名前には影響しない</b>(見出し表は種別を区別しない)。
    /// アイコンの引き先と、内部ID注記の表示区分にだけ効く。
    /// </param>
    public readonly record struct Result(int Id, bool IsBuffSource);

    /// <summary>発生源を畳む。</summary>
    public static Result Resolve(EDamageSource damageSource, int ownerId)
    {
        var isBuff = damageSource == EDamageSource.Buff;
        if (ownerId == 0)
        {
            return new Result(ownerId, isBuff);
        }

        // 1段目 — 同じ行に属するIDを最若IDへ寄せる。ゲーム内メーターの行構成そのもの。
        if (CombatDataCatalog.TryResolveRecountSource(ownerId, out var rowLeadId))
        {
            return new Result(rowLeadId, isBuff);
        }

        // 2段目 — 見出し表にキーがあれば、そのIDで出す。
        // 名前が空でも同じ。総括行(其他)は名前を消してあるが、行としては存在する。
        //
        // どちらにも無いIDは畳まずに ownerId のまま出す。表示は空文字になるが、
        // そのIDが表に足りていないことが見えるほうがよい。
        return new Result(ownerId, isBuff);
    }
}
