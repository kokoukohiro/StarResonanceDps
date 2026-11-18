This page explains how to add cards to "[Widgets](Widgets)" and make the content of widget windows with a plugin. What every plugin needs is described in [Plugin Development](Plugin-Development).

## Two forms

| Form | Interface the entry class implements | Windows |
|---|---|---|
| Widget with a single window | `IPluginWidgetProvider` | One per widget |
| Widget that opens a window per target | `IPluginMultiWidgetProvider` | One per target. The target is chosen in the drop-down list on the card |

- One plugin can implement both. Make the widget `Id` values unique across both. The cards are listed with the single-window form first and then the per-target form, each in the order of the definitions.
- A plugin without an `IPluginWindowProvider` window does not need `PluginGenre`.

| What the app provides | What the plugin provides |
|---|---|
| The card (Favorite, Pin, Click-Through, Settings, and Start), the window frame and header (the state circle, the title, and close), "Window" and "Theme" in the settings window, and saving the position and size | The widget definitions (the name, the default size, whether there is a footer, and the setting sections) and the window content (the body, the footer, and the header buttons) |

## Widget with a single window

```csharp
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(MyPlugin.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "私のプラグイン")]
[assembly: PluginAuthor("YourName")]
[assembly: PluginVersion("1.0.0")]

namespace MyPlugin;

public sealed class Plugin : IStarResonancePlugin, IPluginWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions()
    {
        return
        [
            new PluginWidgetDefinition
            {
                Id = "hello",
                Names = new Dictionary<string, string> { ["ja-JP"] = "あいさつ", ["en-US"] = "Hello" },
                DefaultWidth = 240,
                DefaultHeight = 80,
            },
        ];
    }

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context)
    {
        var text = new TextBlock { Text = "Hello", Margin = new Thickness(8) };
        text.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Text.WidgetWindow);
        return new PluginWidgetContent { Body = text };
    }

    public void Shutdown()
    {
    }
}
```

| Member | When it is called | What it does |
|---|---|---|
| `GetWidgetDefinitions` | Once, right after `Initialize` | Returns the list of definitions of the widgets to add. The cards are listed in the order returned. Changes made later are not reflected |
| `CreateWidget` | Every time a window opens (UI thread) | Returns the content of the window. `widgetId` is the `Id` of the definition |

### Definition (`PluginWidgetDefinition`)

| Property | Required | Contents |
|---|---|---|
| `Id` | Required | The widget ID: 1 to 128 characters of letters, digits, ".", "-", and "_". It is a key for saving, so do not change it between versions |
| `Names` | Required | The name shown on the card, in the window title, and in the title of the settings window |
| `DefaultWidth`, `DefaultHeight` | Required | The window size when it is opened for the first time. The minimum is 240×60, and the height includes the header (30) and the footer (30) |
| `HasFooter` | Optional (default: none) | Whether to create a footer row |
| `ScrollsVertically` | Optional (default: no) | Whether the body scrolls with a vertical scroll bar |
| `SettingSections` | Optional | The sections added to the settings window ("Settings" below) |
| `StatusRule` | Optional (default: `None`) | What the app checks for the state circle ("State circle" below) |
| `UsesWidgetColors` | Optional (default: no) | Whether `Brush.*` inside the window use the widget colors ("The app's appearance keys" below) |

- Write text for each language, like `Names`, as a dictionary keyed by the language name (such as `ja-JP`). The text is chosen the same way as for the attributes ("Entry class and attributes" in [Plugin Development](Plugin-Development)).
- Only the widget's name appears on the card. The plugin's name and icon do not appear.
- Once a window size has been saved, `DefaultWidth` and `DefaultHeight` are not used.

### Content (`PluginWidgetContent`)

| Property | Required | Contents |
|---|---|---|
| `Body` | Required | The body of the window |
| `Footer` | Required when `HasFooter` is set; otherwise `null` | The footer. It is 30 high and uses the full width of the window |
| `HeaderButtons` | Optional | Text buttons in the header (`PluginWidgetHeaderButton`). They are placed to the right of the title and to the left of the close button |

