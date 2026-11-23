// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Serilog;
using Zproto;

namespace StarResonanceDps.Core.Services;

/// <summary>
/// パーティで置くマークの控え。マップが番号の絵(フィールドマーク)と、マークを付けられたプレイヤーの印に使う。
///
/// <para>
/// マークは通知 <c>NotifySceneMark</c> の <see cref="SceneMarkSync"/> で届き、中身はその時に置かれているマークの全部
/// (外すと残りだけ、全部外すと空)。届くたびに丸ごと置き換える。
/// </para>
///
/// <list type="bullet">
/// <item>フィールドマーク: 番号(0〜9)ごとに1つの位置。届く種類は表 <c>SceneMarkType</c> の番号(1〜10)で、番号は種類 − 1</item>
/// <item>プレイヤーのマーク: キャラIDごとに1つの種類(1〜6)</item>
/// </list>
///
/// <para>
/// マップ移動・キャプチャの停止・ログアウトで全部消す。持つのはフィールドマーク 10 個とパーティの人数まで。
/// 知らせは出さない。マップが自分の更新のときに読み、<see cref="Version"/> で変わったかを見る。
/// </para>
/// </summary>
public sealed class FieldMarkStore
{
    private const int FirstMarkType = 1;
    private const int LastMarkType = 10;
    private const int FirstTeammateMarkType = 1;
    private const int LastTeammateMarkType = 6;

    private static readonly Lazy<FieldMarkStore> LazyInstance = new(() => new FieldMarkStore());

    private readonly object _sync = new();
    private readonly Dictionary<int, FieldMark> _fieldMarks = [];
    private readonly Dictionary<long, int> _teammateMarks = [];
    private readonly HashSet<int> _warnedMarkTypes = [];
    private readonly HashSet<int> _warnedTeammateMarkTypes = [];
    private int _version;

    private FieldMarkStore()
    {
    }

    public static FieldMarkStore Instance => LazyInstance.Value;

    /// <param name="Number">マークの番号(0〜9)。</param>
    /// <param name="Position">置いた位置。</param>
    public readonly record struct FieldMark(int Number, EntityPositionStore.WorldPosition Position);

    /// <summary>中身が変わるたびに増える番号。</summary>
    public int Version
    {
        get
        {
            lock (_sync)
            {
                return _version;
            }
        }
    }

    /// <summary>届いたマークで丸ごと置き換える。中身が無ければ全部消す。</summary>
    public void Apply(SceneMarkSync? sync)
    {
        lock (_sync)
        {
            _fieldMarks.Clear();
            _teammateMarks.Clear();
            if (sync is not null)
            {
                foreach (var mark in sync.SceneMarks)
                {
                    if (mark.MarkType is < FirstMarkType or > LastMarkType)
                    {
                        if (_warnedMarkTypes.Add(mark.MarkType))
                        {
                            Log.Warning("Unknown scene mark type {MarkType} was ignored", mark.MarkType);
                        }

                        continue;
                    }

                    if (mark.Position is not { } position)
                    {
                        continue;
                    }

                    var number = mark.MarkType - FirstMarkType;
                    _fieldMarks[number] = new FieldMark(number, new EntityPositionStore.WorldPosition(position.X, position.Y, position.Z));
                }

                foreach (var mark in sync.TeammateMarks)
                {
                    if (mark.MarkType is < FirstTeammateMarkType or > LastTeammateMarkType)
                    {
                        if (_warnedTeammateMarkTypes.Add(mark.MarkType))
                        {
                            Log.Warning("Unknown teammate mark type {MarkType} was ignored", mark.MarkType);
                        }

                        continue;
                    }

                    _teammateMarks[mark.CharId] = mark.MarkType;
                }
            }

            _version++;
        }
    }

    /// <summary>全部消す。マップ移動・キャプチャの停止・ログアウトで呼ぶ。</summary>
    public void Clear()
    {
        lock (_sync)
        {
            if (_fieldMarks.Count == 0 && _teammateMarks.Count == 0)
            {
                return;
            }

            _fieldMarks.Clear();
            _teammateMarks.Clear();
            _version++;
        }
    }

    /// <summary>今のフィールドマークを <paramref name="target"/> に写す(中身は入れ替える)。呼び手の一覧を使い回し、読むたびに新しく作らない。</summary>
    public void CopyFieldMarksTo(List<FieldMark> target)
    {
        target.Clear();
        lock (_sync)
        {
            foreach (var mark in _fieldMarks.Values)
            {
                target.Add(mark);
            }
        }
    }

    /// <summary>
    /// 今のプレイヤーのマーク(キャラID → 種類 1〜6)を <paramref name="target"/> に写す(中身は入れ替える)。写した中身の <see cref="Version"/> を返す。
    /// </summary>
    public int CopyTeammateMarksTo(Dictionary<long, int> target)
    {
        target.Clear();
        lock (_sync)
        {
            foreach (var (characterId, markType) in _teammateMarks)
            {
                target[characterId] = markType;
            }

            return _version;
        }
    }
}
