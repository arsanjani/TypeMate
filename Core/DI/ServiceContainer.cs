using TypeMate.Core.AI;
using TypeMate.Core.Config;
using TypeMate.Core.Platform;

namespace TypeMate.Core.DI
{
    /// <summary>Simple DI container — single instance, no framework.</summary>
    public class ServiceContainer : IDisposable
    {
        public static readonly ServiceContainer Instance = new ServiceContainer();

        private IClipboardCapture? _clipboard;
        private Rewriter? _rewriter;
        public IHotkeyManager? Hotkey { get; internal set; }
        public Services.TrayService? Tray { get; internal set; }

        public IClipboardCapture Clipboard => _clipboard ??= new ClipboardCapture();
        public Rewriter Rewriter => _rewriter ??= CreateRewriter();

        public void Dispose()
        {
            Hotkey?.Dispose();
            Tray?.Dispose();
        }

        private Rewriter CreateRewriter()
        {
            var store = new JsonConfigStore();
            return new Rewriter(store,
                new OpenAIProvider(),
                new GeminiProvider(),
                new OllamaProvider(),
                new OpenRouterProvider());
        }
    }
}