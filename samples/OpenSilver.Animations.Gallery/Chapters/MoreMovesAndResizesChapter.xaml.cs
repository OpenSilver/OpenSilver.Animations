using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenSilver.Animations.Gallery.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class MoreMovesAndResizesChapter : Page
    {
        private const int BarCount = 14;
        private const double CollapsedSidebarWidth = 64;
        private const double FocusedSearchWidth = 320;

        private static readonly Brush BarBrush = new SolidColorBrush(Color.FromArgb(255, 0x5B, 0x5B, 0xD6));
        private static readonly Brush SelectedTabBrush = new SolidColorBrush(Color.FromArgb(255, 0x1C, 0x1E, 0x2B));
        private static readonly Brush TabBrush = new SolidColorBrush(Color.FromArgb(255, 0x54, 0x59, 0x70));

        private readonly Random _random = new Random(7);
        private bool _oneColumn;
        private bool _sidebarCollapsed;

        public MoreMovesAndResizesChapter()
        {
            InitializeComponent();

            for (int i = 0; i < BarCount; i++)
            {
                Bars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var bar = new Border
                {
                    Margin = new Thickness(0, 0, i < BarCount - 1 ? 8 : 0, 0),
                    CornerRadius = new CornerRadius(4, 4, 0, 0),
                    Background = BarBrush,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Height = NextBarHeight(),
                };
                Grid.SetColumn(bar, i);
                Bars.Children.Add(bar);
            }

            ApplyColumns();
            SelectTab(0);
        }

        private void ColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            _oneColumn = !_oneColumn;
            ApplyColumns();
            ColumnsButton.Content = _oneColumn ? "Switch to two columns" : "Switch to one column";
        }

        private void ApplyColumns()
        {
            Border[] cards = { ProfileCard, NotificationsCard, BillingCard, TeamCard };
            SecondColumn.Width = _oneColumn ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

            for (int i = 0; i < cards.Length; i++)
            {
                int column = _oneColumn ? 0 : i % 2;
                Grid.SetRow(cards[i], _oneColumn ? i : i / 2);
                Grid.SetColumn(cards[i], column);
                cards[i].Margin = _oneColumn
                    ? new Thickness(0, 0, 0, 12)
                    : new Thickness(column == 0 ? 0 : 6, 0, column == 0 ? 6 : 0, 12);
            }
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

        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            if (_sidebarCollapsed)
            {
                DemoSidebar.Width = CollapsedSidebarWidth;
            }
            else
            {
                DemoSidebar.ClearValue(WidthProperty);
            }
            SidebarButton.Content = _sidebarCollapsed ? "Expand the sidebar" : "Collapse the sidebar";
        }

        private void OpenDialog_Click(object sender, RoutedEventArgs e)
        {
            new ResizableDialog().Show();
        }

        private void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Photo.Source == null)
            {
                Photo.Source = new BitmapImage(new Uri("/OpenSilver.Animations.Gallery;component/Images/mountains.png", UriKind.Relative));
                PhotoButton.Content = "Remove the photo";
            }
            else
            {
                Photo.Source = null;
                PhotoButton.Content = "Load the photo";
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            double room = SearchRow.ActualWidth - FiltersButton.ActualWidth - SortButton.ActualWidth - 16;
            SearchBox.Width = Math.Max(SearchBox.ActualWidth, Math.Min(FocusedSearchWidth, room));
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) => SearchBox.ClearValue(WidthProperty);

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            int visitors = _random.Next(2, 40) * _random.Next(10, 1000);
            int signUps = _random.Next(5, 2000);
            VisitorsText.Text = visitors.ToString("N0", CultureInfo.InvariantCulture);
            SignUpsText.Text = signUps.ToString("N0", CultureInfo.InvariantCulture);
            ConversionText.Text = Math.Round(100.0 * signUps / Math.Max(1, visitors), 1).ToString(CultureInfo.InvariantCulture) + "%";

            foreach (UIElement bar in Bars.Children)
            {
                ((FrameworkElement)bar).Height = NextBarHeight();
            }
        }

        private void CarveOut_Changed(object sender, RoutedEventArgs e)
        {
            if (Chart == null || CarveOutDemo == null)
            {
                return;
            }

            bool carveOut = CarveOutSwitch.IsChecked == true;
            if (carveOut)
            {
                Animate.SetMoveAndResize(Chart, Movement.None);
            }
            else
            {
                Chart.ClearValue(Animate.MoveAndResizeProperty);
            }

            CarveOutDemo.Xaml =
                "<Border anim:Animate.MoveAndResize=\"{anim:Smooth ApplyTo=EverythingInside}\">\n" +
                "    <!-- The numbers animate -->\n" +
                (carveOut
                    ? "    <Grid x:Name=\"Chart\" anim:Animate.MoveAndResize=\"None\">\n        <!-- The bars snap -->\n"
                    : "    <Grid x:Name=\"Chart\">\n        <!-- The bars glide too -->\n") +
                "    </Grid>\n" +
                "</Border>";
        }

        private double NextBarHeight() => 16 + _random.Next(0, 120);
    }
}
