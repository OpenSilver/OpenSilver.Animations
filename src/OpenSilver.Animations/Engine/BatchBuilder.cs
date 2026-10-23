using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Builds the JavaScript object read by <c>osAnimations.run</c>:
    /// <c>{ t: [timings], m: [[id, timing, delay, dx, dy, ow, oh, nw, nh, flags]], c: [ids], o: [[id, duration, delay, isOrigin]] }</c>.
    /// </summary>
    internal sealed class BatchBuilder
    {
        public const int ClipOwnBox = 1;
        public const int Rectangle = 2;
        public const int Ellipse = 4;

        private readonly List<Timing> _timings = new List<Timing>();
        private readonly StringBuilder _moves = new StringBuilder();
        private readonly StringBuilder _cancels = new StringBuilder();
        private readonly StringBuilder _outlines = new StringBuilder();

        public bool IsEmpty => _moves.Length == 0 && _cancels.Length == 0 && _outlines.Length == 0;

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

        public void AddCancel(UIElement element)
        {
            string id = Interop.GetDiv(element).Uid;
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (_cancels.Length > 0)
            {
                _cancels.Append(',');
            }

            BrowserBridge.AppendString(_cancels, id);
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

            sb.Append("],m:[").Append(_moves).Append("],c:[").Append(_cancels).Append("],o:[").Append(_outlines).Append("]}");
            return sb.ToString();
        }
    }
}
