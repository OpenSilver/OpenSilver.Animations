using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter11 : Page
    {
        private static readonly string[] Available = { "Mushrooms", "Olives", "Peppers", "Onions", "Pineapple" };
        private static readonly string[] Chosen = { "Mozzarella", "Basil" };

        private int _step;

        public Chapter11()
        {
            InitializeComponent();

            foreach (string name in Available)
            {
                AvailableToppings.Children.Add(CreateChip(name));
            }
            foreach (string name in Chosen)
            {
                ChosenToppings.Children.Add(CreateChip(name));
            }

            PhotoList.ItemsSource = new[]
            {
                new Photo(1, "Lake at dawn", "lake.jpg", "The first light on the water, before the wind rises."),
                new Photo(2, "Dunes", "dunes.jpg", "Sand as far as the eye can see, and not a single footprint."),
                new Photo(3, "Pine forest", "forest.jpg", "A walk of two hours, in the shade all the way."),
                new Photo(4, "Peaks at night", "night.jpg", "The snow still shines under the moon."),
                new Photo(5, "Out at sea", "sea.jpg", "A light breeze, and the harbor out of sight."),
                new Photo(6, "Autumn hills", "autumn.jpg", "Two weeks every year when the hills turn red."),
            };

            ShowStep(0);
        }

        private Button CreateChip(string name)
        {
            var chip = new Button { Content = name, Style = (Style)Resources["Chip"] };
            Animate.SetMagicMove(chip, "topping-" + name);
            chip.Click += Chip_Click;
            return chip;
        }

        private void Chip_Click(object sender, RoutedEventArgs e)
        {
            var chip = (Button)sender;
            Panel from = (Panel)chip.Parent;
            Panel to = from == AvailableToppings ? ChosenToppings : AvailableToppings;
            from.Children.Remove(chip);
            to.Children.Add(CreateChip((string)chip.Content));
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            PhotoDetails.DataContext = ((FrameworkElement)sender).DataContext;
            PhotoList.Visibility = Visibility.Collapsed;
            PhotoDetails.Visibility = Visibility.Visible;
        }

        private void BackToPhotos_Click(object sender, RoutedEventArgs e)
        {
            PhotoDetails.Visibility = Visibility.Collapsed;
            PhotoList.Visibility = Visibility.Visible;
        }

        private void WizardNext_Click(object sender, RoutedEventArgs e) => ShowStep(_step == 2 ? 0 : _step + 1);

        private void WizardBack_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

        private void ShowStep(int step)
        {
            _step = step;
            WizardHost.Content = ((DataTemplate)Resources["WizardStep" + (step + 1)]).LoadContent();
            WizardProgress.Text = "Step " + (step + 1) + " of 3";
            WizardBack.IsEnabled = step > 0;
            WizardNext.Content = step == 2 ? "Start over" : "Next";
        }

        private void AskBox_GotFocus(object sender, RoutedEventArgs e) => AskPopup.IsOpen = true;

        private async void AskPopup_Opened(object sender, EventArgs e)
        {
            // The popup's content is only on the page a moment after it opens.
            await Task.Delay(100);
            if (!AskPopup.IsOpen)
            {
                return;
            }

            // The small text box captured the mouse when it was pressed, and was collapsed before the button was released.
            if (Mouse.Captured is UIElement captured)
            {
                captured.ReleaseMouseCapture();
            }

            AskCardBox.Focus();
            AskCardBox.Select(AskCardBox.Text.Length, 0);
        }

        private void AskText_Changed(object sender, TextChangedEventArgs e)
        {
            if (AskPlaceholder == null || AskCardPlaceholder == null)
            {
                return;
            }

            Visibility placeholder = string.IsNullOrEmpty(AskBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            AskPlaceholder.Visibility = placeholder;
            AskCardPlaceholder.Visibility = placeholder;
        }

        private void AskCard_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                AskPopup.IsOpen = false;
            }
        }

        private void AskSend_Click(object sender, RoutedEventArgs e)
        {
            AskBox.Text = "";
            AskPopup.IsOpen = false;
        }
    }

    public sealed class Photo
    {
        public Photo(int id, string title, string file, string description)
        {
            Id = id;
            Title = title;
            Description = description;
            Image = new BitmapImage(new Uri("/OpenSilver.Animations.Gallery;component/Images/" + file, UriKind.Relative));
        }

        public int Id { get; }

        public string Title { get; }

        public string Description { get; }

        public ImageSource Image { get; }
    }
}
