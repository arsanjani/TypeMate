using System.Windows;
using WpfApplication = System.Windows.Application;
using TypeMate.Core.AI;
using TypeMate.Core.Config;
using TypeMate.Core.Platform;

namespace TypeMate
{
    public static class AppBootstrapper
    {
        private static IClipboardCapture? _clipboardCapture;
        private static IHotkeyManager? _hotkeyManager;
        private static Services.TrayService? _trayService;
        private static Rewriter? _rewriter;
        private static Window? _mainWindow;

        public static string? RegisteredHotkeyName { get; private set; }

        public static void Run(WpfApplication app)
        {
            SetupMainWindow(app);
            SetupPlatformServices();
            bool hotkeyOk = RegisterHotkey(app);
            if (!hotkeyOk) Shutdown();
        }

        private static void SetupMainWindow(WpfApplication app)
        {
            _mainWindow = app.MainWindow ?? new MainWindow();
            _mainWindow.WindowState = WindowState.Minimized;
            _mainWindow.ShowInTaskbar = false;
            _mainWindow.Visibility = Visibility.Hidden;
        }

        private static void SetupPlatformServices()
        {
            _clipboardCapture = new ClipboardCapture();
            var configStore = new JsonConfigStore();
            _rewriter = new Rewriter(configStore,
                new OpenAIProvider(), new GeminiProvider(),
                new OllamaProvider(), new OpenRouterProvider());
        }

        private static bool RegisterHotkey(WpfApplication app)
        {
            Hotcode primary = new(0x0003, 0x52, "Ctrl+Alt+R");
            Hotcode[] fallbacks = new[]
            {
                new Hotcode(0x0003, 0x54, "Ctrl+Alt+T"),
                new Hotcode(0x0003, 0x59, "Ctrl+Alt+Y"),
                new Hotcode(0x0003, 0x49, "Ctrl+Alt+I"),
            };

            // Build ordered list: primary first, then all fallbacks in priority order
            Hotcode[] ordered = new Hotcode[1 + (fallbacks?.Length ?? 0)];
            ordered[0] = primary;
            for (int i = 0; i < (fallbacks?.Length ?? 0)!; i++)
                ordered[i + 1] = fallbacks![i];

            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += (s, e) => OnHotkeyPressed();
            bool success = _hotkeyManager.Register(_mainWindow!, ordered);

            if (!success)
            {
                Logger.LogWarning("All hot keys conflicted — TypeMate will not respond to shortcuts until restarted");
                return false;
            }

            string assigned = _hotkeyManager.RegisteredShortcut ?? "None";
            RegisteredHotkeyName = assigned;

            if (assigned != primary.Name)
            {
                Logger.LogInfo("Primary hotkey unavailable — assigned fallback: " + assigned);
                app.Dispatcher.BeginInvoke((Action)(() => System.Windows.MessageBox.Show(
                    "The default shortcut " + primary.Name + " is already in use by another application." + Environment.NewLine +
                    "TypeMate has been assigned: " + assigned + Environment.NewLine + Environment.NewLine +
                    "Please use this key combination instead.",
                    "Shortcut Changed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information)));
            }
            else
            {
                Logger.LogInfo("Global hotkey registered successfully: " + assigned);
            }

            _trayService = new Services.TrayService(_clipboardCapture!, WpfApplication.Current.MainWindow ?? _mainWindow!);
            _trayService.ExitRequested += (s, e) => Shutdown();
            return true;
        }

        private static void OnHotkeyPressed()
        {
            var cb = _clipboardCapture;
            var rw = _rewriter;
            var mw = _mainWindow;
            if (cb is null || rw is null) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    string? capturedText = await cb.CaptureAsync();
                    if (!string.IsNullOrWhiteSpace(capturedText) && rw is not null)
                    {
                        await WpfApplication.Current.Dispatcher.InvokeAsync(
                            () => new PopupWindow(capturedText, mw!).Show());
                    }
                }
                catch (Exception ex) { Logger.LogError("Error handling hotkey press", ex); }
            });
        }

        public static void Shutdown() => WpfApplication.Current.Shutdown();

        public static void Cleanup()
        {
            _hotkeyManager?.Dispose();
            _trayService?.Dispose();
        }
    }
}
