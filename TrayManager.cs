using System.Drawing;
using System.Diagnostics;
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
                _notifyIcon.Text = "TypeMate - AI Writing Assistant";
                _notifyIcon.Visible = true;

                // Create context menu
                CreateContextMenu();

                // Left-click to open freestyle editor; double-click also opens freestyle
                _notifyIcon.MouseClick += OnMouseClick;
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

                // Add Freestyle editor item
                var freestyleMenuItem = new ToolStripMenuItem("Freestyle Editor");
                freestyleMenuItem.Click += OnFreestyleClick;
                contextMenu.Items.Add(freestyleMenuItem);

                // Add separator
                contextMenu.Items.Add(new ToolStripSeparator());

                // Add About item
                var aboutMenuItem = new ToolStripMenuItem("About TypeMate");
                aboutMenuItem.Click += OnAboutClick;
                contextMenu.Items.Add(aboutMenuItem);

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

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Left)
                {
                    OpenFreestyleEditor();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling tray icon click", ex);
            }
        }

        private void OnDoubleClick(object? sender, EventArgs e)
        {
            try
            {
                OpenFreestyleEditor();
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
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var aboutDialog = new AboutDialog();
                        aboutDialog.Show();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error opening about dialog", ex);

                        // Fallback to simple message box if dialog fails
                        var message =
                            "TypeMate — AI-powered writing companion\n\n" +
                            "• Press Ctrl+Alt+R to capture selected text and open the editor\n" +
                            "• Click the tray icon to open the Freestyle Editor\n\n" +
                            "GitHub: https://github.com/arsanjani/TypeMate";

                        System.Windows.Forms.MessageBox.Show(message, "About TypeMate",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error showing about message", ex);
            }
        }

        private void OpenFreestyleEditor()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var popup = new PopupWindow(string.Empty, System.Windows.Application.Current.MainWindow);
                        popup.Show();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error opening freestyle editor", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error dispatching freestyle editor open", ex);
            }
        }

        private void OnFreestyleClick(object? sender, EventArgs e)
        {
            OpenFreestyleEditor();
        }


        private Icon CreateDefaultIcon()
        {
            // Create a 16x16 rectangle icon with a blue background and centered white "T"
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                graphics.Clear(Color.Transparent);

                var backgroundColor = Color.FromArgb(33, 150, 243); // Material Design Blue (#2196F3)

                // Draw a simple rectangle background
                var rectArea = new Rectangle(0, 0, 16, 16);
                using (var brush = new SolidBrush(backgroundColor))
                {
                    graphics.FillRectangle(brush, rectArea);
                }

                // Draw the "T" centered in the rectangle
                using (var font = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    var textRect = new RectangleF(1f, 1f, 16f, 16f);
                    graphics.DrawString("T", font, Brushes.White, textRect, format);
                }
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
