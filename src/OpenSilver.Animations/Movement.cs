using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Markup;
using System.Windows.Media.Animation;

namespace OpenSilver.Animations
{
    /// <summary>
    /// What an element does when it moves or changes size: <see cref="Smooth"/>, <see cref="Bouncy"/>
    /// or <see cref="None"/>. In XAML, write the name (<c>"Smooth"</c>) or use a markup extension to
    /// set parameters (<c>"{anim:Smooth Duration=0.4s}"</c>).
    /// </summary>
    [TypeConverter(typeof(MovementConverter))]
    public abstract class Movement : MarkupExtension
    {
        private double? _bounciness;
        private double _wave;
        private AnimationScope _applyTo;

        private protected Movement() { }

        /// <summary>
        /// Never animates. Inside an element that uses <see cref="AnimationScope.EverythingInside"/>,
        /// it excludes this element and everything inside it.
        /// </summary>
        public static Movement None { get; } = new NoMovement();

        /// <summary>
        /// How long the animation lasts, from 0.05 to 3 seconds. The default is
        /// <see cref="AnimationSettings.DefaultDuration"/> (twice that for <see cref="Bouncy"/>).
        /// </summary>
        public AnimationTime? Duration { get; set; }

        /// <summary>
        /// How it speeds up and slows down: Smooth (the default), Gentle or Linear, or any
        /// <see cref="IEasingFunction"/>. Used by <see cref="Smooth"/>.
        /// </summary>
        [TypeConverter(typeof(EasingConverter))]
        public IEasingFunction Easing { get; set; }

        /// <summary>
        /// How much it bounces, from 0.1 (a little) to 1 (a lot). The default is 0.5. Used by <see cref="Bouncy"/>.
        /// </summary>
        public double? Bounciness
        {
            get => _bounciness;
            set => _bounciness = value is double d ? Math.Max(0.1, Math.Min(1, d)) : null;
        }

        /// <summary>
        /// From 0 (off, the default) to 1: elements farther from where the change started begin a bit
        /// later, like a wave.
        /// </summary>
        public double Wave
        {
            get => _wave;
            set => _wave = Math.Max(0, Math.Min(1, value));
        }

        /// <summary>
        /// <see cref="AnimationScope.ThisElement"/> (the default) animates the moves and size changes of
        /// this element. <see cref="AnimationScope.EverythingInside"/> also animates every change inside it.
        /// When settings are nested, the nearest one to the element where a change starts gives the
        /// timing; a <see cref="AnimationScope.ThisElement"/> setting above it only counts if its
        /// element moves or changes size in that change.
        /// </summary>
        public AnimationScope ApplyTo
        {
            get => _applyTo;
            set
            {
                _applyTo = value;
                if (value == AnimationScope.EverythingInside)
                {
                    Engine.LayoutAnimator.AnyAreas = true;
                }
            }
        }

        /// <summary>
        /// Also animates when the change comes from resizing the browser window. The default is false,
        /// because following a window edge with a delay can feel laggy.
        /// </summary>
        /// <remarks>
        /// A {Responsive} value that switches when the window, or the element it watches, crosses a
        /// breakpoint animates without it, like a click.
        /// </remarks>
        public bool AlsoOnWindowResize { get; set; }

        internal abstract string PresetName { get; }

        internal bool IsNone => this is NoMovement;

        /// <summary>
        /// Returns this object.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        /// <summary>
        /// Reads a movement name: Smooth, Bouncy or None.
        /// </summary>
        /// <exception cref="FormatException">
        /// The text is not a movement name.
        /// </exception>
        public static Movement Parse(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "smooth":
                    return new Smooth();
                case "bouncy":
                    return new Bouncy();
                case "none":
                case "":
                    return None;
                default:
                    throw new FormatException($"'{text}' is not a way to move. Use Smooth, Bouncy or None.");
            }
        }

        /// <summary>
        /// Reads a movement name: Smooth, Bouncy or None.
        /// </summary>
        public static implicit operator Movement(string text) => Parse(text);

        /// <summary>
        /// Returns the preset name, for example <c>Smooth</c>.
        /// </summary>
        public override string ToString() => PresetName;

        private sealed class NoMovement : Movement
        {
            internal override string PresetName => "None";
        }
    }

    /// <summary>
    /// Moves and resizes smoothly.
    /// </summary>
    public sealed class Smooth : Movement
    {
        internal override string PresetName => "Smooth";
    }

    /// <summary>
    /// Moves with a little bounce at the end, like a spring. Sizes change smoothly, without bouncing.
    /// </summary>
    public sealed class Bouncy : Movement
    {
        internal override string PresetName => "Bouncy";
    }

    /// <summary>
    /// Converts the names Smooth, Bouncy and None to a <see cref="Movement"/>.
    /// </summary>
    public sealed class MovementConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            => value is string s ? Movement.Parse(s) : base.ConvertFrom(context, culture, value);
    }

    /// <summary>
    /// Where <see cref="Animate.MoveAndResizeProperty"/> applies.
    /// </summary>
    public enum AnimationScope
    {
        /// <summary>
        /// Only this element. Everything that moves in the same change animates with it.
        /// </summary>
        ThisElement,

        /// <summary>
        /// This element and every element inside it, at any depth, including templates and items
        /// created later.
        /// </summary>
        EverythingInside,
    }
}
