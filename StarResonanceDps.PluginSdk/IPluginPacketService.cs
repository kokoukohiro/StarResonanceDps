namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Reads the game messages the host captures.
/// </summary>
public interface IPluginPacketService
{
    /// <summary>
    /// Returns the captured messages of the given types, in the order they arrived, starting from the first message
    /// that arrives after the enumeration starts. The plugin receives messages only while the enumeration is running:
    /// it starts receiving when <c>await foreach</c> begins and stops when the loop exits.
    /// The enumeration also ends when the host stops the plugin (<see cref="IPluginContext.Stopping"/>).
    /// </summary>
    /// <remarks>
    /// Messages that have not been read yet are kept for the plugin. When they exceed 64 MB for one plugin,
    /// the host stops the plugin. Keep up with the messages or read only the types the plugin needs.
    /// </remarks>
    IAsyncEnumerable<PluginPacketMessage> ReadAsync(
        IReadOnlyCollection<PluginPacketType> types,
        CancellationToken cancellationToken = default);
}
