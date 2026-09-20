using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 色を指定した文字。<see cref="TextSegmentsBehavior"/> が <see cref="Run.Foreground"/> に当てる。
/// 色を持たない素の文字列は <see cref="TextBlock"/> の色のまま出る。
/// </summary>
/// <param name="ToolTipText">
/// 付けるなら、その文字にも TIPS が出る。見た目は
/// <see cref="TextSegmentsBehavior.RunToolTipStyleProperty"/> と
/// <see cref="TextSegmentsBehavior.RunToolTipTextStyleProperty"/> で決める。
/// </param>
public sealed record TextRunSegment(string Text, Brush Foreground, string? ToolTipText = null);

/// <summary>
/// 文字とアイコンを混ぜた1行を <see cref="TextBlock"/> に流し込む。
///
/// <para>
/// 文字列は <see cref="Run"/>、それ以外は <see cref="InlineUIContainer"/> の中の <see cref="ContentPresenter"/> になり、
/// 型ごとの <see cref="DataTemplate"/>(<c>DataType</c> 指定)で描かれる。TextBlock のまま組むので、文字の省略表示が効く。
/// </para>
///
/// <para>
/// <b><see cref="Run"/> の TIPS は、この <see cref="TextBlock"/> を <c>PlacementTarget</c> として開く</b>
/// (<see cref="Run"/> は <see cref="UIElement"/> ではないので、ホストする <see cref="TextBlock"/> が入る)。
/// TIPS のスタイルが <c>PlacementTarget</c> から値を引くなら、この <see cref="TextBlock"/> に載せること。
/// </para>
/// </summary>
public static class TextSegmentsBehavior
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.RegisterAttached(
        "Segments",
        typeof(IEnumerable),
        typeof(TextSegmentsBehavior),
        new PropertyMetadata(null, OnInputChanged));

    /// <summary><see cref="Run"/> に付ける TIPS 自体のスタイル。</summary>
    public static readonly DependencyProperty RunToolTipStyleProperty = DependencyProperty.RegisterAttached(
        "RunToolTipStyle",
        typeof(Style),
        typeof(TextSegmentsBehavior),
        new PropertyMetadata(null, OnInputChanged));

    /// <summary><see cref="Run"/> に付ける TIPS の中の文字のスタイル。</summary>
    public static readonly DependencyProperty RunToolTipTextStyleProperty = DependencyProperty.RegisterAttached(
        "RunToolTipTextStyle",
        typeof(Style),
        typeof(TextSegmentsBehavior),
        new PropertyMetadata(null, OnInputChanged));

    public static IEnumerable? GetSegments(DependencyObject obj)
    {
        return (IEnumerable?)obj.GetValue(SegmentsProperty);
    }

    public static void SetSegments(DependencyObject obj, IEnumerable? value)
    {
        obj.SetValue(SegmentsProperty, value);
    }

    public static Style? GetRunToolTipStyle(DependencyObject obj)
    {
        return (Style?)obj.GetValue(RunToolTipStyleProperty);
    }

    public static void SetRunToolTipStyle(DependencyObject obj, Style? value)
    {
        obj.SetValue(RunToolTipStyleProperty, value);
    }

    public static Style? GetRunToolTipTextStyle(DependencyObject obj)
    {
        return (Style?)obj.GetValue(RunToolTipTextStyleProperty);
    }

    public static void SetRunToolTipTextStyle(DependencyObject obj, Style? value)
    {
        obj.SetValue(RunToolTipTextStyleProperty, value);
    }

    /// <summary>
    /// 3つの入力のどれが変わっても組み直す。XAML の属性の並び順で
    /// 「TIPS のスタイルより先に <see cref="SegmentsProperty"/> が入る」ことがあるため。
    /// </summary>
    private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            throw new InvalidOperationException($"{nameof(TextSegmentsBehavior)} can only be attached to a TextBlock.");
        }

        textBlock.Inlines.Clear();
        if (GetSegments(textBlock) is not IEnumerable segments)
        {
            return;
        }

        foreach (var segment in segments)
        {
            if (segment is string text)
            {
                textBlock.Inlines.Add(new Run(text));
                continue;
            }

            if (segment is TextRunSegment run)
            {
                var inline = new Run(run.Text) { Foreground = run.Foreground };
                if (run.ToolTipText is { } toolTipText)
                {
                    inline.ToolTip = CreateToolTip(textBlock, toolTipText);
                }

                textBlock.Inlines.Add(inline);
                continue;
            }

            textBlock.Inlines.Add(new InlineUIContainer(new ContentPresenter { Content = segment })
            {
                BaselineAlignment = BaselineAlignment.Center
            });
        }
    }

    /// <summary>TIPS は <see cref="Run"/> ごとに作る。1つの実体を複数の <see cref="Run"/> へ付けられないため。</summary>
    private static ToolTip CreateToolTip(TextBlock owner, string text)
    {
        var content = new TextBlock { Text = text };
        if (GetRunToolTipTextStyle(owner) is { } contentStyle)
        {
            content.Style = contentStyle;
        }

        var toolTip = new ToolTip { Content = content };
        if (GetRunToolTipStyle(owner) is { } toolTipStyle)
        {
            toolTip.Style = toolTipStyle;
        }

        return toolTip;
    }
}
