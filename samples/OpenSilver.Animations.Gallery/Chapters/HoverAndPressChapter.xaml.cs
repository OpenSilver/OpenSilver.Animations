using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OpenSilver.Animations.Gallery.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class HoverAndPressChapter : Page
    {
        public HoverAndPressChapter()
        {
            InitializeComponent();
        }

        private void NudgeSettings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (NudgeIcons == null || NudgeDistanceSlider == null || NudgeTiltSlider == null || NudgeDistanceText == null || NudgeTiltText == null
                || NudgeDemo == null)
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

            string nudge = Snippet.Effect("Nudge",
                distance == 3 ? null : "Distance=" + Snippet.Number(distance),
                tilt == 2 ? null : "Tilt=" + Snippet.Number(tilt));
            NudgeDemo.Xaml = "<Border anim:Animate.Hover=\"" + nudge + "\" />";
        }

        private void SendSwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (SendButton == null || SendDemo == null)
            {
                return;
            }

            bool enabled = ((CheckBox)sender).IsChecked == true;
            SendButton.IsEnabled = enabled;
            SendDemo.Xaml = "<Button Content=\"Send\"" + (enabled ? "" : " IsEnabled=\"False\"") + " anim:Animate.Hover=\"Grow\" anim:Animate.Press=\"Shrink\" />";
        }
    }
}
