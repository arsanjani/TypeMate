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
		EnglishToFarsi,
		FarsiToEnglish
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
					return "You are an expert prompt engineer specializing in software development tasks for AI coding agents (Cursor, Copilot, Claude Code, etc.). Transform the user's input into a precision-engineered prompt using the following structure:\n\n**ROLE**: Assign a specific technical role (e.g., \"Senior React developer\", \"Python backend architect\", \"DevOps engineer\") inferred from the input context.\n\n**OBJECTIVE**: One clear, actionable statement of what to build, fix, or refactor (imperative voice).\n\n**CONTEXT**: Tech stack, frameworks, languages detected. Relevant existing code/architecture references. Environment constraints.\n\n**REQUIREMENTS**: Numbered list of functional must-haves derived from the input.\n\n**CONSTRAINTS**: Performance, security, and style guidelines. Explicitly state what NOT to do. Files or modules to avoid modifying.\n\n**DELIVERABLES**: Exact outputs expected (specific code files, tests, migrations, config changes).\n\n**ACCEPTANCE CRITERIA**: Testable conditions that define \"done\".\n\nRules:\n- Infer tech stack from code snippets, file names, or keywords in the input.\n- Be specific and precise — AI coding agents fail on ambiguity.\n- Include edge cases the solution must handle.\n- If input is vague or underspecified, explicitly state your assumptions.\n- Preserve important identifiers (class names, function names, file paths) from the input.\n- Output ONLY the final optimized prompt. No explanations, no preamble.";
				case RewriteStyle.EnglishToFarsi:
					return "You are a professional English (en) to Persian (fa-IR) translator. Your goal is to accurately convey the meaning and nuances of the original English text while adhering to Persian grammar, vocabulary, and cultural sensitivities.\n\nProduce only the Persian translation, without any additional explanations or commentary. Please translate the following English text into the Persian:\n\n";
				case RewriteStyle.FarsiToEnglish:
					return "You are a professional Persian (fa-IR) to English (en) translator. Your goal is to accurately convey the meaning and nuances of the original Persian text while producing natural, idiomatic English output.\n\nProduce only the English translation, without any additional explanations or commentary. Please translate the following Persian text into English:\n\n";
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
				temperature = style == RewriteStyle.PromptOptimizer ? 0.3 : 0.7,
				max_tokens = (style == RewriteStyle.PromptOptimizer) ? 1500 : 512
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
			bool isSmallModel = model.Contains("0.8b", StringComparison.OrdinalIgnoreCase)
							 || model.Contains("0.5b", StringComparison.OrdinalIgnoreCase)
							 || model.Contains("1b", StringComparison.OrdinalIgnoreCase)
							 || model.Contains("270m", StringComparison.OrdinalIgnoreCase);

			int baseMaxTokens = isSmallModel ? 1024 : (style == RewriteStyle.PromptOptimizer ? 1500 : 512);

			// Try with specified max_tokens first, then retry without limit if content was empty
			for (int attempt = 0; attempt < 2; attempt++)
			{
				int maxTokens = attempt == 0 ? baseMaxTokens : 0; // 0 = unlimited in Ollama

				var payload = new
				{
					model = model,
					messages = new object[]
					{
						new { role = "system", content = BuildSystemPrompt(style) },
						new { role = "user", content = input }
					},
					temperature = style == RewriteStyle.PromptOptimizer ? 0.3 : 0.7,
					stream = false,
					max_tokens = maxTokens,
					keep_alive = -1
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
							string result = content.GetString() ?? "";
							if (!string.IsNullOrEmpty(result))
							{
								return result;
							}
							// Empty content - retry with no limit on second attempt
							Logger.LogInfo($"Ollama ({model}) returned empty content (attempt {attempt + 1}), retrying...");
						}
					}
					continue;
				}
				catch (Exception ex)
				{
					Logger.LogError($"Ollama connection failed for model {model}", ex);
					return null;
				}
			}

			Logger.LogWarning($"Ollama ({model}) consistently returned empty response");
			return null;
		}

		public static async Task<string?> RewriteAsync(string input, RewriteStyle style)
		{
			string? userPreferredModel = await ApiKeyStore.GetPreferredModelAsync();
			if (string.IsNullOrWhiteSpace(userPreferredModel))
			{
				return null;
			}

			string? provider = await ApiKeyStore.GetProviderAsync();
			if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
			{
				string? apiKey = await ApiKeyStore.GetOpenAIApiKeyAsync();
				if (string.IsNullOrWhiteSpace(apiKey))
				{
					return null;
				}

				try
				{
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

			return await CallOllamaAsync(userPreferredModel, style, input);
		}
	}
}