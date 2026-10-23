using System.Windows.Controls;
using System.Windows.Navigation;
using OpenSilver.Animations.Gallery.Shell;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class ComingSoon : Page
    {
        public ComingSoon()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            int number = NavigationContext.QueryString.TryGetValue("chapter", out string value) && int.TryParse(value, out int n) ? n : 0;
            ChapterInfo chapter = Tour.Get(number);
            if (chapter == null)
            {
                ChapterTitle.Text = "This chapter doesn't exist";
                return;
            }

            Title = chapter.Title;
            ChapterLabel.Text = "CHAPTER " + chapter.Number;
            ChapterTitle.Text = chapter.Title;
            ChapterSummary.Text = chapter.Summary;
            Navigation.Chapter = chapter.Number;
        }
    }
}
