using System;
using System.Globalization;
using System.Windows.Data;

namespace CAIME.Converters
{
    public class OpacityPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Layer.OpacityToPercent((byte)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Layer.OpacityFromPercent(System.Convert.ToDouble(value, culture));
        }
    }
}
