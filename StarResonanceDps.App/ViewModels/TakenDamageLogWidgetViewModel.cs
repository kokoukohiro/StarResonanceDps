using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 画面に出す被ダメログの1行。時刻を見せるのは予告行・詠唱行と技の行だけ(<see cref="IsTimeShown"/>)。
/// <see cref="Segments"/> は文字列とアイコン(<see cref="TakenDamageLogClassIconSegment"/> / <see cref="TakenDamageLogAttributeIconSegment"/>)の並び。
/// </summary>
/// <param name="TimeText">
/// どの行も自分の経過時刻を持つ。時刻を見せない行は場所だけ取って描かない。時刻の列の幅は画面に作られている行だけで揃うので、
/// 空にすると、時刻を見せる行が画面に無いとき列が縮んで本文が左へずれる。
/// </param>
public sealed record TakenDamageLogEntry(string TimeText, bool IsTimeShown, IReadOnlyList<object> Segments);

/// <summary>被弾行の対象の前に出すクラスアイコン。色はプレイヤーリストのクラスカラー、TIPS は特化名。</summary>
/// <param name="IconMask">職業アイコンの形。クラス色の塗りをこの形で抜く。</param>
/// <param name="Widget">TIPS の色を取る窓のパレットの持ち主。</param>
public sealed record TakenDamageLogClassIconSegment(Brush IconMask, Brush ClassBrush, string SpecName, WidgetListItemViewModel Widget);

/// <summary>被弾行の値の後ろに出す属性アイコン。TIPS は属性名。</summary>
/// <param name="Widget">TIPS の色を取る窓のパレットの持ち主。</param>
public sealed record TakenDamageLogAttributeIconSegment(ImageSource Icon, string Name, WidgetListItemViewModel Widget);

