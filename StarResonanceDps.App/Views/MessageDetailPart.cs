namespace StarResonanceDps.App.Views;

/// <summary>
/// メッセージウィンドウの詳細の欄の一続き。<see cref="Url"/> があればリンクとして出し、押したら開く。
/// </summary>
public sealed record MessageDetailPart(string Text, string? Url = null);
