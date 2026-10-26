using System.Windows;
using System.Windows.Controls;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class Chapter03 : Page
    {
        private int _openSection = -1;

        public Chapter03()
        {
            InitializeComponent();
        }

        private void MessagesButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility visibility = WhileMessage.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            WhileMessage.Visibility = visibility;
            BeforeMessage.Visibility = visibility;
            AfterMessage.Visibility = visibility;
            MessagesButton.Content = visibility == Visibility.Visible ? "Hide the new message" : "Show the new message";
        }

        private void Section_Click(object sender, RoutedEventArgs e)
        {
            int section = int.Parse((string)((Button)sender).Tag);
            _openSection = _openSection == section ? -1 : section;

            StackPanel[] bodies = { Body0, Body1, Body2 };
            TextBlock[] chevrons = { Chevron0, Chevron1, Chevron2 };
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].Visibility = i == _openSection ? Visibility.Visible : Visibility.Collapsed;
                chevrons[i].Text = i == _openSection ? "−" : "+";
            }
        }
    }
}
