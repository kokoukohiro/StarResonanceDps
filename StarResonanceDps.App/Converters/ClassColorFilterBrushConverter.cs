using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// 設定画面のクラスアイコン用。素のクラスカラーにフィルターを掛けた色を返す。
/// 入力は [クラスカラー, フィルター有効, フィルター色, フィルターの強さ]。
/// </summary>
public sealed class ClassColorFilterBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4 || values[0] is not Color color)
        {
            return Brushes.Transparent;
        }

        if (values[1] is true && values[2] is Color filterColor && values[3] is double strength)
        {
            color = ClassColorFilter.Apply(color, filterColor, strength);
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
