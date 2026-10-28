using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Copies an element that disappears, before the browser hides or removes it, and leaves the
        /// copy in its place until a batch plays its effect or removes it.
        /// </summary>
        public static void Ghost(string id)
        {
            var sb = new StringBuilder("globalThis.osAnimations.ghost(");
            AppendString(sb, id);
            Interop.ExecuteJavaScriptVoidAsync(sb.Append(')').ToString());
        }

        /// <summary>
        /// Keeps showing the old text of a TextBlock whose text just changed, until a batch plays
        /// its text change or releases it. Queued after the write of the new text, in the same
        /// flush, so that the new text never shows before.
        /// </summary>
        public static void TextHold(string id, string oldText)
        {
            var sb = new StringBuilder("globalThis.osAnimations.textHold(");
            AppendString(sb, id);
            sb.Append(',');
            AppendString(sb, oldText);
            Interop.ExecuteJavaScriptVoidAsync(sb.Append(')').ToString());
        }

        /// <summary>
        /// Ends the text change that a TextBlock plays, if any, so that its new text shows at once.
        /// </summary>
        public static void TextStop(string id)
        {
            var sb = new StringBuilder("globalThis.osAnimations.textStop(");
            AppendString(sb, id);
            Interop.ExecuteJavaScriptVoidAsync(sb.Append(')').ToString());
        }

        /// <summary>
        /// Copies the elements that are near the screen, like <see cref="Ghost"/>.
        /// </summary>
        public static void GhostInView(IList<string> ids)
        {
            var sb = new StringBuilder("globalThis.osAnimations.ghostInView([");
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                AppendString(sb, ids[i]);
            }
            Interop.ExecuteJavaScriptVoidAsync(sb.Append("])").ToString());
        }

        /// <summary>
        /// Copies the elements with a Magic move inside a container that disappears, with where
        /// they are on screen, before the browser hides or removes it, for them to travel from
        /// there. Called before the container is ghosted, so that its ghost knows them.
        /// </summary>
        public static void Capture(string containerId, IList<string> ids)
        {
            var sb = new StringBuilder("globalThis.osAnimations.capture(");
            AppendString(sb, containerId);
            sb.Append(",[");
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                AppendString(sb, ids[i]);
            }
            Interop.ExecuteJavaScriptVoidAsync(sb.Append("])").ToString());
        }

        /// <summary>
        /// Sets what an element does when the mouse is over it and when it is pressed. A null
        /// reaction does nothing.
        /// </summary>
        public static void React(string id, string hover, string press, bool enabled)
        {
            var sb = new StringBuilder("globalThis.osAnimations.react(");
            AppendString(sb, id);
            sb.Append(',').Append(hover ?? "null").Append(',').Append(press ?? "null").Append(',').Append(enabled ? '1' : '0');
            Interop.ExecuteJavaScriptVoidAsync(sb.Append(')').ToString());
        }

        public static void SetReactionSettings(bool enabled, double speed, double defaultMs, bool respectReducedMotion, bool simulateReducedMotion)
        {
            var sb = new StringBuilder("globalThis.osAnimations.reactSettings(");
            sb.Append(enabled ? '1' : '0').Append(',');
            AppendNumber(sb, speed);
            sb.Append(',');
            AppendNumber(sb, defaultMs);
            sb.Append(',').Append(respectReducedMotion ? '1' : '0').Append(',').Append(simulateReducedMotion ? '1' : '0');
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
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\u2028':
                        sb.Append("\\u2028");
                        break;
                    case '\u2029':
                        sb.Append("\\u2029");
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
