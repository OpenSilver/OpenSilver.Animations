using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using OpenSilver.Animations.Gallery.Shell;

namespace OpenSilver.Animations.Gallery
{
    /// <summary>
    /// The shell of the tour: the toolbar, the chapters, the chapter page and the log.
    /// </summary>
    public partial class MainPage : UserControl
    {
        private const int MaxLogEntries = 40;
        private const double SlowMotionSpeed = 0.25;

        private readonly ObservableCollection<LogEntry> _log = new ObservableCollection<LogEntry>();

        public MainPage()
        {
            InitializeComponent();
            Current = this;

            ChapterList.ItemsSource = Tour.All;
            LogList.ItemsSource = _log;
            Toolbar.DataContext = AnimationSettings.Current;

            AnimationSettings.Logged += OnLogged;
            Loaded += OnLoaded;
        }

        public static MainPage Current { get; private set; }

        public void Navigate(ChapterInfo chapter)
        {
            if (chapter != null)
            {
                ContentFrame.Navigate(new Uri(chapter.Path, UriKind.Relative));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.Source == null || string.IsNullOrEmpty(ContentFrame.Source.OriginalString))
            {
                ContentFrame.Navigate(new Uri("/", UriKind.Relative));
            }
        }

        private void OnLogged(object sender, AnimationLoggedEventArgs e)
        {
            _log.Insert(0, new LogEntry(e.Message, e.Time));
            while (_log.Count > MaxLogEntries)
            {
                _log.RemoveAt(_log.Count - 1);
            }

            EmptyLogText.Visibility = Visibility.Collapsed;
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            _log.Clear();
            EmptyLogText.Visibility = Visibility.Visible;
        }

        private void Chapter_Click(object sender, RoutedEventArgs e)
        {
            Navigate((ChapterInfo)((FrameworkElement)sender).Tag);
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            string path = e.Uri?.OriginalString ?? string.Empty;
            foreach (ChapterInfo chapter in Tour.All)
            {
                chapter.IsCurrent = string.Equals(chapter.Path, path, StringComparison.OrdinalIgnoreCase);
            }

            PageScroller.ScrollToVerticalOffset(0);
        }

        /// <summary>
        /// Sets the speed of the animations to what the Slow motion switch says.
        /// </summary>
        public void ApplySlowMotion()
        {
            AnimationSettings.Current.Speed = SlowMotionSwitch.IsChecked == true ? SlowMotionSpeed : 1;
        }

        private void SlowMotionSwitch_Changed(object sender, RoutedEventArgs e) => ApplySlowMotion();

        private void ReplayPage_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Refresh();
        }
    }
}
