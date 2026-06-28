using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TypeMate.Core.AI
{
	public class OpenAIProvider : IAIProvider
	{
		private static readonly HttpClient Http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(45)
		};

		private const string FallbackModel = "gpt-4o-mini";

		public async Task<string?> RewriteAsync(string input, RewriteStyle style, string model, string? apiKey)
		{
			if (string.IsNullOrWhiteSpace(apiKey)) return null;

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
					new { role = "system", content = PromptBuilder.BuildSystemPrompt(style) },
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
				TypeMate.Logger.LogWarning($"OpenAI error ({model}): {(int)resp.StatusCode} {resp.ReasonPhrase} {respText}");
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
	}
}
