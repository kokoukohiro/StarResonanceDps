using System.Text.Json.Serialization;

namespace StarResonanceDps.Plugins.KeybindTool.Models;

internal enum KeybindModeGroup
{
    Main,
    Photo,
    Fishing
}

internal interface IKeybindActionDefinition
{
    string Name { get; }
}

internal sealed record ControllerInputOption(uint Value, string Label);

internal sealed record KeyMouseInputOption(uint InputType, uint Value, string Label);

internal sealed record HelperBindingOption(uint MainValue, string Label);

internal sealed record ActionHelperOption(uint StateValue, string Label);

internal sealed record PresetOption(uint Value, string Label);

internal sealed record ControllerActionDefinition(
    string Name,
    IReadOnlyList<int> RelativeOffsets,
    IReadOnlyList<uint> AllowedValues,
    bool HasExplicitAllowedValues,
    bool UsesHelper) : IKeybindActionDefinition;

internal sealed record KeyMouseActionDefinition(
    string Name,
    IReadOnlyList<int> RelativeOffsets,
    IReadOnlyList<uint> AllowedInputTypes) : IKeybindActionDefinition;

internal sealed class KeybindLayoutConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 4;

    [JsonPropertyName("input_device")]
    public string InputDevice { get; set; } = string.Empty;

    [JsonPropertyName("controller_profile")]
    public ControllerLayoutProfile ControllerProfile { get; set; } = new();

    [JsonPropertyName("keymouse_profile")]
    public KeyMouseLayoutProfile KeyMouseProfile { get; set; } = new();
}

internal sealed class ControllerLayoutProfile
{
    [JsonPropertyName("controller_type")]
    public string ControllerType { get; set; } = string.Empty;

    [JsonPropertyName("photo_mode_independent")]
    public bool PhotoModeIndependent { get; set; }

    [JsonPropertyName("keybind")]
    public ControllerKeybindProfile Keybind { get; set; } = new();

    [JsonPropertyName("actions")]
    public Dictionary<string, ControllerActionLayout> Actions { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class ControllerKeybindProfile
{
    [JsonPropertyName("helper1")]
    public string Helper1 { get; set; } = string.Empty;

    [JsonPropertyName("helper2")]
    public string Helper2 { get; set; } = string.Empty;

    [JsonPropertyName("preset")]
    public string Preset { get; set; } = string.Empty;
}

internal sealed class ControllerActionLayout
{
    [JsonPropertyName("helper")]
    public string Helper { get; set; } = string.Empty;

    [JsonPropertyName("button")]
    public string Button { get; set; } = string.Empty;
}

internal sealed class KeyMouseLayoutProfile
{
    [JsonPropertyName("photo_mode_independent")]
    public bool PhotoModeIndependent { get; set; }

    [JsonPropertyName("fishing_mode_independent")]
    public bool FishingModeIndependent { get; set; }

    [JsonPropertyName("actions")]
    public Dictionary<string, KeyMouseActionLayout> Actions { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class KeyMouseActionLayout
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}

internal sealed record DetectedSaveFile(string DisplayName, string FilePath)
{
    public override string ToString() => DisplayName;
}
