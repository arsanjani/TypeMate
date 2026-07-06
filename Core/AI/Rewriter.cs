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
			if (config == null)
				throw new InvalidOperationException("Failed to load configuration.");

			string providerName = config.Provider ?? Providers.OpenAI;
			string? model = ResolveModel(config, providerName);
			if (string.IsNullOrWhiteSpace(model))
				throw new InvalidOperationException("No AI model configured.");

			string? apiKey = GetApiKeyForProvider(config, providerName);
			IAIProvider? provider = ResolveProvider(providerName, config);

			if (provider == null)
				throw new InvalidOperationException($"AI provider '{providerName}' is not available.");

			int contextLength = config.CompatibleContextWindow > 0 ? config.CompatibleContextWindow.Value : ResolveDefaultContextLength(providerName);
			return await provider.RewriteAsync(input, style, model, apiKey, contextLength);
		}

		private static int ResolveDefaultContextLength(string providerName)
		{
			return providerName switch
			{
				Providers.Gemini => 131072,
				Providers.Ollama => 32768,
				_ => 8192
			};
		}

		private IAIProvider? ResolveProvider(string providerName, Core.Config.AppConfig config)
		{
			return providerName switch
			{
				Providers.OpenAI => new OpenAICompatibleProvider(
					"OpenAI", "https://api.openai.com"),
				_ when providerName == Providers.OpenAICompatible => new OpenAICompatibleProvider(
					providerName, config.CompatibleBaseUrl ?? "https://api.openai.com"),
				Providers.OpenRouter => _providers.FirstOrDefault(p =>
					p.GetType().Name.StartsWith("OpenRouter", StringComparison.OrdinalIgnoreCase)),
				_ => _providers.FirstOrDefault(p =>
					p.GetType().Name.StartsWith(providerName, StringComparison.OrdinalIgnoreCase))
			};
		}

		private static string? ResolveModel(Core.Config.AppConfig config, string providerName)
		{
			if (providerName == Providers.OpenAICompatible)
				return !string.IsNullOrWhiteSpace(config.CompatibleModel) ? config.CompatibleModel : config.PreferredModel;
			return config.PreferredModel ?? providerName switch
			{
				Providers.OpenAI => "gpt-4o-mini",
				Providers.Ollama => "llama3.2",
				Providers.Gemini => "gemini-flash-latest",
				Providers.OpenRouter => "openai/gpt-4o",
				_ => "gpt-4o-mini"
			};
		}

		private string? GetApiKeyForProvider(Core.Config.AppConfig config, string providerName)
		{
			return providerName switch
			{
				Providers.Gemini => Core.Config.AppConfig.DecryptBase64(config.EncryptedGeminiApiKeyBase64),
				Providers.OpenRouter => Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenRouterApiKeyBase64),
				Providers.OpenAICompatible => Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenAICompatibleApiKeyBase64),
				_ => Core.Config.AppConfig.DecryptBase64(config.EncryptedOpenAIApiKeyBase64)
			};
		}
	}

	static class Providers
	{
		public const string OpenAI = "openai";
		public const string Gemini = "gemini";
		public const string Ollama = "ollama";
		public const string OpenRouter = "openrouter";
		public const string OpenAICompatible = "openaicompatible";
	}
}