using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Watches the layout passes, finds what changed and why, and animates the elements that
    /// moved or changed size from where they were to where they are.
    /// </summary>
    /// <remarks>
    /// During a pass, each arranged element gets a record with its old and new rectangle. The
    /// elements where a change started (the origins) are found while measuring and arranging:
    /// an element measured for the first time in the pass with the same available size, whose
    /// content did not grow or shrink, or an element arranged again in the same rectangle without
    /// being measured. A pass caused by resizing the browser window has no origins. The origins
    /// are grouped with the elements that moved because of them: a group goes up to the ancestors
    /// that were arranged again, then down to everything they arranged. Elements that follow the
    /// window (AlsoOnWindowResize) form groups of their own that only go down. Groups never go
    /// into or out of an element with MoveAndResize=None. At the end of the pass, each group
    /// animates or snaps as a whole, with the elements that appeared inside an area wiping or
    /// fading in, and all the animations of the pass are sent to the browser in one batch, in the
    /// same flush as the layout writes.
    ///
    /// A {Responsive} value that switches is also an origin: the element it applies to and its
    /// parent. When it switches while the window or a panel is being resized, what was arranged
    /// before is completed as a pass of its own, which follows the resize, and the rest of the
    /// pass only has the switches as origins, and animates even while something is dragged: each
    /// setting found going up from a switch animates what is inside it, with its own timing.
    /// While the resize continues, these animations go on instead of being cancelled, and their
    /// sizes are retargeted.
    ///
    /// An element that appears with an effect (Animate.Appear) is a change of its parent, and the
    /// effect gives its timing to the group, which makes room for it. An element that disappears is
    /// copied in the browser when it is collapsed or removed (a ghost), and the ghost plays its
    /// effect while the group fills its place. Inside an area, the topmost element that appears or
    /// disappears without an effect of its own gets an automatic wipe or fade. Elements that appear
    /// with their container play their own effects, without making room.
    /// </remarks>
    internal sealed class LayoutAnimator : LayoutObserver
    {
        private const int MaxAnimatedElements = 300;
        private const double MaxWaveDelayMs = 200;
        private const double WaveDistance = 600;
        private const double CullMargin = 100;
        private const double Tolerance = 0.05;
        private const long SettleMs = 500;
        private const string ScrollBarReason = "the change happened in a scroll bar";
        private const string SettlingReason = "the element had just appeared";
        private const string DraggingReason = "something is being dragged";
        private const string ReducedMotionReason = "the device asks for reduced motion";
        private const string SimulatedReducedMotionReason = "reduced motion is simulated (AnimationSettings.SimulateReducedMotion)";
        private static readonly string TooManyReason = "more than " + MaxAnimatedElements + " elements would animate at once";

        // Automatic effects of elements that disappear at once, like a page full of elements removed.
        private const int MaxAutomaticGhosts = 40;
        private const long GhostLifetimeMs = 3000;
        private static readonly Timing QuickFadeTiming = Timing.QuickFade();
        private static readonly EffectLook FadeLook = new EffectLook { Opacity = 0 };

        private static LayoutAnimator _instance;

        /// <summary>
        /// Set once an element uses <see cref="AnimationScope.EverythingInside"/>. Until then, the
        /// ancestors are never searched for an area.
        /// </summary>
        internal static bool AnyAreas;

        /// <summary>
        /// Set once an element has an Appear or a Disappear. Until then, elements that appear or
        /// disappear are never checked for effects.
        /// </summary>
        internal static bool AnyEffects;

        /// <summary>
        /// Set once an Appear has OnPageOpen=PushOthers. Until then, the elements inside a
        /// container that is shown again are never searched for one.
        /// </summary>
        internal static bool AnyPushOthers;

        private Record[] _records = new Record[64];
        private int _recordCount;
        private readonly Dictionary<UIElement, int> _recordIndex = new Dictionary<UIElement, int>();
        private readonly List<int> _stack = new List<int>();
        private readonly List<int> _sessionOrder = new List<int>();

        private readonly HashSet<UIElement> _measured = new HashSet<UIElement>();
        private readonly HashSet<UIElement> _carriers = new HashSet<UIElement>();
        private readonly HashSet<UIElement> _measureOrigins = new HashSet<UIElement>();
        private readonly HashSet<UIElement> _shown = new HashSet<UIElement>();
        private readonly HashSet<UIElement> _held = new HashSet<UIElement>();
        private readonly Dictionary<UIElement, bool> _scrollChecked = new Dictionary<UIElement, bool>();
        private readonly ConditionalWeakTable<UIElement, ScrollOffset> _scrollOffsets = new ConditionalWeakTable<UIElement, ScrollOffset>();

        private readonly List<int> _groupParents = new List<int> { 0 };
        private readonly List<GroupInfo> _groups = new List<GroupInfo> { default };

        private readonly Dictionary<UIElement, Area> _areaCache = new Dictionary<UIElement, Area>();
        private readonly Dictionary<UIElement, long> _appeared = new Dictionary<UIElement, long>();
        private readonly List<UIElement> _expired = new List<UIElement>();
        // When the animations end, and whether they come from a {Responsive} switch during a resize,
        // which goes on while the resize continues.
        private readonly Dictionary<UIElement, (long Until, bool FromSwitch)> _runningUntil = new Dictionary<UIElement, (long Until, bool FromSwitch)>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _nextSweep;

        private readonly Dictionary<int, Bucket> _buckets = new Dictionary<int, Bucket>();
        private readonly Stack<Bucket> _bucketPool = new Stack<Bucket>();
        private readonly List<UIElement> _toCancel = new List<UIElement>();
        private readonly List<int> _toRetarget = new List<int>();
        private readonly List<string> _messages = new List<string>();

        // The {Responsive} values that switched in this pass: the elements where the change starts,
        // and what to say about it.
        private readonly Dictionary<UIElement, string> _switches = new Dictionary<UIElement, string>();
        private readonly List<Owner> _owners = new List<Owner>();
        private readonly Dictionary<UIElement, Owner> _ownerOf = new Dictionary<UIElement, Owner>();
        private bool _switchOnly;
        private bool _keepRunning;

        // The elements collapsed or removed since the last pass, copied in the browser.
        private readonly List<Disappearance> _disappearing = new List<Disappearance>();
        // The records of the elements that appear in this pass with an Appear of their own.
        private readonly List<int> _effectAppears = new List<int>();
        private readonly Dictionary<Transition, Timing> _effectTimings = new Dictionary<Transition, Timing>();
        // The elements that may still have a ghost in the browser, until when.
        private readonly Dictionary<UIElement, long> _ghostedUntil = new Dictionary<UIElement, long>();
        private bool _effectsPlay;
        private bool _quickFades;

        // OnPageOpen=PushOthers: the elements held out of the layout while their page opens, and
        // those shown again since the last pass, which appear in the next one.
        private readonly PageOpenHolds _pageOpenHolds;
        private readonly HashSet<UIElement> _released = new HashSet<UIElement>();

        private Point[] _absOld = new Point[64];
        private Point[] _absNew = new Point[64];
        private int[] _absStamp = new int[64];
        private double[] _delays = new double[64];
        private int[] _memberStamp = new int[64];
        private int[] _chainStamp = new int[64];
        private int _stamp;
        private int _waveStamp;

        private bool _forced;
        private AnimationTime? _forcedDuration;
        private bool _skip;
        private readonly Stack<(bool Skip, bool Forced, AnimationTime? Duration)> _outerScopes = new Stack<(bool, bool, AnimationTime?)>();
        private long _lastWindowResizeLog = -10000;
        private string _lastSnapMessage;
        private long _lastSnapLog;
        private Size _windowSize;
        private bool _windowChecked;
        private bool _windowResized;

        private LayoutAnimator()
        {
            _pageOpenHolds = new PageOpenHolds(element => _released.Add(element));
        }

        public static void EnsureStarted()
        {
            if (_instance != null || DesignerProperties.IsInDesignTool)
            {
                return;
            }

            _instance = new LayoutAnimator();
            BrowserBridge.EnsureInitialized();
            Register(_instance);
        }

        /// <summary>
        /// Until <see cref="ExitScope"/>, the layout passes animate every change, even without
        /// settings (<paramref name="skip"/> false), or nothing (<paramref name="skip"/> true).
        /// When scopes are nested, the outer scope applies again once the inner one exits.
        /// </summary>
        public static void EnterScope(bool skip, AnimationTime? duration)
        {
            if (_instance is LayoutAnimator animator)
            {
                animator._outerScopes.Push((animator._skip, animator._forced, animator._forcedDuration));
                animator._skip = skip;
                animator._forced = !skip;
                animator._forcedDuration = skip ? null : duration;
            }
        }

        public static void ExitScope()
        {
            if (_instance is LayoutAnimator animator && animator._outerScopes.Count > 0)
            {
                (animator._skip, animator._forced, animator._forcedDuration) = animator._outerScopes.Pop();
            }
        }

        /// <summary>
        /// Holds an element with OnPageOpen=PushOthers out of the layout until its page has opened
        /// and its StartAfter has elapsed. Only before it is loaded: an Appear set on an element
        /// that is already on the page is for its next appearance.
        /// </summary>
        public static void HoldForPageOpen(UIElement element, Transition effect)
        {
            AnyPushOthers = true;
            if (_instance is LayoutAnimator animator && element is FrameworkElement fe && !fe.IsLoaded)
            {
                animator.StartHold(fe, effect);
            }
        }

        private void StartHold(FrameworkElement element, Transition effect)
        {
            // Without animations, or with reduced motion, it simply appears in its place.
            AnimationSettings settings = AnimationSettings.Current;
            string reason = GetGlobalSnapReason(settings);
            if (reason == null || ReferenceEquals(reason, DraggingReason))
            {
                _pageOpenHolds.Start(element, GetWaitMs(effect, settings));
            }
        }

        /// <summary>
        /// The elements with OnPageOpen=PushOthers inside a container that is shown again appear
        /// with it: they are held too. Those inside a held element are held when it is released.
        /// </summary>
        private void HoldDescendants(UIElement container)
        {
            int count = VisualTreeHelper.GetChildrenCount(container);
            for (int i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(container, i) is UIElement child &&
                    (child.Visibility == Visibility.Visible || _pageOpenHolds.IsHolding(child)))
                {
                    if (child is FrameworkElement fe && GetAppearEffect(child) is Transition effect && effect.OnPageOpen == PageOpen.PushOthers)
                    {
                        StartHold(fe, effect);
                    }
                    else
                    {
                        HoldDescendants(child);
                    }
                }
            }
        }

        /// <summary>
        /// Says whether an element held while its page opened appeared with its page or container,
        /// rather than on its own: whether the nearest element arranged in this pass around it
        /// appeared.
        /// </summary>
        private bool AppearsWithContainer(UIElement element)
        {
            for (DependencyObject e = VisualTreeHelper.GetParent(element); e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is UIElement ancestor && _recordIndex.TryGetValue(ancestor, out int index))
                {
                    return (_records[index].Flags & RecordFlags.Appears) != 0;
                }
            }

            return true;
        }

        /// <summary>
        /// Warns about the parameters of an effect that don't apply where it is used. They are ignored.
        /// </summary>
        public static void CheckEffect(Transition effect, bool appear, UIElement element)
        {
            if (_instance == null || effect.IsNone)
            {
                return;
            }

            string problem = null;
            if (appear && effect.OthersMove == OthersMove.After)
            {
                problem = "OthersMove=After is for Disappear, so the elements around it move while it appears";
            }
            else if (!appear && effect.OthersMove == OthersMove.Before)
            {
                problem = "OthersMove=Before is for Appear, so the elements around it move while it disappears";
            }
            else if (!appear && effect is Pop)
            {
                problem = "Pop is for Appear only, so it zooms out";
            }
            else if (!appear && effect.OnPageOpen != PageOpen.InPlace)
            {
                problem = "OnPageOpen is for Appear, so it is ignored";
            }
            else if (appear && effect.HasTo && !effect.HasFrom)
            {
                problem = "To is for Disappear, so it comes from the bottom. Use From to choose the side it comes from";
            }
            else if (!appear && effect.HasFrom && !effect.HasTo)
            {
                problem = "From is for Appear, so it goes to the bottom. Use To to choose the side it goes to";
            }
            else if (effect.Bounciness != null && !effect.IsBouncy)
            {
                problem = "Bounciness is only used by Pop and by Easing=Bouncy";
            }

            // Later, so that the element has its name, and the app had time to turn on the log.
            if (problem != null)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    string message = $"The {(appear ? "Appear" : "Disappear")} of {ElementNames.Of(element)}: {problem}.";
                    if (AnimationSettings.Current.LogForDebugging)
                    {
                        BrowserBridge.WriteToConsole(message);
                    }

                    if (AnimationSettings.HasLoggedHandlers)
                    {
                        AnimationSettings.RaiseLogged(message);
                    }
                });
            }
        }

        #region Layout hooks

        protected override void OnMeasured(UIElement element, Size availableSize, bool sameAvailableSize, Size previousDesiredSize, bool isFirstMeasure)
        {
            if (isFirstMeasure)
            {
                return;
            }

            bool measuredAgain = !_measured.Add(element);

            if (element is IScrollInfo scrollInfo && IsScrolled(element, scrollInfo))
            {
                return;
            }

            if (!sameAvailableSize)
            {
                return;
            }

            if (!AreClose(previousDesiredSize, element.DesiredSize) && VisualTreeHelper.GetParent(element) is UIElement parent)
            {
                _carriers.Add(parent);
            }

            // Only the first measure of a pass tells where a change started. Measuring again with the
            // same size later in the pass is the layout settling, like a scroll viewer that measures
            // its content a second time after showing or hiding a scroll bar.
            if (!measuredAgain && !_carriers.Contains(element))
            {
                _measureOrigins.Add(element);
            }
        }

        protected override void OnArranging(UIElement element, Rect finalRect, Rect previousFinalRect, bool isFirstArrange)
        {
            int parent = _stack.Count > 0 ? _stack[_stack.Count - 1] : -1;

            // Everything inside an element that appears appears with it. Only the elements that have
            // an Appear of their own are recorded, to play it, unless it appears by scrolling.
            if (parent >= 0 && (_records[parent].Flags & RecordFlags.Appears) != 0)
            {
                if (AnyEffects && (_records[parent].Flags & RecordFlags.Scrolled) == 0 && GetAppearEffect(element) != null)
                {
                    int nested = GetOrAddRecord(element);
                    _records[nested].Parent = parent;
                    _records[nested].Flags |= RecordFlags.Appears | RecordFlags.WithContainer;
                    _stack.Add(nested);
                }
                return;
            }

            int index = GetOrAddRecord(element);
            ref Record record = ref _records[index];
            record.Parent = parent;

            // An element shown again still has the box it had before it was collapsed: it appears,
            // like an element arranged for the first time, and the change starts in its parent.
            bool shown = !isFirstArrange && _shown.Count > 0 && _shown.Contains(element);
            if (isFirstArrange || shown)
            {
                record.Flags |= RecordFlags.Appears;
                _appeared[element] = _clock.ElapsedMilliseconds;

                // Realized by a scrolling list, like a virtualizing panel: no effects.
                if (parent >= 0 && (_records[parent].Flags & RecordFlags.Scrolled) != 0)
                {
                    record.Flags |= RecordFlags.Scrolled;
                }

                if (shown && parent >= 0 && !_switchOnly && _measureOrigins.Contains(element) && !IsWindowResizePass() &&
                    (_records[parent].Flags & RecordFlags.Scrolled) == 0)
                {
                    _records[parent].Flags |= RecordFlags.Origin;
                }

                // Held while its page opened (OnPageOpen=PushOthers): it pushes the others now, even
                // though the page has just appeared, and it has already waited its StartAfter.
                if (_released.Count > 0 && _released.Remove(element))
                {
                    record.Flags |= RecordFlags.Released;
                    if (parent >= 0 && !_switchOnly && (_records[parent].Flags & RecordFlags.Scrolled) == 0)
                    {
                        _records[parent].Flags |= RecordFlags.Origin | RecordFlags.Released;
                    }
                }
            }
            else if (element is IScrollInfo scrollInfo && IsScrolled(element, scrollInfo))
            {
                record.Flags |= RecordFlags.Scrolled;
            }
            else if (IsSwitched(element) ||
                     (!_switchOnly && !IsWindowResizePass() &&
                      (_measureOrigins.Contains(element) ||
                       (!_measured.Contains(element) && AreClose(finalRect, previousFinalRect)))))
            {
                record.Flags |= RecordFlags.Origin;
            }

            _stack.Add(index);
            _sessionOrder.Add(index);
        }

        protected override void OnArranged(UIElement element, Rect previousVisualRect, Rect visualRect, bool rendered)
        {
            int count = _stack.Count;
            if (count == 0)
            {
                return;
            }

            int index = _stack[count - 1];
            if (!ReferenceEquals(_records[index].Element, element))
            {
                return;
            }

            _stack.RemoveAt(count - 1);

            ref Record record = ref _records[index];
            if ((record.Flags & RecordFlags.HasRects) == 0)
            {
                record.OldRect = previousVisualRect;
                record.Flags |= RecordFlags.HasRects;
            }
            record.NewRect = visualRect;

            // The browser places some roots, like windows and popup roots: their new box is not
            // written, so there is nothing to animate.
            if (!rendered && !AreClose(previousVisualRect, visualRect))
            {
                record.Flags |= RecordFlags.NotRendered;
            }

            if ((record.Flags & RecordFlags.Origin) != 0 && record.Group == 0)
            {
                record.Group = NewGroup(GroupKind.Change, null, null);
            }

            // Only changes go up to the containers. Following the window concerns the elements that
            // opted in and their content, even when they are arranged again later in the same pass.
            // A change never goes into or out of an element with MoveAndResize=None, so that what
            // it pushes around it snaps, even when another change in the same pass animates.
            if (record.Group != 0 && record.Parent >= 0 && _groups[Find(record.Group)].Kind == GroupKind.Change)
            {
                ref Record parentRecord = ref _records[record.Parent];
                if (!AnyAreas || (!HasNone(element) && !HasNone(parentRecord.Element)))
                {
                    parentRecord.Group = parentRecord.Group == 0 ? record.Group : Union(parentRecord.Group, record.Group);
                }
            }

            if (_stack.Count == 0)
            {
                EndArrangeSession();
            }
        }

        protected override void OnVisibilityChanged(UIElement element, Visibility oldValue, Visibility newValue)
        {
            if (_pageOpenHolds.IsHolding(element))
            {
                return;
            }

            if (newValue != Visibility.Visible)
            {
                if (_shown.Count > 0)
                {
                    _shown.Remove(element);
                }

                if (oldValue == Visibility.Visible && (AnyEffects || AnyAreas))
                {
                    CaptureDisappearing(element, VisualTreeHelper.GetParent(element) as UIElement, newValue == Visibility.Hidden);
                }
                return;
            }

            // A hidden element keeps its space, so nothing moves and there is no layout pass: its
            // effect plays at once.
            if (oldValue == Visibility.Hidden)
            {
                if (AnyEffects && GetAppearEffect(element) is Transition effect && IsAnimatable(element, out string hiddenId))
                {
                    PlayAtOnce(element, effect, appear: true, hiddenId);
                }
                return;
            }

            _shown.Add(element);

            if (AnyPushOthers)
            {
                HoldDescendants(element);
            }

            // The browser shows the element at once, but it is only arranged in the next layout
            // pass. With an effect of its own, or inside an area, it stays hidden until then, so that
            // it never shows where it was before it was collapsed, nor before its effect starts.
            if (!_held.Contains(element) &&
                ((AnyEffects && GetAppearEffect(element) != null) || (AnyAreas && !HasNoEffect(element, appear: true) && IsInsideArea(element))) &&
                Interop.GetDiv(element).Uid is string id && id.Length > 0)
            {
                _held.Add(element);
                BrowserBridge.Hold(id);
            }
            else if (_ghostedUntil.Count > 0 && _ghostedUntil.Remove(element) && !_held.Contains(element) &&
                     Interop.GetDiv(element).Uid is string ghostId && ghostId.Length > 0)
            {
                // It disappeared just before, without an effect to play now: its ghost goes away.
                var batch = new BatchBuilder();
                batch.AddGhostRemoval(ghostId);
                BrowserBridge.Run(batch.ToJson());
            }
        }

        protected override void OnDetaching(UIElement child, UIElement parent)
        {
            if (_runningUntil.Count > 0)
            {
                _runningUntil.Remove(child);
            }

            if ((AnyEffects || AnyAreas) && child.Visibility == Visibility.Visible)
            {
                CaptureDisappearing(child, parent, hidden: false);
            }
        }

        /// <summary>
        /// Copies an element that is being collapsed, hidden or removed, if it has an effect to play:
        /// its own Disappear, or an automatic one inside an area. A hidden element keeps its space,
        /// so its effect plays at once; the others play theirs at the end of the next layout pass.
        /// </summary>
        private void CaptureDisappearing(UIElement element, UIElement parent, bool hidden)
        {
            if (parent == null || !parent.IsVisible || _skip || _held.Contains(element) || !AnimationSettings.Current.IsEnabled)
            {
                return;
            }

            Transition effect = AnyEffects ? GetDisappearEffect(element) : null;
            if (effect == null &&
                (hidden || !AnyAreas || HasNoEffect(element, appear: false) || CountAutomaticGhosts() >= MaxAutomaticGhosts || !IsInsideArea(element)))
            {
                return;
            }

            Size size = element.RenderSize;
            if (size.Width <= 0 || size.Height <= 0 || !IsAnimatable(element, out string id))
            {
                return;
            }

            BrowserBridge.Ghost(id);
            _ghostedUntil[element] = _clock.ElapsedMilliseconds + GhostLifetimeMs;

            if (hidden)
            {
                PlayAtOnce(element, effect, appear: false, id);
                return;
            }

            _disappearing.Add(new Disappearance { Element = element, Parent = parent, Id = id, Effect = effect, ParentIndex = -1 });
        }

        private int CountAutomaticGhosts()
        {
            int count = 0;
            foreach (Disappearance disappearance in _disappearing)
            {
                if (disappearance.Effect == null)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Plays the effect of an element that becomes hidden or visible again, which doesn't need
        /// a layout pass.
        /// </summary>
        private void PlayAtOnce(UIElement element, Transition effect, bool appear, string id)
        {
            AnimationSettings settings = AnimationSettings.Current;
            string reason = GetGlobalSnapReason(settings);
            bool quick = IsReducedMotion(reason);
            var batch = new BatchBuilder();

            if (reason != null && !quick)
            {
                if (!appear)
                {
                    batch.AddGhostRemoval(id);
                    BrowserBridge.Run(batch.ToJson());
                }
                return;
            }

            Timing timing = quick ? QuickFadeTiming : Timing.FromEffect(effect, settings);
            EffectLook look = quick ? FadeLook : effect.GetLook(appear);
            double wait = GetWaitMs(effect, settings);
            if (appear)
            {
                batch.AddEffect(id, batch.AddTiming(timing), wait, look, raise: false);
            }
            else
            {
                batch.AddGhost(id, batch.AddTiming(timing), wait, look, sizeCurve: false);
                KeepGhosted(element, wait + timing.DurationMs);
            }
            BrowserBridge.Run(batch.ToJson());

            if (settings.LogForDebugging || AnimationSettings.HasLoggedHandlers)
            {
                string what = appear ? "became visible again" : "was hidden";
                _messages.Add($"{ElementNames.Of(element)} {what}; {DescribeOwnEffect(effect, appear, quick, timing, reason, wait)}.");
                Publish(settings);
            }
        }

        /// <summary>
        /// "it fades in (Smooth, 0.25 s, from its Appear)", "it fades in after 0.5 s (...)", or "it
        /// fades in quickly, because the device asks for reduced motion".
        /// </summary>
        private static string DescribeOwnEffect(Transition effect, bool appear, bool quick, Timing timing, string reason, double waitMs)
            => quick
                ? $"it {(appear ? "fades in" : "fades out")} quickly{After(waitMs)}, because {reason}"
                : $"it {effect.Describe(appear)}{After(waitMs)} ({timing.Description}, from its {(appear ? "Appear" : "Disappear")})";

        /// <summary>
        /// " after 0.5 s", or nothing when the effect doesn't wait.
        /// </summary>
        private static string After(double waitMs) => waitMs > 0 ? " after " + Seconds(waitMs) : string.Empty;

        private static string Seconds(double ms) => Math.Round(ms / 1000, 2).ToString(CultureInfo.InvariantCulture) + " s";

        /// <summary>
        /// StartAfter, in milliseconds at the current speed.
        /// </summary>
        private static double GetWaitMs(Transition effect, AnimationSettings settings) => effect.StartAfter.Seconds * 1000 / settings.Speed;

        /// <summary>
        /// A ghost that waits for its StartAfter stays on screen longer than usual: it is still
        /// removed if the element appears again before it is gone.
        /// </summary>
        private void KeepGhosted(UIElement element, double ms)
        {
            long until = _clock.ElapsedMilliseconds + (long)ms + 500;
            if (!_ghostedUntil.TryGetValue(element, out long current) || current < until)
            {
                _ghostedUntil[element] = until;
            }
        }

        private static bool IsAnimatable(UIElement element, out string id)
        {
            id = Interop.GetDiv(element).Uid;
            return !string.IsNullOrEmpty(id);
        }

        protected override void OnResponsiveValueChanged(FrameworkElement element, DependencyObject target, DependencyProperty property)
        {
            // The new value is applied between two rounds of the pass, and the next rounds arrange
            // what it changed. While the window or a panel is being resized, what was arranged so
            // far only follows the resize: it is completed on its own, and the rest of the pass
            // starts from there, with the switches as its only origins.
            if (!_switchOnly && _stack.Count == 0 && (BrowserBridge.IsDragging || IsWindowResizePass()))
            {
                if (_recordCount > 0)
                {
                    // The elements that disappear, like one that a switch collapses, belong to the switches.
                    Disappearance[] disappearing = null;
                    if (_disappearing.Count > 0)
                    {
                        disappearing = _disappearing.ToArray();
                        _disappearing.Clear();
                    }

                    try
                    {
                        CompletePass(releaseHeld: false);
                    }
                    catch (Exception ex)
                    {
                        BrowserBridge.WriteToConsole("Something went wrong, nothing was animated: " + ex);
                    }
                    finally
                    {
                        ResetRecords();
                        _disappearing.Clear();
                        if (disappearing != null)
                        {
                            _disappearing.AddRange(disappearing);
                        }
                    }
                }

                _switchOnly = true;
            }

            string description = "{Responsive} switched the " + DescribeProperty(element, target, property);
            AddSwitch(element, description);

            // A value of the parent, like Grid.Row, or a Visibility, changes where the parent puts
            // its children.
            if (VisualTreeHelper.GetParent(element) is UIElement parent)
            {
                AddSwitch(parent, description);
            }
        }

        private void AddSwitch(UIElement element, string description)
        {
            if (!_switches.ContainsKey(element))
            {
                _switches.Add(element, description);
            }
        }

        private bool IsSwitched(UIElement element) => _switches.Count > 0 && _switches.ContainsKey(element);

        private static string DescribeProperty(FrameworkElement element, DependencyObject target, DependencyProperty property)
        {
            if (!ReferenceEquals(target, element))
            {
                return property.Name + " of a " + target.GetType().Name + " in " + ElementNames.Of(element);
            }

            string name = property.OwnerType.IsInstanceOfType(element) ? property.Name : property.OwnerType.Name + "." + property.Name;
            return name + " of " + ElementNames.Of(element);
        }

        protected override void OnLayoutPassCompleted()
        {
            if (_appeared.Count > 0)
            {
                PruneAppeared();
            }

            if (_pageOpenHolds.IsWaitingForPass)
            {
                _pageOpenHolds.OnLayoutPassCompleted(AppearsWithContainer);
            }

            if (_recordCount == 0 && _disappearing.Count == 0 && !_forced && !_skip)
            {
                ReleaseHeld();
                ClearMeasureState();
                return;
            }

            try
            {
                CompletePass(releaseHeld: true);
            }
            catch (Exception ex)
            {
                BrowserBridge.WriteToConsole("Something went wrong, nothing was animated: " + ex);
            }
            finally
            {
                ReleaseHeld();
                Reset();
            }
        }

        #endregion

        #region Grouping

        private int GetOrAddRecord(UIElement element)
        {
            if (_recordIndex.TryGetValue(element, out int index))
            {
                return index;
            }

            if (_recordCount == _records.Length)
            {
                Array.Resize(ref _records, _records.Length * 2);
            }

            index = _recordCount++;
            _records[index] = new Record { Element = element, Parent = -1 };
            _recordIndex.Add(element, index);
            return index;
        }

        /// <summary>
        /// Passes the groups down, in the order the elements were arranged, so that everything
        /// arranged by a member of a group belongs to it.
        /// </summary>
        private void EndArrangeSession()
        {
            foreach (int index in _sessionOrder)
            {
                ref Record record = ref _records[index];
                if (record.Group == 0 && record.Parent >= 0)
                {
                    ref Record parent = ref _records[record.Parent];
                    if (parent.Group != 0 && (parent.Flags & RecordFlags.Scrolled) == 0)
                    {
                        record.Group = parent.Group;
                    }
                }

                if (record.Group == 0 && IsChanged(ref record) &&
                    GetEffectiveMovement(record.Element) is Movement movement && movement.AlsoOnWindowResize)
                {
                    record.Group = NewGroup(GroupKind.WindowResize, movement, record.Element);
                }
            }

            _sessionOrder.Clear();
        }

        private int NewGroup(GroupKind kind, Movement movement, UIElement element)
        {
            int id = _groupParents.Count;
            _groupParents.Add(id);
            _groups.Add(new GroupInfo { Kind = kind, Movement = movement, Element = element });
            return id;
        }

        private int Find(int group)
        {
            while (_groupParents[group] != group)
            {
                int grandParent = _groupParents[_groupParents[group]];
                _groupParents[group] = grandParent;
                group = grandParent;
            }

            return group;
        }

        private int Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a == b)
            {
                return a;
            }

            // A change keeps its own timing when it reaches elements that follow the window.
            if (_groups[a].Kind == GroupKind.WindowResize && _groups[b].Kind == GroupKind.Change)
            {
                (a, b) = (b, a);
            }

            _groupParents[b] = a;
            return a;
        }

        private bool IsScrolled(UIElement element, IScrollInfo scrollInfo)
        {
            if (_scrollChecked.TryGetValue(element, out bool scrolled))
            {
                return scrolled;
            }

            double h = scrollInfo.HorizontalOffset;
            double v = scrollInfo.VerticalOffset;
            ScrollOffset offset = _scrollOffsets.GetOrCreateValue(element);
            scrolled = offset.IsKnown && (!AreClose(offset.Horizontal, h) || !AreClose(offset.Vertical, v));
            offset.Horizontal = h;
            offset.Vertical = v;
            offset.IsKnown = true;
            _scrollChecked[element] = scrolled;
            return scrolled;
        }

        #endregion

        #region Decisions

        private void CompletePass(bool releaseHeld)
        {
            AnimationSettings settings = AnimationSettings.Current;
            bool logging = settings.LogForDebugging || AnimationSettings.HasLoggedHandlers;
            long now = _clock.ElapsedMilliseconds;

            string snapReason = GetGlobalSnapReason(settings);
            _stamp++;

            // Following a resize doesn't stop a {Responsive} switch that animates: it goes on while
            // the edge is dragged further.
            _keepRunning = (BrowserBridge.IsDragging || IsWindowResizePass()) &&
                           (snapReason == null || ReferenceEquals(snapReason, DraggingReason));

            // With reduced motion, effects become quick fades, and everything else snaps.
            _quickFades = IsReducedMotion(snapReason);
            _effectsPlay = snapReason == null || _quickFades || (_switchOnly && ReferenceEquals(snapReason, DraggingReason));

            for (int i = 0; i < _recordCount; i++)
            {
                ref Record record = ref _records[i];
                if ((record.Flags & RecordFlags.Appears) != 0)
                {
                    if ((record.Flags & RecordFlags.Scrolled) != 0)
                    {
                        continue;
                    }

                    if (AnyEffects && GetAppearEffect(record.Element) != null)
                    {
                        _effectAppears.Add(i);
                    }

                    if ((record.Flags & RecordFlags.WithContainer) == 0 && record.Parent >= 0 && (AnyEffects || AnyAreas))
                    {
                        AddAppearing(i);
                    }
                    continue;
                }

                bool changed = IsChanged(ref record);
                bool origin = (record.Flags & RecordFlags.Origin) != 0;
                if (!changed && !origin)
                {
                    continue;
                }

                int group = record.Group == 0 ? 0 : Find(record.Group);
                if (group == 0)
                {
                    if (changed)
                    {
                        Snap(i, now);
                    }
                    continue;
                }

                Bucket bucket = GetBucket(group);
                if (origin)
                {
                    bucket.Origins.Add(i);
                }
                if (changed)
                {
                    bucket.Members.Add(i);
                }
            }

            // An element that disappears is a change of its parent, like one that appears.
            foreach (Disappearance disappearance in _disappearing)
            {
                if (_recordIndex.TryGetValue(disappearance.Parent, out int parentIndex) && _records[parentIndex].Group != 0)
                {
                    int group = Find(_records[parentIndex].Group);
                    if (_groups[group].Kind == GroupKind.Change)
                    {
                        disappearance.ParentIndex = parentIndex;
                        Bucket bucket = GetBucket(group);
                        bucket.Ghosts.Add(disappearance);
                        if (disappearance.Effect != null && bucket.Effect == null)
                        {
                            bucket.Effect = disappearance.Effect;
                            bucket.EffectElement = disappearance.Element;
                            bucket.EffectAppears = false;
                        }
                    }
                }
            }

            var batch = new BatchBuilder();
            foreach (KeyValuePair<int, Bucket> pair in _buckets)
            {
                ProcessGroup(pair.Key, pair.Value, settings, snapReason, logging, now, batch);
            }

            PlayRemainingEffects(settings, snapReason, logging, batch);

            foreach (UIElement element in _toCancel)
            {
                batch.AddCancel(element);
            }

            foreach (int index in _toRetarget)
            {
                ref Record record = ref _records[index];
                if ((!AreClose(record.OldRect.Width, record.NewRect.Width) || !AreClose(record.OldRect.Height, record.NewRect.Height)) &&
                    Interop.GetDiv(record.Element).Uid is string id && id.Length > 0)
                {
                    batch.AddRetarget(id, record.NewRect.Width, record.NewRect.Height);
                }
            }

            if (releaseHeld)
            {
                foreach (UIElement element in _held)
                {
                    batch.AddRelease(element);
                }
                _held.Clear();
            }

            if (!batch.IsEmpty)
            {
                BrowserBridge.Run(batch.ToJson());
            }

            if (_messages.Count > 0)
            {
                Publish(settings);
            }

            if (now >= _nextSweep && (_runningUntil.Count > 0 || _ghostedUntil.Count > 0))
            {
                SweepRunning(now);
            }
        }

        private string GetGlobalSnapReason(AnimationSettings settings)
        {
            if (_skip)
            {
                return "Animate.Skip was used";
            }

            if (!settings.IsEnabled)
            {
                return "AnimationSettings.IsEnabled is false";
            }

            if (DesignerProperties.IsInDesignTool)
            {
                return "the app runs in a designer";
            }

            if (settings.RespectReducedMotion && (BrowserBridge.PrefersReducedMotion || settings.SimulateReducedMotion))
            {
                return settings.SimulateReducedMotion && !BrowserBridge.PrefersReducedMotion
                    ? SimulatedReducedMotionReason
                    : ReducedMotionReason;
            }

            if (BrowserBridge.IsDragging)
            {
                return DraggingReason;
            }

            return null;
        }

        private static bool IsReducedMotion(string reason)
            => ReferenceEquals(reason, ReducedMotionReason) || ReferenceEquals(reason, SimulatedReducedMotionReason);

        /// <summary>
        /// An element that appears with an Appear of its own, or inside an area without a
        /// MoveAndResize=None or an Appear=None of its own, plays its effect with the change that
        /// makes room for it. Inside an area, without an effect of its own, it is wiped or faded in,
        /// if that change animates.
        /// </summary>
        private void AddAppearing(int index)
        {
            ref Record parent = ref _records[_records[index].Parent];
            if (parent.Group == 0)
            {
                return;
            }

            int group = Find(parent.Group);
            if (_groups[group].Kind != GroupKind.Change)
            {
                return;
            }

            UIElement element = _records[index].Element;
            Transition effect = AnyEffects ? GetAppearEffect(element) : null;
            if (effect == null &&
                (GetArea(parent.Element).Movement == null || HasNone(element) || HasNoEffect(element, appear: true)))
            {
                return;
            }

            Bucket bucket = GetBucket(group);
            bucket.Appearing.Add(index);
            if (effect != null && bucket.Effect == null)
            {
                bucket.Effect = effect;
                bucket.EffectElement = element;
                bucket.EffectAppears = true;
            }
        }

        private void ProcessGroup(int group, Bucket bucket, AnimationSettings settings, string snapReason, bool logging, long now, BatchBuilder batch)
        {
            if (bucket.Members.Count == 0 && bucket.Appearing.Count == 0 && bucket.Ghosts.Count == 0)
            {
                return;
            }

            GroupInfo info = _groups[group];
            bool windowResize = info.Kind == GroupKind.WindowResize && bucket.Origins.Count == 0;

            Timing timing = null;
            string source = null;
            string reason = snapReason;
            UIElement firstOrigin = null;
            _owners.Clear();
            _ownerOf.Clear();

            // A {Responsive} value that switches is a change, even while an edge is being dragged.
            if (_switchOnly && ReferenceEquals(reason, DraggingReason) && bucket.Origins.Count > 0)
            {
                reason = null;
            }

            if (reason == null)
            {
                if (windowResize)
                {
                    timing = Timing.FromMovement(info.Movement, settings);
                    source = "the AlsoOnWindowResize of " + ElementNames.Of(info.Element);
                }
                else
                {
                    reason = Decide(group, bucket, settings, out timing, out source, out firstOrigin);
                }
            }
            else if (bucket.Origins.Count > 0)
            {
                firstOrigin = GetDescribedOrigin(bucket);
            }

            if (timing != null && _switchOnly && _owners.Count > 0)
            {
                EmitSwitches(bucket, _forced ? timing : null, settings, logging, now, batch);
                return;
            }

            if (timing != null)
            {
                int visible = CullAndCount(bucket);
                if (visible > MaxAnimatedElements)
                {
                    reason = TooManyReason;
                    timing = null;
                }
                else if (visible == 0 && bucket.Ghosts.Count == 0)
                {
                    return;
                }
            }

            if (timing == null)
            {
                foreach (int index in bucket.Members)
                {
                    Snap(index, now);
                }

                // The effects of the elements that appear or disappear still play, on their own.
                Transition played = PlayEffectsAlone(bucket, settings, batch, out double playedWait);

                if (logging && reason != null && !IsSilent(reason) && (_forced || InvolvesSettings(bucket)))
                {
                    if (!windowResize || now - _lastWindowResizeLog > 1000)
                    {
                        if (windowResize)
                        {
                            _lastWindowResizeLog = now;
                        }

                        if (bucket.Effect != null)
                        {
                            firstOrigin = bucket.EffectElement;
                        }

                        string what;
                        if (played == null)
                        {
                            what = "nothing animated because " + reason;
                        }
                        else
                        {
                            Timing effectTiming = _quickFades ? QuickFadeTiming : GetEffectTiming(played, settings);
                            what = DescribeOwnEffect(played, bucket.EffectAppears, _quickFades, effectTiming, reason, playedWait) +
                                   (_quickFades ? ", and nothing else animated" : ", and nothing else animated because " + reason);
                        }

                        LogSnap(Describe(bucket, firstOrigin, windowResize) + "; " + what + ".", now);
                    }
                }
                return;
            }

            Emit(bucket, timing, firstOrigin, settings, now, batch);

            if (logging)
            {
                if (windowResize)
                {
                    if (now - _lastWindowResizeLog > 1000)
                    {
                        _lastWindowResizeLog = now;
                        _messages.Add($"The window was resized; {ElementNames.Of(info.Element)} follows it ({timing.Description}, from {source}).");
                    }
                }
                else
                {
                    _messages.Add($"{Describe(bucket, firstOrigin, false)}; {DescribeAnimated(bucket)} ({timing.Description}, from {source}).");
                }
            }
        }

        /// <summary>
        /// Says whether a group with origins animates. Returns why not, or null when it animates.
        /// </summary>
        /// <remarks>
        /// The nearest setting wins. The timing comes from the first of: Animate.Changes; going up
        /// from an origin, its own setting, the first EverythingInside, or the first other setting
        /// of an element that moves or changes size in this change; the nearest member, except for
        /// a {Responsive} switch during a resize, where the members also follow the resize. For
        /// such a switch, the elements whose settings are found going up from each origin are
        /// collected in <see cref="_owners"/>, each with its origins.
        /// </remarks>
        private string Decide(int group, Bucket bucket, AnimationSettings settings, out Timing timing, out string source, out UIElement firstOrigin)
        {
            timing = null;
            source = null;
            firstOrigin = null;

            if (bucket.Origins.Count == 0)
            {
                // The change came from outside: a window resize or a parent that was arranged
                // differently, with no element that opted in to follow it.
                return "it came from resizing the window or from outside the elements that have settings";
            }

            UIElement focusedTextInput = GetFocusedTextInput();
            string exclusion = null;
            UIElement eligible = null;
            Movement nearest = null;
            UIElement nearestOwner = null;
            UIElement nearestOrigin = null;
            int nearestLevels = int.MaxValue;

            foreach (int index in bucket.Origins)
            {
                UIElement origin = _records[index].Element;

                if (!_forced)
                {
                    if (focusedTextInput != null && IsStrictDescendant(origin, focusedTextInput))
                    {
                        exclusion = "the change came from typing in " + ElementNames.Of(focusedTextInput);
                        continue;
                    }

                    if (IsScrollChrome(origin))
                    {
                        exclusion = ScrollBarReason;
                        continue;
                    }

                    if ((_records[index].Flags & RecordFlags.Released) == 0 && IsSettling(origin))
                    {
                        exclusion = SettlingReason;
                        continue;
                    }
                }

                Area area = GetArea(origin);
                if (area.IsCarvedOut && !_forced)
                {
                    exclusion = "the change started " + (ReferenceEquals(origin, area.Element) ? "in " : "inside ") +
                                ElementNames.Of(area.Element) + ", which has MoveAndResize=None";
                    continue;
                }

                if (eligible == null || (IsTemplatePart(eligible) && !IsTemplatePart(origin)))
                {
                    eligible = origin;
                }

                int levels = FindNearestMovement(origin, group, _switchOnly ? int.MaxValue : nearestLevels, out Movement movement, out UIElement movementOwner);
                if (_switchOnly && movement != null)
                {
                    if (!_ownerOf.TryGetValue(movementOwner, out Owner owner))
                    {
                        owner = new Owner { Element = movementOwner, Movement = movement };
                        _owners.Add(owner);
                        _ownerOf.Add(movementOwner, owner);
                    }
                    owner.Part.Origins.Add(index);
                }

                if (movement != null &&
                    (levels < nearestLevels || (IsTemplatePart(nearestOrigin) && !IsTemplatePart(origin))))
                {
                    nearest = movement;
                    nearestOwner = movementOwner;
                    nearestOrigin = origin;
                    nearestLevels = levels;
                }
            }

            if (eligible == null)
            {
                firstOrigin = GetDescribedOrigin(bucket);
                return exclusion;
            }

            firstOrigin = nearestOrigin ?? eligible;

            if (_forced)
            {
                timing = Timing.FromDuration(_forcedDuration, settings);
                source = "Animate.Changes";
                if (bucket.Effect != null)
                {
                    firstOrigin = bucket.EffectElement;
                }
                return null;
            }

            // An element that appears or disappears with an effect: the change starts in its parent,
            // and the effect is the nearest setting.
            if (bucket.Effect != null && !_switchOnly)
            {
                timing = Timing.ForOthers(GetEffectTiming(bucket.Effect, settings), bucket.Effect);
                source = (bucket.EffectAppears ? "the Appear of " : "the Disappear of ") + ElementNames.Of(bucket.EffectElement);
                firstOrigin = bucket.EffectElement;
                return null;
            }

            if (nearest != null)
            {
                timing = Timing.FromMovement(nearest, settings);
                source = DescribeSource(nearest, nearestOwner, nearestOrigin);
                return null;
            }

            if (_switchOnly)
            {
                return "neither " + ElementNames.Of(eligible) + " nor an element around it has MoveAndResize";
            }

            if (FindNearestMemberMovement(bucket, eligible, out UIElement memberOwner) is Movement memberMovement)
            {
                timing = Timing.FromMovement(memberMovement, settings);
                source = "the MoveAndResize of " + ElementNames.Of(memberOwner);
                return null;
            }

            return "no element involved has MoveAndResize";
        }

        /// <summary>
        /// During a resize, each setting found going up from the {Responsive} switches animates
        /// what is inside it, with its own timing and message, and the switches that found it as
        /// origins. The rest of the page follows the resize, and may also have been arranged again
        /// because of something else, like a container that changes its own layout at the same width.
        /// Inside Animate.Changes, every setting uses its timing (<paramref name="forcedTiming"/>).
        /// </summary>
        private void EmitSwitches(Bucket bucket, Timing forcedTiming, AnimationSettings settings, bool logging, long now, BatchBuilder batch)
        {
            foreach (int index in bucket.Members)
            {
                if (FindOwner(_records[index].Element) is Owner owner)
                {
                    owner.Part.Members.Add(index);
                }
                else
                {
                    Snap(index, now);
                }
            }

            foreach (int index in bucket.Appearing)
            {
                FindOwner(_records[index].Element)?.Part.Appearing.Add(index);
            }

            foreach (Disappearance disappearance in bucket.Ghosts)
            {
                FindOwner(disappearance.Parent)?.Part.Ghosts.Add(disappearance);
            }

            foreach (Owner owner in _owners)
            {
                Bucket part = owner.Part;
                if (part.Members.Count == 0 && part.Appearing.Count == 0 && part.Ghosts.Count == 0)
                {
                    continue;
                }

                UIElement origin = GetDescribedOrigin(part);
                int visible = CullAndCount(part);
                if (visible == 0 && part.Ghosts.Count == 0)
                {
                    continue;
                }

                if (visible > MaxAnimatedElements)
                {
                    foreach (int index in part.Members)
                    {
                        Snap(index, now);
                    }

                    if (logging)
                    {
                        LogSnap(Describe(part, origin, false) + "; nothing animated because " + TooManyReason + ".", now);
                    }
                    continue;
                }

                Timing timing = forcedTiming ?? Timing.FromMovement(owner.Movement, settings);
                Emit(part, timing, origin, settings, now, batch);

                if (logging)
                {
                    string source = forcedTiming != null ? "Animate.Changes" : DescribeSource(owner.Movement, owner.Element, origin);
                    _messages.Add($"{Describe(part, origin, false)}; {DescribeAnimated(part)} ({timing.Description}, from {source}).");
                }
            }
        }

        /// <summary>
        /// The nearest of the <see cref="_owners"/> that is the element or contains it, or null.
        /// </summary>
        private Owner FindOwner(UIElement element)
        {
            for (DependencyObject e = element; e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is UIElement u && _ownerOf.TryGetValue(u, out Owner owner))
                {
                    return owner;
                }
            }

            return null;
        }

        private static string DescribeSource(Movement movement, UIElement owner, UIElement origin)
            => (movement.ApplyTo == AnimationScope.EverythingInside && owner != origin
                ? "the EverythingInside of "
                : "the MoveAndResize of ") + ElementNames.Of(owner);

        /// <summary>
        /// The setting that gives its timing to a change that starts at the origin: its own, even
        /// when its box stays the same, or else the nearest ancestor that has EverythingInside, or
        /// another setting and moves or changes size in this change. An ancestor with another
        /// setting that stays in place doesn't count: its setting is about its own box. Returns
        /// how many levels up the setting is, looking at most <paramref name="maxLevels"/> up.
        /// </summary>
        private int FindNearestMovement(UIElement origin, int group, int maxLevels, out Movement movement, out UIElement owner)
        {
            if (Animate.GetMoveAndResize(origin) is Movement own && !own.IsNone)
            {
                movement = own;
                owner = origin;
                return 0;
            }

            int levels = 1;
            for (UIElement e = VisualTreeHelper.GetParent(origin) as UIElement; e != null && levels <= maxLevels; e = VisualTreeHelper.GetParent(e) as UIElement, levels++)
            {
                if (Animate.GetMoveAndResize(e) is not Movement m)
                {
                    continue;
                }

                if (m.IsNone)
                {
                    break;
                }

                if (m.ApplyTo == AnimationScope.EverythingInside || IsMember(e, group))
                {
                    movement = m;
                    owner = e;
                    return levels;
                }
            }

            movement = null;
            owner = null;
            return int.MaxValue;
        }

        private bool IsMember(UIElement element, int group)
        {
            if (!_recordIndex.TryGetValue(element, out int index))
            {
                return false;
            }

            ref Record record = ref _records[index];
            return record.Group != 0 && (record.Flags & RecordFlags.Appears) == 0 && Find(record.Group) == group && IsChanged(ref record);
        }

        /// <summary>
        /// The setting of the member that is the fewest steps away from the origin in the tree.
        /// </summary>
        private Movement FindNearestMemberMovement(Bucket bucket, UIElement origin, out UIElement owner)
        {
            owner = null;
            Movement best = null;
            int bestHops = int.MaxValue;
            int originIndex = _recordIndex.TryGetValue(origin, out int oi) ? oi : -1;

            foreach (int index in bucket.Members)
            {
                UIElement element = _records[index].Element;
                if (GetEffectiveMovement(element) is not Movement movement)
                {
                    continue;
                }

                int hops = Hops(originIndex, index);
                if (hops < bestHops)
                {
                    bestHops = hops;
                    best = movement;
                    owner = element;
                }
            }

            return best;
        }

        private int Hops(int from, int to)
        {
            if (from < 0)
            {
                return int.MaxValue - 1;
            }

            int up = 0;
            for (int a = from; a >= 0; a = _records[a].Parent, up++)
            {
                int down = 0;
                for (int b = to; b >= 0; b = _records[b].Parent, down++)
                {
                    if (a == b)
                    {
                        return up + down;
                    }
                }
            }

            return int.MaxValue - 1;
        }

        /// <summary>
        /// Says whether the group would have animated without the reason it snapped: only then
        /// is the snap worth a message. After a {Responsive} switch during a resize, only the
        /// settings around the switches count.
        /// </summary>
        private bool InvolvesSettings(Bucket bucket)
        {
            if (bucket.Effect != null)
            {
                return true;
            }

            if (!_switchOnly)
            {
                foreach (int index in bucket.Members)
                {
                    if (GetEffectiveMovement(_records[index].Element) != null)
                    {
                        return true;
                    }
                }
            }

            foreach (int index in bucket.Origins)
            {
                UIElement origin = _records[index].Element;
                if (GetEffectiveMovement(origin) != null || GetArea(origin).IsCarvedOut)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Typing and dragging repeat the same change many times a second: the same message is
        /// written at most once per second.
        /// </summary>
        private void LogSnap(string message, long now)
        {
            if (message == _lastSnapMessage && now - _lastSnapLog < 1000)
            {
                return;
            }

            _lastSnapMessage = message;
            _lastSnapLog = now;
            _messages.Add(message);
        }

        private string Describe(Bucket bucket, UIElement origin, bool windowResize)
        {
            if (origin == null)
            {
                return windowResize ? "The window was resized" : "Elements moved because of a change outside them";
            }

            if (_switches.Count > 0 && _switches.TryGetValue(origin, out string switched))
            {
                int others = CountOtherSwitches(bucket, switched);
                return others == 0 ? switched : switched + ", and " + ElementNames.Count(others, "other value", "other values");
            }

            if (origin == bucket.EffectElement)
            {
                return bucket.EffectAppears
                    ? DescribeAppeared(origin, _recordIndex.TryGetValue(origin, out int effectIndex) && (_records[effectIndex].Flags & RecordFlags.Released) != 0,
                                       bucket.Effect, AnimationSettings.Current)
                    : ElementNames.Of(origin) + " disappeared";
            }

            if (!_recordIndex.TryGetValue(origin, out int originIndex))
            {
                return ElementNames.Of(origin) + " changed";
            }

            ref Record record = ref _records[originIndex];
            string what;
            if (!IsChanged(ref record))
            {
                what = "rearranged its content";
            }
            else if (AreClose(record.OldRect.Width, record.NewRect.Width) && AreClose(record.OldRect.Height, record.NewRect.Height))
            {
                what = "moved";
            }
            else
            {
                what = "changed size";
            }

            return ElementNames.Of(origin) + " " + what;
        }

        /// <summary>
        /// "Banner appeared", or, for an element held while its page opened, "Banner appeared 0.6 s
        /// after its page opened, as OnPageOpen=PushOthers asks".
        /// </summary>
        private static string DescribeAppeared(UIElement element, bool released, Transition effect, AnimationSettings settings)
        {
            if (!released)
            {
                return ElementNames.Of(element) + " appeared";
            }

            double wait = GetWaitMs(effect, settings);
            return ElementNames.Of(element) + (wait > 0 ? " appeared " + Seconds(wait) : " appeared right") +
                   " after its page opened, as OnPageOpen=PushOthers asks";
        }

        private int CountOtherSwitches(Bucket bucket, string described)
        {
            HashSet<string> others = null;
            foreach (int index in bucket.Origins)
            {
                if (_switches.TryGetValue(_records[index].Element, out string switched) && switched != described)
                {
                    (others ??= new HashSet<string>()).Add(switched);
                }
            }

            return others?.Count ?? 0;
        }

        /// <summary>
        /// "it fades in, and 5 elements animate together", "it wipes in, Body0 wipes out, and 5
        /// elements animate together", "3 elements animate together, and Notes wipes out".
        /// </summary>
        private static string DescribeAnimated(Bucket bucket)
        {
            string moved = bucket.Animated.Count == 0 ? null : ElementNames.Count(bucket.Animated.Count, "element animates", "elements animate together");
            string others = bucket.OtherEffects == 0 ? null
                : bucket.OtherEffects == 1 ? bucket.OtherEffect
                : bucket.OtherEffects + " other elements play their effects";
            var parts = new List<string>(5);

            if (bucket.EffectPlayed)
            {
                Transition effect = bucket.Effect;
                string own = "it " + effect.Describe(bucket.EffectAppears) + After(bucket.EffectWait);
                if (moved != null && bucket.EffectAppears && effect.OthersMove == OthersMove.Before)
                {
                    own = moved + ", then " + own;
                    moved = null;
                }
                else if (moved != null && !bucket.EffectAppears && effect.OthersMove == OthersMove.After)
                {
                    own = own + ", then " + moved;
                    moved = null;
                }

                parts.Add(own);
            }

            parts.Add(others);
            parts.Add(moved);
            parts.Add(DescribeAutomatic(bucket.Wipes, bucket.Fades, bucket.RevealedElement, "in"));
            parts.Add(DescribeAutomatic(bucket.GhostWipes, bucket.GhostFades, bucket.VanishedElement, "out"));
            return JoinAll(parts) ?? "nothing moved";
        }

        private static string DescribeAutomatic(int wipes, int fades, UIElement single, string direction)
        {
            int count = wipes + fades;
            if (count == 0)
            {
                return null;
            }

            string how = fades == 0 ? "wipe" : wipes == 0 ? "fade" : "wipe or fade";
            return count == 1
                ? ElementNames.Of(single) + " " + how + "s " + direction
                : count + " elements " + how + " " + direction;
        }

        /// <summary>
        /// "a", "a, and b", "a, b, and c", skipping the parts that are null.
        /// </summary>
        private static string JoinAll(List<string> parts)
        {
            parts.RemoveAll(part => part == null);
            if (parts.Count == 0)
            {
                return null;
            }

            string last = parts[parts.Count - 1];
            return parts.Count == 1 ? last : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + ", and " + last;
        }

        #endregion

        #region Areas and settings

        /// <summary>
        /// The movement that applies to the element itself, through its own setting or the area
        /// it is in, or null.
        /// </summary>
        private Movement GetEffectiveMovement(UIElement element)
        {
            if (Animate.GetMoveAndResize(element) is Movement own)
            {
                return own.IsNone ? null : own;
            }

            return GetArea(element).Movement;
        }

        private Area GetArea(UIElement element)
        {
            if (!AnyAreas || element == null)
            {
                return default;
            }

            if (_areaCache.TryGetValue(element, out Area area))
            {
                return area;
            }

            if (Animate.GetMoveAndResize(element) is Movement own)
            {
                if (own.IsNone)
                {
                    area = new Area { IsCarvedOut = true, Element = element };
                }
                else if (own.ApplyTo == AnimationScope.EverythingInside)
                {
                    area = new Area { Movement = own, Element = element };
                }
                else
                {
                    area = GetArea(VisualTreeHelper.GetParent(element) as UIElement);
                }
            }
            else
            {
                area = GetArea(VisualTreeHelper.GetParent(element) as UIElement);
            }

            _areaCache[element] = area;
            return area;
        }

        private static bool HasNone(UIElement element)
            => Animate.GetMoveAndResize(element) is Movement own && own.IsNone;

        /// <summary>
        /// Says whether an element would be revealed when it appears. Called outside the layout
        /// passes, so nothing is memoized.
        /// </summary>
        private static bool IsInsideArea(UIElement element)
        {
            if (HasNone(element))
            {
                return false;
            }

            for (UIElement e = VisualTreeHelper.GetParent(element) as UIElement; e != null; e = VisualTreeHelper.GetParent(e) as UIElement)
            {
                if (Animate.GetMoveAndResize(e) is Movement movement)
                {
                    if (movement.IsNone)
                    {
                        return false;
                    }

                    if (movement.ApplyTo == AnimationScope.EverythingInside)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Shows the elements hidden since they became visible that were not wiped or faded in.
        /// </summary>
        private void ReleaseHeld()
        {
            if (_held.Count == 0)
            {
                return;
            }

            var batch = new BatchBuilder();
            foreach (UIElement element in _held)
            {
                batch.AddRelease(element);
            }
            _held.Clear();

            BrowserBridge.Run(batch.ToJson());
        }

        private static UIElement GetFocusedTextInput()
        {
            return FocusManager.GetFocusedElement() switch
            {
                TextBox tb => tb,
                PasswordBox pb => pb,
                RichTextBox rtb => rtb,
                _ => null,
            };
        }

        private static bool IsStrictDescendant(UIElement element, UIElement ancestor)
        {
            for (DependencyObject e = VisualTreeHelper.GetParent(element); e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (ReferenceEquals(e, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Right after an element appears, its layout often settles over a few passes: a scroll bar
        /// shows up, a font or an image loads. Those changes are part of appearing.
        /// </summary>
        private bool IsSettling(UIElement origin)
        {
            if (_appeared.Count == 0)
            {
                return false;
            }

            long now = _clock.ElapsedMilliseconds;
            for (DependencyObject e = origin; e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is UIElement element && _appeared.TryGetValue(element, out long appearedAt) && now - appearedAt < SettleMs)
                {
                    return true;
                }
            }

            return false;
        }

        private void PruneAppeared()
        {
            long now = _clock.ElapsedMilliseconds;
            foreach (KeyValuePair<UIElement, long> pair in _appeared)
            {
                if (now - pair.Value >= SettleMs)
                {
                    _expired.Add(pair.Key);
                }
            }

            foreach (UIElement element in _expired)
            {
                _appeared.Remove(element);
            }
            _expired.Clear();
        }

        /// <summary>
        /// The origin that the message names: one from the page rather than a part of a control
        /// template, like the repeat buttons of a slider.
        /// </summary>
        private UIElement GetDescribedOrigin(Bucket bucket)
        {
            foreach (int index in bucket.Origins)
            {
                if (!IsTemplatePart(_records[index].Element))
                {
                    return _records[index].Element;
                }
            }

            return _records[bucket.Origins[0]].Element;
        }

        private static bool IsTemplatePart(UIElement element)
            => element is FrameworkElement fe && fe.TemplatedParent != null;

        private static bool IsSilent(string reason)
            => ReferenceEquals(reason, ScrollBarReason) || ReferenceEquals(reason, SettlingReason);

        /// <summary>
        /// Scroll bars and the parts of a scroll viewer only change as a consequence: the content
        /// grew, or a scroll bar was shown or hidden.
        /// </summary>
        private static bool IsScrollChrome(UIElement element)
        {
            if (element is FrameworkElement fe && fe.TemplatedParent is ScrollViewer)
            {
                return true;
            }

            DependencyObject e = element;
            for (int depth = 0; e != null && depth < 8; depth++, e = VisualTreeHelper.GetParent(e))
            {
                if (e is ScrollBar)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Emission

        /// <summary>
        /// Leaves out the members that are far outside the window before and after the change.
        /// Returns how many remain.
        /// </summary>
        private int CullAndCount(Bucket bucket)
        {
            bucket.Animated.Clear();
            bucket.Revealed.Clear();
            Rect viewport = GetViewport();

            foreach (int index in bucket.Members)
            {
                ref Record record = ref _records[index];
                if (!viewport.IsEmpty)
                {
                    EnsureAbsolute(index);
                    var oldRect = new Rect(_absOld[index], record.OldRect.Size);
                    var newRect = new Rect(_absNew[index], record.NewRect.Size);
                    if (!oldRect.IntersectsWith(viewport) && !newRect.IntersectsWith(viewport))
                    {
                        continue;
                    }
                }

                bucket.Animated.Add(index);
            }

            foreach (int index in bucket.Appearing)
            {
                if (!viewport.IsEmpty)
                {
                    EnsureAbsolute(index);
                    if (!new Rect(_absNew[index], _records[index].NewRect.Size).IntersectsWith(viewport))
                    {
                        continue;
                    }
                }

                bucket.Revealed.Add(index);
            }

            return bucket.Animated.Count + bucket.Revealed.Count;
        }

        private void Emit(Bucket bucket, Timing timing, UIElement origin, AnimationSettings settings, long now, BatchBuilder batch)
        {
            int timingIndex = batch.AddTiming(timing);
            bool wave = timing.Wave > 0 && origin != null;
            bool fromSwitch = _switchOnly && bucket.Origins.Count > 0;
            int originIndex = origin != null && _recordIndex.TryGetValue(origin, out int oi) ? oi : -1;

            // OthersMove=After: the group waits for the element that disappears, StartAfter included.
            double othersDelay = bucket.Effect != null && !bucket.EffectAppears && bucket.Effect.OthersMove == OthersMove.After
                ? GetEffectTiming(bucket.Effect, settings).DurationMs + GetWaitMs(bucket.Effect, settings)
                : 0;

            if (wave)
            {
                EnsureCapacity(ref _delays, _recordCount);
                EnsureCapacity(ref _memberStamp, _recordCount);
                EnsureCapacity(ref _chainStamp, _recordCount);
                _waveStamp++;
                foreach (int index in bucket.Animated)
                {
                    _memberStamp[index] = _waveStamp;
                    _delays[index] = double.NaN;
                }
                foreach (int index in bucket.Origins)
                {
                    _memberStamp[index] = _waveStamp;
                    _delays[index] = 0;
                    for (int parent = _records[index].Parent; parent >= 0 && _chainStamp[parent] != _waveStamp; parent = _records[parent].Parent)
                    {
                        _chainStamp[parent] = _waveStamp;
                    }
                }
            }

            foreach (int index in bucket.Animated)
            {
                ref Record record = ref _records[index];
                UIElement element = record.Element;
                string id = Interop.GetDiv(element).Uid;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                double delay = (wave ? GetWaveDelay(index, originIndex, timing.Wave) / settings.Speed : 0) + othersDelay;
                int flags = GetFlags(element, ref record);

                batch.AddMove(id, timingIndex, delay,
                    record.OldRect.X - record.NewRect.X,
                    record.OldRect.Y - record.NewRect.Y,
                    record.OldRect.Width, record.OldRect.Height,
                    record.NewRect.Width, record.NewRect.Height,
                    flags);

                _runningUntil[element] = (now + (long)(delay + timing.DurationMs) + 50, fromSwitch);

                if (settings.ShowWhatAnimates)
                {
                    batch.AddOutline(id, timing.DurationMs, delay, element == origin);
                }
            }

            bucket.Wipes = bucket.Fades = bucket.GhostWipes = bucket.GhostFades = bucket.OtherEffects = 0;
            bucket.OtherEffect = null;
            bucket.EffectPlayed = false;
            bucket.EffectWait = 0;
            bool othersMove = bucket.Animated.Count > 0;
            foreach (int index in bucket.Revealed)
            {
                UIElement element = _records[index].Element;
                string id = Interop.GetDiv(element).Uid;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                // OthersMove=Before: the element waits for the room to be made, then for its StartAfter.
                if (AnyEffects && GetAppearEffect(element) is Transition effect)
                {
                    double effectDelay = effect.OthersMove == OthersMove.Before && othersMove ? timing.DurationMs : 0;
                    double wait = PlayAppearEffect(index, effect, effectDelay, raise: othersMove, settings, batch);
                    CountEffect(bucket, element, effect, appears: true, wait);
                    continue;
                }

                int edge = GetRevealEdge(index, out int pusher);
                double delay = (wave && pusher >= 0 && _memberStamp[pusher] == _waveStamp
                    ? GetWaveDelay(pusher, originIndex, timing.Wave) / settings.Speed
                    : 0) + othersDelay;

                batch.AddReveal(id, timingIndex, delay, edge);
                bucket.RevealedElement = element;
                if (edge == BatchBuilder.Fade)
                {
                    bucket.Fades++;
                }
                else
                {
                    bucket.Wipes++;
                }

                if (settings.ShowWhatAnimates)
                {
                    batch.AddOutline(id, timing.DurationMs, delay, false);
                }
            }

            // The elements that disappear: with their own effect, or wiped or faded out with the
            // curve of the sizes, toward the side the elements next to them come from.
            foreach (Disappearance disappearance in bucket.Ghosts)
            {
                if (disappearance.Effect != null)
                {
                    double wait = PlayGhost(disappearance, settings, batch);
                    CountEffect(bucket, disappearance.Element, disappearance.Effect, appears: false, wait);
                    continue;
                }

                int edge = disappearance.ParentIndex >= 0 ? GetDisappearEdge(disappearance.ParentIndex) : BatchBuilder.Fade;
                batch.AddGhost(disappearance.Id, timingIndex, 0, edge == BatchBuilder.Fade ? FadeLook : new EffectLook { Wipe = edge }, sizeCurve: true);
                disappearance.Handled = true;
                bucket.VanishedElement = disappearance.Element;
                if (edge == BatchBuilder.Fade)
                {
                    bucket.GhostFades++;
                }
                else
                {
                    bucket.GhostWipes++;
                }
            }

            if (settings.ShowWhatAnimates)
            {
                foreach (int index in bucket.Origins)
                {
                    UIElement element = _records[index].Element;
                    if (!IsChanged(ref _records[index]) && Interop.GetDiv(element).Uid is string id && id.Length > 0)
                    {
                        batch.AddOutline(id, timing.DurationMs, 0, true);
                    }
                }
            }
        }

        /// <summary>
        /// Members farther from the origin start later. Elements inside a delayed member share its
        /// delay, and elements inside the origin start at once. The containers of the origin pass
        /// nothing down, so the elements next to the origin each get their own delay.
        /// </summary>
        private double GetWaveDelay(int index, int originIndex, double wave)
        {
            if (!double.IsNaN(_delays[index]))
            {
                return _delays[index];
            }

            for (int parent = _records[index].Parent; parent >= 0 && _chainStamp[parent] != _waveStamp; parent = _records[parent].Parent)
            {
                if (_memberStamp[parent] == _waveStamp)
                {
                    return _delays[index] = GetWaveDelay(parent, originIndex, wave);
                }
            }

            double delay = 0;
            if (originIndex >= 0)
            {
                EnsureAbsolute(index);
                EnsureAbsolute(originIndex);
                var member = new Rect(_absNew[index], _records[index].NewRect.Size);
                var origin = new Rect(_absNew[originIndex], _records[originIndex].NewRect.Size);
                double dx = Math.Max(0, Math.Max(origin.Left - member.Right, member.Left - origin.Right));
                double dy = Math.Max(0, Math.Max(origin.Top - member.Bottom, member.Top - origin.Bottom));
                double distance = Math.Sqrt(dx * dx + dy * dy);
                delay = wave * MaxWaveDelayMs * Math.Min(1, distance / WaveDistance);
            }

            return _delays[index] = delay;
        }

        /// <summary>
        /// The edge an appearing element is revealed from: the side it pushes the elements next to
        /// it away from, or its top or left edge when it makes its container grow. When it pushes
        /// nothing, it fades in. <paramref name="pusher"/> is the element whose move it follows.
        /// </summary>
        private int GetRevealEdge(int index, out int pusher)
        {
            int parent = _records[index].Parent;
            int edge = BatchBuilder.Fade;
            double farthest = 0.5;
            pusher = -1;

            for (int i = 0; i < _recordCount; i++)
            {
                ref Record sibling = ref _records[i];
                if (sibling.Parent != parent || i == index || (sibling.Flags & RecordFlags.Appears) != 0 || !IsChanged(ref sibling))
                {
                    continue;
                }

                double dx = sibling.NewRect.X - sibling.OldRect.X;
                double dy = sibling.NewRect.Y - sibling.OldRect.Y;
                if (Math.Abs(dy) > farthest)
                {
                    farthest = Math.Abs(dy);
                    edge = dy > 0 ? BatchBuilder.RevealFromTop : BatchBuilder.RevealFromBottom;
                    pusher = i;
                }
                if (Math.Abs(dx) > farthest)
                {
                    farthest = Math.Abs(dx);
                    edge = dx > 0 ? BatchBuilder.RevealFromLeft : BatchBuilder.RevealFromRight;
                    pusher = i;
                }
            }

            if (edge == BatchBuilder.Fade && IsChanged(ref _records[parent]))
            {
                ref Record container = ref _records[parent];
                double grewHeight = container.NewRect.Height - container.OldRect.Height;
                double grewWidth = container.NewRect.Width - container.OldRect.Width;
                if (grewHeight > 0.5 && grewHeight >= grewWidth)
                {
                    edge = BatchBuilder.RevealFromTop;
                    pusher = parent;
                }
                else if (grewWidth > 0.5)
                {
                    edge = BatchBuilder.RevealFromLeft;
                    pusher = parent;
                }
            }

            return edge;
        }

        /// <summary>
        /// The edge an element that disappears is hidden toward, the opposite of
        /// <see cref="GetRevealEdge"/>: the elements next to it come from the other side into its
        /// place, or its container shrinks. When nothing fills its place, it fades out.
        /// </summary>
        private int GetDisappearEdge(int parent)
        {
            int edge = BatchBuilder.Fade;
            double farthest = 0.5;

            for (int i = 0; i < _recordCount; i++)
            {
                ref Record sibling = ref _records[i];
                if (sibling.Parent != parent || (sibling.Flags & RecordFlags.Appears) != 0 || !IsChanged(ref sibling))
                {
                    continue;
                }

                double dx = sibling.NewRect.X - sibling.OldRect.X;
                double dy = sibling.NewRect.Y - sibling.OldRect.Y;
                if (Math.Abs(dy) > farthest)
                {
                    farthest = Math.Abs(dy);
                    edge = dy < 0 ? BatchBuilder.RevealFromTop : BatchBuilder.RevealFromBottom;
                }
                if (Math.Abs(dx) > farthest)
                {
                    farthest = Math.Abs(dx);
                    edge = dx < 0 ? BatchBuilder.RevealFromLeft : BatchBuilder.RevealFromRight;
                }
            }

            if (edge == BatchBuilder.Fade && IsChanged(ref _records[parent]))
            {
                ref Record container = ref _records[parent];
                double shrankHeight = container.OldRect.Height - container.NewRect.Height;
                double shrankWidth = container.OldRect.Width - container.NewRect.Width;
                if (shrankHeight > 0.5 && shrankHeight >= shrankWidth)
                {
                    edge = BatchBuilder.RevealFromTop;
                }
                else if (shrankWidth > 0.5)
                {
                    edge = BatchBuilder.RevealFromLeft;
                }
            }

            return edge;
        }

        /// <summary>
        /// Plays the Appear of an element, after <paramref name="delay"/> and its StartAfter.
        /// Returns how long it waits for its StartAfter: nothing for an element held while its page
        /// opened, which has already waited.
        /// </summary>
        private double PlayAppearEffect(int index, Transition effect, double delay, bool raise, AnimationSettings settings, BatchBuilder batch)
        {
            ref Record record = ref _records[index];
            record.Flags |= RecordFlags.Played;
            double wait = (record.Flags & RecordFlags.Released) != 0 ? 0 : GetWaitMs(effect, settings);
            if (!IsAnimatable(record.Element, out string id))
            {
                return wait;
            }

            if (_quickFades)
            {
                batch.AddEffect(id, batch.AddTiming(QuickFadeTiming), wait, FadeLook, raise: false);
            }
            else
            {
                Timing timing = GetEffectTiming(effect, settings);
                batch.AddEffect(id, batch.AddTiming(timing), delay + wait, effect.GetLook(appear: true), raise);
                if (settings.ShowWhatAnimates)
                {
                    batch.AddOutline(id, timing.DurationMs, delay + wait, false);
                }
            }

            return wait;
        }

        /// <summary>
        /// Plays the Disappear of a ghost after its StartAfter, and returns how long it waits.
        /// </summary>
        private double PlayGhost(Disappearance disappearance, AnimationSettings settings, BatchBuilder batch)
        {
            disappearance.Handled = true;
            double wait = GetWaitMs(disappearance.Effect, settings);
            Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(disappearance.Effect, settings);
            EffectLook look = _quickFades ? FadeLook : disappearance.Effect.GetLook(appear: false);
            batch.AddGhost(disappearance.Id, batch.AddTiming(timing), wait, look, sizeCurve: false);
            if (wait > 0)
            {
                KeepGhosted(disappearance.Element, wait + timing.DurationMs);
            }

            return wait;
        }

        private static void CountEffect(Bucket bucket, UIElement element, Transition effect, bool appears, double waitMs)
        {
            if (element == bucket.EffectElement)
            {
                bucket.EffectPlayed = true;
                bucket.EffectWait = waitMs;
            }
            else if (bucket.OtherEffects++ == 0)
            {
                bucket.OtherEffect = ElementNames.Of(element) + " " + effect.Describe(appears) + After(waitMs);
            }
        }

        /// <summary>
        /// Plays the effects of the elements that appear or disappear in a group that snaps.
        /// Returns the effect of the element the message names, if it played, and how long it waits.
        /// </summary>
        private Transition PlayEffectsAlone(Bucket bucket, AnimationSettings settings, BatchBuilder batch, out double playedWait)
        {
            playedWait = 0;
            if (!_effectsPlay || (!AnyEffects && bucket.Ghosts.Count == 0))
            {
                return null;
            }

            Transition played = null;
            foreach (int index in bucket.Appearing)
            {
                if ((_records[index].Flags & RecordFlags.Played) == 0 && GetAppearEffect(_records[index].Element) is Transition effect)
                {
                    double wait = PlayAppearEffect(index, effect, 0, raise: false, settings, batch);
                    if (_records[index].Element == bucket.EffectElement)
                    {
                        played = effect;
                        playedWait = wait;
                    }
                }
            }

            foreach (Disappearance disappearance in bucket.Ghosts)
            {
                if (!disappearance.Handled && disappearance.Effect != null)
                {
                    double wait = PlayGhost(disappearance, settings, batch);
                    if (disappearance.Element == bucket.EffectElement)
                    {
                        played = disappearance.Effect;
                        playedWait = wait;
                    }
                }
            }

            return played;
        }

        /// <summary>
        /// Plays the effects that no group played: the elements that appear with their container,
        /// and those whose parent didn't change. Removes the ghosts that have nothing to play.
        /// </summary>
        private void PlayRemainingEffects(AnimationSettings settings, string snapReason, bool logging, BatchBuilder batch)
        {
            int withContainer = 0;
            UIElement firstWithContainer = null;
            Transition firstEffect = null;
            double firstWait = 0;
            int skipped = 0;
            UIElement firstSkipped = null;

            foreach (int index in _effectAppears)
            {
                ref Record record = ref _records[index];
                if ((record.Flags & RecordFlags.Played) != 0 || !_effectsPlay)
                {
                    continue;
                }

                UIElement element = record.Element;
                Transition effect = GetAppearEffect(element);
                bool appearsWithContainer = (record.Flags & RecordFlags.WithContainer) != 0;

                // OnPageOpen=Skip: it is simply there with its page.
                if (appearsWithContainer && effect.OnPageOpen == PageOpen.Skip)
                {
                    record.Flags |= RecordFlags.Played;
                    if (skipped++ == 0)
                    {
                        firstSkipped = element;
                    }
                    continue;
                }

                double wait = PlayAppearEffect(index, effect, 0, raise: false, settings, batch);

                if (logging)
                {
                    if (appearsWithContainer)
                    {
                        if (withContainer++ == 0)
                        {
                            firstWithContainer = element;
                            firstEffect = effect;
                            firstWait = wait;
                        }
                    }
                    else
                    {
                        Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(effect, settings);
                        _messages.Add($"{DescribeAppeared(element, (record.Flags & RecordFlags.Released) != 0, effect, settings)}; " +
                                      $"{DescribeOwnEffect(effect, true, _quickFades, timing, snapReason, wait)}.");
                    }
                }
            }

            if (withContainer == 1)
            {
                Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(firstEffect, settings);
                _messages.Add($"{ElementNames.Of(firstWithContainer)} appeared with its container; {DescribeOwnEffect(firstEffect, true, _quickFades, timing, snapReason, firstWait)}.");
            }
            else if (withContainer > 1)
            {
                _messages.Add($"{withContainer} elements appeared with their container, and play their Appear effects, like {ElementNames.Of(firstWithContainer)}.");
            }

            if (logging && skipped == 1)
            {
                _messages.Add($"{ElementNames.Of(firstSkipped)} appeared with its container, without its effect, as OnPageOpen=Skip asks.");
            }
            else if (logging && skipped > 1)
            {
                _messages.Add($"{skipped} elements appeared with their container, without their effects, as OnPageOpen=Skip asks, like {ElementNames.Of(firstSkipped)}.");
            }

            foreach (Disappearance disappearance in _disappearing)
            {
                if (disappearance.Handled)
                {
                    continue;
                }

                if (disappearance.Effect != null && _effectsPlay)
                {
                    double wait = PlayGhost(disappearance, settings, batch);
                    if (logging)
                    {
                        Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(disappearance.Effect, settings);
                        _messages.Add($"{ElementNames.Of(disappearance.Element)} disappeared; {DescribeOwnEffect(disappearance.Effect, false, _quickFades, timing, snapReason, wait)}.");
                    }
                }
                else
                {
                    batch.AddGhostRemoval(disappearance.Id);
                    disappearance.Handled = true;
                }
            }
        }

        private Timing GetEffectTiming(Transition effect, AnimationSettings settings)
        {
            if (!_effectTimings.TryGetValue(effect, out Timing timing))
            {
                timing = Timing.FromEffect(effect, settings);
                _effectTimings.Add(effect, timing);
            }

            return timing;
        }

        private static Transition GetAppearEffect(UIElement element)
            => Animate.GetAppear(element) is Transition effect && !effect.IsNone ? effect : null;

        private static Transition GetDisappearEffect(UIElement element)
            => Animate.GetDisappear(element) is Transition effect && !effect.IsNone ? effect : null;

        /// <summary>
        /// Says whether the element turned off its automatic effect with Appear=None or Disappear=None.
        /// </summary>
        private static bool HasNoEffect(UIElement element, bool appear)
            => (appear ? Animate.GetAppear(element) : Animate.GetDisappear(element)) is Transition effect && effect.IsNone;

        private static int GetFlags(UIElement element, ref Record record)
        {
            bool resized = !AreClose(record.OldRect.Width, record.NewRect.Width) || !AreClose(record.OldRect.Height, record.NewRect.Height);
            if (!resized)
            {
                return 0;
            }

            int flags = 0;
            if (VisualTreeHelper.GetClip(element) is RectangleGeometry clip && (clip.Transform is null || clip.Transform.Value.IsIdentity))
            {
                Rect r = clip.Rect;
                if (Math.Abs(r.X) < 0.5 && Math.Abs(r.Y) < 0.5 &&
                    Math.Abs(r.Width - record.NewRect.Width) < 0.5 && Math.Abs(r.Height - record.NewRect.Height) < 0.5)
                {
                    flags |= BatchBuilder.ClipOwnBox;
                }
            }

            if (element is Rectangle rectangle && rectangle.Stretch == Stretch.Fill)
            {
                flags |= BatchBuilder.Rectangle;
            }
            else if (element is Ellipse ellipse && ellipse.Stretch == Stretch.Fill)
            {
                flags |= BatchBuilder.Ellipse;
            }

            return flags;
        }

        private void Snap(int index, long now)
        {
            UIElement element = _records[index].Element;
            if (!_runningUntil.TryGetValue(element, out (long Until, bool FromSwitch) running))
            {
                return;
            }

            // The element follows the resize at once, and the animation of the switch goes on from there.
            if (running.Until > now && running.FromSwitch && _keepRunning)
            {
                _toRetarget.Add(index);
                return;
            }

            _runningUntil.Remove(element);
            if (running.Until > now)
            {
                _toCancel.Add(element);
            }
        }

        private void SweepRunning(long now)
        {
            _nextSweep = now + 2000;
            List<UIElement> finished = null;
            foreach (KeyValuePair<UIElement, (long Until, bool FromSwitch)> pair in _runningUntil)
            {
                if (pair.Value.Until <= now)
                {
                    (finished ??= new List<UIElement>()).Add(pair.Key);
                }
            }

            if (finished != null)
            {
                foreach (UIElement element in finished)
                {
                    _runningUntil.Remove(element);
                }
                finished.Clear();
            }

            foreach (KeyValuePair<UIElement, long> pair in _ghostedUntil)
            {
                if (pair.Value <= now)
                {
                    (finished ??= new List<UIElement>()).Add(pair.Key);
                }
            }

            if (finished != null)
            {
                foreach (UIElement element in finished)
                {
                    _ghostedUntil.Remove(element);
                }
            }
        }

        private void EnsureAbsolute(int index)
        {
            EnsureCapacity(ref _absOld, _recordCount);
            EnsureCapacity(ref _absNew, _recordCount);
            EnsureCapacity(ref _absStamp, _recordCount);
            ComputeAbsolute(index);
        }

        private void ComputeAbsolute(int index)
        {
            if (_absStamp[index] == _stamp)
            {
                return;
            }

            ref Record record = ref _records[index];
            Point parentOld, parentNew;
            if (record.Parent >= 0)
            {
                ComputeAbsolute(record.Parent);
                parentOld = _absOld[record.Parent];
                parentNew = _absNew[record.Parent];
            }
            else
            {
                parentOld = parentNew = GetAbsolutePositionOfParent(record.Element);
            }

            _absOld[index] = new Point(parentOld.X + record.OldRect.X, parentOld.Y + record.OldRect.Y);
            _absNew[index] = new Point(parentNew.X + record.NewRect.X, parentNew.Y + record.NewRect.Y);
            _absStamp[index] = _stamp;
        }

        private static Point GetAbsolutePositionOfParent(UIElement element)
        {
            if (VisualTreeHelper.GetParent(element) is not UIElement parent)
            {
                return new Point();
            }

            try
            {
                return parent.TransformToVisual(null).Transform(new Point());
            }
            catch
            {
                return new Point();
            }
        }

        /// <summary>
        /// Says whether the browser window changed size since the previous layout pass. Such a pass
        /// has no origins: everything in it follows the window, and only the elements that have
        /// AlsoOnWindowResize animate. Elements that measure again in it, like scroll bar parts,
        /// would otherwise look like changes and take the whole page with them.
        /// </summary>
        private bool IsWindowResizePass()
        {
            if (!_windowChecked)
            {
                _windowChecked = true;
                Size size = GetWindowSize();
                _windowResized = _windowSize.Width > 0 && size.Width > 0 && !AreClose(size, _windowSize);
                if (size.Width > 0)
                {
                    _windowSize = size;
                }
            }

            return _windowResized;
        }

        private static Size GetWindowSize()
        {
            try
            {
                if (Application.Current?.Host?.Content is System.Windows.Interop.Content content)
                {
                    return new Size(content.ActualWidth, content.ActualHeight);
                }
            }
            catch
            {
                // No window size: no pass counts as a window resize.
            }

            return default;
        }

        private static Rect GetViewport()
        {
            try
            {
                if (Application.Current?.Host?.Content is System.Windows.Interop.Content content &&
                    content.ActualWidth > 0 && content.ActualHeight > 0)
                {
                    return new Rect(-CullMargin, -CullMargin, content.ActualWidth + 2 * CullMargin, content.ActualHeight + 2 * CullMargin);
                }
            }
            catch
            {
                // No window size: nothing is culled.
            }

            return Rect.Empty;
        }

        private static void EnsureCapacity<T>(ref T[] array, int count)
        {
            if (array.Length < count)
            {
                Array.Resize(ref array, Math.Max(count, array.Length * 2));
            }
        }

        #endregion

        #region Logging

        private void Publish(AnimationSettings settings)
        {
            string[] messages = _messages.ToArray();
            _messages.Clear();

            if (settings.LogForDebugging)
            {
                foreach (string message in messages)
                {
                    BrowserBridge.WriteToConsole(message);
                }
            }

            if (AnimationSettings.HasLoggedHandlers)
            {
                // Raised after the layout pass, so that handlers can change the page.
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    foreach (string message in messages)
                    {
                        AnimationSettings.RaiseLogged(message);
                    }
                });
            }
        }

        #endregion

        #region Helpers

        private Bucket GetBucket(int group)
        {
            if (!_buckets.TryGetValue(group, out Bucket bucket))
            {
                bucket = _bucketPool.Count > 0 ? _bucketPool.Pop() : new Bucket();
                _buckets.Add(group, bucket);
            }

            return bucket;
        }

        private static bool IsChanged(ref Record record)
            => (record.Flags & (RecordFlags.HasRects | RecordFlags.NotRendered)) == RecordFlags.HasRects && !AreClose(record.OldRect, record.NewRect);

        private static bool AreClose(double a, double b) => Math.Abs(a - b) < Tolerance || (double.IsNaN(a) && double.IsNaN(b));

        private static bool AreClose(Size a, Size b) => AreClose(a.Width, b.Width) && AreClose(a.Height, b.Height);

        private static bool AreClose(Rect a, Rect b)
        {
            if (a.IsEmpty || b.IsEmpty)
            {
                return a.IsEmpty == b.IsEmpty;
            }

            return AreClose(a.X, b.X) && AreClose(a.Y, b.Y) && AreClose(a.Width, b.Width) && AreClose(a.Height, b.Height);
        }

        private void ClearMeasureState()
        {
            if (_measured.Count > 0) _measured.Clear();
            if (_carriers.Count > 0) _carriers.Clear();
            if (_measureOrigins.Count > 0) _measureOrigins.Clear();
            if (_shown.Count > 0) _shown.Clear();
            if (_released.Count > 0) _released.Clear();
            if (_scrollChecked.Count > 0) _scrollChecked.Clear();
            if (_switches.Count > 0) _switches.Clear();
            _switchOnly = false;
            _windowChecked = false;
        }

        private void Reset()
        {
            ResetRecords();
            ClearMeasureState();
            _messages.Clear();
            _disappearing.Clear();
        }

        /// <summary>
        /// Forgets what was arranged, keeping what was measured and shown in the pass.
        /// </summary>
        private void ResetRecords()
        {
            Array.Clear(_records, 0, _recordCount);
            _recordCount = 0;
            _recordIndex.Clear();
            _stack.Clear();
            _sessionOrder.Clear();
            _areaCache.Clear();
            _toCancel.Clear();
            _toRetarget.Clear();
            _keepRunning = false;
            _effectAppears.Clear();
            _effectTimings.Clear();

            _groupParents.Clear();
            _groupParents.Add(0);
            _groups.Clear();
            _groups.Add(default);

            foreach (Bucket bucket in _buckets.Values)
            {
                bucket.Clear();
                _bucketPool.Push(bucket);
            }
            _buckets.Clear();
        }

        #endregion

        #region Types

        [Flags]
        private enum RecordFlags : byte
        {
            None = 0,
            HasRects = 1,
            Appears = 2,
            Origin = 4,
            Scrolled = 8,
            NotRendered = 16,
            // Appears inside an element that appears too, with an Appear of its own.
            WithContainer = 32,
            // Its Appear was played in this pass.
            Played = 64,
            // Shown again after its page opened (OnPageOpen=PushOthers), or the parent it appears in.
            Released = 128,
        }

        private struct Record
        {
            public UIElement Element;
            public int Parent;
            public Rect OldRect;
            public Rect NewRect;
            public RecordFlags Flags;
            public int Group;
        }

        private enum GroupKind : byte
        {
            None,
            Change,
            WindowResize,
        }

        private struct GroupInfo
        {
            public GroupKind Kind;
            public Movement Movement;
            public UIElement Element;
        }

        private struct Area
        {
            public Movement Movement;
            public UIElement Element;
            public bool IsCarvedOut;
        }

        /// <summary>
        /// A setting found going up from a {Responsive} switch during a resize, and the part of
        /// the group it animates.
        /// </summary>
        private sealed class Owner
        {
            public UIElement Element;
            public Movement Movement;
            public readonly Bucket Part = new Bucket();
        }

        private sealed class ScrollOffset
        {
            public bool IsKnown;
            public double Horizontal;
            public double Vertical;
        }

        private sealed class Bucket
        {
            public readonly List<int> Origins = new List<int>();
            public readonly List<int> Members = new List<int>();
            public readonly List<int> Animated = new List<int>();
            public readonly List<int> Appearing = new List<int>();
            public readonly List<int> Revealed = new List<int>();
            public readonly List<Disappearance> Ghosts = new List<Disappearance>();

            // The first effect of an element that appears or disappears in the group, which gives
            // the group its timing.
            public Transition Effect;
            public UIElement EffectElement;
            public bool EffectAppears;

            // What Emit played, for the message.
            public bool EffectPlayed;
            public double EffectWait;
            public int OtherEffects;
            public string OtherEffect;
            public int Wipes;
            public int Fades;
            public int GhostWipes;
            public int GhostFades;
            public UIElement RevealedElement;
            public UIElement VanishedElement;

            public void Clear()
            {
                Origins.Clear();
                Members.Clear();
                Animated.Clear();
                Appearing.Clear();
                Revealed.Clear();
                Ghosts.Clear();
                Effect = null;
                EffectElement = null;
                EffectAppears = false;
                EffectPlayed = false;
                EffectWait = 0;
                OtherEffects = Wipes = Fades = GhostWipes = GhostFades = 0;
                OtherEffect = null;
                RevealedElement = VanishedElement = null;
            }
        }

        /// <summary>
        /// An element collapsed or removed since the last pass, copied in the browser with the id
        /// it had.
        /// </summary>
        private sealed class Disappearance
        {
            public UIElement Element;
            public UIElement Parent;
            public string Id;
            public Transition Effect;
            public int ParentIndex;
            public bool Handled;
        }

        #endregion
    }
}
