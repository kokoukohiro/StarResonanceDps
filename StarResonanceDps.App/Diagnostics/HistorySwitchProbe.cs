using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.Diagnostics;

/// <summary>
/// 一時計測: 集計タブで履歴の回を選んでから画面が落ち着くまでに、画面のスレッドが何に時間を使ったか。
/// 履歴を選んだときにウィジェットを開いていると画面が数秒止まる件の切り分け用。
///
/// <para>
/// 1回の選択(区間)ごとに書くもの: 押した時刻と開いているウィジェット、読み込みの時間と結果、反映の合図(スレッド)、
/// 顔ぶれの反映(プレイヤーリストとプレイヤーの窓)の時間と行の数、ウィジェットごとの更新(データを作る時間・画面を作り直す時間・数)、
/// 画面のスレッドの処理のうち 1ms 以上のもの1つずつ(優先度・メソッド・時間)と全部の処理の合計、
/// 全ウィジェットが反映の後に1回更新して画面の処理が空いた時刻、GC の回数と止まった時間。
/// 自動でライブへ戻ったとき(押していない反映の合図)も区間として書く。
/// </para>
///
/// <para>
/// 区間の間は、ウィジェットの更新と顔ぶれの反映のたびに、始めにたまっている配置を済ませ(<c>pre</c>)、
/// 終わりにその更新が生んだ配置をその場で済ませる(<c>layout</c>)。配置を窓ごとに分けて測るため。
/// 配置の仕事の量は変わらず、後の描画でまとめて行うはずのものを早めるだけ。更新ごとの GC の差も書く。
/// プレイヤーの窓へ顔ぶれを当てる処理は窓ごとに <c>window</c> の行で書く(登録の無い窓も含む)。
/// </para>
///
/// <para>
/// 画面のスレッドの出来事(メッセージ・Dispatcher の処理の始まりと終わり)が、処理の外で <see cref="GapLineThresholdMs"/> 以上空いたら、
/// <c>gap</c> の行に長さ・その間に画面のスレッドが使った CPU 時間・プロセス全体の CPU 時間・GC の停止・前後の出来事を書く。
/// 画面のスレッドの CPU 時間が長さと同じなら画面のスレッドは忙しく、0 なら待っていたか GC で止まっていた。
/// 画面のスレッドが 0 でほかのスレッドの分(<c>otherThreadsCpu</c>)が長さに近ければ、ほかのスレッドが忙しい間を待っていた。
/// </para>
///
/// <para>
/// プレイヤーリスト・エンティティリスト・被ダメログ・推移グラフをスクロールする入力(ホイール、窓の枠のスクロールバー)が来たときも区間を始める(<c>trigger=scroll</c>)。
/// 入力が続くか位置が動いている間は延ばし、最後に動いてから画面の処理が空いたら閉じる。
/// </para>
/// </summary>
internal static class HistorySwitchProbe
{
    private const double OperationLineThresholdMs = 1d;
    private const double GapLineThresholdMs = 100d;

    private static readonly object Sync = new();

    private static readonly string FilePath =
        Path.Combine(CombatRuntimePaths.LogsDirectory, $"HistorySwitchProbe_{DiagnosticSession.Stamp}.txt");

