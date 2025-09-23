using System;
using System.Diagnostics;
using System.Windows;

namespace TypeMate
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
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
