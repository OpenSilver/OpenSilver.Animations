using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter01 : Page
    {
        private static readonly string[] NoteTexts =
        {
            "Water the plants",
            "Send the invoice to Sam",
            "Renew the passport",
            "Pick up the dry cleaning",
            "Plan the team lunch",
            "Back up the photos",
        };

        private int _added;

        public Chapter01()
        {
            InitializeComponent();
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            bool show = Details.Visibility != Visibility.Visible;
            Details.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DetailsButton.Content = show ? "Hide details" : "Show details";
        }

        private void AddNote_Click(object sender, RoutedEventArgs e)
        {
            var remove = new Button
            {
                Content = "×",
                Style = (Style)Application.Current.Resources["LinkButton"],
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            remove.Click += RemoveNote_Click;

            var note = new Border
            {
                Style = (Style)Application.Current.Resources["Card"],
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new Grid
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = NoteTexts[_added++ % NoteTexts.Length],
                            Style = (Style)Application.Current.Resources["BodyText"],
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 40, 0),
                        },
                        remove,
                    },
                },
            };
            Animate.SetAppear(note, new Fade());
            Animate.SetDisappear(note, new Fade());

            Notes.Children.Add(note);
            UpdateCount();
        }

        private void RemoveNote_Click(object sender, RoutedEventArgs e)
        {
            for (DependencyObject d = (DependencyObject)sender; d != null; d = VisualTreeHelper.GetParent(d))
            {
                if (d is Border note && note.Parent == Notes)
                {
                    Notes.Children.Remove(note);
                    UpdateCount();
                    return;
                }
            }
        }

        private void UpdateCount()
        {
            int count = Notes.Children.Count;
            NoteCount.Text = count == 0 ? "No notes" : count == 1 ? "1 note" : count + " notes";
        }
    }
}
