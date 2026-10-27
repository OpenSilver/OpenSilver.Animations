using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Keeps the elements with OnPageOpen=PushOthers out of the layout while their page or their
    /// container opens, and shows them again after their StartAfter, so that they push the
    /// elements around them aside.
    /// </summary>
    /// <remarks>
    /// The hold is a Storyboard that sets Visibility to Collapsed, applied at once, so that no
    /// layout pass includes the element before it is released. It lasts no time and holds its
    /// value, so it doesn't ask for frames. Stopping it gives the element its own Visibility
    /// back, which is what the app set in the meantime. A Visibility the app sets wins over an
    /// animation until the animation applies its value again, so the hold is applied again when
    /// the app shows the element (<see cref="Reapply"/>). The release waits for the element to be
    /// loaded, for a layout pass after that, and for StartAfter, counted from the end of that pass,
    /// when the effects of the rest of the page start.
    /// </remarks>
    internal sealed class PageOpenHolds
    {
        // Releases that are this close to each other happen together, in the same layout pass.
        private const long TogetherMs = 20;

        private readonly ConditionalWeakTable<UIElement, Hold> _holds = new ConditionalWeakTable<UIElement, Hold>();
        private readonly List<Hold> _waitingForPass = new List<Hold>();
        private readonly List<Hold> _due = new List<Hold>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Action<UIElement> _released;
        private DispatcherTimer _timer;
        private bool _posted;
        private int _count;

        /// <param name="released">
        /// Called for each element shown again after its page opened.
        /// </param>
        public PageOpenHolds(Action<UIElement> released)
        {
            _released = released;
        }

        public bool IsHolding(UIElement element) => _count > 0 && _holds.TryGetValue(element, out _);

        public bool IsWaitingForPass => _waitingForPass.Count > 0;

        /// <summary>
        /// Collapses the element until it is released. An element that is already loaded, like one
        /// inside a container that is shown again, counts as loaded now.
        /// </summary>
        public void Start(FrameworkElement element, double waitMs)
        {
            if (_count > 0 && _holds.TryGetValue(element, out Hold existing))
            {
                // Its container was hidden and shown again before the release: StartAfter counts
                // from this opening.
                if (_due.Remove(existing))
                {
                    existing.WaitMs = waitMs;
                    _waitingForPass.Add(existing);
                }
                return;
            }

            var animation = new ObjectAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.Zero) };
            animation.KeyFrames.Add(new DiscreteObjectKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = Visibility.Collapsed });
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.VisibilityProperty));
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            var hold = new Hold { Element = element, Storyboard = storyboard, WaitMs = waitMs };

            // Registered first: the Visibility change that follows is the hold's, not the app's.
            _holds.Add(element, hold);
            _count++;
            storyboard.Begin();
            storyboard.SeekAlignedToLastTick(TimeSpan.Zero);

            if (element.IsLoaded)
            {
                OnLoaded(hold);
            }
            else
            {
                RoutedEventHandler handler = null;
                handler = (sender, e) =>
                {
                    element.Loaded -= handler;
                    OnLoaded(hold);
                };
                element.Loaded += handler;
            }
        }

        /// <summary>
        /// Collapses again a held element that the app showed, like a menu shown when the window
        /// is wide enough. It still waits for its release.
        /// </summary>
        public void Reapply(UIElement element)
        {
            if (_count > 0 && _holds.TryGetValue(element, out Hold hold))
            {
                hold.Storyboard.Stop();
                hold.Storyboard.Begin();
                hold.Storyboard.SeekAlignedToLastTick(TimeSpan.Zero);
            }
        }

        /// <summary>
        /// Stops holding the element now. It gets back its own Visibility, and appears like any
        /// element if that is Visible.
        /// </summary>
        public void Cancel(UIElement element)
        {
            if (_count > 0 && _holds.TryGetValue(element, out Hold hold))
            {
                hold.PageOpen = false;
                Release(hold);
            }
        }

        private void OnLoaded(Hold hold)
        {
            if (!hold.IsReleased)
            {
                _waitingForPass.Add(hold);
            }
        }

        /// <summary>
        /// Called at the end of a layout pass, while its records still exist. The elements loaded
        /// since the last pass are released after their StartAfter, if they appeared with their
        /// page or container, or now, if they were added on their own.
        /// </summary>
        public void OnLayoutPassCompleted(Func<UIElement, bool> appearsWithContainer)
        {
            if (_waitingForPass.Count == 0)
            {
                return;
            }

            long now = _clock.ElapsedMilliseconds;
            foreach (Hold hold in _waitingForPass)
            {
                if (hold.IsReleased)
                {
                    continue;
                }

                hold.PageOpen = appearsWithContainer(hold.Element);
                hold.Due = hold.PageOpen ? now + (long)hold.WaitMs : now;
                _due.Add(hold);
            }
            _waitingForPass.Clear();

            Schedule();
        }

        private void Schedule()
        {
            if (_due.Count == 0)
            {
                _timer?.Stop();
                return;
            }

            long first = long.MaxValue;
            foreach (Hold hold in _due)
            {
                first = Math.Min(first, hold.Due);
            }

            long wait = first - _clock.ElapsedMilliseconds;
            if (wait <= 0)
            {
                _timer?.Stop();

                // Never during the layout pass: the release changes the layout.
                if (!_posted)
                {
                    _posted = true;
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        _posted = false;
                        ReleaseDue();
                    });
                }
                return;
            }

            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Tick += (sender, e) =>
                {
                    _timer.Stop();
                    ReleaseDue();
                };
            }

            _timer.Stop();
            _timer.Interval = TimeSpan.FromMilliseconds(wait);
            _timer.Start();
        }

        private void ReleaseDue()
        {
            long limit = _clock.ElapsedMilliseconds + TogetherMs;
            for (int i = _due.Count - 1; i >= 0; i--)
            {
                Hold hold = _due[i];
                if (hold.Due <= limit)
                {
                    _due.RemoveAt(i);
                    Release(hold);
                }
            }

            Schedule();
        }

        private void Release(Hold hold)
        {
            if (hold.IsReleased)
            {
                return;
            }

            hold.IsReleased = true;
            FrameworkElement element = hold.Element;
            _holds.Remove(element);
            _count--;
            hold.Storyboard.Stop();

            if (hold.PageOpen && element.IsLoaded && element.Visibility == Visibility.Visible)
            {
                _released(element);
            }
        }

        private sealed class Hold
        {
            public FrameworkElement Element;
            public Storyboard Storyboard;
            public double WaitMs;
            public long Due;
            public bool PageOpen;
            public bool IsReleased;
        }
    }
}
