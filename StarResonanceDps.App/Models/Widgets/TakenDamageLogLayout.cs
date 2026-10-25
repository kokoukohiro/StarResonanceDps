using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.App.Models.Widgets;

public enum TakenDamageLogRowKind
{
    Announcement,
    Cast,
    Skill,
    Hit,

    /// <summary>ダメージの無い戦闘不能の行(「システムの攻撃」の技の行の下)。数字は出さない。</summary>
    Death,

    /// <summary>予告のバーが終わった時刻の行。技の行と同じ書式で、下に被弾は並べない。</summary>
    AnnouncementEnd,

    /// <summary>敵どうしの回復の行(回復した側の技の行の下、回復された敵1体につき1行)。</summary>
    Heal,

    /// <summary>まとめの行。その時刻の後の HP。</summary>
    HealthResult,

    /// <summary>まとめの行。その時刻に戦闘不能になった(HP は 0、最大HP はその時点の値)。</summary>
    DeathResult
}

/// <summary>
/// 被ダメログの画面の1行ぶんの中身。
///
/// <para>
/// <see cref="Line"/> は、予告行・予告のバーの終わりの行・詠唱行・技の行・戦闘不能の行ならその記録、被弾行・回復の行なら畳んだもののうち最後のもの、
/// まとめの行ならその時刻のその人(その敵)の最後の記録(名前とクラスに使う)。
/// 被弾行・回復の行の <see cref="Value"/> は畳んだ値の合計、<see cref="IsLethal"/> は畳んだ被弾のどれかに致死の印があるか。
/// HP・最大HP・バリアはまとめの行だけが持つ(その時刻の後の値)。ほかの行は <c>null</c>。
/// <c>～Before</c> はまとめの行の、その時刻の最初の同期を当てる前の値。分からなければ <c>null</c>(矢印を出さない)。
/// <see cref="ShowsTime"/> はその時刻のジャンルごとの最初の行だけ真。
/// </para>
/// </summary>
public sealed record TakenDamageLogRow(
    TakenDamageLogRowKind Kind,
    TakenDamageLogLine Line,
    long Value,
    long? TargetHp,
    long? TargetMaxHp,
    long? TargetShield,
    bool IsLethal,
    bool ShowsTime,
    long? TargetHpBefore = null,
    long? TargetMaxHpBefore = null,
    long? TargetShieldBefore = null);

/// <summary>
/// 被ダメログの行の並べ方。同じ到着時刻の記録を1つの時刻として扱う。
///
/// <list type="bullet">
///   <item>
///     同じ時刻の中はジャンルの順に並べる: ①敵の行動(予告・予告のバーの終わり・詠唱)→ ②味方の被弾(戦闘不能を含む)とプレイヤーのまとめ
///     → ③敵どうしの回復と敵のまとめ。ジャンルの中は届いた順
///   </item>
///   <item>被弾は、技の行(同じ加害者・同じ発生源)ごとにまとめ、まとまりはその最初の被弾が届いた順に並べる。被弾の行に HP は出さない</item>
///   <item>ダメージの無い戦闘不能は、同じ時刻のものを1つの「システムの攻撃」の技の行の下に並べる(1人1行)。まとまりの位置は被弾と同じく届いた順</item>
///   <item>技の行の下で、同じ対象・同じ属性の被弾は1行に畳んで値を足す。畳んだ行は、その中で最後の被弾の位置に出す</item>
///   <item>属性を持たない被弾(属性を保存していなかった頃の記録)は、同じ属性か分からないので畳まない</item>
///   <item>
///     敵どうしの回復も被弾と同じく、回復した側・発生源ごとの技の行の下に並べる(被弾のまとまりとは別)。同じ敵への回復は1行に畳んで値を足す
///   </item>
///   <item>
///     ②と③の最後に、そのジャンルに出てきた人(敵)を出てきた順に1人1行並べる(まとめの行)。その時刻に戦闘不能になった人
///     (致死の印の被弾か、ダメージの無い戦闘不能)は戦闘不能、ほかはその時刻の後の HP。HP が分からず戦闘不能でもない人は出さない。
///     その時刻の最初の同期を当てる前の HP が分かれば、矢印の前に出す(前の値を持つ最初の記録から取る)
///   </item>
///   <item>
///     フィルターで隠す被弾は行を出さないが、まとめには入れる。まとめは、そのジャンルに見える被弾・回復か戦闘不能の行があるときだけ出す
///   </item>
///   <item>時刻を出すのは、その時刻にあるジャンルのそれぞれ最初の行だけ</item>
/// </list>
///
/// <para>
/// 記録は1件ずつ足されるので、同じ到着時刻の記録が取得の区切りをまたいで分かれて届くことがある。
/// 到着時刻が同じ間はその時刻の行を開いたままにして、後から届いた記録も同じジャンル・同じ技の行・同じ畳み・同じまとめへ入れ、並べ直す。
/// 違う到着時刻の記録が来たら閉じる。
/// </para>
/// </summary>
public sealed class TakenDamageLogLayout
{
    private readonly List<TakenDamageLogRow> _rows = [];
    private readonly List<OpenBlock> _openBlocks = [];
    private readonly List<TargetResult> _openResults = [];
    private int _openStart;
    private DateTime? _openTimestamp;

