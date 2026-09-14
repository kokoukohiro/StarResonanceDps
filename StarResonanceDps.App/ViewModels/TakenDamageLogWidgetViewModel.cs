using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 画面に出す被ダメログの1行。時刻を持つのは予告行・詠唱行と技の行だけで、被弾行は <see cref="TimeText"/> が空。
/// </summary>
public sealed record TakenDamageLogEntry(string TimeText, string Text);

/// <summary>
/// 被ダメログ。表示中のエンカウンターで、ボス大技の予告、プレイヤー以外の詠唱と、プレイヤーがプレイヤー以外から受けたダメージを出す。
///
/// <code>
/// 00:22:01  [敵]が[技]を構えている
/// 00:22:03  [敵]が[技]を唱えている
/// 00:22:05  [敵]の[技]
///           [プレイヤー]に12345ダメージ(123/123456)
/// </code>
///
/// <para>
/// <b>被弾は発動単位で束ねない。</b> 届いた瞬間ごとに技の行を1行出し、その下に
/// 同じ瞬間(同じ加害者・同じ発生源・同じ到着時刻)の被弾だけを並べる。
/// 同じ技の次の被弾は、また技の行から出す。
/// </para>
///
/// <para>
/// <b>履歴表示の対象。</b> エンカウンターの決め方はメーターと同じで、履歴を開けばその戦闘のログになる。
/// ライブ中は <see cref="RefreshInterval"/> ごとに増えた分だけ追記する。
/// </para>
/// </summary>
public sealed class TakenDamageLogWidgetViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<TakenDamageLogEntry> _entries = [];

    // 同じ到着時刻の被弾を技の行の下へ差し込むための、まとまりごとの最後の行の位置。
    // 到着時刻が変わったら閉じる。取得の区切りをまたいでも同じまとまりへ入る。
    private readonly Dictionary<HitGroupKey, int> _openHitGroupEnds = [];
    private DateTime? _openHitGroupTimestamp;

    private Encounter? _encounter;
    private int _nextIndex;

    public TakenDamageLogWidgetViewModel()
    {
        Entries = new ReadOnlyObservableCollection<TakenDamageLogEntry>(_entries);

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
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
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
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
        ClearEntries();
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = MeterSnapshotProvider.GetTakenDamageLog(_encounter, _nextIndex);

        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            _encounter = snapshot.Encounter;
            ClearEntries();
        }

        _nextIndex = snapshot.NextIndex;
        foreach (var line in snapshot.Lines)
        {
            AppendEntries(line);
        }
    }

    private void ClearEntries()
    {
        _entries.Clear();
        _openHitGroupEnds.Clear();
        _openHitGroupTimestamp = null;
    }

    private void AppendEntries(TakenDamageLogLine line)
    {
        var localization = LocalizationManager.Instance;
        var attacker = GetDisplayName(line.Attacker);

        if (line.Kind is TakenDamageLogRecordKind.Announcement or TakenDamageLogRecordKind.Cast)
        {
            var format = line.Kind == TakenDamageLogRecordKind.Announcement
                ? "TakenDamageLog_AnnounceFormat"
                : "TakenDamageLog_CastFormat";
            _entries.Add(new TakenDamageLogEntry(
                MeterWidgetViewModel.FormatDuration(line.Elapsed),
                localization.Format(format, attacker, FormatSourceName(line, "TakenDamageLog_UnnamedCastSkill"))));
            return;
        }

        var source = FormatSourceName(line, "TakenDamageLog_UnnamedSkill");

        if (_openHitGroupTimestamp != line.Timestamp)
        {
            _openHitGroupEnds.Clear();
            _openHitGroupTimestamp = line.Timestamp;
        }

        var hitEntry = new TakenDamageLogEntry(string.Empty, FormatHit(line));
        var key = new HitGroupKey(line.Attacker.Uuid, line.IsBuffSource, line.SourceId, line.Timestamp);
        if (_openHitGroupEnds.TryGetValue(key, out var groupEnd))
        {
            var insertAt = groupEnd + 1;
            _entries.Insert(insertAt, hitEntry);
            foreach (var other in _openHitGroupEnds.Keys.ToArray())
            {
                if (_openHitGroupEnds[other] >= insertAt)
                {
                    _openHitGroupEnds[other]++;
                }
            }

            _openHitGroupEnds[key] = insertAt;
            return;
        }

        _entries.Add(new TakenDamageLogEntry(
            MeterWidgetViewModel.FormatDuration(line.Elapsed),
            localization.Format("TakenDamageLog_SkillFormat", attacker, source)));
        _entries.Add(hitEntry);
        _openHitGroupEnds[key] = _entries.Count - 1;
    }

    /// <summary>
    /// 技(またはバフ)の名前。名前が無ければこのウィジェットだけ <paramref name="unnamedKey"/> の文言を出す。
    /// 技の行は「攻撃」、予告行と詠唱行は「を構えている」「を唱えている」に続くので「何か」。
    /// </summary>
    private static string FormatSourceName(TakenDamageLogLine line, string unnamedKey)
    {
        var name = string.IsNullOrEmpty(line.SourceName)
            ? LocalizationManager.Instance.GetString(unnamedKey)
            : line.SourceName;
        return line.IsBuffSource
            ? CombatDataCatalog.AppendBuffInternalId(name, line.SourceId)
            : CombatDataCatalog.AppendSkillInternalId(name, line.SourceId);
    }

    private static string FormatHit(TakenDamageLogLine line)
    {
        var localization = LocalizationManager.Instance;
        var target = GetDisplayName(line.Target!);
        var value = line.Value.ToString(CultureInfo.InvariantCulture);

        return line.TargetHp is { } hp
            ? localization.Format(
                "TakenDamageLog_HitFormat",
                target,
                value,
                hp.ToString(CultureInfo.InvariantCulture),
                line.TargetMaxHp?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
            : localization.Format("TakenDamageLog_HitFormatNoHp", target, value);
    }

    private static string GetDisplayName(TakenDamageLogParty person)
    {
        return person.IsPlayer
            ? PlayerInfoFormatFormatter.GetDisplayName(
                person.Name,
                person.CharacterId,
                person.IsSelf,
                PlayerRosterPresentationStore.Instance.NameDisplayMode)
            : person.Name;
    }

    private readonly record struct HitGroupKey(long AttackerUuid, bool IsBuffSource, int SourceId, DateTime Timestamp);
}
