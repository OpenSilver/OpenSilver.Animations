using System.Globalization;
using System.Linq;

namespace OpenSilver.Animations.Gallery.Controls
{
    /// <summary>
    /// Writes the values of the XAML snippets that follow the options of a demo.
    /// </summary>
    internal static class Snippet
    {
        /// <summary>
        /// The name of the effect alone, or <c>{anim:Name A=1, B=2}</c> with the parameters that are not null.
        /// </summary>
        public static string Effect(string name, params string[] parameters)
        {
            string[] written = parameters.Where(p => p != null).ToArray();
            return written.Length == 0 ? name : "{anim:" + name + " " + string.Join(", ", written) + "}";
        }

        /// <summary>
        /// A number as XAML writes it, with a dot.
        /// </summary>
        public static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