    /// <summary>Dispatcher の処理が持つ呼び出し先。WPF の内部の項目なので、読めなければ null(名前は「(unknown)」と書く)。</summary>
    private static readonly FieldInfo? OperationMethodField =
        typeof(DispatcherOperation).GetField("_method", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly Dictionary<object, string> Widgets = new(ReferenceEqualityComparer.Instance);
    private static readonly HashSet<object> PendingWidgets = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<object, int> RefreshNumbers = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<DispatcherOperation, long> OperationStarts = [];
    private static readonly Dictionary<string, (int Count, double TotalMs, double MaxMs)> OperationTotals = [];

    private static int _sequence;

    /// <summary>今の区間の番号。0 は測っていない。</summary>
    private static int _session;

    private static long _sessionStart;
    private static bool _isApplied;
    private static bool _settleScheduled;
    private static bool _hooksInstalled;
    private static bool _writeFailureLogged;
    private static (int Gen0, int Gen1, int Gen2, TimeSpan Pause) _gcAtStart;

    /// <summary>画面のスレッドで今更新しているウィジェット(グラフの部品の記録をその更新の行に付ける)。</summary>
    private static RefreshScope? _currentScope;

    /// <summary>
    /// 実行中の Dispatcher の処理(入れ子の処理もある)。数が処理の深さ。区間の外でも持つ。
    /// 始まった処理だけを入れるので、始まる前に取り消された処理(止め直したタイマーなど)で深さは減らない。
    /// </summary>
    private static readonly HashSet<DispatcherOperation> RunningOperations = [];

    /// <summary>
    /// 前の出来事の時刻・画面のスレッドの CPU 時間・プロセス全体の CPU 時間(どちらも 100ns、取れなければ -1)・GC の停止の合計・中身・その後の処理の深さ。
    /// 時刻 0 はまだ無い。
    /// </summary>
    private static long _lastActivityTimestamp;
    private static long _lastActivityCpu;
    private static long _lastActivityProcessCpu;
    private static TimeSpan _lastActivityGcPause;
    private static string _lastActivity = string.Empty;
    private static int _lastActivityDepth;

    /// <summary>今の区間がスクロールで始めたものか。</summary>
    private static bool _isScrollSession;

    /// <summary>スクロールの区間を閉じる予定の番号。動くたびに進め、最後の予定だけが閉じる。</summary>
    private static int _scrollSettleGeneration;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(
        IntPtr process,
        out long creationTime,
        out long exitTime,
        out long kernelTime,
        out long userTime);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(
        IntPtr thread,
        out long creationTime,
        out long exitTime,
        out long kernelTime,
        out long userTime);

    static HistorySwitchProbe()
    {
        EncounterHistoryProvider.SelectionChanged += OnSelectionChanged;
    }

    /// <summary>履歴に追従するウィジェットの ViewModel を登録する(作ったとき)。名前は種類と個体の番号。</summary>
    public static void Register(object viewModel, string kind)
    {
        lock (Sync)
        {
            Widgets[viewModel] = $"{kind}#{RuntimeHelpers.GetHashCode(viewModel):x8}";
        }
    }

    /// <summary>ウィジェットを閉じた。待っている区間からも外す。</summary>
    public static void Unregister(object viewModel)
    {
        lock (Sync)
        {
            Widgets.Remove(viewModel);
            RefreshNumbers.Remove(viewModel);
            if (PendingWidgets.Remove(viewModel))
            {
                ScheduleSettleIfReady();
            }
        }
    }

    /// <summary>集計タブで回を押した(画面のスレッド)。区間を始める。</summary>
    public static void SelectionRequested(ulong encounterId)
    {
        Start($"select encounter={encounterId}");
    }

    /// <summary>集計タブでライブへ戻した(画面のスレッド)。区間を始める。</summary>
    public static void LiveRequested()
    {
        Start("live");
    }

    /// <summary>
    /// 一覧をスクロールする入力が来た(画面のスレッド。ホイールか、窓の枠のスクロールバーから位置が来たとき)。
    /// 位置が動く前に呼ぶので、その入力で起きる配置も区間に入る。区間が無ければスクロールの区間を始め、スクロールの区間の途中なら閉じるのを延ばす。
    /// 履歴の切り替えの区間の中なら何もしない(その区間の処理として書かれる)。
    /// </summary>
    public static void ScrollInputReceived(string listName)
    {
        lock (Sync)
        {
            if (_session == 0)
            {
                Start($"scroll {listName}");
                _isScrollSession = true;
            }
            else if (!_isScrollSession)
            {
                return;
            }

            ScheduleScrollSettle();
        }
    }

    /// <summary>
    /// 一覧の位置が動いた(画面のスレッド、ScrollChanged)。スクロールの区間の途中なら閉じるのを延ばす。区間は始めない
    /// (この合図は配置を終えた後に来るので、ここから始めるとスクロールの配置が区間に入らない)。
    /// </summary>
    public static void ScrollPositionChanged()
    {
        lock (Sync)
        {
            if (_session != 0 && _isScrollSession)
            {
                ScheduleScrollSettle();
            }
        }
    }

    /// <summary>スクロールの区間を、画面の処理が空いたら閉じる予定にする。前の予定は無効にする。ロックの中で呼ぶ。</summary>
    private static void ScheduleScrollSettle()
    {
        if (Application.Current?.Dispatcher is not { } dispatcher)
        {
            return;
        }

        var generation = ++_scrollSettleGeneration;
        var session = _session;
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            lock (Sync)
            {
                if (_session != session || !_isScrollSession || _scrollSettleGeneration != generation)
                {
                    return;
                }

                Write([$"#{_session} scroll idle at +{OffsetMs():0.0}ms"]);
                Finish("scroll settled");
            }
        });
    }

    /// <summary>押した回の読み込みが終わった(画面のスレッド、await の後)。</summary>
    public static void LoadCompleted(ulong encounterId, double elapsedMs, HistorySelectResult result)
    {
        lock (Sync)
        {
            if (_session == 0)
            {
                return;
            }

            Write([$"#{_session} load +{OffsetMs():0.0}ms encounter={encounterId} took={elapsedMs:0.0}ms result={result}"]);
        }
    }

    /// <summary>
    /// 顔ぶれの反映(プレイヤーリストと、プレイヤーの窓へ当てる処理を積むところまで)を測り始める。区間の外なら null。
    /// 終わりに <see cref="RefreshScope.End"/>(数は行の数)。
    /// </summary>
    public static RefreshScope? BeginRoster()
    {
        lock (Sync)
        {
            if (_session == 0)
            {
                return null;
            }
        }

        return CreateScope(widgetViewModel: null, label: "roster");
    }

    /// <summary>
    /// プレイヤーの窓へ顔ぶれを当てる処理を測り始める(登録の無い窓も)。区間の外なら null。
    /// 中で登録のあるウィジェットが更新したら、その行は別に出る(この行の時間はそれを含む)。
    /// </summary>
    public static RefreshScope? BeginWindow(object viewModel, string kind)
    {
        lock (Sync)
        {
            if (_session == 0)
            {
                return null;
            }
        }

        return CreateScope(widgetViewModel: null, label: $"window {kind}#{RuntimeHelpers.GetHashCode(viewModel):x8}");
    }

    /// <summary>
    /// ウィジェットの更新を測り始める。区間の外か、登録の無い ViewModel なら null。
    /// データを作り終えたら <see cref="RefreshScope.DataDone"/>、終わりに <see cref="RefreshScope.End"/>。
    /// </summary>
    public static RefreshScope? BeginRefresh(object viewModel)
    {
        string label;
        lock (Sync)
        {
            if (_session == 0 || !Widgets.TryGetValue(viewModel, out var registered))
            {
                return null;
            }

            var number = RefreshNumbers.TryGetValue(viewModel, out var previous) ? previous + 1 : 1;
            RefreshNumbers[viewModel] = number;
            label = $"widget {registered} refresh#{number}";
        }

        return CreateScope(viewModel, label);
    }

    /// <summary>
    /// たまっている配置を先に済ませてから測り始める。配置はロックの外で行う
    /// (配置の間にパケットのスレッドの反映の合図を待たせない)。
    /// </summary>
    private static RefreshScope CreateScope(object? widgetViewModel, string label)
    {
        double startOffsetMs;
        lock (Sync)
        {
            startOffsetMs = OffsetMs();
        }

        var preLayoutMs = FlushLayout();
        lock (Sync)
        {
            // スクロールの区間には反映が無いので、反映の前の印(before apply)を付けない。
            var scope = new RefreshScope(widgetViewModel, label, startOffsetMs, _isApplied || _isScrollSession, preLayoutMs);
            _currentScope = scope;
            return scope;
        }
    }

    /// <summary>
    /// たまっている配置(全部の窓の分)をその場で済ませ、かかった時間を返す。
    /// <c>UpdateLayout</c> は呼んだ窓だけでなく、この画面のスレッドの配置の待ちを全部処理する。窓が無ければ null。
    /// </summary>
    private static double? FlushLayout()
    {
        if (Application.Current?.MainWindow is not { } window || !window.Dispatcher.CheckAccess())
        {
            return null;
        }

        var watch = Stopwatch.StartNew();
        window.UpdateLayout();
        return watch.Elapsed.TotalMilliseconds;
    }

    private static string FormatMs(double? value)
    {
        return value is { } ms ? $"{ms:0.0}ms" : "n/a";
    }

    private static (int Gen0, int Gen1, int Gen2, TimeSpan Pause) TakeGcSnapshot()
    {
        return (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), GC.GetTotalPauseDuration());
    }

    private static string DescribeGcSince((int Gen0, int Gen1, int Gen2, TimeSpan Pause) start)
    {
        return $"gc0/1/2=+{GC.CollectionCount(0) - start.Gen0}/+{GC.CollectionCount(1) - start.Gen1}/+{GC.CollectionCount(2) - start.Gen2} " +
               $"gcPause=+{(GC.GetTotalPauseDuration() - start.Pause).TotalMilliseconds:0.0}ms";
    }

    /// <summary>今更新しているウィジェットの行に付け足す(グラフの部品のアイコンの作り直しなど)。更新の外なら区間の行として書く。</summary>
    public static void Note(string text)
    {
        lock (Sync)
        {
            if (_session == 0)
            {
                return;
            }

            if (_currentScope is { } scope)
            {
                scope.Notes.Add(text);
                return;
            }

            Write([$"#{_session} note +{OffsetMs():0.0}ms {text}"]);
        }
    }

    /// <summary>
    /// 1回の更新の計測。<c>pre</c> は始めに済ませた、ほかの窓などがためていた配置、
    /// <c>layout</c> は終わりに済ませた、この更新が生んだ配置。GC の差は始めの配置の後から終わりの配置までの分。
    /// ウィジェットの ViewModel が null なら顔ぶれの反映かプレイヤーの窓へ当てる処理(データと画面を分けない)。
    /// </summary>
    internal sealed class RefreshScope
    {
        private readonly object? _widgetViewModel;
        private readonly string _label;
        private readonly double _startOffsetMs;
        private readonly bool _isAfterApply;
        private readonly double? _preLayoutMs;
        private readonly (int Gen0, int Gen1, int Gen2, TimeSpan Pause) _gcStart = TakeGcSnapshot();
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private double? _dataMs;

        public RefreshScope(object? widgetViewModel, string label, double startOffsetMs, bool isAfterApply, double? preLayoutMs)
        {
            _widgetViewModel = widgetViewModel;
            _label = label;
            _startOffsetMs = startOffsetMs;
            _isAfterApply = isAfterApply;
            _preLayoutMs = preLayoutMs;
        }

        public List<string> Notes { get; } = [];

        public void DataDone()
        {
            _dataMs = _watch.Elapsed.TotalMilliseconds;
        }

        public void End(string counts)
        {
            var updateMs = _watch.Elapsed.TotalMilliseconds;
            var dataMs = _dataMs ?? updateMs;
            bool isActive;
            lock (Sync)
            {
                isActive = _session != 0;
            }

            var layoutMs = isActive ? FlushLayout() : null;
            var gc = DescribeGcSince(_gcStart);
            lock (Sync)
            {
                if (ReferenceEquals(_currentScope, this))
                {
                    _currentScope = null;
                }

                if (_session == 0)
                {
                    return;
                }

                var notes = Notes.Count == 0 ? string.Empty : " | " + string.Join(" | ", Notes);
                var totalMs = updateMs + (layoutMs ?? 0d);
                if (_widgetViewModel is null)
                {
                    Write([$"#{_session} {_label} +{_startOffsetMs:0.0}ms took={updateMs:0.0}ms layout={FormatMs(layoutMs)} total={totalMs:0.0}ms " +
                           $"pre={FormatMs(_preLayoutMs)} {gc} {counts}{notes}"]);
                    return;
                }

                Write([$"#{_session} {_label} +{_startOffsetMs:0.0}ms data={dataMs:0.0}ms ui={updateMs - dataMs:0.0}ms " +
                       $"layout={FormatMs(layoutMs)} total={totalMs:0.0}ms pre={FormatMs(_preLayoutMs)} {gc} " +
                       $"{counts}{(_isAfterApply ? string.Empty : " (before apply)")}{notes}"]);

                if (_isAfterApply && PendingWidgets.Remove(_widgetViewModel))
                {
                    ScheduleSettleIfReady();
                }
            }
        }
    }

    private static void Start(string trigger)
    {
        lock (Sync)
        {
            InstallHooks();
            if (_session != 0)
            {
                Finish("aborted by next request");
            }

            _session = ++_sequence;
            _sessionStart = Stopwatch.GetTimestamp();
            _lastActivityTimestamp = 0;
            _isScrollSession = false;
            _isApplied = false;
            _settleScheduled = false;
            PendingWidgets.Clear();
            OperationTotals.Clear();
            _gcAtStart = TakeGcSnapshot();
            Write([$"#{_session} start {DateTime.Now:HH:mm:ss.fff} trigger={trigger} widgets=[{string.Join(", ", Widgets.Values)}]"]);
        }
    }

    /// <summary>
    /// 選択の反映の合図(パケットのスレッドなど)。押していない合図(自動でライブへ戻った・ログアウト)なら区間を始める。
    /// スクロールの区間の途中なら、それを閉じて始め直す。
    /// </summary>
    private static void OnSelectionChanged()
    {
        var thread = Environment.CurrentManagedThreadId;
        var isUiThread = Application.Current?.Dispatcher.CheckAccess() == true;
        lock (Sync)
        {
            if (_session == 0 || _isScrollSession)
            {
                Start($"selection changed without request (selected={EncounterHistoryProvider.SelectedEncounterId?.ToString(CultureInfo.InvariantCulture) ?? "live"})");
            }

            _isApplied = true;
            PendingWidgets.Clear();
            foreach (var widget in Widgets.Keys)
            {
                PendingWidgets.Add(widget);
            }

            Write([$"#{_session} applied +{OffsetMs():0.0}ms thread={thread}{(isUiThread ? " (UI)" : string.Empty)} " +
                   $"selected={EncounterHistoryProvider.SelectedEncounterId?.ToString(CultureInfo.InvariantCulture) ?? "live"} " +
                   $"pending=[{string.Join(", ", PendingWidgets.Select(widget => Widgets[widget]))}]"]);
            ScheduleSettleIfReady();
        }
    }

    /// <summary>反映の後に全ウィジェットが1回更新したら、画面の処理が空いた時刻を取りに行く。ロックの中で呼ぶ。</summary>
    private static void ScheduleSettleIfReady()
    {
        if (_session == 0 || !_isApplied || _settleScheduled || PendingWidgets.Count > 0
            || Application.Current?.Dispatcher is not { } dispatcher)
        {
            return;
        }

        _settleScheduled = true;
        var session = _session;
        var posted = OffsetMs();
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            lock (Sync)
            {
                if (_session != session)
                {
                    return;
                }

                Write([$"#{_session} all widgets refreshed at +{posted:0.0}ms, idle at +{OffsetMs():0.0}ms"]);
                Finish("settled");
            }
        });
    }

    /// <summary>区間を閉じる。合計と GC の差を書く。ロックの中で呼ぶ。</summary>
    private static void Finish(string reason)
    {
        var wallMs = OffsetMs();
        var operationsMs = OperationTotals.Values.Sum(value => value.TotalMs);
        var lines = new List<string>
        {
            $"#{_session} end ({reason}) +{wallMs:0.0}ms dispatcherOps={operationsMs:0.0}ms ({OperationTotals.Values.Sum(value => value.Count)} ops) " +
            $"outsideOps={wallMs - operationsMs:0.0}ms {DescribeGcSince(_gcAtStart)}" +
            (PendingWidgets.Count == 0 ? string.Empty : $" notRefreshed=[{string.Join(", ", PendingWidgets.Select(widget => Widgets.GetValueOrDefault(widget, "?")))}]")
        };
        lines.AddRange(OperationTotals
            .OrderByDescending(pair => pair.Value.TotalMs)
            .Select(pair => $"#{_session}    total {pair.Value.TotalMs,9:0.0}ms n={pair.Value.Count,-5} max={pair.Value.MaxMs:0.0}ms  {pair.Key}"));
        Write(lines);

        OperationTotals.Clear();
        PendingWidgets.Clear();
        _currentScope = null;
        _lastActivityTimestamp = 0;
        _isScrollSession = false;
        _session = 0;
    }

    /// <summary>
    /// 画面のスレッドの Dispatcher の処理の開始と終わり、メッセージを受ける(区間の間だけ数える。処理の深さはいつも数える)。
    /// ロックの中で、画面のスレッドで呼ぶ。
    /// </summary>
    private static void InstallHooks()
    {
        if (_hooksInstalled || Application.Current?.Dispatcher is not { } dispatcher || !dispatcher.CheckAccess())
        {
            return;
        }

        _hooksInstalled = true;
        dispatcher.Hooks.OperationStarted += (_, e) =>
        {
            lock (Sync)
            {
                RunningOperations.Add(e.Operation);
                if (_session != 0)
                {
                    NoteActivity($"op start {e.Operation.Priority} {DescribeOperation(e.Operation)}", RunningOperations.Count);
                    OperationStarts[e.Operation] = Stopwatch.GetTimestamp();
                }
            }
        };
        dispatcher.Hooks.OperationCompleted += (_, e) => RecordOperation(e.Operation, aborted: false);
        dispatcher.Hooks.OperationAborted += (_, e) => RecordOperation(e.Operation, aborted: true);
        ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
        Write([$"dispatcher hooks installed (operation method field {(OperationMethodField is null ? "not found" : "found")})"]);
    }

    private static void OnThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        lock (Sync)
        {
            if (_session != 0)
            {
                NoteActivity($"msg 0x{msg.message:x4} {DescribeWindow(msg.hwnd)}", RunningOperations.Count);
            }
        }
    }

    /// <summary>
    /// 画面のスレッドの出来事を控える。前の出来事の後が処理の外で、そこから <see cref="GapLineThresholdMs"/> 以上空いていたら gap の行を書く。
    /// <paramref name="depthAfter"/> はこの出来事の後の処理の深さ。ロックの中で、区間の間だけ呼ぶ。
    /// </summary>
    private static void NoteActivity(string activity, int depthAfter)
    {
        var now = Stopwatch.GetTimestamp();
        var cpu = GetCurrentThreadCpuTime();
        var processCpu = GetCurrentProcessCpuTime();
        var gcPause = GC.GetTotalPauseDuration();
        if (_lastActivityTimestamp != 0 && _lastActivityDepth == 0)
        {
            var gapMs = Stopwatch.GetElapsedTime(_lastActivityTimestamp, now).TotalMilliseconds;
            if (gapMs >= GapLineThresholdMs)
            {
                var hasThreadCpu = cpu >= 0 && _lastActivityCpu >= 0;
                var hasProcessCpu = processCpu >= 0 && _lastActivityProcessCpu >= 0;
                var threadCpuMs = (cpu - _lastActivityCpu) / 10_000d;
                var processCpuMs = (processCpu - _lastActivityProcessCpu) / 10_000d;
                var cpuText = hasThreadCpu ? $"{threadCpuMs:0.0}ms" : "n/a";
                var processCpuText = hasProcessCpu ? $"{processCpuMs:0.0}ms" : "n/a";
                var otherThreadsCpuText = hasThreadCpu && hasProcessCpu ? $"{processCpuMs - threadCpuMs:0.0}ms" : "n/a";
                Write([$"#{_session} gap +{Stopwatch.GetElapsedTime(_sessionStart, _lastActivityTimestamp).TotalMilliseconds:0.0}ms " +
                       $"len={gapMs:0.0}ms cpu={cpuText} procCpu={processCpuText} otherThreadsCpu={otherThreadsCpuText} " +
                       $"gcPause=+{(gcPause - _lastActivityGcPause).TotalMilliseconds:0.0}ms " +
                       $"after=[{_lastActivity}] before=[{activity}]"]);
            }
        }

        _lastActivityTimestamp = now;
        _lastActivityCpu = cpu;
        _lastActivityProcessCpu = processCpu;
        _lastActivityGcPause = gcPause;
        _lastActivity = activity;
        _lastActivityDepth = depthAfter;
    }

    /// <summary>今のスレッドが使った CPU 時間(カーネル＋ユーザー、100ns)。取れなければ -1。</summary>
    private static long GetCurrentThreadCpuTime()
    {
        return GetThreadTimes(GetCurrentThread(), out _, out _, out var kernelTime, out var userTime)
            ? kernelTime + userTime
            : -1;
    }

    /// <summary>このプロセスの全部のスレッドが使った CPU 時間(カーネル＋ユーザー、100ns)。取れなければ -1。</summary>
    private static long GetCurrentProcessCpuTime()
    {
        return GetProcessTimes(GetCurrentProcess(), out _, out _, out var kernelTime, out var userTime)
            ? kernelTime + userTime
            : -1;
    }

    private static string DescribeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "(thread)";
        }

        return HwndSource.FromHwnd(hwnd)?.RootVisual switch
        {
            Window window => $"{window.GetType().Name} \"{window.Title}\"",
            { } root => root.GetType().Name,
            null => $"hwnd=0x{hwnd.ToInt64():x}"
        };
    }

    private static void RecordOperation(DispatcherOperation operation, bool aborted)
    {
        lock (Sync)
        {
            // 始まっていない処理の取り消し(ほかのスレッドから来ることもある)は、画面のスレッドの出来事ではない。
            if (!RunningOperations.Remove(operation))
            {
                return;
            }

            if (_session == 0)
            {
                OperationStarts.Remove(operation);
                return;
            }

            var description = DescribeOperation(operation);
            NoteActivity($"op end {operation.Priority} {description}", RunningOperations.Count);
            if (!OperationStarts.Remove(operation, out var start))
            {
                return;
            }

            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var key = $"{operation.Priority} {description}{(aborted ? " (aborted)" : string.Empty)}";
            OperationTotals[key] = OperationTotals.TryGetValue(key, out var current)
                ? (current.Count + 1, current.TotalMs + elapsedMs, Math.Max(current.MaxMs, elapsedMs))
                : (1, elapsedMs, elapsedMs);

            if (elapsedMs >= OperationLineThresholdMs)
            {
                var startOffset = Stopwatch.GetElapsedTime(_sessionStart, start).TotalMilliseconds;
                Write([$"#{_session} op +{startOffset:0.0}ms {elapsedMs:0.0}ms {key}"]);
            }
        }
    }

    private static string DescribeOperation(DispatcherOperation operation)
    {
        if (OperationMethodField?.GetValue(operation) is not Delegate method)
        {
            return "(unknown)";
        }

        var type = method.Method.DeclaringType;
        return $"{type?.FullName ?? "?"}.{method.Method.Name}";
    }

    private static double OffsetMs()
    {
        return _session == 0 ? 0d : Stopwatch.GetElapsedTime(_sessionStart).TotalMilliseconds;
    }

    private static void Write(IEnumerable<string> lines)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                DiagnosticSession.EnsureHeader(FilePath);
                File.AppendAllLines(FilePath, lines, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 計測の書き込みの失敗で本来の処理を止めない。1回だけ記録する。
                if (!_writeFailureLogged)
                {
                    _writeFailureLogged = true;
                    Log.Warning(ex, "HistorySwitchProbe could not write {Path}", FilePath);
                }
            }
        }
    }
}