- When `ScrollsVertically` is `false`, the body uses all the space between the header and the footer. When it is `true`, the body stretches to the width of the window, its height is the height the body wants, and a vertical scroll bar appears when it does not fit.
- The `DataContext` of the body and the footer is `null`. If you use bindings, set it yourself.
- The window does not set a text color. To show the white text and shadow of widgets, apply the `Text.WidgetWindow` style.
- "Window Opacity" in the settings applies to the window background and frame; the body and footer do not become lighter.
- Changing `Text` of a header button changes its text, and clicking it raises `Click` on the UI thread. The buttons stay as in the list given when the content was created; none are added or removed. While the header is hidden by "Hide Header While Pinned" in the settings, the buttons are hidden too.
- If the body or footer is `IDisposable`, `Dispose` is called after the window is closed. Unsubscribe from events and stop timers there.

### What you receive when creating content (`IPluginWidgetContext`)

| Member | Contents |
|---|---|
| `WidgetId` | The `Id` of the definition |
| `Settings` | The current setting values of this widget ("Settings" below) |
| `SettingsChanged` | An event raised on the UI thread when the settings change |
| `StatusReasons`, `SetStatusReasons` | The reasons shown with the state circle ("State circle" below) |
| `IsWindowHidden`, `WindowHiddenChanged` | Whether the window is hidden ("While the window is hidden" below) |

- One is created for each window. After the window is closed, `SettingsChanged` no longer arrives.
- No event arrives just because the window opened, so read the initial values from `Settings` in `CreateWidget`.

### State circle

The circle to the left of the window title (green, yellow, or red) and its tooltip show why the widget cannot show something.

- With `StatusRule` in the definition, choose what the app checks. `None` checks nothing. `Encounter` is the same as the Timeline and is for widgets that show the battle the app shows (including history). `Live` is for widgets that always show the current values.
- With `SetStatusReasons`, pass why the widget cannot show something. The circle and the tooltip show it together with what the app checked. It can be called from any thread, also in `CreateWidget`.
- The reasons are `MapOrRelogin` (cannot be received until you move to another map or log in again, red), `Unreadable` (the saved data cannot be read, red), `PartyScreen`, `PlayerClick`, `PlayerApproach`, and `EnemyApproach` (yellow).

### While the window is hidden

- While the window is hidden by "Hide widgets when the game is inactive" in Settings, `IsWindowHidden` is `true`, and `WindowHiddenChanged` arrives on the UI thread when it changes. Stop periodic updates while it is hidden.

## Settings

When you write sections in `SettingSections` of the definition, they appear between "Window" and "Theme" in the widget's settings window.

```csharp
SettingSections =
[
    new PluginWidgetSettingSection
    {
        Names = new Dictionary<string, string> { ["ja-JP"] = "表示", ["en-US"] = "Display" },
        Icon = PluginResourceKeys.Icons.Settings,
        IconStyle = PluginResourceKeys.Icons.FillStyle,
        Settings =
        [
            new PluginSwitchSetting
            {
                Id = "showTime",
                Names = new Dictionary<string, string> { ["ja-JP"] = "時刻を表示", ["en-US"] = "Show time" },
                DefaultValue = true,
            },
        ],
    },
],
```

```csharp
bool showTime = context.Settings.GetSwitch("showTime");
```

| Section (`PluginWidgetSettingSection`) property | Contents |
|---|---|
| `Names` | The name of the section |
| `Icon` | The icon in the list on the left. A shape key in `PluginResourceKeys.Icons` (such as `Icon.Settings`) |
| `IconStyle` | How the icon is drawn: `Icon.Stroke` (line) or `Icon.Fill` (fill) |
| `Settings` | The list of rows (one or more) |

Row types (each requires `Id` and `Names`, and you can add notes with `Notes`):

