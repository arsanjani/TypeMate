using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace TypeMate.Core.Notifications
{
    public enum NotificationType { Info, Success, Warning, Error }

    public class NotificationItem : INotifyPropertyChanged
    {
        public int Id { get; }
        public string Message { get; }
        public NotificationType Type { get; }
        public bool AutoDismiss { get; }

        private double _progress;
        public double ProgressValue
        {
            get => _progress;
            private set { _progress = value; OnPropertyChanged("ProgressValue"); }
        }

        private readonly TimeSpan _duration;
        private readonly DateTime _startAt;
        private readonly DispatcherTimer _dismissTimer;
        private readonly DispatcherTimer _tickTimer;

        public NotificationItem(int id, string message, NotificationType type, TimeSpan duration)
        {
            Id = id;
            Message = message;
            Type = type;
            _duration = duration;
            AutoDismiss = duration.TotalMilliseconds > 0;
            ProgressValue = 1.0;

            if (AutoDismiss)
            {
                _dismissTimer = new DispatcherTimer { Interval = duration };
                _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            } else {
                _dismissTimer = new DispatcherTimer();
                _tickTimer = new DispatcherTimer();
            }

            _startAt = DateTime.UtcNow;
        }

        public void Start()
        {
            if (AutoDismiss)
            {
                _dismissTimer.Tick += (_, _) => Dismiss();
                _tickTimer.Tick += (_, _) => Tick();
                _dismissTimer.Start();
                _tickTimer.Start();
            }
        }

        private void Tick()
        {
            double elapsed = (DateTime.UtcNow - _startAt).TotalMilliseconds;
            double total = _duration.TotalMilliseconds;
            if (total <= 0) return;
            ProgressValue = Math.Max(0, Math.Min(1, 1.0 - (elapsed / total)));
        }

        public void Pause() { _dismissTimer.Stop(); _tickTimer.Stop(); }

        internal void Dismiss()
        {
            _dismissTimer.Stop();
            _tickTimer.Stop();
            _dismissedCallback?.Invoke(this);
        }

        private Action<NotificationItem>? _dismissedCallback;
        public void OnDismissed(Action<NotificationItem> cb) => _dismissedCallback = cb;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public static class NotificationService
    {
        private static readonly ObservableCollection<NotificationItem> _queue = new();
        private static NotificationWindow? _window;
        private static int _nextId;

        public const int MaxVisible = 3;

        public static void Info(string message)   => Show(message, NotificationType.Info, TimeSpan.FromSeconds(20));
        public static void Success(string message)=> Show(message, NotificationType.Success, TimeSpan.FromSeconds(4));
        public static void Warning(string message)=> Show(message, NotificationType.Warning, TimeSpan.FromSeconds(8));
        public static void Error(string message)  => Show(message, NotificationType.Error, TimeSpan.Zero);

        public static void Dismiss(int id)
        {
            Remove(_queue.FirstOrDefault(n => n.Id == id)!);
        }

        private static void Show(string message, NotificationType type, TimeSpan duration)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                bool isError = type == NotificationType.Error;
                while (_queue.Count >= MaxVisible && !isError)
                    Remove(_queue[0]);

                int id = Interlocked.Increment(ref _nextId);
                var item = new NotificationItem(id, message, type, duration);
                item.OnDismissed(Remove);
                _queue.Add(item);
                EnsureWindow();
                item.Start();
            });
        }

        private static void Remove(NotificationItem? item)
        {
            if (item is null || !_queue.Remove(item)) return;
            if (_queue.Count == 0)
            {
                _window?.Hide();
                _window = null;
            }
        }

        public static void HideAll()
        {
            foreach (var item in _queue.ToArray()) Remove(item);
        }

        private static void EnsureWindow()
        {
            if (_window is null)
            {
                _window = new NotificationWindow(_queue);
                _window.Closed += (_, _) => _window = null;
                _window.Show();
            }
        }
    }

    public partial class NotificationWindow : Window
    {
        public NotificationWindow(ObservableCollection<NotificationItem> queue)
        {
            InitializeComponent();
            ToastList.ItemsSource = queue;
            WindowStartupLocation = WindowStartupLocation.Manual;

            var area = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                       ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            Width = Math.Min(360, (int)(area.Width * 0.25));
            Left = area.Right - Width - 16;
            Top = area.Bottom - 80;
        }

        private void OnDismissClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is NotificationItem item)
                NotificationService.Dismiss(item.Id);
        }
    }
}
