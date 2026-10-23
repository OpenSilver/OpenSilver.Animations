using System.Windows;
using System.Windows.Controls;
using OpenSilver.Animations.Gallery.Shell;

namespace OpenSilver.Animations.Gallery.Controls
{
    /// <summary>
    /// The Previous and Next buttons at the bottom of a chapter. They skip the chapters that are
    /// not written yet.
    /// </summary>
    public partial class ChapterNavigation : UserControl
    {
        private int _chapter;
        private ChapterInfo _previous;
        private ChapterInfo _next;

        public ChapterNavigation()
        {
            InitializeComponent();
            Update();
        }

        /// <summary>
        /// The number of the chapter that shows these buttons. 0 is the welcome page.
        /// </summary>
        public int Chapter
        {
            get => _chapter;
            set
            {
                _chapter = value;
                Update();
            }
        }

        private void Update()
        {
            _previous = Tour.PreviousReady(_chapter);
            _next = Tour.NextReady(_chapter);

            PreviousButton.Visibility = _previous != null ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Visibility = _next != null ? Visibility.Visible : Visibility.Collapsed;
            PreviousTitle.Text = _previous != null ? _previous.Number + ". " + _previous.Title : string.Empty;
            NextTitle.Text = _next != null ? _next.Number + ". " + _next.Title : string.Empty;
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => MainPage.Current?.Navigate(_previous);

        private void NextButton_Click(object sender, RoutedEventArgs e) => MainPage.Current?.Navigate(_next);
    }
}
