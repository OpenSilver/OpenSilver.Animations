using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenSilver.Animations.Gallery.Shell;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter13 : Page
    {
        private const int RowCount = 30;
        private const double ShortRow = 40;
        private const double TallRow = 64;
        private const int FewTiles = 100;
        private const int ManyTiles = 400;
        private const double SmallTile = 14;
        private const double LargeTile = 20;

        private static readonly string[] Words = { "quickly", "today", "please", "and", "the", "report", "after", "lunch", "thanks" };
        private static readonly Brush RowLineBrush = new SolidColorBrush(Color.FromArgb(255, 0xE9, 0xEB, 0xF3));
        private static readonly Brush TileBrush = new SolidColorBrush(Color.FromArgb(255, 0x5B, 0x5B, 0xD6));

        private int _wordIndex;

        public Chapter13()
        {
            InitializeComponent();

            for (int i = 1; i <= RowCount; i++)
            {
                ScrollRows.Children.Add(new Border
                {
                    Height = ShortRow,
                    Padding = new Thickness(16, 0, 16, 0),
                    BorderBrush = RowLineBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = new TextBlock
                    {
                        Text = "Message " + i.ToString(CultureInfo.InvariantCulture),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                });
            }

            SetTileCount(FewTiles);
            LeftColumn.Width = new GridLength(LeftPaneWidth);
        }

        private static double LeftPaneWidth => WindowSize.IsPhone ? 120 : 220;

        private void Rows_Click(object sender, RoutedEventArgs e)
        {
            bool taller = ((FrameworkElement)ScrollRows.Children[0]).Height < TallRow;
            foreach (UIElement row in ScrollRows.Children)
            {
                ((FrameworkElement)row).Height = taller ? TallRow : ShortRow;
            }
            RowsButton.Content = taller ? "Make the rows shorter" : "Make the rows taller";
        }

        private void ResetSplit_Click(object sender, RoutedEventArgs e)
        {
            LeftColumn.Width = new GridLength(LeftPaneWidth);
        }

        private void BarSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderBar != null)
            {
                SliderBar.Width = e.NewValue;
            }
        }

        private void AddWord_Click(object sender, RoutedEventArgs e)
        {
            AutoWidthBox.Text += " " + Words[_wordIndex];
            _wordIndex = (_wordIndex + 1) % Words.Length;
        }

        private void Chapter8_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Current?.Navigate(Tour.Get(8));
        }

        private void TileCount_Click(object sender, RoutedEventArgs e)
        {
            bool many = BudgetTiles.Children.Count < ManyTiles;
            SetTileCount(many ? ManyTiles : FewTiles);
            TileCountButton.Content = many ? "Use 100 tiles" : "Use 400 tiles";
        }

        private void SetTileCount(int count)
        {
            double size = BudgetTiles.Children.Count > 0 ? ((FrameworkElement)BudgetTiles.Children[0]).Width : SmallTile;
            BudgetTiles.Children.Clear();
            for (int i = 0; i < count; i++)
            {
                BudgetTiles.Children.Add(new Border
                {
                    Width = size,
                    Height = size,
                    Margin = new Thickness(0, 0, 3, 3),
                    CornerRadius = new CornerRadius(3),
                    Background = TileBrush,
                });
            }
        }

        private void GrowTiles_Click(object sender, RoutedEventArgs e)
        {
            bool grow = ((FrameworkElement)BudgetTiles.Children[0]).Width < LargeTile;
            foreach (UIElement tile in BudgetTiles.Children)
            {
                ((FrameworkElement)tile).Width = grow ? LargeTile : SmallTile;
                ((FrameworkElement)tile).Height = grow ? LargeTile : SmallTile;
            }
        }

        private void WrapBox_Click(object sender, RoutedEventArgs e)
        {
            bool phone = WindowSize.IsPhone;
            double narrow = phone ? 160 : 300;
            WrapBox.Width = WrapBox.Width > narrow ? narrow : phone ? 260 : 520;
        }

        private void Shapes_Click(object sender, RoutedEventArgs e)
        {
            bool phone = WindowSize.IsPhone;
            double small = phone ? 50 : 80;
            double size = RectangleBox.Width > small ? small : phone ? 80 : 150;
            foreach (FrameworkElement box in new FrameworkElement[] { RectangleBox, EllipseBox, PathBox })
            {
                box.Width = size;
                box.Height = size;
            }
        }
    }
}
