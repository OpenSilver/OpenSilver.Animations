using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OpenSilver.Animations.Gallery.Chapters
{
    public partial class ListsChapter : Page
    {
        private const int MaxToasts = 3;
        private const double StepSlot = 50;

        private static readonly string[] NewTasks = { "Call the bank", "Book the train", "Reply to Sam", "Buy coffee", "Plan the week", "Fix the bike", "Send the invoice", "Read a chapter" };
        private static readonly (string Title, string Text)[] ToastTexts =
        {
            ("Saved", "Your changes are saved."),
            ("New message", "Sam: are we still on for lunch?"),
            ("Upload complete", "report.pdf is ready to share."),
            ("Reminder", "Team meeting in 10 minutes."),
        };
        private static readonly (string Question, string Answer)[] Conversation =
        {
            ("What is OpenSilver?", "OpenSilver runs your XAML and C# applications in the browser, with WebAssembly. Your pages look and work the same, and you keep the skills and the code you already have."),
            ("Can lists animate?", "Yes. Set ItemsAppear, ItemsDisappear and ItemsMoveAndResize on the list, and each item animates when it comes, goes or moves, also when the list is sorted."),
            ("Is it hard to set up?", "Not at all. Pick an effect in XAML and the library does the rest: every parameter has a good default, and the log explains what animated and why."),
        };
        private static readonly string[] Customers = { "Alex Martin", "Sam Lee", "Priya Shah", "Jonas Berg", "Maria Rossi", "Tom Baker", "Yuki Sato", "Lena Novak" };

        private readonly ObservableCollection<TaskItem> _tasks = new ObservableCollection<TaskItem>();
        private readonly ObservableCollection<string> _steps = new ObservableCollection<string>();
        private readonly ObservableCollection<ChatMessage> _chat = new ObservableCollection<ChatMessage>();
        private readonly ObservableCollection<Order> _orders = new ObservableCollection<Order>();
        private readonly List<Product> _products = new List<Product>();
        private readonly Random _random = new Random(6);
        private int _newTask;
        private int _toast;
        private int _nextOrder = 1001;

        private Func<IEnumerable<Product>, IEnumerable<Product>> _order;

        private FrameworkElement _dragRow;
        private TranslateTransform _dragShift;
        private UIElement _dragContainer;
        private string _dragStep;
        private int _dragStartIndex;
        private double _dragStartY;
        private Storyboard _drop;
        private UIElement _dropContainer;

        private readonly DispatcherTimer _chatTimer = new DispatcherTimer();
        private int _question;
        private ChatMessage _answer;
        private string[] _words;
        private int _word;

        public ListsChapter()
        {
            InitializeComponent();

            foreach (string title in new[] { "Write the report", "Answer the emails", "Go for a run" })
            {
                _tasks.Add(new TaskItem(title));
            }
            _tasks[1].IsDone = true;
            Tasks.ItemsSource = _tasks;

            AddProduct("Lamp", 49, true, 0x5B5BD6);
            AddProduct("Chair", 129, true, 0x2E9E83);
            AddProduct("Rug", 89, false, 0xD9822B);
            AddProduct("Shelf", 159, true, 0x3B82C4);
            AddProduct("Clock", 35, false, 0xC2477A);
            AddProduct("Vase", 24, true, 0x7A5BD6);
            AddProduct("Desk", 249, false, 0x4F7A3A);
            AddProduct("Mirror", 69, true, 0xB0563A);
            _order = products => products;
            ShowProducts();

            foreach (string step in new[] { "1. Pick a theme", "2. Invite the guests", "3. Order the cake", "4. Decorate the room", "5. Send the photos" })
            {
                _steps.Add(step);
            }
            Steps.ItemsSource = _steps;

            _chat.Add(ChatMessage.FromAssistant("Hi! Ask me anything about OpenSilver."));
            Chat.ItemsSource = _chat;
            _chatTimer.Tick += ChatTimer_Tick;

            for (int i = 0; i < 300; i++)
            {
                _orders.Add(NewOrder());
            }
            Orders.ItemsSource = _orders;

            Unloaded += (s, e) => _chatTimer.Stop();
        }

        #region To-do list

        private void AddTask_Click(object sender, RoutedEventArgs e) => AddTask();

        private void NewTask_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddTask();
            }
        }

        private void AddTask()
        {
            string title = string.IsNullOrWhiteSpace(NewTask.Text) ? NextTaskTitle() : NewTask.Text.Trim();
            _tasks.Insert(0, new TaskItem(title));
            NewTask.Text = NextTaskTitle();
        }

        private void AddThree_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 3; i++)
            {
                _tasks.Insert(0, new TaskItem(NextTaskTitle()));
            }
        }

        private void ClearDone_Click(object sender, RoutedEventArgs e)
        {
            foreach (TaskItem task in _tasks.Where(t => t.IsDone).ToList())
            {
                _tasks.Remove(task);
            }
        }

        private void RemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is TaskItem task)
            {
                _tasks.Remove(task);
            }
        }

        private string NextTaskTitle() => NewTasks[_newTask++ % NewTasks.Length];

        #endregion

        #region Toasts

        private void ShowToast_Click(object sender, RoutedEventArgs e)
        {
            if (Toasts.Children.Count >= MaxToasts)
            {
                Toasts.Children.RemoveAt(0);
            }

            (string title, string text) = ToastTexts[_toast++ % ToastTexts.Length];
            var toast = new Border
            {
                Style = (Style)Resources["Toast"],
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = title, Foreground = new SolidColorBrush(Colors.White), FontWeight = FontWeights.SemiBold, FontSize = 14 },
                        new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromArgb(255, 0xC9, 0xCC, 0xE0)), FontSize = 13, TextWrapping = TextWrapping.Wrap },
                    },
                },
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, a) =>
            {
                timer.Stop();
                Toasts.Children.Remove(toast);
            };
            toast.MouseLeftButtonUp += (s, a) =>
            {
                timer.Stop();
                Toasts.Children.Remove(toast);
            };

            Toasts.Children.Add(toast);
            timer.Start();
        }

        #endregion

        #region Sort and filter

        private void AddProduct(string name, int price, bool inStock, int rgb)
        {
            _products.Add(new Product(name, price, inStock, Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)));
        }

        private void SortByName_Click(object sender, RoutedEventArgs e)
        {
            _order = products => products.OrderBy(p => p.Name, StringComparer.Ordinal);
            ShowProducts();
        }

        private void SortByPrice_Click(object sender, RoutedEventArgs e)
        {
            _order = products => products.OrderBy(p => p.Price);
            ShowProducts();
        }

        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            List<Product> shuffled = _products.OrderBy(p => _random.Next()).ToList();
            _order = products => shuffled.Where(products.Contains);
            ShowProducts();
        }

        private void InStockOnly_Changed(object sender, RoutedEventArgs e) => ShowProducts();

        private void ShowProducts()
        {
            IEnumerable<Product> shown = InStockOnly.IsChecked == true ? _products.Where(p => p.InStock) : _products;
            Products.ItemsSource = _order(shown).ToList();
        }

        #endregion

        #region Drag to reorder

        private void Step_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var row = (FrameworkElement)sender;
            if (_dragRow != null || row.DataContext is not string step || !row.CaptureMouse())
            {
                return;
            }

            StopDrop();
            _dragRow = row;
            _dragShift = (TranslateTransform)row.RenderTransform;
            _dragShift.Y = 0;
            _dragStep = step;
            _dragStartIndex = _steps.IndexOf(step);
            _dragStartY = e.GetPosition(Steps).Y;
            _dragContainer = Steps.ItemContainerGenerator.ContainerFromItem(step) as UIElement;
            if (_dragContainer != null)
            {
                Canvas.SetZIndex(_dragContainer, 1);
            }
            e.Handled = true;
        }

        private void Step_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragRow == null || sender != _dragRow)
            {
                return;
            }

            // The dragged step stays in its container: the steps it passes are moved around it.
            double dy = e.GetPosition(Steps).Y - _dragStartY;
            dy = Math.Max(-_dragStartIndex * StepSlot, Math.Min((_steps.Count - 1 - _dragStartIndex) * StepSlot, dy));
            int target = (int)Math.Round(_dragStartIndex + dy / StepSlot);
            int index = _steps.IndexOf(_dragStep);
            while (index < target)
            {
                _steps.Move(index + 1, index);
                index++;
            }
            while (index > target)
            {
                _steps.Move(index - 1, index);
                index--;
            }

            _dragShift.Y = dy - (index - _dragStartIndex) * StepSlot;
        }

        private void Step_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragRow != null && sender == _dragRow)
            {
                _dragRow.ReleaseMouseCapture();
                EndDrag();
            }
        }

        private void Step_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (sender == _dragRow)
            {
                EndDrag();
            }
        }

        private void EndDrag()
        {
            if (_dragRow == null)
            {
                return;
            }

            // The step settles into its place, above the others until it is there.
            var settle = new DoubleAnimation
            {
                From = _dragShift.Y,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };
            Storyboard.SetTarget(settle, _dragRow);
            Storyboard.SetTargetProperty(settle, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            var drop = new Storyboard { Children = { settle } };
            drop.Completed += (s, a) =>
            {
                if (_drop == drop)
                {
                    StopDrop();
                }
            };

            _dragShift.Y = 0;
            _dragRow = null;
            _drop = drop;
            _dropContainer = _dragContainer;
            _dragContainer = null;
            drop.Begin();
        }

        private void StopDrop()
        {
            _drop?.Stop();
            _drop = null;
            if (_dropContainer != null)
            {
                Canvas.SetZIndex(_dropContainer, 0);
                _dropContainer = null;
            }
        }

        #endregion

        #region Streaming chat

        private void Ask_Click(object sender, RoutedEventArgs e)
        {
            // Clicks wait for the answer. The button isn't disabled: when it is enabled again
            // with the mouse over it, OpenSilver leaves its Disabled look.
            if (_chatTimer.IsEnabled)
            {
                return;
            }

            // The greeting stays, with the last question and its answer.
            while (_chat.Count > 3)
            {
                _chat.RemoveAt(1);
            }

            (string question, _) = Conversation[_question % Conversation.Length];
            _chat.Add(ChatMessage.FromUser(question));
            _answer = null;
            _chatTimer.Interval = TimeSpan.FromMilliseconds(400);
            _chatTimer.Start();
        }

        private void ChatTimer_Tick(object sender, EventArgs e)
        {
            if (_answer == null)
            {
                // The answer bubble waits a moment with "…", while it settles, before the words come.
                _answer = ChatMessage.FromAssistant("…");
                _chat.Add(_answer);
                _words = Conversation[_question % Conversation.Length].Answer.Split(' ');
                _word = 0;
                _chatTimer.Interval = TimeSpan.FromMilliseconds(700);
                return;
            }

            _answer.Text = _word == 0 ? _words[0] : _answer.Text + " " + _words[_word];
            _word++;
            _chatTimer.Interval = TimeSpan.FromMilliseconds(70);
            if (_word == _words.Length)
            {
                _chatTimer.Stop();
                _question++;
            }
        }

        private void StartOver_Click(object sender, RoutedEventArgs e)
        {
            _chatTimer.Stop();
            _answer = null;
            _question = 0;
            _chat.Clear();
            _chat.Add(ChatMessage.FromAssistant("Hi! Ask me anything about OpenSilver."));
        }

        #endregion

        #region Long list

        private Order NewOrder()
        {
            int number = _nextOrder++;
            return new Order(number, Customers[_random.Next(Customers.Length)], _random.Next(12, 400) + _random.Next(0, 100) / 100.0);
        }

        private void AddOrder_Click(object sender, RoutedEventArgs e)
        {
            _orders.Insert(0, NewOrder());
        }

        private void MoveOrderToTop_Click(object sender, RoutedEventArgs e)
        {
            if (Orders.SelectedItem is Order order)
            {
                int index = _orders.IndexOf(order);
                if (index > 0)
                {
                    _orders.Move(index, 0);
                }
            }
        }

        private void RemoveOrder_Click(object sender, RoutedEventArgs e)
        {
            if (Orders.SelectedItem is Order order)
            {
                _orders.Remove(order);
            }
        }

        #endregion
    }

    public sealed class TaskItem : INotifyPropertyChanged
    {
        private bool _isDone;

        public TaskItem(string title)
        {
            Title = title;
        }

        public string Title { get; }

        public bool IsDone
        {
            get => _isDone;
            set
            {
                if (_isDone != value)
                {
                    _isDone = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDone)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString() => Title;
    }

    public sealed class Product
    {
        public Product(string name, int price, bool inStock, Color color)
        {
            Name = name;
            Price = price;
            InStock = inStock;
            Background = new SolidColorBrush(color);
        }

        public string Name { get; }

        public int Price { get; }

        public bool InStock { get; }

        public Brush Background { get; }

        public string PriceText => "$" + Price.ToString(CultureInfo.InvariantCulture);

        public string StockText => InStock ? "In stock" : "Out of stock";

        public override string ToString() => Name;
    }

    public sealed class ChatMessage : INotifyPropertyChanged
    {
        private static readonly Brush UserBrush = new SolidColorBrush(Color.FromArgb(255, 0x5B, 0x5B, 0xD6));
        private static readonly Brush AssistantBrush = new SolidColorBrush(Colors.White);
        private static readonly Brush UserTextBrush = new SolidColorBrush(Colors.White);
        private static readonly Brush AssistantTextBrush = new SolidColorBrush(Color.FromArgb(255, 0x1C, 0x1E, 0x2B));

        private string _text;

        private ChatMessage(string text, bool fromUser)
        {
            _text = text;
            Alignment = fromUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Background = fromUser ? UserBrush : AssistantBrush;
            Foreground = fromUser ? UserTextBrush : AssistantTextBrush;
            Border = fromUser ? new Thickness(0) : new Thickness(1);
        }

        public static ChatMessage FromUser(string text) => new ChatMessage(text, fromUser: true);

        public static ChatMessage FromAssistant(string text) => new ChatMessage(text, fromUser: false);

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        public HorizontalAlignment Alignment { get; }

        public Brush Background { get; }

        public Brush Foreground { get; }

        public Thickness Border { get; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class Order
    {
        public Order(int number, string customer, double amount)
        {
            Number = number;
            Customer = customer;
            Amount = amount;
        }

        public int Number { get; }

        public string Customer { get; }

        public double Amount { get; }

        public string Title => "Order #" + Number.ToString(CultureInfo.InvariantCulture);

        public string AmountText => "$" + Amount.ToString("0.00", CultureInfo.InvariantCulture);

        public override string ToString() => Title;
    }
}
