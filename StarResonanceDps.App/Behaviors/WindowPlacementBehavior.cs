// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Behaviors;

public static class WindowPlacementBehavior
{
    public static readonly DependencyProperty UseAppConfigStartUpStateProperty = DependencyProperty.RegisterAttached(
        "UseAppConfigStartUpState",
        typeof(bool),
        typeof(WindowPlacementBehavior),
        new PropertyMetadata(false, OnUseAppConfigStartUpStateChanged));

    public static bool GetUseAppConfigStartUpState(DependencyObject obj)
    {
        return (bool)obj.GetValue(UseAppConfigStartUpStateProperty);
    }

    public static void SetUseAppConfigStartUpState(DependencyObject obj, bool value)
    {
        obj.SetValue(UseAppConfigStartUpStateProperty, value);
    }

    /// <summary>保存した位置の無い窓を置く、持ち主の窓(メイン画面)の左上からの横の距離。<see cref="PlaceNearOwner"/> が読む。</summary>
    public static readonly DependencyProperty OwnerOffsetXProperty = DependencyProperty.RegisterAttached(
        "OwnerOffsetX",
        typeof(double),
        typeof(WindowPlacementBehavior),
        new PropertyMetadata(0d));

    /// <summary>保存した位置の無い窓を置く、持ち主の窓(メイン画面)の左上からの縦の距離。<see cref="PlaceNearOwner"/> が読む。</summary>
    public static readonly DependencyProperty OwnerOffsetYProperty = DependencyProperty.RegisterAttached(
        "OwnerOffsetY",
        typeof(double),
        typeof(WindowPlacementBehavior),
        new PropertyMetadata(0d));

    /// <summary>
    /// 置く場所に同じ種類の窓があるとき、右下へずらす量。0 ならずらさない(<see cref="MoveToFreeSpot"/>)。
    /// </summary>
    public static readonly DependencyProperty CascadeStepProperty = DependencyProperty.RegisterAttached(
        "CascadeStep",
        typeof(double),
        typeof(WindowPlacementBehavior),
        new PropertyMetadata(0d));

    public static double GetOwnerOffsetX(DependencyObject obj)
    {
        return (double)obj.GetValue(OwnerOffsetXProperty);
    }

    public static void SetOwnerOffsetX(DependencyObject obj, double value)
    {
        obj.SetValue(OwnerOffsetXProperty, value);
    }

    public static double GetOwnerOffsetY(DependencyObject obj)
    {
        return (double)obj.GetValue(OwnerOffsetYProperty);
    }

    public static void SetOwnerOffsetY(DependencyObject obj, double value)
    {
        obj.SetValue(OwnerOffsetYProperty, value);
    }

    public static double GetCascadeStep(DependencyObject obj)
    {
        return (double)obj.GetValue(CascadeStepProperty);
    }

    public static void SetCascadeStep(DependencyObject obj, double value)
    {
        obj.SetValue(CascadeStepProperty, value);
    }

    /// <summary>
    /// 保存した位置の無い窓を、持ち主の窓の左上から <see cref="OwnerOffsetXProperty"/>・<see cref="OwnerOffsetYProperty"/> の場所に置き、
    /// そこに同じ種類の窓があればずらす(<see cref="MoveToFreeSpot"/>)。持ち主が無ければ画面の中央。
    /// 窓を見せる前(コンストラクタ)に呼ぶ。見せないまま位置を保存する窓(ゲームが前面でない間に開いたウィジェット)があるため。
    /// </summary>
    public static void PlaceNearOwner(Window window, Window? owner)
    {
        if (owner is null)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = owner.Left + GetOwnerOffsetX(window);
        window.Top = owner.Top + GetOwnerOffsetY(window);
        MoveToFreeSpot(window);
    }

    /// <summary>
    /// 今の位置に同じ種類のほかの窓の左上がそのままあれば、空くまで <see cref="CascadeStepProperty"/> ずつ右下へずらす。
    /// 左上の座標が完全に一致するときだけずらす(Windows と同じ)ので、開いた後に一度も動かしていない窓だけが場所を塞ぐ。
    /// 数えるのはアプリの窓の一覧にある同じ型の窓で、まだ見せていない窓・隠している窓・最小化した窓も入る。
    /// </summary>
    public static void MoveToFreeSpot(Window window)
    {
        var step = GetCascadeStep(window);
        if (step <= 0 || !double.IsFinite(window.Left) || !double.IsFinite(window.Top))
        {
            return;
        }

        while (IsOccupied(window))
        {
            window.Left += step;
            window.Top += step;
        }
    }

    private static bool IsOccupied(Window window)
    {
        foreach (Window other in Application.Current.Windows)
        {
            if (!ReferenceEquals(other, window)
                && other.GetType() == window.GetType()
                && other.Left == window.Left
                && other.Top == window.Top)
            {
                return true;
            }
        }

        return false;
    }

    private static void OnUseAppConfigStartUpStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            window.SourceInitialized += Window_SourceInitialized;
            window.Closing += Window_Closing;
        }
        else
        {
            window.SourceInitialized -= Window_SourceInitialized;
            window.Closing -= Window_Closing;
        }
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        var bounds = ConfigManager.Instance.AppConfig.StartUpState;
        if (bounds is null || !IsValidBounds(bounds))
        {
            return;
        }

        var restoredBounds = MoveIntoVirtualScreen(bounds);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = restoredBounds.X;
        window.Top = restoredBounds.Y;
        window.Width = restoredBounds.Width;
        window.Height = restoredBounds.Height;
    }

    private static void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        ConfigManager.Instance.AppConfig.StartUpState = new WindowBounds
        {
            X = (int)Math.Round(bounds.Left),
            Y = (int)Math.Round(bounds.Top),
            Width = (int)Math.Round(bounds.Width),
            Height = (int)Math.Round(bounds.Height)
        };

        ConfigManager.Instance.Save();
    }

    private static bool IsValidBounds(WindowBounds bounds)
    {
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static WindowBounds MoveIntoVirtualScreen(WindowBounds bounds)
    {
        var minX = SystemParameters.VirtualScreenLeft;
        var minY = SystemParameters.VirtualScreenTop;
        var maxX = minX + SystemParameters.VirtualScreenWidth;
        var maxY = minY + SystemParameters.VirtualScreenHeight;

        var width = Math.Min(bounds.Width, (int)SystemParameters.VirtualScreenWidth);
        var height = Math.Min(bounds.Height, (int)SystemParameters.VirtualScreenHeight);
        var x = Math.Clamp(bounds.X, (int)minX, (int)Math.Max(minX, maxX - width));
        var y = Math.Clamp(bounds.Y, (int)minY, (int)Math.Max(minY, maxY - height));

        return new WindowBounds
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }
}
