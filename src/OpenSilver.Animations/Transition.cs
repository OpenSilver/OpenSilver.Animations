using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Markup;
using System.Windows.Media.Animation;

namespace OpenSilver.Animations
{
    /// <summary>
    /// What an element does when it appears or disappears: <see cref="Fade"/>, <see cref="Slide"/>,
    /// <see cref="Zoom"/>, <see cref="Pop"/>, <see cref="Wipe"/>, <see cref="Blur"/>, <see cref="Custom"/>
    /// or <see cref="None"/>. In XAML, write the name (<c>"Fade"</c>) or use a markup extension to set
    /// parameters (<c>"{anim:Slide From=Top, Duration=0.4s}"</c>).
    /// </summary>
    [TypeConverter(typeof(TransitionConverter))]
    public abstract class Transition : MarkupExtension
    {
        private static readonly AnimationTime MaxStartAfter = AnimationTime.FromSeconds(10);

        private double? _bounciness;
        private AnimationTime _startAfter;

        private protected Transition() { }

        /// <summary>
        /// No effect: the element appears or disappears at once. Inside an element that uses
        /// <see cref="AnimationScope.EverythingInside"/>, it turns off the automatic wipe or fade.
        /// </summary>
        public static Transition None { get; } = new NoTransition();

        /// <summary>
        /// How long the effect lasts, from 0.05 to 3 seconds. The elements around it that move to make
        /// room or to fill the space take the same time. The default is
        /// <see cref="AnimationSettings.DefaultDuration"/> (twice that for <see cref="Pop"/> and
        /// <c>Easing=Bouncy</c>).
        /// </summary>
        public AnimationTime? Duration { get; set; }

        /// <summary>
        /// How it speeds up and slows down: Smooth (the default), Gentle, Linear or Bouncy, or any
        /// <see cref="IEasingFunction"/>. With Bouncy, the elements around it still move smoothly.
        /// </summary>
        [TypeConverter(typeof(EasingConverter))]
        public IEasingFunction Easing { get; set; }

        /// <summary>
        /// How much it bounces, from 0.1 (a little) to 1 (a lot). The default is 0.5. Used by
        /// <see cref="Pop"/>, and by every effect with <c>Easing=Bouncy</c>.
        /// </summary>
        public double? Bounciness
        {
            get => _bounciness;
            set => _bounciness = value is double d ? Math.Max(0.1, Math.Min(1, d)) : null;
        }

        /// <summary>
        /// When the elements around it move: <see cref="Animations.OthersMove.While"/> it appears or
        /// disappears (the default), <see cref="Animations.OthersMove.Before"/> it appears, or
        /// <see cref="Animations.OthersMove.After"/> it disappears.
        /// </summary>
        public OthersMove OthersMove { get; set; }

        /// <summary>
        /// How long it waits before its effect starts, from 0 (the default) to 10 seconds. It stays
        /// invisible until then. When it appears at runtime, the elements around it make room at
        /// once; when it appears with its page, the time counts from when the page opens, which
        /// makes elements appear one after the other.
        /// </summary>
        public AnimationTime StartAfter
        {
            get => _startAfter;
            set => _startAfter = value.Value > MaxStartAfter.Value ? MaxStartAfter : value;
        }

        /// <summary>
        /// What it does when it appears together with its page or its container, like a page that
        /// opens, a dialog, or a card that appears: <see cref="PageOpen.InPlace"/> (the default),
        /// <see cref="PageOpen.PushOthers"/> or <see cref="PageOpen.Skip"/>. For Appear only.
        /// </summary>
        public PageOpen OnPageOpen { get; set; }

        internal abstract string PresetName { get; }

        internal bool IsNone => this is NoTransition;

        internal bool IsBouncy => this is Pop || Easing is SpringEasing;

        internal virtual bool HasFrom => false;

        internal virtual bool HasTo => false;

        /// <summary>
        /// The look the element starts from when it appears, or ends with when it disappears.
        /// </summary>
        internal abstract EffectLook GetLook(bool appear);

        /// <summary>
        /// Says what the element does, for the log: "fades in", "slides out".
        /// </summary>
        internal abstract string Describe(bool appear);

