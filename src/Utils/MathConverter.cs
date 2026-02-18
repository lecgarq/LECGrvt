using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace LECG.Utils
{
    [ValueConversion(typeof(double), typeof(double))]
    public class MathConverter : MarkupExtension, IMultiValueConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Calculate(value, parameter);
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
             ArgumentNullException.ThrowIfNull(values);

             // For progress bar width: values[0] = progress (0-100), values[1] = total width
             if (values.Length == 2 && values[0] is double progress && values[1] is double totalWidth)
             {
                 return (progress / 100.0) * totalWidth;
             }
             return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();

        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        private double Calculate(object value, object parameter)
        {
            // Simple pass-through or implementation if needed for single binding
            return 0;
        }
    }
}
