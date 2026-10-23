using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter05 : Page
    {
        private static readonly int[] BadgeCounts = { 3, 12, 128 };
        private static bool _outlinesShown;

        private int _badgeIndex;
        private bool _saving;

        public Chapter05()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // This chapter explains what moves together, so the outlines are on the first time.
            if (!_outlinesShown)
            {
                _outlinesShown = true;
                AnimationSettings.Current.ShowWhatAnimates = true;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_saving)
            {
                return;
            }

            _saving = true;
            SaveButton.Content = "Saving...";
            await Task.Delay(1500);
            SaveButton.Content = "Saved";
            await Task.Delay(1200);
            SaveButton.Content = "Save";
            _saving = false;
        }

        private void NewMessages_Click(object sender, RoutedEventArgs e)
        {
            _badgeIndex = (_badgeIndex + 1) % BadgeCounts.Length;
            BadgeCount.Text = BadgeCounts[_badgeIndex].ToString();
        }

        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            bool collapse = DemoSidebar.Width > 100;
            DemoSidebar.Width = collapse ? 64 : 240;
            SidebarButton.Content = collapse ? "Expand the sidebar" : "Collapse the sidebar";
        }

        private void OpenDialog_Click(object sender, RoutedEventArgs e)
        {
            new ResizableDialog().Show();
        }

        private void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Photo.Source == null)
            {
                Photo.Source = new BitmapImage(new Uri("/OpenSilver.Animations.Gallery;component/Images/mountains.png", UriKind.Relative));
                PhotoButton.Content = "Remove the photo";
            }
            else
            {
                Photo.Source = null;
                PhotoButton.Content = "Load the photo";
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) => SearchBox.Width = 320;

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) => SearchBox.Width = 180;
    }
}
