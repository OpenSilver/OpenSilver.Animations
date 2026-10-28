using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Builds the JavaScript object read by <c>osAnimations.run</c>:
    /// <c>{ t: [timings], m: [[id, timing, delay, dx, dy, ow, oh, nw, nh, flags, fromGhost?]], a: [[id, timing, delay, edge]],
    /// e: [[id, timing, delay, look..., raise, text?]], k: [[id, timing, delay, dx, dy]], g: [[id, timing, delay, look..., sizeCurve, text?]],
    /// x: [ids], c: [ids], s: [[id, nw, nh]], o: [[id, duration, delay, isOrigin]], r: [ids], w: [[id, kind, timing, ...]] }</c>,
    /// where a look is <c>opacity, x, y, scale, rotate, blur, wipe, relX, relY</c>, relX and relY being fractions
    /// of the element's own width and height added to x and y, and text is <c>kind, letterMs, cursorMs, blinkFirstMs</c> for
    /// the letters of a TextBlock, cursorMs being 0 without a cursor. <c>w</c> has the text changes of TextBlocks,
    /// after a hold of their old text. <c>v: [[fromId, toId, timing, behind, ancestorId, flags]]</c> has the
    /// Magic moves, from a ghost or from a capture (<see cref="TravelFromCapture"/>).
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

        public const int TextRelease = 0;
        public const int TextRoll = 1;
        public const int TextCrossFade = 2;
        public const int TextScramble = 3;
        public const int TextRetype = 4;
        public const int TextCountUp = 5;

        public const int TravelFromCapture = 1;
        public const int TravelOldText = 2;
        public const int TravelNewText = 4;

        private readonly List<Timing> _timings = new List<Timing>();
        private readonly StringBuilder _moves = new StringBuilder();
        private readonly StringBuilder _reveals = new StringBuilder();
        private readonly StringBuilder _effects = new StringBuilder();
        private readonly StringBuilder _keeps = new StringBuilder();
        private readonly StringBuilder _ghosts = new StringBuilder();
        private readonly StringBuilder _ghostRemovals = new StringBuilder();
        private readonly StringBuilder _cancels = new StringBuilder();
        private readonly StringBuilder _retargets = new StringBuilder();
        private readonly StringBuilder _outlines = new StringBuilder();
        private readonly StringBuilder _releases = new StringBuilder();
        private readonly StringBuilder _texts = new StringBuilder();
        private readonly StringBuilder _travels = new StringBuilder();

        public bool IsEmpty => _moves.Length == 0 && _reveals.Length == 0 && _effects.Length == 0 && _keeps.Length == 0 && _ghosts.Length == 0 &&
                               _ghostRemovals.Length == 0 && _cancels.Length == 0 && _retargets.Length == 0 &&
                               _outlines.Length == 0 && _releases.Length == 0 && _texts.Length == 0 && _travels.Length == 0;

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
            _effects.Append(',').Append(raise ? '1' : '0');
            AppendText(_effects, look);
            _effects.Append(']');
        }

        /// <summary>
        /// Moves an element that appears by (<paramref name="dx"/>, <paramref name="dy"/>) back to
        /// its place, on top of its effect, against the move of the containers it makes grow, so
        /// that it stays where it ends on screen.
        /// </summary>
        public void AddKeepInPlace(string id, int timing, double delay, double dx, double dy)
        {
            StringBuilder sb = _keeps;
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
            sb.Append(']');
        }

        /// <summary>
        /// Plays the effect of an element that disappears on its ghost, the copy made when it
        /// disappeared, to <paramref name="look"/>, then removes the ghost. <paramref name="sizeCurve"/>
        /// uses the curve of the sizes, so that a wipe keeps up with the elements that fill its place.
        /// </summary>
        public void AddGhost(string id, int timing, double delay, in EffectLook look, bool sizeCurve)
        {
            AppendLook(_ghosts, id, timing, delay, look);
            _ghosts.Append(',').Append(sizeCurve ? '1' : '0');
            AppendText(_ghosts, look);
            _ghosts.Append(']');
        }

        private static void AppendText(StringBuilder sb, in EffectLook look)
        {
            if (look.Text == EffectLook.NoText)
            {
                return;
            }

            sb.Append(',').Append(look.Text).Append(',');
            BrowserBridge.AppendNumber(sb, look.LetterMs);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.CursorMs);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.BlinkFirstMs);
        }

        /// <summary>
        /// Shows the new text of a TextBlock held by <see cref="BrowserBridge.TextHold"/> at once.
        /// </summary>
        public void AddTextRelease(string id) => StartText(id, TextRelease, 0).Append(']');

        /// <summary>
        /// Rolls the columns of a TextBlock's text, from below (<paramref name="direction"/> 1) or
        /// from above (-1). A column with one string stays, one with two rolls from the first to the
        /// second.
        /// </summary>
        public void AddTextRoll(string id, int timing, int direction, List<string[]> columns)
        {
            StringBuilder sb = StartText(id, TextRoll, timing);
            sb.Append(',').Append(direction).Append(",[");
            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                string[] column = columns[i];
                if (column.Length == 1)
                {
                    BrowserBridge.AppendString(sb, column[0]);
                }
                else
                {
                    sb.Append('[');
                    BrowserBridge.AppendString(sb, column[0]);
                    sb.Append(',');
                    BrowserBridge.AppendString(sb, column[1]);
                    sb.Append(']');
                }
            }
            sb.Append("]]");
        }

        public void AddTextCrossFade(string id, int timing) => StartText(id, TextCrossFade, timing).Append(']');

        public void AddTextScramble(string id, int timing, string newText)
        {
            StringBuilder sb = StartText(id, TextScramble, timing);
            sb.Append(',');
            BrowserBridge.AppendString(sb, newText);
            sb.Append(']');
        }

        /// <summary>
        /// Erases the old text down to what it shares with the new one, then types the new one, one
        /// letter every <paramref name="letterMs"/>. With a cursor (<paramref name="cursorMs"/>
        /// above 0), the cursor blinks for <paramref name="blinkFirstMs"/> first, follows the
        /// letters, then blinks for <paramref name="cursorMs"/>.
        /// </summary>
        public void AddTextRetype(string id, int timing, double letterMs, string oldText, string newText, double cursorMs, double blinkFirstMs)
        {
            StringBuilder sb = StartText(id, TextRetype, timing);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, letterMs);
            sb.Append(',');
            BrowserBridge.AppendString(sb, oldText);
            sb.Append(',');
            BrowserBridge.AppendString(sb, newText);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, cursorMs);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, blinkFirstMs);
            sb.Append(']');
        }

        /// <summary>
        /// Counts from <paramref name="from"/> to the value of <paramref name="to"/>, written in its format.
        /// </summary>
        public void AddTextCountUp(string id, int timing, double from, TextNumber to)
        {
            StringBuilder sb = StartText(id, TextCountUp, timing);
            sb.Append(',').Append(from.ToString("R", CultureInfo.InvariantCulture))
              .Append(',').Append(to.Value.ToString("R", CultureInfo.InvariantCulture))
              .Append(',').Append(to.Decimals).Append(',');
            BrowserBridge.AppendString(sb, to.Prefix);
            sb.Append(',');
            BrowserBridge.AppendString(sb, to.Suffix);
            sb.Append(',');
            BrowserBridge.AppendString(sb, to.GroupSeparator);
            sb.Append(',');
            BrowserBridge.AppendString(sb, to.DecimalSeparator);
            sb.Append(']');
        }

        private StringBuilder StartText(string id, int kind, int timing)
        {
            StringBuilder sb = _texts;
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append('[');
            BrowserBridge.AppendString(sb, id);
            sb.Append(',').Append(kind).Append(',').Append(timing);
            return sb;
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
            sb.Append(',').Append(look.Wipe).Append(',');
            BrowserBridge.AppendNumber(sb, look.RelX);
            sb.Append(',');
            BrowserBridge.AppendNumber(sb, look.RelY);
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

        /// <summary>
        /// Makes an element travel from where another one disappeared: from its ghost, or from its
        /// capture with <see cref="TravelFromCapture"/>. With <paramref name="behind"/>, it travels
        /// in <paramref name="ancestorId"/>, behind everything in it; otherwise above the page.
        /// </summary>
        public void AddTravel(string fromId, string toId, int timing, bool behind, string ancestorId, int flags)
        {
            StringBuilder sb = _travels;
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append('[');
            BrowserBridge.AppendString(sb, fromId);
            sb.Append(',');
            BrowserBridge.AppendString(sb, toId);
            sb.Append(',').Append(timing).Append(',').Append(behind ? '1' : '0').Append(',');
            BrowserBridge.AppendString(sb, ancestorId ?? string.Empty);
            sb.Append(',').Append(flags).Append(']');
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

            sb.Append("],m:[").Append(_moves).Append("],a:[").Append(_reveals).Append("],e:[").Append(_effects).Append("],k:[").Append(_keeps)
              .Append("],g:[").Append(_ghosts)
              .Append("],x:[").Append(_ghostRemovals).Append("],c:[").Append(_cancels).Append("],s:[").Append(_retargets)
              .Append("],o:[").Append(_outlines).Append("],r:[").Append(_releases).Append("],w:[").Append(_texts).Append("],v:[").Append(_travels).Append("]}");
            return sb.ToString();
        }
    }
}