        /// <summary>
        /// Returns this object.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        /// <summary>
        /// Reads an effect name: Fade, Slide, Zoom, Pop, Wipe, Blur, Custom or None.
        /// </summary>
        /// <exception cref="FormatException">
        /// The text is not an effect name.
        /// </exception>
        public static Transition Parse(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "fade":
                    return new Fade();
                case "slide":
                    return new Slide();
                case "zoom":
                    return new Zoom();
                case "pop":
                    return new Pop();
                case "wipe":
                    return new Wipe();
                case "blur":
                    return new Blur();
                case "custom":
                    return new Custom();
                case "none":
                case "":
                    return None;
                default:
                    throw new FormatException($"'{text}' is not an effect. Use Fade, Slide, Zoom, Pop, Wipe, Blur, Custom or None.");
            }
        }

        /// <summary>
        /// Reads an effect name: Fade, Slide, Zoom, Pop, Wipe, Blur, Custom or None.
        /// </summary>
        public static implicit operator Transition(string text) => Parse(text);

        /// <summary>
        /// Returns the preset name, for example <c>Fade</c>.
        /// </summary>
        public override string ToString() => PresetName;

        private protected static double DistanceOf(Side side, double distance, out double y)
        {
            y = side == Side.Bottom ? distance : side == Side.Top ? -distance : 0;
            return side == Side.Right ? distance : side == Side.Left ? -distance : 0;
        }

        private protected static int WipeEdgeOf(Side side) => side switch
        {
            Side.Top => EffectLook.FromTop,
            Side.Left => EffectLook.FromLeft,
            Side.Right => EffectLook.FromRight,
            _ => EffectLook.FromBottom,
        };

        private sealed class NoTransition : Transition
        {
            internal override string PresetName => "None";

            internal override EffectLook GetLook(bool appear) => EffectLook.Identity;