    /// <summary>開いている時刻に、見える被弾か戦闘不能の行があるか。②のまとめの行はこれが立ってから出す。</summary>
    private bool _openHasVisibleDamage;

    /// <summary>開いている時刻に、見える回復の行があるか。③のまとめの行はこれが立ってから出す。</summary>
    private bool _openHasVisibleHeal;

    public IReadOnlyList<TakenDamageLogRow> Rows => _rows;

    public void Clear()
    {
        _rows.Clear();
        _openBlocks.Clear();
        _openResults.Clear();
        _openStart = 0;
        _openTimestamp = null;
        _openHasVisibleDamage = false;
        _openHasVisibleHeal = false;
    }

    /// <summary>1件足す。表示設定で出さない敵どうしの回復は、呼び出し側が渡さない(まとめにも入れない)。</summary>
    /// <param name="isHidden">フィルターで出さない被弾。行は作らず、まとめにだけ入れる。被弾以外の記録には効かない(隠さない)。</param>
    /// <returns>中身が変わった行の先頭の位置。呼び出し側はそこから後ろを出し直す。変わった行が無ければ <see cref="Rows"/> の件数。</returns>
    public int Append(TakenDamageLogLine line, bool isHidden)
    {
        OpenTime(line.Timestamp);

        if (line.Kind is TakenDamageLogRecordKind.Announcement or TakenDamageLogRecordKind.AnnouncementEnd or TakenDamageLogRecordKind.Cast)
        {
            var kind = line.Kind switch
            {
                TakenDamageLogRecordKind.Announcement => TakenDamageLogRowKind.Announcement,
                TakenDamageLogRecordKind.AnnouncementEnd => TakenDamageLogRowKind.AnnouncementEnd,
                _ => TakenDamageLogRowKind.Cast
            };

            _openBlocks.Add(OpenBlock.ForSingle(LogGenre.Action, CreateHeadRow(kind, line)));
            return RebuildOpenRows();
        }

        var isHeal = line.Kind == TakenDamageLogRecordKind.EnemyHeal;
        var genre = isHeal ? LogGenre.Heal : LogGenre.Damage;
        var result = GetOrAddResult(line, genre);

        if (line.Kind == TakenDamageLogRecordKind.Death)
        {
            result.MarkDead(line);

            // 同じ到着時刻のダメージの無い戦闘不能は、1つの「システムの攻撃」の技の行の下に並べる。
            var deathGroup = _openBlocks.Find(block => block.IsDeathGroup);
            if (deathGroup is null)
            {
                deathGroup = OpenBlock.ForDeathGroup(CreateHeadRow(TakenDamageLogRowKind.Skill, line));
                _openBlocks.Add(deathGroup);
            }

            deathGroup.Folds.Add(new Fold(null, new TakenDamageLogRow(TakenDamageLogRowKind.Death, line, 0, null, null, null, false, false)));
            _openHasVisibleDamage = true;
            return RebuildOpenRows();
        }

        result.ApplyHit(line);
        if (isHidden)
        {
            return RebuildOpenRows();
        }

        var rowKind = isHeal ? TakenDamageLogRowKind.Heal : TakenDamageLogRowKind.Hit;
        var groupKey = new HitGroupKey(line.Attacker.Uuid, line.IsBuffSource, line.SourceId, line.Timestamp, isHeal);
        var group = _openBlocks.Find(block => block.GroupKey == groupKey);
        if (group is null)
        {
            group = OpenBlock.ForGroup(genre, groupKey, CreateHeadRow(TakenDamageLogRowKind.Skill, line));
            _openBlocks.Add(group);
        }

        // 回復は属性を持たないので、同じ敵への回復を畳む。
        FoldKey? foldKey = isHeal
            ? new FoldKey(line.Target!.Uuid, null)
            : line.DamageElement is { } element
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
            hitRow = new TakenDamageLogRow(
                rowKind,
                line,
                previous.Value + line.Value,
                null,
                null,
                null,
                previous.IsLethal || line.IsLethal,
                false);
        }
        else
        {
            hitRow = new TakenDamageLogRow(rowKind, line, line.Value, null, null, null, line.IsLethal, false);
        }

