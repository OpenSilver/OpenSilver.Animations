using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter12 : Page
    {
        private static readonly AnimationTime DefaultDuration = AnimationTime.FromMilliseconds(250);

        private bool _loading = true;

        public Chapter12()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadSettings();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ResetSettings();
        }

        private void ChangeDirectly_Click(object sender, RoutedEventArgs e)
        {
            ChangesBox.Width = NextChangesWidth();
        }

        private void ChangeAnimated_Click(object sender, RoutedEventArgs e)
        {
            double width = NextChangesWidth();
            Animate.Changes(() => ChangesBox.Width = width);
        }

        private void ChangeSlowly_Click(object sender, RoutedEventArgs e)
        {
            double width = NextChangesWidth();
            Animate.Changes(() => ChangesBox.Width = width, AnimationTime.FromSeconds(0.6));
        }

        private double NextChangesWidth() => ChangesBox.Width > 200 ? 140 : 360;

        private void SkipChange_Click(object sender, RoutedEventArgs e)
        {
            SkipPanel.Width = NextSkipWidth();
        }

        private void SkipChangeInstant_Click(object sender, RoutedEventArgs e)
        {
            double width = NextSkipWidth();
            Animate.Skip(() => SkipPanel.Width = width);
        }

        private double NextSkipWidth() => SkipPanel.Width > 300 ? 220 : 420;

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SaveSettings();

        private void EnabledSwitch_Changed(object sender, RoutedEventArgs e) => SaveSettings();

        private void TrySettings_Click(object sender, RoutedEventArgs e)
        {
            bool wide = SettingsBox.Width > 300;
            SettingsBox.Width = wide ? 220 : 420;
            SettingsBox.HorizontalAlignment = wide ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            ResetSettings();
            LoadSettings();
        }

        private void LoadSettings()
        {
            AnimationSettings settings = AnimationSettings.Current;
            _loading = true;
            SpeedSlider.Value = settings.Speed;
            DurationSlider.Value = settings.DefaultDuration.Seconds;
            EnabledSwitch.IsChecked = settings.IsEnabled;
            _loading = false;
            UpdateLabels();
        }

        private void SaveSettings()
        {
            if (_loading)
            {
                return;
            }

            AnimationSettings settings = AnimationSettings.Current;
            settings.Speed = Math.Round(SpeedSlider.Value * 4) / 4;
            settings.DefaultDuration = AnimationTime.FromSeconds(Math.Round(DurationSlider.Value * 20) / 20);
            settings.IsEnabled = EnabledSwitch.IsChecked == true;
            UpdateLabels();
        }

        private static void ResetSettings()
        {
            AnimationSettings settings = AnimationSettings.Current;
            settings.DefaultDuration = DefaultDuration;
            settings.IsEnabled = true;
            MainPage.Current?.ApplySlowMotion();
        }

        private void UpdateLabels()
        {
            AnimationSettings settings = AnimationSettings.Current;
            SpeedText.Text = settings.Speed.ToString(CultureInfo.InvariantCulture);
            DurationText.Text = settings.DefaultDuration.Seconds.ToString(CultureInfo.InvariantCulture) + " s";
        }
    }
}
