/*===================================================================================
* 
*   Copyright (c) Userware (OpenSilver.net/XAML.io)
*      
*   This file is part of the OpenSilver.Animations library by Userware
*   (https://github.com/OpenSilver/OpenSilver.Animations),
*   which is licensed under the MIT license: https://opensource.org/licenses/MIT
*   
*   As stated in the MIT license, "the above copyright notice and this permission
*   notice shall be included in all copies or substantial portions of the Software."
*  
\*====================================================================================*/

using System;
using System.Windows;
using OpenSilver.Animations.Engine;

namespace OpenSilver.Animations
{
    /// <summary>
    /// Makes elements animate when they appear, disappear, move or change size, when the mouse is
    /// over them or presses them, when the text of a TextBlock changes, and when an element travels
    /// to another one (Magic move).
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;StackPanel x:Name="Details" anim:Animate.Appear="Fade" anim:Animate.Disappear="Fade" /&gt;
    /// &lt;Button Content="Save" anim:Animate.MoveAndResize="Smooth" /&gt;
    /// &lt;TextBlock Text="{Binding Total}" anim:Animate.TextChange="CountUp" /&gt;
    /// &lt;StackPanel anim:Animate.MoveAndResize="{anim:Smooth ApplyTo=EverythingInside}"&gt;...&lt;/StackPanel&gt;
    /// &lt;ItemsControl anim:Animate.ItemsAppear="Fade" anim:Animate.ItemsDisappear="Fade" anim:Animate.ItemsMoveAndResize="Smooth" /&gt;
    /// &lt;Button Content="Buy" anim:Animate.Hover="Grow" anim:Animate.Press="Shrink" /&gt;
    /// &lt;Border anim:Animate.MagicMove="SelectionPill" /&gt;
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
            if (d is not UIElement element)
            {
                return;
            }

            bool appear = e.Property == AppearProperty;
            if (e.NewValue is Transition effect)
            {
                LayoutAnimator.AnyEffects = true;
                LayoutAnimator.EnsureStarted();
                LayoutAnimator.CheckEffect(effect, appear, element, items: false);

                // The page opens without it: it is held out of the layout before it is ever shown.
                if (appear && effect.OnPageOpen == PageOpen.PushOthers)
                {
                    LayoutAnimator.HoldForPageOpen(element, effect);
                    return;
                }
            }

