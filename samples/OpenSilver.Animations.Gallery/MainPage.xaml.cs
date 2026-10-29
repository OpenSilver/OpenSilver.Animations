using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using OpenSilver.Animations.Gallery.Shell;

namespace OpenSilver.Animations.Gallery
{
    /// <summary>
    /// The shell of the tour: the toolbar, the chapters, the chapter page and the log.
    /// </summary>
    /// <remarks>
    /// On a desktop the chapters and the log are columns beside the page. On a tablet the log
    /// becomes a drawer opened from the top bar, and on a phone the chapters and the options
    /// become a drawer too. The breakpoints are <see cref="ResponsiveThreshold.Default"/>.
    /// </remarks>
    public partial class MainPage : UserControl
    {
        private const int MaxLogEntries = 40;
        private const double SlowMotionSpeed = 0.25;
        private const double ChaptersWidth = 272;
        private const double LogWidth = 320;
        private const double MenuWidth = 300;
        private const double MenuGap = 48;

        // Each new page slides in a little. Not the first one, which comes after the shell when the app opens.
        private static readonly Transition PageAppear = new Slide { From = Side.Bottom, Distance = Distance.Short };

        // The shell's animations stay out of the log. A message about LogList would add an entry,
        // which animates and logs again, forever.
        private static readonly string[] ShellNames = { "ChaptersPanel", "LogPanel", "LogList", "Scrim", "ChapterSelection", "ChapterHover", "UnreadDot" };

        private readonly ObservableCollection<LogEntry> _log = new ObservableCollection<LogEntry>();

        private string _pageAppeared;
        private bool _hasOpenedAPage;
        private bool _isPhone;
        private bool _isLogBeside = true;
        private bool _isMenuOpen;
        private bool _isLogOpen;

        public MainPage()
        {
            InitializeComponent();
            Current = this;

            ChapterList.ItemsSource = Tour.All;
            LogList.ItemsSource = _log;
            Toolbar.DataContext = AnimationSettings.Current;

            AnimationSettings.Logged += OnLogged;

            // Before the first layout pass, so that on a phone the drawers start hidden.
            UpdateLayoutForWidth(WindowSize.Width);
        }

        public static MainPage Current { get; private set; }

        public void Navigate(ChapterInfo chapter)
        {
            if (chapter != null)
            {
                ContentFrame.Navigate(new Uri(chapter.Path, UriKind.Relative));
            }
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(new Uri("/", UriKind.Relative));
        }

        private void OnLogged(object sender, AnimationLoggedEventArgs e)
        {
            if (IsAboutTheShell(e.Message))
            {
                return;
            }

            _log.Insert(0, new LogEntry(e.Message, e.Time));
            while (_log.Count > MaxLogEntries)
            {
                _log.RemoveAt(_log.Count - 1);
            }

            EmptyLogText.Visibility = Visibility.Collapsed;
            if (!_isLogBeside && !_isLogOpen)
            {
                UnreadDot.Visibility = Visibility.Visible;
            }
        }

        private bool IsAboutTheShell(string message)
        {
            if (_pageAppeared != null && message.StartsWith(_pageAppeared, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (string name in ShellNames)
            {
                if (message.Contains(name))
                {
                    return true;
                }
            }

            return false;
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

            if (e.Content is UIElement page && _hasOpenedAPage)
            {
                Animate.SetAppear(page, PageAppear);
                _pageAppeared = page.GetType().Name + " appeared";
            }
            _hasOpenedAPage = true;

            PageScroller.ScrollToVerticalOffset(0);
            _isMenuOpen = false;
            UpdateDrawers();
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

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateLayoutForWidth(e.NewSize.Width);

        private void UpdateLayoutForWidth(double width)
        {
            ResponsiveThreshold threshold = ResponsiveThreshold.Default;
            _isPhone = WindowSize.Width < threshold.Mobile;
            _isLogBeside = WindowSize.Width >= threshold.Tablet;
            if (!_isPhone)
            {
                _isMenuOpen = false;
            }
            if (_isLogBeside)
            {
                _isLogOpen = false;
            }

            ChaptersColumn.Width = new GridLength(_isPhone ? 0 : ChaptersWidth);
            Grid.SetColumnSpan(ChaptersPanel, _isPhone ? 3 : 1);
            ChaptersPanel.HorizontalAlignment = _isPhone ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            ChaptersPanel.Width = _isPhone ? Math.Min(MenuWidth, width - MenuGap) : double.NaN;
            DrawerOptions.Visibility = _isPhone ? Visibility.Visible : Visibility.Collapsed;
            MoveToolbar(_isPhone ? DrawerOptions : ToolbarHost);

            LogColumn.Width = new GridLength(_isLogBeside ? LogWidth : 0);
            Grid.SetColumn(LogPanel, _isLogBeside ? 2 : 0);
            Grid.SetColumnSpan(LogPanel, _isLogBeside ? 1 : 3);
            LogPanel.HorizontalAlignment = _isLogBeside ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
            LogPanel.Width = _isLogBeside ? double.NaN : Math.Min(LogWidth, width);

            UpdateDrawers();
        }

        private void MoveToolbar(Panel host)
        {
            if (Toolbar.Parent is Panel parent && parent != host)
            {
                parent.Children.Remove(Toolbar);
                host.Children.Add(Toolbar);
            }
        }

        private void UpdateDrawers()
        {
            ChaptersPanel.Visibility = !_isPhone || _isMenuOpen ? Visibility.Visible : Visibility.Collapsed;
            LogPanel.Visibility = _isLogBeside || _isLogOpen ? Visibility.Visible : Visibility.Collapsed;
            Scrim.Visibility = _isMenuOpen || _isLogOpen ? Visibility.Visible : Visibility.Collapsed;
            if (_isLogBeside || _isLogOpen)
            {
                UnreadDot.Visibility = Visibility.Collapsed;
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            _isMenuOpen = !_isMenuOpen;
            _isLogOpen = false;
            UpdateDrawers();
        }

        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
            _isLogOpen = !_isLogOpen;
            _isMenuOpen = false;
            UpdateDrawers();
        }

        private void CloseLog_Click(object sender, RoutedEventArgs e)
        {
            _isLogOpen = false;
            UpdateDrawers();
        }

        private void Scrim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMenuOpen = false;
            _isLogOpen = false;
            UpdateDrawers();
        }
    }
}
