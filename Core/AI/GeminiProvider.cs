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

		public async Task<string?> RewriteAsync(string input, RewriteStyle style, string model, string? apiKey)
		{
			if (string.IsNullOrWhiteSpace(apiKey)) return null;

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
					maxOutputTokens = (style == RewriteStyle.PromptOptimizer) ? 1500 : 1024
				}
			};

			string json = JsonSerializer.Serialize(payload);
			using StringContent body = new StringContent(json, Encoding.UTF8, "application/json");
			using HttpResponseMessage resp = await Http.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent", body);
			string respText = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
			{
				TypeMate.Logger.LogWarning($"Gemini error ({model}): {(int)resp.StatusCode} {resp.ReasonPhrase} {respText}");
				return null;
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
			return null;
		}
	}
}
