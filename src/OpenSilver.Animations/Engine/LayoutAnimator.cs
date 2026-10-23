using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    /// window (AlsoOnWindowResize) form groups of their own that only go down. At the end of the
    /// pass, each group animates or snaps as a whole, and all the animations of the pass are sent
    /// to the browser in one batch, in the same flush as the layout writes.
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

        private static LayoutAnimator _instance;

        /// <summary>
        /// Set once an element uses <see cref="AnimationScope.EverythingInside"/>. Until then, the
        /// ancestors are never searched for an area.
        /// </summary>
        internal static bool AnyAreas;

        private Record[] _records = new Record[64];
        private int _recordCount;
        private readonly Dictionary<UIElement, int> _recordIndex = new Dictionary<UIElement, int>();
        private readonly List<int> _stack = new List<int>();
        private readonly List<int> _sessionOrder = new List<int>();

        private readonly HashSet<UIElement> _measured = new HashSet<UIElement>();
        private readonly HashSet<UIElement> _carriers = new HashSet<UIElement>();
        private readonly HashSet<UIElement> _measureOrigins = new HashSet<UIElement>();
        private readonly Dictionary<UIElement, bool> _scrollChecked = new Dictionary<UIElement, bool>();
        private readonly ConditionalWeakTable<UIElement, ScrollOffset> _scrollOffsets = new ConditionalWeakTable<UIElement, ScrollOffset>();

        private readonly List<int> _groupParents = new List<int> { 0 };
        private readonly List<GroupInfo> _groups = new List<GroupInfo> { default };

        private readonly Dictionary<UIElement, Area> _areaCache = new Dictionary<UIElement, Area>();
        private readonly Dictionary<UIElement, long> _appeared = new Dictionary<UIElement, long>();
        private readonly List<UIElement> _expired = new List<UIElement>();
        private readonly Dictionary<UIElement, long> _runningUntil = new Dictionary<UIElement, long>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _nextSweep;

        private readonly Dictionary<int, Bucket> _buckets = new Dictionary<int, Bucket>();
        private readonly Stack<Bucket> _bucketPool = new Stack<Bucket>();
        private readonly List<UIElement> _toCancel = new List<UIElement>();
        private readonly List<string> _messages = new List<string>();

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

        private LayoutAnimator() { }

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

            // Everything inside an element that appears appears with it.
            if (isFirstArrange && parent >= 0 && (_records[parent].Flags & RecordFlags.FirstArrange) != 0)
            {
                return;
            }

            int index = GetOrAddRecord(element);
            ref Record record = ref _records[index];
            record.Parent = parent;

            if (isFirstArrange)
            {
                record.Flags |= RecordFlags.FirstArrange;
                _appeared[element] = _clock.ElapsedMilliseconds;
            }
            else if (element is IScrollInfo scrollInfo && IsScrolled(element, scrollInfo))
            {
                record.Flags |= RecordFlags.Scrolled;
            }
            else if (!IsWindowResizePass() &&
                     (_measureOrigins.Contains(element) ||
                      (!_measured.Contains(element) && AreClose(finalRect, previousFinalRect))))
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
            if (record.Group != 0 && record.Parent >= 0 && _groups[Find(record.Group)].Kind == GroupKind.Change)
            {
                ref Record parentRecord = ref _records[record.Parent];
                parentRecord.Group = parentRecord.Group == 0 ? record.Group : Union(parentRecord.Group, record.Group);
            }

            if (_stack.Count == 0)
            {
                EndArrangeSession();
            }
        }

        protected override void OnDetaching(UIElement child, UIElement parent)
        {
            if (_runningUntil.Count > 0)
            {
                _runningUntil.Remove(child);
            }
        }

        protected override void OnLayoutPassCompleted()
        {
            if (_appeared.Count > 0)
            {
                PruneAppeared();
            }

            if (_recordCount == 0 && !_forced && !_skip)
            {
                ClearMeasureState();
                return;
            }

            try
            {
                CompletePass();
            }
            catch (Exception ex)
            {
                BrowserBridge.WriteToConsole("Something went wrong, nothing was animated: " + ex);
            }
            finally
            {
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

        private void CompletePass()
        {
            AnimationSettings settings = AnimationSettings.Current;
            bool logging = settings.LogForDebugging || AnimationSettings.HasLoggedHandlers;
            long now = _clock.ElapsedMilliseconds;

            string snapReason = GetGlobalSnapReason(settings);
            _stamp++;

            for (int i = 0; i < _recordCount; i++)
            {
                ref Record record = ref _records[i];
                if ((record.Flags & RecordFlags.FirstArrange) != 0)
                {
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
                        Snap(record.Element, now);
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

            var batch = new BatchBuilder();
            foreach (KeyValuePair<int, Bucket> pair in _buckets)
            {
                ProcessGroup(pair.Key, pair.Value, settings, snapReason, logging, now, batch);
            }

            foreach (UIElement element in _toCancel)
            {
                batch.AddCancel(element);
            }

            if (!batch.IsEmpty)
            {
                BrowserBridge.Run(batch.ToJson());
            }

            if (_messages.Count > 0)
            {
                Publish(settings);
            }

            if (now >= _nextSweep && _runningUntil.Count > 0)
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
                    ? "reduced motion is simulated (AnimationSettings.SimulateReducedMotion)"
                    : "the device asks for reduced motion";
            }

            if (BrowserBridge.IsDragging)
            {
                return "something is being dragged";
            }

            return null;
        }

        private void ProcessGroup(int group, Bucket bucket, AnimationSettings settings, string snapReason, bool logging, long now, BatchBuilder batch)
        {
            if (bucket.Members.Count == 0)
            {
                return;
            }

            GroupInfo info = _groups[group];
            bool windowResize = info.Kind == GroupKind.WindowResize && bucket.Origins.Count == 0;

            Timing timing = null;
            string source = null;
            string reason = snapReason;
            UIElement firstOrigin = null;

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

            if (timing != null)
            {
                int visible = CullAndCount(bucket);
                if (visible > MaxAnimatedElements)
                {
                    reason = "more than " + MaxAnimatedElements + " elements would animate at once";
                    timing = null;
                }
                else if (visible == 0)
                {
                    return;
                }
            }

            if (timing == null)
            {
                foreach (int index in bucket.Members)
                {
                    Snap(_records[index].Element, now);
                }

                if (logging && reason != null && !IsSilent(reason) && (_forced || InvolvesSettings(bucket)))
                {
                    if (!windowResize || now - _lastWindowResizeLog > 1000)
                    {
                        if (windowResize)
                        {
                            _lastWindowResizeLog = now;
                        }

                        LogSnap(Describe(bucket, firstOrigin, windowResize) + "; nothing animated because " + reason + ".", now);
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
                    _messages.Add($"{Describe(bucket, firstOrigin, false)}; {ElementNames.Count(bucket.Animated.Count, "element animates", "elements animate together")} ({timing.Description}, from {source}).");
                }
            }
        }

        /// <summary>
        /// Says whether a group with origins animates. Returns why not, or null when it animates.
        /// </summary>
        /// <remarks>
        /// It animates when an origin has a setting, when it is inside an area, or when a member
        /// has a setting. The timing comes from the first of: Animate.Changes, the setting of the
        /// origin, its area, the nearest ancestor that is a member, the nearest member.
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
            Movement originMovement = null;
            UIElement originMovementElement = null;
            Movement areaMovement = null;
            UIElement areaElement = null;

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

                    if (IsSettling(origin))
                    {
                        exclusion = SettlingReason;
                        continue;
                    }
                }

                Area area = GetArea(origin);
                if (area.IsCarvedOut && !_forced)
                {
                    exclusion = "the change started inside an element with MoveAndResize=None";
                    continue;
                }

                if (eligible == null || (IsTemplatePart(eligible) && !IsTemplatePart(origin)))
                {
                    eligible = origin;
                }

                if (originMovement == null && Animate.GetMoveAndResize(origin) is Movement own && !own.IsNone)
                {
                    originMovement = own;
                    originMovementElement = origin;
                }

                if (areaMovement == null && area.Movement != null)
                {
                    areaMovement = area.Movement;
                    areaElement = area.Element;
                }
            }

            if (eligible == null)
            {
                firstOrigin = GetDescribedOrigin(bucket);
                return exclusion;
            }

            firstOrigin = originMovementElement ?? eligible;

            if (_forced)
            {
                timing = Timing.FromDuration(_forcedDuration, settings);
                source = "Animate.Changes";
                return null;
            }

            if (originMovement != null)
            {
                timing = Timing.FromMovement(originMovement, settings);
                source = "the MoveAndResize of " + ElementNames.Of(originMovementElement);
                return null;
            }

            if (areaMovement != null)
            {
                timing = Timing.FromMovement(areaMovement, settings);
                source = "the EverythingInside of " + ElementNames.Of(areaElement);
                return null;
            }

            if (FindAncestorMovement(eligible, group, out UIElement owner) is Movement ancestorMovement)
            {
                timing = Timing.FromMovement(ancestorMovement, settings);
                source = "the MoveAndResize of " + ElementNames.Of(owner);
                return null;
            }

            if (FindNearestMemberMovement(bucket, eligible, out owner) is Movement memberMovement)
            {
                timing = Timing.FromMovement(memberMovement, settings);
                source = "the MoveAndResize of " + ElementNames.Of(owner);
                return null;
            }

            return "no element involved has MoveAndResize";
        }

        /// <summary>
        /// The setting of the nearest ancestor that moved or changed size in this change. An
        /// ancestor that stays in place doesn't count: its setting is about its own box.
        /// </summary>
        private Movement FindAncestorMovement(UIElement origin, int group, out UIElement owner)
        {
            for (UIElement e = VisualTreeHelper.GetParent(origin) as UIElement; e != null; e = VisualTreeHelper.GetParent(e) as UIElement)
            {
                if (Animate.GetMoveAndResize(e) is Movement m && !m.IsNone && IsMember(e, group))
                {
                    owner = e;
                    return m;
                }
            }

            owner = null;
            return null;
        }

        private bool IsMember(UIElement element, int group)
        {
            if (!_recordIndex.TryGetValue(element, out int index))
            {
                return false;
            }

            ref Record record = ref _records[index];
            return record.Group != 0 && (record.Flags & RecordFlags.FirstArrange) == 0 && Find(record.Group) == group && IsChanged(ref record);
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
        /// is the snap worth a message.
        /// </summary>
        private bool InvolvesSettings(Bucket bucket)
        {
            foreach (int index in bucket.Members)
            {
                if (GetEffectiveMovement(_records[index].Element) != null)
                {
                    return true;
                }
            }

            foreach (int index in bucket.Origins)
            {
                if (GetEffectiveMovement(_records[index].Element) != null)
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

            ref Record record = ref _records[_recordIndex[origin]];
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
                    area = new Area { IsCarvedOut = true };
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

            return bucket.Animated.Count;
        }

        private void Emit(Bucket bucket, Timing timing, UIElement origin, AnimationSettings settings, long now, BatchBuilder batch)
        {
            int timingIndex = batch.AddTiming(timing);
            bool wave = timing.Wave > 0 && origin != null;
            int originIndex = origin != null && _recordIndex.TryGetValue(origin, out int oi) ? oi : -1;

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

                double delay = wave ? GetWaveDelay(index, originIndex, timing.Wave) / settings.Speed : 0;
                int flags = GetFlags(element, ref record);

                batch.AddMove(id, timingIndex, delay,
                    record.OldRect.X - record.NewRect.X,
                    record.OldRect.Y - record.NewRect.Y,
                    record.OldRect.Width, record.OldRect.Height,
                    record.NewRect.Width, record.NewRect.Height,
                    flags);

                _runningUntil[element] = now + (long)(delay + timing.DurationMs) + 50;

                if (settings.ShowWhatAnimates)
                {
                    batch.AddOutline(id, timing.DurationMs, delay, element == origin);
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

        private static int GetFlags(UIElement element, ref Record record)
        {
            bool resized = !AreClose(record.OldRect.Width, record.NewRect.Width) || !AreClose(record.OldRect.Height, record.NewRect.Height);
            if (!resized)
            {
                return 0;
            }

            int flags = 0;
            if (VisualTreeHelper.GetClip(element) is RectangleGeometry clip && clip.Transform == null)
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

        private void Snap(UIElement element, long now)
        {
            if (_runningUntil.TryGetValue(element, out long until))
            {
                _runningUntil.Remove(element);
                if (until > now)
                {
                    _toCancel.Add(element);
                }
            }
        }

        private void SweepRunning(long now)
        {
            _nextSweep = now + 2000;
            List<UIElement> finished = null;
            foreach (KeyValuePair<UIElement, long> pair in _runningUntil)
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
                    _runningUntil.Remove(element);
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
            if (_scrollChecked.Count > 0) _scrollChecked.Clear();
            _windowChecked = false;
        }

        private void Reset()
        {
            Array.Clear(_records, 0, _recordCount);
            _recordCount = 0;
            _recordIndex.Clear();
            _stack.Clear();
            _sessionOrder.Clear();
            ClearMeasureState();
            _areaCache.Clear();
            _toCancel.Clear();
            _messages.Clear();

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
            FirstArrange = 2,
            Origin = 4,
            Scrolled = 8,
            NotRendered = 16,
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

            public void Clear()
            {
                Origins.Clear();
                Members.Clear();
                Animated.Clear();
            }
        }

        #endregion
    }
}
