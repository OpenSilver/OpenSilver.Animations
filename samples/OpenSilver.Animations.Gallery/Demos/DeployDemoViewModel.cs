using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OpenSilver.Animations.Gallery.Demos
{
    /// <summary>
    /// The state of the deploy demo on the Welcome page. Each command only changes the state, and
    /// nothing runs on a timer: the XAML shows each state with bindings, and every animation is an
    /// anim: attribute there.
    /// </summary>
    public sealed class DeployDemoViewModel : INotifyPropertyChanged
    {
        private bool _isIdle = true;
        private bool _isConfiguring;
        private bool _isConfirming;
        private bool _isLive;
        private bool _isOverviewTab = true;
        private DeployTarget _target;
        private Commit _commit;
        private TrafficRange _range;
        private string _buildTime = "0 s";
        private string _testCount = "0";
        private string _latency = "0 ms";

        public DeployDemoViewModel()
        {
            Targets = new[]
            {
                new DeployTarget("Preview", "#38BDF8", "pr-218.northwind.dev", "1 region · 2 instances"),
                new DeployTarget("Staging", "#8B7CFF", "staging.northwind.dev", "2 regions · 4 instances"),
                new DeployTarget("Production", "#FB7185", "shop.northwind.dev", "6 regions · 48 instances"),
            };
            Commits = new[]
            {
                new Commit("feat: one-tap checkout", "a41f9c2", "Maya", "#F472B6", "12 min ago"),
                new Commit("fix: cart total rounding on iOS", "7c03e1d", "Leo", "#34D399", "1 h ago"),
                new Commit("perf: lazy-load product images", "e19b442", "Ana", "#FBBF24", "3 h ago"),
            };
            Checks = new[]
            {
                new Check("Unit tests", "1,284 passed"),
                new Check("End-to-end", "64 passed"),
                new Check("Lighthouse", "98 / 100"),
                new Check("Bundle size", "−12.4 KB"),
            };
            Ranges = new[]
            {
                new TrafficRange("1 h", "60 min ago", "2,431", "0.02%", 36, 4,
                    26, 38, 33, 45, 40, 52, 44, 56, 48, 58, 62, 68, 64, 74),
                new TrafficRange("24 h", "24 h ago", "1,874", "0.04%", 30, 2,
                    34, 24, 16, 12, 14, 22, 38, 56, 66, 60, 70, 64, 52, 58),
                new TrafficRange("7 d", "7 days ago", "1,652", "0.05%", 28, 1,
                    48, 52, 46, 58, 54, 30, 26, 50, 56, 49, 62, 57, 34, 66),
            };
            Bars = Enumerable.Range(0, 14).Select(_ => new TrafficBar()).ToArray();
            _target = Targets[1];
            Range = Ranges[0];

            Open = new RelayCommand(OpenCard);
            Close = new RelayCommand(() => Show(idle: true));
            Review = new RelayCommand(() => Show(confirming: true));
            Cancel = new RelayCommand(() => Show(configuring: true));
            Ship = new RelayCommand(ShipIt);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DeployTarget[] Targets { get; }

        public Commit[] Commits { get; }

        public Check[] Checks { get; }

        public TrafficRange[] Ranges { get; }

        public TrafficBar[] Bars { get; }

        public ICommand Open { get; }

        public ICommand Close { get; }

        public ICommand Review { get; }

        public ICommand Cancel { get; }

        public ICommand Ship { get; }

        public bool IsIdle { get => _isIdle; private set => Set(ref _isIdle, value); }

        public bool IsConfiguring { get => _isConfiguring; private set => Set(ref _isConfiguring, value); }

        public bool IsConfirming { get => _isConfirming; private set => Set(ref _isConfirming, value); }

        public bool IsLive { get => _isLive; private set => Set(ref _isLive, value); }

        public bool IsOverviewTab { get => _isOverviewTab; set => Set(ref _isOverviewTab, value); }

        public DeployTarget Target { get => _target; set => Set(ref _target, value); }

        public Commit Commit
        {
            get => _commit;
            set
            {
                if (Set(ref _commit, value))
                {
                    OnPropertyChanged(nameof(HasCommit));
                }
            }
        }

        public bool HasCommit => _commit != null;

        public TrafficRange Range
        {
            get => _range;
            set
            {
                if (value != null && Set(ref _range, value))
                {
                    for (int i = 0; i < Bars.Length; i++)
                    {
                        Bars[i].Show(value, i);
                    }
                }
            }
        }

        public string BuildTime { get => _buildTime; private set => Set(ref _buildTime, value); }

        public string TestCount { get => _testCount; private set => Set(ref _testCount, value); }

        public string Latency { get => _latency; private set => Set(ref _latency, value); }

        private void Show(bool idle = false, bool configuring = false, bool confirming = false, bool live = false)
        {
            IsIdle = idle;
            IsConfiguring = configuring;
            IsConfirming = confirming;
            IsLive = live;
        }

        private void OpenCard()
        {
            // While the live card is hidden, its numbers go back to zero, to count up next time
            BuildTime = "0 s";
            TestCount = "0";
            Latency = "0 ms";
            Commit = null;
            Show(configuring: true);
        }

        private void ShipIt()
        {
            IsOverviewTab = true;
            Range = Ranges[0];
            Show(live: true);
            BuildTime = "48 s";
            TestCount = "1,284";
            Latency = "82 ms";
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class DeployTarget
    {
        public DeployTarget(string name, string accent, string url, string details)
        {
            Name = name;
            Accent = accent;
            Url = url;
            Details = details;
        }

        public string Name { get; }

        public string Accent { get; }

        public string Url { get; }

        public string Details { get; }

        public bool IsProduction => Name == "Production";
    }

    public sealed class Commit
    {
        public Commit(string message, string hash, string author, string color, string time)
        {
            Message = message;
            Hash = hash;
            Author = author;
            Color = color;
            Time = time;
        }

        public string Message { get; }

        public string Hash { get; }

        public string Author { get; }

        public string Initial => Author.Substring(0, 1);

        public string Color { get; }

        public string Time { get; }

        public string Details => $"{Hash} · {Author} · {Time}";
    }

    public sealed class Check
    {
        public Check(string name, string result)
        {
            Name = name;
            Result = result;
        }

        public string Name { get; }

        public string Result { get; }
    }

    /// <summary>
    /// The requests per minute over a period, in 14 bars. The last ones, in green, are the new release.
    /// </summary>
    public sealed class TrafficRange
    {
        public TrafficRange(string name, string since, string requests, string errors, int requestsPerPixel, int newBars, params int[] heights)
        {
            Name = name;
            Since = since;
            Requests = requests;
            Errors = errors;
            RequestsPerPixel = requestsPerPixel;
            NewBars = newBars;
            Heights = heights;
        }

        public string Name { get; }

        public string Since { get; }

        public string Requests { get; }

        public string Errors { get; }

        public int RequestsPerPixel { get; }

        public int NewBars { get; }

        public int[] Heights { get; }
    }

    public sealed class TrafficBar : INotifyPropertyChanged
    {
        private double _height;
        private string _color;
        private string _label;

        public event PropertyChangedEventHandler PropertyChanged;

        public double Height { get => _height; private set => Set(ref _height, value); }

        public string Color { get => _color; private set => Set(ref _color, value); }

        public string Label { get => _label; private set => Set(ref _label, value); }

        public void Show(TrafficRange range, int index)
        {
            int height = range.Heights[index];
            Height = height;
            Color = index >= range.Heights.Length - range.NewBars ? "#34D399" : "#8B7CFF";
            Label = (height * range.RequestsPerPixel / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "k";
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;

        public event EventHandler CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _execute();
    }
}
