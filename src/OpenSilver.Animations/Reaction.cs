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
using System.ComponentModel;
using System.Globalization;
using System.Windows.Markup;
using System.Windows.Media.Animation;

namespace OpenSilver.Animations
{
    /// <summary>
    /// What an element does in reaction to the mouse, while the mouse is over it
    /// (<see cref="Animate.SetHover">Hover</see>) or while it is pressed
    /// (<see cref="Animate.SetPress">Press</see>): <see cref="Grow"/>, <see cref="Lift"/>,
    /// <see cref="Shrink"/>, <see cref="Nudge"/> or <see cref="None"/>. In XAML, write the name
    /// (<c>"Grow"</c>) or use a markup extension to set parameters (<c>"{anim:Grow Zoom=1.1}"</c>).
    /// </summary>
    [TypeConverter(typeof(ReactionConverter))]
    public abstract class Reaction : MarkupExtension
    {
        private double? _bounciness;

        private protected Reaction() { }

        /// <summary>
        /// Nothing happens, for example to turn off a reaction that a Style sets.
        /// </summary>
        public static Reaction None { get; } = new NoReaction();

        /// <summary>
        /// How long it takes to change, and to come back, from 0.05 to 3 seconds. The default is
        /// <see cref="AnimationSettings.DefaultDuration"/> (twice that with <c>Easing=Bouncy</c>),
        /// and 0.4 seconds for <see cref="Nudge"/>.
        /// </summary>
        public AnimationTime? Duration { get; set; }

        /// <summary>
        /// How it speeds up and slows down: Smooth (the default), Gentle, Linear, Bouncy, EaseOut,
        /// EaseIn, EaseInOut or Overshoot, or any <see cref="IEasingFunction"/>. Not used by
        /// <see cref="Nudge"/>, which always springs back.
        /// </summary>
        [TypeConverter(typeof(EasingConverter))]
        public IEasingFunction Easing { get; set; }

        /// <summary>
        /// How much it bounces, from 0.1 (a little) to 1 (a lot). The default is 0.5. Used by
        /// <see cref="Nudge"/>, by every reaction with <c>Easing=Bouncy</c>, and by
        /// <c>Easing=Overshoot</c>, where it says how far it goes past.
        /// </summary>
        public double? Bounciness
        {
            get => _bounciness;
            set => _bounciness = value is double d ? Math.Max(0.1, Math.Min(1, d)) : null;
        }

        internal abstract string PresetName { get; }

        internal bool IsNone => this is NoReaction;

        internal bool IsBouncy => Easing is SpringEasing;

        /// <summary>
        /// Returns this object.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        /// <summary>
        /// Reads a reaction name: Grow, Lift, Shrink, Nudge or None.
        /// </summary>
        /// <exception cref="FormatException">
        /// The text is not a reaction name.
        /// </exception>
        public static Reaction Parse(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "grow":
                    return new Grow();
                case "lift":
                    return new Lift();
                case "shrink":
                    return new Shrink();
                case "nudge":
                    return new Nudge();
                case "none":
                case "":
                    return None;
                default:
                    throw new FormatException($"'{text}' is not a reaction. Use Grow, Lift, Shrink, Nudge or None.");
            }
        }

        /// <summary>
        /// Reads a reaction name: Grow, Lift, Shrink, Nudge or None.
        /// </summary>
        public static implicit operator Reaction(string text) => Parse(text);

        /// <summary>
        /// Returns the preset name, for example <c>Grow</c>.
        /// </summary>
        public override string ToString() => PresetName;

        private protected static double ZoomOf(double value, double fallback)
            => double.IsNaN(value) ? fallback : Math.Max(0.5, Math.Min(2, value));

        private sealed class NoReaction : Reaction
        {
            internal override string PresetName => "None";
        }
    }

    /// <summary>
    /// Grows a little around its center, and comes back to its size afterwards.
    /// </summary>
    public sealed class Grow : Reaction
    {
        private double _zoom = 1.05;

        /// <summary>
        /// Its size while it reacts, from 0.5 to 2 (1 is its normal size). The default is 1.05.
        /// </summary>
        public double Zoom
        {
            get => _zoom;
            set => _zoom = ZoomOf(value, 1.05);
        }

        internal override string PresetName => "Grow";
    }

    /// <summary>
    /// Rises a little, and comes back down afterwards.
    /// </summary>
    public sealed class Lift : Reaction
    {
        internal const double DefaultPixels = 4;

        /// <summary>
        /// How far it rises: a number of pixels, 4 by default, or Short (10 pixels).
        /// </summary>
        public Distance Distance { get; set; } = new Distance(DefaultPixels);

        internal override string PresetName => "Lift";
    }

    /// <summary>
    /// Shrinks a little around its center, like a button pushed in, and comes back to its size
    /// afterwards.
    /// </summary>
    public sealed class Shrink : Reaction
    {
        private double _zoom = 0.96;

        /// <summary>
        /// Its size while it reacts, from 0.5 to 2 (1 is its normal size). The default is 0.96.
        /// </summary>
        public double Zoom
        {
            get => _zoom;
            set => _zoom = ZoomOf(value, 0.96);
        }

        internal override string PresetName => "Shrink";
    }

    /// <summary>
    /// When the mouse comes over it, it is pushed a little in the direction the mouse moves,
    /// turns a little, and springs back. The faster the mouse, the more it moves. For Hover only.
    /// </summary>
    public sealed class Nudge : Reaction
    {
        internal const double DefaultPixels = 3;

        private double _tilt = 2;

        /// <summary>
        /// How far it is pushed when the mouse comes fast: a number of pixels, 3 by default, from 0 to 20.
        /// </summary>
        public Distance Distance { get; set; } = new Distance(DefaultPixels);

        /// <summary>
        /// How much it turns when the mouse comes fast near a corner, in degrees: from 0 to 15,
        /// 2 by default.
        /// </summary>
        public double Tilt
        {
            get => _tilt;
            set => _tilt = double.IsNaN(value) ? 2 : Math.Max(0, Math.Min(15, value));
        }

        internal override string PresetName => "Nudge";
    }

    /// <summary>
    /// Converts the names Grow, Lift, Shrink, Nudge and None to a <see cref="Reaction"/>.
    /// </summary>
    public sealed class ReactionConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            => value is string s ? Reaction.Parse(s) : base.ConvertFrom(context, culture, value);
    }
}
