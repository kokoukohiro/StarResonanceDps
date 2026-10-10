using System.IO;
using System.Text.Json;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.Config;

public sealed class WidgetStateManager
{
    private static readonly Lazy<WidgetStateManager> LazyInstance = new(() => new WidgetStateManager());
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _statePath;
    private readonly object _syncRoot = new();
    private WidgetStateDocument _document;

    private WidgetStateManager()
    {
        _statePath = AppDataPaths.WidgetStatePath;
        _document = Load();
    }

    public static WidgetStateManager Instance => LazyInstance.Value;

    /// <summary>起動時に、ファイルはあるのに読めなかった(壊れている・ウィジェットの設定が入っていない)か。既定値で動いている。</summary>
    public bool HasLoadFailed { get; private set; }

    public WidgetConfig GetWidgetSnapshot(WidgetKind kind)
    {
        lock (_syncRoot)
        {
            var key = WidgetConfigDefaults.GetKey(kind);
            if (!_document.Widgets.TryGetValue(key, out var config))
            {
                config = WidgetConfigDefaults.Create(kind);
                _document.Widgets[key] = config;
            }

            return WidgetConfigDefaults.CloneNormalized(kind, config);
        }
    }

    public void SaveWidget(WidgetKind kind, WidgetConfig config)
    {
        lock (_syncRoot)
        {
            var key = WidgetConfigDefaults.GetKey(kind);
            _document.Widgets[key] = WidgetConfigDefaults.CloneNormalized(kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetFlags(WidgetKind kind, bool isFavorite, bool isPinned, bool isClickThrough)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.IsFavorite = isFavorite;
            config.IsPinned = isPinned;
            config.IsClickThrough = isClickThrough;
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetState(WidgetKind kind, WidgetState state)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.State = state;
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetTheme(WidgetKind kind, WidgetThemeConfig theme)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.Theme = WidgetConfigDefaults.CloneNormalizedTheme(theme);
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetWindowBounds(WidgetKind kind, double x, double y, double width, double height)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.Window = new WidgetWindowConfig
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            };
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// 開いていた窓の対象一覧を保存する。窓が増減したときだけ呼ぶ。
    /// </summary>
    public void SaveWidgetOpenTargets(WidgetKind kind, IReadOnlyList<WidgetOpenTargetConfig> targets)
    {
        if (!WidgetConfigDefaults.SupportsOpenTargets(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.OpenTargets = targets.Count == 0
                ? null
                : targets.Select(target => target.Clone()).ToList();
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// ステータス詳細の行の並びだけを保存する。
    ///
    /// <para>
    /// 設定一式を書き戻す形にすると、開いている設定ウィンドウが持っている古い値とぶつかる。
    /// ウィンドウの位置と同じく、この項目だけを書く。
    /// </para>
    /// </summary>
    public void SavePlayerStatusRowOrder(WidgetKind kind, IReadOnlyList<int> order)
    {
        if (kind != WidgetKind.PlayerStatus)
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.PlayerStatusRowOrder = [.. order];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフカードの倍率を1件だけ保存する。
    ///
    /// <para>
    /// 拡大縮小はウィンドウごとに独立していて、他のウィンドウの値を巻き込みたくない。
    /// 設定一式を書き戻す形にすると、開いている別のカードが持っている値を上書きしてしまう。
    /// </para>
    /// </summary>
    public void SaveBuffCardScale(WidgetKind kind, string scaleKey, int scale)
    {
        if (string.IsNullOrWhiteSpace(scaleKey))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffCard ??= WidgetConfigDefaults.CreateBuffCardSettings();
            config.BuffCard.Scales[scaleKey] = WidgetConfigDefaults.ClampBuffCardScale(scale);
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフ一覧の非表示のバフだけを保存する。窓の右クリックで非表示にしたときに呼ぶ。
    ///
    /// <para>
    /// 設定一式を書き戻す形にすると、開いている設定ウィンドウが持っている未保存の値とぶつかる。
    /// ステータス詳細の行の並びと同じく、この項目だけを書く。
    /// </para>
    /// </summary>
    public void SaveBuffListHiddenBuffs(WidgetKind kind, IReadOnlyList<int> hiddenBuffIds)
    {
        if (!WidgetConfigDefaults.SupportsBuffListSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffList ??= WidgetConfigDefaults.CreateBuffListSettings(kind);
            config.BuffList.HiddenBuffIds = [.. hiddenBuffIds];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// 推移グラフの横軸の長さだけを保存する。窓の上のホイールで変えて、ホイールが止まったときに呼ぶ。
    ///
    /// <para>
    /// 設定一式を書き戻す形にすると、開いている設定ウィンドウが持っている未保存の値とぶつかる。
    /// バフ・デバフ一覧の非表示のバフと同じく、この項目だけを書く。
    /// </para>
    /// </summary>
    public void SaveMetricTimelineVisibleSeconds(WidgetKind kind, int visibleSeconds)
    {
        if (!WidgetConfigDefaults.SupportsMetricTimelineSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.MetricTimeline ??= WidgetConfigDefaults.CreateMetricTimelineSettings(kind);
            config.MetricTimeline.VisibleSeconds = visibleSeconds;
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    private WidgetConfig GetOrCreateWidgetConfig(WidgetKind kind)
    {
        var key = WidgetConfigDefaults.GetKey(kind);
        if (_document.Widgets.TryGetValue(key, out var config))
        {
            return config;
        }

        config = WidgetConfigDefaults.Create(kind);
        _document.Widgets[key] = config;
        return config;
    }

    private WidgetStateDocument Load()
    {
        if (!File.Exists(_statePath))
        {
            return CreateDefaultDocument();
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            var document = JsonSerializer.Deserialize<WidgetStateDocument>(json, JsonOptions);
            if (document?.Widgets is null)
            {
                HasLoadFailed = true;
                return CreateDefaultDocument();
            }

            Normalize(document);
            return document;
        }
        catch
        {
            // 読めないファイルは丸ごと既定値で動く。メイン窓が出たところで知らせ(SettingsLoadFailureMessage)、次の保存で上書きする。
            HasLoadFailed = true;
            return CreateDefaultDocument();
        }
    }

    private static WidgetStateDocument CreateDefaultDocument()
    {
        var document = new WidgetStateDocument();
        Normalize(document);
        return document;
    }

    private static void Normalize(WidgetStateDocument document)
    {
        document.Widgets ??= new Dictionary<string, WidgetConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (WidgetKind kind in Enum.GetValues<WidgetKind>())
        {
            var key = WidgetConfigDefaults.GetKey(kind);
            if (!document.Widgets.TryGetValue(key, out var config))
            {
                document.Widgets[key] = WidgetConfigDefaults.Create(kind);
                continue;
            }

            WidgetConfigDefaults.Normalize(kind, config);
        }
    }

    private void SaveCore()
    {
        Normalize(_document);

        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_document, JsonOptions);
        File.WriteAllText(_statePath, json);
    }
}
