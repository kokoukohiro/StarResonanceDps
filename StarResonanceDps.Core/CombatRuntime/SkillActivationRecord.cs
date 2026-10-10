using ProtoBuf;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// プレイヤーの技の開始1件(周りの差分で <c>AttrSkillId</c> に技が入ったとき)。推移グラフの横軸の下にアイコンで出す。
/// 出現・入場の全属性に入っている技は、その時点で始めたものではないので残さない。
/// </summary>
[ProtoContract]
public sealed class SkillActivationRecord
{
    [ProtoMember(1)]
    public long PlayerUuid { get; set; }

    [ProtoMember(2)]
    public int SkillId { get; set; }

    /// <summary>技の開始が届いたパケットの到着時刻(UTC)。</summary>
    [ProtoMember(3)]
    public DateTime Timestamp { get; set; }
}
