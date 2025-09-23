using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

        private void AiButton_Click(object sender, RoutedEventArgs e)
        {
            if (AiButton.ContextMenu != null)
            {
                AiButton.ContextMenu.PlacementTarget = AiButton;
                AiButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                AiButton.ContextMenu.IsOpen = true;
            }
        }

        private async void AiMenu_EasyRead_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.EasyRead);
        private async void AiMenu_Witty_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.Witty);
        private async void AiMenu_Formal_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.Formal);
        private async void AiMenu_Summarise_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.Summarise);
        private async void AiMenu_Expand_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.Expand);
        private async void AiMenu_LinkedIn_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.LinkedInPost);

        private async void AiMenu_SetApiKey_Click(object sender, RoutedEventArgs e)
        {
            await PromptForApiKeyAsync();
        }

        private async Task<bool> EnsureApiKeyAsync()
        {
            string? key = await ApiKeyStore.GetOpenAIApiKeyAsync();
            if (string.IsNullOrWhiteSpace(key))
            {
                return await PromptForApiKeyAsync();
            }
            return true;
        }

        private async Task<bool> PromptForApiKeyAsync()
        {
            ApiKeyDialog dialog = new ApiKeyDialog
            {
                Owner = this
            };
            bool? result = dialog.ShowDialog();
            await Task.CompletedTask;
            return result == true;
        }

        private async Task RewriteWithStyle(RewriteStyle style)
        {
            try
            {
                string source = TextEditor.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(source))
                {
                    System.Windows.MessageBox.Show("No text to rewrite.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool ok = await EnsureApiKeyAsync();
                if (!ok)
                {
                    return;
                }

                SetUiBusy(true);

                string? rewritten = await OpenAIService.RewriteAsync(source, style);
                if (string.IsNullOrWhiteSpace(rewritten))
                {
                    System.Windows.MessageBox.Show("Failed to rewrite. Check your API key and network.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                TextEditor.Text = rewritten;
                TextEditor.Focus();
                TextEditor.SelectAll();
            }
            catch (Exception ex)
            {
                Logger.LogError("AI rewrite error", ex);
                System.Windows.MessageBox.Show("An error occurred while rewriting.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SetUiBusy(false);
            }
        }

        private void SetUiBusy(bool isBusy)
        {
            LoadingOverlay.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            AiButton.IsEnabled = !isBusy;
            InsertButton.IsEnabled = !isBusy;
            CancelButton.IsEnabled = !isBusy;
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
                AiButton.IsEnabled = false;
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
                    AiButton.IsEnabled = true;
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
                AiButton.IsEnabled = true;
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
