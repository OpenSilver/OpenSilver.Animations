using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace OpenSilver.Animations.Gallery.Controls
{
    /// <summary>
    /// Draws the curve of an easing: the time goes from left to right, and the position from where
    /// the element starts, the bottom dotted line, to where it ends, the top one. A curve that goes
    /// above the top line goes past the end.
    /// </summary>
    public class EasingCurve : Canvas
    {
        private const double DrawingWidth = 60;
        private const double DrawingHeight = 34;
        private const int Steps = 48;

        // The same scale for every curve, so that they compare. Overshoot goes up to about 1.13.
        private const double Lowest = -0.06;
        private const double Highest = 1.2;

        private static readonly Brush LevelBrush = new SolidColorBrush(Color.FromArgb(255, 0xC9, 0xCC, 0xD8));

        public static readonly DependencyProperty EasingNameProperty =
            DependencyProperty.Register(nameof(EasingName), typeof(string), typeof(EasingCurve),
                new PropertyMetadata(null, (d, e) => ((EasingCurve)d).Draw()));

        public EasingCurve()
        {
            Width = DrawingWidth;
            Height = DrawingHeight;
        }

        /// <summary>
        /// The name of the easing, as in XAML: Smooth, Gentle, Linear, Bouncy, EaseOut, EaseIn,
        /// EaseInOut, Overshoot, StrongEaseOut, StrongEaseIn or StrongEaseInOut.
        /// </summary>
        public string EasingName
        {
            get => (string)GetValue(EasingNameProperty);
            set => SetValue(EasingNameProperty, value);
        }

        private void Draw()
        {
            Children.Clear();
            if (string.IsNullOrEmpty(EasingName))
            {
                return;
            }

            var easing = (IEasingFunction)new EasingConverter().ConvertFrom(null, CultureInfo.InvariantCulture, EasingName);

            Children.Add(Level(0));
            Children.Add(Level(1));

            var curve = new Polyline
            {
                Stroke = (Brush)Application.Current.Resources["AccentBrush"],
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            for (int i = 0; i <= Steps; i++)
            {
                double time = (double)i / Steps;
                curve.Points.Add(new Point(time * DrawingWidth, Y(easing.Ease(time))));
            }
            Children.Add(curve);
        }

        private static Line Level(double position) => new Line
        {
            X1 = 0,
            X2 = DrawingWidth,
            Y1 = Y(position),
            Y2 = Y(position),
            Stroke = LevelBrush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 },
        };

        private static double Y(double position) => (Highest - position) / (Highest - Lowest) * DrawingHeight;
    }
}
