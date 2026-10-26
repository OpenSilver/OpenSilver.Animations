using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class ResizableDialog : ChildWindow
    {
        private const double NarrowWidth = 300;
        private const double WideWidth = 600;
        private const double WindowMargin = 24;

        private FrameworkElement _contentRoot;
        private bool _wide;

        public ResizableDialog()
        {
            InitializeComponent();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // When its size changes, a ChildWindow keeps its top-left corner in place with an
            // offset. This handler runs after that, and removes the offset to stay centered.
            _contentRoot = GetTemplateChild("ContentRoot") as FrameworkElement;
            if (GetTemplateChild("ContentPresenter") is FrameworkElement presenter)
            {
                presenter.SizeChanged += ContentPresenter_SizeChanged;
            }
        }

        private void ContentPresenter_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_contentRoot?.RenderTransform is TransformGroup transforms)
            {
                foreach (Transform transform in transforms.Children)
                {
                    if (transform is TranslateTransform offset)
                    {
                        offset.X = 0;
                        offset.Y = 0;
                    }
                }
            }
        }

        private void WiderButton_Click(object sender, RoutedEventArgs e)
        {
            // While it is open, a ChildWindow covers the page: its Width reads as the width of
            // the page, and setting it changes the width of the dialog.
            _wide = !_wide;
            double windowWidth = Application.Current.Host.Content.ActualWidth;
            Width = _wide ? Math.Max(NarrowWidth, Math.Min(WideWidth, windowWidth - WindowMargin)) : NarrowWidth;
            WiderButton.Content = _wide ? "Make it narrower" : "Make it wider";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
