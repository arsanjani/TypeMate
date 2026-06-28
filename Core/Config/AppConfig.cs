using System.Security.Cryptography;
using System.Text;

namespace TypeMate.Core.Config
{
    public class AppConfig
    {
        public string? EncryptedOpenAIApiKeyBase64 { get; set; }
        public string? EncryptedGeminiApiKeyBase64 { get; set; }
        public string? EncryptedOpenRouterApiKeyBase64 { get; set; }
        public string? PreferredModel { get; set; }
        public string? Provider { get; set; } // "openai", "gemini", "ollama", or "openrouter"

        public static byte[] Encrypt(string plainText)
        {
            byte[] plain = Encoding.UTF8.GetBytes(plainText);
            return ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        }

        public static string DecryptBase64(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return string.Empty;
            byte[] encrypted = Convert.FromBase64String(base64);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
