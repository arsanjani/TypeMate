using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TypeMate
{
	// Provider identifiers that match config storage
	static class Providers
	{
		public const string OpenAI = "openai";
		public const string Gemini = "gemini";
		public const string Ollama = "ollama";
		public const string OpenRouter = "openrouter";
	}

	public partial class ApiKeyDialog : Window
	{
		private static readonly Core.Config.IConfigStore ConfigStore = new Core.Config.JsonConfigStore();

		public string? SavedKey { get; private set; }
		public string? SavedModel { get; private set; }

		public ApiKeyDialog()
		{
			InitializeComponent();

			// Populate provider dropdown
			ProviderComboBox.Items.Add("OpenAI");
			ProviderComboBox.Items.Add("Gemini");
			ProviderComboBox.Items.Add("Ollama");
			ProviderComboBox.Items.Add("OpenRouter");

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

		private bool _isInitializing;
		private bool _userEditedApiKey;

		private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
		{
			if (_isInitializing) return;
			_userEditedApiKey = true;
		}

		private async Task LoadCurrentSettingsAsync()
		{
			try
			{
				_isInitializing = true;

				Core.Config.AppConfig? config = await ConfigStore.GetAsync();
				string? provider = config?.Provider ?? Providers.OpenAI;
				string? currentModel = config?.PreferredModel;

				// Select the saved provider in dropdown
				if (!string.IsNullOrEmpty(provider))
				{
					string displayName = GetProviderDisplayName(provider);
					for (int i = 0; i < ProviderComboBox.Items.Count; i++)
					{
						if (ProviderComboBox.Items[i].ToString() == displayName)
						{
							ProviderComboBox.SelectedIndex = i;
							break;
						}
					}
				}

				// Restore model text
				if (!string.IsNullOrEmpty(currentModel))
				{
					ModelBox.Text = currentModel;
				}
				else
				{
					ModelBox.Text = GetDefaultModel(provider ?? Providers.OpenAI);
				}

				SyncUI();

				// Load the appropriate existing key based on provider
				string? existingKey = GetExistingKeyForProvider(config, provider);
				if (!string.IsNullOrEmpty(existingKey))
				{
					ApiKeyBox.Password = new string('\u2022', 50);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Error loading current settings", ex);
			}
			finally
			{
				_isInitializing = false;
			}
		}

		private void SyncUI()
		{
			string provider = GetSelectedProvider();
			bool isOllama = provider == Providers.Ollama;

			if (isOllama)
			{
				ApiKeyPanel.Visibility = Visibility.Collapsed;
				ModelPanel.Visibility = Visibility.Visible;
				OllamaBanner.Visibility = Visibility.Visible;
				SecurityNotice.Visibility = Visibility.Collapsed;
				HelperText.Text = "These models run locally via Ollama (localhost:11434). No API key required.";
				ModelHelperText.Text = "Enter the Ollama model name (e.g., llama3.2, gemma:2b)";
			}
			else
			{
				ApiKeyPanel.Visibility = Visibility.Visible;
				ModelPanel.Visibility = Visibility.Visible;
				OllamaBanner.Visibility = Visibility.Collapsed;
				SecurityNotice.Visibility = Visibility.Visible;

				string keyLabel = GetProviderKeyLabel(provider);
				ApiKeyLabelText.Text = $"\u0001f510  {keyLabel} API Key";
				HelperText.Text = "Configure your AI provider settings. Credentials are stored securely.";
				ModelHelperText.Text = GetModelHelperText(provider);
			}
		}

		private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncUI();

		private string GetSelectedProvider()
		{
			if (ProviderComboBox.SelectedItem?.ToString() is not string displayName)
				return Providers.OpenAI;

			return GetProviderKey(displayName);
		}

		private static string GetProviderKey(string displayName)
		{
			return displayName.ToLowerInvariant();
		}

		private static string GetProviderDisplayName(string providerKey)
		{
			return providerKey switch
			{
				"openai" => "OpenAI",
				"gemini" => "Gemini",
				"ollama" => "Ollama",
				"openrouter" => "OpenRouter",
				_ => "OpenAI"
			};
		}

		private static string GetProviderKeyLabel(string provider)
		{
			return provider switch
			{
				"openai" => "OpenAI",
				"gemini" => "Gemini",
				"openrouter" => "OpenRouter",
				_ => "OpenAI"
			};
		}

		private static string GetModelHelperText(string provider)
		{
			return provider switch
			{
				"openai" => "e.g., gpt-4o-mini, gpt-4o, o3-mini",
				"gemini" => "e.g., gemini-2.0-flash, gemini-flash-latest",
				"openrouter" => "e.g., openai/gpt-4o, anthropic/claude-sonnet-4-20250514",
				_ => "Enter the model identifier for your provider"
			};
		}

		private static string GetDefaultModel(string provider)
		{
			return provider switch
			{
				"openai" => "gpt-4o-mini",
				"gemini" => "gemini-flash-latest",
				"ollama" => "llama3.2",
				"openrouter" => "openai/gpt-4o",
				_ => "gpt-4o-mini"
			};
		}

		private static string? GetExistingKeyForProvider(Core.Config.AppConfig? config, string? provider)
		{
			if (config == null)
				return null;
			if (string.Equals(provider, Providers.Gemini, StringComparison.OrdinalIgnoreCase))
				return Core.Config.AppConfig.DecryptBase64(config.EncryptedGeminiApiKeyBase64);
			if (string.Equals(provider, Providers.OpenRouter, StringComparison.OrdinalIgnoreCase))
				return Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenRouterApiKeyBase64);
			return Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenAIApiKeyBase64);
		}

		private async void Save_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string provider = GetSelectedProvider();
				bool isOllama = provider == Providers.Ollama;
				string model = ModelBox.Text?.Trim() ?? string.Empty;

				if (string.IsNullOrWhiteSpace(model))
				{
					System.Windows.MessageBox.Show("Please enter a model name.", "TypeMate");
					return;
				}

				Core.Config.AppConfig config = await ConfigStore.GetAsync() ?? new Core.Config.AppConfig();				if (isOllama)
				{
					config.PreferredModel = model;
					config.Provider = Providers.Ollama;

					if (!await ConfigStore.SaveAsync(config))
					{
						System.Windows.MessageBox.Show("Failed to save configuration.", "TypeMate");
						return;
					}
					SavedModel = model;
					this.DialogResult = true;
					this.Close();
					return;
				}

				// Provider requires API key
				string key;
				if (!_userEditedApiKey)
				{
					key = GetExistingKeyForProvider(config, provider) ?? string.Empty;
					if (string.IsNullOrEmpty(key))
					{
						string label = GetProviderKeyLabel(provider);
						System.Windows.MessageBox.Show($"Please enter a valid {label} API key.", "TypeMate");
						return;
					}
				}
				else
				{
					key = ApiKeyBox.Password?.Trim() ?? string.Empty;
					if (string.IsNullOrWhiteSpace(key))
					{
						string label = GetProviderKeyLabel(provider);
						System.Windows.MessageBox.Show($"Please enter a valid {label} API key.", "TypeMate");
						return;
					}
				}

				config.PreferredModel = model;
				config.Provider = provider;

				if (provider == Providers.OpenAI)
				{
					byte[] encrypted = Core.Config.AppConfig.Encrypt(key);
					config.EncryptedOpenAIApiKeyBase64 = Convert.ToBase64String(encrypted);
				}
				else if (provider == Providers.Gemini)
				{
					byte[] encrypted = Core.Config.AppConfig.Encrypt(key);
					config.EncryptedGeminiApiKeyBase64 = Convert.ToBase64String(encrypted);
				}
				else if (provider == Providers.OpenRouter)
				{
					byte[] encrypted = Core.Config.AppConfig.Encrypt(key);
					config.EncryptedOpenRouterApiKeyBase64 = Convert.ToBase64String(encrypted);
				}

				if (!await ConfigStore.SaveAsync(config))
				{
					System.Windows.MessageBox.Show("Failed to save configuration.", "TypeMate");
					return;
				}

				SavedKey = key;
				SavedModel = model;
				this.DialogResult = true;
				this.Close();
			}
			catch (Exception ex)
			{
				Logger.LogError("Error saving configuration", ex);
				System.Windows.MessageBox.Show("An error occurred while saving the configuration.", "TypeMate");
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e) { this.DialogResult = false; this.Close(); }

		private void ApiKeyBox_GotFocus(object sender, RoutedEventArgs e)
		{
			ApiKeyBoxGrid.SetValue(Grid.TagProperty, "Focused");
			SetInputBorder("#6366F1", 2);
		}

		private void ApiKeyBox_LostFocus(object sender, RoutedEventArgs e)
		{
			ApiKeyBoxGrid.SetValue(Grid.TagProperty, null);
			SetInputBorder("#E2E8F0", 1.5);
		}

		private void ApiKeyBox_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (!ApiKeyBox.IsKeyboardFocused)
				SetInputBorder("#94A3B8", 1.5);
		}

		private void ApiKeyBox_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (!ApiKeyBox.IsKeyboardFocused)
				SetInputBorder("#E2E8F0", 1.5);
		}

		private void ModelBox_GotFocus(object sender, RoutedEventArgs e)
		{
			SetModelInputBorder("#6366F1", 2);
		}

		private void ModelBox_LostFocus(object sender, RoutedEventArgs e)
		{
			SetModelInputBorder("#E2E8F0", 1.5);
		}

		private void ModelBox_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (!ModelBox.IsKeyboardFocused)
				SetModelInputBorder("#94A3B8", 1.5);
		}

		private void ModelBox_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (!ModelBox.IsKeyboardFocused)
				SetModelInputBorder("#E2E8F0", 1.5);
		}

		private void SetInputBorder(string hexColor, double thickness)
		{
			var color = System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
			ApiKeyBoxBorder.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)color);
			ApiKeyBoxBorder.BorderThickness = new Thickness(thickness);
		}

		private void SetModelInputBorder(string hexColor, double thickness)
		{
			var color = System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
			ModelBoxBorder.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)color);
			ModelBoxBorder.BorderThickness = new Thickness(thickness);
		}
	}
}
