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
    /// What an element does when it appears or disappears: <see cref="Fade"/>, <see cref="Slide"/>,
    /// <see cref="Zoom"/>, <see cref="Pop"/>, <see cref="Wipe"/>, <see cref="Blur"/>, <see cref="Custom"/>
    /// or <see cref="None"/>, and for a TextBlock, <see cref="Typewriter"/>, <see cref="FadeLetters"/>
    /// and <see cref="Backspace"/>. In XAML, write the name (<c>"Fade"</c>) or use a markup extension
    /// to set parameters (<c>"{anim:Slide From=Top, Duration=0.4s}"</c>).
    /// </summary>
    [TypeConverter(typeof(TransitionConverter))]
    public abstract class Transition : MarkupExtension
    {
        private static readonly AnimationTime MaxStartAfter = AnimationTime.FromSeconds(10);

        private double? _bounciness;
        private AnimationTime _startAfter;
        private OthersMove? _othersMove;

        private protected Transition() { }

        /// <summary>
        /// No effect: the element appears or disappears at once. Inside an element that uses
        /// <see cref="AnimationScope.EverythingInside"/>, it turns off the automatic wipe or fade.
        /// </summary>
        public static Transition None { get; } = new NoTransition();

        /// <summary>
        /// How long the effect lasts, from 0.05 to 3 seconds. The elements around it that move to make
        /// room or to fill the space take the same time, unless you set <see cref="OthersDuration"/>.
        /// The default is <see cref="AnimationSettings.DefaultDuration"/> (twice that for
        /// <see cref="Pop"/>, <see cref="FadeLetters"/> and <c>Easing=Bouncy</c>). Not used by
        /// <see cref="Typewriter"/> and <see cref="Backspace"/>, which have a Speed.
        /// </summary>
        public AnimationTime? Duration { get; set; }

        /// <summary>
        /// How long the elements around it take to move to make room or to fill the space, from
        /// 0.05 to 3 seconds. The default is the <see cref="Duration"/> of the effect, and
        /// <see cref="AnimationSettings.DefaultDuration"/> for the effects of a TextBlock's
        /// letters. A little longer makes them follow it softly.
        /// </summary>
        public AnimationTime? OthersDuration { get; set; }

        /// <summary>
        /// How it speeds up and slows down: Smooth (the default), Gentle, Linear, Bouncy, EaseOut,
        /// EaseIn, EaseInOut or Overshoot, or any <see cref="IEasingFunction"/>. With Bouncy, it
        /// fades without bouncing, and the elements around it move smoothly. With Overshoot, it
        /// fades, and they move, with EaseOut. Not used by the effects of a TextBlock's letters.
        /// </summary>
        [TypeConverter(typeof(EasingConverter))]
        public IEasingFunction Easing { get; set; }

        /// <summary>
        /// How much it bounces, from 0.1 (a little) to 1 (a lot). The default is 0.5. Used by
        /// <see cref="Pop"/>, by every effect with <c>Easing=Bouncy</c>, and by
        /// <c>Easing=Overshoot</c>, where it says how far it goes past the end.
        /// </summary>
        public double? Bounciness
        {
            get => _bounciness;
            set => _bounciness = value is double d ? Math.Max(0.1, Math.Min(1, d)) : null;
        }

        /// <summary>
        /// When the elements around it move: <see cref="Animations.OthersMove.While"/> it appears or
        /// disappears (the default), <see cref="Animations.OthersMove.Before"/> it appears, or
        /// <see cref="Animations.OthersMove.After"/> it disappears (the default for
        /// <see cref="Backspace"/>, so that they don't cover the letters it erases).
        /// </summary>
        public OthersMove OthersMove
        {
            get => _othersMove ?? DefaultOthersMove;
            set => _othersMove = value;
        }

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

        internal bool UsesBounciness => IsBouncy || Easing is OvershootEasing;

        /// <summary>
        /// Whether it plays on the letters of a TextBlock.
        /// </summary>
        internal bool IsText => this is Typewriter || this is FadeLetters || this is Backspace;

        /// <summary>
        /// Whether it types or erases at a Speed instead of lasting a Duration.
        /// </summary>
        internal bool HasSpeed => this is Typewriter || this is Backspace;

        /// <summary>
        /// The letters per second of Typewriter and Backspace.
        /// </summary>
        internal virtual double LettersPerSecond => 0;

        /// <summary>
        /// Whether a cursor follows the letters of Typewriter and Backspace.
        /// </summary>
        internal virtual bool ShowsCursor => false;

        /// <summary>
        /// How long the cursor of Typewriter and Backspace blinks before the first letter, when
        /// it shows.
        /// </summary>
        internal virtual AnimationTime BlinkFirstTime => AnimationTime.Zero;

        /// <summary>
        /// Whether BlinkFirst was set, for the warning when there is no cursor.
        /// </summary>
        internal virtual bool HasBlinkFirst => false;

        internal virtual OthersMove DefaultOthersMove => OthersMove.While;

        internal virtual bool HasFrom => false;

        internal virtual bool HasTo => false;

        /// <summary>
        /// The warning for a To on an Appear, or a From on a Disappear.
        /// </summary>
        internal virtual string MisplacedFromOrTo(bool appear) => appear
            ? "To is for Disappear, so it comes from the bottom. Use From to choose the side it comes from"
            : "From is for Appear, so it goes to the bottom. Use To to choose the side it goes to";

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
        /// Reads an effect name: Fade, Slide, Zoom, Pop, Wipe, Blur, Custom, Typewriter,
        /// FadeLetters, Backspace or None.
        /// </summary>
        /// <exception cref="FormatException">
        /// The text is not an effect name.
        /// </exception>
        public static Transition Parse(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "typewriter":
                    return new Typewriter();
                case "fadeletters":
                    return new FadeLetters();
                case "backspace":
                    return new Backspace();
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
                    throw new FormatException($"'{text}' is not an effect. Use Fade, Slide, Zoom, Pop, Wipe, Blur, Custom, Typewriter, FadeLetters, Backspace or None.");
            }
        }

        /// <summary>
        /// Reads an effect name: Fade, Slide, Zoom, Pop, Wipe, Blur, Custom, Typewriter,
        /// FadeLetters, Backspace or None.
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
    /// Fades in while sliding in from a side, or fades out while sliding out to a side. Set
    /// <see cref="Fade"/> to false to only slide.
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
        /// a number of pixels, or a percent of its own size: <c>100%</c> slides it by its whole
        /// width from the left or the right, or by its whole height from the top or the bottom.
        /// </summary>
        public Distance Distance { get; set; } = Distance.Medium;

        /// <summary>
        /// Whether it fades in or out while it slides: true (the default), or false to only slide,
        /// for example to come in from the edge of the window.
        /// </summary>
        public bool Fade { get; set; } = true;

        internal override string PresetName => "Slide";

        internal override EffectLook GetLook(bool appear)
        {
            Side side = appear ? From : To;
            double opacity = Fade ? 0 : 1;
            if (Distance.IsPercent)
            {
                double relX = DistanceOf(side, Distance.Percent / 100, out double relY);
                return new EffectLook { Opacity = opacity, RelX = relX, RelY = relY };
            }

            double x = DistanceOf(side, Distance.Pixels, out double y);
            return new EffectLook { Opacity = opacity, X = x, Y = y };
        }

        internal override string Describe(bool appear) => appear ? "slides in" : "slides out";
    }

    /// <summary>
    /// Fades in while growing from 90% of its size, or fades out while shrinking to 90%.
    /// <see cref="From"/> and <see cref="To"/> choose another size, and <see cref="Fade"/> set to
    /// false makes it only grow or shrink.
    /// </summary>
    public sealed class Zoom : Transition
    {
        private const double DefaultSize = 0.9;

        private double? _from;
        private double? _to;

        /// <summary>
        /// The size it grows from when it appears, from 0 to 3: 0.9 (90% of its size, the default),
        /// 0.5 for half its size, or more than 1 to shrink into place.
        /// </summary>
        public double From
        {
            get => _from ?? DefaultSize;
            set => _from = SizeOf(value);
        }

        /// <summary>
        /// The size it shrinks to when it disappears, from 0 to 3: 0.9 (90% of its size, the default),
        /// 0.5 for half its size, or more than 1 to grow away.
        /// </summary>
        public double To
        {
            get => _to ?? DefaultSize;
            set => _to = SizeOf(value);
        }

        /// <summary>
        /// Whether it fades in or out while it grows or shrinks: true (the default), or false to
        /// only grow or shrink.
        /// </summary>
        public bool Fade { get; set; } = true;

        internal override bool HasFrom => _from.HasValue;

        internal override bool HasTo => _to.HasValue;

        internal override string PresetName => "Zoom";

        internal override EffectLook GetLook(bool appear) => new EffectLook { Opacity = Fade ? 0 : 1, Scale = appear ? From : To };

        internal override string Describe(bool appear) => appear ? "zooms in" : "zooms out";

        internal override string MisplacedFromOrTo(bool appear) => appear
            ? "To is for Disappear, so it grows from 90% of its size. Use From to choose the size it grows from"
            : "From is for Appear, so it shrinks to 90% of its size. Use To to choose the size it shrinks to";

        private static double SizeOf(double value) => double.IsNaN(value) ? DefaultSize : Math.Max(0, Math.Min(3, value));
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
    /// For a TextBlock: its letters are typed one after the other, like on a typewriter. A cursor
    /// blinks where the first letter goes, follows the letters, then blinks twice and fades away.
    /// A long text types faster, so that it never takes more than 10 seconds. For Appear: as a
    /// Disappear, its letters are erased like <see cref="Backspace"/>.
    /// </summary>
    /// <example>
    /// <code>&lt;TextBlock Text="Welcome" anim:Animate.Appear="{anim:Typewriter Speed=40, BlinkFirst=1.5s}" /&gt;</code>
    /// </example>
    public sealed class Typewriter : Transition
    {
        private static readonly AnimationTime DefaultBlinkFirst = AnimationTime.FromSeconds(0.8);
        private static readonly AnimationTime MaxBlinkFirst = AnimationTime.FromSeconds(10);

        private double _speed = 30;
        private AnimationTime? _blinkFirst;

        /// <summary>
        /// How many letters it types per second, from 5 to 200. The default is 30.
        /// </summary>
        public double Speed
        {
            get => _speed;
            set => _speed = LettersOf(value);
        }

        /// <summary>
        /// Whether a cursor shows where the next letter goes while it types, then blinks twice and
        /// fades away. The default is true.
        /// </summary>
        public bool Cursor { get; set; } = true;

        /// <summary>
        /// How long the cursor blinks where the first letter goes before typing starts, from 0 to
        /// 10 seconds. The default is 0.8 seconds: the cursor shows, goes off once, and typing
        /// starts as it comes back. It counts after <see cref="Transition.StartAfter"/>, during
        /// which nothing shows. Not used without the cursor.
        /// </summary>
        public AnimationTime BlinkFirst
        {
            get => _blinkFirst ?? DefaultBlinkFirst;
            set => _blinkFirst = BlinkFirstOf(value);
        }

        internal override double LettersPerSecond => Speed;

        internal override bool ShowsCursor => Cursor;

        internal override AnimationTime BlinkFirstTime => BlinkFirst;

        internal override bool HasBlinkFirst => _blinkFirst != null;

        internal override string PresetName => "Typewriter";

        internal override EffectLook GetLook(bool appear) => new EffectLook
        {
            Text = appear ? EffectLook.TypeLetters : EffectLook.EraseLetters,
            Cursor = Cursor,
        };

        internal override string Describe(bool appear) => appear ? "is typed in" : "is erased letter by letter";

        internal static double LettersOf(double value) => double.IsNaN(value) ? 30 : Math.Max(5, Math.Min(200, value));

        internal static AnimationTime BlinkFirstOf(AnimationTime value) => value.Value > MaxBlinkFirst.Value ? MaxBlinkFirst : value;
    }

    /// <summary>
    /// For a TextBlock: its letters fade in one after the other, from the first to the last, or
    /// fade out the same way. The whole effect lasts its Duration, twice
    /// <see cref="AnimationSettings.DefaultDuration"/> by default.
    /// </summary>
    public sealed class FadeLetters : Transition
    {
        internal override string PresetName => "FadeLetters";

        internal override EffectLook GetLook(bool appear) => new EffectLook { Text = EffectLook.FadeLetters };

        internal override string Describe(bool appear) => appear ? "fades in letter by letter" : "fades out letter by letter";
    }

    /// <summary>
    /// For a TextBlock: its letters are erased one after the other, from the last one, like with
    /// the Backspace key, with a cursor after the last letter. The elements around it move into
    /// its place after that (<see cref="Transition.OthersMove"/> is After by default). A long text
    /// is erased faster, so that it never takes more than 3 seconds. For Disappear: as an Appear,
    /// its letters are typed like <see cref="Typewriter"/>.
    /// </summary>
    public sealed class Backspace : Transition
    {
        private double _speed = 30;
        private AnimationTime? _blinkFirst;

        /// <summary>
        /// How many letters it erases per second, from 5 to 200. The default is 30.
        /// </summary>
        public double Speed
        {
            get => _speed;
            set => _speed = Typewriter.LettersOf(value);
        }

        /// <summary>
        /// Whether a cursor shows after the last letter while it erases. The default is true.
        /// </summary>
        public bool Cursor { get; set; } = true;

        /// <summary>
        /// How long the cursor blinks after the last letter before erasing starts, from 0 (the
        /// default) to 10 seconds. It counts after <see cref="Transition.StartAfter"/>. Not used
        /// without the cursor.
        /// </summary>
        public AnimationTime BlinkFirst
        {
            get => _blinkFirst ?? AnimationTime.Zero;
            set => _blinkFirst = Typewriter.BlinkFirstOf(value);
        }

        internal override double LettersPerSecond => Speed;

        internal override bool ShowsCursor => Cursor;

        internal override AnimationTime BlinkFirstTime => BlinkFirst;

        internal override bool HasBlinkFirst => _blinkFirst != null;

        internal override OthersMove DefaultOthersMove => OthersMove.After;

        internal override string PresetName => "Backspace";

        internal override EffectLook GetLook(bool appear) => new EffectLook
        {
            Text = appear ? EffectLook.TypeLetters : EffectLook.EraseLetters,
            Cursor = Cursor,
        };

        internal override string Describe(bool appear) => appear ? "is typed in" : "is erased letter by letter";
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
    /// Converts the names Fade, Slide, Zoom, Pop, Wipe, Blur, Custom, Typewriter, FadeLetters,
    /// Backspace and None to a <see cref="Transition"/>.
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
    /// <c>Long</c> (100 pixels), a number of pixels (<c>40</c> or <c>40px</c>), or, for
    /// <see cref="Slide"/>, a percent of the element's own size (<c>50%</c>).
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
            Percent = 0;
        }

        private Distance(double pixels, double percent)
        {
            Pixels = pixels;
            Percent = percent;
        }

        /// <summary>
        /// Creates a distance that is a percent of the element's own size, from 0 to 1000: of its
        /// width when it slides from the left or the right, and of its height when it slides from
        /// the top or the bottom. 100 slides it by its whole size.
        /// </summary>
        public static Distance FromPercent(double percent)
            => new Distance(0, double.IsNaN(percent) ? 0 : Math.Max(0, Math.Min(1000, percent)));

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
        /// Gets the distance in pixels, or 0 when it is a percent of the element's size.
        /// </summary>
        public double Pixels { get; }

        /// <summary>
        /// Gets the percent of the element's size, or 0 when it is a number of pixels.
        /// </summary>
        public double Percent { get; }

        internal bool IsPercent => Percent > 0;

        /// <summary>
        /// Reads a distance written as Short, Medium, Long, a number of pixels (<c>40</c> or
        /// <c>40px</c>), or a percent of the element's size (<c>50%</c>).
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
            bool percent = number.EndsWith("%", StringComparison.Ordinal);
            if (percent)
            {
                number = number.Substring(0, number.Length - 1);
            }
            else if (number.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                number = number.Substring(0, number.Length - 2);
            }

            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !double.IsInfinity(value))
            {
                return percent ? FromPercent(value) : new Distance(value);
            }

            throw new FormatException($"'{text}' is not a distance. Use Short, Medium, Long, a number of pixels, or a percent of the element's size, like 50%.");
        }

        /// <summary>
        /// Converts a number of pixels to a distance.
        /// </summary>
        public static implicit operator Distance(double pixels) => new Distance(pixels);

        /// <inheritdoc />
        public bool Equals(Distance other) => Pixels == other.Pixels && Percent == other.Percent;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Distance other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Pixels.GetHashCode() ^ (Percent.GetHashCode() * 397);

        /// <summary>
        /// Compares two distances.
        /// </summary>
        public static bool operator ==(Distance left, Distance right) => left.Equals(right);

        /// <summary>
        /// Compares two distances.
        /// </summary>
        public static bool operator !=(Distance left, Distance right) => !left.Equals(right);

        /// <summary>
        /// Returns the distance, for example <c>30px</c> or <c>50%</c>.
        /// </summary>
        public override string ToString() => IsPercent
            ? Percent.ToString(CultureInfo.InvariantCulture) + "%"
            : Pixels.ToString(CultureInfo.InvariantCulture) + "px";
    }

    /// <summary>
    /// Converts Short (10), Medium (30) and Long (100), a number of pixels, or a percent like
    /// <c>50%</c>, to a <see cref="Distance"/>.
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

        public const int NoText = 0;
        public const int TypeLetters = 1;
        public const int FadeLetters = 2;
        public const int EraseLetters = 3;

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

        /// <summary>
        /// A fraction of the element's own width, added to <see cref="X"/>.
        /// </summary>
        public double RelX;

        /// <summary>
        /// A fraction of the element's own height, added to <see cref="Y"/>.
        /// </summary>
        public double RelY;

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

        /// <summary>
        /// What happens to the letters of a TextBlock, or <see cref="NoText"/>. The rest of the
        /// look is not used then.
        /// </summary>
        public int Text;

        /// <summary>
        /// The time between two letters typed or erased, in milliseconds at the current speed.
        /// </summary>
        public double LetterMs;

        public bool Cursor;

        /// <summary>
        /// How long the cursor blinks after the last letter typed, in milliseconds at the current
        /// speed, or 0 without a cursor. Each blink lasts a fifth of it, also before the first
        /// letter.
        /// </summary>
        public double CursorMs;

        /// <summary>
        /// How long the cursor blinks before the first letter, in milliseconds at the current speed.
        /// </summary>
        public double BlinkFirstMs;
    }
}
