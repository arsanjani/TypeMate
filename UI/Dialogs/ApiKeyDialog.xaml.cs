using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TypeMate.Core.AI;

namespace TypeMate
{
public partial class ApiKeyDialog : Window
	{
		private static readonly Core.Config.IConfigStore ConfigStore = new Core.Config.JsonConfigStore();

		public string? SavedKey { get; private set; }
		public string? SavedModel { get; private set; }

		public ApiKeyDialog()
		{
			InitializeComponent();

			ProviderComboBox.Items.Add("Gemini");
			ProviderComboBox.Items.Add("Ollama");
			ProviderComboBox.Items.Add("OpenRouter");
			ProviderComboBox.Items.Add("OpenAI Compatible");

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

			// Restore OpenAI Compatible fields
			BaseUrlBox.Text = config?.CompatibleBaseUrl ?? string.Empty;
			CompatibleModelBox.Text = !string.IsNullOrWhiteSpace(config?.CompatibleModel)
				? config.CompatibleModel
				: (currentModel ?? string.Empty);
			ContextWindowBox.Text = config?.CompatibleContextWindow?.ToString() ?? string.Empty;

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
			bool isCompatible = provider == Providers.OpenAICompatible;

			if (isOllama)
			{
				ApiKeyPanel.Visibility = Visibility.Collapsed;
				ModelPanel.Visibility = Visibility.Visible;
				BaseUrlPanel.Visibility = Visibility.Collapsed;
				CompatibleModelPanel.Visibility = Visibility.Collapsed;
				ContextWindowPanel.Visibility = Visibility.Collapsed;
				OllamaBanner.Visibility = Visibility.Visible;
				SecurityNotice.Visibility = Visibility.Collapsed;
				HelperText.Text = "These models run locally via Ollama (localhost:11434). No API key required.";
				ModelHelperText.Text = "Enter the Ollama model name (e.g., llama3.2, gemma:2b)";
			}
			else if (isCompatible)
			{
				ApiKeyPanel.Visibility = Visibility.Visible;
				ModelPanel.Visibility = Visibility.Collapsed;
				BaseUrlPanel.Visibility = Visibility.Visible;
				CompatibleModelPanel.Visibility = Visibility.Visible;
				ContextWindowPanel.Visibility = Visibility.Visible;
				OllamaBanner.Visibility = Visibility.Collapsed;
				SecurityNotice.Visibility = Visibility.Visible;
				ApiKeyLabelText.Text = "🔑  API Key";
				HelperText.Text = "Point TypeMate at any OpenAI-compatible chat completions endpoint.";
			}
			else
			{
				ApiKeyPanel.Visibility = Visibility.Visible;
				ModelPanel.Visibility = Visibility.Visible;
				BaseUrlPanel.Visibility = Visibility.Collapsed;
				CompatibleModelPanel.Visibility = Visibility.Collapsed;
				ContextWindowPanel.Visibility = Visibility.Collapsed;
				OllamaBanner.Visibility = Visibility.Collapsed;
				SecurityNotice.Visibility = Visibility.Visible;

				string keyLabel = GetProviderKeyLabel(provider);
				ApiKeyLabelText.Text = $"🔑  {keyLabel} API Key";
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
			return displayName switch
			{
				"OpenAI Compatible" => Providers.OpenAICompatible,
				"OpenRouter" => Providers.OpenRouter,
				"Gemini" => Providers.Gemini,
				"Ollama" => Providers.Ollama,
				_ => Providers.OpenAICompatible
			};
		}

		private static string GetProviderDisplayName(string providerKey)
		{
			return providerKey switch
			{
				"openai" => "OpenAI Compatible",
				"gemini" => "Gemini",
				"ollama" => "Ollama",
				"openrouter" => "OpenRouter",
				"openaicompatible" => "OpenAI Compatible",
				_ => "OpenAI Compatible"
			};
		}

		private static string GetProviderKeyLabel(string provider)
		{
			return provider switch
			{
				"gemini" => "Gemini",
				"openrouter" => "OpenRouter",
				"openaicompatible" => "OpenAI Compatible",
				_ => "OpenAI Compatible"
			};
		}

		private static string GetModelHelperText(string provider)
		{
			return provider switch
			{
				"gemini" => "e.g., gemini-2.0-flash, gemini-flash-latest",
				"openrouter" => "e.g., openai/gpt-4o, anthropic/claude-sonnet-4-20250514",
				"openaicompatible" => "Enter the model identifier for your endpoint",
				_ => "e.g., gpt-4o-mini, gpt-4o, o3-mini"
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
				"openaicompatible" => string.Empty,
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
			if (string.Equals(provider, Providers.OpenAICompatible, StringComparison.OrdinalIgnoreCase))
				return Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenAICompatibleApiKeyBase64);
			return Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenAIApiKeyBase64);
		}

