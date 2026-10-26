using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using OpenSilver.Animations.Gallery.Shell;

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

        private double NextChangesWidth()
        {
            double narrow = WindowSize.IsPhone ? 110 : 140;
            double room = ChangesRow.ActualWidth - FollowerText.ActualWidth - FollowerText.Margin.Left;
            return ChangesBox.Width > narrow ? narrow : Math.Max(narrow, Math.Min(360, room));
        }

        private void SkipChange_Click(object sender, RoutedEventArgs e)
        {
            SkipPanel.Width = NextSkipWidth();
        }

        private void SkipChangeInstant_Click(object sender, RoutedEventArgs e)
        {
            double width = NextSkipWidth();
            Animate.Skip(() => SkipPanel.Width = width);
        }

        private double NextSkipWidth() => NextBoxWidth(SkipPanel, SkipStage);

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SaveSettings();

        private void EnabledSwitch_Changed(object sender, RoutedEventArgs e) => SaveSettings();

        private void TrySettings_Click(object sender, RoutedEventArgs e)
        {
            double width = NextBoxWidth(SettingsBox, SettingsStage);
            SettingsBox.HorizontalAlignment = width > SettingsBox.Width ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            SettingsBox.Width = width;
        }

        /// <summary>
        /// The other width of a box that goes from narrow to wide: 420 pixels, or the width of the
        /// demo on a small screen.
        /// </summary>
        private static double NextBoxWidth(FrameworkElement box, FrameworkElement stage)
        {
            double narrow = WindowSize.IsPhone ? 120 : 220;
            return box.Width > narrow ? narrow : Math.Max(narrow, Math.Min(420, stage.ActualWidth));
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
