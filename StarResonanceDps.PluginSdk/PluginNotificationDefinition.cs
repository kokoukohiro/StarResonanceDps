namespace StarResonanceDps.PluginSdk;

/// <summary>
/// One item a plugin adds to the host's notification tab (<see cref="IPluginNotificationProvider"/>).
///
/// <para>
/// Localized texts are keyed by culture name (<c>ja-JP</c>, <c>ko-KR</c>, <c>zh-CN</c>, <c>en-US</c>).
/// The host shows the text for the current UI culture, then the text for its language (<c>ja</c>), then <c>en-US</c>, then any text.
/// Blank texts are ignored, and a culture may appear only once (culture names are compared without case).
/// </para>
///
/// <para>
/// The host checks the definitions when the plugin starts and stops the plugin when any of these rules is broken:
/// <see cref="Id"/> follows the ID rules (1 to 128 ASCII letters, digits, <c>.</c>, <c>-</c> or <c>_</c>) and is unique in the plugin (without case);
/// <see cref="Names"/> and <see cref="DefaultTexts"/> have at least one text;
/// each field and setting is valid and their keys and IDs are unique (without case).
/// </para>
/// </summary>
public sealed class PluginNotificationDefinition
{
    /// <summary>The item ID. The user's settings for the item are saved under this ID, so do not change it between versions.</summary>
    public required string Id { get; init; }

    /// <summary>The item name shown in the notification tab.</summary>
    public required IReadOnlyDictionary<string, string> Names { get; init; }

    /// <summary>
    /// The notification text used until the user writes their own. Write a field as <c>{Key}</c> (<see cref="PluginNotificationField.Key"/>).
    /// </summary>
    public required IReadOnlyDictionary<string, string> DefaultTexts { get; init; }

    /// <summary>The values the text can contain. The notification tab lists them so the user can insert them.</summary>
    public IReadOnlyList<PluginNotificationField> Fields { get; init; } = [];

    /// <summary>
    /// The item's own settings, shown above the delivery settings in the notification tab.
    /// Read the values with <see cref="IPluginNotificationService.GetSettings"/>. <see cref="PluginColorSetting"/> is not allowed here.
    /// </summary>
    public IReadOnlyList<PluginSettingDefinition> Settings { get; init; } = [];
}

/// <summary>A value a notification text can contain, written as <c>{Key}</c>.</summary>
public sealed class PluginNotificationField
{
    /// <summary>The key, 1 to 64 ASCII letters and digits. Keys are matched without case.</summary>
    public required string Key { get; init; }

    /// <summary>The name shown in the list of values that can be inserted (at least one text).</summary>
    public required IReadOnlyDictionary<string, string> Names { get; init; }

    /// <summary>The value shown in the notification tab's preview. May be empty.</summary>
    public required IReadOnlyDictionary<string, string> SampleValues { get; init; }
}
