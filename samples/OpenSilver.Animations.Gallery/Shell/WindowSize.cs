using System;
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
        public static double Width => Application.Current.MainWindow is Window window && window.ActualWidth > 0
            ? window.ActualWidth
            : BrowserWidth();

        public static bool IsPhone => Width < ResponsiveThreshold.Default.Mobile;

        // Before the window is created and laid out, as when the shell builds its first layout.
        private static double BrowserWidth()
        {
            using (IDisposable width = OpenSilver.Interop.ExecuteJavaScript("window.innerWidth"))
            {
                return Convert.ToDouble(width);
            }
        }
    }
}
