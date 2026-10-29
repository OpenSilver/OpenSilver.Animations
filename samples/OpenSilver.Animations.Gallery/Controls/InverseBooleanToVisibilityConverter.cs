using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenSilver.Animations.Gallery.Controls
{
    /// <summary>
    /// Shows an element only when the bound value is false, for example while a popup is closed.
    /// </summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
