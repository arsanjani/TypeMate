using TypeMate.Core.Platform;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using WpfApplication = System.Windows.Application;
using WinWindow = System.Windows.Window;
using WinVisibility = System.Windows.Visibility;

namespace TypeMate.Services
{
    public class TrayService : IDisposable
    {
        private readonly Core.Platform.IClipboardCapture _clipboardCapture;
        private readonly WinWindow _mainWindow;
        private bool _disposed = false;
        private NotifyIcon? _notifyIcon;

        public event EventHandler? ExitRequested;

        public TrayService(IClipboardCapture clipboardCapture, WinWindow mainWindow)
        {
            _clipboardCapture = clipboardCapture;
            _mainWindow = mainWindow;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new NotifyIcon();
                _notifyIcon.Icon = CreateDefaultIcon();
                _notifyIcon.Text = "TypeMate - AI Writing Assistant";
                _notifyIcon.Visible = true;
                CreateContextMenu();
                _notifyIcon.MouseClick += OnMouseClick;
                _notifyIcon.DoubleClick += OnDoubleClick;
                Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error creating tray icon", ex);
                throw;
            }
        }

        private void CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            var freestyle = new ToolStripMenuItem("Freestyle Editor");
            freestyle.Click += (s, e) => OpenFreestyleEditor();
            menu.Items.Add(freestyle);

            menu.Items.Add(new ToolStripSeparator());

            var about = new ToolStripMenuItem("About TypeMate");
            about.Click += OnAboutClick;
            menu.Items.Add(about);

            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("Exit");
            exit.Click += OnExitClick;
            menu.Items.Add(exit);

            _notifyIcon!.ContextMenuStrip = menu;
        }

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) OpenFreestyleEditor();
        }

        private void OnDoubleClick(object? sender, EventArgs e) => OpenFreestyleEditor();

        private void OnAboutClick(object? sender, EventArgs e) => ShowAboutMessage();

        private void OnExitClick(object? sender, EventArgs e)
        {
            Logger.LogInfo("Exit requested from tray menu");

            // ContextMenuStrip runs a nested WinForms message loop on WPF's STA thread.
            // Calling Application.Shutdown() here deadlocks — WPF can't process its own
            // shutdown messages while trapped in that frame.  Use Environment.Exit(0)
            // on a background thread after cleanup to bypass the dispatcher entirely.
            ExitRequested?.Invoke(this, EventArgs.Empty);

            _ = Task.Run(() =>
            {
                AppBootstrapper.Cleanup();
                Environment.Exit(0);
            });
        }

        private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            if (e.Reason is Microsoft.Win32.SessionSwitchReason.SessionUnlock or Microsoft.Win32.SessionSwitchReason.SessionLogon)
            {
                Logger.LogInfo($"Session switch: {e.Reason}");
                if (_notifyIcon != null && !_notifyIcon.Visible) _notifyIcon.Visible = true;
            }
        }

        private void ShowAboutMessage()
        {
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                foreach (WinWindow w in WpfApplication.Current.Windows)
                    if (w is PopupWindow pw && pw.IsVisible) w.Visibility = WinVisibility.Collapsed;

                var dlg = new AboutDialog(AppBootstrapper.RegisteredHotkeyName ?? "Ctrl+Alt+R")
                { Owner = WpfApplication.Current.MainWindow, Topmost = true };
                dlg.Show();
                var handle = new System.Windows.Interop.WindowInteropHelper(dlg).Handle;
                if (handle != IntPtr.Zero) { SetForegroundWindow(handle); dlg.Activate(); }
            });
        }

        private void OpenFreestyleEditor()
        {
            WpfApplication.Current.Dispatcher.BeginInvoke(async () =>
            {
                var popup = new PopupWindow(string.Empty, _mainWindow);
                await popup.ShowAndWaitAsync();
            });
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private Icon CreateDefaultIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(33, 150, 243)))
                    g.FillRectangle(b, new Rectangle(0, 0, 16, 16));
                using var f = new Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold, GraphicsUnit.Point);
                using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("T", f, Brushes.White, new RectangleF(1f, 1f, 16f, 16f), fmt);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
                if (_notifyIcon != null) { _notifyIcon.Visible = false; _notifyIcon.Dispose(); _notifyIcon = null; }
                Logger.LogInfo("TrayService disposed");
                _disposed = true;
            }
        }
    }
}
