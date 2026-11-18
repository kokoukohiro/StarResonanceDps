// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 値を渡す(<c>Value</c> の束縛の元へ書く)のを、つまみを手放したときと、ドラッグ以外の操作(クリック・キー・ホイール)で値が変わったときだけにするスライダー。
/// ドラッグ中は打つ前の値のまま(1刻みの値が変わるたびに保存・プレビューすると重いもの)。作っている途中(範囲と値を入れているあいだ)の値の変化は渡さない。
/// 使い方: <c>Value</c> を <c>UpdateSourceTrigger=Explicit</c> で束縛し、<c>ReleaseCommitSliderBehavior.IsEnabled="True"</c> を付ける。
/// 横の数字はスライダーの値(<c>ElementName</c>)を出すと、ドラッグ中もつまみに合わせて動く。
/// 使うのは通知タブの通知音量とプラグインの設定のスライドバー、ウィジェット設定のウィンドウの拡大率。
/// </summary>
public static class ReleaseCommitSliderBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ReleaseCommitSliderBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>つまみをドラッグしている最中か。スライダーごとに持つ。</summary>
    private static readonly DependencyProperty IsDraggingProperty = DependencyProperty.RegisterAttached(
        "IsDragging",
        typeof(bool),
        typeof(ReleaseCommitSliderBehavior),
        new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider)
        {
            return;
        }

        slider.RemoveHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(Slider_DragStarted));
        slider.RemoveHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Slider_DragCompleted));
        slider.ValueChanged -= Slider_ValueChanged;

        if (e.NewValue is true)
        {
            slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(Slider_DragStarted));
            slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Slider_DragCompleted));
            slider.ValueChanged += Slider_ValueChanged;
        }
    }

    private static void Slider_DragStarted(object sender, DragStartedEventArgs e)
    {
        ((Slider)sender).SetValue(IsDraggingProperty, true);
    }

    private static void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        var slider = (Slider)sender;
        slider.SetValue(IsDraggingProperty, false);
        Commit(slider);
    }

    private static void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var slider = (Slider)sender;
        if (!(bool)slider.GetValue(IsDraggingProperty) && slider.IsLoaded)
        {
            Commit(slider);
        }
    }

    private static void Commit(Slider slider)
    {
        slider.GetBindingExpression(RangeBase.ValueProperty)?.UpdateSource();
    }
}
