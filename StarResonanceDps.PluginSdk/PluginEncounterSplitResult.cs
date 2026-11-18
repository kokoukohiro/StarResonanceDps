namespace StarResonanceDps.PluginSdk;

/// <summary>How <see cref="IPluginEncounterControlService.SplitAsync"/> ended.</summary>
/// <param name="Outcome">The result.</param>
/// <param name="ClosedEncounterHadRecords">
/// The closed encounter had records, so the host saves it and raises <see cref="IPluginGameDataService.EncounterClosed"/>.
/// An encounter without records is not saved. <c>false</c> when nothing was split.
/// </param>
public sealed record PluginEncounterSplitResult(
    PluginEncounterSplitOutcome Outcome,
    bool ClosedEncounterHadRecords);

/// <summary>The result of <see cref="IPluginEncounterControlService.SplitAsync"/>.</summary>
public enum PluginEncounterSplitOutcome
{
    /// <summary>The host closed the current encounter and created a new one.</summary>
    Split = 0,

    /// <summary>Nothing was done, because a benchmark is running or waiting for its first event.</summary>
    SkippedBenchmark,

    /// <summary>Nothing was done, because the host has stopped the plugin or the app has started to exit.</summary>
    Unavailable
}
