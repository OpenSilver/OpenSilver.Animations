using System.Windows;
using System.Windows.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class ResizableDialog : ChildWindow
    {
        private bool _wide;

        public ResizableDialog()
        {
            InitializeComponent();
        }

        private void WiderButton_Click(object sender, RoutedEventArgs e)
        {
            // While it is open, a ChildWindow covers the page: its Width reads as the width of
            // the page, and setting it changes the width of the dialog.
            _wide = !_wide;
            Width = _wide ? 600 : 300;
            WiderButton.Content = _wide ? "Make it narrower" : "Make it wider";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
