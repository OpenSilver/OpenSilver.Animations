using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Names elements in log messages: their x:Name, or their type and a bit of their text. An
    /// item of a list is named by its text and its list: Item "Buy milk" of TodoList.
    /// </summary>
    internal static class ElementNames
    {
        private const int MaxTextLength = 24;
        private const int MaxTextDepth = 8;

        /// <summary>
        /// Names an element. <paramref name="parent"/> is where it was, for an element that was
        /// removed; otherwise its parent is found. <paramref name="item"/> is the data item of an
        /// item, which names it when its container shows no text anymore, like after a refresh.
        /// </summary>
        public static string Of(UIElement element, UIElement parent = null, object item = null)
        {
            if (element is null)
            {
                return "(nothing)";
            }

            if (element is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
            {
                return fe.Name;
            }

            string text = Shorten(OwnText(element));
            if ((parent ?? VisualTreeHelper.GetParent(element)) is Panel panel && ListOf(panel) is UIElement list)
            {
                text ??= Shorten(FindText(element, 0)) ?? Shorten(TextOf(item));
                return (text == null ? "An item" : "Item \"" + text + "\"") + " of " + Of(list);
            }

            string type = element.GetType().Name;
            return text == null ? type : type + " \"" + text + "\"";
        }

        public static string Count(int count, string singular, string plural)
            => count == 1 ? "1 " + singular : count + " " + plural;

        /// <summary>
        /// The list a panel shows the items of: the ItemsControl, or the panel itself when it has
        /// item settings.
        /// </summary>
        private static UIElement ListOf(Panel panel)
        {
            if (panel.IsItemsHost && ItemsControl.GetItemsOwner(panel) is ItemsControl owner)
            {
                return owner;
            }

            return LayoutAnimator.AnyItems &&
                   (panel.GetValue(Animate.ItemsAppearProperty) != null || panel.GetValue(Animate.ItemsDisappearProperty) != null ||
                    panel.GetValue(Animate.ItemsMoveAndResizeProperty) != null)
                ? panel
                : null;
        }

        private static string OwnText(UIElement element) => element switch
        {
            TextBlock tb => tb.Text,
            TextBox tb => tb.Text,
            ContentControl cc => cc.Content as string,
            ContentPresenter cp => cp.Content as string,
            _ => null,
        };

        // The ToString of a data item, unless it is only the name of its type.
        private static string TextOf(object item)
        {
            string text = item?.ToString();
            return text == null || text == item.GetType().ToString() ? null : text;
        }

        /// <summary>
        /// The first text inside an item, like the title of a card.
        /// </summary>
        private static string FindText(DependencyObject element, int depth)
        {
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(element, i) is UIElement child)
                {
                    if (!string.IsNullOrWhiteSpace(OwnText(child)))
                    {
                        return OwnText(child);
                    }

                    if (depth < MaxTextDepth && FindText(child, depth + 1) is string text)
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = text.Trim();
            return text.Length > MaxTextLength ? text.Substring(0, MaxTextLength) + "…" : text;
        }
    }
}
