using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenSilver.Animations.Gallery.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter10 : Page
    {
        private const double CollapsedCardHeight = 64;

        private static readonly Brush SelectedTabBrush = new SolidColorBrush(Color.FromArgb(255, 0x1C, 0x1E, 0x2B));
        private static readonly Brush TabBrush = new SolidColorBrush(Color.FromArgb(255, 0x54, 0x59, 0x70));

        public Chapter10()
        {
            InitializeComponent();
            SelectTab(0);
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            SelectTab(int.Parse((string)((FrameworkElement)sender).Tag, CultureInfo.InvariantCulture));
        }

        private void SelectTab(int index)
        {
            Grid.SetColumn(Underline, index);
            foreach (UIElement child in Tabs.Children)
            {
                if (child is Button tab)
                {
                    tab.Foreground = Grid.GetColumn(tab) == index ? SelectedTabBrush : TabBrush;
                }
            }
        }

        private void BouncySettings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Underline == null || BouncinessSlider == null || BouncyDurationSlider == null || BouncinessText == null || BouncyDurationText == null
                || BouncyDemo == null)
            {
                return;
            }

            double bounciness = Math.Round(BouncinessSlider.Value, 1);
            double duration = Math.Round(BouncyDurationSlider.Value, 1);
            BouncinessText.Text = bounciness.ToString(CultureInfo.InvariantCulture);
            BouncyDurationText.Text = duration.ToString(CultureInfo.InvariantCulture) + " s";

            Animate.SetMoveAndResize(Underline, new Bouncy
            {
                Bounciness = bounciness,
                Duration = AnimationTime.FromSeconds(duration),
            });

            string bouncy = Snippet.Effect("Bouncy",
                bounciness == 0.5 ? null : "Bounciness=" + Snippet.Number(bounciness),
                duration == 0.5 ? null : "Duration=" + AnimationTime.FromSeconds(duration));
            BouncyDemo.Xaml = "<Border x:Name=\"Underline\" Grid.Row=\"1\" Height=\"3\" anim:Animate.MoveAndResize=\"" + bouncy + "\" />";
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
    }
}
