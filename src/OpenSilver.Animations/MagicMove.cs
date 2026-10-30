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
    /// Links elements with the same id: when one of them disappears and another one appears, the
    /// old one seems to travel to the new one's place and to turn into it, like a selection pill
    /// that slides to the button you click. In XAML, write the id (<c>"SelectionPill"</c>), bind it
    /// (<c>"{Binding Id, StringFormat='photo-{0}'}"</c>), or use the markup extension to set
    /// parameters (<c>"{anim:MagicMove Id=SelectionPill, Duration=0.4s}"</c>).
    /// </summary>
    /// <remarks>
    /// A markup extension can't hold a binding: a bound id uses the default parameters.
    /// </remarks>
    [TypeConverter(typeof(MagicMoveConverter))]
    public sealed class MagicMove : MarkupExtension
    {
        /// <summary>
        /// The id that links the elements. Elements that use the same id travel to each other.
        /// When several of them could, like in the templates of two lists, the two that are the
        /// closest in the page travel.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Where the element travels: <see cref="MagicMoveTravel.Automatic"/> (the default),
        /// <see cref="MagicMoveTravel.Above"/> or <see cref="MagicMoveTravel.Behind"/> the other elements.
        /// </summary>
        public MagicMoveTravel Travel { get; set; }

        /// <summary>
        /// How long the travel lasts, from 0.05 to 3 seconds. By default, it depends on the
        /// distance: twice <see cref="AnimationSettings.DefaultDuration"/> for a short trip (0.5 s),
        /// up to almost twice that for a long one (0.9 s).
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

        internal bool HasId => !string.IsNullOrWhiteSpace(Id);

        internal bool HasParameters => Travel != MagicMoveTravel.Automatic || Duration != null || Easing != null;

        /// <summary>
        /// Returns this object.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        /// <summary>
        /// Makes a <see cref="MagicMove"/> with this id.
        /// </summary>
        public static MagicMove Parse(string text) => new MagicMove { Id = text?.Trim() };

        /// <summary>
        /// Makes a <see cref="MagicMove"/> with this id.
        /// </summary>
        public static implicit operator MagicMove(string id) => Parse(id);

        /// <summary>
        /// Returns the id.
        /// </summary>
        public override string ToString() => Id ?? string.Empty;
    }

    /// <summary>
    /// Where an element with <see cref="Animate.MagicMoveProperty">MagicMove</see> travels.
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
    /// Converts an id to a <see cref="MagicMove"/>, which also makes a bound id work.
    /// </summary>
    public sealed class MagicMoveConverter : TypeConverter
    {
        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            => value is string s ? MagicMove.Parse(s) : base.ConvertFrom(context, culture, value);
    }
}
