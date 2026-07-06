using System.Windows;
using TypeMate.Core.DI;
using TypeMate.Core.Notifications;
using TypeMate.Core.Platform;
using WpfApp = System.Windows.Application;

namespace TypeMate
{
    public static class AppBootstrapper
    {
        private static readonly ServiceContainer DI = ServiceContainer.Instance;
        private static Window? _mainWindow;

        public static string? RegisteredHotkeyName { get; private set; }

        public static void Run(WpfApp app)
        {
            _mainWindow = SetupMainWindow(app);
            if (!RegisterHotkey()) Shutdown();
        }

        private static Window SetupMainWindow(WpfApp app)
        {
            var window = app.MainWindow ?? new MainWindow();
            window.WindowState = WindowState.Minimized;
            window.ShowInTaskbar = false;
            window.Visibility = Visibility.Hidden;
            return window;
        }

        private static bool RegisterHotkey()
        {
            var primary = new Hotcode(0x0003, 0x52, "Ctrl+Alt+R");
            var ordered = CreateHotkeyArray(primary);

            DI.Hotkey = new HotkeyManager();
            DI.Hotkey.HotkeyPressed += (_, _) => OnHotkeyPressed();
            if (!DI.Hotkey.Register(_mainWindow!, ordered))
            {
                Logger.LogWarning("All hot keys conflicted — TypeMate will not respond to shortcuts until restarted");
                return false;
            }

            string assigned = DI.Hotkey.RegisteredShortcut ?? "None";
            RegisteredHotkeyName = assigned;

            if (assigned != primary.Name)
            {
                Logger.LogInfo($"Primary hotkey unavailable — assigned fallback: {assigned}");
                NotificationService.Info($"Default shortcut {primary.Name} is already in use. TypeMate assigned: {assigned}");
            }
            else
            {
                Logger.LogInfo("Global hotkey registered successfully: " + assigned);
            }

            DI.Tray = new Services.TrayService(DI.Clipboard, WpfApp.Current.MainWindow ?? _mainWindow!);
            DI.Tray.ExitRequested += (_, _) => Shutdown();
            return true;
        }

        private static Hotcode[] CreateHotkeyArray(Hotcode primary)
        {
            var fallbacks = new[]
            {
                new Hotcode(0x0003, 0x54, "Ctrl+Alt+T"),
                new Hotcode(0x0003, 0x59, "Ctrl+Alt+Y"),
                new Hotcode(0x0003, 0x49, "Ctrl+Alt+I"),
            };

            var ordered = new Hotcode[1 + fallbacks.Length];
            ordered[0] = primary;
            for (int i = 0; i < fallbacks.Length; i++) ordered[i + 1] = fallbacks[i];
            return ordered;
        }

        private static void OnHotkeyPressed()
        {
            var cb = DI.Clipboard;
            var mw = _mainWindow;
            if (cb is null) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    string? text = await cb.CaptureAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                        await WpfApp.Current.Dispatcher.InvokeAsync(() => new PopupWindow(text, mw!).Show());
                }
                catch (Exception ex) { Logger.LogError("Error handling hotkey press", ex); }
            });
        }

        public static void Shutdown() => WpfApp.Current.Shutdown();

        public static void Cleanup() => ServiceContainer.Instance.Dispose();
    }
}
