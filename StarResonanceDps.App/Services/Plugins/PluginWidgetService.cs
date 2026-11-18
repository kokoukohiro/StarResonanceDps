// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグイン1つに渡す、ウィジェットを開く口。引数は呼んだスレッドで確かめ、開く処理は画面のスレッドの後の順に積む
/// (画面のスレッドから呼んでもその場では走らせない。プラグインが中身を作る途中から呼んでも窓の処理が入れ子にならない)。
/// <c>Task</c> は続きを非同期にして完了する。窓をアクティブにするのは、走るときにこのアプリの窓のどれかがアクティブなときだけ。
/// 止めたら(<see cref="Release"/>)何もせず偽で終える。
/// </summary>
internal sealed class PluginWidgetService : IPluginWidgetService
{
    private readonly string _pluginKey;
    private Dictionary<string, string>? _singleWidgetIds;
    private Dictionary<string, string>? _multiWidgetIds;
    private volatile bool _isReleased;

    public PluginWidgetService(string pluginKey)
    {
        _pluginKey = pluginKey ?? throw new ArgumentNullException(nameof(pluginKey));
    }

    /// <summary>検証したウィジェットの定義を受け取る(初期化の後、画面のスレッドで)。これより前の自分のウィジェットの呼び出しは引数の誤り。</summary>
    public void Attach(IReadOnlyList<PluginWidgetDefinition> definitions, IReadOnlyList<PluginWidgetDefinition> multiWindowDefinitions)
    {
        Volatile.Write(ref _singleWidgetIds, definitions.ToDictionary(definition => definition.Id, definition => definition.Id, StringComparer.OrdinalIgnoreCase));
        Volatile.Write(ref _multiWidgetIds, multiWindowDefinitions.ToDictionary(definition => definition.Id, definition => definition.Id, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>プラグインを止めた。積んだまま走っていない要求も、走るときに偽で終える。</summary>
    public void Release()
    {
        _isReleased = true;
    }

    public Task<bool> OpenAsync(string widgetId)
    {
        var id = ResolveWidgetId(Volatile.Read(ref _singleWidgetIds), widgetId, "a single-window widget (IPluginWidgetProvider)");
        var key = WidgetKey.ForPlugin(_pluginKey, id);
        return Run(activate => WidgetWindowManager.Instance.OpenPluginWidgetFromPlugin(key, targetId: null, activate));
    }

    public Task<bool> OpenAsync(string widgetId, string targetId)
    {
        var id = ResolveWidgetId(Volatile.Read(ref _multiWidgetIds), widgetId, "a per-target widget (IPluginMultiWidgetProvider)");
        if (!PluginIdRules.IsValid(targetId))
        {
            throw new ArgumentException($"The target ID '{targetId}' does not follow the ID rules.", nameof(targetId));
        }

        var key = WidgetKey.ForPlugin(_pluginKey, id);
        return Run(activate => WidgetWindowManager.Instance.OpenPluginWidgetFromPlugin(key, targetId, activate));
    }

    public Task<bool> OpenHostAsync(PluginHostWidgetKind widget)
    {
        var kind = ToWidgetKind(widget);
        if (!WidgetConfigDefaults.IsPluginOpenableSingleWindowKind(kind))
        {
            throw new ArgumentException($"{widget} cannot be opened with OpenHostAsync.", nameof(widget));
        }

        return Run(activate => WidgetWindowManager.Instance.OpenHostWidgetFromPlugin(kind, activate));
    }

    public Task<bool> OpenHostForPlayerAsync(PluginHostWidgetKind widget, long characterId)
    {
        var kind = ToWidgetKind(widget);
        if (!WidgetConfigDefaults.IsPluginOpenablePlayerWindowKind(kind))
        {
            throw new ArgumentException($"{widget} does not show one player.", nameof(widget));
        }

        return Run(activate => WidgetWindowManager.Instance.OpenHostPlayerWidgetFromPlugin(kind, characterId, activate));
    }

    public Task<bool> OpenHostForEntityAsync(PluginHostWidgetKind widget, long entityUuid)
    {
        var kind = ToWidgetKind(widget);
        if (!WidgetConfigDefaults.IsPluginOpenableEntityWindowKind(kind))
        {
            throw new ArgumentException($"{widget} does not show one enemy.", nameof(widget));
        }

        return Run(activate => WidgetWindowManager.Instance.OpenHostEntityWidgetFromPlugin(kind, entityUuid, activate));
    }

    private static string ResolveWidgetId(Dictionary<string, string>? ids, string widgetId, string expected)
    {
        if (ids is null)
        {
            throw new ArgumentException("The widget definitions are read after Initialize returns; open widgets after that.", nameof(widgetId));
        }

        return widgetId is not null && ids.TryGetValue(widgetId, out var id)
            ? id
            : throw new ArgumentException($"'{widgetId}' is not {expected} of this plugin.", nameof(widgetId));
    }

    private static WidgetKind ToWidgetKind(PluginHostWidgetKind widget)
    {
        return Enum.IsDefined(widget)
            ? (WidgetKind)(int)widget
            : throw new ArgumentException($"{(int)widget} is not a value of PluginHostWidgetKind.", nameof(widget));
    }

    /// <summary>
    /// 画面のスレッドの後の順で開く。走るときにこのアプリの窓のどれかがアクティブなら、窓をアクティブにしてよい。
    /// 本体の処理の例外は、Task に入れたうえで画面のスレッドへ投げ直す(本体の不具合を隠さない)。
    /// </summary>
    private Task<bool> Run(Func<bool, bool> open)
    {
        if (_isReleased || Application.Current?.Dispatcher is not { HasShutdownStarted: false } dispatcher)
        {
            return Task.FromResult(false);
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.BeginInvoke(() =>
        {
            if (_isReleased)
            {
                completion.SetResult(false);
                return;
            }

            bool opened;
            try
            {
                var activate = Application.Current.Windows.OfType<Window>().Any(window => window.IsActive);
                opened = open(activate);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
                throw;
            }

            completion.SetResult(opened && !_isReleased);
        });
        return completion.Task;
    }
}
