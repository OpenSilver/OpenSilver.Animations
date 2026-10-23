using System;
using OpenSilver.Simulator;

namespace OpenSilver.Animations.Gallery.Simulator
{
    internal static class Startup
    {
        [STAThread]
        static int Main(string[] args)
        {
            return SimulatorLauncher.Start<App>();
        }
    }
}
