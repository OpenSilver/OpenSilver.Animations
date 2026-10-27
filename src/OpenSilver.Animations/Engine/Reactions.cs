using System;
using System.ComponentModel;
using System.Text;
using System.Windows;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Sends the Hover and Press of elements to the script, which plays them on the pointer events.
    /// They don't go through the layout: an element that reacts keeps its place and its size, and
    /// its reaction plays on top of its moves.
    /// </summary>
    internal static class Reactions
    {
        private const int KindNudge = 1;
        private const int KindZoom = 2;
        private const int KindLift = 3;

        private const double MaxNudgeDistance = 20;

        private static bool _started;
        private static AnimationSettings _settings;

        public static void OnChanged(UIElement element, Reaction reaction, bool press)
        {
            if (DesignerProperties.IsInDesignTool)
            {
                return;
            }

            EnsureStarted();
            Check(element, reaction, press);

            bool reacts = Animate.GetHover(element) != null || Animate.GetPress(element) != null;
            element.IsEnabledChanged -= OnIsEnabledChanged;
            if (element is FrameworkElement fe)
            {
                fe.Loaded -= OnLoaded;
            }

            if (reacts)
            {
                element.IsEnabledChanged += OnIsEnabledChanged;
                if (element is FrameworkElement fe2)
                {
                    fe2.Loaded += OnLoaded;
                }
            }

            Send(element);
        }

        /// <summary>
        /// The application set other settings.
        /// </summary>
        public static void OnSettingsReplaced()
        {
            if (_started)
            {
                Watch(AnimationSettings.Current);
            }
        }

        private static void EnsureStarted()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            BrowserBridge.EnsureInitialized();
            Watch(AnimationSettings.Current);
        }

        private static void Watch(AnimationSettings settings)
        {
            if (_settings != null)
            {
                _settings.PropertyChanged -= OnSettingChanged;
            }

            _settings = settings;
            _settings.PropertyChanged += OnSettingChanged;
            SendSettings();
        }

        private static void OnSettingChanged(object sender, PropertyChangedEventArgs e) => SendSettings();

        private static void SendSettings()
        {
            AnimationSettings s = _settings;
            double defaultMs = Math.Max(0.05, Math.Min(3, s.DefaultDuration.Seconds)) * 1000;
            BrowserBridge.SetReactionSettings(s.IsEnabled, s.Speed, defaultMs, s.RespectReducedMotion, s.SimulateReducedMotion);
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) => Send((UIElement)sender);

        private static void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) => Send((UIElement)sender);

        private static void Send(UIElement element)
        {
            string id = Interop.GetDiv(element).Uid;
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            Reaction hover = Animate.GetHover(element);
            Reaction press = Animate.GetPress(element);
            BrowserBridge.React(id, SpecOf(hover, press: false), SpecOf(press, press: true), element.IsEnabled);
        }

        /// <summary>
        /// The reaction as the script reads it, or null when nothing happens.
        /// </summary>
        private static string SpecOf(Reaction reaction, bool press)
        {
            if (reaction == null || reaction.IsNone || (press && reaction is Nudge))
            {
                return null;
            }

            var sb = new StringBuilder("{k:");
            switch (reaction)
            {
                case Nudge nudge:
                    sb.Append(KindNudge).Append(",dist:");
                    BrowserBridge.AppendNumber(sb, nudge.Distance.IsPercent ? Nudge.DefaultPixels : Math.Min(MaxNudgeDistance, nudge.Distance.Pixels));
                    sb.Append(",a:");
                    BrowserBridge.AppendNumber(sb, nudge.Tilt);
                    break;
                case Grow grow:
                    sb.Append(KindZoom).Append(",z:");
                    BrowserBridge.AppendNumber(sb, grow.Zoom);
                    break;
                case Shrink shrink:
                    sb.Append(KindZoom).Append(",z:");
                    BrowserBridge.AppendNumber(sb, shrink.Zoom);
                    break;
                case Lift lift:
                    sb.Append(KindLift).Append(",dist:");
                    BrowserBridge.AppendNumber(sb, lift.Distance.IsPercent ? Lift.DefaultPixels : lift.Distance.Pixels);
                    break;
                default:
                    return null;
            }

            sb.Append(",b:");
            BrowserBridge.AppendNumber(sb, reaction.Bounciness ?? 0.5);
            if (reaction.IsBouncy)
            {
                sb.Append(",bo:1");
            }

            sb.Append(",t:");
            Timing.FromReaction(reaction).AppendJson(sb);
            return sb.Append('}').ToString();
        }

        private static void Check(UIElement element, Reaction reaction, bool press)
        {
            if (reaction == null || reaction.IsNone)
            {
                return;
            }

            string problem = null;
            if (press && reaction is Nudge)
            {
                problem = "Nudge is for Hover only, so nothing happens when it is pressed. Use Shrink, Grow or Lift";
            }
            else if (reaction is Nudge && reaction.Easing != null)
            {
                problem = "Easing is not used by Nudge, which always springs back";
            }
            else if (reaction.Bounciness != null && !(reaction is Nudge) && !reaction.IsBouncy && !(reaction.Easing is OvershootEasing))
            {
                problem = "Bounciness is only used by Nudge, Easing=Bouncy and Easing=Overshoot";
            }
            else if ((reaction as Lift)?.Distance.IsPercent == true || (reaction as Nudge)?.Distance.IsPercent == true)
            {
                problem = "A Distance in percent is for Slide only, so it moves by its default distance";
            }

            if (problem != null)
            {
                LayoutAnimator.Warn(press ? "Press" : "Hover", element, problem);
            }
        }
    }
}
