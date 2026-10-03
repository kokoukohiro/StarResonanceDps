using System.Diagnostics;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>アプリの外のリンクを既定のブラウザで開く。開けなかったらログに残す。</summary>
internal static class ExternalLinkOpener
{
    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not open {Url}", url);
        }
    }
}
