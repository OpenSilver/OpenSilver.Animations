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

        // How far up from an element that is removed a container of a list is searched for.
        private const int MaxContainerDepth = 6;

        // ItemCascade: the time between two items, and the whole cascade, at most.
        private const double MaxCascadeMs = 300;
        private const double MaxCascadeTotalMs = 600;
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

        /// <summary>
        /// Set once a Panel or an ItemsControl has an item setting. Until then, elements are never
        /// checked for the settings of the list they are in.
        /// </summary>
        internal static bool AnyItems;

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
        // The records that move in the group being emitted, for KeepInPlace.
        private readonly HashSet<int> _animatedNow = new HashSet<int>();
        private readonly Dictionary<Transition, Timing> _effectTimings = new Dictionary<Transition, Timing>();
        // The items that follow the window each make a group, with the same setting: one timing for all.
        private readonly Dictionary<Movement, Timing> _windowTimings = new Dictionary<Movement, Timing>();
        // The elements that may still have a ghost in the browser, until when.
        private readonly Dictionary<UIElement, long> _ghostedUntil = new Dictionary<UIElement, long>();
        private bool _effectsPlay;
        private bool _quickFades;

        // OnPageOpen=PushOthers: the elements held out of the layout while their page opens, and
        // those shown again since the last pass, which appear in the next one.
        private readonly PageOpenHolds _pageOpenHolds;
        private readonly HashSet<UIElement> _released = new HashSet<UIElement>();

        // The lists with item settings, or inside an area: where each of their items was at the end
        // of the last pass, the lists found since then (null for the panels that are not lists),
        // the containers removed from them since the last pass, and the ghosts of those containers.
        private readonly ConditionalWeakTable<Panel, ItemList> _lists = new ConditionalWeakTable<Panel, ItemList>();
        private readonly Dictionary<Panel, ItemList> _listCache = new Dictionary<Panel, ItemList>();
        private readonly List<RemovedItem> _removedItems = new List<RemovedItem>();
        private readonly Dictionary<UIElement, Disappearance> _ghostOf = new Dictionary<UIElement, Disappearance>();
        // The containers that show an item again in this pass, and the ghost of its old container.
        private readonly Dictionary<UIElement, string> _pairedGhosts = new Dictionary<UIElement, string>();
        // The containers copied when their list was refreshed, before their panel removes them, with
        // the id of their copy.
        private readonly Dictionary<UIElement, string> _copiedBeforeRemoval = new Dictionary<UIElement, string>();

        // The items of each list that play their effects together in this pass, for ItemCascade.
        private readonly Dictionary<ItemList, Cascade> _appearCascades = new Dictionary<ItemList, Cascade>();
        private readonly Dictionary<ItemList, Cascade> _disappearCascades = new Dictionary<ItemList, Cascade>();

        // While something is dragged: the element that holds the mouse capture and its containers.
        private readonly HashSet<DependencyObject> _capturedChain = new HashSet<DependencyObject>();

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

        /// <summary>
        /// Stops holding an element whose Appear no longer has OnPageOpen=PushOthers. It gets back
        /// the Visibility the app gave it.
        /// </summary>
        public static void CancelHold(UIElement element)
        {
            if (AnyPushOthers && _instance is LayoutAnimator animator)
            {
                animator._pageOpenHolds.Cancel(element);
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
                    if (child is FrameworkElement fe && Animate.GetAppear(child) is Transition effect && effect.OnPageOpen == PageOpen.PushOthers)
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
        /// rather than on its own: whether an element arranged in this pass around it appeared.
        /// The nearest ones can be arranged again in the same pass without appearing, like the
        /// panel of a menu that the app shows while its page opens.
        /// </summary>
        private bool AppearsWithContainer(UIElement element)
        {
            bool arranged = false;
            for (DependencyObject e = VisualTreeHelper.GetParent(element); e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is UIElement ancestor && _recordIndex.TryGetValue(ancestor, out int index))
                {
                    if ((_records[index].Flags & RecordFlags.Appears) != 0)
                    {
                        return true;
                    }

                    arranged = true;
                }
            }

            return !arranged;
        }

        /// <summary>
        /// Warns about the parameters of an effect that don't apply where it is used. They are ignored.
        /// </summary>
        public static void CheckEffect(Transition effect, bool appear, UIElement element, bool items)
        {
            if (_instance == null || effect.IsNone)
            {
                return;
            }

            string problem = null;
            if (items && appear && effect.OnPageOpen == PageOpen.PushOthers)
            {
                problem = "OnPageOpen=PushOthers is not available for items, so they play their effect in their place (InPlace)";
            }
            else if (appear && effect.OthersMove == OthersMove.After)
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
            else if (appear ? effect.HasTo && !effect.HasFrom : effect.HasFrom && !effect.HasTo)
            {
                problem = effect.MisplacedFromOrTo(appear);
            }
            else if (effect.Bounciness != null && !effect.UsesBounciness)
            {
                problem = "Bounciness is only used by Pop, Easing=Bouncy and Easing=Overshoot";
            }

            if (problem != null)
            {
                Warn((items ? "Item" : string.Empty) + (appear ? "Appear" : "Disappear"), element, problem);
            }
        }

        /// <summary>
        /// Warns about an item setting that doesn't apply where it is used.
        /// </summary>
        public static void CheckItemSetting(DependencyProperty property, object value, UIElement element)
        {
            if (_instance == null)
            {
                return;
            }

            string name = property == Animate.ItemAppearProperty ? "ItemAppear"
                : property == Animate.ItemDisappearProperty ? "ItemDisappear"
                : property == Animate.ItemMoveAndResizeProperty ? "ItemMoveAndResize"
                : "ItemCascade";

            if (element is not Panel && element is not ItemsControl)
            {
                Warn(name, element, "it is for a Panel or an ItemsControl, so it is ignored");
                return;
            }

            if (value is Transition effect)
            {
                CheckEffect(effect, property == Animate.ItemAppearProperty, element, items: true);
            }
            else if (value is Movement movement && movement.ApplyTo == AnimationScope.EverythingInside)
            {
                Warn(name, element, "ApplyTo is not used for items, so each item animates its own moves and size changes. " +
                                    "To animate everything inside the list, set MoveAndResize on the list instead");
            }
            else if (value is AnimationTime cascade && cascade.Seconds > MaxCascadeMs / 1000)
            {
                Warn(name, element, $"it is at most {Seconds(MaxCascadeMs)}, so {Seconds(MaxCascadeMs)} is used");
            }
        }

        internal static void Warn(string property, UIElement element, string problem)
        {
            // Later, so that the element has its name, and the app had time to turn on the log.
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                string message = $"The {property} of {ElementNames.Of(element)}: {problem}.";
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

        #region Layout hooks

        protected internal override void OnMeasured(UIElement element, bool sameAvailableSize, Size previousDesiredSize, bool isFirstMeasure)
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

        protected internal override void OnArranging(UIElement element, Rect finalRect, Rect previousFinalRect, bool isFirstArrange)
        {
            int parent = _stack.Count > 0 ? _stack[_stack.Count - 1] : -1;

            // Everything inside an element that appears appears with it. Only the elements that have
            // an Appear of their own are recorded, to play it, unless it appears by scrolling.
            // An item that glides to its new place keeps its content as it was, without effects.
            if (parent >= 0 && (_records[parent].Flags & (RecordFlags.Appears | RecordFlags.Paired)) != 0)
            {
                if (AnyEffects && (_records[parent].Flags & (RecordFlags.Scrolled | RecordFlags.Paired)) == 0 && GetAppearEffect(element) != null)
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

            // A new container for an item that the list showed in the last pass, like after a sort,
            // or the same container moved: it glides from where the item was.
            if (isFirstArrange && (AnyItems || AnyAreas) && parent >= 0 && (_records[parent].Flags & RecordFlags.Scrolled) == 0 &&
                TryPair(element, out Rect itemRect))
            {
                record.OldRect = itemRect;
                record.Flags |= RecordFlags.HasRects | RecordFlags.Paired;
                _stack.Add(index);
                _sessionOrder.Add(index);
                return;
            }

            // An element shown again still has the box it had before it was collapsed: it appears,
            // like an element arranged for the first time, and the change starts in its parent.
            bool shown = !isFirstArrange && _shown.Count > 0 && _shown.Contains(element);
            if (isFirstArrange || shown)
            {
                record.Flags |= RecordFlags.Appears;
                _appeared[element] = _clock.ElapsedMilliseconds;

                // Where it starts, so that its first scroll is a scroll.
                if (element is IScrollInfo appearingScrollInfo)
                {
                    IsScrolled(element, appearingScrollInfo);
                }

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

        protected internal override void OnArranged(UIElement element, Rect previousVisualRect, Rect visualRect, bool rendered)
        {
            // Where each item is, including those that appear with their container, which are not recorded.
            if ((AnyItems || AnyAreas) && VisualTreeHelper.GetParent(element) is Panel panel && GetList(panel) is ItemList list)
            {
                object item = list.ItemFromContainer(element);
                if (list.Owner != null && item != element && element is FrameworkElement container && !list.ItemOf.ContainsKey(container))
                {
                    container.DataContextChanged -= OnContainerDataContextChanged;
                    container.DataContextChanged += OnContainerDataContextChanged;
                }
                list.ItemOf[element] = item;
                list.Slots[item] = new Slot { Container = element, Rect = visualRect };
            }

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

        protected internal override void OnVisibilityChanged(UIElement element, Visibility oldValue, Visibility newValue)
        {
            if (_pageOpenHolds.IsHolding(element))
            {
                // The app showed it while it waits for its page to open: it stays out of the
                // layout until its release.
                if (newValue != Visibility.Collapsed)
                {
                    _pageOpenHolds.Reapply(element);
                }
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
                    PlayAtOnce(element, effect, appear: true, hiddenId, VisualTreeHelper.GetParent(element) as UIElement);
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

        protected internal override void OnDetaching(UIElement child, UIElement parent)
        {
            bool moving = false;
            if (_runningUntil.Count > 0 && _runningUntil.TryGetValue(child, out (long Until, bool FromSwitch) running))
            {
                moving = running.Until > _clock.ElapsedMilliseconds;
                _runningUntil.Remove(child);
            }

            // Its item may be shown again in the next pass, by a new container or by this one. While
            // it moves, its ghost keeps where it is on screen, for its item to go on from there.
            ItemList list = null;
            object shown = null;
            ItemList parentList = (AnyItems || AnyAreas) && parent is Panel panel ? GetList(panel) : null;
            if (parentList != null && parentList.ItemOf.TryGetValue(child, out shown))
            {
                list = parentList;
                _removedItems.Add(new RemovedItem { List = list, Container = child, Item = shown });
            }
            else if (parentList == null && (AnyItems || AnyAreas) && FindContainer(parent) is UIElement container)
            {
                // A ListBox clears the content of its containers when it is refreshed, before
                // their DataContext.
                CopyBeforeRemoval(container);
            }

            string copy = null;
            if (_copiedBeforeRemoval.Count > 0 && _copiedBeforeRemoval.TryGetValue(child, out copy))
            {
                _copiedBeforeRemoval.Remove(child);
            }

            bool captured = false;
            if ((AnyEffects || AnyAreas || copy != null || (list != null && moving)) && child.Visibility == Visibility.Visible)
            {
                captured = CaptureDisappearing(child, parent, hidden: false, keepOffset: list != null && (moving || copy != null), copied: copy != null);
                if (list?.Owner != null && _ghostOf.TryGetValue(child, out Disappearance disappearance))
                {
                    disappearance.Owner = list.Owner;
                    disappearance.Item = shown;
                }
            }

            if (copy != null && !captured)
            {
                RemoveGhosts(new[] { copy });
            }
        }

        /// <summary>
        /// Copies an element that is being collapsed, hidden or removed, if it has an effect to play:
        /// its own Disappear, or an automatic one inside an area. A hidden element keeps its space,
        /// so its effect plays at once; the others play theirs at the end of the next layout pass.
        /// With <paramref name="keepOffset"/>, an item that moves is copied anyway, so that its
        /// item goes on from where it is if it is shown again in the next pass. With
        /// <paramref name="copied"/>, it was copied already (<see cref="CopyBeforeRemoval"/>).
        /// Says whether the copy is kept, to play or for where it is on screen.
        /// </summary>
        private bool CaptureDisappearing(UIElement element, UIElement parent, bool hidden, bool keepOffset = false, bool copied = false)
        {
            if (parent == null || !parent.IsVisible || _skip || _held.Contains(element) || !AnimationSettings.Current.IsEnabled)
            {
                return false;
            }

            // Removed by a scrolling list, like a virtualizing panel: no effects.
            if (parent is IScrollInfo scrollInfo && WasScrolled(parent, scrollInfo))
            {
                return false;
            }

            ItemList effectList = null;
            Transition effect = AnyEffects ? GetDisappearEffect(element, parent, out effectList) : null;
            bool offsetOnly = false;
            if (effect == null &&
                (hidden || !AnyAreas || HasNoEffect(element, appear: false, parent) || CountAutomaticGhosts() >= MaxAutomaticGhosts || !IsInsideArea(element)))
            {
                if (!keepOffset)
                {
                    return false;
                }
                offsetOnly = true;
            }

            Size size = element.RenderSize;
            if (size.Width <= 0 || size.Height <= 0 || !IsAnimatable(element, out string id))
            {
                return false;
            }

            if (!copied)
            {
                BrowserBridge.Ghost(id);
            }
            _ghostedUntil[element] = _clock.ElapsedMilliseconds + GhostLifetimeMs;

            if (hidden)
            {
                PlayAtOnce(element, effect, appear: false, id, parent);
                return true;
            }

            var disappearance = new Disappearance { Element = element, Parent = parent, Id = id, Effect = effect, List = effectList, NoEffect = offsetOnly, ParentIndex = -1 };
            _disappearing.Add(disappearance);
            if (AnyItems || AnyAreas)
            {
                _ghostOf[element] = disappearance;
            }
            return true;
        }

        private int CountAutomaticGhosts()
        {
            int count = 0;
            foreach (Disappearance disappearance in _disappearing)
            {
                if (disappearance.Effect == null && !disappearance.NoEffect)
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
        private void PlayAtOnce(UIElement element, Transition effect, bool appear, string id, UIElement parent)
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
                _messages.Add($"{NameOf(element, parent)} {what}; " +
                              $"{DescribeOwnEffect(effect, appear, quick, timing, reason, wait, DescribeEffectSource(element, effect, appear, parent, own: true))}.");
                Publish(settings);
            }
        }

        /// <summary>
        /// "it fades in (Smooth, 0.25 s, from its Appear)", "it fades in after 0.5 s (...)", or "it
        /// fades in quickly, because the device asks for reduced motion".
        /// </summary>
        private static string DescribeOwnEffect(Transition effect, bool appear, bool quick, Timing timing, string reason, double waitMs, string source)
            => quick
                ? $"it {(appear ? "fades in" : "fades out")} quickly{After(waitMs)}, because {reason}"
                : $"it {effect.Describe(appear)}{After(waitMs)} ({timing.Description}, from {source})";

        /// <summary>
        /// Where an effect comes from: "its Appear" (<paramref name="own"/>) or "the Appear of
        /// Banner", or, for an item, "the ItemAppear of TodoList".
        /// </summary>
        private string DescribeEffectSource(UIElement element, Transition effect, bool appear, UIElement parent, bool own)
        {
            Transition elementEffect = appear ? Animate.GetAppear(element) : Animate.GetDisappear(element);
            if (!ReferenceEquals(elementEffect, effect) && parent is Panel panel && GetList(panel) is ItemList list)
            {
                DependencyProperty property = appear ? Animate.ItemAppearProperty : Animate.ItemDisappearProperty;
                return $"the {(appear ? "ItemAppear" : "ItemDisappear")} of {ElementNames.Of(list.HolderOf(property))}";
            }

            string name = appear ? "Appear" : "Disappear";
            return own ? "its " + name : $"the {name} of {ElementNames.Of(element, parent)}";
        }

        /// <summary>
        /// Names an element in a message. An item that was removed is also named by its data item,
        /// as its container may show no text anymore.
        /// </summary>
        private string NameOf(UIElement element, UIElement parent)
        {
            object item = null;
            if ((AnyItems || AnyAreas) && parent is Panel panel && GetList(panel) is ItemList list)
            {
                list.ItemOf.TryGetValue(element, out item);
            }

            return ElementNames.Of(element, parent, item);
        }

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

        protected internal override void OnResponsiveValueChanged(FrameworkElement element, DependencyObject target, DependencyProperty property)
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

        protected internal override void OnLayoutPassCompleted()
        {
            if (_appeared.Count > 0)
            {
                PruneAppeared();
            }

            if (_pageOpenHolds.IsWaitingForPass)
            {
                _pageOpenHolds.OnLayoutPassCompleted(AppearsWithContainer);
            }

            // The containers copied before their removal, but not removed.
            if (_copiedBeforeRemoval.Count > 0)
            {
                RemoveGhosts(_copiedBeforeRemoval.Values);
                _copiedBeforeRemoval.Clear();
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
            scrolled = !AreClose(offset.Horizontal, h) || !AreClose(offset.Vertical, v);
            offset.Horizontal = h;
            offset.Vertical = v;
            _scrollChecked[element] = scrolled;
            return scrolled;
        }

        /// <summary>
        /// Like <see cref="IsScrolled"/>, without remembering the offset: for the containers that a
        /// virtualizing panel removes while it measures, and for elements removed between passes.
        /// </summary>
        private bool WasScrolled(UIElement element, IScrollInfo scrollInfo)
        {
            if (_scrollChecked.TryGetValue(element, out bool scrolled))
            {
                return scrolled;
            }

            ScrollOffset offset = _scrollOffsets.GetOrCreateValue(element);
            return !AreClose(offset.Horizontal, scrollInfo.HorizontalOffset) || !AreClose(offset.Vertical, scrollInfo.VerticalOffset);
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
                    if ((record.Flags & RecordFlags.Paired) != 0 && bucket.Paired++ == 0)
                    {
                        bucket.PairedList = VisualTreeHelper.GetParent(record.Element) as Panel;
                    }
                }
            }

            // An element that disappears is a change of its parent, like one that appears. The
            // ghost of an item shown again in its new place has nothing to play.
            foreach (Disappearance disappearance in _disappearing)
            {
                if (disappearance.Paired || disappearance.NoEffect)
                {
                    continue;
                }

                // A virtualizing panel removes its containers while it measures, before it tells
                // its new offset, and the containers it removes still have their items in the list.
                if ((disappearance.Parent is IScrollInfo scrollInfo && IsScrolled(disappearance.Parent, scrollInfo)) ||
                    (disappearance.Owner != null && disappearance.Owner.Items.Contains(disappearance.Item)))
                {
                    disappearance.Effect = null;
                    disappearance.NoEffect = true;
                    continue;
                }

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
                            bucket.EffectParent = disappearance.Parent;
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
                bucket.EffectParent = VisualTreeHelper.GetParent(element) as UIElement;
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

            // Dragging to reorder a list changes its order: its items glide out of the way, while
            // the dragged item, which holds the mouse capture, follows the pointer.
            else if (ReferenceEquals(reason, DraggingReason) && bucket.Paired > 0)
            {
                reason = null;
            }

            if (reason == null)
            {
                if (windowResize)
                {
                    if (!_windowTimings.TryGetValue(info.Movement, out timing))
                    {
                        timing = Timing.FromMovement(info.Movement, settings);
                        _windowTimings.Add(info.Movement, timing);
                    }
                    source = IsItemOfList(info.Element, out _)
                        ? DescribeSource(info.Movement, info.Element, null)
                        : "the AlsoOnWindowResize of " + ElementNames.Of(info.Element);
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
                            string effectSource = DescribeEffectSource(bucket.EffectElement, played, bucket.EffectAppears, bucket.EffectParent, own: true);
                            what = DescribeOwnEffect(played, bucket.EffectAppears, _quickFades, effectTiming, reason, playedWait, effectSource) +
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
                        string followers = IsItemOfList(info.Element, out ItemList list)
                            ? $"the items of {ElementNames.Of(list.Element)} follow it"
                            : $"{ElementNames.Of(info.Element)} follows it";
                        _messages.Add($"The window was resized; {followers} ({timing.Description}, from {source}).");
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
                timing = Timing.ForOthers(GetEffectTiming(bucket.Effect, settings), bucket.Effect, settings);
                source = DescribeEffectSource(bucket.EffectElement, bucket.Effect, bucket.EffectAppears, bucket.EffectParent, own: false);
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
                source = DescribeSource(memberMovement, memberOwner, memberOwner);
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

        /// <summary>
        /// "the MoveAndResize of Card", "the EverythingInside of Form", or, for an item, "the
        /// ItemMoveAndResize of TodoList".
        /// </summary>
        private string DescribeSource(Movement movement, UIElement owner, UIElement origin)
        {
            if (!ReferenceEquals(Animate.GetMoveAndResize(owner), movement) && GetListOf(owner) is ItemList list && ReferenceEquals(list.Movement, movement))
            {
                return "the ItemMoveAndResize of " + ElementNames.Of(list.HolderOf(Animate.ItemMoveAndResizeProperty));
            }

            return (movement.ApplyTo == AnimationScope.EverythingInside && owner != origin
                ? "the EverythingInside of "
                : "the MoveAndResize of ") + ElementNames.Of(owner);
        }

        /// <summary>
        /// The setting that gives its timing to a change that starts at the origin: its own, even
        /// when its box stays the same, or else the nearest ancestor that has EverythingInside, or
        /// another setting and moves or changes size in this change. An ancestor with another
        /// setting that stays in place doesn't count: its setting is about its own box. Returns
        /// how many levels up the setting is, looking at most <paramref name="maxLevels"/> up.
        /// </summary>
        private int FindNearestMovement(UIElement origin, int group, int maxLevels, out Movement movement, out UIElement owner)
        {
            if (GetOwnMovement(origin, out _) is Movement own && !own.IsNone)
            {
                movement = own;
                owner = origin;
                return 0;
            }

            int levels = 1;
            for (UIElement e = VisualTreeHelper.GetParent(origin) as UIElement; e != null && levels <= maxLevels; e = VisualTreeHelper.GetParent(e) as UIElement, levels++)
            {
                if (GetOwnMovement(e, out bool fromItems) is not Movement m)
                {
                    continue;
                }

                if (m.IsNone)
                {
                    break;
                }

                if ((m.ApplyTo == AnimationScope.EverythingInside && !fromItems) || IsMember(e, group))
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
                    : NameOf(origin, bucket.EffectParent) + " disappeared";
            }

            // Items shown again in new places: the list was sorted, filtered or refreshed, or an
            // item was moved.
            if (bucket.Paired > 0 && bucket.PairedList != null && GetList(bucket.PairedList) is ItemList pairedList)
            {
                return $"The items of {ElementNames.Of(pairedList.Element)} " +
                       (bucket.Ghosts.Count > 0 || bucket.Appearing.Count > 0 ? "changed" : "changed order");
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
        /// elements animate together", "3 elements animate together, and Notes wipes out", "it
        /// slides in, 2 other items follow, one every 0.05 s, and 4 elements animate together".
        /// </summary>
        private static string DescribeAnimated(Bucket bucket)
        {
            string moved = bucket.Animated.Count == 0 ? null : ElementNames.Count(bucket.Animated.Count, "element animates", "elements animate together");
            string others = bucket.OtherEffects == 0 ? null
                : bucket.CascadeStepMs > 0 ? ElementNames.Count(bucket.OtherEffects, "other item follows", "other items follow") + ", one every " + Seconds(bucket.CascadeStepMs)
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
            if (GetOwnMovement(element, out _) is Movement own)
            {
                return own.IsNone ? null : own;
            }

            return GetArea(element).Movement;
        }

        /// <summary>
        /// The MoveAndResize of the element, or else the ItemMoveAndResize of the list it is an
        /// item of (<paramref name="fromItems"/>), whose ApplyTo is not used.
        /// </summary>
        private Movement GetOwnMovement(UIElement element, out bool fromItems)
        {
            fromItems = false;
            if (Animate.GetMoveAndResize(element) is Movement own)
            {
                return own;
            }

            if (AnyItems && GetListOf(element) is ItemList list && list.Movement is Movement item)
            {
                fromItems = true;
                return item;
            }

            return null;
        }

        /// <summary>
        /// Says whether the setting of the element comes from the ItemMoveAndResize of its list.
        /// </summary>
        private bool IsItemOfList(UIElement element, out ItemList list)
        {
            list = GetOwnMovement(element, out bool fromItems) != null && fromItems ? GetListOf(element) : null;
            return list != null;
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

            if (GetOwnMovement(element, out bool fromItems) is Movement own)
            {
                if (own.IsNone)
                {
                    area = new Area { IsCarvedOut = true, Element = element };
                }
                else if (own.ApplyTo == AnimationScope.EverythingInside && !fromItems)
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

        private bool HasNone(UIElement element)
            => GetOwnMovement(element, out _) is Movement own && own.IsNone;

        /// <summary>
        /// Says whether an element would be revealed when it appears. Called outside the layout
        /// passes, so nothing is memoized.
        /// </summary>
        private bool IsInsideArea(UIElement element)
        {
            if (HasNone(element))
            {
                return false;
            }

            for (UIElement e = VisualTreeHelper.GetParent(element) as UIElement; e != null; e = VisualTreeHelper.GetParent(e) as UIElement)
            {
                if (GetOwnMovement(e, out bool fromItems) is Movement movement)
                {
                    if (movement.IsNone)
                    {
                        return false;
                    }

                    if (movement.ApplyTo == AnimationScope.EverythingInside && !fromItems)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The list whose items include the element, or null: its parent is a Panel with item
        /// settings, the items panel of an ItemsControl that has them, or an items panel inside
        /// an area.
        /// </summary>
        private ItemList GetListOf(UIElement element)
            => VisualTreeHelper.GetParent(element) is Panel panel ? GetList(panel) : null;

        private ItemList GetList(Panel panel)
        {
            if (_listCache.TryGetValue(panel, out ItemList list))
            {
                return list;
            }

            ItemsControl owner = panel.IsItemsHost ? ItemsControl.GetItemsOwner(panel) : null;
            bool settings = AnyItems && (ItemList.HasSettings(panel) || (owner != null && ItemList.HasSettings(owner)));
            bool inArea = !settings && AnyAreas && owner != null &&
                          ((Animate.GetMoveAndResize(panel) is Movement own && !own.IsNone && own.ApplyTo == AnimationScope.EverythingInside) || IsInsideArea(panel));

            list = null;
            if (settings || inArea)
            {
                list = _lists.GetValue(panel, p => new ItemList(p));
                list.Owner = owner;
            }

            _listCache[panel] = list;
            return list;
        }

        private static void OnContainerDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
            => _instance?.CopyBeforeRemoval((UIElement)sender);

        /// <summary>
        /// The container of a list that the element is in, a few levels up at most, or null.
        /// </summary>
        private UIElement FindContainer(UIElement element)
        {
            for (int depth = 0; element != null && depth < MaxContainerDepth; depth++)
            {
                UIElement parent = VisualTreeHelper.GetParent(element) as UIElement;
                if (parent is Panel panel && GetList(panel) is ItemList list && list.ItemOf.ContainsKey(element))
                {
                    return element;
                }
                element = parent;
            }

            return null;
        }

        /// <summary>
        /// Copies the containers of a list that is refreshed, like when its ItemsSource is
        /// replaced. Its containers are emptied first, then removed: copied when the first one is
        /// emptied, they are still shown until the next pass, which pairs their items with the
        /// new containers. Only a refresh empties a container still in its panel: the others
        /// remove it first.
        /// </summary>
        private void CopyBeforeRemoval(UIElement container)
        {
            if (_skip || !AnimationSettings.Current.IsEnabled || _copiedBeforeRemoval.ContainsKey(container) ||
                GetListOf(container) is not ItemList list || list.Owner == null || !list.Panel.IsVisible ||
                !list.ItemOf.ContainsKey(container) ||
                list.Owner.ItemContainerGenerator.ItemFromContainer(container) != DependencyProperty.UnsetValue)
            {
                return;
            }

            var ids = new List<string>();
            foreach (UIElement child in list.Panel.Children)
            {
                if (child.Visibility == Visibility.Visible && list.ItemOf.ContainsKey(child) && !_copiedBeforeRemoval.ContainsKey(child) &&
                    child.RenderSize.Width > 0 && child.RenderSize.Height > 0 && IsAnimatable(child, out string id))
                {
                    _copiedBeforeRemoval[child] = id;
                    ids.Add(id);
                }
            }

            if (ids.Count > 0)
            {
                BrowserBridge.GhostInView(ids);
            }
        }

        private void RemoveGhosts(IEnumerable<string> ids)
        {
            var batch = new BatchBuilder();
            foreach (string id in ids)
            {
                batch.AddGhostRemoval(id);
            }
            BrowserBridge.Run(batch.ToJson());
        }

        /// <summary>
        /// Pairs a container arranged for the first time with the item it shows, if the list
        /// showed that item at the end of the last pass in a container that is gone since then:
        /// the item glides from there. Its old container doesn't play its disappear effect.
        /// </summary>
        private bool TryPair(UIElement element, out Rect itemRect)
        {
            itemRect = default;
            if (VisualTreeHelper.GetParent(element) is not Panel panel || GetList(panel) is not ItemList list ||
                !list.Slots.TryGetValue(list.ItemFromContainer(element), out Slot slot))
            {
                return false;
            }

            UIElement old = slot.Container;
            if (old != element && VisualTreeHelper.GetParent(old) == panel)
            {
                return false;
            }

            itemRect = slot.Rect;
            if (_ghostOf.TryGetValue(old, out Disappearance ghost))
            {
                ghost.Paired = true;
                _pairedGhosts[element] = ghost.Id;
            }
            _ghostedUntil.Remove(old);
            return true;
        }

        /// <summary>
        /// Forgets where the items of the containers removed since the last pass were, unless the
        /// container is back in its list with the same item.
        /// </summary>
        private void ForgetRemovedItems()
        {
            foreach (RemovedItem removed in _removedItems)
            {
                ItemList list = removed.List;
                bool back = VisualTreeHelper.GetParent(removed.Container) == list.Panel;
                if (back && list.ItemOf.TryGetValue(removed.Container, out object item) && Equals(item, removed.Item))
                {
                    continue;
                }

                if (!back)
                {
                    list.ItemOf.Remove(removed.Container);
                }

                if (list.Slots.TryGetValue(removed.Item, out Slot slot) && slot.Container == removed.Container)
                {
                    list.Slots.Remove(removed.Item);
                }
            }

            _removedItems.Clear();
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

            // While something is dragged, the element that holds the mouse capture follows the
            // pointer: it, the elements around it and the elements inside it snap.
            UIElement captured = BrowserBridge.IsDragging ? Mouse.Captured as UIElement : null;
            if (captured != null)
            {
                _capturedChain.Clear();
                for (DependencyObject e = captured; e != null; e = VisualTreeHelper.GetParent(e))
                {
                    _capturedChain.Add(e);
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

                if (captured != null && (_capturedChain.Contains(element) || IsStrictDescendant(element, captured)))
                {
                    Snap(index, now);
                    continue;
                }

                double delay = (wave ? GetWaveDelay(index, originIndex, timing.Wave) / settings.Speed : 0) + othersDelay;
                int flags = GetFlags(element, ref record);

                string fromGhost = null;
                if ((record.Flags & RecordFlags.Paired) != 0)
                {
                    _pairedGhosts.TryGetValue(element, out fromGhost);
                }

                batch.AddMove(id, timingIndex, delay,
                    record.OldRect.X - record.NewRect.X,
                    record.OldRect.Y - record.NewRect.Y,
                    record.OldRect.Width, record.OldRect.Height,
                    record.NewRect.Width, record.NewRect.Height,
                    flags, fromGhost);

                _runningUntil[element] = (now + (long)(delay + timing.DurationMs) + 50, fromSwitch);

                if (settings.ShowWhatAnimates)
                {
                    batch.AddOutline(id, timing.DurationMs, delay, element == origin);
                }
            }

            if (captured != null)
            {
                bucket.Animated.RemoveAll(index => _capturedChain.Contains(_records[index].Element) || IsStrictDescendant(_records[index].Element, captured));
            }

            _animatedNow.Clear();
            if (bucket.Revealed.Count > 0)
            {
                _animatedNow.UnionWith(bucket.Animated);
            }

            bucket.Wipes = bucket.Fades = bucket.GhostWipes = bucket.GhostFades = bucket.OtherEffects = 0;
            bucket.OtherEffect = null;
            bucket.EffectPlayed = false;
            bucket.EffectWait = 0;
            bucket.CascadeStepMs = 0;
            bool othersMove = bucket.Animated.Count > 0;
            CountCascades(bucket.Revealed, bucket.Ghosts);
            foreach (int index in bucket.Revealed)
            {
                UIElement element = _records[index].Element;
                string id = Interop.GetDiv(element).Uid;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                // OthersMove=Before: the element waits for the room to be made, then for its turn
                // in the cascade of its list, then for its StartAfter.
                if (AnyEffects && GetAppearEffect(element, out ItemList itemList) is Transition effect)
                {
                    double effectDelay = (effect.OthersMove == OthersMove.Before && othersMove ? timing.DurationMs : 0) +
                                         NextCascadeDelay(_appearCascades, itemList, bucket, element, settings);
                    double wait = PlayAppearEffect(index, effect, effectDelay, raise: othersMove, settings, batch);
                    KeepInPlace(index, timingIndex, wave ? timing.Wave : 0, originIndex, othersDelay, settings, batch);
                    CountEffect(bucket, element, null, effect, appears: true, wait);
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
                    double wait = PlayGhost(disappearance, NextCascadeDelay(_disappearCascades, disappearance.List, bucket, disappearance.Element, settings), settings, batch);
                    CountEffect(bucket, disappearance.Element, disappearance.Parent, disappearance.Effect, appears: false, wait);
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
        /// An element that appears with an Appear of its own stays where it ends on screen while
        /// the containers it makes grow move to stay centered, or aligned to the right or the
        /// bottom: their moves are taken back from it, with their timing. It still moves with a
        /// container that something else pushes.
        /// </summary>
        private void KeepInPlace(int index, int timingIndex, double wave, int originIndex, double othersDelay, AnimationSettings settings, BatchBuilder batch)
        {
            if (_quickFades || _animatedNow.Count == 0)
            {
                return;
            }

            double dx = 0, dy = 0;
            int nearest = -1;
            for (int parent = _records[index].Parent; parent >= 0; parent = _records[parent].Parent)
            {
                if (!_animatedNow.Contains(parent))
                {
                    continue;
                }

                ref Record container = ref _records[parent];
                double x = GetAlignmentMove(container.Element, container.OldRect.X - container.NewRect.X, container.NewRect.Width - container.OldRect.Width, horizontal: true);
                double y = GetAlignmentMove(container.Element, container.OldRect.Y - container.NewRect.Y, container.NewRect.Height - container.OldRect.Height, horizontal: false);
                if (x != 0 || y != 0)
                {
                    dx += x;
                    dy += y;
                    if (nearest < 0)
                    {
                        nearest = parent;
                    }
                }
            }

            if (nearest < 0 || !IsAnimatable(_records[index].Element, out string id))
            {
                return;
            }

            double delay = (wave > 0 ? GetWaveDelay(nearest, originIndex, wave) / settings.Speed : 0) + othersDelay;
            batch.AddKeepInPlace(id, timingIndex, delay, -dx, -dy);
        }

        /// <summary>
        /// The move of an element on one axis (from its old place to its new one), when it only
        /// comes from its new size where its alignment puts it: half of what it grew when it is
        /// centered or stretched, all of it on the right or at the bottom. Otherwise 0, like when
        /// it is pushed, or along the axis where a StackPanel or a Canvas places it.
        /// </summary>
        private static double GetAlignmentMove(UIElement element, double moved, double grew, bool horizontal)
        {
            if (Math.Abs(moved) < 0.5 || element is not FrameworkElement fe)
            {
                return 0;
            }

            DependencyObject parent = VisualTreeHelper.GetParent(fe);
            if (parent is Canvas ||
                (parent is StackPanel panel && (panel.Orientation == Orientation.Horizontal) == horizontal) ||
                (parent is VirtualizingStackPanel list && (list.Orientation == Orientation.Horizontal) == horizontal))
            {
                return 0;
            }

            double share = horizontal
                ? fe.HorizontalAlignment switch { HorizontalAlignment.Left => 0, HorizontalAlignment.Right => 1, _ => 0.5 }
                : fe.VerticalAlignment switch { VerticalAlignment.Top => 0, VerticalAlignment.Bottom => 1, _ => 0.5 };
            return share > 0 && Math.Abs(moved - share * grew) <= 1 ? moved : 0;
        }

        /// <summary>
        /// Plays the Disappear of a ghost after its turn in the cascade of its list
        /// (<paramref name="cascadeMs"/>) and its StartAfter, and returns how long it waits for
        /// its StartAfter.
        /// </summary>
        private double PlayGhost(Disappearance disappearance, double cascadeMs, AnimationSettings settings, BatchBuilder batch)
        {
            disappearance.Handled = true;
            double wait = GetWaitMs(disappearance.Effect, settings);
            double delay = _quickFades ? wait : cascadeMs + wait;
            Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(disappearance.Effect, settings);
            EffectLook look = _quickFades ? FadeLook : disappearance.Effect.GetLook(appear: false);
            batch.AddGhost(disappearance.Id, batch.AddTiming(timing), delay, look, sizeCurve: false);
            if (delay > 0)
            {
                KeepGhosted(disappearance.Element, delay + timing.DurationMs);
            }

            return wait;
        }

        /// <summary>
        /// Counts, for each list with ItemCascade, the items that play its ItemAppear or its
        /// ItemDisappear together, for <see cref="NextCascadeDelay"/>.
        /// </summary>
        private void CountCascades(List<int> appearing, List<Disappearance> ghosts)
        {
            _appearCascades.Clear();
            _disappearCascades.Clear();
            if (!AnyItems)
            {
                return;
            }

            foreach (int index in appearing)
            {
                if ((_records[index].Flags & RecordFlags.Played) == 0 && GetAppearEffect(_records[index].Element, out ItemList list) != null)
                {
                    CountCascade(_appearCascades, list);
                }
            }

            foreach (Disappearance disappearance in ghosts)
            {
                if (!disappearance.Handled && !disappearance.Paired && disappearance.Effect != null)
                {
                    CountCascade(_disappearCascades, disappearance.List);
                }
            }
        }

        private static void CountCascade(Dictionary<ItemList, Cascade> cascades, ItemList list)
        {
            if (list == null || list.CascadeMs <= 0)
            {
                return;
            }

            if (!cascades.TryGetValue(list, out Cascade cascade))
            {
                cascade = new Cascade();
                cascades.Add(list, cascade);
            }
            cascade.Count++;
        }

        /// <summary>
        /// When the next item of a list starts its effect, in the cascade of the items counted by
        /// <see cref="CountCascades"/>. With many items, they come closer together, so that the
        /// whole cascade lasts at most 0.6 s.
        /// </summary>
        private static double NextCascadeDelay(Dictionary<ItemList, Cascade> cascades, ItemList list, Bucket bucket, UIElement element, AnimationSettings settings)
        {
            double step = GetCascadeStep(cascades, list, settings);
            if (step <= 0)
            {
                return 0;
            }

            if (bucket != null && element == bucket.EffectElement)
            {
                bucket.CascadeStepMs = step;
            }

            return cascades[list].Next++ * step;
        }

        private static double GetCascadeStep(Dictionary<ItemList, Cascade> cascades, ItemList list, AnimationSettings settings)
            => list != null && cascades.TryGetValue(list, out Cascade cascade) && cascade.Count >= 2
                ? Math.Min(list.CascadeMs, MaxCascadeTotalMs / (cascade.Count - 1)) / settings.Speed
                : 0;

        private void CountEffect(Bucket bucket, UIElement element, UIElement parent, Transition effect, bool appears, double waitMs)
        {
            if (element == bucket.EffectElement)
            {
                bucket.EffectPlayed = true;
                bucket.EffectWait = waitMs;
            }
            else if (bucket.OtherEffects++ == 0)
            {
                bucket.OtherEffect = NameOf(element, parent) + " " + effect.Describe(appears) + After(waitMs);
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
            CountCascades(bucket.Appearing, bucket.Ghosts);
            foreach (int index in bucket.Appearing)
            {
                UIElement element = _records[index].Element;
                if ((_records[index].Flags & RecordFlags.Played) == 0 && GetAppearEffect(element, out ItemList itemList) is Transition effect)
                {
                    double wait = PlayAppearEffect(index, effect, NextCascadeDelay(_appearCascades, itemList, null, element, settings), raise: false, settings, batch);
                    if (element == bucket.EffectElement)
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
                    double wait = PlayGhost(disappearance, NextCascadeDelay(_disappearCascades, disappearance.List, null, disappearance.Element, settings), settings, batch);
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
            // The cascade is told when all of them are items of the same list.
            double firstCascade = 0;
            ItemList firstList = null;
            int skipped = 0;
            UIElement firstSkipped = null;

            CountCascades(_effectAppears, _disappearing);
            foreach (int index in _effectAppears)
            {
                ref Record record = ref _records[index];
                if ((record.Flags & RecordFlags.Played) != 0 || !_effectsPlay)
                {
                    continue;
                }

                UIElement element = record.Element;
                Transition effect = GetAppearEffect(element, out ItemList itemList);
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

                double wait = PlayAppearEffect(index, effect, NextCascadeDelay(_appearCascades, itemList, null, element, settings), raise: false, settings, batch);

                if (logging)
                {
                    if (appearsWithContainer)
                    {
                        if (withContainer++ == 0)
                        {
                            firstWithContainer = element;
                            firstEffect = effect;
                            firstWait = wait;
                            firstList = itemList;
                            firstCascade = GetCascadeStep(_appearCascades, itemList, settings);
                        }
                        else if (itemList != firstList)
                        {
                            firstCascade = 0;
                        }
                    }
                    else
                    {
                        Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(effect, settings);
                        UIElement parent = VisualTreeHelper.GetParent(element) as UIElement;
                        _messages.Add($"{DescribeAppeared(element, (record.Flags & RecordFlags.Released) != 0, effect, settings)}; " +
                                      $"{DescribeOwnEffect(effect, true, _quickFades, timing, snapReason, wait, DescribeEffectSource(element, effect, true, parent, own: true))}.");
                    }
                }
            }

            if (withContainer == 1)
            {
                Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(firstEffect, settings);
                UIElement parent = VisualTreeHelper.GetParent(firstWithContainer) as UIElement;
                _messages.Add($"{ElementNames.Of(firstWithContainer)} appeared with its container; " +
                              $"{DescribeOwnEffect(firstEffect, true, _quickFades, timing, snapReason, firstWait, DescribeEffectSource(firstWithContainer, firstEffect, true, parent, own: true))}.");
            }
            else if (withContainer > 1)
            {
                string cascade = firstCascade > 0 && !_quickFades ? $", one after the other (every {Seconds(firstCascade)})" : string.Empty;
                _messages.Add($"{withContainer} elements appeared with their container, and play their effects{cascade}, like {ElementNames.Of(firstWithContainer)}.");
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

                if (disappearance.Effect != null && _effectsPlay && !disappearance.Paired)
                {
                    double wait = PlayGhost(disappearance, NextCascadeDelay(_disappearCascades, disappearance.List, null, disappearance.Element, settings), settings, batch);
                    if (logging)
                    {
                        Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(disappearance.Effect, settings);
                        string source = DescribeEffectSource(disappearance.Element, disappearance.Effect, false, disappearance.Parent, own: true);
                        _messages.Add($"{NameOf(disappearance.Element, disappearance.Parent)} disappeared; " +
                                      $"{DescribeOwnEffect(disappearance.Effect, false, _quickFades, timing, snapReason, wait, source)}.");
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

        /// <summary>
        /// The Appear of the element, or else the ItemAppear of the list it is an item of.
        /// </summary>
        private Transition GetAppearEffect(UIElement element) => GetAppearEffect(element, out _);

        private Transition GetAppearEffect(UIElement element, out ItemList itemList)
        {
            itemList = null;
            Transition effect = Animate.GetAppear(element);
            if (effect == null && AnyItems && GetListOf(element) is ItemList list && list.Appear is Transition item)
            {
                effect = item;
                itemList = list;
            }

            return effect != null && !effect.IsNone ? effect : null;
        }

        /// <summary>
        /// The Disappear of the element, or else the ItemDisappear of the list it is an item of
        /// (<paramref name="itemList"/>). <paramref name="parent"/> is where it is removed from.
        /// </summary>
        private Transition GetDisappearEffect(UIElement element, UIElement parent, out ItemList itemList)
        {
            itemList = null;
            Transition effect = Animate.GetDisappear(element);
            if (effect == null && AnyItems && parent is Panel panel && GetList(panel) is ItemList list && list.Disappear is Transition item)
            {
                effect = item;
                itemList = list;
            }

            return effect != null && !effect.IsNone ? effect : null;
        }

        /// <summary>
        /// Says whether the element turned off its automatic effect with Appear=None or Disappear=None,
        /// or the list it is an item of with ItemAppear=None or ItemDisappear=None.
        /// </summary>
        private bool HasNoEffect(UIElement element, bool appear, UIElement parent = null)
        {
            Transition effect = appear ? Animate.GetAppear(element) : Animate.GetDisappear(element);
            if (effect == null && AnyItems && (parent ?? VisualTreeHelper.GetParent(element)) is Panel panel && GetList(panel) is ItemList list)
            {
                effect = appear ? list.Appear : list.Disappear;
            }

            return effect != null && effect.IsNone;
        }

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
            if (_removedItems.Count > 0) ForgetRemovedItems();
            if (_listCache.Count > 0) _listCache.Clear();
            _switchOnly = false;
            _windowChecked = false;
        }

        private void Reset()
        {
            ResetRecords();
            ClearMeasureState();
            _messages.Clear();
            _disappearing.Clear();
            _ghostOf.Clear();
        }

        /// <summary>
        /// Forgets what was arranged, keeping what was measured and shown in the pass.
        /// </summary>
        private void ResetRecords()
        {
            Array.Clear(_records, 0, _recordCount);
            _recordCount = 0;
            _recordIndex.Clear();
            _pairedGhosts.Clear();
            _stack.Clear();
            _sessionOrder.Clear();
            _areaCache.Clear();
            _toCancel.Clear();
            _toRetarget.Clear();
            _keepRunning = false;
            _effectAppears.Clear();
            _effectTimings.Clear();
            _windowTimings.Clear();

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
        private enum RecordFlags : ushort
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
            // A container that shows an item its list showed in the last pass: it glides from there.
            Paired = 256,
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

        /// <summary>
        /// The offset of a scrolling element at the last pass where it was measured or arranged,
        /// from when it appeared. Until then, it is at the top left.
        /// </summary>
        private sealed class ScrollOffset
        {
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
            // the group its timing, and the parent it appears in or disappears from.
            public Transition Effect;
            public UIElement EffectElement;
            public UIElement EffectParent;
            public bool EffectAppears;

            // The items that glide to their new places, and the list of the first one.
            public int Paired;
            public Panel PairedList;

            // What Emit played, for the message.
            public bool EffectPlayed;
            public double EffectWait;
            public double CascadeStepMs;
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
                EffectParent = null;
                EffectAppears = false;
                Paired = 0;
                PairedList = null;
                EffectPlayed = false;
                EffectWait = 0;
                CascadeStepMs = 0;
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
            // The list whose ItemDisappear it plays, or null.
            public ItemList List;
            public int ParentIndex;
            public bool Handled;
            // Its item is shown again in the next pass: its ghost goes away.
            public bool Paired;
            // Copied without an effect to play: only for where it is on screen, or because it was
            // only scrolled or virtualized out of view.
            public bool NoEffect;
            // For an item of an ItemsControl: the ItemsControl, and the data item it showed.
            public ItemsControl Owner;
            public object Item;
        }

        /// <summary>
        /// A Panel whose children are items: with item settings, on it or on the ItemsControl it
        /// is the items panel of, or the items panel of an ItemsControl inside an area. Knows
        /// where each item was at the end of the last pass, to pair it with its new container.
        /// </summary>
        private sealed class ItemList
        {
            public ItemList(Panel panel)
            {
                Panel = panel;
            }

            public Panel Panel { get; }

            public ItemsControl Owner;

            // The item shown by each container, and where each item is, relative to the panel.
            public readonly Dictionary<UIElement, object> ItemOf = new Dictionary<UIElement, object>();
            public readonly Dictionary<object, Slot> Slots = new Dictionary<object, Slot>();

            /// <summary>
            /// The list, as the app sees it: the ItemsControl, or the Panel.
            /// </summary>
            public UIElement Element => (UIElement)Owner ?? Panel;

            public Transition Appear => (Transition)Read(Animate.ItemAppearProperty);

            public Transition Disappear => (Transition)Read(Animate.ItemDisappearProperty);

            public Movement Movement => (Movement)Read(Animate.ItemMoveAndResizeProperty);

            public double CascadeMs
            {
                get
                {
                    AnimationTime cascade = Animate.GetItemCascade(Panel);
                    if (cascade == AnimationTime.Zero && Owner != null)
                    {
                        cascade = Animate.GetItemCascade(Owner);
                    }

                    return Math.Min(MaxCascadeMs, cascade.Seconds * 1000);
                }
            }

            /// <summary>
            /// The element that has the setting: the Panel, or else the ItemsControl.
            /// </summary>
            public UIElement HolderOf(DependencyProperty property)
                => Panel.GetValue(property) != null || Owner == null ? Panel : Owner;

            /// <summary>
            /// The data item a container shows, or the container itself for the children of a Panel.
            /// </summary>
            public object ItemFromContainer(UIElement container)
            {
                if (Owner != null)
                {
                    object item = Owner.ItemContainerGenerator.ItemFromContainer(container);
                    if (item != null && item != DependencyProperty.UnsetValue)
                    {
                        return item;
                    }
                }

                return container;
            }

            public static bool HasSettings(UIElement element)
                => element.GetValue(Animate.ItemAppearProperty) != null ||
                   element.GetValue(Animate.ItemDisappearProperty) != null ||
                   element.GetValue(Animate.ItemMoveAndResizeProperty) != null ||
                   Animate.GetItemCascade(element) != AnimationTime.Zero;

            private object Read(DependencyProperty property) => Panel.GetValue(property) ?? Owner?.GetValue(property);
        }

        private struct Slot
        {
            public UIElement Container;
            public Rect Rect;
        }

        private struct RemovedItem
        {
            public ItemList List;
            public UIElement Container;
            public object Item;
        }

        private sealed class Cascade
        {
            public int Count;
            public int Next;
        }

        #endregion
    }
}
