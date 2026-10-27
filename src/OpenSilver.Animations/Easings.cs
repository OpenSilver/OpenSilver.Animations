using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Media.Animation;

namespace OpenSilver.Animations
{
    /// <summary>
    /// The curves that say how an animation speeds up and slows down. In XAML, write their
    /// names: <c>Easing=Gentle</c>.
    /// </summary>
    public static class Easings
    {
        /// <summary>
        /// Starts fast and slows down softly. This is the default.
        /// </summary>
        public static IEasingFunction Smooth { get; } = new CubicBezierEasing("Smooth", 0.2, 0, 0, 1);

        /// <summary>
        /// Speeds up, then slows down.
        /// </summary>
        public static IEasingFunction Gentle { get; } = new CubicBezierEasing("Gentle", 0.45, 0, 0.55, 1);

        /// <summary>
        /// Constant speed.
        /// </summary>
        public static IEasingFunction Linear { get; } = new LinearEasing();

        /// <summary>
        /// Overshoots and settles like a spring. The amount of bounce comes from the <c>Bounciness</c>
        /// of the effect that uses it.
        /// </summary>
        public static IEasingFunction Bouncy { get; } = new SpringEasing("Bouncy", 0.5);

        internal static string GetName(IEasingFunction easing) => easing switch
        {
            null => null,
            CubicBezierEasing cb => cb.Name,
            LinearEasing => "Linear",
            SpringEasing s => s.Name,
            _ => easing.GetType().Name,
        };

        internal static bool TryGet(string name, out IEasingFunction easing)
        {
            switch (name?.Trim().ToLowerInvariant())
            {
                case "smooth":
                    easing = Smooth;
                    return true;
                case "gentle":
                    easing = Gentle;
                    return true;
                case "linear":
                    easing = Linear;
                    return true;
                case "bouncy":
                    easing = Bouncy;
                    return true;
                default:
                    easing = null;
                    return false;
            }
        }
    }

    /// <summary>
    /// Converts the names Smooth, Gentle, Linear and Bouncy to an easing function.
    /// </summary>
    public sealed class EasingConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
            {
                if (Easings.TryGet(s, out IEasingFunction easing))
                {
                    return easing;
                }

                throw new FormatException($"'{s}' is not an easing. Use Smooth, Gentle, Linear or Bouncy.");
            }

            return base.ConvertFrom(context, culture, value);
        }
    }

    internal sealed class LinearEasing : IEasingFunction
    {
        public double Ease(double normalizedTime) => normalizedTime;

        public override string ToString() => "Linear";
    }

    internal sealed class CubicBezierEasing : IEasingFunction
    {
        private readonly double _x1, _y1, _x2, _y2;

        public CubicBezierEasing(string name, double x1, double y1, double x2, double y2)
        {
            Name = name;
            _x1 = x1;
            _y1 = y1;
            _x2 = x2;
            _y2 = y2;
            Css = FormattableString.Invariant($"cubic-bezier({x1}, {y1}, {x2}, {y2})");
        }

        public string Name { get; }

        public string Css { get; }

        public double Ease(double normalizedTime)
        {
            double x = Math.Max(0, Math.Min(1, normalizedTime));
            return Bezier(SolveT(x), _y1, _y2);
        }

        private double SolveT(double x)
        {
            double t = x;
            for (int i = 0; i < 8; i++)
            {
                double err = Bezier(t, _x1, _x2) - x;
                if (Math.Abs(err) < 1e-6)
                {
                    return t;
                }

                double d = BezierSlope(t, _x1, _x2);
                if (Math.Abs(d) < 1e-6)
                {
                    break;
                }

                t -= err / d;
            }

            double lo = 0, hi = 1;
            t = x;
            for (int i = 0; i < 30; i++)
            {
                double v = Bezier(t, _x1, _x2);
                if (Math.Abs(v - x) < 1e-6)
                {
                    break;
                }

                if (v < x)
                {
                    lo = t;
                }
                else
                {
                    hi = t;
                }

                t = (lo + hi) / 2;
            }

            return t;
        }

        private static double Bezier(double t, double p1, double p2)
        {
            double u = 1 - t;
            return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
        }

        private static double BezierSlope(double t, double p1, double p2)
        {
            double u = 1 - t;
            return 3 * u * u * p1 + 6 * u * t * (p2 - p1) + 3 * t * t * (1 - p2);
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// A damped spring that reaches its target at the end of the normalized time.
    /// </summary>
    internal sealed class SpringEasing : IEasingFunction
    {
        private readonly double _zeta;
        private readonly double _omega;
        private readonly double _omegaD;

        public SpringEasing(string name, double bounciness)
            : this(name, bounciness, b => 1 - 0.8 * b, 1000)
        {
        }

        private SpringEasing(string name, double bounciness, Func<double, double> dampingRatio, double settle)
        {
            Name = name;
            Bounciness = Math.Max(0.1, Math.Min(1, bounciness));
            _zeta = dampingRatio(Bounciness);
            _omega = Math.Log(settle) / _zeta;
            _omegaD = _omega * Math.Sqrt(1 - _zeta * _zeta);
        }

        /// <summary>
        /// The spring of hover and press reactions. They change the element by a few pixels, so
        /// the spring of moves would overshoot by less than a pixel: this one overshoots by a third
        /// of the change at the default bounciness. It settles to 2% of the change, which is less
        /// than a pixel there, so that it doesn't ring faster when it bounces more.
        /// </summary>
        public static SpringEasing ForReaction(double bounciness) =>
            new SpringEasing("Bouncy", bounciness, b => 0.45 - 0.25 * b, 50);

        public string Name { get; }

        public double Bounciness { get; }

        public double Ease(double normalizedTime)
        {
            double t = Math.Max(0, Math.Min(1, normalizedTime));
            if (t >= 1)
            {
                return 1;
            }

            double envelope = Math.Exp(-_zeta * _omega * t);
            return 1 - envelope * (Math.Cos(_omegaD * t) + _zeta * _omega / _omegaD * Math.Sin(_omegaD * t));
        }

        public override string ToString() => Name;
    }
}
