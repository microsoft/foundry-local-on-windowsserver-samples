using System;
using System.Globalization;
using System.Windows.Data;

namespace PatientSummaryTool.Utils.Converters
{
    public class FirstNonEmptyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (var value in values)
            {
                if (value is string str && !string.IsNullOrWhiteSpace(str))
                    return str;
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