		private async void Save_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string provider = GetSelectedProvider();
				bool isOllama = provider == Providers.Ollama;
				bool isCompatible = provider == Providers.OpenAICompatible;

				string model = isCompatible
					? (CompatibleModelBox.Text?.Trim() ?? string.Empty)
					: (ModelBox.Text?.Trim() ?? string.Empty);

				if (string.IsNullOrWhiteSpace(model) && !isOllama)
				{
					System.Windows.MessageBox.Show(
						isCompatible ? "Please enter a Model ID." : "Please enter a model name.", "TypeMate");
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

				// OpenAI Compatible extra validation
				if (isCompatible)
				{
					string baseUrl = BaseUrlBox.Text?.Trim() ?? string.Empty;
					if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
					{
						System.Windows.MessageBox.Show("Please enter a valid Base URL (e.g., https://api.openai.com).", "TypeMate");
						return;
					}
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

			string ek = Convert.ToBase64String(Core.Config.AppConfig.Encrypt(key));
			if (provider == Providers.Gemini) config.EncryptedGeminiApiKeyBase64 = ek;
			else if (provider == Providers.OpenRouter) config.EncryptedOpenRouterApiKeyBase64 = ek;
			else if (provider == Providers.OpenAICompatible) config.EncryptedOpenAICompatibleApiKeyBase64 = ek;
			else config.EncryptedOpenAIApiKeyBase64 = ek;

				if (isCompatible)
				{
					config.CompatibleBaseUrl = BaseUrlBox.Text?.Trim();
					config.CompatibleModel = model;
					config.CompatibleContextWindow = int.TryParse(ContextWindowBox.Text?.Trim(), out int cw) && cw > 0
						? cw
						: (int?)null;
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

		private static Border? GetParentBorder(Grid grid) => grid.Children.OfType<Border>().FirstOrDefault();

		private void FieldBox_GotFocus(object sender, RoutedEventArgs e)
		{
			if (sender is System.Windows.Controls.TextBox tb && tb.Parent is Grid g && GetParentBorder(g) is Border b)
				SetBorder(b, "#6366F1", 2);
		}

		private void FieldBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (sender is System.Windows.Controls.TextBox tb && tb.Parent is Grid g && GetParentBorder(g) is Border b)
				SetBorder(b, "#E2E8F0", 1.5);
		}

		private void FieldBox_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (sender is not System.Windows.Controls.TextBox tb || tb.IsKeyboardFocused) return;
			if (tb.Parent is Grid gp && GetParentBorder(gp) is Border bp)
				SetBorder(bp, "#94A3B8", 1.5);
		}

		private void FieldBox_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (sender is not System.Windows.Controls.TextBox tb || tb.IsKeyboardFocused) return;
			if (tb.Parent is Grid gp && GetParentBorder(gp) is Border bp)
				SetBorder(bp, "#E2E8F0", 1.5);
		}

		private static void SetBorder(Border b, string hexColor, double thickness)
		{
			var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
			b.BorderBrush = new System.Windows.Media.SolidColorBrush(color);
			b.BorderThickness = new Thickness(thickness);
		}
	}
}