| Type | On screen | How to read it |
|---|---|---|
| `PluginSwitchSetting` | A switch | `GetSwitch(id)` → `bool` |
| `PluginChoiceSetting` | A drop-down list. Choose from the `PluginChoiceOption` items in `Options`; the default is `DefaultOptionId` | `GetChoice(id)` → the `Id` of the chosen option |
| `PluginNumberSetting` | A number field. An integer of 0 or more from `Minimum` to `Maximum` | `GetNumber(id)` → `long` |
| `PluginTextSetting` | A one-line text field | `GetText(id)` → `string` |
| `PluginSliderSetting` | A slider from `Minimum` to `Maximum` in steps of `Step` | `GetSlider(id)` → `double` |
| `PluginColorSetting` | Color circles and "Select color". One to five `#RRGGBB` values in `DefaultColors` | `GetColor(id)` → `Color` |

- When the value changes: switches and colors when clicked, drop-down lists when an option is chosen, numbers when confirmed (Enter or leaving the field), text with each character, and sliders while being dragged.
- While the settings window is open, a changed value goes into `Settings` right away and `SettingsChanged` arrives (a preview before saving). It also arrives when "Reset" or "Save" is clicked, and when the window is closed without saving and the values return to the previous ones. It arrives even if the values are the same.
- The app saves the values in `Data\WidgetSettings.json` (separate from the settings files in the plugin's folder). They are removed when the plugin is deleted and kept when it is updated.
- If a saved value cannot be read, or its option no longer exists, the default value is used. A value outside the range becomes the nearer end of the range.
- The `Id` of a row is a key for saving, so do not change it between versions. Make it unique within the widget across sections.
- Reading an `id` that is not in the definition throws `KeyNotFoundException`, and reading it with the method for a different type throws `InvalidOperationException`.
- No matter how many windows of the same widget are open, there is one set of settings.

## Widget that opens a window per target

```csharp
public sealed class Plugin : IStarResonancePlugin, IPluginMultiWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public IReadOnlyList<PluginWidgetDefinition> GetMultiWidgetDefinitions()
    {
        return
        [
            new PluginWidgetDefinition
            {
                Id = "slot",
                Names = new Dictionary<string, string> { ["ja-JP"] = "枠", ["en-US"] = "Slot" },
                DefaultWidth = 240,
                DefaultHeight = 80,
            },
        ];
    }

    public IReadOnlyList<PluginWidgetTarget> GetWidgetTargets(string widgetId)
    {
        return
        [
            new PluginWidgetTarget { Id = "a", Names = new Dictionary<string, string> { ["ja-JP"] = "A" }, IsDefault = true },
            new PluginWidgetTarget { Id = "b", Names = new Dictionary<string, string> { ["ja-JP"] = "B" } },
        ];
    }

    public PluginWidgetContent CreateWidget(string widgetId, string targetId, IPluginWidgetContext context)
    {
        var text = new TextBlock { Text = targetId, Margin = new Thickness(8) };
        text.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Text.WidgetWindow);
        return new PluginWidgetContent { Body = text };
    }

    public void Shutdown()
    {
    }
}
```

| Member | When it is called | What it does |
|---|---|---|
| `GetMultiWidgetDefinitions` | Once, right after `Initialize` | Returns the list of definitions |
| `GetWidgetTargets` | When the drop-down list on the card opens, and while the default target is being chosen again (UI thread) | Returns the list of targets |
| `CreateWidget` | Every time a target's window opens (UI thread) | Returns the content of that target's window |

- A target (`PluginWidgetTarget`) has `Id` (1 to 128 characters of letters, digits, ".", "-", and "_", unique in the list), `Names`, and `IsDefault`.
- Until the user chooses one, the target with `IsDefault` is selected, or the first one in the list if there is none. If a target with `IsDefault` appears later, the selection changes to it.
- Choosing in the drop-down list does not open a window. The start button opens or closes the window of the chosen target. There is one window per target.
- When the list is empty, the start button makes the app show an empty window (the plugin is not called). When a target appears in the list later, content is put into that window.
- A window does not close when its target disappears from the list.
- If the app is closed while a window is running, the window for the same target opens again at the next startup. `CreateWidget` is called with the saved ID even if the target is not in the list.
- Each window has its own target, position and size, and content. The settings, theme, favorite, pin, and click-through are shared by all windows of the widget.
- `GetWidgetTargets` can be called more than once per second, so do not do heavy work in it.
- If it always returns an empty list, the build stops (SRDPS013). A `return` that returns an empty list temporarily is fine if there is another `return` that returns targets.

## Opening from code

With `IPluginContext.Widgets`, open windows of your own widgets and of the app's widgets.

| Method | What it opens |
|---|---|
| `OpenAsync` | Your own widget. For a widget that opens a window per target, also pass the target's ID |
| `OpenHostAsync` | The app's widgets with one window (DPS Meter, HPS Meter, Threat Meter, Break Meter, Timeline, Player List, Entity List, Revive List, Damage Taken Log, Map, and Target Info), and widgets that show only the user (Status, and Buff & Debuff Group) |
| `OpenHostForPlayerAsync` | An app widget that shows a player, for the player with the character ID |
| `OpenHostForEntityAsync` | The app's Target Info, Buff List, or Debuff List, for an enemy that is nearby |

- The methods can be called from any thread. After the window is opened (or brought to the front if it is already open), the `Task` ends with `true`. When it cannot be opened, for example because there is no card or the enemy is not nearby, it ends with `false`.
- The window is activated only while you are using the app's windows. While you are playing the game, the focus is not taken from the game.
- In `Initialize`, the app has not read the widget definitions yet, so your own widgets cannot be opened.
- A combination of a kind and a method that do not fit throws `ArgumentException`.

## The app's appearance keys

- `Text.WidgetWindow` (white text and shadow) and `Text.WidgetWindowSubtitle` (the same text at size 13) are text styles for the dark background of widgets. They stay white even if the widget color is changed.
- `Brush.*` and the styles that use them (such as `Button.Text`) are drawn in the colors of the app's overall theme. With `UsesWidgetColors` set to `true` in the definition, they are drawn in the widget colors (in the app's theme while a background image is used).
- With `ScrollViewer.WidgetWindow`, the scroll bar color follows the widget's color and opacity. It works only inside a widget window.
- With `ToolTip.WidgetWindowInfo`, the background and border of the tooltip use the widget's colors. The colors are not applied unless the `Tag` of the element with the tooltip holds the `DataContext` of the widget window (`Window`).
- The list of keys is in "The app's appearance keys" in [Plugin Development (Window)](Plugin-Development-(Window)).

