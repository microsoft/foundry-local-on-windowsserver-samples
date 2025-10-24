using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PatientSummaryTool.Utils.Converters
{
    public class ObjectVisibilityHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null || value == null) return Visibility.Collapsed;

            return parameter.Equals(value) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Not necessary to implement this
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
