using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace OpenSilver.Animations
{
    /// <summary>
    /// Settings that apply to every animation of the application. Change them in code through
    /// <see cref="Current"/>, or declare them in the resources of App.xaml:
    /// <code>&lt;anim:AnimationSettings x:Key="AnimationSettings" Speed="1" /&gt;</code>
    /// </summary>
    public sealed class AnimationSettings : INotifyPropertyChanged
    {
        /// <summary>
        /// The resource key that is looked up in the application resources at startup.
        /// </summary>
        public const string ResourceKey = "AnimationSettings";

        private static AnimationSettings _current;

        private bool _isEnabled = true;
        private bool _respectReducedMotion = true;
        private bool _simulateReducedMotion;
        private double _speed = 1;
        private AnimationTime _defaultDuration = AnimationTime.FromMilliseconds(250);
        private bool _logForDebugging;
        private bool _showWhatAnimates;

        /// <summary>
        /// Gets or sets the settings in use. The first time it is read, it comes from the
        /// <see cref="ResourceKey"/> resource of the application if there is one.
        /// </summary>
        public static AnimationSettings Current
        {
            get
            {
                if (_current is null)
                {
                    AnimationSettings fromResources = null;
                    try
                    {
                        if (Application.Current?.Resources is ResourceDictionary resources &&
                            resources.Contains(ResourceKey))
                        {
                            fromResources = resources[ResourceKey] as AnimationSettings;
                        }
                    }
                    catch
                    {
                        // The resources are not available yet: use the defaults.
                    }

                    if (fromResources is null && Application.Current is null)
                    {
                        return Defaults;
                    }

                    _current = fromResources ?? new AnimationSettings();
                }

                return _current;
            }
            set
            {
                _current = value ?? throw new ArgumentNullException(nameof(value));
                Engine.Reactions.OnSettingsReplaced();
            }
        }

        private static AnimationSettings Defaults { get; } = new AnimationSettings();

        /// <summary>
        /// Turns all animations on or off. The default is true.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => Set(ref _isEnabled, value);
        }

        /// <summary>
        /// When the user asked their device for less motion, moves and resizes don't animate, and
        /// effects become a short fade. The default is true.
        /// </summary>
        public bool RespectReducedMotion
        {
            get => _respectReducedMotion;
            set => Set(ref _respectReducedMotion, value);
        }

        /// <summary>
        /// Behaves as if the user had asked their device for less motion. Useful to test it.
        /// The default is false.
        /// </summary>
        public bool SimulateReducedMotion
        {
            get => _simulateReducedMotion;
            set => Set(ref _simulateReducedMotion, value);
        }

        /// <summary>
        /// Plays every animation faster or slower: 2 is twice as fast, 0.5 is half as fast.
        /// The default is 1.
        /// </summary>
        public double Speed
        {
            get => _speed;
            set => Set(ref _speed, double.IsNaN(value) ? 1 : Math.Max(0.05, Math.Min(20, value)));
        }

        /// <summary>
        /// The duration of animations that don't set one. The default is 0.25 seconds.
        /// </summary>
        public AnimationTime DefaultDuration
        {
            get => _defaultDuration;
            set => Set(ref _defaultDuration, value);
        }

        /// <summary>
        /// Writes to the browser console, in plain language, why each change animated or not.
        /// The same messages are raised as the <see cref="Logged"/> event. The default is false.
        /// </summary>
        public bool LogForDebugging
        {
            get => _logForDebugging;
            set => Set(ref _logForDebugging, value);
        }

        /// <summary>
        /// Outlines the elements that animate together while they animate, with the element
        /// where the change started in another color. The default is false.
        /// </summary>
        public bool ShowWhatAnimates
        {
            get => _showWhatAnimates;
            set => Set(ref _showWhatAnimates, value);
        }

        /// <summary>
        /// Occurs when the library explains why a change animated or not.
        /// </summary>
        public static event EventHandler<AnimationLoggedEventArgs> Logged;

        internal static bool HasLoggedHandlers => Logged != null;

        internal static void RaiseLogged(string message)
            => Logged?.Invoke(null, new AnimationLoggedEventArgs(message));

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    /// <summary>
    /// Provides data for the <see cref="AnimationSettings.Logged"/> event.
    /// </summary>
    public sealed class AnimationLoggedEventArgs : EventArgs
    {
        internal AnimationLoggedEventArgs(string message)
        {
            Message = message;
            Time = DateTime.Now;
        }

        /// <summary>
        /// The explanation, for example "SaveButton changed size; 3 elements moved with it".
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// When the message was written.
        /// </summary>
        public DateTime Time { get; }
    }
}
