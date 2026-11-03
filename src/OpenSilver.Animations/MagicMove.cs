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
    /// How an element with a <see cref="Animate.SetMagicMoveId">MagicMoveId</see> travels: above or
    /// behind the other elements, how long, and with which curve, given in
    /// <see cref="Animate.SetMagicMoveSettings">MagicMoveSettings</see>
    /// (<c>"{anim:MagicMove Travel=Behind, Duration=0.4s}"</c>). Without it, the element travels
    /// with the default parameters. The id itself is given by MagicMoveId.
    /// </summary>
    [TypeConverter(typeof(MagicMoveConverter))]
    public sealed class MagicMove : MarkupExtension
    {
        /// <summary>
        /// Where the element travels: <see cref="MagicMoveTravel.Automatic"/> (the default),
        /// <see cref="MagicMoveTravel.Above"/> or <see cref="MagicMoveTravel.Behind"/> the other elements.
        /// </summary>
        public MagicMoveTravel Travel { get; set; }

        /// <summary>
        /// How long the travel lasts, from 0.05 to 3 seconds. By default, it depends on the
        /// distance: 0.5 seconds for a short trip, up to 0.9 seconds for a long one, longer or
        /// shorter with <see cref="AnimationSettings.DefaultDuration"/>.
        /// </summary>
        public AnimationTime? Duration { get; set; }

        /// <summary>
        /// How it speeds up and slows down: StrongEaseInOut (the default), Smooth, Gentle, Linear,
        /// Bouncy, EaseOut, EaseIn, EaseInOut, StrongEaseOut, StrongEaseIn or Overshoot, or any
        /// <see cref="IEasingFunction"/>. StrongEaseInOut is very slow at the start and the end, and
        /// very fast in between. With Bouncy, it changes size without bouncing.
        /// </summary>
        [TypeConverter(typeof(EasingConverter))]
        public IEasingFunction Easing { get; set; }

        internal bool HasParameters => Travel != MagicMoveTravel.Automatic || Duration != null || Easing != null;

        /// <summary>
        /// Returns this object.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }

    /// <summary>
    /// Where an element with a <see cref="Animate.SetMagicMoveId">MagicMoveId</see> travels.
    /// </summary>
    public enum MagicMoveTravel
    {
        /// <summary>
        /// Behind the other elements when it is a background, like a selection pill behind the
        /// text of a button, that fills its container and is drawn first. Otherwise above them.
        /// </summary>
        Automatic,

        /// <summary>
        /// Above everything on the page, even outside the containers that cut off their content.
        /// </summary>
        Above,

        /// <summary>
        /// Behind the elements it travels between, but in front of the background of the container
        /// that holds them both.
        /// </summary>
        Behind,
    }

    /// <summary>
    /// Explains that the id goes in <see cref="Animate.SetMagicMoveId">MagicMoveId</see>, when a
    /// text is given to <see cref="Animate.SetMagicMoveSettings">MagicMoveSettings</see>.
    /// </summary>
    public sealed class MagicMoveConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        /// <exception cref="FormatException">Always, for a text.</exception>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            => value is string s
                ? throw new FormatException($"The id of a Magic move goes in anim:Animate.MagicMoveId=\"{s.Trim()}\". " +
                                            "MagicMoveSettings only says how it travels, like {anim:MagicMove Duration=0.4s}.")
                : base.ConvertFrom(context, culture, value);
    }
}
