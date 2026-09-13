using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 集計タブ。**エンカウンターという1つの対象への操作をまとめた場所。**
///
/// <para>
/// メーターのヘッダーにある 3分計測 / リセット と同じものをここにも置く。
/// どちらから押しても対象は1つ(<c>EncounterManager.Current</c>)で、
/// ウィジェットごとに分かれてはいない。
/// </para>
///
/// <para>
/// 履歴を選ぶと <c>AppState.OpenedHistoricalEncounter</c> が入り、
/// **メーター・スキル詳細・グラフ・集計のすべてがその戦闘を表示したまま止まる。**
/// **ライブへ戻す操作は「選択中の行をもう一度押す」だけ。**
/// 見出しは状態を出すだけで、押せる要素ではない。
/// </para>
/// </summary>
public sealed partial class AggregationViewModel : ObservableObject
{
    private const int ThreeMinuteBenchmarkDurationSeconds = 180;

    [ObservableProperty]
    private ObservableCollection<EncounterHistoryItem> _entries = [];

    /// <summary>ライブ(現在の戦闘)を見ているか。履歴を開いていない状態。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isLiveSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreeMinuteBenchmarkText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isBenchmarkActive;

    /// <summary>
    /// 見出しに出す状態。**優先順位は 履歴 &gt; 3分計測 &gt; 集計。**
    ///
    /// <para>
    /// 3分計測中に履歴を開いても計測は止まらない(<c>TrySelect</c> は計測に触らない)ので、
    /// そのときは「履歴表示中」が正しい。逆に 3分計測 / リセット は押した時点で
    /// <see cref="SelectLive"/> を通るので、見出しが履歴から切り替わって解除が見える。
    /// </para>
    /// </summary>
    public string StatusText => LocalizationManager.Instance.GetString(
        !IsLiveSelected ? "Aggregation_StatusHistory"
        : IsBenchmarkActive ? "Aggregation_StatusBenchmark"
        : "Aggregation_StatusLive");

    public string ThreeMinuteBenchmarkText => LocalizationManager.Instance.GetString(
        IsBenchmarkActive ? "Meter_StopBenchmark" : "Meter_ThreeMinuteBenchmark");

    public string ResetText => LocalizationManager.Instance.GetString("Meter_Reset");

    public string EmptyText => LocalizationManager.Instance.GetString("Aggregation_NoHistory");

    public bool HasEntries => Entries.Count > 0;

    /// <summary>一覧を読み直す。タブを開いたときと、戦闘が保存されたときに呼ぶ。</summary>
    public void Reload()
    {
        var selected = EncounterHistoryProvider.SelectedEncounterId;
        var items = new ObservableCollection<EncounterHistoryItem>();
        foreach (var entry in EncounterHistoryProvider.GetEntries())
        {
            items.Add(new EncounterHistoryItem(entry) { IsSelected = entry.EncounterId == selected });
        }

        Entries = items;
        IsLiveSelected = selected is null;
        OnPropertyChanged(nameof(HasEntries));
    }

    /// <summary>
    /// ベンチマークの状態を取り直す。3分計測は <c>AppState</c> の静的値で通知が無いので、
    /// ビュー側が定期的に呼ぶ。値が変わらなければ <c>ObservableProperty</c> が何も通知しない。
    /// </summary>
    public void RefreshBenchmarkState()
    {
        IsBenchmarkActive = MeterSnapshotProvider.GetBenchmarkState().IsActive;
    }

    /// <summary>
    /// ライブへ戻す。**コマンドにはしない** — 画面に「ライブへ戻す」ボタンは無く、
    /// 呼ぶのは <see cref="SelectEntry"/> のトグルと、3分計測 / リセット の3か所だけ。
    /// </summary>
    private void SelectLive()
    {
        EncounterHistoryProvider.SelectLive();
        ApplySelection(null);
    }

