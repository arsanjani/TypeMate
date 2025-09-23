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

        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;
        private const int VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        
        private static readonly object ClipboardLock = new object();
        private static IntPtr _lastForegroundWindow = IntPtr.Zero;

        public static void CaptureSelectedText()
        {
            Task.Run(async () =>
            {
                try
                {
                    Logger.LogInfo("Starting text capture process");
                    
                    // Store the currently active window
                    _lastForegroundWindow = GetForegroundWindow();
                    
                    // Wait a bit to ensure the hotkey is released
                    await Task.Delay(150);

                    // Store original clipboard content to restore later if needed
                    string? originalClipboard = null;
                    try
                    {
                        originalClipboard = await GetClipboardTextSafely();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Could not backup original clipboard content: {ex.Message}");
                    }

                    // Send Ctrl+C to copy selected text
                    if (!SendCtrlC())
                    {
                        Logger.LogWarning("Failed to send Ctrl+C");
                        ShowErrorMessage("Could not copy selected text. Please try again.");
                        return;
                    }

                    // Wait for clipboard to be populated
                    await Task.Delay(200);

                    // Get text from clipboard with retries
                    string selectedText = await GetClipboardTextWithRetry();

                    if (!string.IsNullOrWhiteSpace(selectedText))
                    {
                        // Check if the text is different from what was originally in clipboard
                        if (selectedText != originalClipboard)
                        {
                            Logger.LogInfo($"Captured text: {selectedText.Length} characters");
                            ShowPopupWindow(selectedText);
                        }
                        else
                        {
                            Logger.LogInfo("No new text was selected");
                            ShowInfoMessage("No text was selected. Please select some text and try again.");
                        }
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
            const int maxRetries = 3;
            const int retryDelay = 100;

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
            try
            {
                return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    lock (ClipboardLock)
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(text);
                            Logger.LogInfo($"Clipboard set with {text.Length} characters");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error setting clipboard text", ex);
                            return false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error dispatching clipboard set operation", ex);
                return false;
            }
        }

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
