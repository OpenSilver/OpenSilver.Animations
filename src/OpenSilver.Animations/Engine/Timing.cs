using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media.Animation;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// The duration and curve shared by all the elements of an animated group, or of an effect.
    /// </summary>
    internal sealed class Timing
    {
        private const int CurveSamples = 32;
        private const int SpringSamples = 64;

        private Timing() { }

        public double DurationMs { get; private set; }

        /// <summary>
        /// A CSS easing, when the curve has one.
        /// </summary>
        public string Easing { get; private set; }

        /// <summary>
        /// The curve sampled at regular intervals, when it has no CSS equivalent.
        /// </summary>
        public double[] Samples { get; private set; }

        /// <summary>
        /// The CSS easing used for sizes, when it differs from the one used for moves.
        /// </summary>
        public string SizeEasing { get; private set; }

        /// <summary>
        /// The CSS easing used for the opacity, when it differs from the curve: an effect that
        /// bounces or overshoots fades in or out without bouncing.
        /// </summary>
        public string FadeEasing { get; private set; }

        public double Wave { get; private set; }

        public string Description { get; private set; }

        public static Timing FromMovement(Movement movement, AnimationSettings settings)
        {
            bool bouncy = movement is Bouncy;
            double seconds = movement.Duration?.Seconds ?? settings.DefaultDuration.Seconds * (bouncy ? 2 : 1);

            var timing = new Timing
            {
                DurationMs = Clamp(seconds) * 1000 / settings.Speed,
                Wave = movement.Wave,
            };

            if (bouncy)
            {
                double bounciness = movement.Bounciness ?? 0.5;
                timing.Samples = Sample(new SpringEasing("Bouncy", bounciness), SpringSamples);
                timing.SizeEasing = ((CubicBezierEasing)Easings.Smooth).Css;
                timing.Description = Invariant($"Bouncy, {Math.Round(timing.DurationMs / 1000, 2)} s");
            }
            else
            {
                IEasingFunction easing = movement.Easing ?? Easings.Smooth;
                timing.SetCurve(easing, movement.Bounciness ?? 0.5);
                timing.Description = Invariant($"{Easings.GetName(easing)}, {Math.Round(timing.DurationMs / 1000, 2)} s");
            }

            return timing;
        }

        /// <summary>
        /// The timing of the effect itself.
        /// </summary>
        public static Timing FromEffect(Transition effect, AnimationSettings settings)
        {
            bool bouncy = effect.IsBouncy;
            double seconds = effect.Duration?.Seconds ?? settings.DefaultDuration.Seconds * (bouncy ? 2 : 1);
            var timing = new Timing { DurationMs = Clamp(seconds) * 1000 / settings.Speed };

            if (bouncy)
            {
                timing.Samples = Sample(new SpringEasing("Bouncy", effect.Bounciness ?? 0.5), SpringSamples);
                timing.FadeEasing = ((CubicBezierEasing)Easings.Smooth).Css;
                timing.Description = Invariant($"Bouncy, {Math.Round(timing.DurationMs / 1000, 2)} s");
            }
            else
            {
                IEasingFunction easing = effect.Easing ?? Easings.Smooth;
                timing.SetCurve(easing, effect.Bounciness ?? 0.5);
                if (easing is OvershootEasing)
                {
                    timing.FadeEasing = ((CubicBezierEasing)Easings.EaseOut).Css;
                }
                timing.Description = Invariant($"{Easings.GetName(easing)}, {Math.Round(timing.DurationMs / 1000, 2)} s");
            }

            return timing;
        }

        /// <summary>
        /// The timing of the elements that move because of an effect: the same, over the
        /// OthersDuration of the effect when it has one, except that they don't bounce or
        /// overshoot: they move with Smooth when the effect bounces, and with EaseOut when it
        /// overshoots.
        /// </summary>
        public static Timing ForOthers(Timing effectTiming, Transition effect, AnimationSettings settings)
        {
            bool overshoots = effect.Easing is OvershootEasing;
            if (!effect.IsBouncy && !overshoots && effect.OthersDuration == null)
            {
                return effectTiming;
            }

            var timing = new Timing
            {
                DurationMs = effect.OthersDuration is AnimationTime duration
                    ? Clamp(duration.Seconds) * 1000 / settings.Speed
                    : effectTiming.DurationMs,
                Wave = effectTiming.Wave,
            };
            IEasingFunction easing = effect.IsBouncy ? Easings.Smooth
                : overshoots ? Easings.EaseOut
                : effect.Easing ?? Easings.Smooth;
            timing.SetCurve(easing, effect.Bounciness ?? 0.5);
            timing.Description = Invariant($"{Easings.GetName(easing)}, {Math.Round(timing.DurationMs / 1000, 2)} s");
            return timing;
        }

        /// <summary>
        /// The short fade that replaces effects when the device asks for reduced motion.
        /// </summary>
        public static Timing QuickFade()
        {
            var timing = new Timing { DurationMs = 150 };
            timing.SetCurve(Easings.Smooth, 0.5);
            timing.Description = "Smooth, 0.15 s";
            return timing;
        }

        /// <summary>
        /// The timing of a hover or press reaction. The script applies the speed, and the default
        /// duration when <see cref="DurationMs"/> is 0, when it plays: they can change at any time.
        /// </summary>
        public static Timing FromReaction(Reaction reaction)
        {
            var timing = new Timing { DurationMs = reaction.Duration is AnimationTime duration ? Clamp(duration.Seconds) * 1000 : 0 };
            if (reaction.IsBouncy)
            {
                timing.Samples = Sample(SpringEasing.ForReaction(reaction.Bounciness ?? 0.5), SpringSamples);
            }
            else
            {
                timing.SetCurve(reaction.Easing ?? Easings.Smooth, reaction.Bounciness ?? 0.5);
            }
            return timing;
        }

        public static Timing FromDuration(AnimationTime? duration, AnimationSettings settings)
        {
            double seconds = duration?.Seconds ?? settings.DefaultDuration.Seconds;
            var timing = new Timing { DurationMs = Clamp(seconds) * 1000 / settings.Speed };
            timing.SetCurve(Easings.Smooth, 0.5);
            timing.Description = Invariant($"Smooth, {Math.Round(timing.DurationMs / 1000, 2)} s");
            return timing;
        }

        private void SetCurve(IEasingFunction easing, double bounciness)
        {
            switch (easing)
            {
                case CubicBezierEasing cb:
                    Easing = cb.Css;
                    break;
                case LinearEasing:
                    Easing = "linear";
                    break;
                case SpringEasing:
                    Samples = Sample(new SpringEasing("Bouncy", bounciness), SpringSamples);
                    break;
                case OvershootEasing o:
                    Samples = Sample(new OvershootEasing(o.Name, bounciness), CurveSamples);
                    break;
                default:
                    Samples = Sample(easing, CurveSamples);
                    break;
            }
        }

        public void AppendJson(StringBuilder sb)
        {
            sb.Append("{d:");
            BrowserBridge.AppendNumber(sb, DurationMs);
            if (Easing != null)
            {
                sb.Append(",e:");
                BrowserBridge.AppendString(sb, Easing);
            }
            if (Samples != null)
            {
                sb.Append(",s:[");
                for (int i = 0; i < Samples.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(Math.Round(Samples[i], 4).ToString(CultureInfo.InvariantCulture));
                }
                sb.Append(']');
            }
            if (SizeEasing != null)
            {
                sb.Append(",se:");
                BrowserBridge.AppendString(sb, SizeEasing);
            }
            if (FadeEasing != null)
            {
                sb.Append(",fe:");
                BrowserBridge.AppendString(sb, FadeEasing);
            }
            sb.Append('}');
        }

        private static double[] Sample(IEasingFunction easing, int count)
        {
            var samples = new double[count + 1];
            for (int i = 0; i <= count; i++)
            {
                double v;
                try
                {
                    v = easing.Ease((double)i / count);
                }
                catch
                {
                    v = (double)i / count;
                }

                samples[i] = double.IsNaN(v) || double.IsInfinity(v) ? (double)i / count : v;
            }

            samples[0] = 0;
            samples[count] = 1;
            return samples;
        }

        private static double Clamp(double seconds) => Math.Max(0.05, Math.Min(3, seconds));

        private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
    }
}