            internal override string Describe(bool appear) => appear ? "appears at once" : "disappears at once";
        }
    }

    /// <summary>
    /// Fades in or out.
    /// </summary>
    public sealed class Fade : Transition
    {
        internal override string PresetName => "Fade";

        internal override EffectLook GetLook(bool appear) => new EffectLook { Opacity = 0 };

        internal override string Describe(bool appear) => appear ? "fades in" : "fades out";
    }

    /// <summary>
    /// Fades in while sliding in from a side, or fades out while sliding out to a side.
    /// </summary>
    public sealed class Slide : Transition
    {
        private Side? _from;
        private Side? _to;

        /// <summary>
        /// The side it comes from when it appears: Top, Bottom (the default), Left or Right.
        /// </summary>
        public Side From
        {
            get => _from ?? Side.Bottom;
            set => _from = value;
        }

        /// <summary>
        /// The side it goes to when it disappears: Top, Bottom (the default), Left or Right.
        /// </summary>
        public Side To
        {
            get => _to ?? Side.Bottom;
            set => _to = value;
        }

        internal override bool HasFrom => _from.HasValue;

        internal override bool HasTo => _to.HasValue;

        /// <summary>
        /// How far it slides: Short (10 pixels), Medium (30 pixels, the default), Long (100 pixels),
        /// or a number of pixels.
        /// </summary>
        public Distance Distance { get; set; } = Distance.Medium;

        internal override string PresetName => "Slide";

        internal override EffectLook GetLook(bool appear)
        {
            double x = DistanceOf(appear ? From : To, Distance.Pixels, out double y);
            return new EffectLook { Opacity = 0, X = x, Y = y };
        }

        internal override string Describe(bool appear) => appear ? "slides in" : "slides out";
    }

    /// <summary>
    /// Fades in while growing from 90% of its size, or fades out while shrinking to 90%.
    /// </summary>
    public sealed class Zoom : Transition
    {
        internal override string PresetName => "Zoom";

        internal override EffectLook GetLook(bool appear) => new EffectLook { Opacity = 0, Scale = 0.9 };

        internal override string Describe(bool appear) => appear ? "zooms in" : "zooms out";
    }

    /// <summary>
    /// Grows from half its size with a bounce. For Appear only: as a Disappear, it plays like <see cref="Zoom"/>.
    /// </summary>
    public sealed class Pop : Transition
    {
        internal override string PresetName => "Pop";

        internal override EffectLook GetLook(bool appear)
            => appear ? new EffectLook { Opacity = 0, Scale = 0.5 } : new EffectLook { Opacity = 0, Scale = 0.9 };

        internal override string Describe(bool appear) => appear ? "pops in" : "zooms out";
    }

    /// <summary>
    /// Reveals the element from one of its edges, or hides it toward one of its edges, without fading.
    /// </summary>
    public sealed class Wipe : Transition
    {
        private Side? _from;
        private Side? _to;

        /// <summary>
        /// The edge it is revealed from when it appears: Top, Bottom (the default), Left or Right.
        /// </summary>
        public Side From
        {
            get => _from ?? Side.Bottom;
            set => _from = value;
        }

        /// <summary>
        /// The edge it is hidden toward when it disappears: Top, Bottom (the default), Left or Right.
        /// </summary>
        public Side To
        {
            get => _to ?? Side.Bottom;
            set => _to = value;
        }

        internal override bool HasFrom => _from.HasValue;

        internal override bool HasTo => _to.HasValue;

        internal override string PresetName => "Wipe";

        internal override EffectLook GetLook(bool appear) => new EffectLook { Wipe = WipeEdgeOf(appear ? From : To) };

        internal override string Describe(bool appear) => appear ? "wipes in" : "wipes out";
    }

    /// <summary>
    /// Fades in while coming into focus from an 8-pixel blur, or fades out into a blur.
    /// </summary>
    public sealed class Blur : Transition
    {
        internal override string PresetName => "Blur";

        internal override EffectLook GetLook(bool appear) => new EffectLook { Opacity = 0, Blur = 8 };

        internal override string Describe(bool appear) => appear ? "fades in from a blur" : "fades out into a blur";
    }

    /// <summary>
    /// An effect of your own: the look it starts from when it appears, or ends with when it disappears.
    /// </summary>
    /// <example>
    /// <code>&lt;Border anim:Animate.Appear="{anim:Custom Opacity=0, OffsetY=20, Zoom=0.8, Rotate=-10}" /&gt;</code>
    /// </example>
    public sealed class Custom : Transition
    {
        private double _opacity;
        private double _zoom = 1;
        private double _blur;

        /// <summary>
        /// From 0 (invisible, the default) to 1 (fully visible).
        /// </summary>
        public double Opacity
        {
            get => _opacity;
            set => _opacity = double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
        }

        /// <summary>
        /// How far it is moved to the right, in pixels (negative to the left). The default is 0.
        /// </summary>
        public double OffsetX { get; set; }

        /// <summary>
        /// How far it is moved down, in pixels (negative up). The default is 0.
        /// </summary>
        public double OffsetY { get; set; }

        /// <summary>
        /// Its size, from 0 to 3 (1, the default, is its normal size), around its center.
        /// </summary>
        public double Zoom
        {
            get => _zoom;
            set => _zoom = double.IsNaN(value) ? 1 : Math.Max(0, Math.Min(3, value));
        }

        /// <summary>
        /// How much it is turned around its center, in degrees, clockwise. The default is 0.
        /// </summary>
        public double Rotate { get; set; }

        /// <summary>
        /// How blurred it is, in pixels. The default is 0.
        /// </summary>
        public double Blur
        {
            get => _blur;
            set => _blur = double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(100, value));
        }

        internal override string PresetName => "Custom";

        internal override EffectLook GetLook(bool appear) => new EffectLook
        {
            Opacity = Opacity,
            X = double.IsNaN(OffsetX) ? 0 : OffsetX,
            Y = double.IsNaN(OffsetY) ? 0 : OffsetY,
            Scale = Zoom,
            Rotate = double.IsNaN(Rotate) ? 0 : Rotate,
            Blur = Blur,
        };

        internal override string Describe(bool appear) => appear ? "plays its Custom effect in" : "plays its Custom effect out";
    }

    /// <summary>
    /// A side or an edge of an element.
    /// </summary>
    public enum Side
    {
        /// <summary>
        /// The bottom.
        /// </summary>
        Bottom,

        /// <summary>
        /// The top.
        /// </summary>
        Top,

        /// <summary>
        /// The left.
        /// </summary>
        Left,

        /// <summary>
        /// The right.
        /// </summary>
        Right,
    }

    /// <summary>
    /// When the elements around an element that appears or disappears move to make room or to fill
    /// the space.
    /// </summary>
    public enum OthersMove
    {
        /// <summary>
        /// While it appears or disappears. This is the default.
        /// </summary>
        While,

        /// <summary>
        /// Before it appears: they make room first, and it appears in the empty space. For Appear only.
        /// </summary>
        Before,

        /// <summary>
        /// After it disappears: it disappears first, then they move into its place. For Disappear only.
        /// </summary>
        After,
    }

    /// <summary>
    /// What an element with an Appear does when it appears together with its page or its container.
    /// </summary>
    public enum PageOpen
    {
        /// <summary>
        /// It plays its effect in the space kept for it, after its StartAfter. Nothing else moves.
        /// This is the default.
        /// </summary>
        InPlace,

        /// <summary>
        /// The page opens without it. After its StartAfter, it appears, and the elements around it
        /// move to make room.
        /// </summary>
        PushOthers,

        /// <summary>
        /// It is there when the page opens, without its effect. It still plays its effect when it
        /// appears later on its own.
        /// </summary>
        Skip,
    }

    /// <summary>
    /// Converts the names Fade, Slide, Zoom, Pop, Wipe, Blur, Custom and None to a <see cref="Transition"/>.
    /// </summary>
    public sealed class TransitionConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            => value is string s ? Transition.Parse(s) : base.ConvertFrom(context, culture, value);
    }

    /// <summary>
    /// How far an element slides. In XAML, write <c>Short</c> (10 pixels), <c>Medium</c> (30 pixels),
    /// <c>Long</c> (100 pixels), or a number of pixels (<c>40</c> or <c>40px</c>).
    /// </summary>
    [TypeConverter(typeof(DistanceConverter))]
    public readonly struct Distance : IEquatable<Distance>
    {
        /// <summary>
        /// Creates a distance of a number of pixels, from 0 to 2000.
        /// </summary>
        public Distance(double pixels)
        {
            Pixels = double.IsNaN(pixels) ? 0 : Math.Max(0, Math.Min(2000, pixels));
        }

        /// <summary>
        /// 10 pixels.
        /// </summary>
        public static Distance Short => new Distance(10);

        /// <summary>
        /// 30 pixels.
        /// </summary>
        public static Distance Medium => new Distance(30);

        /// <summary>
        /// 100 pixels.
        /// </summary>
        public static Distance Long => new Distance(100);

        /// <summary>
        /// Gets the distance in pixels.
        /// </summary>
        public double Pixels { get; }

        /// <summary>
        /// Reads a distance written as Short, Medium, Long, or a number of pixels (<c>40</c> or <c>40px</c>).
        /// </summary>
        /// <exception cref="FormatException">
        /// The text is not a distance.
        /// </exception>
        public static Distance Parse(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "short":
                    return Short;
                case "medium":
                    return Medium;
                case "long":
                    return Long;
            }

            string number = text?.Trim() ?? string.Empty;
            if (number.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                number = number.Substring(0, number.Length - 2);
            }

            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double pixels) && !double.IsInfinity(pixels))
            {
                return new Distance(pixels);
            }

            throw new FormatException($"'{text}' is not a distance. Use Short, Medium, Long, or a number of pixels.");
        }

        /// <summary>
        /// Converts a number of pixels to a distance.
        /// </summary>
        public static implicit operator Distance(double pixels) => new Distance(pixels);

        /// <inheritdoc />
        public bool Equals(Distance other) => Pixels == other.Pixels;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Distance other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Pixels.GetHashCode();

        /// <summary>
        /// Compares two distances.
        /// </summary>
        public static bool operator ==(Distance left, Distance right) => left.Equals(right);

        /// <summary>
        /// Compares two distances.
        /// </summary>
        public static bool operator !=(Distance left, Distance right) => !left.Equals(right);

        /// <summary>
        /// Returns the distance in pixels, for example <c>30px</c>.
        /// </summary>
        public override string ToString() => Pixels.ToString(CultureInfo.InvariantCulture) + "px";
    }

    /// <summary>
    /// Converts Short (10), Medium (30) and Long (100), or a number of pixels, to a <see cref="Distance"/>.
    /// </summary>
    public sealed class DistanceConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || sourceType == typeof(double) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return value switch
            {
                string s => Distance.Parse(s),
                double d => new Distance(d),
                _ => base.ConvertFrom(context, culture, value),
            };
        }
    }

    /// <summary>
    /// The look of an element at one end of an effect. The other end is the element as it is.
    /// </summary>
    internal struct EffectLook
    {
        public const int NoWipe = 0;
        public const int FromTop = 1;
        public const int FromLeft = 2;
        public const int FromBottom = 3;
        public const int FromRight = 4;

        public static EffectLook Identity => new EffectLook { Opacity = 1, Scale = 1 };

        private double? _opacity;
        private double? _scale;

        /// <summary>
        /// A fraction of the element's own opacity.
        /// </summary>
        public double Opacity
        {
            get => _opacity ?? 1;
            set => _opacity = value;
        }

        public double X;

        public double Y;

        public double Scale
        {
            get => _scale ?? 1;
            set => _scale = value;
        }

        /// <summary>
        /// In degrees, clockwise.
        /// </summary>
        public double Rotate;

        /// <summary>
        /// In pixels.
        /// </summary>
        public double Blur;

        /// <summary>
        /// The edge of a wipe, or <see cref="NoWipe"/>.
        /// </summary>
        public int Wipe;
    }
}
