using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TypeMate
{
    public partial class PopupWindow : Window
    {
        public PopupWindow(string selectedText, Window owner)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.Manual;
            InitializeComponent();
            if (owner != null)
            {
                Left = owner.Left + (owner.Width - Width) / 2;
                Top = owner.Top + (owner.Height - Height) / 2;
            }
            TextEditor.Text = selectedText;
            AnimateIn();
            TextEditor.Focus();
            TextEditor.SelectAll();
        }

        private void AnimateIn()
        {
            var fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromSeconds(0.25), DecelerationRatio = 0.8 };
            BeginAnimation(OpacityProperty, fade);
            var slide = new DoubleAnimation { From = 30, To = 0, Duration = TimeSpan.FromSeconds(0.25), DecelerationRatio = 0.8 };
            WindowTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var fadeOut = new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(0.15) };
            BeginAnimation(OpacityProperty, fadeOut);
            Task.Delay(150).ContinueWith(_ => Dispatcher.Invoke(() => base.OnClosing(e)));
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
        private async void AiMenu_PromptOptimizer_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.PromptOptimizer);
        private async void AiMenu_EnglishToFarsi_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.EnglishToFarsi);
        private async void AiMenu_FarsiToEnglish_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.FarsiToEnglish);
        private async void AiMenu_SetApiKey_Click(object sender, RoutedEventArgs e)
        {
            await PromptForApiKeyAsync();
        }

        private async Task<bool> EnsureApiKeyAsync()
        {
            string? provider = await ApiKeyStore.GetProviderAsync();
            if (string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

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
                string textToInsert = TextEditor.Text;
                if (string.IsNullOrWhiteSpace(textToInsert)) return;

                Logger.LogInfo($"Attempting to insert {textToInsert.Length} characters");
                SetUiBusy(true);

                // Set clipboard BEFORE closing the window to avoid Application context issues
                if (!await ClipboardManager.SetClipboardText(textToInsert))
                {
                    System.Windows.MessageBox.Show("Failed to copy text to clipboard. Please try again.",
                        "TypeMate Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetUiBusy(false);
                    return;
                }

                this.Close();

                _ = Task.Run(async () =>
                {
                    await Task.Delay(300);
                    try { await ClipboardManager.SendCtrlV(); }
                    catch (Exception ex) { Logger.LogError("Paste failed", ex); }
                });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape) { this.Close(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e) => DragMove();
    }
}
