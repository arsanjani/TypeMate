using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace TypeMate
{
    public partial class AboutDialog : Window
    {
        public AboutDialog(string? hotkeyName = null)
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                var hk = hotkeyName ?? GlobalHotkey.RegisteredName ?? "Not set";
                HotkeyTextBlock.Inlines.Clear();
                HotkeyTextBlock.Inlines.Add(new Run(hk) { FontWeight = FontWeights.SemiBold });
                HotkeyTextBlock.Inlines.Add(new Run("\r\n") { Foreground = (System.Windows.Media.Brush)FindResource("SubtitleBrush") });
                HotkeyTextBlock.Inlines.Add(new Run("Capture") { Foreground = (System.Windows.Media.Brush)FindResource("SubtitleBrush") });
            };
        }

        private void GitHubLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Try to open the GitHub URL in the default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/arsanjani/TypeMate",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening GitHub URL", ex);
                System.Windows.MessageBox.Show("Unable to open the link. Please visit: https://github.com/arsanjani/TypeMate",
                              "Error", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
