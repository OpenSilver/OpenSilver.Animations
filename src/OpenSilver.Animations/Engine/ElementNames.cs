using System.Windows;
using System.Windows.Controls;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Names elements in log messages: their x:Name, or their type and a bit of their text.
    /// </summary>
    internal static class ElementNames
    {
        private const int MaxTextLength = 24;

        public static string Of(UIElement element)
        {
            if (element is null)
            {
                return "(nothing)";
            }

            if (element is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
            {
                return fe.Name;
            }

            string type = element.GetType().Name;
            string text = element switch
            {
                TextBlock tb => tb.Text,
                TextBox tb => tb.Text,
                ContentControl cc => cc.Content as string,
                ContentPresenter cp => cp.Content as string,
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.Trim();
                if (text.Length > MaxTextLength)
                {
                    text = text.Substring(0, MaxTextLength) + "…";
                }

                return type + " \"" + text + "\"";
            }

            return type;
        }

        public static string Count(int count, string singular, string plural)
            => count == 1 ? "1 " + singular : count + " " + plural;
    }
}
