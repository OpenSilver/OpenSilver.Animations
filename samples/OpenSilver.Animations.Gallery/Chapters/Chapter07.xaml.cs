using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter07 : Page
    {
        private const int BarCount = 14;
        private static readonly Brush BarBrush = new SolidColorBrush(Color.FromArgb(255, 0x5B, 0x5B, 0xD6));

        private readonly Random _random = new Random(7);
        private bool _oneColumn;

        public Chapter07()
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

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            EmailDetails.Visibility = EmailOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            TwoStepDetails.Visibility = TwoStepOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PublicDetails.Visibility = PublicOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

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
