using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TypeMate;
using WpfApplication = System.Windows.Application;

namespace TypeMate.Core.Platform
{
    public class ClipboardCapture : IClipboardCapture
    {
        private const uint CF_UNICODETEXT = 13;
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;
        private const int VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static readonly object ClipboardLock = new object();
        private IntPtr _lastForegroundWindow = IntPtr.Zero;
        private int _captureInProgress = 0;

        public async Task<string?> CaptureAsync()
        {
            try
            {
                if (System.Threading.Interlocked.Exchange(ref _captureInProgress, 1) == 1)
                {
                    Logger.LogWarning("Capture skipped because another capture is in progress");
                    return null;
                }

                Logger.LogInfo("Starting text capture process");

                // Store the currently active window
                _lastForegroundWindow = NativeMethods.GetForegroundWindow();

                // Wait a bit to ensure the hotkey is released
                await Task.Delay(120);

                // Restore focus to the original window before sending Ctrl+C
                if (_lastForegroundWindow != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(_lastForegroundWindow);
                    await Task.Delay(120);
                }

                // Send Ctrl+C to copy selected text
                if (!SendCtrlC())
                {
                    Logger.LogWarning("Failed to send Ctrl+C");
                    return null;
                }

                // Wait for clipboard to be populated
                await Task.Delay(220);

                // Get text from clipboard with retries
                string selectedText = await GetClipboardTextWithRetry();

                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    Logger.LogInfo($"Captured text: {selectedText.Length} characters");
                    return selectedText;
                }

                Logger.LogInfo("No text found in clipboard after copy operation");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during text capture process", ex);
                return null;
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _captureInProgress, 0);
            }
        }

        public async Task SendPasteAsync(CancellationToken ct = default)
        {
            try
            {
                Logger.LogInfo("Attempting to paste text");

                // Try to restore focus to the original window
                if (_lastForegroundWindow != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(_lastForegroundWindow);
                    await Task.Delay(100, ct); // Give window time to gain focus
                }

                // Wait a bit before pasting
                await Task.Delay(50, ct);

                // Press Ctrl
                NativeMethods.keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
                await Task.Delay(10, ct);

                // Press V
                NativeMethods.keybd_event((byte)VK_V, 0, 0, UIntPtr.Zero);
                await Task.Delay(10, ct);

                // Release V
                NativeMethods.keybd_event((byte)VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                await Task.Delay(10, ct);

                // Release Ctrl
                NativeMethods.keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                Logger.LogInfo("Ctrl+V sent successfully");
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo("Paste operation was cancelled");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sending Ctrl+V", ex);
            }
        }

        private bool SendCtrlC()
        {
            try
            {
                NativeMethods.keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10);
                NativeMethods.keybd_event((byte)VK_C, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10);
                NativeMethods.keybd_event((byte)VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(10);
                NativeMethods.keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Logger.LogInfo("Ctrl+C sent successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sending Ctrl+C", ex);
                return false;
            }
        }

        private async Task<string> GetClipboardTextSafely()
        {
            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                lock (ClipboardLock)
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        return System.Windows.Clipboard.GetText();
                    }
                    return string.Empty;
                }
            });
        }

        private async Task<string> GetClipboardTextWithRetry()
        {
            const int maxRetries = 6;
            int retryDelay = 120;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var text = await GetClipboardTextSafely();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Clipboard access attempt {i + 1} failed: {ex.Message}");
                }

                if (i < maxRetries - 1)
                {
                    await Task.Delay(retryDelay);
                    retryDelay = Math.Min(retryDelay + 80, 400);
                }
            }

            Logger.LogWarning("All clipboard access attempts failed");
            return string.Empty;
        }

        public async Task<bool> SetClipboardText(string text)
        {
            const int maxRetries = 10;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        if (NativeMethods.OpenClipboard(IntPtr.Zero))
                        {
                            try
                            {
                                NativeMethods.EmptyClipboard();
                                var bytes = System.Text.Encoding.Unicode.GetBytes(text);
                                IntPtr hGlob = NativeMethods.GlobalAlloc(0x2, (uint)(bytes.Length + 2));
                                if (hGlob != IntPtr.Zero)
                                {
                                    IntPtr pGlob = NativeMethods.GlobalLock(hGlob);
                                    if (pGlob != IntPtr.Zero)
                                    {
                                        Marshal.Copy(bytes, 0, pGlob, bytes.Length);
                                    }
                                    NativeMethods.GlobalUnlock(hGlob);
                                    NativeMethods.SetClipboardData(CF_UNICODETEXT, hGlob);
                                }
                                NativeMethods.CloseClipboard();
                                Logger.LogInfo($"Clipboard set with {text.Length} characters via Win32 API");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"Win32 clipboard attempt {attempt + 1} failed: {ex.Message}");
                                try { NativeMethods.CloseClipboard(); } catch { }
                            }
                        }
                        if (attempt < 4) await Task.Delay(200);
                    }

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        System.Windows.Clipboard.SetText(text);
                    });
                    Logger.LogInfo($"Clipboard set with {text.Length} characters via WPF fallback");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Clipboard set retry {i + 1} failed: {ex.Message}");
                    if (i < maxRetries - 1) await Task.Delay(300);
                }
            }

            Logger.LogError("All clipboard set attempts failed");
            return false;
        }
    }
}
