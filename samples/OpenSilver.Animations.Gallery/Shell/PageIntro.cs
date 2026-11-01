using System;
using System.Windows;
using System.Windows.Controls;
using OpenSilver.Animations.Gallery.Chapters;

namespace OpenSilver.Animations.Gallery.Shell
{
    /// <summary>
    /// The intro of the chapter pages, set from code on the elements of the page when it opens:
    /// the overline fades in letter by letter, the title rises out of a blur, and the paragraphs,
    /// the demos and the tips follow, one after the other. The demos inside them play their own
    /// Appear once their card has started.
    /// </summary>
    /// <remarks>
    /// The welcome page and chapter 4 keep their own intros, and an element with an Appear of its
    /// own, like the title of chapter 8, keeps it.
    /// </remarks>
    public static class PageIntro
    {
        private const double StepSeconds = 0.08;
        private const double LastStartSeconds = 0.6;

        /// <summary>
        /// Sets the Appear of the elements of a page that was just created. Returns how many of
        /// them appear, with the intro or with their own Appear, or 0 for a page left as it is.
        /// </summary>
        public static int Apply(Page page)
        {
            if (page is Welcome || page is PageIntrosChapter || !(page.Content is Panel root))
            {
                return 0;
            }

            int count = 0;
            foreach (UIElement child in root.Children)
            {
                if (child.Visibility != Visibility.Visible)
                {
                    continue;
                }

                if (Animate.GetAppear(child) == null)
                {
                    Animate.SetAppear(child, Create(child, AnimationTime.FromSeconds(Math.Min(count * StepSeconds, LastStartSeconds))));
                }
                count++;
            }

            return count;
        }

        private static Transition Create(UIElement element, AnimationTime startAfter)
        {
            Style style = (element as FrameworkElement)?.Style;
            if (style != null && style == Application.Current.Resources["OverlineText"])
            {
                return new FadeLetters { Duration = AnimationTime.FromSeconds(0.5), StartAfter = startAfter };
            }

            if (style != null && style == Application.Current.Resources["ChapterTitleText"])
            {
                return new Custom { OffsetY = 24, Blur = 12, Duration = AnimationTime.FromSeconds(1.1), Easing = Easings.StrongEaseOut, StartAfter = startAfter };
            }

            if (element is TextBlock)
            {
                return new Custom { OffsetY = 16, Blur = 4, Duration = AnimationTime.FromSeconds(1), Easing = Easings.StrongEaseOut, StartAfter = startAfter };
            }

            return new Custom { OffsetY = 40, Zoom = 0.97, Duration = AnimationTime.FromSeconds(1.1), Easing = Easings.StrongEaseOut, StartAfter = startAfter };
        }
    }
}
