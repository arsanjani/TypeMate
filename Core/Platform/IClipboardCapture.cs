using System.Threading;
using System.Threading.Tasks;

namespace TypeMate.Core.Platform
{
    public interface IClipboardCapture
    {
        Task<string?> CaptureAsync();
        Task<bool> SetClipboardText(string text);
        Task SendPasteAsync(CancellationToken ct = default);
    }
}
