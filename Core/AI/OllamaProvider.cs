using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TypeMate.Core.AI
{
	public class OllamaProvider : IAIProvider
	{
		private static readonly HttpClient Http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(60)
		};

		private const string Endpoint = "http://localhost:11434/v1/chat/completions";

		public async Task<string?> RewriteAsync(string input, RewriteStyle style, string model, string? apiKey)
		{
			bool isSmallModel = model.Contains("0.8b", StringComparison.OrdinalIgnoreCase)
							 || model.Contains("0.5b", StringComparison.OrdinalIgnoreCase)
							 || model.Contains("1b", StringComparison.OrdinalIgnoreCase)
							 || model.Contains("270m", StringComparison.OrdinalIgnoreCase);

			int baseMaxTokens = isSmallModel ? 1024 : (style == RewriteStyle.PromptOptimizer ? 1500 : 512);

			for (int attempt = 0; attempt < 2; attempt++)
			{
				int maxTokens = attempt == 0 ? baseMaxTokens : 0;

				var payload = new
				{
					model = model,
					messages = new object[]
					{
						new { role = "system", content = PromptBuilder.BuildSystemPrompt(style) },
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
					using HttpResponseMessage resp = await Http.PostAsync(Endpoint, body);
					string respText = await resp.Content.ReadAsStringAsync();

					if (!resp.IsSuccessStatusCode)
					{
						TypeMate.Logger.LogWarning($"Ollama error ({model}): {(int)resp.StatusCode} {resp.ReasonPhrase} {respText}");
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
							if (!string.IsNullOrEmpty(result)) return result;
							TypeMate.Logger.LogInfo($"Ollama ({model}) returned empty content (attempt {attempt + 1}), retrying...");
						}
					}
				}
				catch (Exception ex)
				{
					TypeMate.Logger.LogError($"Ollama connection failed for model {model}", ex);
					return null;
				}
			}

			TypeMate.Logger.LogWarning($"Ollama ({model}) consistently returned empty response");
			return null;
		}
	}
}
