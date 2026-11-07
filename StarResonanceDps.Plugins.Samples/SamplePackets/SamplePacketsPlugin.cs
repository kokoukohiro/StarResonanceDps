using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SamplePackets.SamplePacketsPlugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("en-US", "Sample: Game messages")]
[assembly: PluginDisplayName("ja-JP", "サンプル: ゲームのメッセージ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SamplePackets;

/// <summary>
/// A plugin that runs in the background: it has no window, no widget and no notification.
/// It reads one type of game message and writes its size to the app's log. It does not read the content.
/// </summary>
public sealed class SamplePacketsPlugin : IStarResonancePlugin
{
    // Read only the types the plugin needs: the app stops a plugin that leaves more than 64 MB unread.
    private static readonly PluginPacketType[] MessageTypes =
    [
        new PluginPacketType(PluginPacketKind.Notify, ServiceId: 164931432, MethodId: 1)
    ];

    public void Initialize(IPluginContext context)
    {
        _ = ReadMessagesAsync(context);
    }

    public void Shutdown()
    {
    }

    /// <summary>The loop ends by itself when the app stops the plugin.</summary>
    private static async Task ReadMessagesAsync(IPluginContext context)
    {
        await foreach (var message in context.Packets.ReadAsync(MessageTypes).ConfigureAwait(false))
        {
            context.Logger.Info($"Received a message of {message.Payload.Length} bytes.");
        }
    }
}
