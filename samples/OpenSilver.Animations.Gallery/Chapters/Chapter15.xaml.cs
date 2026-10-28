using System.Windows;
using System.Windows.Controls;
using OpenSilver.Animations.Gallery.Shell;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter15 : Page
    {
        private const double PhoneWidth = 340;

        public Chapter15()
        {
            InitializeComponent();
            OpenShowcase();
        }

        private void OpenAgain_Click(object sender, RoutedEventArgs e) => OpenShowcase();

        private void OpenShowcase() => ShowcaseHost.Child = new MiniShowcase { Width = GetShowcaseWidth() };

        private void PhoneSizeSwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowcaseHost.Child is MiniShowcase showcase)
            {
                showcase.Width = GetShowcaseWidth();
            }
        }

        private double GetShowcaseWidth() => PhoneSizeSwitch.IsChecked == true ? PhoneWidth : double.NaN;

        private void Chapter_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Current?.Navigate(Tour.Get(int.Parse((string)((FrameworkElement)sender).Tag)));
        }
    }
}
