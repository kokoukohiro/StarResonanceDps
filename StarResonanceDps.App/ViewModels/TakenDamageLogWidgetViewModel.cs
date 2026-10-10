using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Diagnostics;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 画面に出す被ダメログの1行。時刻を見せるのは、その時刻の最初の行だけ(<see cref="IsTimeShown"/>、決めるのは <see cref="TakenDamageLogLayout"/>)。
/// <see cref="Segments"/> は文字列とアイコン(<see cref="TakenDamageLogClassIconSegment"/> / <see cref="TakenDamageLogAttributeIconSegment"/>)の並び。
/// </summary>
/// <param name="TimeText">
/// どの行も自分の経過時刻(戦闘の時計の起点から。起点の前は負)を持つ。時刻を見せない行は場所だけ取って描かない。時刻の列の幅は画面に作られている行だけで揃うので、
/// 空にすると、時刻を見せる行が画面に無いとき列が縮んで本文が左へずれる。空にするのは起点がまだ無い間だけ(立ったら全部の行を作り直す)。
/// </param>
/// <param name="Widget">文字の TIPS の見た目が引く窓のパレットの持ち主。行の TextBlock の Tag に載る。</param>
public sealed record TakenDamageLogEntry(
    string TimeText,
    bool IsTimeShown,
    IReadOnlyList<object> Segments,
    WidgetListItemViewModel Widget);

/// <summary>被弾行の対象の前に出すクラスアイコン。色はプレイヤーリストのクラスカラー、TIPS は特化名。</summary>
/// <param name="IconMask">職業アイコンの形。クラス色の塗りをこの形で抜く。</param>
/// <param name="Widget">TIPS の色を取る窓のパレットの持ち主。</param>
public sealed record TakenDamageLogClassIconSegment(Brush IconMask, Brush ClassBrush, string SpecName, WidgetListItemViewModel Widget);

/// <summary>被弾行の値の後ろに出す属性アイコン。TIPS は属性名。</summary>
/// <param name="IconMask">属性アイコンの形。テキストカラーの塗りをこの形で抜く(クラスアイコンと同じ染め方)。</param>
/// <param name="ElementBrush">その属性のテキストカラー。設定を変えるとアイコンの色も変わる。</param>
/// <param name="Widget">TIPS の色を取る窓のパレットの持ち主。</param>
public sealed record TakenDamageLogAttributeIconSegment(Brush IconMask, Brush ElementBrush, string Name, WidgetListItemViewModel Widget);

