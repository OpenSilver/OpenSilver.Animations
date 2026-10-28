using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenSilver.Animations.Engine
{
    // Magic move. An element with a Magic move that is collapsed or removed is copied where it is on
    // screen: its ghost, or, inside a container that disappears, a capture, which the browser keeps
    // aside. At the end of the pass, each element with a Magic move that appeared is paired with one
    // that disappeared with the same id, in the pass or up to 0.3 s before, and travels from there:
    // in the browser, a layer with a copy of each moves and changes size from one place to the
    // other, while they cross-fade. One that disappears after its partner's pass, but in the same
    // task, so before the browser paints, pairs in the next pass. A container with MagicMoveByName
    // pairs the elements with the same x:Name in its old content and in its new one, which cross-fade.
    internal sealed partial class LayoutAnimator
    {
        // How long an element that disappeared waits for another one with its id, and a container
        // with MagicMoveByName for its new content.
        private const long MagicWaitMs = 300;

        // A trip up to NearTrip pixels takes the default duration, and FarTripFactor times as much
        // from FarTrip pixels.
        private const double NearTrip = 150;
        private const double FarTrip = 900;
        private const double FarTripFactor = 1.8;

        // A background is the first of several children, and covers this much of its parent's
        // width and height.
        private const double BackgroundShare = 0.9;

        // The elements copied inside a container that disappears, at most.
        private const int MaxCaptures = 100;

        /// <summary>
        /// Set once an element has a MagicMove or a MagicMoveByName. Until then, the elements that
        /// appear or disappear are never checked for one.
        /// </summary>
        internal static bool AnyMagic;

        // The elements with a Magic move that disappeared since the last pass, or that wait for
        // another one to appear, and the contents replaced in containers with MagicMoveByName.
        private readonly List<Departure> _departures = new List<Departure>();
        private readonly List<NameSwap> _swaps = new List<NameSwap>();
        private DispatcherTimer _magicTimer;

        // The elements with a Magic move that appeared without a partner in a pass that ended in this
        // task, like the content of a popup, which is laid out as it opens, before the element it
        // replaces is collapsed. Until the end of the task, nothing was painted yet.
        private readonly List<EarlyArrival> _earlyArrivals = new List<EarlyArrival>();
        private bool _earlyArrivalsExpire;

        public static void OnMagicMoveSet(UIElement element, MagicMove magicMove)
        {
            if (_instance != null && !magicMove.HasId)
            {
                Warn("MagicMove", element, "it has no Id, so it is ignored");
            }
        }

        public static void CheckMagicMoveByName(UIElement element)
        {
            if (_instance != null && element is not ContentControl)
            {
                Warn("MagicMoveByName", element, "it is for a ContentControl or a Frame, so it is ignored");
            }
        }

        private static string GetMagicId(UIElement element)
            => element.GetValue(Animate.MagicMoveProperty) is MagicMove magicMove && magicMove.HasId ? magicMove.Id.Trim() : null;

        #region Departures

        /// <summary>
        /// The content of a container with MagicMoveByName is being removed: the container waits
        /// for its new content.
        /// </summary>
        private NameSwap StartSwap(UIElement content, UIElement parent)
        {
            ContentControl host = parent as ContentControl ?? (parent as ContentPresenter)?.TemplatedParent as ContentControl;
            if (host == null || !Animate.GetMagicMoveByName(host) || !parent.IsVisible || _skip || !AnimationSettings.Current.IsEnabled)
            {
                return null;
            }

            // Replaced again before its new content came: the first content goes on.
            foreach (NameSwap existing in _swaps)
            {
                if (ReferenceEquals(existing.Presenter, parent))
                {
                    return null;
                }
            }

            var swap = new NameSwap { Host = host, Presenter = parent, OldContent = content };
            _swaps.Add(swap);
            return swap;
        }

        /// <summary>
        /// Copies the elements with a Magic move inside a container that is collapsed or removed,
        /// and the named elements of the old content of a container with MagicMoveByName
        /// (<paramref name="swap"/>), where they are on screen, to travel from there. Before the
        /// container's ghost, so that it knows them.
        /// </summary>
        private void DepartInside(UIElement container, UIElement parent, NameSwap swap)
        {
            if (parent == null || !parent.IsVisible || _skip || !AnimationSettings.Current.IsEnabled ||
                (parent is IScrollInfo scrollInfo && WasScrolled(parent, scrollInfo)) ||
                !IsAnimatable(container, out string containerId))
            {
                return;
            }

            List<UIElement> found = null;
            FindMagicInside(container, ref found);
            Dictionary<string, UIElement> names = null;
            if (swap != null)
            {
                names = new Dictionary<string, UIElement>();
                CollectNames(container, names);
            }

            if (found == null && (names == null || names.Count == 0))
            {
                return;
            }

            // Those far from the screen would travel from out of view.
            Rect viewport = GetViewport();
            var ids = new List<string>();
            if (found != null)
            {
                foreach (UIElement element in found)
                {
                    if (ids.Count < MaxCaptures && Depart(element, GetMagicId(element), null, null, null, viewport) is Departure departure)
                    {
                        ids.Add(departure.Id);
                    }
                }
            }

            if (names != null)
            {
                foreach (KeyValuePair<string, UIElement> pair in names)
                {
                    if (ids.Count < MaxCaptures && Depart(pair.Value, pair.Key, null, null, swap, viewport) is Departure departure)
                    {
                        ids.Add(departure.Id);
                    }
                }
            }

            if (ids.Count > 0)
            {
                BrowserBridge.Capture(containerId, ids);
            }
        }

        /// <summary>
        /// Remembers an element with a Magic move that disappears, copied in its parent
        /// (<paramref name="ghost"/>) or inside its container, with where it is on screen.
        /// </summary>
        private Departure Depart(UIElement element, string key, UIElement parent, Disappearance ghost, NameSwap swap, Rect viewport)
        {
            string id = ghost?.Id;
            Size size = element.RenderSize;
            if ((id == null && !IsAnimatable(element, out id)) || size.Width <= 0 || size.Height <= 0)
            {
                return null;
            }

            Rect rect = GetWindowRect(element);
            if (!viewport.IsEmpty && (rect.IsEmpty || !viewport.IntersectsWith(rect)))
            {
                return null;
            }

            parent ??= VisualTreeHelper.GetParent(element) as UIElement;
            var ancestors = new List<UIElement>();
            for (DependencyObject e = parent; e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is UIElement ancestor)
                {
                    ancestors.Add(ancestor);
                }
            }

            // It disappears again before the end of its wait: its newest copy counts.
            _departures.RemoveAll(d => ReferenceEquals(d.Element, element));

            var departure = new Departure
            {
                Key = key,
                Element = element,
                Id = id,
                Ghost = ghost,
                Swap = swap,
                Settings = Animate.GetMagicMove(element),
                Rect = rect,
                Ancestors = ancestors,
                IsBackground = IsBackground(element, parent),
                IsText = element is TextBlock,
            };
            _departures.Add(departure);
            if (swap == null && _earlyArrivals.Count > 0)
            {
                ClaimEarlyArrival(key, element);
            }
            return departure;
        }

        /// <summary>
        /// An element with the same id that appeared earlier in this task stays hidden, to travel
        /// from this one in the next pass.
        /// </summary>
        private void ClaimEarlyArrival(string key, UIElement element)
        {
            foreach (EarlyArrival early in _earlyArrivals)
            {
                if (!early.Claimed && !ReferenceEquals(early.Element, element) && string.Equals(early.Key, key, StringComparison.Ordinal) &&
                    IsAnimatable(early.Element, out string id))
                {
                    early.Claimed = true;
                    if (_held.Add(early.Element))
                    {
                        BrowserBridge.Hold(id);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// The elements with a Magic move inside a container, without those inside them.
        /// </summary>
        private static void FindMagicInside(UIElement container, ref List<UIElement> found)
        {
            int count = VisualTreeHelper.GetChildrenCount(container);
            for (int i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(container, i) is UIElement child && child.Visibility == Visibility.Visible)
                {
                    if (GetMagicId(child) != null)
                    {
                        (found ??= new List<UIElement>()).Add(child);
                    }
                    else
                    {
                        FindMagicInside(child, ref found);
                    }
                }
            }
        }

        /// <summary>
        /// The elements inside a content that have an x:Name, and no Magic move of their own. The
        /// parts of control templates don't count: their names are the same in every control.
        /// </summary>
        private static void CollectNames(UIElement element, Dictionary<string, UIElement> names)
        {
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(element, i) is UIElement child && child.Visibility == Visibility.Visible)
                {
                    if (child is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name) && fe.TemplatedParent is not Control &&
                        GetMagicId(child) == null && !names.ContainsKey(fe.Name))
                    {
                        names.Add(fe.Name, child);
                    }

                    CollectNames(child, names);
                }
            }
        }

        /// <summary>
        /// The elements with a Magic move inside a container that is shown again stay hidden until
        /// they travel, like the container's own one.
        /// </summary>
        private void HoldMagicInside(UIElement container)
        {
            List<UIElement> found = null;
            FindMagicInside(container, ref found);
            if (found == null)
            {
                return;
            }

            foreach (UIElement element in found)
            {
                if (!_held.Contains(element) && IsAnimatable(element, out string id))
                {
                    _held.Add(element);
                    BrowserBridge.Hold(id);
                }
            }
        }

        private static Rect GetWindowRect(UIElement element)
        {
            try
            {
                return new Rect(element.TransformToVisual(null).Transform(new Point()), element.RenderSize);
            }
            catch
            {
                return Rect.Empty;
            }
        }

        /// <summary>
        /// Says whether an element is the background of its parent, like a selection pill: the
        /// first of several children, as large as the parent, or almost.
        /// </summary>
        private static bool IsBackground(UIElement element, UIElement parent)
        {
            if (parent == null || VisualTreeHelper.GetChildrenCount(parent) < 2 || !ReferenceEquals(VisualTreeHelper.GetChild(parent, 0), element))
            {
                return false;
            }

            Size own = element.RenderSize;
            Size around = parent.RenderSize;
            return around.Width > 0 && around.Height > 0 &&
                   own.Width >= around.Width * BackgroundShare && own.Height >= around.Height * BackgroundShare;
        }

        private static UIElement GetContent(UIElement presenter)
            => VisualTreeHelper.GetChildrenCount(presenter) > 0 ? VisualTreeHelper.GetChild(presenter, 0) as UIElement : null;

        #endregion

        #region Pairing

        /// <summary>
        /// Pairs each element with a Magic move that appeared in this pass with one that
        /// disappeared with the same id, and makes it travel. Those that disappeared without a pair
        /// wait for one. A container with MagicMoveByName whose new content came pairs their named
        /// elements, and cross-fades them.
        /// </summary>
        private void PlayMagicMoves(AnimationSettings settings, string snapReason, bool logging, long now, BatchBuilder batch)
        {
            try
            {
                var arrivals = new List<Arrival>();
                for (int i = 0; i < _recordCount; i++)
                {
                    ref Record record = ref _records[i];
                    if ((record.Flags & (RecordFlags.Appears | RecordFlags.Scrolled)) == RecordFlags.Appears && GetMagicId(record.Element) is string key)
                    {
                        arrivals.Add(new Arrival { Key = key, Element = record.Element, Record = i });
                    }
                }

                foreach (EarlyArrival early in _earlyArrivals)
                {
                    if (early.Claimed && early.Element.IsVisible)
                    {
                        arrivals.Add(new Arrival
                        {
                            Key = early.Key,
                            Element = early.Element,
                            Record = _recordIndex.TryGetValue(early.Element, out int index) ? index : -1,
                            Early = true,
                        });
                    }
                }
                _earlyArrivals.RemoveAll(early => early.Claimed);

                foreach (NameSwap swap in _swaps)
                {
                    if (swap.NewContent == null && GetContent(swap.Presenter) is UIElement content &&
                        !ReferenceEquals(content, swap.OldContent) && content.Visibility == Visibility.Visible)
                    {
                        swap.NewContent = content;
                        var names = new Dictionary<string, UIElement>();
                        CollectNames(content, names);
                        foreach (KeyValuePair<string, UIElement> pair in names)
                        {
                            arrivals.Add(new Arrival
                            {
                                Key = pair.Key,
                                Element = pair.Value,
                                Swap = swap,
                                Record = _recordIndex.TryGetValue(pair.Value, out int index) ? index : -1,
                            });
                        }
                    }
                }

                List<Trip> trips = Pair(arrivals);
                if (trips != null)
                {
                    foreach (Trip trip in trips)
                    {
                        if (trip.Dropped)
                        {
                            continue;
                        }

                        Timing timing = PlayTrip(trip, settings, snapReason, logging, now, batch);
                        if (trip.Arrival.Swap is NameSwap swap && timing != null)
                        {
                            swap.Trips++;
                            swap.LongestMs = Math.Max(swap.LongestMs, timing.DurationMs);
                            swap.FirstName ??= trip.Arrival.Key;
                            swap.FirstTiming ??= timing;
                        }
                    }
                }

                KeepEarlyArrivals(arrivals, trips);
                SettleWaits(now, settings, snapReason, logging, batch, inPass: true);
            }
            catch (Exception ex)
            {
                BrowserBridge.WriteToConsole("Something went wrong, the Magic moves don't play: " + ex);
                foreach (Departure departure in _departures)
                {
                    batch.AddGhostRemoval(departure.Id);
                }
                _departures.Clear();
                _swaps.Clear();
                _earlyArrivals.Clear();
            }

            // What the Magic moves don't play plays like without them.
            foreach (Disappearance disappearance in _disappearing)
            {
                if (disappearance.Magic && !disappearance.Handled)
                {
                    disappearance.Magic = false;
                    disappearance.NoEffect |= disappearance.Effect == null;
                }
            }

            StartMagicTimer(now);
        }

        /// <summary>
        /// The elements that appeared in this pass without a partner wait until the end of the task
        /// for one that disappears.
        /// </summary>
        private void KeepEarlyArrivals(List<Arrival> arrivals, List<Trip> trips)
        {
            foreach (Arrival arrival in arrivals)
            {
                if (arrival.Swap != null || arrival.Early ||
                    (trips != null && trips.Exists(trip => ReferenceEquals(trip.Arrival.Element, arrival.Element))) ||
                    _earlyArrivals.Exists(early => ReferenceEquals(early.Element, arrival.Element)))
                {
                    continue;
                }

                _earlyArrivals.Add(new EarlyArrival { Key = arrival.Key, Element = arrival.Element });
            }

            if (_earlyArrivals.Count > 0 && !_earlyArrivalsExpire)
            {
                _earlyArrivalsExpire = true;
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    _earlyArrivalsExpire = false;
                    _earlyArrivals.RemoveAll(early => !early.Claimed);
                });
            }
        }

        /// <summary>
        /// Pairs each arrival with a departure that has the same id, or the same name in the same
        /// container with MagicMoveByName: the same element first, which moved, then the nearest
        /// one in the tree. A pair inside another one travels with it.
        /// </summary>
        private List<Trip> Pair(List<Arrival> arrivals)
        {
            if (arrivals.Count == 0 || _departures.Count == 0)
            {
                return null;
            }

            var candidates = new List<Candidate>();
            for (int a = 0; a < arrivals.Count; a++)
            {
                Arrival arrival = arrivals[a];
                foreach (Departure departure in _departures)
                {
                    if (!ReferenceEquals(departure.Swap, arrival.Swap) || !string.Equals(departure.Key, arrival.Key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    UIElement ancestor = FindCommonAncestor(arrival.Element, departure, out int hops);
                    candidates.Add(new Candidate
                    {
                        Arrival = a,
                        Departure = departure,
                        Rank = ReferenceEquals(departure.Element, arrival.Element) ? -1 : hops,
                        Ancestor = ancestor,
                    });
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            candidates.Sort((x, y) => x.Rank != y.Rank ? x.Rank.CompareTo(y.Rank) : x.Arrival.CompareTo(y.Arrival));
            var taken = new bool[arrivals.Count];
            var trips = new List<Trip>();
            foreach (Candidate candidate in candidates)
            {
                if (taken[candidate.Arrival] || candidate.Departure.Paired)
                {
                    continue;
                }

                taken[candidate.Arrival] = true;
                candidate.Departure.Paired = true;
                trips.Add(new Trip { Arrival = arrivals[candidate.Arrival], Departure = candidate.Departure, Ancestor = candidate.Ancestor });
            }

            if (trips.Count > 1)
            {
                foreach (Trip trip in trips)
                {
                    foreach (Trip other in trips)
                    {
                        if (!ReferenceEquals(trip, other) && !other.Dropped && trip.Departure.Ancestors.Contains(other.Departure.Element))
                        {
                            trip.Dropped = true;
                            trip.Departure.Paired = false;
                            trip.Departure.Dropped = true;
                            break;
                        }
                    }
                }
            }

            return trips;
        }

        /// <summary>
        /// The nearest element that contains both an element that appeared and where the one that
        /// disappeared was, and how far it is from them in the tree.
        /// </summary>
        private static UIElement FindCommonAncestor(UIElement element, Departure departure, out int hops)
        {
            int up = 0;
            for (DependencyObject e = VisualTreeHelper.GetParent(element); e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is UIElement ancestor)
                {
                    int index = departure.Ancestors.IndexOf(ancestor);
                    if (index >= 0)
                    {
                        hops = up + index;
                        return ancestor;
                    }
                    up++;
                }
            }

            hops = int.MaxValue / 2;
            return null;
        }

        /// <summary>
        /// Makes an element travel from where the other one disappeared, or cross-fades them in
        /// place quickly with reduced motion. Returns the timing, or null when it appears at once.
        /// </summary>
        private Timing PlayTrip(Trip trip, AnimationSettings settings, string snapReason, bool logging, long now, BatchBuilder batch)
        {
            Departure departure = trip.Departure;
            UIElement element = trip.Arrival.Element;
            if (departure.Ghost != null)
            {
                departure.Ghost.Handled = true;
            }
            if (trip.Arrival.Record >= 0)
            {
                _records[trip.Arrival.Record].Flags |= RecordFlags.Magic;
            }

            if (!IsAnimatable(element, out string toId))
            {
                batch.AddGhostRemoval(departure.Id);
                return null;
            }

            bool self = ReferenceEquals(departure.Element, element);
            bool named = trip.Arrival.Swap != null;
            string toName = NameOfMagic(element, null);
            string fromName = NameOfMagic(departure.Element, departure.Parent);
            string described = self
                ? $"{toName} moved, with its Magic move \"{departure.Key}\""
                : $"{toName} appeared as {(fromName == toName ? "another " + fromName : fromName)} disappeared, with the same Magic move \"{departure.Key}\"";

            if (!_effectsPlay)
            {
                batch.AddGhostRemoval(departure.Id);
                if (logging && !named)
                {
                    LogSnap($"{described}; it appears at once, because {snapReason}.", now);
                }
                return null;
            }

            if (_quickFades)
            {
                int quick = batch.AddTiming(QuickFadeTiming);
                if (departure.Ghost != null)
                {
                    batch.AddGhost(departure.Id, quick, 0, FadeLook, sizeCurve: false);
                }
                else
                {
                    batch.AddGhostRemoval(departure.Id);
                }
                batch.AddEffect(toId, quick, 0, FadeLook, raise: false);
                if (logging && !named)
                {
                    _messages.Add($"{described}; {(self ? "it fades in its new place" : "they cross-fade in place")} quickly, because {snapReason}.");
                }
                return QuickFadeTiming;
            }

            // The settings of the element that appears, or else those of the one that disappeared.
            MagicMove own = Animate.GetMagicMove(element);
            MagicMove magicMove = own != null && own.HasParameters ? own
                : departure.Settings != null && departure.Settings.HasParameters ? departure.Settings
                : null;

            Rect from = departure.Rect;
            Rect to = GetWindowRect(element);
            double distance = 0;
            if (!from.IsEmpty && !to.IsEmpty)
            {
                double dx = Math.Max(Math.Abs(from.Left - to.Left), Math.Abs(from.Right - to.Right));
                double dy = Math.Max(Math.Abs(from.Top - to.Top), Math.Abs(from.Bottom - to.Bottom));
                distance = Math.Sqrt(dx * dx + dy * dy);
            }
            Timing timing = Timing.FromMagicMove(magicMove, distance, NearTrip, FarTrip, FarTripFactor, settings);

            MagicMoveTravel travel = magicMove?.Travel ?? MagicMoveTravel.Automatic;
            string ancestorId = null;
            bool behind = (travel == MagicMoveTravel.Behind ||
                           (travel == MagicMoveTravel.Automatic && departure.IsBackground && IsBackground(element, VisualTreeHelper.GetParent(element) as UIElement))) &&
                          trip.Ancestor != null && IsAnimatable(trip.Ancestor, out ancestorId);
            int flags = (departure.Ghost == null ? BatchBuilder.TravelFromCapture : 0) |
                        (departure.IsText ? BatchBuilder.TravelOldText : 0) |
                        (element is TextBlock ? BatchBuilder.TravelNewText : 0);
            batch.AddTravel(departure.Id, toId, batch.AddTiming(timing), behind, ancestorId, flags);
            if (settings.ShowWhatAnimates)
            {
                batch.AddOutline(toId, timing.DurationMs, 0, isOrigin: false);
            }

            if (logging && !named)
            {
                string where = behind ? "behind the elements around it" : "above the page";
                string because = travel == MagicMoveTravel.Behind && !behind ? ", because the two elements are in no container together" : string.Empty;
                _messages.Add($"{described}; it travels {(self ? "to its new place" : "from where that one was")}, {where}{because} ({timing.Description}).");
            }

            return timing;
        }

        #endregion

        #region Waits

        /// <summary>
        /// The elements that disappeared without a pair wait for one, and the containers with
        /// MagicMoveByName for their new content, until the end of their wait; then the elements
        /// play their own Disappear, or disappear at once, and the old contents fade out. Outside a
        /// pass (<paramref name="inPass"/> false), only the waits that ended.
        /// </summary>
        private void SettleWaits(long now, AnimationSettings settings, string snapReason, bool logging, BatchBuilder batch, bool inPass)
        {
            int keptSwaps = 0;
            for (int i = 0; i < _swaps.Count; i++)
            {
                NameSwap swap = _swaps[i];
                if (swap.NewContent != null)
                {
                    FinishSwap(swap, settings, snapReason, logging, now, batch);
                    swap.Done = true;
                }
                else if (swap.Until == 0)
                {
                    if (!inPass)
                    {
                        _swaps[keptSwaps++] = swap;
                    }
                    else if (!_effectsPlay)
                    {
                        swap.Done = true;
                    }
                    else
                    {
                        swap.Until = now + MagicWaitMs;
                        if (swap.Ghost != null)
                        {
                            swap.Ghost.Handled = true;
                        }
                        _swaps[keptSwaps++] = swap;
                    }
                }
                else if (now >= swap.Until)
                {
                    GiveUpSwap(swap, settings, snapReason, logging, batch);
                    swap.Done = true;
                }
                else
                {
                    _swaps[keptSwaps++] = swap;
                }
            }
            _swaps.RemoveRange(keptSwaps, _swaps.Count - keptSwaps);

            int kept = 0;
            for (int i = 0; i < _departures.Count; i++)
            {
                Departure departure = _departures[i];
                bool keep = false;
                if (departure.Paired)
                {
                    // It travels.
                }
                else if (departure.Dropped || (departure.Swap != null && departure.Swap.Done))
                {
                    RemoveCopy(departure, batch);
                }
                else if (departure.Swap != null)
                {
                    keep = true;
                }
                else if (departure.Until == 0)
                {
                    if (!inPass)
                    {
                        // It disappeared after the last pass: the next one pairs it.
                        keep = true;
                    }
                    else if (!_effectsPlay)
                    {
                        // Its ghost plays what it would without its Magic move.
                        if (departure.Ghost == null)
                        {
                            RemoveCopy(departure, batch);
                        }
                    }
                    else
                    {
                        departure.Until = now + MagicWaitMs;
                        if (departure.Ghost != null)
                        {
                            departure.Ghost.Handled = true;
                        }
                        keep = true;
                    }
                }
                else if (now >= departure.Until)
                {
                    GiveUp(departure, settings, snapReason, logging, batch);
                }
                else
                {
                    keep = true;
                }

                if (keep)
                {
                    _departures[kept++] = departure;
                }
            }
            _departures.RemoveRange(kept, _departures.Count - kept);
        }

        private static void RemoveCopy(Departure departure, BatchBuilder batch)
        {
            if (departure.Ghost != null)
            {
                departure.Ghost.Handled = true;
            }
            batch.AddGhostRemoval(departure.Id);
        }

        /// <summary>
        /// No element with its id appeared in time: it plays its own Disappear, or disappears at once.
        /// </summary>
        private void GiveUp(Departure departure, AnimationSettings settings, string snapReason, bool logging, BatchBuilder batch)
        {
            Disappearance ghost = departure.Ghost;
            if (ghost == null)
            {
                batch.AddGhostRemoval(departure.Id);
                return;
            }

            ghost.Handled = true;
            string start = $"{NameOfMagic(departure.Element, ghost.Parent)} disappeared, and no element with its Magic move \"{departure.Key}\" appeared within {Seconds(MagicWaitMs)}";
            if (ghost.Effect != null && _effectsPlay)
            {
                double wait = PlayGhost(ghost, 0, settings, batch);
                if (logging)
                {
                    Timing timing = _quickFades ? QuickFadeTiming : GetEffectTiming(ghost.Effect, settings);
                    string source = DescribeEffectSource(ghost.Element, ghost.Effect, false, ghost.Parent, own: true);
                    _messages.Add($"{start}; {DescribeOwnEffect(ghost.Effect, false, _quickFades, timing, snapReason, wait, source)}.");
                }
            }
            else
            {
                batch.AddGhostRemoval(departure.Id);
                if (logging)
                {
                    _messages.Add($"{start}; it disappears at once.");
                }
            }
        }

        /// <summary>
        /// The new content of a container with MagicMoveByName came: the old content fades out and
        /// the new one fades in, while their elements with the same x:Name travel, unless they
        /// have a Disappear or an Appear of their own.
        /// </summary>
        private void FinishSwap(NameSwap swap, AnimationSettings settings, string snapReason, bool logging, long now, BatchBuilder batch)
        {
            Disappearance ghost = swap.Ghost;
            string host = ElementNames.Of(swap.Host);
            if (!_effectsPlay)
            {
                if (ghost != null)
                {
                    ghost.Handled = true;
                    batch.AddGhostRemoval(ghost.Id);
                }
                if (logging)
                {
                    LogSnap($"The content of {host} changed at once (MagicMoveByName), because {snapReason}.", now);
                }
                return;
            }

            Timing fade = _quickFades ? QuickFadeTiming
                : swap.LongestMs > 0 ? Timing.FromMilliseconds(swap.LongestMs)
                : Timing.FromDuration(null, settings);
            int timingIndex = batch.AddTiming(fade);

            bool fadesOut = false;
            if (ghost != null)
            {
                ghost.Handled = true;
                batch.AddGhost(ghost.Id, timingIndex, 0, FadeLook, sizeCurve: false);
                fadesOut = true;
            }

            bool fadesIn = false;
            UIElement content = swap.NewContent;
            if (GetAppearEffect(content) == null && IsAnimatable(content, out string id))
            {
                batch.AddEffect(id, timingIndex, 0, FadeLook, raise: false);
                if (_recordIndex.TryGetValue(content, out int index))
                {
                    _records[index].Flags |= RecordFlags.Magic;
                }
                fadesIn = true;
            }

            if (!logging)
            {
                return;
            }

            if (_quickFades)
            {
                _messages.Add($"The content of {host} changed (MagicMoveByName); it cross-fades quickly, because {snapReason}.");
                return;
            }

            string travels = swap.Trips == 0
                ? "no element has the same x:Name in both"
                : $"{ElementNames.Count(swap.Trips, "element", "elements")} with the same x:Name in both travel to {(swap.Trips == 1 ? "its new place" : "their new places")}, " +
                  $"like {swap.FirstName} ({swap.FirstTiming.Description})";
            string fades = fadesOut && fadesIn ? "the old content fades out and the new one fades in"
                : fadesOut ? "the old content fades out"
                : fadesIn ? "the new content fades in"
                : null;
            _messages.Add($"The content of {host} changed (MagicMoveByName): {travels}" +
                          (fades != null ? $", while {fades} ({fade.Description})" : string.Empty) + ".");
        }

        /// <summary>
        /// No new content came in time: the old content fades out.
        /// </summary>
        private void GiveUpSwap(NameSwap swap, AnimationSettings settings, string snapReason, bool logging, BatchBuilder batch)
        {
            Disappearance ghost = swap.Ghost;
            if (ghost == null)
            {
                return;
            }

            if (!_effectsPlay)
            {
                batch.AddGhostRemoval(ghost.Id);
                return;
            }

            Timing fade = _quickFades ? QuickFadeTiming : Timing.FromDuration(null, settings);
            batch.AddGhost(ghost.Id, batch.AddTiming(fade), 0, FadeLook, sizeCurve: false);
            if (logging)
            {
                _messages.Add($"The content of {ElementNames.Of(swap.Host)} was removed, and no new content came within {Seconds(MagicWaitMs)} (MagicMoveByName); " +
                              $"it fades out ({fade.Description}).");
            }
        }

        private void StartMagicTimer(long now)
        {
            long next = long.MaxValue;
            foreach (Departure departure in _departures)
            {
                if (departure.Until > 0 && departure.Swap == null)
                {
                    next = Math.Min(next, departure.Until);
                }
            }
            foreach (NameSwap swap in _swaps)
            {
                if (swap.Until > 0)
                {
                    next = Math.Min(next, swap.Until);
                }
            }

            if (next == long.MaxValue)
            {
                return;
            }

            if (_magicTimer == null)
            {
                _magicTimer = new DispatcherTimer();
                _magicTimer.Tick += OnMagicTimerTick;
            }

            _magicTimer.Stop();
            _magicTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, next - now) + 10);
            _magicTimer.Start();
        }

        private void OnMagicTimerTick(object sender, EventArgs e)
        {
            _magicTimer.Stop();
            AnimationSettings settings = AnimationSettings.Current;
            try
            {
                string snapReason = GetGlobalSnapReason(settings);
                _quickFades = IsReducedMotion(snapReason);
                _effectsPlay = snapReason == null || _quickFades;
                var batch = new BatchBuilder();
                SettleWaits(_clock.ElapsedMilliseconds, settings, snapReason, settings.LogForDebugging || AnimationSettings.HasLoggedHandlers, batch, inPass: false);
                if (!batch.IsEmpty)
                {
                    BrowserBridge.Run(batch.ToJson());
                }
            }
            catch (Exception ex)
            {
                BrowserBridge.WriteToConsole("Something went wrong, the Magic moves don't play: " + ex);
                _departures.Clear();
                _swaps.Clear();
            }

            if (_messages.Count > 0)
            {
                Publish(settings);
            }

            StartMagicTimer(_clock.ElapsedMilliseconds);
        }

        #endregion

        /// <summary>
        /// Names an element in a message, and the control whose template it is part of: Pill of
        /// RadioButton "Week".
        /// </summary>
        private static string NameOfMagic(UIElement element, UIElement parent)
        {
            string name = ElementNames.Of(element, parent);
            return element is FrameworkElement fe && fe.TemplatedParent is Control owner ? name + " of " + ElementNames.Of(owner) : name;
        }

        #region Types

        /// <summary>
        /// An element with a Magic move that disappeared, or a named element of an old content
        /// (<see cref="Swap"/>), copied in its parent (<see cref="Ghost"/>) or captured inside its
        /// container, with where it was on screen and the elements it was in.
        /// </summary>
        private sealed class Departure
        {
            public string Key;
            public UIElement Element;
            public string Id;
            public Disappearance Ghost;
            public NameSwap Swap;
            public MagicMove Settings;
            public Rect Rect;
            public List<UIElement> Ancestors;
            public bool IsBackground;
            public bool IsText;
            // When it stops waiting, or 0 before its first pass.
            public long Until;
            public bool Paired;
            // Inside another element that travels: it travels with it.
            public bool Dropped;

            public UIElement Parent => Ancestors.Count > 0 ? Ancestors[0] : null;
        }

        /// <summary>
        /// The content of a container with MagicMoveByName, replaced since the last pass.
        /// </summary>
        private sealed class NameSwap
        {
            public ContentControl Host;
            // Where the content is: the ContentPresenter of the host's template, or the host.
            public UIElement Presenter;
            public UIElement OldContent;
            public UIElement NewContent;
            // The copy of the old content, which fades out, unless it has a Disappear of its own.
            public Disappearance Ghost;
            public long Until;
            public bool Done;

            // The elements that travel, for the fade and the message.
            public int Trips;
            public double LongestMs;
            public string FirstName;
            public Timing FirstTiming;
        }

        private struct Arrival
        {
            public string Key;
            public UIElement Element;
            public NameSwap Swap;
            public int Record;
            // It appeared in an earlier pass of this task (see EarlyArrival).
            public bool Early;
        }

        /// <summary>
        /// An element with a Magic move that appeared without a partner in a pass of this task, and
        /// whether one with its id disappeared since (<see cref="Claimed"/>), which hides it.
        /// </summary>
        private sealed class EarlyArrival
        {
            public string Key;
            public UIElement Element;
            public bool Claimed;
        }

        private struct Candidate
        {
            public int Arrival;
            public Departure Departure;
            public int Rank;
            public UIElement Ancestor;
        }

        private sealed class Trip
        {
            public Arrival Arrival;
            public Departure Departure;
            public UIElement Ancestor;
            public bool Dropped;
        }

        #endregion
    }
}
