# TypeMate - AI-Powered Text Capture & Rewriting Tool

🚀 **Instant text capture and AI-powered rewriting** - Select any text, press Ctrl+Alt+R, and transform your writing with this lightweight Windows tray application. Perfect for writers, developers, and productivity enthusiasts.

## Features

- **System Tray Operation**: Runs quietly in the background with a system tray icon
- **Global Hotkey**: Press `Ctrl + Alt + R` from any application to capture selected text
- **Text Capture**: Automatically copies selected text using simulated `Ctrl+C`
- **Popup Editor**: Shows a popup window where you can edit the captured text
- **AI Text Rewriting**: Uses OpenAI to rewrite captured text with a selection of styles (EasyRead, Witty, Formal, Summarise, Expand, LinkedInPost). Requires an OpenAI API key configured in the app.
- **Text Insertion**: Click "Insert" to paste the modified text back to the original application

## How to Use

1. **Build and Run**:

   ```bash
   dotnet build
   dotnet run
   ```

2. **Using the Application**:
   - The app will start and appear as a tray icon
   - Select any text in any application (Word, browser, email client, etc.)
   - Press `Ctrl + Alt + R` to capture the selected text
   - A popup window will appear with the captured text
   - Click "Rewrite" to modify the text (adds "[Rewritten] " prefix)
   - Click "Insert" to paste the modified text back to the original application
   - Click "Cancel" or press `Escape` to close the popup without changes

3. **Tray Icon**:
   - Double-click the tray icon to see usage instructions
   - Right-click the tray icon and select "Exit" to quit the application

## Technical Details

- **.NET 8**: Built on .NET 8 with Windows-specific features
- **WPF**: Uses Windows Presentation Foundation for the popup UI
- **System Tray**: Uses `System.Windows.Forms.NotifyIcon` for tray functionality
- **Global Hotkeys**: Uses Windows API (`RegisterHotKey`) for system-wide hotkey capture
- **Clipboard Operations**: Uses Windows clipboard API for text capture and insertion
- **Keystroke Simulation**: Uses Windows API (`keybd_event`) for simulating Ctrl+C and Ctrl+V

## Requirements

- Windows OS
- .NET 8 Runtime
- Administrator privileges may be required for global hotkey registration

## Reliability Features

**Long-Term Background Operation:**

- Comprehensive exception handling prevents crashes
- Automatic resource cleanup and memory management
- Thread-safe clipboard operations with retry logic
- Robust hotkey registration with failure recovery
- Graceful degradation when components fail
- Built-in logging for troubleshooting (logs stored in `%LocalAppData%\TypeMate\`)

**Error Recovery:**

- Hotkey re-registration if lost during system events
- Tray icon restoration after Windows session changes
- Clipboard operation retries with fallback handling
- Background task exception handling without UI crashes

**Monitoring & Debugging:**

- Automatic log rotation to prevent disk space issues
- Detailed logging of all operations and errors
- Performance-optimized clipboard access patterns
- Memory leak prevention through proper resource disposal

## Limitations (MVP)

- Fixed hotkey (Ctrl+Alt+R) - no customization UI
- Plain text only - no rich text formatting
- Windows only - no cross-platform support

---

## Topics

`text-capture` `text-rewriting` `productivity-tool` `wpf-application` `system-tray` `global-hotkey` `clipboard-manager` `writing-assistant` `dotnet-8` `c-sharp`

---

**⭐ Star this repo if you find TypeMate useful!**

[Report Bug](https://github.com/arsanjani/TypeMate/issues) • [Request Feature](https://github.com/arsanjani/TypeMate/issues)
