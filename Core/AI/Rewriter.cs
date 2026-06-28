using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TypeMate.Core.AI
{
	public class Rewriter
	{
		private readonly Core.Config.IConfigStore _configStore;
		private readonly IEnumerable<IAIProvider> _providers;

		public Rewriter(Core.Config.IConfigStore configStore, params IAIProvider[] providers)
		{
			_configStore = configStore;
			_providers = providers;
		}

		public async Task<string?> RewriteAsync(string input, RewriteStyle style)
		{
			var config = await _configStore.GetAsync();
			if (config == null) return null;

			string model = config.PreferredModel ?? "gpt-4o-mini";
			string providerName = config.Provider ?? "OpenAI";

			// Resolve API Key based on provider
			string? apiKey = GetApiKeyForProvider(config, providerName);

			IAIProvider? provider = _providers.FirstOrDefault(p => 
				p.GetType().Name.StartsWith(providerName, StringComparison.OrdinalIgnoreCase));

			if (provider == null)
			{
				TypeMate.Logger.LogWarning($"AI Provider '{providerName}' not found in registered providers.");
				return null;
			}

			try
			{
				string? result = await provider.RewriteAsync(input, style, model, apiKey);
				
				// OpenAI specific fallback logic: if result is empty and we aren't already using the fallback model
				if (string.IsNullOrWhiteSpace(result) && 
					providerName.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) && 
					model != "gpt-4o-mini")
				{
					result = await provider.RewriteAsync(input, style, "gpt-4o-mini", apiKey);
				}

				return result;
			}
			catch (Exception ex)
			{
				TypeMate.Logger.LogError($"{providerName} rewrite failed", ex);
				return null;
			}
		}

		private string? GetApiKeyForProvider(Core.Config.AppConfig config, string providerName)
		{
			if (string.Equals(providerName, "Gemini", StringComparison.OrdinalIgnoreCase))
				return Core.Config.AppConfig.DecryptBase64(config.EncryptedGeminiApiKeyBase64);
			if (string.Equals(providerName, "OpenRouter", StringComparison.OrdinalIgnoreCase))
				return Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenRouterApiKeyBase64);
			
			// Default to OpenAI
			return Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenAIApiKeyBase64);
		}
	}
}
