# TypeMate — AI-Powered Text Rewriting for Windows

[![.NET 8](https://img.shields.io/badge/.NET-8-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Windows](https://img.shields.io/badge/platform-Windows-lightgrey)](https://windows.com)

> Select any text anywhere on Windows → press **Ctrl+Alt+R** → rewrite with AI → paste back. That's it.

TypeMate is a lightweight **Windows tray application** that lets you capture selected text, rewrite it using multiple AI providers (OpenAI, Google Gemini, OpenRouter, Ollama), and insert the result back into any application. Built with **.NET 8**, **WPF**, and **WinForms**.

[Features](#features) • [Quick Start](#quick-start) • [AI Providers](#ai-providers) • [Rewrite Styles](#rewrite-styles) • [Hotkeys](#hotkeys) • [Architecture](#architecture) • [Troubleshooting](#troubleshooting)

---

## ✨ Features

- **Multi-Provider AI** — Choose from OpenAI, Google Gemini, OpenRouter, or local Ollama models
- **Global Hotkey** — One keystroke (`Ctrl+Alt+R`) captures selected text from any application
- **Smart Rewriting** — 10 rewrite styles including translation (English ↔ Farsi), prompt engineering, social posts
- **RTL Support** — Toggle right-to-left text direction for Farsi/Persian content
- **System Tray** — Runs quietly in the background with minimal resource usage
- **Secure API Keys** — Keys encrypted with Windows DPAPI (user-scoped, machine-bound)
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
3. Choose a provider (OpenAI, Gemini, OpenRouter, or Ollama) and enter credentials
4. Select any text in any application and press **Ctrl+Alt+R**

---

## 🤖 AI Providers

| Provider | Models | API Key Required | Notes |
|----------|--------|-----------------|-------|
| **OpenAI** | `o4-mini`, `gpt-4o-mini` | ✅ Yes | Default provider with automatic fallback |
| **Google Gemini** | `gemini-flash-latest` | ✅ Yes | Fast, cost-effective rewrites |
| **OpenRouter** | Any OpenRouter-supported model | ✅ Yes | Access to 100+ models through one API |
| **Ollama** | `nemotron`, `gemma`, `qwen`, `translategemma`, etc. | ❌ No | Fully local, runs on `localhost:11434` |

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

If `Ctrl+Alt+R` is already in use, TypeMate automatically falls back to `Ctrl+Shift+R`, then `Alt+Shift+R`. The currently registered hotkey is shown in the **About** dialog (right-click tray icon → About).

---

## 🏗️ Architecture

```
TypeMate/
├── App.xaml.cs           # Entry point — wires TrayManager + GlobalHotkey
├── MainWindow.xaml.cs    # Hidden host window (provides HwndSource for hotkey registration)
├── PopupWindow.xaml.cs   # Floating editor popup — AI rewrite + insert simulation
├── TrayManager.cs        # NotifyIcon (WinForms) + context menu
├── GlobalHotkey.cs       # P/Invoke RegisterHotKey with fallback logic
├── ClipboardManager.cs   # keybd_event for Ctrl+C/Ctrl+V, window target tracking
├── OpenAIService.cs      # Multi-provider AI calls (OpenAI, Gemini, OpenRouter, Ollama)
├── ApiKeyStore.cs        # DPAPI-encrypted config persistence
├── ApiKeyDialog.xaml.cs  # Provider selection + model configuration UI
└── Logger.cs             # Append-only rotating log (~1 MB cap)
```

### Key Design Decisions

- **Single-project solution** — no unnecessary layering or abstraction
- **Flat file structure** — all source files at root, no `src/` subfolder
- **DPAPI encryption** — API keys stored via `ProtectedData`, user-scoped and machine-bound
- **Clipboard simulation** — uses `keybd_event` P/Invoke for reliable cross-application text capture and insertion
- **Owner-aware popup** — PopupWindow centers on the owning window for consistent positioning

---

## 🔧 Troubleshooting

### Hotkey not working

TypeMate registers `Ctrl+Alt+R` at startup. If another application holds this hotkey, TypeMate falls back automatically. Check the active hotkey in the **About** dialog.

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
- **Fixed hotkey options** — chooses from `Ctrl+Alt+R` → `Ctrl+Shift+R` → `Alt+Shift+R` fallback chain

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