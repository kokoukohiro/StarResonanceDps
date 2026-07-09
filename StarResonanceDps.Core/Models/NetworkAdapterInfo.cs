namespace StarResonanceDps.Core.Models;

public sealed record NetworkAdapterInfo(string DeviceName, string Description)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Description)
        ? DeviceName
        : Description;

    public override string ToString()
    {
        return DisplayName;
    }
}
