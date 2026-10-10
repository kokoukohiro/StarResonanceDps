using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// ヒール・ダメージ詳細の4つの列の行。行は窓を開いたときに1回だけ作り、更新では文字と表示の有無だけを入れ替える
/// (同じ文字なら通知しないので、値が変わらない間は画面の部品も描画も動かない)。
/// </summary>
public sealed class PlayerMetricSummaryEntry
{
    /// <summary>与ダメのときだけ出す「免疫の数」の行(<see cref="RateLines"/> の最後)。</summary>
    public const int ImmuneLineIndex = 3;

    /// <summary>値が出るまで出さない「毎分の行動回数」の行(<see cref="CastLines"/> の最後)。</summary>
    public const int CastsPerMinuteLineIndex = 3;

    public IReadOnlyList<PlayerMetricSummaryLine> ValueLines { get; } = CreateLines(4);

    public IReadOnlyList<PlayerMetricSummaryLine> RateLines { get; } = CreateLines(4);

    public IReadOnlyList<PlayerMetricSummaryLine> DistributionLines { get; } = CreateLines(3);

    public IReadOnlyList<PlayerMetricSummaryLine> CastLines { get; } = CreateLines(4);

    private static PlayerMetricSummaryLine[] CreateLines(int count)
    {
        var lines = new PlayerMetricSummaryLine[count];
        for (var index = 0; index < count; index++)
        {
            lines[index] = new PlayerMetricSummaryLine();
        }

        return lines;
    }
}

/// <summary>ヒール・ダメージ詳細の1行。</summary>
public sealed partial class PlayerMetricSummaryLine : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>文字を入れて出す。null なら出さない(文字はそのまま残す)。</summary>
    public void Show(string? text)
    {
        if (text is null)
        {
            IsVisible = false;
            return;
        }

        Text = text;
        IsVisible = true;
    }
}
