using System.Windows;

namespace TypeMate
{
    public partial class PopupWindow : Window
    {
        public PopupWindow(string selectedText)
        {
            InitializeComponent();
            TextEditor.Text = selectedText;
            TextEditor.Focus();
            TextEditor.SelectAll();
        }

        private void RewriteButton_Click(object sender, RoutedEventArgs e)
        {
            // MVP implementation: prepend [Rewritten] to the text
            string originalText = TextEditor.Text;
            if (!originalText.StartsWith("[Rewritten] "))
            {
                TextEditor.Text = "[Rewritten] " + originalText;
            }
        }

        private async void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string textToInsert = TextEditor.Text;
                
                if (string.IsNullOrWhiteSpace(textToInsert))
                {
                    Logger.LogWarning("No text to insert - text editor is empty");
                    return;
                }

                Logger.LogInfo($"Attempting to insert {textToInsert.Length} characters");

                // Disable buttons to prevent multiple clicks
                InsertButton.IsEnabled = false;
                RewriteButton.IsEnabled = false;
                CancelButton.IsEnabled = false;

                // Set clipboard with the modified text
                bool clipboardSet = await ClipboardManager.SetClipboardText(textToInsert);
                
                if (!clipboardSet)
                {
                    Logger.LogWarning("Failed to set clipboard text");
                    System.Windows.MessageBox.Show("Failed to copy text to clipboard. Please try again.", 
                        "TypeMate Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    
                    // Re-enable buttons
                    InsertButton.IsEnabled = true;
                    RewriteButton.IsEnabled = true;
                    CancelButton.IsEnabled = true;
                    return;
                }
                
                // Close popup
                this.Close();
                
                // Wait a moment for the popup to close, then paste
                await Task.Delay(300);
                
                bool pasteSuccess = await ClipboardManager.SendCtrlV();
                if (!pasteSuccess)
                {
                    Logger.LogWarning("Failed to send Ctrl+V");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in InsertButton_Click", ex);
                System.Windows.MessageBox.Show("An error occurred while inserting text. Please try again.", 
                    "TypeMate Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // Re-enable buttons
                InsertButton.IsEnabled = true;
                RewriteButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // Handle Escape key to close
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}
