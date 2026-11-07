using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace DistrictJsonToSql
{
    /// <summary>把与 UI 无关的 StatusSeverity 映射到 InfoBarSeverity。</summary>
    public sealed class StatusSeverityToInfoBarSeverityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is StatusSeverity severity)
            {
                switch (severity)
                {
                    case StatusSeverity.Success:
                        return InfoBarSeverity.Success;
                    case StatusSeverity.Warning:
                        return InfoBarSeverity.Warning;
                    case StatusSeverity.Error:
                        return InfoBarSeverity.Error;
                }
            }

            return InfoBarSeverity.Informational;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
