using System.Windows;

namespace OpenSilver.Animations.Gallery.Shell
{
    /// <summary>
    /// Tells the code of the tour which layout it uses, with the same breakpoints as the
    /// {Responsive} values in the XAML.
    /// </summary>
    public static class WindowSize
    {
        /// <summary>
        /// The width of the window, which is what {Responsive} compares with the breakpoints. The
        /// page can be a little narrower, because the window draws a border around it.
        /// </summary>
        public static double Width => Application.Current.MainWindow.ActualWidth;

        public static bool IsPhone => Width < ResponsiveThreshold.Default.Mobile;
    }
}
