using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace TypeMate
{
    public partial class App : WpfApplication
    {
        private TrayManager? _trayManager;
        private GlobalHotkey? _globalHotkey;
        private bool _isShuttingDown = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                Logger.LogInfo("TypeMate starting up...");
                
                // Set up global exception handlers
                SetupExceptionHandlers();
                
                base.OnStartup(e);

                // Initialize components with error handling
                InitializeApplication();
                
                Logger.LogInfo("TypeMate startup completed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Critical error during startup", ex);
                ShowCriticalError("Failed to start TypeMate", ex);
                Shutdown(1);
            }
        }

        private void SetupExceptionHandlers()
        {
            // Handle unhandled exceptions in UI thread
            DispatcherUnhandledException += (sender, e) =>
            {
                Logger.LogError("Unhandled UI thread exception", e.Exception);
                HandleUnhandledException(e.Exception);
                e.Handled = true; // Prevent application crash
            };

            // Handle unhandled exceptions in background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                Logger.LogError("Unhandled background thread exception", exception);
                
                if (e.IsTerminating)
                {
                    Logger.LogError("Application is terminating due to unhandled exception");
                }
            };

            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Logger.LogError("Unobserved task exception", e.Exception);
                e.SetObserved(); // Prevent application crash
            };
        }

        private void InitializeApplication()
        {
            // Ensure MainWindow is created and properly configured
            if (MainWindow == null)
            {
                Logger.LogInfo("MainWindow is null; creating hidden MainWindow instance for hotkey registration");
                MainWindow = new MainWindow();
            }

            MainWindow.WindowState = WindowState.Minimized;
            MainWindow.ShowInTaskbar = false;
            MainWindow.Visibility = Visibility.Hidden;

            // Initialize global hotkey with retry logic
            InitializeGlobalHotkey();

            // Initialize tray manager
            InitializeTrayManager();
        }

        private void InitializeGlobalHotkey()
        {
            try
            {
                Logger.LogInfo("Initializing global hotkey system...");
                
                _globalHotkey = new GlobalHotkey();
                _globalHotkey.HotkeyPressed += OnHotkeyPressed;
                
                // Try to register Ctrl+Alt+R first
                bool success = _globalHotkey.RegisterHotkey(MainWindow!, 0x0003, 0x52); // MOD_CONTROL | MOD_ALT, VK_R
                
                if (success)
                {
                    Logger.LogInfo("Global hotkey (Ctrl+Alt+R) registered successfully");
                }
                else
                {
                    Logger.LogWarning("Primary hotkey (Ctrl+Alt+R) registration failed, trying alternatives...");
                    
                    // Dispose the failed hotkey instance
                    _globalHotkey.Dispose();
                    
                    // Try alternative hotkey combinations
                    if (GlobalHotkey.TryRegisterAlternativeHotkey(MainWindow!, out _globalHotkey))
                    {
                        _globalHotkey!.HotkeyPressed += OnHotkeyPressed;
                        Logger.LogInfo("Alternative hotkey registered successfully");
                        ShowWarning("Hotkey Registration", 
                            "Ctrl+Alt+R was not available, but an alternative hotkey was registered. " +
                            "Check the application logs for details on which hotkey is active.");
                    }
                    else
                    {
                        Logger.LogError("All hotkey registration attempts failed");
                        ShowWarning("Hotkey Registration", 
                            "Could not register any hotkey combination. They may be in use by other applications. " +
                            "TypeMate will continue running, but hotkey functionality will not be available. " +
                            "Try closing other applications that might be using global hotkeys and restart TypeMate.");
                        _globalHotkey = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing global hotkey", ex);
                ShowWarning("Hotkey Error", "Failed to set up global hotkey. TypeMate will continue running without hotkey support.");
                _globalHotkey = null;
            }
        }

        private void InitializeTrayManager()
        {
            try
            {
                _trayManager = new TrayManager();
                _trayManager.ExitRequested += OnExitRequested;
                Logger.LogInfo("Tray manager initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing tray manager", ex);
                ShowCriticalError("Failed to initialize system tray", ex);
                Shutdown(1);
            }
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            if (_isShuttingDown) return;

            try
            {
                Logger.LogInfo("Hotkey pressed - starting text capture");
                ClipboardManager.CaptureSelectedText();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling hotkey press", ex);
                ShowWarning("Hotkey Error", "An error occurred while processing the hotkey. Please try again.");
            }
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            try
            {
                Logger.LogInfo("Exit requested from tray menu");
                Shutdown();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during shutdown", ex);
                Environment.Exit(0); // Force exit if normal shutdown fails
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _isShuttingDown = true;
                Logger.LogInfo("TypeMate shutting down...");
                
                _globalHotkey?.Dispose();
                _trayManager?.Dispose();
                
                Logger.LogInfo("TypeMate shutdown completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during application exit", ex);
            }
            finally
            {
                base.OnExit(e);
            }
        }

        private void HandleUnhandledException(Exception exception)
        {
            try
            {
                ShowWarning("Unexpected Error", 
                    "TypeMate encountered an unexpected error but will continue running. " +
                    "If this problem persists, please restart the application.");
            }
            catch
            {
                // If we can't even show a message, just log and continue
                Logger.LogError("Failed to show error message to user");
            }
        }

        private void ShowWarning(string title, string message)
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show warning message: {title}", ex);
            }
        }

        private void ShowCriticalError(string title, Exception exception)
        {
            try
            {
                var message = $"{title}\n\nError: {exception.Message}\n\nTypeMate will now exit.";
                System.Windows.MessageBox.Show(message, "TypeMate Critical Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show critical error message: {title}", ex);
            }
        }
    }
}
