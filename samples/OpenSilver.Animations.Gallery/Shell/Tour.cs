using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace OpenSilver.Animations.Gallery.Shell
{
    /// <summary>
    /// A chapter of the tour, as shown in the list of chapters.
    /// </summary>
    public sealed class ChapterInfo : INotifyPropertyChanged
    {
        private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromArgb(255, 0x5B, 0x5B, 0xD6));
        private static readonly Brush AccentLightBrush = new SolidColorBrush(Color.FromArgb(255, 0xEC, 0xEC, 0xFD));
        private static readonly Brush NumberBrush = new SolidColorBrush(Color.FromArgb(255, 0xEE, 0xEF, 0xF5));
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromArgb(255, 0x1C, 0x1E, 0x2B));
        private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromArgb(255, 0x9A, 0x9E, 0xB0));
        private static readonly Brush TransparentBrush = new SolidColorBrush(Colors.Transparent);
        private static readonly Brush WhiteBrush = new SolidColorBrush(Colors.White);

        private bool _isCurrent;

        public ChapterInfo(int number, string title, string summary, bool isReady)
        {
            Number = number;
            Title = title;
            Summary = summary;
            IsReady = isReady;
        }

        public int Number { get; }

        public string Title { get; }

        public string Summary { get; }

        public bool IsReady { get; }

        public string Path => "/chapter/" + Number;

        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent != value)
                {
                    _isCurrent = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                }
            }
        }

        public Brush Background => IsCurrent ? AccentLightBrush : TransparentBrush;

        public Brush NumberBackground => IsCurrent ? AccentBrush : NumberBrush;

        public Brush NumberForeground => IsCurrent ? WhiteBrush : (IsReady ? TextBrush : MutedBrush);

        public Brush TitleForeground => IsReady || IsCurrent ? TextBrush : MutedBrush;

        public FontWeight TitleWeight => IsCurrent ? FontWeights.SemiBold : FontWeights.Normal;

        public Visibility SoonVisibility => IsReady ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public static class Tour
    {
        public static IReadOnlyList<ChapterInfo> All { get; } = new[]
        {
            new ChapterInfo(1, "Your first animation", "Appear and Disappear set to Fade on a details panel: nothing jumps, and the elements below slide by themselves.", true),
            new ChapterInfo(2, "Pick an effect", "Every Appear and Disappear effect, with From, Distance, Duration and Easing.", true),
            new ChapterInfo(3, "When the others move", "Other elements move while, before or after an element appears or disappears.", true),
            new ChapterInfo(4, "Page intros", "StartAfter and OnPageOpen on a profile, an inbox and an order.", true),
            new ChapterInfo(5, "Smooth moves and resizes", "MoveAndResize on a button, a badge, a sidebar, a dialog, an image card and a search box.", true),
            new ChapterInfo(6, "Lists", "ItemAppear, ItemDisappear, ItemMoveAndResize and ItemCascade on a to-do list, toasts, a chat and a long list.", true),
            new ChapterInfo(7, "Everything inside", "ApplyTo=EverythingInside on a form and on a responsive layout.", true),
            new ChapterInfo(8, "When the window or a panel is resized", "Elements that glide while you resize the window, and layouts that switch.", true),
            new ChapterInfo(9, "Text that moves", "TextChange on a badge, a dashboard, a clock and a tagline, and Typewriter, Backspace and FadeLetters on a message.", true),
            new ChapterInfo(10, "Bouncy and Wave", "A bouncy tab underline, and cards that move like a wave.", true),
            new ChapterInfo(11, "Magic move", "Elements that travel from one place to another.", false),
            new ChapterInfo(12, "Hover and press", "Hover and Press on buttons, cards and icons: Grow, Lift, Shrink and Nudge.", true),
            new ChapterInfo(13, "From code", "Animate.Changes, Animate.Skip and AnimationSettings.", true),
            new ChapterInfo(14, "What never animates, and why", "Scrolling, dragging, typing, resizing the window, and the limits.", true),
            new ChapterInfo(15, "Putting it together", "The OpenSilver Showcase: a small copy of its window, and how each of its animations is made.", true),
        };

        public static ChapterInfo Get(int number) => All.FirstOrDefault(c => c.Number == number);

        public static ChapterInfo PreviousReady(int number) => All.LastOrDefault(c => c.IsReady && c.Number < number);

        public static ChapterInfo NextReady(int number) => All.FirstOrDefault(c => c.IsReady && c.Number > number);
    }
}
