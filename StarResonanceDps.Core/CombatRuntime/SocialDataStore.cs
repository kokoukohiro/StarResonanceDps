using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>顔写真・名刺の半身写真の状態。写真はゲームの審査を通ったものだけ使う。</summary>
/// <param name="AvatarId">顔の種類。0 か 1 はアップロードした写真、それ以外はゲームの用意したアイコン。</param>
public sealed record SocialAvatarState(
    int AvatarId,
    string ProfileUrl,
    bool IsProfileReviewed,
    string HalfBodyUrl,
    bool IsHalfBodyReviewed);

/// <summary>
/// 相手ごとの名刺の顔写真と半身写真。名刺照会の応答・パーティ情報・自分宛てのソーシャルデータから控える。
///
/// <para>
/// 名刺照会はゲームが送ったものしか届かないので、相手によっては何も届かない。
/// <b>顔と名刺の入っていない応答では前の値を消さない</b>(照会の種類ごとに入る部分が違う)。
/// </para>
/// </summary>
public static class SocialDataStore
{
    private static readonly object StateLock = new();
    private static readonly Dictionary<long, SocialAvatarState> Avatars = [];

    /// <summary>その相手の値が変わった。引数はキャラID。受信のスレッドから上がる。</summary>
    public static event Action<long>? Changed;

    /// <summary>その相手の顔と名刺。まだ届いていなければ null。</summary>
    public static SocialAvatarState? GetAvatar(long characterId)
    {
        lock (StateLock)
        {
            return Avatars.TryGetValue(characterId, out var avatar) ? avatar : null;
        }
    }

    /// <param name="avatarInfo">無ければ前の値のまま。</param>
    public static void Apply(long characterId, AvatarInfo? avatarInfo)
    {
        if (characterId <= 0 || avatarInfo is null)
        {
            return;
        }

        var next = ToAvatarState(avatarInfo);
        bool changed;
        lock (StateLock)
        {
            changed = !Avatars.TryGetValue(characterId, out var existing) || existing != next;
            Avatars[characterId] = next;
        }

        if (changed)
        {
            Changed?.Invoke(characterId);
        }
    }

    /// <summary>
    /// 控えを全部消した(キャプチャの停止とログアウト)。表示側はこれを受けて、相手を問わず写真を引き直す。
    /// 停止かログアウトのスレッドから上がる。
    /// </summary>
    public static event Action? Cleared;

    /// <summary>起動時の値に戻す(キャプチャの停止とログアウト)。</summary>
    public static void ResetToStartup()
    {
        lock (StateLock)
        {
            Avatars.Clear();
        }

        Cleared?.Invoke();
    }

    private static SocialAvatarState ToAvatarState(AvatarInfo avatarInfo)
    {
        return new SocialAvatarState(
            avatarInfo.AvatarId,
            avatarInfo.Profile?.Url ?? string.Empty,
            IsReviewed(avatarInfo.Profile),
            avatarInfo.HalfBody?.Url ?? string.Empty,
            IsReviewed(avatarInfo.HalfBody));
    }

    private static bool IsReviewed(PictureInfo? picture)
    {
        return picture?.Verify?.ReviewStartTime == (uint)EPictureReviewType.EpictureReviewed;
    }
}
