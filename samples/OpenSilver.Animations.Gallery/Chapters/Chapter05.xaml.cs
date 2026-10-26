using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter05 : Page
    {
        private const double CollapsedSidebarWidth = 64;
        private const double FocusedSearchWidth = 320;

        private static readonly int[] BadgeCounts = { 3, 12, 128 };

        private int _badgeIndex;
        private bool _saving;
        private bool _sidebarCollapsed;

        public Chapter05()
        {
            InitializeComponent();
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
            _sidebarCollapsed = !_sidebarCollapsed;
            if (_sidebarCollapsed)
            {
                DemoSidebar.Width = CollapsedSidebarWidth;
            }
            else
            {
                DemoSidebar.ClearValue(WidthProperty);
            }
            SidebarButton.Content = _sidebarCollapsed ? "Expand the sidebar" : "Collapse the sidebar";
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

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            double room = SearchRow.ActualWidth - FiltersButton.ActualWidth - SortButton.ActualWidth - 16;
            SearchBox.Width = Math.Max(SearchBox.ActualWidth, Math.Min(FocusedSearchWidth, room));
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) => SearchBox.ClearValue(WidthProperty);
    }
}
