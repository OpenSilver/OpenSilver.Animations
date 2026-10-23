using System;
using System.Windows;
using OpenSilver.Animations.Engine;

namespace OpenSilver.Animations
{
    /// <summary>
    /// Makes elements animate when they move or change size.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;Button Content="Save" anim:Animate.MoveAndResize="Smooth" /&gt;
    /// &lt;StackPanel anim:Animate.MoveAndResize="{anim:Smooth ApplyTo=EverythingInside}"&gt;...&lt;/StackPanel&gt;
    /// </code>
    /// </example>
    public static class Animate
    {
        /// <summary>
        /// Identifies the MoveAndResize attached property.
        /// </summary>
        public static readonly DependencyProperty MoveAndResizeProperty =
            DependencyProperty.RegisterAttached(
                "MoveAndResize",
                typeof(Movement),
                typeof(Animate),
                new PropertyMetadata(null, OnMoveAndResizeChanged));

        /// <summary>
        /// Gets how the element animates when it moves or changes size.
        /// </summary>
        public static Movement GetMoveAndResize(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Movement)element.GetValue(MoveAndResizeProperty);
        }

        /// <summary>
        /// Sets how the element animates when it moves or changes size: <see cref="Smooth"/>,
        /// <see cref="Bouncy"/> or <see cref="Movement.None"/>. Everything that moves or changes
        /// size in the same change animates with it, with the same timing.
        /// </summary>
        public static void SetMoveAndResize(UIElement element, Movement value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(MoveAndResizeProperty, value);
        }

        private static void OnMoveAndResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is Movement movement)
            {
                if (movement.ApplyTo == AnimationScope.EverythingInside || movement.IsNone)
                {
                    LayoutAnimator.AnyAreas = true;
                }

                LayoutAnimator.EnsureStarted();
            }
        }

        /// <summary>
        /// Runs <paramref name="changes"/> and animates everything that moves or changes size
        /// because of them, even elements without settings.
        /// </summary>
        /// <param name="changes">The code that changes the page.</param>
        /// <param name="duration">How long the animation lasts. The default is <see cref="AnimationSettings.DefaultDuration"/>.</param>
        /// <example>
        /// <code>Animate.Changes(() => DetailsPanel.Visibility = Visibility.Collapsed);</code>
        /// </example>
        public static void Changes(Action changes, AnimationTime? duration = null)
        {
            if (changes is null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            Run(changes, skip: false, duration);
        }

        /// <summary>
        /// Runs <paramref name="changes"/> without animating anything they cause, even for
        /// elements that have settings.
        /// </summary>
        /// <param name="changes">The code that changes the page.</param>
        public static void Skip(Action changes)
        {
            if (changes is null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            Run(changes, skip: true, null);
        }

        private static void Run(Action changes, bool skip, AnimationTime? duration)
        {
            LayoutAnimator.EnsureStarted();

            // Layout that was already pending belongs to earlier changes.
            UpdateLayout();
            LayoutAnimator.EnterScope(skip, duration);
            try
            {
                changes();
                UpdateLayout();
            }
            finally
            {
                LayoutAnimator.ExitScope();
            }
        }

        private static void UpdateLayout()
        {
            if (Application.Current?.RootVisual is UIElement root)
            {
                root.UpdateLayout();
            }
        }
    }
}
