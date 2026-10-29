using System;
using System.Globalization;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Runs the functions that people call in the browser console, like
    /// <c>osAnimations.slowMotion(4)</c>. Each one changes a setting of
    /// <see cref="AnimationSettings.Current"/> until the page is reloaded, and says what it did.
    /// Without a value, it says what the setting is.
    /// </summary>
    internal static class ConsoleCommands
    {
        private const string Reloaded = "until the page is reloaded";

        public static void Run(string name, string value)
        {
            AnimationSettings settings = AnimationSettings.Current;
            value = value?.Trim() ?? string.Empty;
            string answer;
            switch (name)
            {
                case "slowMotion":
                    answer = SlowMotion(settings, value);
                    break;
                case "showWhatAnimates":
                    answer = Switch(name, value, settings.ShowWhatAnimates, on => settings.ShowWhatAnimates = on,
                        "Show what animates is on: the elements that animate together are outlined while they animate.",
                        "Show what animates is off.");
                    break;
                case "logForDebugging":
                    answer = Switch(name, value, settings.LogForDebugging, on => settings.LogForDebugging = on,
                        "The explanations are on: why each change animates or not is written here.",
                        "The explanations are off.");
                    break;
                case "simulateReducedMotion":
                    answer = Switch(name, value, settings.SimulateReducedMotion, on => settings.SimulateReducedMotion = on,
                        "Reduced motion is simulated: moves and resizes don't animate, and effects become a short fade.",
                        "Reduced motion is not simulated.");
                    break;
                case "isEnabled":
                    answer = Switch(name, value, settings.IsEnabled, on => settings.IsEnabled = on,
                        "Animations are on.",
                        "Animations are off: every change happens at once.");
                    break;
                default:
                    return;
            }

            BrowserBridge.AnswerInConsole(answer);
        }

        private static string SlowMotion(AnimationSettings settings, string value)
        {
            double asked = 0;
            if (value.Length > 0)
            {
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out asked) ||
                    !(asked > 0) || double.IsInfinity(asked))
                {
                    return "slowMotion takes how many times slower the animations play, like osAnimations.slowMotion(4), " +
                           "or 1 for their normal speed.";
                }

                settings.Speed = 1 / asked;
            }

            double times = 1 / settings.Speed;
            if (Math.Abs(times - 1) < 0.005)
            {
                return asked == 0
                    ? "Animations play at their normal speed. osAnimations.slowMotion(4) makes them 4 times slower."
                    : "Animations play at their normal speed.";
            }

            string answer = times > 1
                ? $"Animations play {Number(times)} times slower"
                : $"Animations play {Number(1 / times)} times faster";
            if (asked != 0)
            {
                // Speed keeps itself between 0.05 and 20.
                if (Math.Abs(times - asked) > 0.005 * asked)
                {
                    answer += ", the most they can";
                }

                answer += ", " + Reloaded;
            }

            return answer + ". osAnimations.slowMotion(1) brings back their normal speed.";
        }

        private static string Switch(string name, string value, bool current, Action<bool> set, string on, string off)
        {
            if (value.Length == 0)
            {
                return (current ? on : off) + $" Type osAnimations.{name}({(current ? "false" : "true")}) to change this setting.";
            }

            if (!bool.TryParse(value, out bool wanted))
            {
                return $"{name} takes true or false, like osAnimations.{name}({(current ? "false" : "true")}).";
            }

            set(wanted);
            return (wanted ? on : off) + $" This setting lasts {Reloaded}.";
        }

        private static string Number(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
