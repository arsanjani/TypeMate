using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TypeMate
{
	public enum RewriteStyle
	{
		EasyRead,
		Witty,
		Formal,
		Summarise,
		Expand,
		LinkedInPost,
		PromptOptimizer,
		EnglishToFarsi
	}

	public static class OpenAIService
	{
		private static readonly HttpClient Http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(45)
		};

		private static readonly HttpClient OllamaHttp = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(60)
		};

		private const string PreferredModel = "o4-mini";
		private const string FallbackModel = "gpt-4o-mini";
		private const string OllamaEndpoint = "http://localhost:11434/v1/chat/completions";

		private static bool IsOllamaModel(string? model)
		{
			if (string.IsNullOrWhiteSpace(model)) return false;
			
			string lower = model.ToLowerInvariant();
			return lower.Contains("nemotron") || lower.Contains("gemma") || lower.Contains("qwen") || lower.Contains("translategemma");
		}

		private static string BuildSystemPrompt(RewriteStyle style)
		{
			switch (style)
			{
				case RewriteStyle.EasyRead:
					return "You are a writing assistant. Rewrite the user's text in clear, simple, accessible language while preserving meaning. Keep it concise.";
				case RewriteStyle.Witty:
					return "You are a witty copywriter. Rewrite the user's text with playful, clever phrasing, light humor, and personality, without changing the core message.";
				case RewriteStyle.Formal:
					return "You are a professional editor. Rewrite the user's text in a formal, polished, and concise tone suitable for business communication.";
				case RewriteStyle.Summarise:
					return "You are an expert summarizer. Provide a concise summary of the user's text in 3-5 bullet points or a short paragraph, keeping key facts.";
				case RewriteStyle.Expand:
					return "You are an explainer. Expand the user's text by elaborating on important points, adding helpful context and examples, while staying on-topic.";
				case RewriteStyle.LinkedInPost:
					return "You are a LinkedIn ghostwriter. Rewrite the user's text as a compelling LinkedIn post with a strong hook, clear value, and a call to action. Keep it professional and authentic.";
				case RewriteStyle.PromptOptimizer:
					return "You are a senior prompt engineer. Transform the user's input into a concise, high-signal prompt suitable for a code editor agent like Cursor AI.\n\nRequirements:\n- Start with a one-line goal statement (imperative voice).\n- Include only essential context and constraints.\n- List 3-6 high-level steps the agent should take.\n- Specify expected outputs and acceptance criteria.\n- If input includes code, preserve important identifiers and reference them succinctly.\n- Avoid fluff; output only the final optimized prompt ready to paste into Cursor.";
			case RewriteStyle.EnglishToFarsi:
					return "You are a professional English (en) to Persian (fa-IR) translator. Your goal is to accurately convey the meaning and nuances of the original English text while adhering to Persian grammar, vocabulary, and cultural sensitivities.\n\nProduce only the Persian translation, without any additional explanations or commentary. Please translate the following English text into the Persian:\n\n";
				default:
					return "You are a helpful writing assistant. Improve clarity and impact.";
			}
		}

		private static async Task<string?> CallOpenAIAsync(string model, string apiKey, RewriteStyle style, string input)
		{
			Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
			if (Http.DefaultRequestHeaders.Contains("OpenAI-Beta"))
			{
				Http.DefaultRequestHeaders.Remove("OpenAI-Beta");
			}

			var payload = new
			{
				model = model,
				messages = new object[]
				{
					new { role = "system", content = BuildSystemPrompt(style) },
					new { role = "user", content = input }
				},
				temperature = 0.7,
				max_tokens = 512
			};

			string json = JsonSerializer.Serialize(payload);
			using StringContent body = new StringContent(json, Encoding.UTF8, "application/json");
			using HttpResponseMessage resp = await Http.PostAsync("https://api.openai.com/v1/chat/completions", body);
			string respText = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
			{
				Logger.LogWarning($"OpenAI error ({model}): {(int)resp.StatusCode} {resp.ReasonPhrase} {respText}");
				return null;
			}

			using JsonDocument doc = JsonDocument.Parse(respText);
			JsonElement root = doc.RootElement;
			if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
			{
				JsonElement first = choices[0];
				if (first.TryGetProperty("message", out JsonElement message) && message.TryGetProperty("content", out JsonElement content))
				{
					return content.GetString();
				}
			}
			return null;
		}

		private static async Task<string?> CallOllamaAsync(string model, RewriteStyle style, string input)
		{
			var payload = new
			{
				model = model,
				messages = new object[]
				{
					new { role = "system", content = BuildSystemPrompt(style) },
					new { role = "user", content = input }
				},
				temperature = 0.7,
				stream = false
			};

			string json = JsonSerializer.Serialize(payload);
			using StringContent body = new StringContent(json, Encoding.UTF8, "application/json");

			try
			{
				using HttpResponseMessage resp = await OllamaHttp.PostAsync(OllamaEndpoint, body);
				string respText = await resp.Content.ReadAsStringAsync();

				if (!resp.IsSuccessStatusCode)
				{
					Logger.LogWarning($"Ollama error ({model}): {(int)resp.StatusCode} {resp.ReasonPhrase} {respText}");
					return null;
				}

				using JsonDocument doc = JsonDocument.Parse(respText);
				JsonElement root = doc.RootElement;
				if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
				{
					JsonElement first = choices[0];
					if (first.TryGetProperty("message", out JsonElement message) && message.TryGetProperty("content", out JsonElement content))
					{
						return content.GetString();
					}
				}
				return null;
			}
			catch (Exception ex)
			{
				Logger.LogError($"Ollama connection failed for model {model}", ex);
				return null;
			}
		}

		public static async Task<string?> RewriteAsync(string input, RewriteStyle style)
		{
			string? userPreferredModel = await ApiKeyStore.GetPreferredModelAsync();
			if (string.IsNullOrWhiteSpace(userPreferredModel))
			{
				return null;
			}

			bool isOllama = IsOllamaModel(userPreferredModel);

			if (isOllama)
			{
				return await CallOllamaAsync(userPreferredModel, style, input);
			}

			string? apiKey = await ApiKeyStore.GetOpenAIApiKeyAsync();
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				return null;
			}

			try
			{
				// Try user's preferred model first, then the configured fallback
				string? result = await CallOpenAIAsync(userPreferredModel, apiKey, style, input);
				if (string.IsNullOrWhiteSpace(result) && userPreferredModel != FallbackModel)
				{
					result = await CallOpenAIAsync(FallbackModel, apiKey, style, input);
				}
				return result;
			}
			catch (Exception ex)
			{
				Logger.LogError("OpenAI rewrite failed", ex);
				return null;
			}
		}
	}
}