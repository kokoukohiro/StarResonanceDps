using System;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 行の数が変わったら、窓の高さを全部の行が入る高さに合わせてほしい中身。窓の大きさは窓(<see cref="WidgetWindow"/>)が決め、
/// 中身は合図と足りない高さだけを渡す。手で高さを変えたときは合図を出さない(スクロールバーで見る)。
/// </summary>
public interface IWidgetAutoFitHeightContent
{
    /// <summary>行の数が変わった(中身の全体の高さが変わった)。行が無くなったときは出さない。</summary>
    event EventHandler? FitHeightRequested;

    /// <summary>中身の全体の高さから、いま見えている高さを引いた値。正なら足りない、負なら余っている。</summary>
    double GetHeightShortfall();
}
