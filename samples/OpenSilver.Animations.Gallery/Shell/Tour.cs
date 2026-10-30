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
        private static readonly Brush NumberBrush = new SolidColorBrush(Color.FromArgb(255, 0xEE, 0xEF, 0xF5));
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromArgb(255, 0x1C, 0x1E, 0x2B));
        private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromArgb(255, 0x9A, 0x9E, 0xB0));
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
            new ChapterInfo(2, "Magic move", "Steps that flow into each other, a text box that opens into a popup, toppings that change lists, a selection pill and a photo that opens where it is.", true),
            new ChapterInfo(3, "Smooth moves and resizes", "MoveAndResize on a button and a badge, then on everything inside a form, cards that move like a wave, and a panel that you resize.", true),
            new ChapterInfo(4, "Page intros", "StartAfter and OnPageOpen on a profile, an order and an inbox.", true),
            new ChapterInfo(5, "Hover and press", "Hover and Press on icons, buttons and cards: Nudge, Grow, Lift and Shrink.", true),
            new ChapterInfo(6, "Lists", "ItemsAppear, ItemsDisappear, ItemsMoveAndResize and ItemsCascade on a to-do list, toasts, a chat and a long list.", true),
            new ChapterInfo(7, "More moves and resizes", "Two columns that become one, a bouncy tab underline, a sidebar, a dialog, an image card, a search box and a live chart.", true),
            new ChapterInfo(8, "Text that moves", "TextChange on a badge, a tagline, a dashboard and a clock, and Typewriter, Backspace and FadeLetters on a message.", true),
            new ChapterInfo(9, "Pick an effect", "Every Appear and Disappear effect, with From, Distance, Duration and Easing.", true),
            new ChapterInfo(10, "Putting it together", "The OpenSilver Showcase: a small copy of its window, and how each of its animations is made.", true),
            new ChapterInfo(11, "When the others move", "Other elements move while, before or after an element appears or disappears.", true),
            new ChapterInfo(12, "When the window is resized", "Elements that glide while you resize the window, and layouts that switch.", true),
            new ChapterInfo(13, "From code", "Animate.Changes, Animate.Skip and AnimationSettings.", true),
            new ChapterInfo(14, "What never animates, and why", "Scrolling, dragging, typing, resizing the window, and the limits.", true),
        };

        public static ChapterInfo Get(int number) => All.FirstOrDefault(c => c.Number == number);

        public static ChapterInfo PreviousReady(int number) => All.LastOrDefault(c => c.IsReady && c.Number < number);

        public static ChapterInfo NextReady(int number) => All.FirstOrDefault(c => c.IsReady && c.Number > number);
    }
}
