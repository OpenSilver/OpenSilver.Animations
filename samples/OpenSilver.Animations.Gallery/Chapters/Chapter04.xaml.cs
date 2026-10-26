using System.Windows;
using System.Windows.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter04 : Page
    {
        public Chapter04()
        {
            InitializeComponent();
            OpenProfile();
            OpenInboxes();
        }

        private void OpenProfile_Click(object sender, RoutedEventArgs e) => OpenProfile();

        private void OpenInboxes_Click(object sender, RoutedEventArgs e) => OpenInboxes();

        private void OpenProfile() => ProfileHost.Child = Build("ProfilePage");

        private void OpenInboxes()
        {
            InPlaceHost.Child = Build("InPlaceInbox");
            PushOthersHost.Child = Build("PushOthersInbox");
            SkipHost.Child = Build("SkipInbox");
        }

        private UIElement Build(string template) => (UIElement)((DataTemplate)Resources[template]).LoadContent();

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            bool show = Details.Visibility != Visibility.Visible;
            Details.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DetailsButton.Content = show ? "Hide the details" : "Show the details";
        }

        private void GiftSwitch_Changed(object sender, RoutedEventArgs e)
        {
            GiftMessage.Visibility = GiftSwitch.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
