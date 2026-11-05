using System.Windows.Media;

namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The current values of the settings defined by <see cref="PluginSettingDefinition"/>, read from the user's saved settings.
/// A snapshot: it does not change after it is returned. Each getter throws <see cref="KeyNotFoundException"/> for an ID that is not defined,
/// and <see cref="InvalidOperationException"/> when the setting is of another kind.
/// </summary>
public sealed class PluginSettingValues
{
    private readonly IReadOnlyDictionary<string, object> _values;

    internal PluginSettingValues(IReadOnlyDictionary<string, object> values)
    {
        _values = values;
    }

    /// <summary>The value of a <see cref="PluginSwitchSetting"/>.</summary>
    public bool GetSwitch(string id) => Get<bool>(id, nameof(PluginSwitchSetting));

    /// <summary>The chosen <see cref="PluginChoiceOption.Id"/> of a <see cref="PluginChoiceSetting"/>.</summary>
    public string GetChoice(string id) => Get<PluginChoiceValue>(id, nameof(PluginChoiceSetting)).OptionId;

    /// <summary>The value of a <see cref="PluginNumberSetting"/>.</summary>
    public long GetNumber(string id) => Get<long>(id, nameof(PluginNumberSetting));

    /// <summary>The value of a <see cref="PluginTextSetting"/>.</summary>
    public string GetText(string id) => Get<string>(id, nameof(PluginTextSetting));

    /// <summary>The value of a <see cref="PluginSliderSetting"/>.</summary>
    public double GetSlider(string id) => Get<double>(id, nameof(PluginSliderSetting));

    /// <summary>The chosen color of a <see cref="PluginColorSetting"/> (opaque).</summary>
    public Color GetColor(string id) => Get<Color>(id, nameof(PluginColorSetting));

    private T Get<T>(string id, string kind)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!_values.TryGetValue(id, out var value))
        {
            throw new KeyNotFoundException($"The setting '{id}' is not defined.");
        }

        return value is T typed
            ? typed
            : throw new InvalidOperationException($"The setting '{id}' is not a {kind}.");
    }
}

/// <summary>The value of a choice setting. A separate type so that a text setting is not read as a choice.</summary>
internal sealed record PluginChoiceValue(string OptionId);