    [RelayCommand]
    private void SelectEntry(EncounterHistoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        // 選択中の行をもう一度押したら解除してライブへ戻す。
        // ライブへ戻す入口はここ1つなので、押し先を探させない。
        if (item.IsSelected)
        {
            SelectLive();
            return;
        }

        // 開けなかったら選択を動かさない。読めないものを「表示中」にしない。
        if (!EncounterHistoryProvider.TrySelect(item.EncounterId))
        {
            return;
        }

        ApplySelection(item.EncounterId);
    }

    [RelayCommand]
    private void ToggleThreeMinuteBenchmark()
    {
        // 履歴を開いたままだと、操作は効くのに画面はその戦闘に固定されたままで
        // 何も起きていないように見える。先にライブへ戻してから効かせる。
        SelectLive();

        if (MeterSnapshotProvider.GetBenchmarkState().IsActive)
        {
            MeterSnapshotProvider.TryStopBenchmark();
        }
        else
        {
            MeterSnapshotProvider.TryStartBenchmark(ThreeMinuteBenchmarkDurationSeconds);
        }

        RefreshBenchmarkState();
    }

    [RelayCommand]
    private void ResetEncounter()
    {
        SelectLive();
        MeterSnapshotProvider.ResetCurrentEncounter();
    }

    private void ApplySelection(ulong? encounterId)
    {
        foreach (var entry in Entries)
        {
            entry.IsSelected = entry.EncounterId == encounterId;
        }

        IsLiveSelected = encounterId is null;
    }

    /// <summary>
    /// 言語が変わったときに呼ぶ。**購読はビュー側が持つ** — <c>LocalizationManager</c> は
    /// singleton なので、ここで購読するとビューを開き直すたびに古いインスタンスが残る。
    /// </summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ThreeMinuteBenchmarkText));
        OnPropertyChanged(nameof(ResetText));
        OnPropertyChanged(nameof(EmptyText));
        foreach (var entry in Entries)
        {
            entry.RefreshTexts();
        }
    }
}

/// <summary>履歴一覧の1行。</summary>
public sealed partial class EncounterHistoryItem : ObservableObject
{
    private readonly EncounterHistoryEntry _entry;

    [ObservableProperty]
    private bool _isSelected;

    public EncounterHistoryItem(EncounterHistoryEntry entry)
    {
        _entry = entry;
    }

    public ulong EncounterId => _entry.EncounterId;

    public string TimeText => _entry.StartTime.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// シーン名は<b>表示時に引き直す</b>。DBの <c>SceneName</c> は記録時の言語と
    /// 内部ID注記のまま固定されているので、そのまま出すと表示言語に追従しない。
    /// 引けなかったときだけ保存値へ落とす。
    ///
    /// <para>
    /// 注記は<b>半角括弧で詰めて末尾に足す</b>(スペースを入れない)。
    /// フェーズは <c>PhaseNumber</c> から組み立てる —
    /// DBの <c>SceneSubName</c> は <c>"Phase 2"</c> の英語で固定されていて訳せない。
    /// 3分計測はボタンと同じ <c>Meter_ThreeMinuteBenchmark</c>。
    /// どちらも言語を切り替えると一緒に変わる(<see cref="RefreshTexts"/> が再通知する)。
    /// </para>
    /// </summary>
    public string SceneText
    {
        get
        {
            var resolved = CombatDataCatalog.GetSceneName(_entry.SceneId);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                resolved = _entry.SceneName;
            }

            var text = resolved;

            if (_entry.PhaseNumber > 0)
            {
                var phase = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationManager.Instance.GetString("Aggregation_EncounterPhase"),
                    _entry.PhaseNumber);
                text += $"({phase})";
            }
            else if (!string.IsNullOrWhiteSpace(_entry.SceneSubName))
            {
                // 数字が取れないのに記録だけある形。通常は起きない(書き手が必ず対で書く)が、
                // 黙って落とさずに保存された値をそのまま出す。
                text += $"({_entry.SceneSubName})";
            }

            if (_entry.BenchmarkSeconds > 0)
            {
                text += $"({LocalizationManager.Instance.GetString("Meter_ThreeMinuteBenchmark")})";
            }

            return text;
        }
    }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(SceneText));
    }
}
