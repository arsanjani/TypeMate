using System.Threading;
using System.Threading.Tasks;

namespace TypeMate.Core.Platform
{
    public interface IClipboardCapture
    {
        Task<string?> CaptureAsync();
        Task SendPasteAsync(CancellationToken ct = default);
    }
}
