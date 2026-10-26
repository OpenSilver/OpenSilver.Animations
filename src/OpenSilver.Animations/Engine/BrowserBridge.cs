using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Loads the JavaScript part of the library and talks to it.
    /// </summary>
    internal static class BrowserBridge
    {
        private static bool _initialized;
        private static IDisposable[] _callbacks;

        public static bool IsDragging { get; private set; }

        public static bool PrefersReducedMotion { get; private set; }

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            string script;
            using (Stream stream = typeof(BrowserBridge).Assembly.GetManifestResourceStream("OpenSilver.Animations.animations.js"))
            using (var reader = new StreamReader(stream))
            {
                script = reader.ReadToEnd();
            }

            Interop.ExecuteJavaScriptVoid(script);

            Action dragStarted = () => IsDragging = true;
            Action dragEnded = () => IsDragging = false;
            Action reducedMotionOn = () => PrefersReducedMotion = true;
            Action reducedMotionOff = () => PrefersReducedMotion = false;

            _callbacks = new[]
            {
                Interop.CreateJavascriptCallback(dragStarted),
                Interop.CreateJavascriptCallback(dragEnded),
                Interop.CreateJavascriptCallback(reducedMotionOn),
                Interop.CreateJavascriptCallback(reducedMotionOff),
            };

            Interop.ExecuteJavaScriptVoid(
                "globalThis.osAnimations.init($0, $1, $2, $3)",
                _callbacks[0], _callbacks[1], _callbacks[2], _callbacks[3]);
        }

        /// <summary>
        /// Queues the batch after the pending layout writes, in the same flush.
        /// </summary>
        public static void Run(string batch) => Interop.ExecuteJavaScriptVoidAsync("globalThis.osAnimations.run(" + batch + ")");

        /// <summary>
        /// Hides an element that just became visible until a batch wipes it in or releases it.
        /// </summary>
        public static void Hold(string id)
        {
            var sb = new StringBuilder("globalThis.osAnimations.hold(");
            AppendString(sb, id);
            Interop.ExecuteJavaScriptVoidAsync(sb.Append(')').ToString());
        }

        public static void WriteToConsole(string message)
        {
            Interop.ExecuteJavaScriptVoidAsync("console.log($0)", "[OpenSilver.Animations] " + message);
        }

        public static void AppendNumber(StringBuilder sb, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                sb.Append('0');
                return;
            }

            sb.Append(Math.Round(value, 2).ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('\'');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\'':
                        sb.Append("\\'");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            sb.Append('\'');
        }
    }
}
