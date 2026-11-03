using System;
using System.Collections.Generic;
using System.Windows;

namespace OpenSilver.Animations.Gallery
{
    public sealed partial class App : Application
    {
        public App()
        {
            // Below 960 pixels the tour uses its phone layout, and below 1280 its tablet layout.
            // The {Responsive} values in the XAML and the shell's layout share these breakpoints.
            ResponsiveThreshold.Default = new ResponsiveThreshold(960, 1280);

            UnhandledException += App_UnhandledException;

            InitializeComponent();
            RootVisual = new MainPage();
        }

        private static void App_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            // Each exception comes with its depth, so that the nested ones are indented.
            var pending = new Stack<(Exception Exception, int Depth)>();
            if (e.ExceptionObject != null)
            {
                pending.Push((e.ExceptionObject, 0));
            }

            while (pending.Count > 0)
            {
                (Exception exception, int depth) = pending.Pop();
                string indent = new string(' ', depth * 2);
                Console.WriteLine(indent + (depth == 0 ? "Unhandled exception: " : "Inner exception: ") + exception.GetType().FullName + ": " + exception.Message);
                Console.WriteLine(indent + exception.StackTrace);

                if (exception is AggregateException aggregate)
                {
                    for (int i = aggregate.InnerExceptions.Count - 1; i >= 0; i--)
                    {
                        pending.Push((aggregate.InnerExceptions[i], depth + 1));
                    }
                }
                else if (exception.InnerException != null)
                {
                    pending.Push((exception.InnerException, depth + 1));
                }
            }
        }
    }
}
