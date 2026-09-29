using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>取った写真。絵と、届いたままの中身。</summary>
/// <param name="Url">取った URL。保存するときの既定のファイル名と拡張子はここから決める。</param>
/// <param name="Data">届いたままの中身。保存はこれをそのまま書く(拡張子と中身を一致させるため)。</param>
public sealed record PlayerPhoto(string Url, BitmapSource Image, byte[] Data);

/// <summary>
/// プレイヤー情報の顔写真・名刺の写真を取る。URL は通信で届いたものをそのまま使う。
///
/// <para>
/// <b>ファイルには残さない。</b> URL を WPF にそのまま渡すとシステムのキャッシュに残り得るので、
/// メモリに読んでから絵にする。届いたままの中身も絵と一緒に返す(ユーザーが保存を選んだときに書く)。
/// </para>
/// </summary>
public static class PlayerPhotoLoader
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// 写真を取ってメモリの絵にする。https 以外と取得の失敗は null で、ログに警告を1行出す。取り消されたら null。
    /// </summary>
    /// <param name="characterId">ログに出す相手。URL はログに出さない。</param>
    public static async Task<PlayerPhoto?> LoadAsync(string url, long characterId, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            Log.Warning("Player photo URL is not https (character {CharacterId})", characterId);
            return null;
        }

        try
        {
            var bytes = await Client.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return new PlayerPhoto(url, image, bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load player photo (character {CharacterId})", characterId);
            return null;
        }
    }
}
