using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DistrictJsonToSql
{
    /// <summary>bool → Visibility，供 x:Bind 使用。</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool flag && flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
