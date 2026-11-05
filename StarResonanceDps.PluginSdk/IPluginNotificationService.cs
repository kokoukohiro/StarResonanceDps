namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Sends the notifications a plugin defined with <see cref="IPluginNotificationProvider"/>, and reads the user's settings for them.
/// The definitions are read after <see cref="IStarResonancePlugin.Initialize"/> returns, so the methods throw <see cref="ArgumentException"/>
/// for every ID while the plugin is initializing, and for an ID the plugin did not define.
/// </summary>
public interface IPluginNotificationService
{
    /// <summary>Whether the user has turned the item on. Safe to call from any thread.</summary>
    bool IsEnabled(string id);

    /// <summary>
    /// Sends the item's notification. Safe to call from any thread; the host delivers it on the UI thread.
    /// Does nothing when the item is off. The text is the user's text, or <see cref="PluginNotificationDefinition.DefaultTexts"/> when they have not written one,
    /// with each <c>{Key}</c> replaced by the value in <paramref name="values"/>. A field without a value becomes empty.
    /// Throws <see cref="ArgumentException"/> when <paramref name="values"/> has a key that is not one of the item's fields.
    /// </summary>
    void Notify(string id, IReadOnlyDictionary<string, string>? values = null);

    /// <summary>The current values of the item's own settings (<see cref="PluginNotificationDefinition.Settings"/>). Safe to call from any thread.</summary>
    PluginSettingValues GetSettings(string id);

    /// <summary>
    /// Raised on the UI thread after the user turned an item on or off or changed its own settings. The value is the item ID.
    /// Changes to the text and to how the notification is delivered are not raised.
    /// </summary>
    event EventHandler<string>? SettingsChanged;
}