/// <summary>
/// 被ダメログ。表示中のエンカウンターで、ボス大技の予告、プレイヤー以外の詠唱と、プレイヤーがプレイヤー以外から受けたダメージを出す。
///
/// <code>
/// 00:22:01  [敵]が[技]を構えている
/// 00:22:03  [敵]が[技]を唱えている
/// 00:22:05  [敵]の[技]
///           [クラス][プレイヤー]に12345[属性]ダメージ(123/123456)
/// </code>
///
/// <para>
/// <b>被弾は発動単位で束ねない。</b> 届いた瞬間ごとに技の行を1行出し、その下に
/// 同じ瞬間(同じ加害者・同じ発生源・同じ到着時刻)の被弾だけを並べる。
/// 同じ技の次の被弾は、また技の行から出す。
/// 技の行の下で同じ対象・同じ属性の被弾は1行に畳む(並べ方は <see cref="TakenDamageLogLayout"/>)。
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

    private Encounter? _encounter;
    private int _nextIndex;
    private TakenDamageLogWidgetSettingsConfig _settings;

    public TakenDamageLogWidgetViewModel(WidgetListItemViewModel widget)
    {
        _widget = widget;
        _settings = widget.GetTakenDamageLogSettingsSnapshot();
        Entries = new ReadOnlyObservableCollection<TakenDamageLogEntry>(_entries);

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.TakenDamageLogSettingsChanged += Widget_TakenDamageLogSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        Refresh();
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
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
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
        _lines.Clear();
        ClearEntries();
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = MeterSnapshotProvider.GetTakenDamageLog(_encounter, _nextIndex);

        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            _encounter = snapshot.Encounter;
            _lines.Clear();
            ClearEntries();
        }

        _nextIndex = snapshot.NextIndex;
        _lines.AddRange(snapshot.Lines);
        foreach (var line in snapshot.Lines)
        {
            AppendEntries(line);
        }
    }

    private void ClearEntries()
    {
        _entries.Clear();
        _entryRows.Clear();
        _layout.Clear();
    }

    private void AppendEntries(TakenDamageLogLine line)
    {
        if (IsHiddenByFilter(line))
        {
            return;
        }

        var rows = _layout.Rows;
        for (var index = _layout.Append(line); index < rows.Count; index++)
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
        var localization = LocalizationManager.Instance;
        var line = row.Line;

        switch (row.Kind)
        {
            case TakenDamageLogRowKind.Announcement:
            case TakenDamageLogRowKind.Cast:
                var format = row.Kind == TakenDamageLogRowKind.Announcement
                    ? "TakenDamageLog_AnnounceFormat"
                    : "TakenDamageLog_CastFormat";
                return new TakenDamageLogEntry(
                    MeterWidgetViewModel.FormatDuration(line.Elapsed),
                    true,
                    [localization.Format(format, GetDisplayName(line.Attacker), FormatSourceName(line, "TakenDamageLog_UnnamedCastSkill"))]);
            case TakenDamageLogRowKind.Skill:
                return new TakenDamageLogEntry(
                    MeterWidgetViewModel.FormatDuration(line.Elapsed),
                    true,
                    [localization.Format("TakenDamageLog_SkillFormat", GetDisplayName(line.Attacker), FormatSourceName(line, "TakenDamageLog_UnnamedSkill"))]);
            case TakenDamageLogRowKind.Hit:
                return new TakenDamageLogEntry(MeterWidgetViewModel.FormatDuration(line.Elapsed), false, CreateHitSegments(row));
            case TakenDamageLogRowKind.Death:
                return new TakenDamageLogEntry(MeterWidgetViewModel.FormatDuration(line.Elapsed), false, CreateDeathSegments(row));
            default:
                throw new ArgumentOutOfRangeException(nameof(row), row.Kind, null);
        }
    }

    /// <summary>
    /// 技(またはバフ)の名前。名前が無ければこのウィジェットだけ <paramref name="unnamedKey"/> の文言を出す。
    /// 技の行は「攻撃」、予告行と詠唱行は「を構えている」「を唱えている」に続くので「何か」。
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
    /// 被弾行。書式の対象名({0})の前にクラスアイコン、属性アイコンの位置({2})に属性アイコンを差し込む。
    /// 属性を持たない被弾(属性を保存していなかった頃の記録)は属性アイコンを出さない。
    /// </summary>
    private IReadOnlyList<object> CreateHitSegments(TakenDamageLogRow row)
    {
        var localization = LocalizationManager.Instance;
        var line = row.Line;
        var target = line.Target!;
        var value = row.Value.ToString(CultureInfo.InvariantCulture);

        var text = row.TargetHp is { } hp
            ? localization.Format(
                "TakenDamageLog_HitFormat",
                TargetNameMarker,
                value,
                AttributeIconMarker,
                FormatHealthValue(hp, row.TargetShield ?? 0L),
                (row.TargetMaxHp ?? 0L).ToString(CultureInfo.InvariantCulture))
            : localization.Format("TakenDamageLog_HitFormatNoHp", TargetNameMarker, value, AttributeIconMarker);

        if (!text.Contains(TargetNameMarker, StringComparison.Ordinal)
            || !text.Contains(AttributeIconMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "TakenDamageLog_HitFormat / TakenDamageLog_HitFormatNoHp must contain {0} (target) and {2} (damage property icon).");
        }

        var segments = new List<object>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var marker = text[index];
            if (marker != TargetNameMarker[0] && marker != AttributeIconMarker[0])
            {
                continue;
            }

            if (index > start)
            {
                segments.Add(text[start..index]);
            }

            if (marker == TargetNameMarker[0])
            {
                segments.Add(CreateClassIconSegment(target));
                segments.Add(GetDisplayName(target));
            }
            else if (line.DamageElement is { } element)
            {
                segments.Add(new TakenDamageLogAttributeIconSegment(
                    (ImageSource)Application.Current.FindResource($"Icon.DamageProperty.{element}"),
                    localization.GetString($"DamageProperty_{element}"),
                    _widget));
            }

            start = index + 1;
        }

        if (start < text.Length)
        {
            segments.Add(text[start..]);
        }

        return segments;
    }

    /// <summary>
    /// ダメージの無い死亡の行。書式の対象名({0})の前にクラスアイコンを差し込む。
    /// 死亡時の最大HPが分からなければ 0 と出す(被弾の行・プレイヤーリストと同じ)。
    /// </summary>
    private IReadOnlyList<object> CreateDeathSegments(TakenDamageLogRow row)
    {
        var localization = LocalizationManager.Instance;
        var target = row.Line.Target!;

        var text = localization.Format(
            "TakenDamageLog_DeathFormat",
            TargetNameMarker,
            FormatHealthValue(row.TargetHp ?? 0L, 0L),
            (row.TargetMaxHp ?? 0L).ToString(CultureInfo.InvariantCulture));

        var markerIndex = text.IndexOf(TargetNameMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException(
                "TakenDamageLog_DeathFormat must contain {0} (target).");
        }

        var segments = new List<object>();
        if (markerIndex > 0)
        {
            segments.Add(text[..markerIndex]);
        }

        segments.Add(CreateClassIconSegment(target));
        segments.Add(GetDisplayName(target));

        var rest = markerIndex + TargetNameMarker.Length;
        if (rest < text.Length)
        {
            segments.Add(text[rest..]);
        }

        return segments;
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
            LocalizationManager.Instance.GetString($"ClassSpec_{target.ClassSpec}"),
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
    /// 自傷(加害者の無いバフ・落下・自分の技)は被弾した本人、プレイヤーの召喚体の攻撃は大元のプレイヤーが加害者として記録されている。
    /// 予告・詠唱・ダメージの無い死亡は絞らない。加害者は記録から決まるので、履歴表示中も同じに効く。
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
