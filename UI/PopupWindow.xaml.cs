using TypeMate.Core.AI;
using TypeMate.Core.Config;
using TypeMate.Core.DI;
using TypeMate.Core.Notifications;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TypeMate
{
    public partial class PopupWindow : Window
    {
        private static Rewriter Rewriter => ServiceContainer.Instance.Rewriter;
        private static Core.Platform.IClipboardCapture Clipboard => ServiceContainer.Instance.Clipboard;

        private bool isRTL = false;

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
        private async void AiMenu_TwitterPost_Click(object sender, RoutedEventArgs e) => await RewriteWithStyle(RewriteStyle.TwitterPost);
        private async void AiMenu_SetApiKey_Click(object sender, RoutedEventArgs e)
        {
            await PromptForApiKeyAsync();
        }

        private async Task<bool> EnsureApiKeyAsync()
        {
            var configStore = new JsonConfigStore();
            var config = await configStore.GetAsync();
            if (config?.Provider == "ollama") return true;

            string? key = config?.EncryptedOpenAIApiKeyBase64 is { Length: > 0 }
                ? AppConfig.DecryptBase64(config.EncryptedOpenAIApiKeyBase64)
                : config?.EncryptedGeminiApiKeyBase64 is { Length: > 0 }
                    ? AppConfig.DecryptBase64(config.EncryptedGeminiApiKeyBase64)
                    : config?.EncryptedOpenRouterApiKeyBase64 is { Length: > 0 }
                        ? AppConfig.DecryptBase64(config.EncryptedOpenRouterApiKeyBase64)
                        : null;
            if (!string.IsNullOrWhiteSpace(key)) return true;

            return await PromptForApiKeyAsync();
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
                    NotificationService.Warning("No text selected to rewrite.");
                    return;
                }

                bool ok = await EnsureApiKeyAsync();
                if (!ok)
                {
                    return;
                }

                SetUiBusy(true);

                string? rewritten = await Rewriter.RewriteAsync(source, style);
                if (string.IsNullOrWhiteSpace(rewritten))
                {
                    NotificationService.Warning("Rewrite failed. Check your API key and network.");
                    return;
                }

                TextEditor.Text = rewritten;
                TextEditor.Focus();
                TextEditor.SelectAll();
            }
            catch (Exception ex)
            {
                Logger.LogError("AI rewrite error", ex);
                NotificationService.Error(ex.Message);
            }
            finally
            {
                SetUiBusy(false);
            }
        }

        private void SetUiBusy(bool isBusy)
        {
            LoadingOverlay.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            RtlButton.IsEnabled = !isBusy;
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
                if (!await Clipboard.SetClipboardText(textToInsert))
                {
                    NotificationService.Error("Failed to copy text to clipboard.");
                    SetUiBusy(false);
                    return;
                }

                this.Close();

                _ = Task.Run(async () =>
                {
                    await Task.Delay(300);
                    try { await Clipboard.SendPasteAsync(); }
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

        private void RtlButton_Click(object sender, RoutedEventArgs e)
        {
            isRTL = !isRTL;
            TextEditor.FlowDirection = isRTL ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
            if (RtlIcon != null)
            {
                RtlIcon.Text = isRTL ? "\u2190" : "\u2194";
                RtlIcon.Foreground = isRTL ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 99, 102, 241)) : new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 148, 163, 184));
            }
        }
    }
}
