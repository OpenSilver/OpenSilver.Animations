using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenSilver.Animations.Gallery.Shell;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter08 : Page
    {
        private const int TileCount = 14;

        // The Threshold of the {Responsive} values of the profile panel, and two widths on either side.
        private const double ProfileBreakpoint = 220;
        private const double NarrowProfile = 170;
        private const double WideProfile = 300;

        // The splitter and the smallest width of the pane next to the profile.
        private const double ProfileGap = 40;

        private static double TileWidth => WindowSize.IsPhone ? 120 : 150;

        private static readonly string[] Places =
        {
            "Lisbon", "Kyoto", "Oslo", "Lima", "Cairo", "Quebec", "Hanoi",
            "Seville", "Cusco", "Tromsø", "Petra", "Bruges", "Valparaíso", "Hoi An",
        };

        private static readonly Brush TextBrush = new SolidColorBrush(Colors.White);

        private static readonly Color[] TileColors =
        {
            Color.FromArgb(255, 0x5B, 0x5B, 0xD6), Color.FromArgb(255, 0xE0, 0x7A, 0x1F), Color.FromArgb(255, 0x23, 0x9B, 0x6A),
            Color.FromArgb(255, 0xC8, 0x3E, 0x7A), Color.FromArgb(255, 0x2E, 0x86, 0xC1), Color.FromArgb(255, 0x8E, 0x5B, 0xD6),
            Color.FromArgb(255, 0xC9, 0x9A, 0x16),
        };

        public Chapter08()
        {
            InitializeComponent();
            AddTiles(FollowingTiles, clickable: false);
            AddTiles(SnappingTiles, clickable: true);
        }

        private static void AddTiles(Panel panel, bool clickable)
        {
            for (int i = 0; i < TileCount; i++)
            {
                var tile = new Border
                {
                    Width = TileWidth,
                    Height = 96,
                    Margin = new Thickness(0, 0, 12, 12),
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(TileColors[i % TileColors.Length]),
                    Padding = new Thickness(12, 10, 12, 10),
                    Child = new TextBlock
                    {
                        Text = Places[i],
                        Foreground = TextBrush,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Bottom,
                    },
                };

                if (clickable)
                {
                    tile.Cursor = Cursors.Hand;
                    tile.MouseLeftButtonUp += Tile_MouseLeftButtonUp;
                }

                panel.Children.Add(tile);
            }
        }

        private static void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var tile = (Border)sender;
            double width = TileWidth;
            tile.Width = tile.Width > width ? width : width * 2 + 12;
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            WindowWidthText.Text = $"Your window is {WindowSize.Width:0} pixels wide.";
        }

        private void SwitchProfile_Click(object sender, RoutedEventArgs e)
        {
            double wide = Math.Min(WideProfile, ProfileGrid.ActualWidth - ProfileGap);
            ProfileColumn.Width = new GridLength(ProfilePanel.ActualWidth < ProfileBreakpoint ? wide : NarrowProfile);
        }
    }
}
