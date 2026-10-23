using System.Windows;

namespace OpenSilver.Animations.Gallery
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            RootVisual = new MainPage();
        }
    }
}
