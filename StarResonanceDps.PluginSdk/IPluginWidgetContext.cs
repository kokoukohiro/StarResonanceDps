namespace StarResonanceDps.PluginSdk;

/// <summary>
/// What the host gives one open widget window
/// (<see cref="IPluginWidgetProvider.CreateWidget"/> and <see cref="IPluginMultiWidgetProvider.CreateWidget"/>).
/// </summary>
public interface IPluginWidgetContext
{
    /// <summary>The <see cref="PluginWidgetDefinition.Id"/> of the widget.</summary>
    string WidgetId { get; }

    /// <summary>
    /// The current values of the widget's own settings (<see cref="PluginWidgetDefinition.SettingSections"/>).
    /// While the settings window is open these are the unsaved values the user is trying, so the widget shows them as a preview.
    /// Safe to read from any thread.
    /// </summary>
    PluginSettingValues Settings { get; }

    /// <summary>
    /// Raised on the UI thread after <see cref="Settings"/> has changed: the user changed a value in the settings window (preview), saved,
    /// discarded the changes or reset to the defaults. Not raised after the window has closed.
    /// </summary>
    event EventHandler? SettingsChanged;

    /// <summary>The reasons last passed to <see cref="SetStatusReasons"/>. Safe to read from any thread.</summary>
    PluginWidgetStatusReasons StatusReasons { get; }

    /// <summary>
    /// Tells the host what the widget cannot show now. The window shows these reasons together with what the host checks by
    /// <see cref="PluginWidgetDefinition.StatusRule"/>. Safe to call from any thread, also inside the method that creates the widget.
    /// </summary>
    /// <param name="reasons">The reasons, or <see cref="PluginWidgetStatusReasons.None"/> when nothing is missing.</param>
    /// <exception cref="ArgumentException"><paramref name="reasons"/> has a value that <see cref="PluginWidgetStatusReasons"/> does not define.</exception>
    void SetStatusReasons(PluginWidgetStatusReasons reasons);

    /// <summary>
    /// Whether the window is hidden now (the settings "Hide Widgets While the Game Is Inactive"). Stop updating while it is hidden.
    /// Safe to read from any thread.
    /// </summary>
    bool IsWindowHidden { get; }

    /// <summary>Raised on the UI thread after <see cref="IsWindowHidden"/> has changed. Not raised after the window has closed.</summary>
    event EventHandler? WindowHiddenChanged;
}
