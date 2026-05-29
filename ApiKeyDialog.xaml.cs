using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TypeMate
{
	public partial class ApiKeyDialog : Window
	{
		public string? SavedKey { get; private set; }
		public string? SavedModel { get; private set; }

		public ApiKeyDialog()
		{
			InitializeComponent();
			// Set default selection
			ModelComboBox.SelectedIndex = 0;
			Loaded += ApiKeyDialog_Loaded;
			ApiKeyBox.PasswordChanged += ApiKeyBox_PasswordChanged;
		}

		private async void ApiKeyDialog_Loaded(object sender, RoutedEventArgs e)
		{
			await LoadCurrentSettingsAsync();
		}

		private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
		{
			if (ApiKeyBox.Tag?.ToString() == "HasExistingKey")
			{
				ApiKeyBox.Tag = null;
			}
		}

		private async Task LoadCurrentSettingsAsync()
		{
			try
			{
				// Load current provider and existing key
				Task<string?> providerTask = ApiKeyStore.GetProviderAsync();
				Task<string?> existingKeyTask = ApiKeyStore.GetOpenAIApiKeyAsync();
				Task<string?> currentModelTask = ApiKeyStore.GetPreferredModelAsync();

				await Task.WhenAll(providerTask, existingKeyTask, currentModelTask);

				string? provider = providerTask.Result;
				string? existingKey = existingKeyTask.Result;
				bool hasKey = !string.IsNullOrEmpty(existingKey);
				string? currentModel = currentModelTask.Result;

				// Set selected model if we have a saved one
				if (!string.IsNullOrEmpty(currentModel))
				{
					// Skip Separator and TextBlock, only check ComboBoxItem
					int idx = 0;
					foreach (var item in ModelComboBox.Items)
					{
						if (item is ComboBoxItem cbi && cbi.Content?.ToString() == currentModel)
						{
							ModelComboBox.SelectedIndex = idx;
							break;
						}
						idx++;
					}
				}

				// Sync UI based on selection
				ModelComboBox_SelectionChanged(null!, null!);

				// Set placeholder if existing key exists
				if (hasKey)
				{
					ApiKeyBox.Password = "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022";
					ApiKeyBox.Tag = "HasExistingKey";
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Error loading current settings", ex);
			}
		}

		private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ModelComboBox.SelectedItem is ComboBoxItem selectedItem)
			{
				string? model = selectedItem.Content?.ToString();
				bool isOllama = IsOllamaModel(model);

				ApiKeyPanel.Visibility = isOllama ? Visibility.Collapsed : Visibility.Visible;
				OllamaInfoText.Visibility = isOllama ? Visibility.Visible : Visibility.Collapsed;

				if (isOllama)
				{
					HelperText.Text = "These models run locally via Ollama (localhost:11434). No API key required.";
				}
				else
				{
					HelperText.Text = "Configure your OpenAI API settings. Your credentials are stored securely on this device.";
				}
			}
		}

		private bool IsOllamaModel(string? model)
		{
			return string.Equals(model, "nemotron-3-nano:4b", StringComparison.OrdinalIgnoreCase) ||
			       string.Equals(model, "gemma4:latest", StringComparison.OrdinalIgnoreCase) ||
			       string.Equals(model, "qwen3.5:0.8b", StringComparison.OrdinalIgnoreCase) ||
			       string.Equals(model, "translategemma:4b", StringComparison.OrdinalIgnoreCase);
		}

		private async void Save_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Get selected model
				string selectedModel = "gpt-4o-mini";
				if (ModelComboBox.SelectedItem is ComboBoxItem selectedItem)
				{
					selectedModel = selectedItem.Content?.ToString() ?? "gpt-4o-mini";
				}

				bool isOllama = IsOllamaModel(selectedModel);

				if (isOllama)
				{
					// Save Ollama config (no API key needed)
					bool saved = await ApiKeyStore.SaveOllamaConfigAsync(selectedModel);
					if (!saved)
					{
						System.Windows.MessageBox.Show("Failed to save configuration.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Warning);
						return;
					}
				}
				else
				{
					// Validate API key for OpenAI models
					string key = ApiKeyBox.Password?.Trim() ?? string.Empty;
					bool hasExistingKey = ApiKeyBox.Tag?.ToString() == "HasExistingKey";
					bool isPlaceholder = key.StartsWith("\u2022\u2022\u2022\u2022");

					if (hasExistingKey && isPlaceholder)
					{
						string? existingKey = await ApiKeyStore.GetOpenAIApiKeyAsync();
						if (string.IsNullOrEmpty(existingKey))
						{
							System.Windows.MessageBox.Show("Please enter a valid API key.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Information);
							return;
						}
						key = existingKey;
					}
					else if (string.IsNullOrWhiteSpace(key) || isPlaceholder)
					{
						System.Windows.MessageBox.Show("Please enter a valid API key.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Information);
						return;
					}

					// Save OpenAI config
					bool saved = await ApiKeyStore.SaveOpenAIConfigAsync(key, selectedModel);
					if (!saved)
					{
						System.Windows.MessageBox.Show("Failed to save OpenAI configuration.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Warning);
						return;
					}

					SavedKey = key;
				}

				SavedModel = selectedModel;
				this.DialogResult = true;
				this.Close();
			}
			catch (Exception ex)
			{
				Logger.LogError("Error saving configuration", ex);
				System.Windows.MessageBox.Show("An error occurred while saving the configuration.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
			this.Close();
		}
	}
}
