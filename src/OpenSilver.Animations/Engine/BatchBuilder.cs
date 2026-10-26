using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Builds the JavaScript object read by <c>osAnimations.run</c>:
    /// <c>{ t: [timings], m: [[id, timing, delay, dx, dy, ow, oh, nw, nh, flags]], a: [[id, timing, delay, edge]], c: [ids], o: [[id, duration, delay, isOrigin]], r: [ids] }</c>.
    /// </summary>
    internal sealed class BatchBuilder
    {
        public const int ClipOwnBox = 1;
        public const int Rectangle = 2;
        public const int Ellipse = 4;

        public const int Fade = 0;
        public const int RevealFromTop = 1;
        public const int RevealFromLeft = 2;
        public const int RevealFromBottom = 3;
        public const int RevealFromRight = 4;

        private readonly List<Timing> _timings = new List<Timing>();
        private readonly StringBuilder _moves = new StringBuilder();
        private readonly StringBuilder _reveals = new StringBuilder();
        private readonly StringBuilder _cancels = new StringBuilder();
        private readonly StringBuilder _outlines = new StringBuilder();
        private readonly StringBuilder _releases = new StringBuilder();

        public bool IsEmpty => _moves.Length == 0 && _reveals.Length == 0 && _cancels.Length == 0 && _outlines.Length == 0 && _releases.Length == 0;

        public int AddTiming(Timing timing)
        {
            int index = _timings.IndexOf(timing);
            if (index < 0)
            {
                index = _timings.Count;
                _timings.Add(timing);
            }

            return index;
        }

        public void AddMove(string id, int timing, double delay, double dx, double dy, double oldWidth, double oldHeight, double newWidth, double newHeight, int flags)
        {
            StringBuilder sb = _moves;
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append('[');
            BrowserBridge.AppendString(sb, id);
            sb.Append(',').Append(timing).Append(',');
            BrowserBridge.AppendNumber(sb, delay);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, dx);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, dy);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, oldWidth);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, oldHeight);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, newWidth);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, newHeight);
            sb.Append(',').Append(flags).Append(']');
        }

        /// <summary>
        /// Wipes in an element that appears from <paramref name="edge"/>, one of the RevealFrom
        /// constants, or fades it in (<see cref="Fade"/>).
        /// </summary>
        public void AddReveal(string id, int timing, double delay, int edge)
        {
            StringBuilder sb = _reveals;
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append('[');
            BrowserBridge.AppendString(sb, id);
            sb.Append(',').Append(timing).Append(',');
            BrowserBridge.AppendNumber(sb, delay);
            sb.Append(',').Append(edge).Append(']');
        }

        public void AddCancel(UIElement element) => AddId(_cancels, element);

        /// <summary>
        /// Shows an element hidden by <see cref="BrowserBridge.Hold"/>.
        /// </summary>
        public void AddRelease(UIElement element) => AddId(_releases, element);

        private static void AddId(StringBuilder sb, UIElement element)
        {
            string id = Interop.GetDiv(element).Uid;
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            BrowserBridge.AppendString(sb, id);
        }

        public void AddOutline(string id, double duration, double delay, bool isOrigin)
        {
            StringBuilder sb = _outlines;
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append('[');
            BrowserBridge.AppendString(sb, id);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, duration);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, delay);
            sb.Append(',').Append(isOrigin ? '1' : '0').Append(']');
        }

        public string ToJson()
        {
            var sb = new StringBuilder("{t:[");
            for (int i = 0; i < _timings.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                _timings[i].AppendJson(sb);
            }

            sb.Append("],m:[").Append(_moves).Append("],a:[").Append(_reveals).Append("],c:[").Append(_cancels).Append("],o:[").Append(_outlines).Append("],r:[").Append(_releases).Append("]}");
            return sb.ToString();
        }
    }
}
