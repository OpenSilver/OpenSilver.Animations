using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter02 : Page
    {
        private const string CustomParameters = "Opacity=0, OffsetY=20, Zoom=0.8, Rotate=-6";
        private const double DefaultZoomSize = 0.9;
        private const string DefaultDistance = "Medium";

        private readonly bool _ready;

        public Chapter02()
        {
            InitializeComponent();
            _ready = true;
            ApplyEffect();
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (_ready)
            {
                ApplyEffect();
            }
        }

        private void ApplyEffect()
        {
            string name = (string)Selected(EffectChoices).Content;
            var choices = new Choices
            {
                Side = (Side)Enum.Parse(typeof(Side), (string)Selected(SideChoices).Content),
                Distance = (string)Selected(DistanceChoices).Tag,
                Size = double.Parse((string)Selected(SizeChoices).Tag, CultureInfo.InvariantCulture),
                Fade = bool.Parse((string)Selected(FadeChoices).Tag),
                Duration = (string)Selected(DurationChoices).Tag,
                OthersDuration = (string)Selected(OthersDurationChoices).Tag,
                Easing = (string)Selected(EasingChoices).Content,
            };

            bool hasSide = name == "Slide" || name == "Wipe";
            SideLabel.Visibility = SideChoices.Visibility = hasSide ? Visibility.Visible : Visibility.Collapsed;

            bool hasDistance = name == "Slide";
            DistanceLabel.Visibility = DistanceChoices.Visibility = hasDistance ? Visibility.Visible : Visibility.Collapsed;

            bool hasSize = name == "Zoom";
            SizeLabel.Visibility = SizeChoices.Visibility = hasSize ? Visibility.Visible : Visibility.Collapsed;

            bool hasFade = name == "Slide" || name == "Zoom";
            FadeLabel.Visibility = FadeChoices.Visibility = hasFade ? Visibility.Visible : Visibility.Collapsed;

            bool hasEasing = name != "Pop";
            EasingChoices.IsEnabled = hasEasing;
            EasingLabel.Opacity = EasingChoices.Opacity = hasEasing ? 1 : 0.45;

            // Pop is for appearing: the card disappears with Zoom, at its default size.
            string disappearName = name == "Pop" ? "Zoom" : name;
            if (!hasSize)
            {
                choices.Size = DefaultZoomSize;
            }
            if (!hasDistance)
            {
                choices.Distance = DefaultDistance;
            }
            if (!hasFade)
            {
                choices.Fade = true;
            }
            if (!hasEasing)
            {
                choices.Easing = "Smooth";
            }
            Animate.SetAppear(SampleCard, Create(name, choices, appear: true));
            Animate.SetDisappear(SampleCard, Create(disappearName, choices, appear: false));

            EffectDemo.Xaml =
                "<Border anim:Animate.Appear=\"" + ToXaml(name, choices, true) + "\"\n" +
                "        anim:Animate.Disappear=\"" + ToXaml(disappearName, choices, false) + "\">";
        }

        private sealed class Choices
        {
            public Side Side;
            public string Distance;
            public double Size;
            public bool Fade;
            public string Duration;
            public string OthersDuration;
            public string Easing;
        }

        private static Transition Create(string name, Choices choices, bool appear)
        {
            Transition effect;
            switch (name)
            {
                case "Slide":
                    var slide = appear ? new Slide { From = choices.Side } : new Slide { To = choices.Side };
                    slide.Distance = Distance.Parse(choices.Distance);
                    slide.Fade = choices.Fade;
                    effect = slide;
                    break;
                case "Wipe":
                    effect = appear ? new Wipe { From = choices.Side } : new Wipe { To = choices.Side };
                    break;
                case "Zoom":
                    var zoom = choices.Size == DefaultZoomSize ? new Zoom() : appear ? new Zoom { From = choices.Size } : new Zoom { To = choices.Size };
                    zoom.Fade = choices.Fade;
                    effect = zoom;
                    break;
                case "Custom":
                    effect = new Custom { Opacity = 0, OffsetY = 20, Zoom = 0.8, Rotate = -6 };
                    break;
                default:
                    effect = Transition.Parse(name);
                    break;
            }

            if (choices.Duration.Length > 0)
            {
                effect.Duration = AnimationTime.Parse(choices.Duration);
            }
            if (choices.OthersDuration.Length > 0)
            {
                effect.OthersDuration = AnimationTime.Parse(choices.OthersDuration);
            }

            effect.Easing = GetEasing(choices.Easing);
            return effect;
        }

        private static IEasingFunction GetEasing(string name)
        {
            switch (name)
            {
                case "Gentle": return Easings.Gentle;
                case "Linear": return Easings.Linear;
                case "Bouncy": return Easings.Bouncy;
                case "EaseOut": return Easings.EaseOut;
                case "EaseIn": return Easings.EaseIn;
                case "EaseInOut": return Easings.EaseInOut;
                case "Overshoot": return Easings.Overshoot;
                default: return null;
            }
        }

        private static string ToXaml(string name, Choices choices, bool appear)
        {
            var parameters = new List<string>();
            if ((name == "Slide" || name == "Wipe") && choices.Side != Side.Bottom)
            {
                parameters.Add((appear ? "From=" : "To=") + choices.Side);
            }
            if (name == "Slide" && choices.Distance != DefaultDistance)
            {
                parameters.Add("Distance=" + choices.Distance);
            }
            if (name == "Zoom" && choices.Size != DefaultZoomSize)
            {
                parameters.Add((appear ? "From=" : "To=") + choices.Size.ToString(CultureInfo.InvariantCulture));
            }
            if ((name == "Slide" || name == "Zoom") && !choices.Fade)
            {
                parameters.Add("Fade=False");
            }
            if (name == "Custom")
            {
                parameters.Add(CustomParameters);
            }
            if (choices.Duration.Length > 0)
            {
                parameters.Add("Duration=" + choices.Duration);
            }
            if (choices.OthersDuration.Length > 0)
            {
                parameters.Add("OthersDuration=" + choices.OthersDuration);
            }
            if (choices.Easing != "Smooth")
            {
                parameters.Add("Easing=" + choices.Easing);
            }

            return parameters.Count == 0 ? name : "{anim:" + name + " " + string.Join(", ", parameters) + "}";
        }

        private static RadioButton Selected(Panel choices)
            => choices.Children.OfType<RadioButton>().First(r => r.IsChecked == true);

        private void SampleButton_Click(object sender, RoutedEventArgs e)
        {
            bool show = SampleCard.Visibility != Visibility.Visible;
            SampleCard.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            SampleButton.Content = show ? "Hide the card" : "Show the card";
        }

        private void SignUp_Click(object sender, RoutedEventArgs e)
        {
            EmailError.Visibility = IsValidEmail(EmailBox.Text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void EmailBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (EmailError != null && EmailError.Visibility == Visibility.Visible && IsValidEmail(EmailBox.Text))
            {
                EmailError.Visibility = Visibility.Collapsed;
            }
        }

        private static bool IsValidEmail(string text)
        {
            int at = text.IndexOf('@');
            int dot = text.LastIndexOf('.');
            return at > 0 && dot > at + 1 && dot < text.Length - 2;
        }
    }
}
