using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LECG.ViewModels;

namespace LECG.Utils
{
    public class FilterStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FilterStatus status)
            {
                switch (status)
                {
                    case FilterStatus.Created:
                        return new SolidColorBrush(Color.FromRgb(56, 161, 105)); // ColorSuccess
                    case FilterStatus.Modified:
                        return new SolidColorBrush(Color.FromRgb(49, 130, 206)); // A nice Blue
                    case FilterStatus.Removable:
                        return new SolidColorBrush(Color.FromRgb(197, 48, 48)); // ColorAlert
                    default:
                        return Brushes.Transparent;
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
