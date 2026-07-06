using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TypeMate.Core.AI
{
	public class OpenAICompatibleProvider : IAIProvider
	{
		private static readonly HttpClient Http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(45)
		};

		public string Name { get; }
		private readonly string _baseUrl;

		public OpenAICompatibleProvider(string name, string baseUrl)
		{
			Name = name;
			string trimmed = baseUrl.TrimEnd('/');
			if (!trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
				trimmed += "/chat/completions";
			_baseUrl = trimmed;
		}

		private const int DefaultMaxTokens = 4096;

		public async Task<string?> RewriteAsync(string input, RewriteStyle style, string model, string? apiKey, int contextLength = 8192)
		{
			if (string.IsNullOrWhiteSpace(apiKey))
				throw new InvalidOperationException("API key is not configured.");
			if (string.IsNullOrWhiteSpace(_baseUrl))
				throw new InvalidOperationException("Base URL is not configured.");

			using HttpRequestMessage req = new(HttpMethod.Post, _baseUrl);
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

			int maxTokens = contextLength > 0 ? contextLength : DefaultMaxTokens;

			var payload = new
			{
				model,
				messages = new object[]
				{
					new { role = "system", content = PromptBuilder.BuildSystemPrompt(style) },
					new { role = "user", content = input }
				},
				temperature = style == RewriteStyle.PromptOptimizer ? 0.7 : 1.0,
				max_tokens = maxTokens
            };

			req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

			using HttpResponseMessage resp = await Http.SendAsync(req);
			string respText = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
			{
				string msg = $"{Name} error ({(int)resp.StatusCode} {resp.ReasonPhrase}): {respText.Trim()}";
				Logger.LogWarning($"{Name} error ({model}): {(int)resp.StatusCode} {resp.ReasonPhrase} {respText}");
				throw new InvalidOperationException(msg);
			}

			using JsonDocument doc = JsonDocument.Parse(respText);
			JsonElement root = doc.RootElement;
			if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0
				&& choices[0].TryGetProperty("message", out JsonElement message)
				&& message.TryGetProperty("content", out JsonElement content))
			{
				return content.GetString();
			}
			throw new InvalidOperationException($"{Name} returned an unexpected response format.");
		}
	}

	public class OpenAIProvider : OpenAICompatibleProvider
	{
		public OpenAIProvider() : base("OpenAI", "https://api.openai.com") { }
	}
}
