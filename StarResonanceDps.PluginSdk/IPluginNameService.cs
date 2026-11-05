namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Returns the names the app shows for game IDs, in the current UI culture
/// (<see cref="IPluginLocalizationService.CurrentCulture"/>). Each method returns <c>null</c> when the name is unknown.
/// Safe to call from any thread.
/// </summary>
public interface IPluginNameService
{
    string? GetSkillName(int skillId);

    string? GetBuffName(int buffId);

    string? GetMonsterName(int monsterId);

    /// <summary>The name of a spec. The ID is the spec ID (profession ID × 10000 + spec number).</summary>
    string? GetClassSpecName(int specId);

    /// <summary>The name of a scene. With a dungeon difficulty above 0, the difficulty name is appended.</summary>
    string? GetSceneName(long sceneId, int dungeonDifficulty = 0);

    /// <summary>The name of an item. Equipment IDs are item IDs.</summary>
    string? GetItemName(int itemId);
}
