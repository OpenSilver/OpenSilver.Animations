using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenSilver.Animations.Gallery.Controls
{
    /// <summary>
    /// Shows an element only when the bound text is not empty.
    /// </summary>
    public sealed class NonEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
