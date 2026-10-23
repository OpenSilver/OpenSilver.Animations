using System.Windows;
using System.Windows.Controls;
using OpenSilver.Animations.Gallery.Shell;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Welcome : Page
    {
        public Welcome()
        {
            InitializeComponent();
        }

        private void Start_Click(object sender, RoutedEventArgs e) => MainPage.Current?.Navigate(Tour.Get(5));
    }
}