## Errors and exceptions

- A mistake in a definition (the form of an ID or a duplicate, the size, the icon, the range of a setting value, and so on) stops the plugin, and its row in "[Plugins](Plugins)" shows "Error". The "Plugin Error" window shows "Could not be initialized." The reason is written to the log in English.
- A mistake in the content (`Body` is `null`, or `HasFooter` and `Footer` do not match) or an exception in `CreateWidget` stops the plugin, and that window does not open, not even at the next startup.
- An exception in `SettingsChanged`, in `Click` of a header button, in `Dispose`, or in `GetWidgetTargets` also stops the plugin.
- Passing a value not defined to `SetStatusReasons`, or an ID not defined to `OpenAsync`, throws `ArgumentException`.
- When the plugin is stopped, its open windows close. The window state is kept, so they open again at the next startup.
- If required text such as a name is empty, the build stops (SRDPS014).

## Samples

| Sample | What it shows |
|---|---|
| [SampleWidget](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleWidget) | Two single-window widgets (one with a footer, scrolling, all six setting types, header buttons, the state circle, and the widget colors, and a minimal one) and a `.png` icon |
| [SampleMultiWidget](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleMultiWidget) | Two per-target widgets (one with fixed targets, and one with the players around you that uses yourself as the default), header buttons that open windows from code (another target of your own widget, and the app's Player Info), and the state circle with `Live` |
| [SampleCombined](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleCombined) | A widget that sends a notification from a header button |
