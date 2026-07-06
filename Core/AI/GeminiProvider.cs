using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TypeMate.Core.AI
{
	public class GeminiProvider : IAIProvider
	{
		private static readonly HttpClient Http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(45)
		};

		private const int DefaultMaxOutputTokens = 2048;

		public async Task<string?> RewriteAsync(string input, RewriteStyle style, string model, string? apiKey, int contextLength = 131072)
		{
			if (string.IsNullOrWhiteSpace(apiKey))
				throw new InvalidOperationException("API key is not configured.");

			Http.DefaultRequestHeaders.Clear();
			Http.DefaultRequestHeaders.Add("X-goog-api-key", apiKey);

			var payload = new
			{
				contents = new object[]
				{
					new { parts = new object[] { new { text = input } } }
				},
				systemInstruction = new { parts = new object[] { new { text = PromptBuilder.BuildSystemPrompt(style) } } },
				generationConfig = new
				{
					temperature = style == RewriteStyle.PromptOptimizer ? 0.3 : 0.7,
					maxOutputTokens = Math.Min(contextLength > 0 ? contextLength : DefaultMaxOutputTokens,
						style == RewriteStyle.PromptOptimizer ? 1500 : 1024)
				}
			};

			string json = JsonSerializer.Serialize(payload);
			using StringContent body = new StringContent(json, Encoding.UTF8, "application/json");
			using HttpResponseMessage resp = await Http.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent", body);
			string respText = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
			{
				string msg = $"Gemini error ({(int)resp.StatusCode} {resp.ReasonPhrase}): {respText.Trim()}";
				TypeMate.Logger.LogWarning($"Gemini error ({model}): {(int)resp.StatusCode} {resp.ReasonPhrase} {respText}");
				throw new InvalidOperationException(msg);
			}

			using JsonDocument doc = JsonDocument.Parse(respText);
			JsonElement root = doc.RootElement;
			if (root.TryGetProperty("candidates", out JsonElement candidates) && candidates.GetArrayLength() > 0)
			{
				JsonElement first = candidates[0];
				if (first.TryGetProperty("content", out JsonElement content) && content.TryGetProperty("parts", out JsonElement parts) && parts.GetArrayLength() > 0)
				{
					return parts[0].TryGetProperty("text", out JsonElement text) ? text.GetString() : null;
				}
			}
			throw new InvalidOperationException("Gemini returned an unexpected response format.");
		}
	}
}
