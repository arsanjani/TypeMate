using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace TypeMate
{
    public static class ClipboardManager
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndOwner);

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;
        private const int VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        
        private static readonly object ClipboardLock = new object();
        private static IntPtr _lastForegroundWindow = IntPtr.Zero;
        private static int _captureInProgress = 0;

        public static void CaptureSelectedText()
        {
            Task.Run(async () =>
            {
                try
                {
                    if (System.Threading.Interlocked.Exchange(ref _captureInProgress, 1) == 1)
                    {
                        Logger.LogWarning("Capture skipped because another capture is in progress");
                        return;
                    }

                    Logger.LogInfo("Starting text capture process");
                    
                    // Store the currently active window
                    _lastForegroundWindow = GetForegroundWindow();

                    // Wait a bit to ensure the hotkey is released
                    await Task.Delay(120);

                    // Restore focus to the original window before sending Ctrl+C
                    if (_lastForegroundWindow != IntPtr.Zero)
                    {
                        SetForegroundWindow(_lastForegroundWindow);
                        await Task.Delay(120);
                    }

                    // Attempt to copy current selection

                    // Send Ctrl+C to copy selected text
                    if (!SendCtrlC())
                    {
                        Logger.LogWarning("Failed to send Ctrl+C");
                        ShowErrorMessage("Could not copy selected text. Please try again.");
                        return;
                    }

                    // Wait for clipboard to be populated
                    await Task.Delay(220);

                    // Get text from clipboard with retries
                    string selectedText = await GetClipboardTextWithRetry();

                    if (!string.IsNullOrWhiteSpace(selectedText))
                    {
                        Logger.LogInfo($"Captured text: {selectedText.Length} characters");
                        ShowPopupWindow(selectedText);
                    }
                    else
                    {
                        Logger.LogInfo("No text found in clipboard after copy operation");
                        ShowInfoMessage("No text was captured. Please select some text and try again.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error during text capture process", ex);
                    ShowErrorMessage("An error occurred while capturing text. Please try again.");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _captureInProgress, 0);
                }
            });
        }

        private static bool SendCtrlC()
        {
            try
            {
                // Press Ctrl
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10);
                
                // Press C
                keybd_event(VK_C, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10);
                
                // Release C
                keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(10);
                
                // Release Ctrl
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                
                Logger.LogInfo("Ctrl+C sent successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sending Ctrl+C", ex);
                return false;
            }
        }

        public static async Task<bool> SendCtrlV()
        {
            try
            {
                Logger.LogInfo("Attempting to paste text");
                
                // Try to restore focus to the original window
                if (_lastForegroundWindow != IntPtr.Zero)
                {
                    SetForegroundWindow(_lastForegroundWindow);
                    await Task.Delay(100); // Give window time to gain focus
                }

                // Wait a bit before pasting
                await Task.Delay(50);

                // Press Ctrl
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                await Task.Delay(10);
                
                // Press V
                keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                await Task.Delay(10);
                
                // Release V
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                await Task.Delay(10);
                
                // Release Ctrl
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                Logger.LogInfo("Ctrl+V sent successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sending Ctrl+V", ex);
                return false;
            }
        }

        private static async Task<string> GetClipboardTextSafely()
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

        private static async Task<string> GetClipboardTextWithRetry()
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

        private static void ShowPopupWindow(string text)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var popup = new PopupWindow(text);
                        popup.Show();
                        Logger.LogInfo("Popup window displayed successfully");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error showing popup window", ex);
                        ShowErrorMessage("Failed to open text editor window.");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error dispatching popup window creation", ex);
            }
        }

        public static async Task<bool> SetClipboardText(string text)
        {
            const int maxRetries = 10;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Try Win32 clipboard API directly - more reliable than WPF Clipboard
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        if (OpenClipboard(IntPtr.Zero))
                        {
                            try
                            {
                                EmptyClipboard();
                                var bytes = System.Text.Encoding.Unicode.GetBytes(text);
                                // Allocate global memory (clipboard requires movable global memory)
                                IntPtr hGlob = GlobalAlloc(0x2, (uint)(bytes.Length + 2));
                                if (hGlob != IntPtr.Zero)
                                {
                                    IntPtr pGlob = GlobalLock(hGlob);
                                    if (pGlob != IntPtr.Zero)
                                    {
                                        Marshal.Copy(bytes, 0, pGlob, bytes.Length);
                                    }
                                    GlobalUnlock(hGlob);
                                    SetClipboardData(CF_UNICODETEXT, hGlob);
                                }
                                CloseClipboard();
                                Logger.LogInfo($"Clipboard set with {text.Length} characters via Win32 API");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"Win32 clipboard attempt {attempt + 1} failed: {ex.Message}");
                                try { CloseClipboard(); } catch { }
                            }
                        }
                        if (attempt < 4) await Task.Delay(200);
                    }

                    // Fallback to WPF Clipboard
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

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, uint dwBytes);

        [DllImport("user32.dll")]
        private static extern bool SetClipboardData(uint uFormat, IntPtr hMem);

        private static void ShowErrorMessage(string message)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    System.Windows.MessageBox.Show(message, "TypeMate Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show error message: {message}", ex);
            }
        }

        private static void ShowInfoMessage(string message)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    System.Windows.MessageBox.Show(message, "TypeMate", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show info message: {message}", ex);
            }
        }
    }
}
