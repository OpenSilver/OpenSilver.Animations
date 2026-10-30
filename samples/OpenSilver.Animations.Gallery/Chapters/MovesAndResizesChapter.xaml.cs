using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OpenSilver.Animations.Gallery.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class MovesAndResizesChapter : Page
    {
        private const double CollapsedCardHeight = 64;

        // The Threshold of the {Responsive} values of the profile panel, and two widths on either side.
        private const double ProfileBreakpoint = 220;
        private const double NarrowProfile = 170;
        private const double WideProfile = 300;

        // The splitter and the smallest width of the pane next to the profile.
        private const double ProfileGap = 40;

        private static readonly int[] BadgeCounts = { 3, 12, 128 };

        private int _badgeIndex;
        private bool _saving;

        public MovesAndResizesChapter()
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

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            EmailDetails.Visibility = EmailOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            TwoStepDetails.Visibility = TwoStepOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PublicDetails.Visibility = PublicOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Grow_Click(object sender, RoutedEventArgs e)
        {
            bool expand = !double.IsNaN(GrowingCard.Height);
            GrowingCard.Height = expand ? double.NaN : CollapsedCardHeight;
            GrowButton.Content = expand ? "Hide the release notes" : "Show the release notes";
        }

        private void WaveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WaveArea == null || WaveSlider == null || WaveText == null || WaveDemo == null)
            {
                return;
            }

            double wave = Math.Round(WaveSlider.Value, 1);
            WaveText.Text = wave.ToString(CultureInfo.InvariantCulture);

            Animate.SetMoveAndResize(WaveArea, new Smooth
            {
                ApplyTo = AnimationScope.EverythingInside,
                Wave = wave,
                Duration = AnimationTime.FromSeconds(0.4),
            });

            string smooth = Snippet.Effect("Smooth", "ApplyTo=EverythingInside", wave == 0 ? null : "Wave=" + Snippet.Number(wave), "Duration=0.4s");
            WaveDemo.Xaml = "<StackPanel anim:Animate.MoveAndResize=\"" + smooth + "\">";
        }

        private void SwitchProfile_Click(object sender, RoutedEventArgs e)
        {
            double wide = Math.Min(WideProfile, ProfileGrid.ActualWidth - ProfileGap);
            ProfileColumn.Width = new GridLength(ProfilePanel.ActualWidth < ProfileBreakpoint ? wide : NarrowProfile);
        }
    }
}
