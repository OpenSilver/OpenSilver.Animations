using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Builds the JavaScript object read by <c>osAnimations.run</c>:
    /// <c>{ t: [timings], m: [[id, timing, delay, dx, dy, ow, oh, nw, nh, flags, fromGhost?]], a: [[id, timing, delay, edge]],
    /// e: [[id, timing, delay, look..., raise]], g: [[id, timing, delay, look..., sizeCurve]], x: [ids], c: [ids],
    /// s: [[id, nw, nh]], o: [[id, duration, delay, isOrigin]], r: [ids] }</c>, where a look is
    /// <c>opacity, x, y, scale, rotate, blur, wipe</c>.
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
        private readonly StringBuilder _effects = new StringBuilder();
        private readonly StringBuilder _ghosts = new StringBuilder();
        private readonly StringBuilder _ghostRemovals = new StringBuilder();
        private readonly StringBuilder _cancels = new StringBuilder();
        private readonly StringBuilder _retargets = new StringBuilder();
        private readonly StringBuilder _outlines = new StringBuilder();
        private readonly StringBuilder _releases = new StringBuilder();

        public bool IsEmpty => _moves.Length == 0 && _reveals.Length == 0 && _effects.Length == 0 && _ghosts.Length == 0 &&
                               _ghostRemovals.Length == 0 && _cancels.Length == 0 && _retargets.Length == 0 &&
                               _outlines.Length == 0 && _releases.Length == 0;

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

        /// <summary>
        /// Moves and resizes an element from its old box. <paramref name="fromGhost"/> is the ghost
        /// of the element that showed the same item before, if any: the move goes on from where
        /// that element was on screen.
        /// </summary>
        public void AddMove(string id, int timing, double delay, double dx, double dy, double oldWidth, double oldHeight, double newWidth, double newHeight, int flags, string fromGhost = null)
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
            sb.Append(',').Append(flags);
            if (fromGhost != null)
            {
                sb.Append(',');
                BrowserBridge.AppendString(sb, fromGhost);
            }
            sb.Append(']');
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

        /// <summary>
        /// Plays the effect of an element that appears, from <paramref name="look"/> to the element
        /// as it is. <paramref name="raise"/> puts it above its siblings while it pushes them.
        /// </summary>
        public void AddEffect(string id, int timing, double delay, in EffectLook look, bool raise)
        {
            AppendLook(_effects, id, timing, delay, look);
            _effects.Append(',').Append(raise ? '1' : '0').Append(']');
        }

        /// <summary>
        /// Plays the effect of an element that disappears on its ghost, the copy made when it
        /// disappeared, to <paramref name="look"/>, then removes the ghost. <paramref name="sizeCurve"/>
        /// uses the curve of the sizes, so that a wipe keeps up with the elements that fill its place.
        /// </summary>
        public void AddGhost(string id, int timing, double delay, in EffectLook look, bool sizeCurve)
        {
            AppendLook(_ghosts, id, timing, delay, look);
            _ghosts.Append(',').Append(sizeCurve ? '1' : '0').Append(']');
        }

        /// <summary>
        /// Removes the ghost of an element that disappears at once.
        /// </summary>
        public void AddGhostRemoval(string id)
        {
            if (_ghostRemovals.Length > 0)
            {
                _ghostRemovals.Append(',');
            }

            BrowserBridge.AppendString(_ghostRemovals, id);
        }

        private static void AppendLook(StringBuilder sb, string id, int timing, double delay, in EffectLook look)
        {
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append('[');
            BrowserBridge.AppendString(sb, id);
            sb.Append(',').Append(timing).Append(',');
            BrowserBridge.AppendNumber(sb, delay);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.Opacity);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.X);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.Y);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.Scale);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.Rotate);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.Blur);
            sb.Append(',').Append(look.Wipe);
        }

        public void AddCancel(UIElement element) => AddId(_cancels, element);

        /// <summary>
        /// Moves the end of the running size animation of an element to its new size, which the
        /// element got without animating, like the rest of a resize it follows.
        /// </summary>
        public void AddRetarget(string id, double newWidth, double newHeight)
        {
            StringBuilder sb = _retargets;
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append('[');
            BrowserBridge.AppendString(sb, id);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, newWidth);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, newHeight);
            sb.Append(']');
        }

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

            sb.Append("],m:[").Append(_moves).Append("],a:[").Append(_reveals).Append("],e:[").Append(_effects).Append("],g:[").Append(_ghosts)
              .Append("],x:[").Append(_ghostRemovals).Append("],c:[").Append(_cancels).Append("],s:[").Append(_retargets)
              .Append("],o:[").Append(_outlines).Append("],r:[").Append(_releases).Append("]}");
            return sb.ToString();
        }
    }
}
