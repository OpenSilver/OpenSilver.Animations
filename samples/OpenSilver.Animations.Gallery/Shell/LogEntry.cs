using System;

namespace OpenSilver.Animations.Gallery.Shell
{
    /// <summary>
    /// A message of the "What just happened" panel.
    /// </summary>
    public sealed class LogEntry
    {
        public LogEntry(string message, DateTime time)
        {
            Message = message;
            Time = time.ToString("HH:mm:ss");
        }

        public string Message { get; }

        public string Time { get; }
    }
}
