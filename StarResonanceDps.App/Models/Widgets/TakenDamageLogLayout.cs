using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.App.Models.Widgets;

public enum TakenDamageLogRowKind
{
    Announcement,
    Cast,
    Skill,
    Hit,
    Death
}

/// <summary>
/// 被ダメログの画面の1行ぶんの中身。
///
/// <para>
/// <see cref="Line"/> は、予告行・詠唱行・技の行ならその記録、被弾行なら畳んだ被弾のうち最後のもの。
/// 被弾行の <see cref="Value"/> は畳んだ被弾の値の合計で、HP・最大HP・バリアは畳んだ被弾のうち HP を持つもの
/// (同期の中で最後の被弾)の値。どれも HP を持たなければ <c>null</c>。
/// </para>
/// </summary>
public sealed record TakenDamageLogRow(
    TakenDamageLogRowKind Kind,
    TakenDamageLogLine Line,
    long Value,
    long? TargetHp,
    long? TargetMaxHp,
    long? TargetShield);

/// <summary>
/// 被ダメログの行の並べ方。
///
/// <list type="bullet">
///   <item>被弾は、届いた瞬間ごとに技の行を出し、その下に同じ加害者・同じ発生源・同じ到着時刻の被弾を並べる</item>
///   <item>ダメージの無い死亡は、「システムの攻撃」の技の行とその下の死亡の行の2行。ほかの行と畳まない</item>
///   <item>技の行の下で、同じ対象・同じ属性の被弾は1行に畳んで値を足す。畳んだ行は、その中で最後の被弾の位置に出す</item>
///   <item>属性を持たない被弾(属性を保存していなかった頃の記録)は、同じ属性か分からないので畳まない</item>
/// </list>
///
/// <para>
/// 被弾は1件ずつ記録に足されるので、同じ到着時刻の被弾が取得の区切りをまたいで分かれて届くことがある。
/// 到着時刻が同じ間はその時刻の行を開いたままにして、後から届いた被弾も同じ技の行・同じ畳みへ入れる。
/// 違う到着時刻の被弾か、違う到着時刻の予告・詠唱が来たら閉じる。
/// </para>
/// </summary>
public sealed class TakenDamageLogLayout
{
    private readonly List<TakenDamageLogRow> _rows = [];
    private readonly List<OpenBlock> _openBlocks = [];
    private int _openStart;
    private DateTime? _openTimestamp;

    public IReadOnlyList<TakenDamageLogRow> Rows => _rows;

    public void Clear()
    {
        _rows.Clear();
        _openBlocks.Clear();
        _openStart = 0;
        _openTimestamp = null;
    }

