using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TypeMate
{
	public static class ApiKeyStore
	{
		private const string AppFolderName = "TypeMate";
		private const string ConfigFileName = "config.json";

		private class ConfigModel
		{
			public string? EncryptedOpenAIApiKeyBase64 { get; set; }
			public string? PreferredModel { get; set; }
			public string? Provider { get; set; } // "openai" or "ollama"
		}

		public static async Task<string?> GetOpenAIApiKeyAsync()
		{
			try
			{
				string configPath = GetConfigPath();
				if (!File.Exists(configPath))
				{
					return null;
				}

				await using FileStream stream = File.OpenRead(configPath);
				ConfigModel? config = await JsonSerializer.DeserializeAsync<ConfigModel>(stream);
				if (config == null || string.IsNullOrWhiteSpace(config.EncryptedOpenAIApiKeyBase64))
				{
					return null;
				}

				byte[] encrypted = Convert.FromBase64String(config.EncryptedOpenAIApiKeyBase64);
				byte[] decrypted = ProtectedData.Unprotect(encrypted, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
				return Encoding.UTF8.GetString(decrypted);
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to read OpenAI API key", ex);
				return null;
			}
		}

		public static async Task<bool> SaveOpenAIApiKeyAsync(string apiKey)
		{
			try
			{
				// Read existing config to preserve model setting
				ConfigModel config = await GetConfigAsync() ?? new ConfigModel();

				string directory = GetAppDirectory();
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				byte[] plain = Encoding.UTF8.GetBytes(apiKey);
				byte[] encrypted = ProtectedData.Protect(plain, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
				string base64 = Convert.ToBase64String(encrypted);

				config.EncryptedOpenAIApiKeyBase64 = base64;

				JsonSerializerOptions options = new JsonSerializerOptions
				{
					WriteIndented = true
				};

				string configPath = GetConfigPath();
				await using FileStream stream = File.Create(configPath);
				await JsonSerializer.SerializeAsync(stream, config, options);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to save OpenAI API key", ex);
				return false;
			}
		}

		public static async Task<bool> SaveOpenAIConfigAsync(string apiKey, string preferredModel)
		{
			return await SaveConfigAsync(apiKey, preferredModel, "openai");
		}

		public static async Task<bool> SaveOllamaConfigAsync(string preferredModel)
		{
			return await SaveConfigAsync(null, preferredModel, "ollama");
		}

		private static async Task<bool> SaveConfigAsync(string? apiKey, string preferredModel, string provider)
		{
			try
			{
				ConfigModel config = new ConfigModel
				{
					EncryptedOpenAIApiKeyBase64 = apiKey == null ? null : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), null, DataProtectionScope.CurrentUser)),
					PreferredModel = preferredModel,
					Provider = provider
				};

				string directory = GetAppDirectory();
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				JsonSerializerOptions options = new JsonSerializerOptions
				{
					WriteIndented = true
				};

				string configPath = GetConfigPath();
				await using FileStream stream = File.Create(configPath);
				await JsonSerializer.SerializeAsync(stream, config, options);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to save configuration", ex);
				return false;
			}
		}

		public static async Task<string?> GetPreferredModelAsync()
		{
			try
			{
				ConfigModel? config = await GetConfigAsync();
				return config?.PreferredModel ?? "gpt-4o-mini"; // Default model
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to read preferred model", ex);
				return "gpt-4o-mini"; // Default model
			}
		}

		public static async Task<string?> GetProviderAsync()
		{
			try
			{
				ConfigModel? config = await GetConfigAsync();
				return config?.Provider ?? "openai";
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to read provider", ex);
				return "openai";
			}
		}

		public static async Task<bool> SavePreferredModelAsync(string preferredModel)
		{
			try
			{
				ConfigModel config = await GetConfigAsync() ?? new ConfigModel();
				config.PreferredModel = preferredModel;

				string directory = GetAppDirectory();
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				JsonSerializerOptions options = new JsonSerializerOptions
				{
					WriteIndented = true
				};

				string configPath = GetConfigPath();
				await using FileStream stream = File.Create(configPath);
				await JsonSerializer.SerializeAsync(stream, config, options);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to save preferred model", ex);
				return false;
			}
		}

		private static async Task<ConfigModel?> GetConfigAsync()
		{
			try
			{
				string configPath = GetConfigPath();
				if (!File.Exists(configPath))
				{
					return null;
				}

				await using FileStream stream = File.OpenRead(configPath);
				ConfigModel? config = await JsonSerializer.DeserializeAsync<ConfigModel>(stream);
				return config;
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to read configuration", ex);
				return null;
			}
		}

		private static string GetAppDirectory()
		{
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			return Path.Combine(appData, AppFolderName);
		}

		private static string GetConfigPath()
		{
			return Path.Combine(GetAppDirectory(), ConfigFileName);
		}
	}
}
