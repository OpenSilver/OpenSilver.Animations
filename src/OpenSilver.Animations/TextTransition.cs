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

namespace OpenSilver.Animations
{
    /// <summary>
    /// What a TextBlock does when its text changes: <see cref="Roll"/>, <see cref="CrossFade"/>,
    /// <see cref="Scramble"/>, <see cref="Retype"/>, <see cref="CountUp"/> or <see cref="None"/>.
    /// In XAML, write the name (<c>"Roll"</c>) or use a markup extension to set parameters
    /// (<c>"{anim:Roll Duration=0.4s}"</c>).
    /// </summary>
    [TypeConverter(typeof(TextTransitionConverter))]
    public abstract class TextTransition : MarkupExtension
    {
        private protected TextTransition() { }

        /// <summary>
        /// Nothing happens: the new text shows at once, for example to turn off an effect that a
        /// Style sets.
        /// </summary>
        public static TextTransition None { get; } = new NoTextTransition();

        /// <summary>
        /// How long it lasts, from 0.05 to 3 seconds. The default is
        /// <see cref="AnimationSettings.DefaultDuration"/> for <see cref="Roll"/> and
        /// <see cref="CrossFade"/>, and four times that for <see cref="Scramble"/> and
        /// <see cref="CountUp"/>. Not used by <see cref="Retype"/>, which has a
        /// <see cref="Retype.Speed"/>.
        /// </summary>
        public AnimationTime? Duration { get; set; }

        internal abstract string PresetName { get; }

        internal bool IsNone => this is NoTextTransition;

        /// <summary>
        /// The default duration, as a number of times <see cref="AnimationSettings.DefaultDuration"/>.
        /// </summary>
        internal virtual double DurationFactor => 1;

        /// <summary>
        /// Returns this object.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        /// <summary>
        /// Reads a text effect name: Roll, CrossFade, Scramble, Retype, CountUp or None.
        /// </summary>
        /// <exception cref="FormatException">
        /// The text is not a text effect name.
        /// </exception>
        public static TextTransition Parse(string text)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "roll":
                    return new Roll();
                case "crossfade":
                    return new CrossFade();
                case "scramble":
                    return new Scramble();
                case "retype":
                    return new Retype();
                case "countup":
                    return new CountUp();
                case "none":
                case "":
                    return None;
                default:
                    throw new FormatException($"'{text}' is not a text effect. Use Roll, CrossFade, Scramble, Retype, CountUp or None.");
            }
        }

        /// <summary>
        /// Reads a text effect name: Roll, CrossFade, Scramble, Retype, CountUp or None.
        /// </summary>
        public static implicit operator TextTransition(string text) => Parse(text);

        /// <summary>
        /// Returns the preset name, for example <c>Roll</c>.
        /// </summary>
        public override string ToString() => PresetName;

        private sealed class NoTextTransition : TextTransition
        {
            internal override string PresetName => "None";
        }
    }

    /// <summary>
    /// The characters that change roll in, like the digits of a counter, while the old ones roll
    /// out. A number rolls up when it grows and down when it gets smaller; other text rolls up.
    /// For text on one line: text that wraps, or that has several runs, cross-fades.
    /// </summary>
    public sealed class Roll : TextTransition
    {
        private Side? _from;

        /// <summary>
        /// The side the new characters come from: Bottom or Top. By default, it follows the value:
        /// from the bottom when a number grows, from the top when it gets smaller, and from the
        /// bottom for other text.
        /// </summary>
        public Side From
        {
            get => _from ?? Side.Bottom;
            set => _from = value;
        }

        internal bool HasFrom => _from.HasValue;

        internal override string PresetName => "Roll";
    }

    /// <summary>
    /// The old text fades out while the new one fades in, in the same place.
    /// </summary>
    public sealed class CrossFade : TextTransition
    {
        internal override string PresetName => "CrossFade";
    }

    /// <summary>
    /// The characters show random letters and digits, then settle on the new text from left to
    /// right, like a code being decoded. The letters that are the same in the old and the new text,
    /// at the same place, stay.
    /// </summary>
    public sealed class Scramble : TextTransition
    {
        internal override string PresetName => "Scramble";

        internal override double DurationFactor => 4;
    }

    /// <summary>
    /// The old text is erased letter by letter, then the new one is typed, with a cursor that
    /// follows the letters, then blinks twice and fades away. The beginning they share stays: from
    /// "Hello world" to "Hello there", only "world" is erased.
    /// </summary>
    public sealed class Retype : TextTransition
    {
        private double _speed = 30;
        private AnimationTime? _blinkFirst;

        /// <summary>
        /// How many letters it erases or types per second, from 5 to 200. The default is 30. A
        /// long text goes faster, so that it never takes more than 3 seconds.
        /// </summary>
        public double Speed
        {
            get => _speed;
            set => _speed = Typewriter.LettersOf(value);
        }

        /// <summary>
        /// Whether a cursor shows where the letters are erased and typed. The default is true.
        /// </summary>
        public bool Cursor { get; set; } = true;

        /// <summary>
        /// How long the cursor blinks after the old text before erasing starts, from 0 (the
        /// default) to 10 seconds. Not used without the cursor.
        /// </summary>
        public AnimationTime BlinkFirst
        {
            get => _blinkFirst ?? AnimationTime.Zero;
            set => _blinkFirst = Typewriter.BlinkFirstOf(value);
        }

        internal bool HasBlinkFirst => _blinkFirst != null;

        internal override string PresetName => "Retype";
    }

    /// <summary>
    /// The number counts from its old value to the new one, slowing down at the end, in the format
    /// of the new text: "$1,250.50", "4.2%" or "12 items". It counts from 0 when the old text was
    /// not a number. For numbers only: other text cross-fades.
    /// </summary>
    public sealed class CountUp : TextTransition
    {
        internal override string PresetName => "CountUp";

        internal override double DurationFactor => 4;
    }

    /// <summary>
    /// Converts the names Roll, CrossFade, Scramble, Retype, CountUp and None to a
    /// <see cref="TextTransition"/>.
    /// </summary>
    public sealed class TextTransitionConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            => value is string s ? TextTransition.Parse(s) : base.ConvertFrom(context, culture, value);
    }
}
