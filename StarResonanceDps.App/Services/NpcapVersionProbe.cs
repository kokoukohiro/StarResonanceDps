using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace StarResonanceDps.App.Services;

public static partial class NpcapVersionProbe
{
    public static string GetVersionString()
    {
        try
        {
            var pointer = pcap_lib_version();
            return Marshal.PtrToStringAnsi(pointer) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static Version GetVersion()
    {
        var versionString = GetVersionString();
        if (string.IsNullOrWhiteSpace(versionString))
        {
            return new Version();
        }

        var match = NpcapVersionRegex().Match(versionString);
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version)
            ? version
            : new Version();
    }

    [GeneratedRegex(@"\w+ version (\d+(?:\.\d+)+)", RegexOptions.IgnoreCase)]
    private static partial Regex NpcapVersionRegex();

    [DllImport("wpcap.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pcap_lib_version();
}