    /// <summary>1件足す。</summary>
    /// <returns>中身が変わった行の先頭の位置。呼び出し側はそこから後ろを出し直す。変わった行が無ければ <see cref="Rows"/> の件数。</returns>
    public int Append(TakenDamageLogLine line)
    {
        if (line.Kind is TakenDamageLogRecordKind.Announcement or TakenDamageLogRecordKind.Cast)
        {
            var row = new TakenDamageLogRow(
                line.Kind == TakenDamageLogRecordKind.Announcement ? TakenDamageLogRowKind.Announcement : TakenDamageLogRowKind.Cast,
                line,
                0,
                null,
                null,
                null);

            // 開いている到着時刻と同じなら、その時刻の行の並びに入れる(後から届く同じ時刻の被弾は技の行の下へ差し込まれる)。
            if (_openTimestamp == line.Timestamp)
            {
                _openBlocks.Add(OpenBlock.ForSingle(row));
                return RebuildOpenRows();
            }

            Close();
            _rows.Add(row);
            _openStart = _rows.Count;
            return _rows.Count - 1;
        }

        if (line.Kind == TakenDamageLogRecordKind.Death)
        {
            var skillRow = new TakenDamageLogRow(TakenDamageLogRowKind.Skill, line, 0, null, null, null);
            var deathRow = new TakenDamageLogRow(TakenDamageLogRowKind.Death, line, 0, line.TargetHp, line.TargetMaxHp, null);

            // 予告・詠唱と同じく、開いている到着時刻と同じならその時刻の並びに入れる。
            if (_openTimestamp == line.Timestamp)
            {
                var block = OpenBlock.ForSingle(skillRow);
                block.Folds.Add(new Fold(null, deathRow));
                _openBlocks.Add(block);
                return RebuildOpenRows();
            }

            Close();
            _rows.Add(skillRow);
            _rows.Add(deathRow);
            _openStart = _rows.Count;
            return _rows.Count - 2;
        }

        if (_openTimestamp != line.Timestamp)
        {
            Close();
            _openTimestamp = line.Timestamp;
        }

        var groupKey = new HitGroupKey(line.Attacker.Uuid, line.IsBuffSource, line.SourceId, line.Timestamp);
        var group = _openBlocks.Find(block => block.GroupKey == groupKey);
        if (group is null)
        {
            group = OpenBlock.ForGroup(groupKey, new TakenDamageLogRow(TakenDamageLogRowKind.Skill, line, 0, null, null, null));
            _openBlocks.Add(group);
        }

        FoldKey? foldKey = line.DamageElement is { } element
            ? new FoldKey(line.Target!.Uuid, element)
            : null;
        var foldIndex = foldKey is null
            ? -1
            : group.Folds.FindIndex(fold => fold.Key == foldKey);

        TakenDamageLogRow hitRow;
        if (foldIndex >= 0)
        {
            var previous = group.Folds[foldIndex].Row;
            group.Folds.RemoveAt(foldIndex);
            var carriesHp = line.TargetHp is not null;
            hitRow = new TakenDamageLogRow(
                TakenDamageLogRowKind.Hit,
                line,
                previous.Value + line.Value,
                carriesHp ? line.TargetHp : previous.TargetHp,
                carriesHp ? line.TargetMaxHp : previous.TargetMaxHp,
                carriesHp ? line.TargetShield : previous.TargetShield);
        }
        else
        {
            hitRow = new TakenDamageLogRow(
                TakenDamageLogRowKind.Hit,
                line,
                line.Value,
                line.TargetHp,
                line.TargetMaxHp,
                line.TargetShield);
        }

        group.Folds.Add(new Fold(foldKey, hitRow));
        return RebuildOpenRows();
    }

    private void Close()
    {
        _openBlocks.Clear();
        _openStart = _rows.Count;
        _openTimestamp = null;
    }

    /// <summary>開いている到着時刻の行を並べ直す。開いている範囲の行数は減らない。</summary>
    private int RebuildOpenRows()
    {
        var index = _openStart;
        var firstChanged = -1;
        foreach (var block in _openBlocks)
        {
            foreach (var row in block.EnumerateRows())
            {
                if (index < _rows.Count)
                {
                    if (!ReferenceEquals(_rows[index], row))
                    {
                        _rows[index] = row;
                        firstChanged = firstChanged < 0 ? index : firstChanged;
                    }
                }
                else
                {
                    _rows.Add(row);
                    firstChanged = firstChanged < 0 ? index : firstChanged;
                }

                index++;
            }
        }

        if (index != _rows.Count)
        {
            throw new InvalidOperationException(
                $"Taken damage log open rows shrank (expected {_rows.Count}, rebuilt {index}).");
        }

        return firstChanged < 0 ? _rows.Count : firstChanged;
    }

    private readonly record struct HitGroupKey(long AttackerUuid, bool IsBuffSource, int SourceId, DateTime Timestamp);

    private readonly record struct FoldKey(long TargetUuid, EDamageProperty Element);

    private sealed record Fold(FoldKey? Key, TakenDamageLogRow Row);

    /// <summary>開いている到着時刻の行のまとまり。予告・詠唱の1行か、技の行とその下の被弾。</summary>
    private sealed class OpenBlock
    {
        private OpenBlock(TakenDamageLogRow head, HitGroupKey? groupKey)
        {
            Head = head;
            GroupKey = groupKey;
        }

        public TakenDamageLogRow Head { get; }

        public HitGroupKey? GroupKey { get; }

        public List<Fold> Folds { get; } = [];

        public static OpenBlock ForSingle(TakenDamageLogRow row)
        {
            return new OpenBlock(row, null);
        }

        public static OpenBlock ForGroup(HitGroupKey key, TakenDamageLogRow skillRow)
        {
            return new OpenBlock(skillRow, key);
        }

        public IEnumerable<TakenDamageLogRow> EnumerateRows()
        {
            yield return Head;
            foreach (var fold in Folds)
            {
                yield return fold.Row;
            }
        }
    }
}
