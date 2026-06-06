using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace TypeMate
{
	public partial class ApiKeyDialog : Window
	{
		public string? SavedKey { get; private set; }
		public string? SavedModel { get; private set; }

		public ApiKeyDialog()
		{
			InitializeComponent();
			ModelComboBox.SelectedIndex = 1;
			Loaded += ApiKeyDialog_Loaded;
			ApiKeyBox.PasswordChanged += ApiKeyBox_PasswordChanged;
		}

		private async void ApiKeyDialog_Loaded(object sender, RoutedEventArgs e)
		{
			AnimateIn();
			await LoadCurrentSettingsAsync();
		}

		private void AnimateIn()
		{
			this.Opacity = 0;
			var fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromSeconds(0.3), DecelerationRatio = 0.8 };
			this.BeginAnimation(Window.OpacityProperty, fade);
			this.BeginAnimation(Window.MarginProperty, new ThicknessAnimation { From = new Thickness(0, -20, 0, 0), To = new Thickness(0), Duration = TimeSpan.FromSeconds(0.3), DecelerationRatio = 0.8 });
		}

		private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
		{
			if (ApiKeyBox.Tag?.ToString() == "HasExistingKey") ApiKeyBox.Tag = null;
		}

		private async Task LoadCurrentSettingsAsync()
		{
			try
			{
				string? existingKey = await ApiKeyStore.GetOpenAIApiKeyAsync();
				string? currentModel = await ApiKeyStore.GetPreferredModelAsync();

				if (!string.IsNullOrEmpty(currentModel))
				{
					int idx = 0;
					foreach (var item in ModelComboBox.Items)
					{
						if (item is ComboBoxItem cbi && cbi.Content?.ToString() == currentModel) { ModelComboBox.SelectedIndex = idx; break; }
						idx++;
					}
				}

				SyncUI();

				if (!string.IsNullOrEmpty(existingKey))
				{
					ApiKeyBox.Password = new string('\u2022', 50);
					ApiKeyBox.Tag = "HasExistingKey";
				}
			}
			catch (Exception ex) { Logger.LogError("Error loading current settings", ex); }
		}

		private void SyncUI()
		{
			if (!(ModelComboBox.SelectedItem is ComboBoxItem selectedItem)) return;
			string? model = selectedItem.Content?.ToString();
			bool isOllama = IsOllamaModel(model);
			ApiKeyPanel.Visibility = isOllama ? Visibility.Collapsed : Visibility.Visible;
			OllamaBanner.Visibility = isOllama ? Visibility.Visible : Visibility.Collapsed;
			HelperText.Text = isOllama ? "These models run locally via Ollama (localhost:11434). No API key required." : "Configure your AI provider settings. Credentials are stored securely.";
		}

		private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncUI();

		private bool IsOllamaModel(string? model) => model is "nemotron-3-nano:4b" or "gemma4:latest" or "qwen3.5:0.8b" or "qwen3.6:35b" or "qwen3.6:27b" or "translategemma:4b";

		private async void Save_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string selectedModel = (ModelComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "gpt-4o-mini";
				bool isOllama = IsOllamaModel(selectedModel);

				if (isOllama)
				{
					if (!await ApiKeyStore.SaveOllamaConfigAsync(selectedModel)) { System.Windows.MessageBox.Show("Failed to save configuration.", "TypeMate"); return; }
				}
				else
				{
					string key = ApiKeyBox.Password?.Trim() ?? string.Empty;
					bool hasExistingKey = ApiKeyBox.Tag?.ToString() == "HasExistingKey";
					bool isPlaceholder = key.StartsWith("\u2022");

					if (hasExistingKey && isPlaceholder)
					{
						key = await ApiKeyStore.GetOpenAIApiKeyAsync() ?? string.Empty;
						if (string.IsNullOrEmpty(key)) { System.Windows.MessageBox.Show("Please enter a valid API key.", "TypeMate"); return; }
					}

					if (string.IsNullOrWhiteSpace(key) || isPlaceholder) { System.Windows.MessageBox.Show("Please enter a valid API key.", "TypeMate"); return; }
					if (!await ApiKeyStore.SaveOpenAIConfigAsync(key, selectedModel)) { System.Windows.MessageBox.Show("Failed to save OpenAI configuration.", "TypeMate"); return; }
					SavedKey = key;
				}

				SavedModel = selectedModel;
				this.DialogResult = true;
				this.Close();
			}
			catch (Exception ex) { Logger.LogError("Error saving configuration", ex); System.Windows.MessageBox.Show("An error occurred while saving the configuration.", "TypeMate"); }
		}

		private void Cancel_Click(object sender, RoutedEventArgs e) { this.DialogResult = false; this.Close(); }
	}
}