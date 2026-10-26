using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter02 : Page
    {
        private const string CustomParameters = "Opacity=0, OffsetY=20, Zoom=0.8, Rotate=-6";

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
            var side = (Side)Enum.Parse(typeof(Side), (string)Selected(SideChoices).Content);
            string duration = (string)Selected(DurationChoices).Tag;
            string easing = (string)Selected(EasingChoices).Content;

            bool hasSide = name == "Slide" || name == "Wipe";
            SideLabel.Visibility = SideChoices.Visibility = hasSide ? Visibility.Visible : Visibility.Collapsed;

            bool hasEasing = name != "Pop";
            EasingChoices.IsEnabled = hasEasing;
            EasingLabel.Opacity = EasingChoices.Opacity = hasEasing ? 1 : 0.45;

            // Pop is for appearing: the card disappears with Zoom.
            string disappearName = name == "Pop" ? "Zoom" : name;
            Transition appear = Create(name, side, appear: true, duration, hasEasing ? easing : "Smooth");
            Transition disappear = Create(disappearName, side, appear: false, duration, hasEasing ? easing : "Smooth");
            Animate.SetAppear(SampleCard, appear);
            Animate.SetDisappear(SampleCard, disappear);

            EffectDemo.Xaml =
                "<Border anim:Animate.Appear=\"" + ToXaml(name, side, true, duration, hasEasing ? easing : "Smooth") + "\"\n" +
                "        anim:Animate.Disappear=\"" + ToXaml(disappearName, side, false, duration, hasEasing ? easing : "Smooth") + "\">";
        }

        private static Transition Create(string name, Side side, bool appear, string duration, string easing)
        {
            Transition effect;
            switch (name)
            {
                case "Slide":
                    effect = appear ? new Slide { From = side } : new Slide { To = side };
                    break;
                case "Wipe":
                    effect = appear ? new Wipe { From = side } : new Wipe { To = side };
                    break;
                case "Custom":
                    effect = new Custom { Opacity = 0, OffsetY = 20, Zoom = 0.8, Rotate = -6 };
                    break;
                default:
                    effect = Transition.Parse(name);
                    break;
            }

            if (duration.Length > 0)
            {
                effect.Duration = AnimationTime.Parse(duration);
            }

            effect.Easing = GetEasing(easing);
            return effect;
        }

        private static IEasingFunction GetEasing(string name)
        {
            switch (name)
            {
                case "Gentle": return Easings.Gentle;
                case "Linear": return Easings.Linear;
                case "Bouncy": return Easings.Bouncy;
                default: return null;
            }
        }

        private static string ToXaml(string name, Side side, bool appear, string duration, string easing)
        {
            var parameters = new List<string>();
            if ((name == "Slide" || name == "Wipe") && side != Side.Bottom)
            {
                parameters.Add((appear ? "From=" : "To=") + side);
            }
            if (name == "Custom")
            {
                parameters.Add(CustomParameters);
            }
            if (duration.Length > 0)
            {
                parameters.Add("Duration=" + duration);
            }
            if (easing != "Smooth")
            {
                parameters.Add("Easing=" + easing);
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
