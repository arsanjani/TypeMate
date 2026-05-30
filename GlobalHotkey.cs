using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TypeMate
{
    public class GlobalHotkey : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;
        
        // Windows API constants for modifiers
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        
        // Virtual key codes
        private const uint VK_R = 0x52;
        
        private HwndSource? _hwndSource;
        private IntPtr _hwnd;
        private bool _disposed = false;
        private bool _isRegistered = false;
        private uint _modifiers;
        private uint _key;

        public event EventHandler? HotkeyPressed;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        public bool RegisterHotkey(System.Windows.Window window, uint modifiers, uint key)
        {
            try
            {
                if (_disposed)
                {
                    Logger.LogWarning("Attempted to register hotkey on disposed GlobalHotkey instance");
                    return false;
                }

                _modifiers = modifiers;
                _key = key;

                Logger.LogInfo($"Attempting to register hotkey with modifiers: 0x{modifiers:X}, key: 0x{key:X}");

                var helper = new WindowInteropHelper(window);
                _hwnd = helper.EnsureHandle();

                if (_hwnd == IntPtr.Zero)
                {
                    Logger.LogError("Failed to get window handle for hotkey registration");
                    return false;
                }

                Logger.LogInfo($"Window handle obtained: 0x{_hwnd:X}");

                _hwndSource = HwndSource.FromHwnd(_hwnd);
                if (_hwndSource == null)
                {
                    Logger.LogError("Failed to create HwndSource for hotkey registration");
                    return false;
                }

                _hwndSource.AddHook(WndProc);
                Logger.LogInfo("Window procedure hook added successfully");

                // Attempt to register the hotkey with retries
                const int maxRetries = 5;
                for (int i = 0; i < maxRetries; i++)
                {
                    Logger.LogInfo($"Hotkey registration attempt {i + 1}/{maxRetries}");
                    bool success = RegisterHotKey(_hwnd, HOTKEY_ID, modifiers, key);
                    
                    if (success)
                    {
                        _isRegistered = true;
                        Logger.LogInfo($"Hotkey registered successfully on attempt {i + 1}! Hotkey: Ctrl+Alt+R");
                        return true;
                    }

                    uint errorCode = GetLastError();
                    Logger.LogWarning($"Hotkey registration attempt {i + 1} failed with error code: {errorCode} (0x{errorCode:X})");
                    
                    // Error codes reference:
                    // 1409 (0x581) = ERROR_HOTKEY_ALREADY_REGISTERED
                    // 87 (0x57) = ERROR_INVALID_PARAMETER
                    if (errorCode == 1409)
                    {
                        Logger.LogError("Hotkey is already registered by another application. Cannot proceed.");
                        break; // No point in retrying
                    }
                    
                    // Wait before retry
                    if (i < maxRetries - 1)
                    {
                        Thread.Sleep(200);
                    }
                }

                Logger.LogError("Failed to register hotkey after all retry attempts");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception during hotkey registration", ex);
                return false;
            }
        }
        
        public static bool TryRegisterAlternativeHotkey(System.Windows.Window window, out GlobalHotkey? hotkey, out string? registeredHotkeyName)
        {
            hotkey = null;
            registeredHotkeyName = null;
            
            // Try alternative hotkey combinations if Ctrl+Alt+R fails
            var alternatives = new[]
            {
                new { Modifiers = MOD_CONTROL | MOD_ALT, Key = VK_R, Name = "Ctrl+Alt+R" },
                new { Modifiers = MOD_CONTROL | MOD_SHIFT, Key = VK_R, Name = "Ctrl+Shift+R" },
                new { Modifiers = MOD_ALT | MOD_SHIFT, Key = VK_R, Name = "Alt+Shift+R" },
                new { Modifiers = MOD_CONTROL | MOD_ALT, Key = (uint)0x54, Name = "Ctrl+Alt+T" }, // VK_T
            };
            
            foreach (var alt in alternatives)
            {
                Logger.LogInfo($"Trying alternative hotkey: {alt.Name}");
                var tempHotkey = new GlobalHotkey();
                if (tempHotkey.RegisterHotkey(window, alt.Modifiers, alt.Key))
                {
                    Logger.LogInfo($"Successfully registered alternative hotkey: {alt.Name}");
                    hotkey = tempHotkey;
                    registeredHotkeyName = alt.Name;
                    return true;
                }
                tempHotkey.Dispose();
            }
            
            Logger.LogError("All alternative hotkey combinations failed");
            return false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
                {
                    Logger.LogInfo("Hotkey message received");
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in WndProc hotkey handler", ex);
                handled = true; // Still mark as handled to prevent further issues
            }
            return IntPtr.Zero;
        }

        public bool TryReregister()
        {
            try
            {
                if (_disposed || _hwnd == IntPtr.Zero)
                {
                    Logger.LogWarning("Cannot reregister hotkey - instance disposed or no window handle");
                    return false;
                }

                // Unregister first
                if (_isRegistered)
                {
                    UnregisterHotKey(_hwnd, HOTKEY_ID);
                    _isRegistered = false;
                }

                // Wait a bit
                Thread.Sleep(100);

                // Try to register again
                bool success = RegisterHotKey(_hwnd, HOTKEY_ID, _modifiers, _key);
                if (success)
                {
                    _isRegistered = true;
                    Logger.LogInfo("Hotkey reregistered successfully");
                }
                else
                {
                    Logger.LogWarning("Failed to reregister hotkey");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during hotkey reregistration", ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    Logger.LogInfo("Disposing GlobalHotkey");

                    if (_isRegistered && _hwnd != IntPtr.Zero)
                    {
                        bool unregistered = UnregisterHotKey(_hwnd, HOTKEY_ID);
                        Logger.LogInfo($"Hotkey unregistration: {(unregistered ? "successful" : "failed")}");
                        _isRegistered = false;
                    }

                    if (_hwndSource != null)
                    {
                        _hwndSource.RemoveHook(WndProc);
                        _hwndSource = null;
                    }

                    Logger.LogInfo("GlobalHotkey disposed successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error disposing GlobalHotkey", ex);
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
