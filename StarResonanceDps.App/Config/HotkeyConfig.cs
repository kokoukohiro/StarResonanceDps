using System.Text.Json.Serialization;
using System.Windows.Input;

namespace StarResonanceDps.App.Config;

/// <summary>ホットキーで動かす操作。並びは全体設定の行の並び。</summary>
public enum HotkeyAction
{
    /// <summary>1つでも起動中なら「すべてを停止」、無ければ「お気に入りを起動」。</summary>
    StartFavoritesOrStopAll,

    /// <summary>1つでもピン留め中なら「ピン留めをすべて解除」、無ければ「起動中をピン留め」。</summary>
    PinRunningOrUnpinAll,

    /// <summary>1つでもクリック透過中なら「クリック透過をすべて解除」、無ければ「ピン留めをクリック透過」。</summary>
    ClickThroughPinnedOrClearAll,

    /// <summary>
    /// 常に最前面なら「ピン留め時のみ最前面」、そうでなければ「常に最前面」に切り替える。
    /// 全体設定の表示設定「ウィジェットウィンドウ」と同じ値を書き換えて保存する。
    /// </summary>
    WidgetWindowTopmostAlwaysOrPinnedOnly,

    /// <summary>3分計測(計測中なら止める)。集計タブのボタンと同じ。</summary>
    ThreeMinuteBenchmark,

    /// <summary>リセット。集計タブのボタンと同じ。</summary>
    ResetEncounter
}

/// <summary>
/// ホットキーの割り当て。操作ごとに1つ。保存は名前で書く(<c>"F8"</c>、<c>"Control, Shift"</c>)。
/// </summary>
public sealed class HotkeySettingsConfig
{
    public HotkeyBindingConfig StartFavoritesOrStopAll { get; set; } = new();

    public HotkeyBindingConfig PinRunningOrUnpinAll { get; set; } = new();

    public HotkeyBindingConfig ClickThroughPinnedOrClearAll { get; set; } = new();

    public HotkeyBindingConfig WidgetWindowTopmostAlwaysOrPinnedOnly { get; set; } = new();

    public HotkeyBindingConfig ThreeMinuteBenchmark { get; set; } = new();

    public HotkeyBindingConfig ResetEncounter { get; set; } = new();

    public HotkeyBindingConfig Get(HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.StartFavoritesOrStopAll => StartFavoritesOrStopAll,
            HotkeyAction.PinRunningOrUnpinAll => PinRunningOrUnpinAll,
            HotkeyAction.ClickThroughPinnedOrClearAll => ClickThroughPinnedOrClearAll,
            HotkeyAction.WidgetWindowTopmostAlwaysOrPinnedOnly => WidgetWindowTopmostAlwaysOrPinnedOnly,
            HotkeyAction.ThreeMinuteBenchmark => ThreeMinuteBenchmark,
            HotkeyAction.ResetEncounter => ResetEncounter,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    public void Set(HotkeyAction action, HotkeyBindingConfig binding)
    {
        switch (action)
        {
            case HotkeyAction.StartFavoritesOrStopAll:
                StartFavoritesOrStopAll = binding;
                break;
            case HotkeyAction.PinRunningOrUnpinAll:
                PinRunningOrUnpinAll = binding;
                break;
            case HotkeyAction.ClickThroughPinnedOrClearAll:
                ClickThroughPinnedOrClearAll = binding;
                break;
            case HotkeyAction.WidgetWindowTopmostAlwaysOrPinnedOnly:
                WidgetWindowTopmostAlwaysOrPinnedOnly = binding;
                break;
            case HotkeyAction.ThreeMinuteBenchmark:
                ThreeMinuteBenchmark = binding;
                break;
            case HotkeyAction.ResetEncounter:
                ResetEncounter = binding;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    public bool HasSameBindings(HotkeySettingsConfig other)
    {
        return Enum.GetValues<HotkeyAction>().All(action => Get(action).SameAs(other.Get(action)));
    }

    public HotkeySettingsConfig Clone()
    {
        return new HotkeySettingsConfig
        {
            StartFavoritesOrStopAll = StartFavoritesOrStopAll?.Clone() ?? new(),
            PinRunningOrUnpinAll = PinRunningOrUnpinAll?.Clone() ?? new(),
            ClickThroughPinnedOrClearAll = ClickThroughPinnedOrClearAll?.Clone() ?? new(),
            WidgetWindowTopmostAlwaysOrPinnedOnly = WidgetWindowTopmostAlwaysOrPinnedOnly?.Clone() ?? new(),
            ThreeMinuteBenchmark = ThreeMinuteBenchmark?.Clone() ?? new(),
            ResetEncounter = ResetEncounter?.Clone() ?? new()
        };
    }
}

/// <summary>
/// 1つの操作に割り当てたキー。<see cref="Key.None"/> は割り当てなし(全体設定では空欄)。
/// 修飾キーは Ctrl・Alt・Shift だけを持つ。
/// </summary>
public sealed class HotkeyBindingConfig
{
    public const ModifierKeys SupportedModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Key Key { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModifierKeys Modifiers { get; set; }

    [JsonIgnore]
    public bool IsAssigned => Key != Key.None;

    public static HotkeyBindingConfig Create(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        return new HotkeyBindingConfig
        {
            Key = key,
            Modifiers = modifiers & SupportedModifiers
        };
    }

    /// <summary>修飾キーそのもの。主キーにはしない(押しても割り当てない)。</summary>
    public static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
    }

    public bool SameAs(HotkeyBindingConfig other)
    {
        return Key == other.Key && Modifiers == other.Modifiers;
    }

    public HotkeyBindingConfig Clone()
    {
        return new HotkeyBindingConfig
        {
            Key = Key,
            Modifiers = Modifiers
        };
    }
}
