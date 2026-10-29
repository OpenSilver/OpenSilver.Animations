using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenSilver.Animations.Gallery.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter09 : Page
    {
        private static readonly string[] Words = { "the web", "desktops", "phones", "tablets" };

        private readonly Random _random = new Random();
        private readonly DispatcherTimer _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private int _count = 8;
        private int _wordIndex;
        private double _revenue = 12480;
        private double _visitors = 3204;
        private double _conversion = 4.2;
        private readonly bool _ready;

        public Chapter09()
        {
            InitializeComponent();
            _clock.Tick += (s, e) => ShowTime();
            UpdateLetterEffects();
            _ready = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) => StopClock();

        private void AddOne_Click(object sender, RoutedEventArgs e) => SetCount(_count + 1);

        private void AddTen_Click(object sender, RoutedEventArgs e) => SetCount(_count + 10);

        private void RemoveOne_Click(object sender, RoutedEventArgs e) => SetCount(Math.Max(0, _count - 1));

        private void SetCount(int count)
        {
            _count = count;
            BadgeCount.Text = count.ToString(CultureInfo.InvariantCulture);
        }

        private void RollFrom_Checked(object sender, RoutedEventArgs e)
        {
            if (!_ready)
            {
                return;
            }

            string from = (string)((RadioButton)sender).Tag;
            Animate.SetTextChange(BadgeCount, string.IsNullOrEmpty(from)
                ? new Roll()
                : new Roll { From = (Side)Enum.Parse(typeof(Side), from) });

            string roll = Snippet.Effect("Roll", string.IsNullOrEmpty(from) ? null : "From=" + from);
            BadgeDemo.Xaml =
                "<Border CornerRadius=\"10\" anim:Animate.MoveAndResize=\"Smooth\">\n" +
                "    <TextBlock Text=\"{Binding UnreadCount}\" anim:Animate.TextChange=\"" + roll + "\" />\n" +
                "</Border>";
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _revenue = Math.Round(_revenue * (0.6 + _random.NextDouble() * 0.9));
            _visitors = Math.Round(_visitors * (0.6 + _random.NextDouble() * 0.9));
            _conversion = Math.Round(1 + _random.NextDouble() * 8, 1);
            RevenueText.Text = "$" + _revenue.ToString("N0", CultureInfo.InvariantCulture);
            VisitorsText.Text = _visitors.ToString("N0", CultureInfo.InvariantCulture);
            ConversionText.Text = _conversion.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private void ClockButton_Click(object sender, RoutedEventArgs e)
        {
            if (_clock.IsEnabled)
            {
                StopClock();
                return;
            }

            ShowTime();
            _clock.Start();
            ClockButton.Content = "Stop";
        }

        private void StopClock()
        {
            _clock.Stop();
            ClockButton.Content = "Start the clock";
        }

        private void ShowTime() => ClockText.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        private void NextWord_Click(object sender, RoutedEventArgs e)
        {
            _wordIndex = (_wordIndex + 1) % Words.Length;
            string text = "Build apps for " + Words[_wordIndex];
            RetypeText.Text = text;
            CrossFadeText.Text = text;
            ScrambleText.Text = text;
        }

        private void LetterOption_Checked(object sender, RoutedEventArgs e)
        {
            if (_ready)
            {
                UpdateLetterEffects();
            }
        }

        private void UpdateLetterEffects()
        {
            if (FadeLettersChoice.IsChecked == true)
            {
                Animate.SetAppear(LetterMessage, new FadeLetters());
                Animate.SetDisappear(LetterMessage, new FadeLetters());
                ShowLettersXaml("FadeLetters", "FadeLetters");
                return;
            }

            double speed = SlowChoice.IsChecked == true ? 15 : FastChoice.IsChecked == true ? 60 : 30;
            bool cursor = CursorChoice.IsChecked == true;
            var typewriter = new Typewriter { Speed = speed, Cursor = cursor };
            var backspace = new Backspace { Speed = speed, Cursor = cursor };
            string typewriterBlink = null;
            string backspaceBlink = null;
            if (cursor && NoBlinkChoice.IsChecked == true)
            {
                typewriter.BlinkFirst = AnimationTime.Zero;
                typewriterBlink = "BlinkFirst=0s";
            }
            else if (cursor && LongBlinkChoice.IsChecked == true)
            {
                typewriter.BlinkFirst = AnimationTime.FromSeconds(1.5);
                backspace.BlinkFirst = AnimationTime.FromSeconds(1.5);
                typewriterBlink = backspaceBlink = "BlinkFirst=1.5s";
            }

            Animate.SetAppear(LetterMessage, typewriter);
            Animate.SetDisappear(LetterMessage, backspace);

            string speedParameter = speed == 30 ? null : "Speed=" + Snippet.Number(speed);
            string cursorParameter = cursor ? null : "Cursor=False";
            ShowLettersXaml(
                Snippet.Effect("Typewriter", speedParameter, cursorParameter, typewriterBlink),
                Snippet.Effect("Backspace", speedParameter, cursorParameter, backspaceBlink));
        }

        private void ShowLettersXaml(string appear, string disappear)
        {
            LettersDemo.Xaml =
                "<TextBlock Text=\"Your order has shipped.\"\n" +
                "           anim:Animate.Appear=\"" + appear + "\"\n" +
                "           anim:Animate.Disappear=\"" + disappear + "\" />";
        }

        private void LetterButton_Click(object sender, RoutedEventArgs e)
        {
            bool show = LetterMessage.Visibility != Visibility.Visible;
            LetterMessage.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            LetterButton.Content = show ? "Hide the message" : "Show the message";
        }
    }
}
