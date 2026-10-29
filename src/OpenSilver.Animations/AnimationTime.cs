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

namespace OpenSilver.Animations
{
    /// <summary>
    /// A duration or a delay. In XAML, write it in seconds (<c>0.3s</c>), in milliseconds
    /// (<c>300ms</c>), or as a plain number of milliseconds (<c>300</c>).
    /// </summary>
    [TypeConverter(typeof(AnimationTimeConverter))]
    public readonly struct AnimationTime : IEquatable<AnimationTime>
    {
        /// <summary>
        /// Creates a time from a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="value">
        /// The time. Negative values are treated as zero.
        /// </param>
        public AnimationTime(TimeSpan value)
        {
            Value = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        }

        /// <summary>
        /// A time of zero.
        /// </summary>
        public static AnimationTime Zero => default;

        /// <summary>
        /// Gets the time as a <see cref="TimeSpan"/>.
        /// </summary>
        public TimeSpan Value { get; }

        /// <summary>
        /// Gets the time in seconds.
        /// </summary>
        public double Seconds => Value.TotalSeconds;

        /// <summary>
        /// Creates a time from a number of seconds.
        /// </summary>
        public static AnimationTime FromSeconds(double seconds) => new AnimationTime(TimeSpan.FromTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond)));

        /// <summary>
        /// Creates a time from a number of milliseconds.
        /// </summary>
        public static AnimationTime FromMilliseconds(double milliseconds) => new AnimationTime(TimeSpan.FromTicks((long)Math.Round(milliseconds * TimeSpan.TicksPerMillisecond)));

        /// <summary>
        /// Reads a time written as <c>0.3s</c>, <c>300ms</c>, <c>300</c> (milliseconds) or <c>00:00:00.3</c>.
        /// </summary>
        /// <exception cref="FormatException">
        /// The text is not a time.
        /// </exception>
        public static AnimationTime Parse(string text)
        {
            if (TryParse(text, out AnimationTime time))
            {
                return time;
            }

            throw new FormatException($"'{text}' is not a time. Write it like 0.3s, 300ms or 300 (milliseconds).");
        }

        /// <summary>
        /// Tries to read a time written as <c>0.3s</c>, <c>300ms</c>, <c>300</c> (milliseconds) or <c>00:00:00.3</c>.
        /// </summary>
        public static bool TryParse(string text, out AnimationTime time)
        {
            time = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string s = text.Trim().ToLowerInvariant();

            if (s.IndexOf(':') >= 0)
            {
                if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out TimeSpan span))
                {
                    time = new AnimationTime(span);
                    return true;
                }

                return false;
            }

            double factor = 1;
            if (s.EndsWith("ms", StringComparison.Ordinal))
            {
                s = s.Substring(0, s.Length - 2);
            }
            else if (s.EndsWith("s", StringComparison.Ordinal))
            {
                s = s.Substring(0, s.Length - 1);
                factor = 1000;
            }

            if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
                !double.IsNaN(value) && !double.IsInfinity(value))
            {
                time = FromMilliseconds(value * factor);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a <see cref="TimeSpan"/> to a time.
        /// </summary>
        public static implicit operator AnimationTime(TimeSpan value) => new AnimationTime(value);

        /// <summary>
        /// Reads a time written as <c>0.3s</c>, <c>300ms</c> or <c>300</c> (milliseconds).
        /// </summary>
        public static implicit operator AnimationTime(string text) => Parse(text);

        /// <inheritdoc />
        public bool Equals(AnimationTime other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is AnimationTime other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Compares two times.
        /// </summary>
        public static bool operator ==(AnimationTime left, AnimationTime right) => left.Equals(right);

        /// <summary>
        /// Compares two times.
        /// </summary>
        public static bool operator !=(AnimationTime left, AnimationTime right) => !left.Equals(right);

        /// <summary>
        /// Returns the time in seconds, for example <c>0.25s</c>.
        /// </summary>
        public override string ToString() => Math.Round(Seconds, 3).ToString(CultureInfo.InvariantCulture) + "s";
    }

    /// <summary>
    /// Converts text such as <c>0.3s</c>, <c>300ms</c> or <c>300</c> to an <see cref="AnimationTime"/>.
    /// </summary>
    public sealed class AnimationTimeConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || sourceType == typeof(TimeSpan) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return value switch
            {
                string s => AnimationTime.Parse(s),
                TimeSpan t => new AnimationTime(t),
                _ => base.ConvertFrom(context, culture, value),
            };
        }

        /// <inheritdoc />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is AnimationTime time)
            {
                return time.ToString();
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