/// <summary>
/// 被ダメログ。表示中のエンカウンターで、ボス大技の予告、プレイヤー以外の詠唱と、プレイヤーがプレイヤー以外から受けたダメージを出す。
///
/// <code>
/// 00:22:01  [敵]が[技]を構えている
/// 00:22:03  [敵]が[技]を唱えている
/// 00:22:05  [敵]の[技]
///           [クラス][プレイヤー]に12345[属性]ダメージ
///           [クラス][プレイヤー]に23456[属性]ダメージで戦闘不能
///           [クラス][プレイヤー]のHP(12468/123456) → HP(123/123456)
///           [クラス][プレイヤー]のHP(23456/234567) → 戦闘不能(0/234567)
/// </code>
///
/// <para>
/// <b>被弾は発動単位で束ねない。</b> 届いた瞬間ごとに技の行を1行出し、その下に
/// 同じ瞬間(同じ加害者・同じ発生源・同じ到着時刻)の被弾だけを並べる。
/// 同じ技の次の被弾は、また技の行から出す。
/// 技の行の下で同じ対象・同じ属性の被弾は1行に畳む。HP は被弾の行に出さず、その時刻の最後のまとめの行に1人1行で出す
/// (矢印の前はその時刻の最初の同期を当てる前、後は当てた後。並べ方は <see cref="TakenDamageLogLayout"/>)。
/// </para>
///
/// <para>
/// クラスアイコンの色は被ダメログ自身のクラスカラーの設定を使う(形と既定の色はプレイヤーリストと同じ)。
/// </para>
///
/// <para>
/// <b>履歴表示の対象。</b> エンカウンターの決め方はメーターと同じで、履歴を開けばその戦闘のログになる。
/// ライブ中は <see cref="RefreshInterval"/> ごとに増えた分だけ追記する。
/// </para>
///
/// <para>
/// 取得した行(<see cref="_lines"/>)は全部持ち、表示の設定は画面に出すときに当てる。
/// 設定を変えれば、持っている行から組み直す。
/// </para>
/// </summary>
public sealed class TakenDamageLogWidgetViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    // 書式の中の対象名({0})と属性アイコン({2})の位置の印。整形した文字列をここで区切ってアイコンを差し込む。
    private const string TargetNameMarker = "\x01";
    private const string AttributeIconMarker = "\x02";
    private const string AttackerNameMarker = "\x03";
    private const string SourceNameMarker = "\x04";
    private const string DamagePartMarker = "\x05";
    private const string HealthPartMarker = "\x06";
    private const string MaxHealthMarker = "\x07";
    private const string AfterHealthMarker = "\x08";

    private readonly WidgetListItemViewModel _widget;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<TakenDamageLogLine> _lines = [];
    private readonly ObservableCollection<TakenDamageLogEntry> _entries = [];

    // 画面の行の並び(技の行・畳み)と、_entries の各行を作った元の行。元の行が変わった位置だけ作り直す。
    private readonly TakenDamageLogLayout _layout = new();
    private readonly List<TakenDamageLogRow> _entryRows = [];
    private readonly Dictionary<string, SolidColorBrush> _classBrushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageBrush> _professionIconMasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageBrush> _elementIconMasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SolidColorBrush> _textBrushes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>通知(予兆技)の判定。窓の表示(履歴を含む)とは別に、ライブの戦闘を読む。窓を開いている間だけ動く。</summary>
    private readonly TelegraphedSkillNotificationTracker _notificationTracker;

    private Encounter? _encounter;
    private int _nextIndex;

    // 行の時刻の起点(戦闘の時計の起点)。同じエンカウンターでも後から立つ。
    private DateTime? _combatStartUtc;
    private TakenDamageLogWidgetSettingsConfig _settings;

    public TakenDamageLogWidgetViewModel(WidgetListItemViewModel widget)
    {
        _widget = widget;
        _settings = widget.GetTakenDamageLogSettingsSnapshot();
        _notificationTracker = new TelegraphedSkillNotificationTracker(widget.GetTakenDamageLogSettingsSnapshot, GetDisplayName);
        Entries = new ReadOnlyObservableCollection<TakenDamageLogEntry>(_entries);

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.TakenDamageLogSettingsChanged += Widget_TakenDamageLogSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        HistorySwitchProbe.Register(this, widget.Kind.ToString());

        Refresh();
        _notificationTracker.Poll();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<TakenDamageLogEntry> Entries { get; }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _widget.TakenDamageLogSettingsChanged -= Widget_TakenDamageLogSettingsChanged;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        HistorySwitchProbe.Unregister(this);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
        _notificationTracker.Poll();
    }

    /// <summary>
    /// 表示設定(保存かプレビュー)が変わった。フィルターが変われば並ぶ行が変わるので、持っている行から組み直す。
    /// HP 数値の出し方とクラスカラーは行の中身だけなので、並びはそのままで各行を作り直す
    /// (色見本を触るたびに全行を並べ直さない)。
    /// </summary>
    private void Widget_TakenDamageLogSettingsChanged(object? sender, EventArgs e)
    {
        var previous = _settings;
        _settings = _widget.GetTakenDamageLogSettingsSnapshot();
        _classBrushes.Clear();
        _textBrushes.Clear();

        if (_settings.AttackerFilterIndex != previous.AttackerFilterIndex)
        {
            ClearEntries();
            foreach (var line in _lines)
            {
                AppendEntries(line);
            }

            return;
        }

        for (var index = 0; index < _entryRows.Count; index++)
        {
            _entries[index] = CreateEntry(_entryRows[index]);
        }
    }

    /// <summary>
    /// 内部IDの表示と名前の伏せ字は、行を取得した時点の文字列に焼き込まれている。
    /// 言語切替と同じく、先頭から読み直す。
    /// </summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        Reload();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Reload();
    }

    private void Reload()
    {
        _encounter = null;
        _nextIndex = 0;
        _combatStartUtc = null;
        _lines.Clear();
        ClearEntries();
        Refresh();
    }

    private void Refresh()
    {
        var probe = HistorySwitchProbe.BeginRefresh(this);
        var snapshot = MeterSnapshotProvider.GetTakenDamageLog(_encounter, _nextIndex);
        probe?.DataDone();
        var encounterChanged = !ReferenceEquals(snapshot.Encounter, _encounter);

        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            _encounter = snapshot.Encounter;
            _combatStartUtc = snapshot.CombatStartUtc;
            _lines.Clear();
            ClearEntries();
        }
        else if (snapshot.CombatStartUtc != _combatStartUtc)
        {
            // 起点が立った。並びは変わらないので、持っている行の時刻だけ作り直す。
            _combatStartUtc = snapshot.CombatStartUtc;
            for (var index = 0; index < _entryRows.Count; index++)
            {
                _entries[index] = CreateEntry(_entryRows[index]);
            }
        }

        _nextIndex = snapshot.NextIndex;
        _lines.AddRange(snapshot.Lines);
        foreach (var line in snapshot.Lines)
        {
            AppendEntries(line);
        }

        probe?.End($"lines=+{snapshot.Lines.Count} entries={_entries.Count} encounterChanged={encounterChanged}");
    }

    private void ClearEntries()
    {
        _entries.Clear();
        _entryRows.Clear();
        _layout.Clear();
    }

    private void AppendEntries(TakenDamageLogLine line)
    {
        var rows = _layout.Rows;
        for (var index = _layout.Append(line, IsHiddenByFilter(line)); index < rows.Count; index++)
        {
            var row = rows[index];
            if (index < _entryRows.Count)
            {
                if (!ReferenceEquals(_entryRows[index], row))
                {
                    _entryRows[index] = row;
                    _entries[index] = CreateEntry(row);
                }

                continue;
            }

            _entryRows.Add(row);
            _entries.Add(CreateEntry(row));
        }
    }

    private TakenDamageLogEntry CreateEntry(TakenDamageLogRow row)
    {
        var line = row.Line;

        switch (row.Kind)
        {
            case TakenDamageLogRowKind.Announcement:
            case TakenDamageLogRowKind.Cast:
                var format = row.Kind == TakenDamageLogRowKind.Announcement
                    ? "TakenDamageLog_AnnounceFormat"
                    : "TakenDamageLog_CastFormat";
                return new TakenDamageLogEntry(
                    FormatTime(line),
                    row.ShowsTime,
                    CreateSourceSegments(format, line, "TakenDamageLog_UnnamedCastSkill"),
                    _widget);
            case TakenDamageLogRowKind.Skill:
            case TakenDamageLogRowKind.AnnouncementEnd:
                return new TakenDamageLogEntry(
                    FormatTime(line),
                    row.ShowsTime,
                    CreateSourceSegments("TakenDamageLog_SkillFormat", line, "TakenDamageLog_UnnamedSkill"),
                    _widget);
            case TakenDamageLogRowKind.Hit:
                return new TakenDamageLogEntry(
                    FormatTime(line), row.ShowsTime, CreateHitSegments(row), _widget);
            case TakenDamageLogRowKind.Death:
                return new TakenDamageLogEntry(
                    FormatTime(line), row.ShowsTime, CreateDefeatedSegments(row), _widget);
            case TakenDamageLogRowKind.HealthResult:
                return new TakenDamageLogEntry(
                    FormatTime(line), row.ShowsTime, CreateHealthResultSegments(row), _widget);
            case TakenDamageLogRowKind.DeathResult:
                return new TakenDamageLogEntry(
                    FormatTime(line), row.ShowsTime, CreateDeathResultSegments(row), _widget);
            default:
                throw new ArgumentOutOfRangeException(nameof(row), row.Kind, null);
        }
    }

    /// <summary>
    /// 行の時刻。戦闘の経過(<see cref="Encounter.ToCombatOffset"/>、メーターの経過と同じで自動一時停止で止まっていた区間を除く)で、
    /// 起点の前は負。起点がまだ無い間は空欄。
    /// </summary>
    private string FormatTime(TakenDamageLogLine line)
    {
        return _combatStartUtc is not null && _encounter?.ToCombatOffset(line.Timestamp) is { } offset
            ? MeterWidgetViewModel.FormatDuration(offset)
            : string.Empty;
    }

    /// <summary>
    /// 技(またはバフ)の名前。名前が無ければこのウィジェットだけ <paramref name="unnamedKey"/> の文言を出す。
    /// 技の行と予告のバーの終わりの行は「攻撃」、予告行と詠唱行は「を構えている」「を唱えている」に続くので「何か」。
    /// 落下は技を持たないので「落下」の文言。
    /// </summary>
    private static string FormatSourceName(TakenDamageLogLine line, string unnamedKey)
    {
        if (line.IsFall)
        {
            return LocalizationManager.Instance.GetString("TakenDamageLog_FallSkill");
        }

        var name = string.IsNullOrEmpty(line.SourceName)
            ? LocalizationManager.Instance.GetString(unnamedKey)
            : line.SourceName;
        return line.IsBuffSource
            ? CombatDataCatalog.AppendBuffInternalId(name, line.SourceId)
            : CombatDataCatalog.AppendSkillInternalId(name, line.SourceId);
    }

    /// <summary>
    /// 予告行・予告のバーの終わりの行・詠唱行・技の行。加害者の名前(エンティティ名)と技の名前(スキル名)に、それぞれのテキストカラーを当てる。
    /// 加害者がプレイヤー(自傷・フレンドリーファイア)なら、被弾行と同じクラスアイコンを名前の前に置く。
    /// </summary>
    private IReadOnlyList<object> CreateSourceSegments(string formatKey, TakenDamageLogLine line, string unnamedKey)
    {
        var text = LocalizationManager.Instance.Format(formatKey, AttackerNameMarker, SourceNameMarker);
        EnsureMarkers(text, formatKey, AttackerNameMarker, SourceNameMarker);

        IReadOnlyList<object> attacker = line.Attacker.IsPlayer
            ? [CreateClassIconSegment(line.Attacker), ColoredText(GetDisplayName(line.Attacker), "EntityName")]
            : [ColoredText(GetDisplayName(line.Attacker), "EntityName")];

        return SplitByMarkers(text, new Dictionary<char, IReadOnlyList<object>>
        {
            [AttackerNameMarker[0]] = attacker,
            [SourceNameMarker[0]] = [ColoredText(FormatSourceName(line, unnamedKey), "SkillName")]
        });
    }

    /// <summary>
    /// 被弾行。書式の対象名({0})の前にクラスアイコン、ダメージ部({1})を属性の色で差し込む。HP はまとめの行に出すので、ここには出さない。
    /// 致死の印の被弾は、後ろに戦闘不能の語({2}、戦闘不能の色)を付ける。
    /// 属性を持たない被弾(属性を保存していなかった頃の記録)は属性アイコンを出さない。
    /// </summary>
    private IReadOnlyList<object> CreateHitSegments(TakenDamageLogRow row)
    {
        var localization = LocalizationManager.Instance;
        var target = row.Line.Target!;

        var replacements = new Dictionary<char, IReadOnlyList<object>>
        {
            [TargetNameMarker[0]] = [CreateClassIconSegment(target), ColoredText(GetDisplayName(target), "PlayerName")],
            [DamagePartMarker[0]] = CreateDamageSegments(row)
        };

        string text;
        if (row.IsLethal)
        {
            text = localization.Format("TakenDamageLog_LethalHitFormat", TargetNameMarker, DamagePartMarker, HealthPartMarker);
            EnsureMarkers(text, "TakenDamageLog_LethalHitFormat", TargetNameMarker, DamagePartMarker, HealthPartMarker);
            replacements[HealthPartMarker[0]] = [ColoredText(localization.GetString("TakenDamageLog_LethalText"), "Death")];
        }
        else
        {
            text = localization.Format("TakenDamageLog_HitFormatNoHp", TargetNameMarker, DamagePartMarker);
            EnsureMarkers(text, "TakenDamageLog_HitFormatNoHp", TargetNameMarker, DamagePartMarker);
        }

        return SplitByMarkers(text, replacements);
    }

    /// <summary>
    /// ダメージ部(値＋属性アイコン＋「物理ダメージ」などの語)。ひとまとまりで属性の色にし、
    /// <b>どの要素にも同じ TIPS</b>(属性名)を出す。
    /// 属性を持たない被弾(属性を保存していなかった頃の記録)はアイコンを出さず、色は無属性のもので TIPS も付けない。
    ///
    /// <para>
    /// 物理か魔法かは語を差し込まず<b>書式ごと分ける</b>。差し込みだと、種類の無い被弾のときに
    /// 言語によっては空白が二重に残る。
    /// </para>
    /// </summary>
    private IReadOnlyList<object> CreateDamageSegments(TakenDamageLogRow row)
    {
        var localization = LocalizationManager.Instance;
        var element = row.Line.DamageElement;
        var colorKey = element?.ToString() ?? "General";

        var formatKey = row.Line.DamageMode switch
        {
            Zproto.EDamageMode.DamagePhysical => "TakenDamageLog_HitDamagePhysical",
            Zproto.EDamageMode.DamageMagical => "TakenDamageLog_HitDamageMagical",
            _ => "TakenDamageLog_HitDamage"
        };

        var text = localization.Format(
            formatKey,
            row.Value.ToString(CultureInfo.InvariantCulture),
            AttributeIconMarker);
        EnsureMarkers(text, formatKey, AttributeIconMarker);

        IReadOnlyList<object> icon = [];
        string? attributeName = null;
        if (element is { } value)
        {
            attributeName = localization.GetString($"DamageProperty_{value}");
            icon =
            [
                new TakenDamageLogAttributeIconSegment(
                    GetElementIconMask(colorKey),
                    GetTextBrush(colorKey),
                    attributeName,
                    _widget)
            ];
        }

        return SplitByMarkers(
            text,
            new Dictionary<char, IReadOnlyList<object>> { [AttributeIconMarker[0]] = icon },
            GetTextBrush(colorKey),
            attributeName);
    }

    /// <summary>
    /// HP部。外側の括弧まで HP 数値の色にし、バリア量の括弧だけバリア量の色にする。
    /// </summary>
    private IReadOnlyList<object> CreateHealthSegments(long hp, long shield, long maxHp)
    {
        var text = LocalizationManager.Instance.Format("TakenDamageLog_HitHealth", TargetNameMarker, MaxHealthMarker);
        EnsureMarkers(text, "TakenDamageLog_HitHealth", TargetNameMarker, MaxHealthMarker);

        return SplitByMarkers(
            text,
            new Dictionary<char, IReadOnlyList<object>>
            {
                [TargetNameMarker[0]] = CreateCurrentHealthSegments(hp, shield),
                [MaxHealthMarker[0]] = [ColoredText(maxHp.ToString(CultureInfo.InvariantCulture), "HpValue")]
            },
            GetTextBrush("HpValue"));
    }

    /// <summary>
    /// 現在HP。バリア量を分けて出す設定のときだけ、括弧ごとバリア量の色にする
    /// (足して出す設定と、バリアが無いときは HP 数値だけ)。
    /// </summary>
    private IReadOnlyList<object> CreateCurrentHealthSegments(long hp, long shield)
    {
        var currentShield = Math.Max(shield, 0L);
        if (_settings.HealthValueDisplayModeIndex == WidgetConfigDefaults.SeparateShieldHealthValueDisplayModeIndex
            && currentShield > 0)
        {
            return
            [
                ColoredText(hp.ToString(CultureInfo.InvariantCulture), "HpValue"),
                ColoredText(string.Create(CultureInfo.InvariantCulture, $"({currentShield})"), "ShieldValue")
            ];
        }

        return [ColoredText(FormatHealthValue(hp, shield), "HpValue")];
    }

    /// <summary>
    /// ダメージの無い戦闘不能の行(「システムの攻撃」の技の行の下)。書式の対象名({0})の前にクラスアイコンを差し込み、
    /// 戦闘不能の語だけを戦闘不能の色で出す。数字はまとめの行に出す。
    /// </summary>
    private IReadOnlyList<object> CreateDefeatedSegments(TakenDamageLogRow row)
    {
        return CreateTargetSegments(
            "TakenDamageLog_DeathFormat",
            row,
            [ColoredText(LocalizationManager.Instance.GetString("TakenDamageLog_LethalText"), "Death")]);
    }

    /// <summary>
    /// まとめの行(戦闘不能になった人)。「戦闘不能」の語と HP をまとめて戦闘不能の色にする。
    /// 最大HP が分からなければ 0 と出す(プレイヤーリストと同じ)。その時刻の前の HP が分かれば「XのHP(…) → 戦闘不能(…)」、分からなければ「Xが戦闘不能(…)」。
    /// </summary>
    private IReadOnlyList<object> CreateDeathResultSegments(TakenDamageLogRow row)
    {
        var deathText = LocalizationManager.Instance.Format(
            "TakenDamageLog_DeathText",
            FormatHealthValue(row.TargetHp ?? 0L, 0L),
            (row.TargetMaxHp ?? 0L).ToString(CultureInfo.InvariantCulture));
        IReadOnlyList<object> after = [ColoredText(deathText, "Death")];
        return row.TargetHpBefore is null
            ? CreateTargetSegments("TakenDamageLog_DeathFormat", row, after)
            : CreateHealthChangeSegments(row, after);
    }

    /// <summary>
    /// まとめの行(その時刻の後の HP)。HP 部は今の HP の出し方。
    /// その時刻の前の HP が分かれば「XのHP(…) → HP(…)」、分からなければ「XのHP(…)」。
    /// </summary>
    private IReadOnlyList<object> CreateHealthResultSegments(TakenDamageLogRow row)
    {
        var after = CreateHealthSegments(row.TargetHp ?? 0L, row.TargetShield ?? 0L, row.TargetMaxHp ?? 0L);
        return row.TargetHpBefore is null
            ? CreateTargetSegments("TakenDamageLog_HealthSummaryFormat", row, after)
            : CreateHealthChangeSegments(row, after);
    }

    /// <summary>書式の対象名({0})の前にクラスアイコンを置き、{1} に <paramref name="part"/> を差し込む。</summary>
    private IReadOnlyList<object> CreateTargetSegments(string formatKey, TakenDamageLogRow row, IReadOnlyList<object> part)
    {
        var target = row.Line.Target!;
        var text = LocalizationManager.Instance.Format(formatKey, TargetNameMarker, HealthPartMarker);
        EnsureMarkers(text, formatKey, TargetNameMarker, HealthPartMarker);

        return SplitByMarkers(text, new Dictionary<char, IReadOnlyList<object>>
        {
            [TargetNameMarker[0]] = [CreateClassIconSegment(target), ColoredText(GetDisplayName(target), "PlayerName")],
            [HealthPartMarker[0]] = part
        });
    }

    /// <summary>
    /// まとめの行の矢印の形。{1} にその時刻の最初の同期を当てる前の HP、{2} に後(HP か戦闘不能)を差し込む。
    /// 前の HP も今の HP の出し方(バリア量・最大HP はその時点の値。最大HP が分からなければ 0)。
    /// </summary>
    private IReadOnlyList<object> CreateHealthChangeSegments(TakenDamageLogRow row, IReadOnlyList<object> after)
    {
        var target = row.Line.Target!;
        var text = LocalizationManager.Instance.Format("TakenDamageLog_HealthChangeFormat", TargetNameMarker, HealthPartMarker, AfterHealthMarker);
        EnsureMarkers(text, "TakenDamageLog_HealthChangeFormat", TargetNameMarker, HealthPartMarker, AfterHealthMarker);

        return SplitByMarkers(text, new Dictionary<char, IReadOnlyList<object>>
        {
            [TargetNameMarker[0]] = [CreateClassIconSegment(target), ColoredText(GetDisplayName(target), "PlayerName")],
            [HealthPartMarker[0]] = CreateHealthSegments(row.TargetHpBefore!.Value, row.TargetShieldBefore ?? 0L, row.TargetMaxHpBefore ?? 0L),
            [AfterHealthMarker[0]] = after
        });
    }

    /// <summary>
    /// マーカーを埋めた文字列を、マーカーの位置で切って segment の並びにする。
    /// <paramref name="textBrush"/> を渡すと、マーカー以外の文字もその色になる(渡さなければ行の既定の色)。
    /// <paramref name="toolTipText"/> を渡すと、その文字にも TIPS が出る。
    /// </summary>
    private static IReadOnlyList<object> SplitByMarkers(
        string text,
        IReadOnlyDictionary<char, IReadOnlyList<object>> replacements,
        Brush? textBrush = null,
        string? toolTipText = null)
    {
        var segments = new List<object>();
        var start = 0;

        void AddText(int end)
        {
            if (end <= start)
            {
                return;
            }

            var part = text[start..end];
            segments.Add(textBrush is null ? part : new TextRunSegment(part, textBrush, toolTipText));
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (!replacements.TryGetValue(text[index], out var replacement))
            {
                continue;
            }

            AddText(index);
            segments.AddRange(replacement);
            start = index + 1;
        }

        AddText(text.Length);
        return segments;
    }

    /// <summary>
    /// 書式にプレースホルダが残っているかを見る。訳を差し替えたときに静かに消えるのを防ぐ。
    /// </summary>
    private static void EnsureMarkers(string text, string formatKey, params string[] markers)
    {
        foreach (var marker in markers)
        {
            if (!text.Contains(marker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{formatKey} must contain every placeholder.");
            }
        }
    }

    private TextRunSegment ColoredText(string text, string colorKey)
    {
        return new TextRunSegment(text, GetTextBrush(colorKey));
    }

    /// <summary>テキストカラーの1色。設定が変われば <see cref="_textBrushes"/> ごと捨てて引き直す。</summary>
    private Brush GetTextBrush(string colorKey)
    {
        if (!_textBrushes.TryGetValue(colorKey, out var brush))
        {
            brush = new SolidColorBrush(GetTextColor(colorKey));
            brush.Freeze();
            _textBrushes[colorKey] = brush;
        }

        return brush;
    }

    /// <summary>テキストカラーの設定で選んでいる色(引き方はクラスカラーと同じ)。</summary>
    private Color GetTextColor(string colorKey)
    {
        var palette = _settings.TextColorPalettes.TryGetValue(colorKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultTextColors(colorKey);
        var selectedIndex = _settings.TextColorIndexes.TryGetValue(colorKey, out var index)
            ? index
            : WidgetConfigDefaults.GetDefaultTextColorIndex(colorKey);
        var selectedColor = palette.Count == 0
            ? "#FFFFFF"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        return ColorUtilities.TryParseHex(selectedColor, out var color)
            ? color
            : Colors.White;
    }

    /// <summary>属性アイコンの形。塗りをこの形で抜く(クラスアイコンと同じ染め方)。</summary>
    private ImageBrush GetElementIconMask(string elementKey)
    {
        if (!_elementIconMasks.TryGetValue(elementKey, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.DamageProperty.{elementKey}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _elementIconMasks[elementKey] = mask;
        }

        return mask;
    }

    private TakenDamageLogClassIconSegment CreateClassIconSegment(TakenDamageLogParty target)
    {
        var professionKey = PlayerProfession.GetKey(target.ProfessionId, target.ClassSpec);
        if (!_classBrushes.TryGetValue(professionKey, out var brush))
        {
            brush = new SolidColorBrush(GetClassColor(professionKey));
            brush.Freeze();
            _classBrushes[professionKey] = brush;
        }

        if (!_professionIconMasks.TryGetValue(professionKey, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.Profession.{professionKey}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _professionIconMasks[professionKey] = mask;
        }

        return new TakenDamageLogClassIconSegment(
            mask,
            brush,
            PlayerInfoFormatFormatter.GetClassSpecText(target.ClassSpec),
            _widget);
    }

    /// <summary>クラスアイコンの色。被ダメログのクラスカラーの設定で選んでいる色(引き方はプレイヤーリストと同じ)。</summary>
    private Color GetClassColor(string professionKey)
    {
        var palette = _settings.ClassColorPalettes.TryGetValue(professionKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.TakenDamageLog, professionKey);
        var selectedIndex = _settings.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? "#A8A8A8"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        return ColorUtilities.TryParseHex(selectedColor, out var color)
            ? color
            : Color.FromRgb(0xA8, 0xA8, 0xA8);
    }

    /// <summary>
    /// HP の出し方。プレイヤーリスト・エンティティリストと同じ規則で、
    /// バリア量を足して出すか、括弧で分けて出すかを設定で選ぶ。分けて出すときもバリアが無ければ括弧を出さない。
    /// </summary>
    private string FormatHealthValue(long hp, long shield)
    {
        var currentShield = Math.Max(shield, 0L);
        if (_settings.HealthValueDisplayModeIndex == WidgetConfigDefaults.SeparateShieldHealthValueDisplayModeIndex)
        {
            return currentShield > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{hp}({currentShield})")
                : hp.ToString(CultureInfo.InvariantCulture);
        }

        var total = currentShield > 0 && hp > long.MaxValue - currentShield
            ? long.MaxValue
            : hp + currentShield;
        return total.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// フィルター「自傷・フレンドリーファイア以外」で出さない行か。加害者がプレイヤーの被弾(技の行ごと)を出さない。
    /// 隠した被弾もその時刻のまとめの行には入る。見える被弾も戦闘不能の行も無い時刻は、まとめごと出ない(<see cref="TakenDamageLogLayout"/>)。
    /// 自傷(加害者の無いバフ・落下・自分の技)は被弾した本人、プレイヤーの召喚体の攻撃は大元のプレイヤーが加害者として記録されている。
    /// 予告・詠唱・ダメージの無い戦闘不能は絞らない。加害者は記録から決まるので、履歴表示中も同じに効く。
    /// </summary>
    private bool IsHiddenByFilter(TakenDamageLogLine line)
    {
        return _settings.AttackerFilterIndex == WidgetConfigDefaults.NoSelfOrFriendlyFireTakenDamageLogAttackerFilterIndex
            && line.Kind == TakenDamageLogRecordKind.Hit
            && line.Attacker.IsPlayer;
    }

    private static string GetDisplayName(TakenDamageLogParty person)
    {
        if (person.IsSystem)
        {
            return LocalizationManager.Instance["TakenDamageLog_SystemActor"];
        }

        // 名前の無い、HP バーの見える敵。文言はエンティティリストと同じ。名前には内部ID注記だけが入っていることがある。
        if (person.IsUnnamedEnemy)
        {
            return LocalizationManager.Instance["EntityList_UnnamedEnemy"] + person.Name;
        }

        if (person.IsUnknownEnemy)
        {
            return LocalizationManager.Instance["EntityList_UnknownEnemy"];
        }

        return person.IsPlayer
            ? PlayerInfoFormatFormatter.GetDisplayName(
                person.Name,
                person.CharacterId,
                person.IsSelf,
                person.IsNpc,
                person.ProfessionId,
                PlayerRosterPresentationStore.Instance.NameDisplayMode)
            : person.Name;
    }
}
