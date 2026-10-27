using System;
using System.Windows;
using OpenSilver.Animations.Engine;

namespace OpenSilver.Animations
{
    /// <summary>
    /// Makes elements animate when they appear, disappear, move or change size, and when the
    /// mouse is over them or presses them.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;StackPanel x:Name="Details" anim:Animate.Appear="Fade" anim:Animate.Disappear="Fade" /&gt;
    /// &lt;Button Content="Save" anim:Animate.MoveAndResize="Smooth" /&gt;
    /// &lt;StackPanel anim:Animate.MoveAndResize="{anim:Smooth ApplyTo=EverythingInside}"&gt;...&lt;/StackPanel&gt;
    /// &lt;ItemsControl anim:Animate.ItemAppear="Fade" anim:Animate.ItemDisappear="Fade" anim:Animate.ItemMoveAndResize="Smooth" /&gt;
    /// &lt;Button Content="Buy" anim:Animate.Hover="Grow" anim:Animate.Press="Shrink" /&gt;
    /// </code>
    /// </example>
    public static class Animate
    {
        /// <summary>
        /// Identifies the Appear attached property.
        /// </summary>
        public static readonly DependencyProperty AppearProperty =
            DependencyProperty.RegisterAttached(
                "Appear",
                typeof(Transition),
                typeof(Animate),
                new PropertyMetadata(null, OnEffectChanged));

        /// <summary>
        /// Gets what the element does when it appears.
        /// </summary>
        public static Transition GetAppear(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Transition)element.GetValue(AppearProperty);
        }

        /// <summary>
        /// Sets what the element does when it appears: when it is added to the page, or when its
        /// Visibility becomes Visible. The elements around it move smoothly to make room, with the
        /// same duration. When it appears with its page, see <see cref="Transition.OnPageOpen"/>.
        /// </summary>
        public static void SetAppear(UIElement element, Transition value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(AppearProperty, value);
        }

        /// <summary>
        /// Identifies the Disappear attached property.
        /// </summary>
        public static readonly DependencyProperty DisappearProperty =
            DependencyProperty.RegisterAttached(
                "Disappear",
                typeof(Transition),
                typeof(Animate),
                new PropertyMetadata(null, OnEffectChanged));