        group.Folds.Add(new Fold(foldKey, hitRow));
        if (isHeal)
        {
            _openHasVisibleHeal = true;
        }
        else
        {
            _openHasVisibleDamage = true;
        }

        return RebuildOpenRows();
    }

    private void OpenTime(DateTime timestamp)
    {
        if (_openTimestamp != timestamp)
        {
            Close();
            _openTimestamp = timestamp;
        }
    }

    private void Close()
    {
        _openBlocks.Clear();
        _openResults.Clear();
        _openHasVisibleDamage = false;
        _openHasVisibleHeal = false;
        _openStart = _rows.Count;
        _openTimestamp = null;
    }

    /// <summary>まとまりの先頭の行。時刻を出すかは並べ直すときに決める(<see cref="RebuildOpenRows"/>)。</summary>
    private static TakenDamageLogRow CreateHeadRow(TakenDamageLogRowKind kind, TakenDamageLogLine line)
    {
        return new TakenDamageLogRow(kind, line, 0, null, null, null, false, false);
    }

    /// <summary>そのジャンルの、その人(敵)のまとめ。②のプレイヤーと③の敵は別に持つ。</summary>
    private TargetResult GetOrAddResult(TakenDamageLogLine line, LogGenre genre)
    {
        var target = line.Target
            ?? throw new InvalidOperationException($"Taken damage log {line.Kind} record has no target (sequence {line.Sequence}).");
        var result = _openResults.Find(item => item.Uuid == target.Uuid && item.Genre == genre);
        if (result is null)
        {
            result = new TargetResult(target.Uuid, genre, line);
            _openResults.Add(result);
        }

        return result;
    }

    /// <summary>
    /// 開いている時刻の行を並べ直す。ジャンルの順に、まとまりの行と、そのジャンルのまとめの行を置く。
    /// 各ジャンルの最初の行だけ時刻を出す(先頭の行の印が変わったら、そのまとまりの先頭の行を作り直す)。開いている範囲の行数は減らない。
    /// </summary>
    private int RebuildOpenRows()
    {
        var index = _openStart;
        var firstChanged = -1;

        void Place(TakenDamageLogRow row)
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

        foreach (var genre in GenreOrder)
        {
            var isFirstInGenre = true;
            foreach (var block in _openBlocks)
            {
                if (block.Genre != genre)
                {
                    continue;
                }

                if (block.Head.ShowsTime != isFirstInGenre)
                {
                    block.Head = block.Head with { ShowsTime = isFirstInGenre };
                }

                isFirstInGenre = false;
                foreach (var row in block.EnumerateRows())
                {
                    Place(row);
                }
            }

            var showsResults = genre switch
            {
                LogGenre.Damage => _openHasVisibleDamage,
                LogGenre.Heal => _openHasVisibleHeal,
                _ => false
            };
            if (!showsResults)
            {
                continue;
            }

            foreach (var result in _openResults)
            {
                if (result.Genre == genre && result.GetRow() is { } row)
                {
                    Place(row);
                }
            }
        }

        if (index != _rows.Count)
        {
            throw new InvalidOperationException(
                $"Taken damage log open rows shrank (expected {_rows.Count}, rebuilt {index}).");
        }

        return firstChanged < 0 ? _rows.Count : firstChanged;
    }

    /// <summary>同じ時刻の中のジャンル。並べる順は <see cref="GenreOrder"/>。</summary>
    private enum LogGenre
    {
        /// <summary>①敵の行動(予告・予告のバーの終わり・詠唱)。</summary>
        Action,

        /// <summary>②味方の被弾(ダメージの無い戦闘不能を含む)とプレイヤーのまとめ。</summary>
        Damage,

        /// <summary>③敵どうしの回復と敵のまとめ。</summary>
        Heal
    }

    private static readonly LogGenre[] GenreOrder = [LogGenre.Action, LogGenre.Damage, LogGenre.Heal];

    /// <summary>技の行のまとまりの鍵。同じ出し手・同じ発生源でも、被弾と敵どうしの回復は別のまとまりにする。</summary>
    private readonly record struct HitGroupKey(long AttackerUuid, bool IsBuffSource, int SourceId, DateTime Timestamp, bool IsHeal);

    /// <summary>畳む鍵。被弾は対象と属性、回復は対象だけ(属性は null)。</summary>
    private readonly record struct FoldKey(long TargetUuid, EDamageProperty? Element);

    private sealed record Fold(FoldKey? Key, TakenDamageLogRow Row);

    /// <summary>
    /// 開いている到着時刻の行のまとまり。予告・予告のバーの終わり・詠唱の1行か、技の行とその下の被弾・回復か、
    /// 「システムの攻撃」の技の行とその下のダメージの無い戦闘不能。
    /// </summary>
    private sealed class OpenBlock
    {
        private OpenBlock(LogGenre genre, TakenDamageLogRow head, HitGroupKey? groupKey, bool isDeathGroup)
        {
            Genre = genre;
            Head = head;
            GroupKey = groupKey;
            IsDeathGroup = isDeathGroup;
        }

        public LogGenre Genre { get; }

        /// <summary>先頭の行。時刻を出すかが変わったときだけ、並べ直しが作り直して入れ替える。</summary>
        public TakenDamageLogRow Head { get; set; }

        public HitGroupKey? GroupKey { get; }

        /// <summary>ダメージの無い戦闘不能のまとまりか。同じ到着時刻に1つだけ作る。</summary>
        public bool IsDeathGroup { get; }

        public List<Fold> Folds { get; } = [];

        public static OpenBlock ForSingle(LogGenre genre, TakenDamageLogRow row)
        {
            return new OpenBlock(genre, row, null, isDeathGroup: false);
        }

        public static OpenBlock ForGroup(LogGenre genre, HitGroupKey key, TakenDamageLogRow skillRow)
        {
            return new OpenBlock(genre, skillRow, key, isDeathGroup: false);
        }

        public static OpenBlock ForDeathGroup(TakenDamageLogRow skillRow)
        {
            return new OpenBlock(LogGenre.Damage, skillRow, null, isDeathGroup: true);
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

    /// <summary>
    /// 開いている時刻の、そのジャンルの1人(1体)ぶんのまとめ。HP は記録が持つ同期の後の値で、同じ時刻なら最後に届いたものを使う。
    /// 同期を当てる前の HP は、前の値を持つ最初の記録から取る(同じ時刻に同期が2つ以上あっても、最初の同期の前)。
    /// 一度戦闘不能になったら、その時刻の中では戦闘不能のまま。行は中身が変わったときだけ作り直す(画面の作り直しを減らすため)。
    /// </summary>
    private sealed class TargetResult
    {
        private TakenDamageLogLine _line;
        private long? _hp;
        private long? _maxHp;
        private long? _shield;
        private bool _isDead;
        private long? _hpBefore;
        private long? _maxHpBefore;
        private long? _shieldBefore;
        private TakenDamageLogRow? _row;

        public TargetResult(long uuid, LogGenre genre, TakenDamageLogLine line)
        {
            Uuid = uuid;
            Genre = genre;
            _line = line;
        }

        public long Uuid { get; }

        /// <summary>まとめの行を置くジャンル(②のプレイヤーか③の敵)。</summary>
        public LogGenre Genre { get; }

        private void ApplyBefore(TakenDamageLogLine line)
        {
            if (_hpBefore is null && line.TargetHpBefore is { } hpBefore)
            {
                _hpBefore = hpBefore;
                _maxHpBefore = line.TargetMaxHpBefore;
                _shieldBefore = line.TargetShieldBefore;
                _row = null;
            }
        }

        public void ApplyHit(TakenDamageLogLine line)
        {
            _line = line;
            ApplyBefore(line);
            if (line.TargetHp is { } hp && (hp != _hp || line.TargetMaxHp != _maxHp || line.TargetShield != _shield))
            {
                _hp = hp;
                _maxHp = line.TargetMaxHp;
                _shield = line.TargetShield;
                _row = null;
            }

            if (line.IsLethal && !_isDead)
            {
                _isDead = true;
                _row = null;
            }
        }

        public void MarkDead(TakenDamageLogLine line)
        {
            _line = line;
            ApplyBefore(line);
            if (line.TargetMaxHp is { } maxHp && maxHp != _maxHp)
            {
                _maxHp = maxHp;
                _row = null;
            }

            if (!_isDead)
            {
                _isDead = true;
                _row = null;
            }
        }

        /// <summary>まとめの行。HP が分からず戦闘不能でもなければ出さない(null)。</summary>
        public TakenDamageLogRow? GetRow()
        {
            if (_row is not null)
            {
                return _row;
            }

            if (_isDead)
            {
                _row = new TakenDamageLogRow(
                    TakenDamageLogRowKind.DeathResult, _line, 0, 0, _maxHp, null, false, false, _hpBefore, _maxHpBefore, _shieldBefore);
            }
            else if (_hp is { } hp)
            {
                _row = new TakenDamageLogRow(
                    TakenDamageLogRowKind.HealthResult, _line, 0, hp, _maxHp, _shield, false, false, _hpBefore, _maxHpBefore, _shieldBefore);
            }

            return _row;
        }
    }
}