            // Its new Appear doesn't push the others: it opens with its page, like any element.
            if (appear && e.OldValue is Transition old && old.OnPageOpen == PageOpen.PushOthers)
            {
                LayoutAnimator.CancelHold(element);
            }
        }

        /// <summary>
        /// Identifies the TextChange attached property.
        /// </summary>
        public static readonly DependencyProperty TextChangeProperty =
            DependencyProperty.RegisterAttached(
                "TextChange",
                typeof(TextTransition),
                typeof(Animate),
                new PropertyMetadata(null, OnTextChangeChanged));

        /// <summary>
        /// Gets what the TextBlock does when its text changes.
        /// </summary>
        public static TextTransition GetTextChange(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (TextTransition)element.GetValue(TextChangeProperty);
        }

        /// <summary>
        /// Sets what a TextBlock does when its text changes, whether you set Text in code or a
        /// binding changes it: <see cref="Roll"/>, <see cref="CrossFade"/>, <see cref="Scramble"/>,
        /// <see cref="Retype"/>, <see cref="CountUp"/> or <see cref="TextTransition.None"/>. When
        /// the new text has another size, add <see cref="SetMoveAndResize">MoveAndResize</see> to
        /// the TextBlock, so that it and the elements around it change size smoothly.
        /// </summary>
        /// <example>
        /// <code>&lt;TextBlock Text="{Binding UnreadCount}" anim:Animate.TextChange="Roll" anim:Animate.MoveAndResize="Smooth" /&gt;</code>
        /// </example>
        public static void SetTextChange(UIElement element, TextTransition value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(TextChangeProperty, value);
        }

        private static void OnTextChangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && e.NewValue is TextTransition effect)
            {
                LayoutAnimator.EnsureStarted();
                LayoutAnimator.CheckTextChange(effect, element);
                TextChanges.Watch(element);
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
        /// Identifies the ItemsAppear attached property.
        /// </summary>
        public static readonly DependencyProperty ItemsAppearProperty =
            DependencyProperty.RegisterAttached(
                "ItemsAppear",
                typeof(Transition),
                typeof(Animate),
                new PropertyMetadata(null, OnItemSettingChanged));

        /// <summary>
        /// Gets what the items of a list do when they appear.
        /// </summary>
        public static Transition GetItemsAppear(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Transition)element.GetValue(ItemsAppearProperty);
        }

        /// <summary>
        /// Sets what the items of a list do when they appear, as if each item had this
        /// <see cref="SetAppear">Appear</see>. Set it on a Panel, for its children, or on an
        /// ItemsControl, for its items. An item's own Appear wins. When the list appears with its
        /// page, <see cref="Transition.OnPageOpen"/> can be InPlace or Skip.
        /// </summary>
        public static void SetItemsAppear(UIElement element, Transition value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ItemsAppearProperty, value);
        }

        /// <summary>
        /// Identifies the ItemsDisappear attached property.
        /// </summary>
        public static readonly DependencyProperty ItemsDisappearProperty =
            DependencyProperty.RegisterAttached(
                "ItemsDisappear",
                typeof(Transition),
                typeof(Animate),
                new PropertyMetadata(null, OnItemSettingChanged));

        /// <summary>
        /// Gets what the items of a list do when they disappear.
        /// </summary>
        public static Transition GetItemsDisappear(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Transition)element.GetValue(ItemsDisappearProperty);
        }

        /// <summary>
        /// Sets what the items of a list do when they disappear, as if each item had this
        /// <see cref="SetDisappear">Disappear</see>. Set it on a Panel, for its children, or on an
        /// ItemsControl, for its items. An item's own Disappear wins.
        /// </summary>
        public static void SetItemsDisappear(UIElement element, Transition value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ItemsDisappearProperty, value);
        }

        /// <summary>
        /// Identifies the ItemsMoveAndResize attached property.
        /// </summary>
        public static readonly DependencyProperty ItemsMoveAndResizeProperty =
            DependencyProperty.RegisterAttached(
                "ItemsMoveAndResize",
                typeof(Movement),
                typeof(Animate),
                new PropertyMetadata(null, OnItemSettingChanged));

        /// <summary>
        /// Gets how the items of a list animate when they move or change size.
        /// </summary>
        public static Movement GetItemsMoveAndResize(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (Movement)element.GetValue(ItemsMoveAndResizeProperty);
        }

        /// <summary>
        /// Sets how the items of a list animate when they move or change size, as if each item had
        /// this <see cref="SetMoveAndResize">MoveAndResize</see>, including when the list is sorted,
        /// filtered or refreshed: <see cref="Smooth"/>, <see cref="Bouncy"/> or
        /// <see cref="Movement.None"/>. Set it on a Panel, for its children, or on an ItemsControl,
        /// for its items. <see cref="Movement.ApplyTo"/> is not used. An item's own MoveAndResize wins.
        /// </summary>
        public static void SetItemsMoveAndResize(UIElement element, Movement value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ItemsMoveAndResizeProperty, value);
        }

        /// <summary>
        /// Identifies the ItemsCascade attached property.
        /// </summary>
        public static readonly DependencyProperty ItemsCascadeProperty =
            DependencyProperty.RegisterAttached(
                "ItemsCascade",
                typeof(AnimationTime),
                typeof(Animate),
                new PropertyMetadata(AnimationTime.Zero, OnItemSettingChanged));

        /// <summary>
        /// Gets the time between the items of a list that appear or disappear together.
        /// </summary>
        public static AnimationTime GetItemsCascade(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (AnimationTime)element.GetValue(ItemsCascadeProperty);
        }

        /// <summary>
        /// Sets the time between the items of a list that appear or disappear together, so that
        /// they play their effects one after the other: from 0 (all together, the default) to
        /// 0.3 s. The whole cascade lasts at most 0.6 s: with many items, they come closer together.
        /// Set it on a Panel or an ItemsControl, with <see cref="SetItemsAppear">ItemsAppear</see> or
        /// <see cref="SetItemsDisappear">ItemsDisappear</see>.
        /// </summary>
        public static void SetItemsCascade(UIElement element, AnimationTime value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ItemsCascadeProperty, value);
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
        /// Identifies the MagicMove attached property.
        /// </summary>
        public static readonly DependencyProperty MagicMoveProperty =
            DependencyProperty.RegisterAttached(
                "MagicMove",
                typeof(MagicMove),
                typeof(Animate),
                new PropertyMetadata(null, OnMagicMoveChanged));

        /// <summary>
        /// Gets the id that links the element to the other elements it travels to and from.
        /// </summary>
        public static MagicMove GetMagicMove(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (MagicMove)element.GetValue(MagicMoveProperty);
        }

        /// <summary>
        /// Gives the element an id, so that it travels to another element with the same id: when
        /// one of them disappears and the other one appears, the old one moves and changes size
        /// into the new one, as if it were the same element going to its new place. Put it, for
        /// example, on the selection background in the template of a RadioButton, which is only
        /// visible when the button is checked, or on a photo in a list and on the same photo in its
        /// detail view. The elements are hidden with Visibility; a template whose states use
        /// Opacity instead needs to use Visibility. Its Appear and Disappear only play when it
        /// doesn't travel.
        /// </summary>
        /// <example>
        /// <code>
        /// &lt;Border anim:Animate.MagicMove="SelectionPill" /&gt;
        /// &lt;Image anim:Animate.MagicMove="{Binding Id, StringFormat='photo-{0}'}" /&gt;
        /// </code>
        /// </example>
        public static void SetMagicMove(UIElement element, MagicMove value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(MagicMoveProperty, value);
        }

        private static void OnMagicMoveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && e.NewValue is MagicMove magicMove)
            {
                LayoutAnimator.AnyMagic = true;
                LayoutAnimator.EnsureStarted();
                LayoutAnimator.OnMagicMoveSet(element, magicMove);
            }
        }

        /// <summary>
        /// Identifies the MagicMoveByName attached property.
        /// </summary>
        public static readonly DependencyProperty MagicMoveByNameProperty =
            DependencyProperty.RegisterAttached(
                "MagicMoveByName",
                typeof(bool),
                typeof(Animate),
                new PropertyMetadata(false, OnMagicMoveByNameChanged));

        /// <summary>
        /// Gets whether the elements with the same name travel when the content changes.
        /// </summary>
        public static bool GetMagicMoveByName(UIElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (bool)element.GetValue(MagicMoveByNameProperty);
        }

        /// <summary>
        /// Set it to true on a Frame or a ContentControl: when its content changes, like the pages
        /// of a wizard, each element of the old content travels to the element with the same
        /// x:Name in the new content, like <see cref="SetMagicMove">MagicMove</see>. Meanwhile, the
        /// old content fades out and the new one fades in, unless they have a Disappear or an
        /// Appear of their own.
        /// </summary>
        /// <example>
        /// <code>&lt;ContentControl x:Name="WizardHost" anim:Animate.MagicMoveByName="True" /&gt;</code>
        /// </example>
        public static void SetMagicMoveByName(UIElement element, bool value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(MagicMoveByNameProperty, value);
        }

        private static void OnMagicMoveByNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && (bool)e.NewValue)
            {
                LayoutAnimator.AnyMagic = true;
                LayoutAnimator.EnsureStarted();
                LayoutAnimator.CheckMagicMoveByName(element);
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