        /// <summary>
        /// Gets what the element does when it disappears.
        /// </summary>
        public static Transition GetDisappear(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Transition)element.GetValue(DisappearProperty);
        }

        /// <summary>
        /// Sets what the element does when it disappears: when it is removed from the page, or when
        /// its Visibility becomes Collapsed or Hidden. The elements around it move smoothly into its
        /// place, with the same duration.
        /// </summary>
        public static void SetDisappear(UIElement element, Transition value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(DisappearProperty, value);
        }

        private static void OnEffectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is Transition effect && d is UIElement element)
            {
                bool appear = e.Property == AppearProperty;
                LayoutAnimator.AnyEffects = true;
                LayoutAnimator.EnsureStarted();
                LayoutAnimator.CheckEffect(effect, appear, element, items: false);

                // The page opens without it: it is held out of the layout before it is ever shown.
                if (appear && effect.OnPageOpen == PageOpen.PushOthers)
                {
                    LayoutAnimator.HoldForPageOpen(element, effect);
                }
            }
        }

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
        /// Identifies the ItemAppear attached property.
        /// </summary>
        public static readonly DependencyProperty ItemAppearProperty =
            DependencyProperty.RegisterAttached(
                "ItemAppear",
                typeof(Transition),
                typeof(Animate),
                new PropertyMetadata(null, OnItemSettingChanged));

        /// <summary>
        /// Gets what the items of a list do when they appear.
        /// </summary>
        public static Transition GetItemAppear(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Transition)element.GetValue(ItemAppearProperty);
        }

        /// <summary>
        /// Sets what the items of a list do when they appear, as if each item had this
        /// <see cref="SetAppear">Appear</see>. Set it on a Panel, for its children, or on an
        /// ItemsControl, for its items. An item's own Appear wins. When the list appears with its
        /// page, <see cref="Transition.OnPageOpen"/> can be InPlace or Skip.
        /// </summary>
        public static void SetItemAppear(UIElement element, Transition value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ItemAppearProperty, value);
        }

        /// <summary>
        /// Identifies the ItemDisappear attached property.
        /// </summary>
        public static readonly DependencyProperty ItemDisappearProperty =
            DependencyProperty.RegisterAttached(
                "ItemDisappear",
                typeof(Transition),
                typeof(Animate),
                new PropertyMetadata(null, OnItemSettingChanged));

        /// <summary>
        /// Gets what the items of a list do when they disappear.
        /// </summary>
        public static Transition GetItemDisappear(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Transition)element.GetValue(ItemDisappearProperty);
        }

        /// <summary>
        /// Sets what the items of a list do when they disappear, as if each item had this
        /// <see cref="SetDisappear">Disappear</see>. Set it on a Panel, for its children, or on an
        /// ItemsControl, for its items. An item's own Disappear wins.
        /// </summary>
        public static void SetItemDisappear(UIElement element, Transition value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ItemDisappearProperty, value);
        }

        /// <summary>
        /// Identifies the ItemMoveAndResize attached property.
        /// </summary>
        public static readonly DependencyProperty ItemMoveAndResizeProperty =
            DependencyProperty.RegisterAttached(
                "ItemMoveAndResize",
                typeof(Movement),
                typeof(Animate),
                new PropertyMetadata(null, OnItemSettingChanged));

        /// <summary>
        /// Gets how the items of a list animate when they move or change size.
        /// </summary>
        public static Movement GetItemMoveAndResize(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Movement)element.GetValue(ItemMoveAndResizeProperty);
        }

        /// <summary>
        /// Sets how the items of a list animate when they move or change size, as if each item had
        /// this <see cref="SetMoveAndResize">MoveAndResize</see>, including when the list is sorted,
        /// filtered or refreshed: <see cref="Smooth"/>, <see cref="Bouncy"/> or
        /// <see cref="Movement.None"/>. Set it on a Panel, for its children, or on an ItemsControl,
        /// for its items. <see cref="Movement.ApplyTo"/> is not used. An item's own MoveAndResize wins.
        /// </summary>
        public static void SetItemMoveAndResize(UIElement element, Movement value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ItemMoveAndResizeProperty, value);
        }

        /// <summary>
        /// Identifies the ItemCascade attached property.
        /// </summary>
        public static readonly DependencyProperty ItemCascadeProperty =
            DependencyProperty.RegisterAttached(
                "ItemCascade",
                typeof(AnimationTime),
                typeof(Animate),
                new PropertyMetadata(AnimationTime.Zero, OnItemSettingChanged));

        /// <summary>
        /// Gets the time between the items of a list that appear or disappear together.
        /// </summary>
        public static AnimationTime GetItemCascade(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (AnimationTime)element.GetValue(ItemCascadeProperty);
        }

        /// <summary>
        /// Sets the time between the items of a list that appear or disappear together, so that
        /// they play their effects one after the other: from 0 (all together, the default) to
        /// 0.3 s. The whole cascade lasts at most 0.6 s: with many items, they come closer together.
        /// Set it on a Panel or an ItemsControl, with <see cref="SetItemAppear">ItemAppear</see> or
        /// <see cref="SetItemDisappear">ItemDisappear</see>.
        /// </summary>
        public static void SetItemCascade(UIElement element, AnimationTime value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ItemCascadeProperty, value);
        }

        private static void OnItemSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element || e.NewValue is null)
            {
                return;
            }

            LayoutAnimator.AnyItems = true;
            if (e.NewValue is Transition)
            {
                LayoutAnimator.AnyEffects = true;
            }
            else if (e.NewValue is Movement movement && movement.IsNone)
            {
                LayoutAnimator.AnyAreas = true;
            }

            LayoutAnimator.EnsureStarted();
            LayoutAnimator.CheckItemSetting(e.Property, e.NewValue, element);
        }

        /// <summary>
        /// Identifies the Hover attached property.
        /// </summary>
        public static readonly DependencyProperty HoverProperty =
            DependencyProperty.RegisterAttached(
                "Hover",
                typeof(Reaction),
                typeof(Animate),
                new PropertyMetadata(null, OnReactionChanged));

        /// <summary>
        /// Gets what the element does when the mouse is over it.
        /// </summary>
        public static Reaction GetHover(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Reaction)element.GetValue(HoverProperty);
        }

        /// <summary>
        /// Sets what the element does when the mouse is over it: <see cref="Grow"/>,
        /// <see cref="Lift"/> or <see cref="Shrink"/> until the mouse leaves, or <see cref="Nudge"/>
        /// once, when the mouse comes in. Nothing happens on a touch screen, which has no mouse, or
        /// while the element is disabled.
        /// </summary>
        public static void SetHover(UIElement element, Reaction value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(HoverProperty, value);
        }

        /// <summary>
        /// Identifies the Press attached property.
        /// </summary>
        public static readonly DependencyProperty PressProperty =
            DependencyProperty.RegisterAttached(
                "Press",
                typeof(Reaction),
                typeof(Animate),
                new PropertyMetadata(null, OnReactionChanged));

        /// <summary>
        /// Gets what the element does while it is pressed.
        /// </summary>
        public static Reaction GetPress(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Reaction)element.GetValue(PressProperty);
        }

        /// <summary>
        /// Sets what the element does while it is pressed, with the mouse, a finger or a pen:
        /// <see cref="Shrink"/>, <see cref="Grow"/> or <see cref="Lift"/>, until it is released.
        /// Nothing happens while the element is disabled. <see cref="Nudge"/> is for Hover only.
        /// </summary>
        public static void SetPress(UIElement element, Reaction value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(PressProperty, value);
        }

        private static void OnReactionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                Reactions.OnChanged(element, e.NewValue as Reaction, press: e.Property == PressProperty);
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
