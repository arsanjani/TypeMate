using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace TypeMate
{
    public partial class App : WpfApplication
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            SetupExceptionHandlers();
            base.OnStartup(e);
            AppBootstrapper.Run(this);
            Logger.LogInfo("TypeMate startup completed");
        }

        private void SetupExceptionHandlers()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                Logger.LogError("Unhandled UI thread exception", e.Exception);
                ShowWarning("Unexpected Error", "TypeMate encountered an unexpected error.");
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Logger.LogError("Unhandled background thread exception", ex);
            };

            TaskScheduler.UnobservedTaskException += (s, e) => e.SetObserved();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.LogInfo("TypeMate shutting down...");
            Core.Notifications.NotificationService.HideAll();
            AppBootstrapper.Cleanup();
            base.OnExit(e);
        }

        private void ShowWarning(string title, string message)
        {
            Dispatcher.BeginInvoke(() =>
                System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning));
        }
    }
}
