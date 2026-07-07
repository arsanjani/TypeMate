# TypeMate — AI-Powered Text Rewriting for Windows

[![.NET 8](https://img.shields.io/badge/.NET-8-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Windows](https://img.shields.io/badge/platform-Windows-lightgrey)](https://windows.com)

> Select any text anywhere on Windows → press **Ctrl+Alt+R** → rewrite with AI → paste back. That's it.

TypeMate is a lightweight **Windows tray application** that lets you capture selected text, rewrite it using multiple AI providers (OpenAI, Google Gemini, OpenRouter, Ollama, or any OpenAI-compatible endpoint), and insert the result back into any application. Built with **.NET 8**, **WPF**, and **WinForms**.

[Features](#features) • [Quick Start](#quick-start) • [AI Providers](#ai-providers) • [Rewrite Styles](#rewrite-styles) • [Hotkeys](#hotkeys) • [Architecture](#architecture) • [Troubleshooting](#troubleshooting)

---

## ✨ Features

- **Multi-Provider AI** — Choose from OpenAI, Google Gemini, OpenRouter, local Ollama, or any OpenAI-compatible endpoint (LM Studio, vLLM, custom servers)
- **Global Hotkey** — One keystroke (`Ctrl+Alt+R`) captures selected text from any application
- **Smart Rewriting** — 10 rewrite styles including translation (English ↔ Farsi), prompt engineering, social posts
- **RTL Support** — Toggle right-to-left text direction for Farsi/Persian content
- **Toast Notifications** — Auto-dismissing notifications for hotkey fallbacks and status updates
- **System Tray** — Runs quietly in the background with minimal resource usage
- **Secure API Keys** — Keys encrypted with Windows DPAPI (user-scoped, machine-bound), supports per-provider key storage
- **Auto-Focus** — Inserts rewritten text back into the original application window
- **Robust Error Handling** — Built for long-term background operation without crashes

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10/11** (tray icon, global hotkeys, and keyboard simulation require a Desktop session)
- **.NET 8 Runtime**

### Build & Run

```bash
# Clone the repository
git clone https://github.com/arsanjani/TypeMate.git
cd TypeMate

# Build the project
dotnet build

# Run from source
dotnet run
```

### Produce a Standalone Executable

```bash
dotnet publish -c Release /p:PublishSingleFile=true
```

Output: `bin/Release/net8.0-windows/publish/TypeMate.exe`

### First-Time Setup

1. TypeMate starts as a **tray icon** (no main window)
2. Right-click the tray icon → **Set API Key** to configure your AI provider
3. Choose a provider (OpenAI, Gemini, OpenRouter, Ollama, or OpenAI Compatible) and enter credentials
4. For OpenAI Compatible providers, you can also set a custom base URL, model name, and context window size
5. Select any text in any application and press **Ctrl+Alt+R**

---

## 🤖 AI Providers

| Provider | Models | API Key Required | Notes |
|----------|--------|-----------------|-------|
| **OpenAI** | `o4-mini`, `gpt-4o-mini` | ✅ Yes | Default provider with automatic fallback |
| **Google Gemini** | `gemini-flash-latest` | ✅ Yes | Fast, cost-effective rewrites (128K context) |
| **OpenRouter** | Any OpenRouter-supported model | ✅ Yes | Access to 100+ models through one API |
| **Ollama** | `nemotron`, `gemma`, `qwen`, `translategemma`, etc. | ❌ No | Fully local, runs on `localhost:11434` |
| **OpenAI Compatible** | Any model on a compatible endpoint | ✅ Yes | Connect to any OpenAI-compatible API (LM Studio, vLLM, custom servers) |

### Switching Providers

Right-click the tray icon → **Set API Key** → select your preferred provider and model. Settings are saved to `%AppData%\TypeMate\config.json`.

---

## ✍️ Rewrite Styles

Access styles by clicking the **AI Tools** button in the popup or via the context menu:

| Style | Description |
|-------|-------------|
| **Easy Read** | Clear, simple, accessible language |
| **Witty** | Playful, clever phrasing with personality |
| **Formal** | Polished, professional tone for business communication |
| **Summarise** | Concise 3-5 bullet point summary |
| **Expand** | Elaborate with helpful context and examples |
| **LinkedIn Post** | Professional LinkedIn-style post with hook and CTA |
| **Prompt Optimizer** | Transform input into a high-signal prompt for AI coding agents (Cursor, Copilot, Claude Code) |
| **English → Farsi** | Translate English text to Persian (best with `translategemma:4b`) |
| **Farsi → English** | Translate Persian text to natural, idiomatic English |
| **Twitter Post (Farsi)** | Rewrite any text as a professional Farsi Twitter/X post (RTL) |

---

## ⌨️ Hotkeys

| Hotkey | Action |
|--------|--------|
| `Ctrl + Alt + R` | Capture selected text and open editor popup |
| `Escape` | Close popup without changes |

If `Ctrl+Alt+R` is already in use, TypeMate automatically falls back through a chain: `Ctrl+Alt+T` → `Ctrl+Alt+Y` → `Ctrl+Alt+I`. A toast notification appears if a fallback shortcut is assigned. The currently registered hotkey is shown in the **About** dialog (right-click tray icon → About).

---

## 🏗️ Architecture

```
TypeMate/
├── App.xaml.cs               # Entry point — delegates to AppBootstrapper
├── AppBootstrapper.cs        # Wires DI, hotkey registration, tray service, and shutdown
├── Core/                     # Core logic and abstractions
│   ├── AI/                   # Multi-provider AI rewriting
│   │   ├── IAIProvider.cs          # Provider interface
│   │   ├── Rewriter.cs             # Provider resolution + rewrite orchestration
│   │   ├── RewriteStyle.cs         # Styles + PromptBuilder
│   │   ├── OpenAICompatibleProvider.cs  # Base for OpenAI-compatible APIs
│   │   ├── GeminiProvider.cs       # Google Gemini
│   │   ├── OllamaProvider.cs       # Local Ollama
│   │   └── OpenRouterProvider.cs   # OpenRouter gateway
│   ├── Config/               # Configuration persistence
│   │   ├── AppConfig.cs              # Config model with DPAPI encrypt/decrypt
│   │   ├── IConfigStore.cs           # Store interface
│   │   └── JsonConfigStore.cs        # JSON file-backed store
│   ├── DI/                   # Lightweight dependency injection
│   │   └── ServiceContainer.cs       # Singleton container for all services
│   ├── Notifications/        # Toast-style notification system
│   │   ├── NotificationWindow.xaml
│   │   └── NotificationWindow.xaml.cs
│   ├── Platform/             # OS-level abstractions
│   │   ├── IClipboardCapture.cs      # Clipboard capture interface
│   │   ├── ClipboardCapture.cs       # keybd_event-based implementation
│   │   ├── IHotkeyManager.cs         # Global hotkey interface
│   │   ├── HotkeyManager.cs          # RegisterHotKey P/Invoke implementation
│   │   ├── Hotcode.cs                # Hotkey shortcut definition
│   │   └── NativeMethods.cs          # Win32 P/Invoke helpers
│   └── Logger.cs             # Append-only rotating log (~1 MB cap)
├── Services/                 # Cross-cutting services
│   └── TrayService.cs        # NotifyIcon (WinForms) + context menu
└── UI/                       # Presentation layer
    ├── MainWindow.xaml/.cs           # Hidden host window (HwndSource for hotkeys)
    ├── PopupWindow.xaml/.cs          # Floating editor popup — AI rewrite + insert
    ├── PopupWindowExtensions.cs      # ShowAndWaitAsync helper
    └── Dialogs/                      # Modal dialogs
        ├── AboutDialog.xaml/.cs
        └── ApiKeyDialog.xaml/.cs     # Provider selection + model config UI
```

### Key Design Decisions

- **Layered architecture** — `Core/` for business logic, `Services/` for cross-cutting concerns, `UI/` for presentation
- **Interface-driven platform layer** — `IClipboardCapture` and `IHotkeyManager` abstract OS-level operations behind testable contracts
- **Simple DI container** — `ServiceContainer` provides singleton services without a heavy framework
- **Strategy pattern for AI providers** — `IAIProvider` interface with `Rewriter` orchestrating provider resolution and model selection
- **OpenAI-compatible abstraction** — `OpenAICompatibleProvider` serves as base class for any OpenAI-compatible API (including custom endpoints)
- **Bootstrapper pattern** — `AppBootstrapper` centralizes startup wiring, hotkey registration with fallback chain, and shutdown coordination
- **Toast notifications** — `NotificationWindow` provides auto-dismissing info/warning/error toasts with progress animation

---

## 🔧 Troubleshooting

### Hotkey not working

TypeMate registers `Ctrl+Alt+R` at startup. If another application holds this hotkey, TypeMate falls back automatically through `Ctrl+Alt+T` → `Ctrl+Alt+Y` → `Ctrl+Alt+I`. A toast notification appears when a fallback is used. Check the active hotkey in the **About** dialog.

### "Failed to rewrite" error

1. Verify your API key: right-click tray icon → **Set API Key**
2. Check network connectivity (Ollama requires `localhost:11434` running)
3. Review logs at `%LocalAppData%\TypeMate\typemate.log`

### Insert doesn't paste text

The insert flow simulates `Ctrl+V` to the last-active foreground window. Make sure the target application is still focused when you click Insert. Some applications (e.g., elevated admin windows) may block simulated input.

### API key won't decrypt on another machine

Keys are encrypted with Windows DPAPI and are **user-scoped + machine-bound**. You must re-enter your API key on a different computer or user account.

---

## 📝 Limitations (Current Release)

- **Windows only** — relies on Win32 P/Invoke for hotkeys, tray icon, and keyboard simulation
- **Plain text** — no rich text or formatting preservation
- **Fixed hotkey options** — chooses from `Ctrl+Alt+R` → `Ctrl+Alt+T` → `Ctrl+Alt+Y` → `Ctrl+Alt+I` fallback chain

---

## 📄 License

MIT — see [LICENSE](LICENSE) for details.

---

## 🙋 FAQ

**Q: Can I use TypeMate without an API key?**
Yes — Ollama provider runs entirely locally with no API key required. Install [Ollama](https://ollama.com) and pull any supported model.

**Q: Which OpenAI models work best for rewriting?**
`o4-mini` is the preferred model with `gpt-4o-mini` as automatic fallback. Both are fast and cost-effective.

**Q: Does TypeMate send my text anywhere?**
Only when using cloud providers (OpenAI, Gemini, OpenRouter). Ollama processes everything locally on your machine.

---

## Topics

`text-rewriting` `ai-writing-assistant` `productivity-tool` `windows-tray-app` `global-hotkey` `openai` `gemini` `ollama` `openrouter` `wpf` `dotnet-8` `csharp` `clipboard-manager` `prompt-engineering` `farsi-translation`

---

**⭐ If you find TypeMate useful, consider starring the repository!**

[Report Bug](https://github.com/arsanjani/TypeMate/issues) • [Request Feature](https://github.com/arsanjani/TypeMate/issues)