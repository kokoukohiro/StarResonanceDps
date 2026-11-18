namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The limits of the data one plugin stores with one encounter (<see cref="IPluginHistoryService"/>).
/// </summary>
public static class PluginEncounterDataLimits
{
    /// <summary>The number of keys.</summary>
    public const int MaxKeys = 32;

    /// <summary>The total length of the data under all keys, in bytes (4 MiB).</summary>
    public const int MaxTotalBytes = 4 * 1024 * 1024;
}
