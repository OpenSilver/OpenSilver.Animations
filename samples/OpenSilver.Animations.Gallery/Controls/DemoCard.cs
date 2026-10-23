using System.Windows;
using System.Windows.Controls;

namespace OpenSilver.Animations.Gallery.Controls
{
    /// <summary>
    /// A demo of a chapter: a title, a sentence, the live example, its XAML, and what should
    /// happen when you try it. The look is defined in App.xaml.
    /// </summary>
    public class DemoCard : ContentControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(DemoCard), new PropertyMetadata(null));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(DemoCard), new PropertyMetadata(null));

        public static readonly DependencyProperty XamlProperty =
            DependencyProperty.Register(nameof(Xaml), typeof(string), typeof(DemoCard), new PropertyMetadata(null));

        public static readonly DependencyProperty ExpectationsProperty =
            DependencyProperty.Register(nameof(Expectations), typeof(string), typeof(DemoCard), new PropertyMetadata(null));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        /// <summary>
        /// The XAML that makes the demo animate.
        /// </summary>
        public string Xaml
        {
            get => (string)GetValue(XamlProperty);
            set => SetValue(XamlProperty, value);
        }

        /// <summary>
        /// What should happen, one line per expectation, for manual testing.
        /// </summary>
        public string Expectations
        {
            get => (string)GetValue(ExpectationsProperty);
            set => SetValue(ExpectationsProperty, value);
        }
    }
}
