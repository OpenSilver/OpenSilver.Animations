using System.Windows;

namespace OpenSilver.Animations.Gallery
{
    public sealed partial class App : Application
    {
        public App()
        {
            // Below 960 pixels the tour uses its phone layout, and below 1280 its tablet layout.
            // The {Responsive} values in the XAML and the shell's layout share these breakpoints.
            ResponsiveThreshold.Default = new ResponsiveThreshold(960, 1280);

            InitializeComponent();
            RootVisual = new MainPage();
        }
    }
}
