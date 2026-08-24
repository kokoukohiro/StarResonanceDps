using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerBuffEntry : ObservableObject
{
    public PlayerBuffEntry(PlayerBuffSnapshot snapshot)
    {
        Key = snapshot.Key;
        Uuid = snapshot.Uuid;
        Update(snapshot);
    }

    public string Key { get; private set; }

    public long Uuid { get; private set; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _layerText = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    public void Update(PlayerBuffSnapshot snapshot)
    {
        Key = snapshot.Key;
        Uuid = snapshot.Uuid;
        Name = snapshot.Name ?? string.Empty;
        LayerText = snapshot.Layer > 0 ? snapshot.Layer.ToString() : string.Empty;
        DurationText = FormatDuration(snapshot.RemainingSeconds);
        IconPath = CombatIconResolver.ResolveBuffIcon(snapshot.IconName);
    }

    private static string FormatDuration(double? seconds)
    {
        if (seconds is null)
        {
            return string.Empty;
        }

        var roundedSeconds = Math.Max(0, (int)Math.Ceiling(seconds.Value));
        return roundedSeconds.ToString();
    }


}
