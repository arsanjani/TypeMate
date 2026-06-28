using System.Threading.Tasks;

namespace TypeMate.Core.AI
{
public interface IAIProvider {
    Task<string?> RewriteAsync(string input, RewriteStyle style, string model, string? apiKey);
}
}
