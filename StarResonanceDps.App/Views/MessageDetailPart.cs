using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>
/// メッセージ・確認の窓の詳細の欄の一続き。<see cref="Url"/> があればリンクとして出し、押したら開く。
/// </summary>
public sealed record MessageDetailPart(string Text, string? Url = null);

/// <summary>メッセージ・確認の窓の詳細の欄へ、一続きの文とリンク(<c>Hyperlink.Inline</c>)を入れる。</summary>
internal static class MessageDetailText
{
    public static void Fill(TextBlock target, IReadOnlyList<MessageDetailPart> parts)
    {
        var linkStyle = (Style)target.FindResource("Hyperlink.Inline");
        foreach (var part in parts)
        {
            if (part.Url is null)
            {
                AddText(target.Inlines, part.Text);
                continue;
            }

            var url = part.Url;
            var link = new Hyperlink { Style = linkStyle };
            AddText(link.Inlines, part.Text);
            link.Click += (_, _) => ExternalLinkOpener.Open(url);
            target.Inlines.Add(link);
        }
    }

    /// <summary>改行を <see cref="LineBreak"/> にして足す。</summary>
    private static void AddText(InlineCollection inlines, string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            if (lines[i].Length > 0)
            {
                inlines.Add(new Run(lines[i]));
            }
        }
    }
}
