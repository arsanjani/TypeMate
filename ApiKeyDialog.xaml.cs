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
		Loaded += ApiKeyDialog_Loaded;
		ApiKeyBox.PasswordChanged += ApiKeyBox_PasswordChanged;
	}

	private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
	{
		// Clear the placeholder flag when user starts typing
		if (ApiKeyBox.Tag?.ToString() == "HasExistingKey")
		{
			ApiKeyBox.Tag = null;
		}
	}

		private async void ApiKeyDialog_Loaded(object sender, RoutedEventArgs e)
		{
			await LoadCurrentSettingsAsync();
		}

		private async Task LoadCurrentSettingsAsync()
		{
			try
			{
				// Check if there's an existing API key and show placeholder
				string? existingKey = await ApiKeyStore.GetOpenAIApiKeyAsync();
				if (!string.IsNullOrEmpty(existingKey))
				{
					// Show placeholder characters to indicate an API key exists
					ApiKeyBox.Password = "••••••••••••••••••••••••••••••••••••••••••••••••••••";
					ApiKeyBox.Tag = "HasExistingKey"; // Flag to track this is placeholder
				}

				// Load existing model preference
				string? currentModel = await ApiKeyStore.GetPreferredModelAsync();
				if (!string.IsNullOrEmpty(currentModel))
				{
					// Find and select the current model in the ComboBox
					bool modelFound = false;
					foreach (ComboBoxItem item in ModelComboBox.Items)
					{
						if (item.Content?.ToString() == currentModel)
						{
							ModelComboBox.SelectedItem = item;
							modelFound = true;
							break;
						}
					}
					
					// If the stored model isn't in our list, select the default
					if (!modelFound && ModelComboBox.Items.Count > 0)
					{
						ModelComboBox.SelectedIndex = 0; // Select first item (gpt-4o-mini)
					}
				}
				else if (ModelComboBox.Items.Count > 0)
				{
					// No stored preference, select default
					ModelComboBox.SelectedIndex = 0; // Select first item (gpt-4o-mini)
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Error loading current settings", ex);
				// Fallback to default selection if error occurs
				if (ModelComboBox.Items.Count > 0)
				{
					ModelComboBox.SelectedIndex = 0;
				}
			}
		}

		private async void Save_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string key = ApiKeyBox.Password?.Trim() ?? string.Empty;
				
				// Check if user didn't change the placeholder (keeping existing key)
				bool hasExistingKey = ApiKeyBox.Tag?.ToString() == "HasExistingKey";
				bool isPlaceholder = key.StartsWith("••••");
				
				if (hasExistingKey && isPlaceholder)
				{
					// User kept the existing key, just update the model
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

				// Get selected model
				string selectedModel = "gpt-4o-mini"; // Default
				if (ModelComboBox.SelectedItem is ComboBoxItem selectedItem)
				{
					selectedModel = selectedItem.Content?.ToString() ?? "gpt-4o-mini";
				}

				// Save both API key and model
				bool saved = await ApiKeyStore.SaveOpenAIConfigAsync(key, selectedModel);
				if (!saved)
				{
					System.Windows.MessageBox.Show("Failed to save OpenAI configuration.", "TypeMate", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				SavedKey = key;
				SavedModel = selectedModel;
				this.DialogResult = true;
				this.Close();
			}
			catch (Exception ex)
			{
				Logger.LogError("Error saving OpenAI configuration", ex);
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


