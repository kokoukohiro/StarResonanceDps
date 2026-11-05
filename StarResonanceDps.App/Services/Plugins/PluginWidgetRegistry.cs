using System.Globalization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.ViewModels.Plugins;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>プラグインが足したウィジェット1つ(カード1枚)。定義は検証した写し。</summary>
public sealed class PluginWidgetRegistration
{
    private readonly PluginCallGuard _guard;
    private readonly IPluginWidgetProvider _provider;

    internal PluginWidgetRegistration(
        PluginInfo info,
        PluginWidgetDefinition definition,
        PluginCallGuard guard,
        IPluginWidgetProvider provider)
    {
        Info = info;
        Definition = definition;
        Key = WidgetKey.ForPlugin(info.Key, definition.Id);
        _guard = guard;
        _provider = provider;
    }

    public PluginInfo Info { get; }

    public PluginWidgetDefinition Definition { get; }

    public WidgetKey Key { get; }

    /// <summary>全部の節の設定(節の順、節の中の順)。</summary>
    public IEnumerable<PluginSettingDefinition> Settings => Definition.SettingSections.SelectMany(section => section.Settings);

    public string GetDisplayName(CultureInfo culture)
    {
        return PluginTexts.Resolve(Definition.Names, culture)
            ?? throw new InvalidOperationException($"The validated widget '{Definition.Id}' has no name.");
    }

    /// <summary>
    /// 窓の中身を作る(窓を開くたび、画面のスレッドで)。プラグインの例外と、返した中身が定義に合わないときは、
    /// 受け止め(<see cref="PluginCallGuard"/>)がそのプラグインを止め、null を返す。
    /// </summary>
    internal PluginWidgetWindowViewModel? CreateWindowViewModel(WidgetListItemViewModel widget)
    {
        var context = new PluginWidgetContext(Definition.Id, this, widget, _guard);
        if (!_guard.TryInvoke(
                "Widget content creation",
                () => Validate(_provider.CreateWidget(Definition.Id, context)),
                out var content))
        {
            context.Detach();
            return null;
        }

        return new PluginWidgetWindowViewModel(content, Definition.ScrollsVertically, context, _guard);
    }

    private PluginWidgetContent Validate(PluginWidgetContent? content)
    {
        if (content?.Body is null)
        {
            throw new InvalidOperationException($"CreateWidget returned no body for the widget '{Definition.Id}'.");
        }

        if (Definition.HasFooter != content.Footer is not null)
        {
            throw new InvalidOperationException(Definition.HasFooter
                ? $"The widget '{Definition.Id}' has a footer row, but CreateWidget returned no footer."
                : $"The widget '{Definition.Id}' has no footer row, but CreateWidget returned a footer.");
        }

        if (content.HeaderButtons is null || content.HeaderButtons.Any(button => button is null))
        {
            throw new InvalidOperationException($"CreateWidget returned a null header button list or button for the widget '{Definition.Id}'.");
        }

        return content;
    }
}

/// <summary>動いているプラグイン1つのウィジェット。</summary>
public sealed class PluginWidgetGroup
{
    internal PluginWidgetGroup(PluginInfo info, IReadOnlyList<PluginWidgetRegistration> widgets)
    {
        Info = info;
        Widgets = widgets;
    }

    public PluginInfo Info { get; }

    public string PluginKey => Info.Key;

    public IReadOnlyList<PluginWidgetRegistration> Widgets { get; }
}

/// <summary>
/// 動いているプラグインのウィジェット(全部のプラグインで1つ、画面のスレッドで使う)。
/// メイン画面はこの知らせでカードを足し外しする。止めたプラグインのカードは外し、窓は閉じる(保存した設定と状態は残す)。
/// </summary>
internal sealed class PluginWidgetRegistry
{
    private readonly List<PluginWidgetGroup> _groups = [];

    private PluginWidgetRegistry()
    {
    }

    public static PluginWidgetRegistry Instance { get; } = new();

    /// <summary>ウィジェットのあるプラグイン。並びはプラグインの鍵の順(プラグインの一覧と同じ)。</summary>
    public IReadOnlyList<PluginWidgetGroup> Groups => _groups;

    public event EventHandler<PluginWidgetGroup>? GroupAdded;

    public event EventHandler<PluginWidgetGroup>? GroupRemoved;

    /// <summary>初期化できたプラグインの、検証した定義を受け取る。ウィジェットが1つも無ければ何も足さない。</summary>
    public void Register(
        PluginInfo info,
        IReadOnlyList<PluginWidgetDefinition> definitions,
        PluginCallGuard guard,
        IPluginWidgetProvider provider)
    {
        if (definitions.Count == 0)
        {
            return;
        }

        var group = new PluginWidgetGroup(
            info,
            [.. definitions.Select(definition => new PluginWidgetRegistration(info, definition, guard, provider))]);
        var index = _groups.FindIndex(other => string.Compare(other.PluginKey, info.Key, StringComparison.OrdinalIgnoreCase) > 0);
        _groups.Insert(index < 0 ? _groups.Count : index, group);
        GroupAdded?.Invoke(this, group);
    }

    /// <summary>プラグインを止めた。そのウィジェットのカードを外させる。</summary>
    public void Unregister(string pluginKey)
    {
        var index = _groups.FindIndex(group => string.Equals(group.PluginKey, pluginKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var group = _groups[index];
        _groups.RemoveAt(index);
        GroupRemoved?.Invoke(this, group);
    }
}
