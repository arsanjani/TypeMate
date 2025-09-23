using System.Drawing;
using System.Windows.Forms;

namespace TypeMate
{
    public class TrayManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private bool _disposed = false;

        public event EventHandler? ExitRequested;

        public TrayManager()
        {
            try
            {
                InitializeTrayIcon();
                Logger.LogInfo("TrayManager initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing TrayManager", ex);
                throw;
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new NotifyIcon();
                
                // Create a simple icon (you can replace this with a proper icon file)
                _notifyIcon.Icon = CreateDefaultIcon();
                _notifyIcon.Text = "TypeMate - Text Rewriting Tool (Ctrl+Alt+R)";
                _notifyIcon.Visible = true;

                // Create context menu
                CreateContextMenu();

                // Double-click to show info
                _notifyIcon.DoubleClick += OnDoubleClick;
                
                // Handle potential icon recreation on Windows session changes
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
            try
            {
                var contextMenu = new ContextMenuStrip();
                
                // Add About item
                var aboutMenuItem = new ToolStripMenuItem("About TypeMate");
                aboutMenuItem.Click += OnAboutClick;
                contextMenu.Items.Add(aboutMenuItem);
                
                // Add separator
                contextMenu.Items.Add(new ToolStripSeparator());
                
                // Add Exit item
                var exitMenuItem = new ToolStripMenuItem("Exit");
                exitMenuItem.Click += OnExitClick;
                contextMenu.Items.Add(exitMenuItem);
                
                if (_notifyIcon != null)
                {
                    _notifyIcon.ContextMenuStrip = contextMenu;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error creating context menu", ex);
                throw;
            }
        }

        private void OnDoubleClick(object? sender, EventArgs e)
        {
            try
            {
                ShowAboutMessage();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling tray icon double-click", ex);
            }
        }

        private void OnAboutClick(object? sender, EventArgs e)
        {
            try
            {
                ShowAboutMessage();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling about menu click", ex);
            }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            try
            {
                Logger.LogInfo("Exit requested from tray menu");
                ExitRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling exit menu click", ex);
            }
        }

        private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            try
            {
                // Recreate tray icon after session switch (e.g., unlock, logon)
                if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock ||
                    e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLogon)
                {
                    Logger.LogInfo($"Session switch detected: {e.Reason}");
                    
                    // Ensure tray icon is still visible
                    if (_notifyIcon != null && !_notifyIcon.Visible)
                    {
                        _notifyIcon.Visible = true;
                        Logger.LogInfo("Tray icon visibility restored");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling session switch", ex);
            }
        }

        private void ShowAboutMessage()
        {
            try
            {
                var message = "TypeMate - Text Rewriting Tool\n\n" +
                             "Press Ctrl+Alt+R to capture and rewrite selected text.\n\n" +
                             "Running in background...";
                
                System.Windows.Forms.MessageBox.Show(message, "TypeMate", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error showing about message", ex);
            }
        }

        private Icon CreateDefaultIcon()
        {
            // Create a simple 16x16 icon with a "T" character
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Blue);
                graphics.DrawString("T", new Font("Arial", 10, FontStyle.Bold),
                                  Brushes.White, new PointF(2, 1));
            }
            
            return Icon.FromHandle(bitmap.GetHicon());
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // Unsubscribe from session events
                    Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
                    
                    // Dispose tray icon
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                        _notifyIcon = null;
                    }
                    
                    Logger.LogInfo("TrayManager disposed successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error disposing TrayManager", ex);
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
