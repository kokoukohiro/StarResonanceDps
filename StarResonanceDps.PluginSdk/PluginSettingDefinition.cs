namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One setting a plugin adds to a host screen (for example a notification item, <see cref="PluginNotificationDefinition.Settings"/>).
/// The host draws the row with the same look as its own settings and saves the value. Use one of the derived types.
///
/// <para>
/// Localized texts follow the rules of <see cref="PluginNotificationDefinition"/>.
/// <see cref="Id"/> follows the plugin ID rules (1 to 128 ASCII letters, digits, <c>.</c>, <c>-</c> or <c>_</c>) and is unique among the settings of the item (without case).
/// The saved value is kept under this ID, so do not change it between versions.
/// When a saved value no longer fits the definition (an option was removed, the range changed), the host uses the default or the nearest end of the range.
/// </para>
/// </summary>
public abstract class PluginSettingDefinition
{
    private protected PluginSettingDefinition()
    {
    }

    public required string Id { get; init; }

    /// <summary>The row name (at least one text).</summary>
    public required IReadOnlyDictionary<string, string> Names { get; init; }

    /// <summary>A note shown under the row, or <c>null</c> for none.</summary>
    public IReadOnlyDictionary<string, string>? Notes { get; init; }
}

/// <summary>An on/off switch. Read with <see cref="PluginSettingValues.GetSwitch"/>.</summary>
public sealed class PluginSwitchSetting : PluginSettingDefinition
{
    public bool DefaultValue { get; init; }
}

/// <summary>A drop-down list. Read the chosen <see cref="PluginChoiceOption.Id"/> with <see cref="PluginSettingValues.GetChoice"/>.</summary>
public sealed class PluginChoiceSetting : PluginSettingDefinition
{
    /// <summary>The options in list order (at least one). Option IDs follow the plugin ID rules and are unique (without case).</summary>
    public required IReadOnlyList<PluginChoiceOption> Options { get; init; }

    /// <summary>The ID of the option chosen until the user changes it. Must be one of <see cref="Options"/>.</summary>
    public required string DefaultOptionId { get; init; }
}

/// <summary>An option of a <see cref="PluginChoiceSetting"/>.</summary>
public sealed class PluginChoiceOption
{
    public required string Id { get; init; }

    /// <summary>The option name (at least one text).</summary>
    public required IReadOnlyDictionary<string, string> Names { get; init; }
}

/// <summary>
/// A box that takes a whole number. The box accepts digits only, so the range cannot be negative.
/// The value is applied when the user presses Enter or leaves the box; a value outside the range becomes the nearest end, and an empty box goes back to the previous value.
/// Read with <see cref="PluginSettingValues.GetNumber"/>.
/// </summary>
public sealed class PluginNumberSetting : PluginSettingDefinition
{
    /// <summary>At least 0.</summary>
    public required long Minimum { get; init; }

    /// <summary>At least <see cref="Minimum"/>.</summary>
    public required long Maximum { get; init; }

    /// <summary>Between <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    public required long DefaultValue { get; init; }
}

/// <summary>A one-line text box. Read with <see cref="PluginSettingValues.GetText"/>.</summary>
public sealed class PluginTextSetting : PluginSettingDefinition
{
    public string DefaultValue { get; init; } = string.Empty;
}

/// <summary>
/// A color, chosen from up to 5 color slots or with the color picker (like the window color of the theme).
/// Widget settings only: a notification item with a color setting is invalid.
/// Read the chosen color with <see cref="PluginSettingValues.GetColor"/>.
/// </summary>
public sealed class PluginColorSetting : PluginSettingDefinition
{
    /// <summary>
    /// The colors in the slots until the user changes them, as <c>#RRGGBB</c> (1 to 5, no duplicates). The first one is chosen.
    /// </summary>
    public required IReadOnlyList<string> DefaultColors { get; init; }
}

/// <summary>
/// A slider with the value shown on its right. The value moves in steps of <see cref="Step"/> from <see cref="Minimum"/>,
/// and is applied when the user releases the thumb. Read with <see cref="PluginSettingValues.GetSlider"/>.
/// </summary>
public sealed class PluginSliderSetting : PluginSettingDefinition
{
    /// <summary>A finite number below <see cref="Maximum"/>.</summary>
    public required double Minimum { get; init; }

    /// <summary>A finite number above <see cref="Minimum"/>.</summary>
    public required double Maximum { get; init; }

    /// <summary>Between <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    public required double DefaultValue { get; init; }

    /// <summary>A finite number above 0.</summary>
    public double Step { get; init; } = 1;
}
