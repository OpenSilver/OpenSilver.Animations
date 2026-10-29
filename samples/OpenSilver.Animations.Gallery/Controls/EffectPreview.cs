using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace OpenSilver.Animations.Gallery.Controls
{
    /// <summary>
    /// A drawing of an effect: a little card with hints, like arrows, that tell what the effect does.
    /// Half a second after the mouse enters the tile it is in, the card plays the effect in a loop,
    /// slower and bigger than the real one, so that it is easy to see. Storyboards play it, not
    /// the library, so that each drawing shows its own effect clearly.
    /// </summary>
    public class EffectPreview : Canvas
    {
        private const double DrawingWidth = 60;
        private const double DrawingHeight = 34;
        private const double CardWidth = 30;
        private const double CardHeight = 20;
        private const double CardLeft = (DrawingWidth - CardWidth) / 2;
        private const double CardTop = (DrawingHeight - CardHeight) / 2;

        private static readonly TimeSpan HoverDelay = TimeSpan.FromSeconds(0.5);

        // One loop: the card appears, stays, disappears, and stays hidden a moment.
        private const double Appeared = 0.9;
        private const double Shown = 1.9;
        private const double Disappeared = 2.5;
        private const double LoopEnd = 3.0;

        private static readonly Color CardColor = Color.FromArgb(255, 0x8A, 0x63, 0xE0);

        public static readonly DependencyProperty EffectNameProperty =
            DependencyProperty.Register(nameof(EffectName), typeof(string), typeof(EffectPreview),
                new PropertyMetadata(null, (d, e) => ((EffectPreview)d).Draw()));

        private Storyboard _loop;
        private UIElement _hoverTarget;

        public EffectPreview()
        {
            Width = DrawingWidth;
            Height = DrawingHeight;
            Clip = new RectangleGeometry { Rect = new Rect(0, 0, DrawingWidth, DrawingHeight) };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// The name of the effect, as in XAML: Fade, Slide, Zoom, Pop, Wipe, Blur or Custom.
        /// </summary>
        public string EffectName
        {
            get => (string)GetValue(EffectNameProperty);
            set => SetValue(EffectNameProperty, value);
        }

        // The whole tile starts the loop, not only the drawing.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hoverTarget = FindTile() ?? this;
            _hoverTarget.MouseEnter += OnMouseEnter;
            _hoverTarget.MouseLeave += OnMouseLeave;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_hoverTarget != null)
            {
                _hoverTarget.MouseEnter -= OnMouseEnter;
                _hoverTarget.MouseLeave -= OnMouseLeave;
                _hoverTarget = null;
            }
            _loop?.Stop();
        }

        private UIElement FindTile()
        {
            for (DependencyObject parent = VisualTreeHelper.GetParent(this); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is ButtonBase tile)
                {
                    return tile;
                }
            }
            return null;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e) => _loop?.Begin();

        // Stopping gives every property its value back, so the drawing is still again.
        private void OnMouseLeave(object sender, MouseEventArgs e) => _loop?.Stop();

        private void Draw()
        {
            _loop?.Stop();
            Children.Clear();
            _loop = new Storyboard { BeginTime = HoverDelay };
            if (string.IsNullOrEmpty(EffectName))
            {
                return;
            }

            var hints = new Canvas();
            Children.Add(hints);
            HideWhilePlaying(hints);

            Border card = NewCard();
            var transform = new CompositeTransform();
            card.RenderTransform = transform;
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            UIElement actor = card;

            var fadeIn = new CubicEase { EasingMode = EasingMode.EaseOut };
            switch (EffectName)
            {
                case "Fade":
                    // Stripes, from the full color to almost nothing.
                    hints.Children.Add(Stripe(0, 1, new CornerRadius(4, 0, 0, 4)));
                    hints.Children.Add(Stripe(1, 0.5, new CornerRadius(0)));
                    hints.Children.Add(Stripe(2, 0.18, new CornerRadius(0, 4, 4, 0)));
                    card.Opacity = 0;
                    Place(card, CardLeft, CardTop);
                    Loop(card, "Opacity", 0, 1, new SineEase { EasingMode = EasingMode.EaseInOut });
                    break;

                case "Slide":
                    // The card comes from below, where the arrow is.
                    Place(card, CardLeft, 4);
                    hints.Children.Add(Arrow("M30,33 L30,26 M26.5,29.5 L30,26 L33.5,29.5"));
                    Loop(transform, "TranslateY", 26, 0, fadeIn);
                    Loop(card, "Opacity", 0, 1, fadeIn);
                    break;

                case "Zoom":
                    // Arrows from the corners: the card grows.
                    Place(card, CardLeft, CardTop);
                    hints.Children.Add(Arrow("M13,5 L9,1 M9,4.5 L9,1 L12.5,1"));
                    hints.Children.Add(Arrow("M47,5 L51,1 M47.5,1 L51,1 L51,4.5"));
                    hints.Children.Add(Arrow("M13,29 L9,33 M9,29.5 L9,33 L12.5,33"));
                    hints.Children.Add(Arrow("M47,29 L51,33 M47.5,33 L51,33 L51,29.5"));
                    Loop(transform, "ScaleX", 0.2, 1, fadeIn);
                    Loop(transform, "ScaleY", 0.2, 1, fadeIn);
                    Loop(card, "Opacity", 0, 1, fadeIn);
                    break;

                case "Pop":
                    // Rings around the card: it springs out, past its size, and back.
                    hints.Children.Add(Halo(6, 0.18));
                    hints.Children.Add(Halo(3, 0.3));
                    Place(card, CardLeft, CardTop);
                    var spring = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 4 };
                    Loop(transform, "ScaleX", 0, 1, spring);
                    Loop(transform, "ScaleY", 0, 1, spring);
                    Loop(card, "Opacity", 0, 1, new CubicEase { EasingMode = EasingMode.EaseOut });
                    break;

                case "Wipe":
                    // Half shown, from the bottom, with the edge that moves. The card stays in place
                    // while a window moves over it.
                    hints.Children.Add(Pale(CardLeft, CardTop, CardWidth, CardHeight, new CornerRadius(4)));
                    hints.Children.Add(Solid(CardLeft, CardTop + CardHeight / 2, CardWidth, CardHeight / 2, new CornerRadius(0, 0, 4, 4)));
                    hints.Children.Add(Arrow("M11,17 L49,17"));
                    var window = new Canvas
                    {
                        Width = CardWidth,
                        Height = CardHeight,
                        Opacity = 0,
                        Clip = new RectangleGeometry { Rect = new Rect(0, 0, CardWidth, CardHeight) },
                    };
                    var windowTransform = new TranslateTransform();
                    window.RenderTransform = windowTransform;
                    Place(window, CardLeft, CardTop);
                    window.Children.Add(card);
                    actor = window;
                    ShowWhilePlaying(window);
                    Loop(windowTransform, "Y", CardHeight, 0, fadeIn);
                    Loop(transform, "TranslateY", -CardHeight, 0, fadeIn);
                    break;

                case "Blur":
                    // A blurry card: it comes into focus.
                    Border blurry = NewCard();
                    blurry.Effect = new BlurEffect { Radius = 8 };
                    Place(blurry, CardLeft, CardTop);
                    hints.Children.Add(blurry);
                    card.Opacity = 0;
                    Place(card, CardLeft, CardTop);
                    var blur = new BlurEffect { Radius = 0 };
                    card.Effect = blur;
                    Loop(blur, "Radius", 12, 0, fadeIn);
                    Loop(card, "Opacity", 0, 1, fadeIn);
                    break;

                case "Custom":
                    // A tilted copy: the card turns, grows and rises into place.
                    var tilted = Pale(CardLeft - 7, CardTop + 5, CardWidth, CardHeight, new CornerRadius(4));
                    tilted.RenderTransform = new RotateTransform { Angle = -20 };
                    tilted.RenderTransformOrigin = new Point(0.5, 0.5);
                    hints.Children.Add(tilted);
                    Place(card, CardLeft, CardTop);
                    Loop(transform, "TranslateY", 14, 0, fadeIn);
                    Loop(transform, "ScaleX", 0.6, 1, fadeIn);
                    Loop(transform, "ScaleY", 0.6, 1, fadeIn);
                    Loop(transform, "Rotation", -25, 0, fadeIn);
                    Loop(card, "Opacity", 0, 1, fadeIn);
                    break;

                default:
                    Place(card, CardLeft, CardTop);
                    Loop(card, "Opacity", 0, 1, fadeIn);
                    break;
            }

            Children.Add(actor);
        }

        // From hidden to shown, then back, again and again.
        private void Loop(DependencyObject target, string property, double hidden, double shown, IEasingFunction appearing)
        {
            var animation = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = At(0), Value = hidden });
            animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = At(Appeared), Value = shown, EasingFunction = appearing });
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = At(Shown), Value = shown });
            animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = At(Disappeared), Value = hidden, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = At(LoopEnd), Value = hidden });
            Add(animation, target, property);
        }

        private void HideWhilePlaying(UIElement element) => Set(element, 0);

        private void ShowWhilePlaying(UIElement element) => Set(element, 1);

        private void Set(UIElement element, double opacity)
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = At(0), Value = opacity });
            Add(animation, element, "Opacity");
        }

        private void Add(Timeline animation, DependencyObject target, string property)
        {
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            _loop.Children.Add(animation);
        }

        private static KeyTime At(double seconds) => KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds));

        private static void Place(UIElement element, double left, double top)
        {
            SetLeft(element, left);
            SetTop(element, top);
        }

        private static Border NewCard()
        {
            return new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                CornerRadius = new CornerRadius(4),
                Background = CardBrush(1),
                Child = new Border
                {
                    Width = 12,
                    Height = 3,
                    CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(4, 0, 0, 4),
                },
            };
        }

        private static Brush CardBrush(double opacity) => new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            Opacity = opacity,
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(255, 0x6D, 0x6D, 0xE8), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(255, 0xB4, 0x5B, 0xD6), Offset = 1 },
            },
        };

        private static Border Stripe(int index, double opacity, CornerRadius corners)
        {
            var stripe = new Border
            {
                Width = CardWidth / 3,
                Height = CardHeight,
                CornerRadius = corners,
                Background = new SolidColorBrush(CardColor) { Opacity = opacity },
            };
            Place(stripe, CardLeft + index * CardWidth / 3, CardTop);
            return stripe;
        }

        private static Border Halo(double spread, double opacity)
        {
            var halo = new Border
            {
                Width = CardWidth + 2 * spread,
                Height = CardHeight + 2 * spread,
                CornerRadius = new CornerRadius(4 + spread),
                Background = new SolidColorBrush(CardColor) { Opacity = opacity },
            };
            Place(halo, CardLeft - spread, CardTop - spread);
            return halo;
        }

        private static Border Pale(double left, double top, double width, double height, CornerRadius corners)
        {
            var pale = new Border { Width = width, Height = height, CornerRadius = corners, Background = CardBrush(0.3) };
            Place(pale, left, top);
            return pale;
        }

        private static Border Solid(double left, double top, double width, double height, CornerRadius corners)
        {
            var solid = new Border { Width = width, Height = height, CornerRadius = corners, Background = CardBrush(1) };
            Place(solid, left, top);
            return solid;
        }

        private static Path Arrow(string data) => new Path
        {
            Data = (Geometry)new GeometryConverter().ConvertFrom(data),
            Stroke = (Brush)Application.Current.Resources["AccentBrush"],
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
    }
}
