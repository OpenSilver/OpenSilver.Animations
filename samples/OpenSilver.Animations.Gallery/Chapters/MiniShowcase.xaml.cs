using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenSilver.Animations.Gallery.Chapters
{
    /// <summary>
    /// A small copy of the window of the OpenSilver Showcase, for chapter 10. Most of its animations
    /// and the code that drives them are the Showcase's: the menu, its button on small screens, the
    /// welcome page, the loading popup and the source code pane. Three ideas are not in the Showcase
    /// yet: the page shows at once under the loading popup, the menu slides out on small screens,
    /// and the pages have a transition.
    /// </summary>
    public partial class MiniShowcase : UserControl
    {
        private const double SmallScreenWidth = 480;
        private const double QuickLoadingSeconds = 0.3;
        private const double SlowLoadingSeconds = 2.5;

        private static readonly Brush SelectedItemBrush = new SolidColorBrush(Color.FromArgb(255, 0xEC, 0xEC, 0xFD));
        private static readonly Brush ItemBrush = new SolidColorBrush(Colors.Transparent);

        private enum State
        {
            Unset,
            LargeScreen,
            SmallScreenWithMenu,
            SmallScreenWithoutMenu,
        }

        private State _state;
        private FrameworkElement _page;
        private int _navigation;

        public MiniShowcase()
        {
            InitializeComponent();
            _page = WelcomePage;
            SelectMenuItem(WelcomePage);

            // As in the Showcase, the popup waits 1 s: when a sample loads sooner, it never shows.
            Animate.SetAppear(LoadingPopup, new Zoom { From = 0.5, Easing = Easings.Overshoot, Bounciness = 0.3, StartAfter = AnimationTime.FromSeconds(1) });

            PageScrollViewer.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(PageScrollViewer_MouseLeftButtonDown), true);
        }

        private void MiniShowcase_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width >= SmallScreenWidth)
            {
                GoToState(State.LargeScreen);
            }
            else if (_state == State.LargeScreen || _state == State.Unset)
            {
                GoToState(State.SmallScreenWithoutMenu);
            }
        }

        private void GoToState(State state)
        {
            if (state == _state)
            {
                return;
            }

            if (state == State.LargeScreen)
            {
                ButtonToHideOrShowMenu.Visibility = Visibility.Collapsed;
                PageContainer.Margin = new Thickness(0);
                Grid.SetColumn(PageScrollViewer, 1);
                Grid.SetColumnSpan(PageScrollViewer, 1);
                MenuContainer.Visibility = Visibility.Visible;
            }
            else
            {
                ButtonToHideOrShowMenu.Visibility = Visibility.Visible;
                PageContainer.Margin = new Thickness(0, 40, 0, 0);
                Grid.SetColumn(PageScrollViewer, 0);
                Grid.SetColumnSpan(PageScrollViewer, 2);

                // On small screens the menu is quicker, and doesn't wait for the page to open.
                Animate.SetAppear(MenuContainer, (Transition)Resources["MenuAnimation_OnAppear_Faster"]);
                MenuContainer.Visibility = state == State.SmallScreenWithMenu ? Visibility.Visible : Visibility.Collapsed;
            }
            _state = state;
        }

        private void ButtonToHideOrShowMenu_Click(object sender, RoutedEventArgs e)
        {
            GoToState(_state == State.SmallScreenWithMenu ? State.SmallScreenWithoutMenu : State.SmallScreenWithMenu);
        }

        private void PageScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_state == State.SmallScreenWithMenu)
            {
                GoToState(State.SmallScreenWithoutMenu);
            }
        }

        private void Logo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => NavigateToPage(WelcomePage, QuickLoadingSeconds);

        private void WelcomeItem_Click(object sender, RoutedEventArgs e) => NavigateToPage(WelcomePage, QuickLoadingSeconds);

        private void ButtonsItem_Click(object sender, RoutedEventArgs e) => NavigateToPage(ButtonsPage, QuickLoadingSeconds);

        private void DataGridItem_Click(object sender, RoutedEventArgs e) => NavigateToPage(DataGridPage, SlowLoadingSeconds, Orders);

        // The page shows at once, and its data, if any, when it's loaded.
        private async void NavigateToPage(FrameworkElement page, double loadingSeconds, UIElement data = null)
        {
            if (_state == State.SmallScreenWithMenu)
            {
                GoToState(State.SmallScreenWithoutMenu);
            }
            if (page == _page)
            {
                return;
            }

            int navigation = ++_navigation;
            SelectMenuItem(page);
            if (data != null)
            {
                data.Visibility = Visibility.Collapsed;
            }
            _page.Visibility = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
            _page = page;
            PageScrollViewer.ScrollToVerticalOffset(0);
            LoadingPopup.Visibility = Visibility.Visible;

            // The loading takes longer in slow motion too, so that it still compares with the 1 s
            // that the popup waits.
            await Task.Delay(TimeSpan.FromSeconds(loadingSeconds / AnimationSettings.Current.Speed));
            if (navigation != _navigation)
            {
                return;
            }

            LoadingPopup.Visibility = Visibility.Collapsed;
            if (data != null)
            {
                data.Visibility = Visibility.Visible;
            }
        }

        private void SelectMenuItem(FrameworkElement page)
        {
            WelcomeItem.Background = page == WelcomePage ? SelectedItemBrush : ItemBrush;
            ButtonsItem.Background = page == ButtonsPage ? SelectedItemBrush : ItemBrush;
            DataGridItem.Background = page == DataGridPage ? SelectedItemBrush : ItemBrush;
        }

        private void ViewSource_Click(object sender, RoutedEventArgs e)
        {
            // The pane slides up while the page makes room for it (see "Animate.Appear" in the XAML).
            if (SourceCodePane.Visibility == Visibility.Collapsed)
            {
                RowThatContainsTheGridSplitter.Height = new GridLength(5);
                RowThatContainsTheSourceCodePane.Height = new GridLength(1, GridUnitType.Star);
                GridSplitter1.Visibility = Visibility.Visible;
                SourceCodePane.Visibility = Visibility.Visible;
            }
            SourceCode.Text = (string)((FrameworkElement)sender).Tag;
        }

        private void ButtonToCloseSourceCode_Click(object sender, RoutedEventArgs e)
        {
            // It slides down while the page fills its place (see "Animate.Disappear" in the XAML).
            GridSplitter1.Visibility = Visibility.Collapsed;
            SourceCodePane.Visibility = Visibility.Collapsed;

            // The splitter may have been dragged.
            RowThatContainsThePage.Height = new GridLength(1, GridUnitType.Star);
            RowThatContainsTheGridSplitter.Height = new GridLength(0);
            RowThatContainsTheSourceCodePane.Height = new GridLength(0, GridUnitType.Star);
        }
    }
}
