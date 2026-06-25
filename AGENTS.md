<!-- TypeMate AGENTS.md -->

## Project

Single-project .NET 8 WPF+WinForms tray app. Select text anywhere on Windows, press Ctrl+Alt+R, rewrite with AI (OpenAI or Ollama), paste back. No tests, no linting, no CI.

## Platform

- **Windows only** (`net8.0-windows`). Both `<UseWPF>` and `<UseWindowsForms>` enabled.
- Requires a running Desktop session — tray icon, global hotkeys, and keyboard simulation won't work in server containers or headless environments.

## Commands

| Command | Purpose |
|---------|---------|
| `dotnet build` | Build (produces `bin/Debug/net8.0-windows/TypeMate.exe`) |
| `dotnet run` | Run from source |
| `dotnet publish -c Release /p:PublishSingleFile=true` | Self-contained single-file executable |

No typecheck, lint, format, test, or codegen steps. A plain `dotnet build` is the only required verification.

## Architecture — File Map

All source files sit flat in the repository root (no `src/` subfolder). Key files:

| File | Role |
|------|------|
| `App.xaml.cs` | Entry point. Wires `TrayManager` + `GlobalHotkey`, catches all exceptions, manages shutdown. |
| `MainWindow.xaml` / `.cs` | Minimal host window — kept hidden solely to provide an `HwndSource` for hotkey registration via `WindowInteropHelper`. |
| `PopupWindow.xaml` / `.cs` | Floating editor popup. Shows captured text, calls `OpenAIService.RewriteAsync`, simulates Ctrl+V on Insert. |
| `TrayManager.cs` | `NotifyIcon` (WinForms) + context menu. Left-click / double-click opens freestyle editor. |
| `GlobalHotkey.cs` | P/Invoke `RegisterHotKey` / `UnregisterHotKey`. Registers Ctrl+Alt+R; falls back to Ctrl+Shift+R or Alt+Shift+R if taken. |
| `ClipboardManager.cs` | P/Invoke `keybd_event` to simulate Ctrl+C (capture) and Ctrl+V (paste). Stores `_lastForegroundWindow` handle for paste targeting. Thread-safe capture gate via `Interlocked.Exchange`. |
| `OpenAIService.cs` | Calls OpenAI (`api.openai.com`) or Ollama (`localhost:11434`). Per-style system prompts, fallback model logic, retry on empty Ollama responses. |
| `ApiKeyStore.cs` | Persists config to `%AppData%\TypeMate\config.json`. API key encrypted/decrypted via `ProtectedData` (DPAPI). |
| `Logger.cs` | Append-only log at `%LocalAppData%\TypeMate\typemate.log`, capped at ~1 MB. |

## Runtime Behavior to Know

- **Startup**: `App.OnStartup` creates hidden MainWindow → registers global hotkey → shows tray icon. App never shows a main window.
- **Hotkey flow**: Press Ctrl+Alt+R → `GlobalHotkey.WndProc` fires event → `ClipboardManager.CaptureSelectedText()` simulates Ctrl+C on the foreground window → waits for clipboard content → opens `PopupWindow`.
- **Insert flow**: Click Insert → sets clipboard text → closes PopupWindow → background task waits 300 ms → simulates Ctrl+V to restore focus window.
- **API key storage**: Uses Windows DPAPI (`ProtectedData`). Keys are user-scoped and machine-bound; won't decrypt on a different user/machine.

## Quirks

- XAML files use code-behind pattern (partial classes). Generated files go to `obj/` via WPF markup compile — never edit files under `obj/`.
- `ClipboardManager` relies on timing delays around `keybd_event`; changes there affect reliability across apps with different input latency.
- The `.ico` file name contains spaces and dates — it's referenced literally in the `.csproj`. Renaming it requires updating `<ApplicationIcon>`.
