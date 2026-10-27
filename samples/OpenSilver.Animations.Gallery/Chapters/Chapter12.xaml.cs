using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter12 : Page
    {
        public Chapter12()
        {
            InitializeComponent();
        }

        private void NudgeSettings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (NudgeIcons == null || NudgeDistanceSlider == null || NudgeTiltSlider == null || NudgeDistanceText == null || NudgeTiltText == null)
            {
                return;
            }

            double distance = Math.Round(NudgeDistanceSlider.Value);
            double tilt = Math.Round(NudgeTiltSlider.Value);
            NudgeDistanceText.Text = distance.ToString(CultureInfo.InvariantCulture) + " px";
            NudgeTiltText.Text = tilt.ToString(CultureInfo.InvariantCulture) + "°";

            foreach (UIElement icon in NudgeIcons.Children)
            {
                Animate.SetHover(icon, new Nudge { Distance = distance, Tilt = tilt });
            }
        }

        private void SendSwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (SendButton != null)
            {
                SendButton.IsEnabled = ((CheckBox)sender).IsChecked == true;
            }
        }
    }
}
