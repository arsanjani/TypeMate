using System;
using TypeMate;
using HwndSrc = System.Windows.Interop.HwndSource;
using WpfHelper = System.Windows.Interop.WindowInteropHelper;
using WpfWindow = System.Windows.Window;

namespace TypeMate.Core.Platform
{
    public class HotkeyManager : IHotkeyManager
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        private HwndSrc? _hwndSource;
        private IntPtr _registeredHwnd;
        private readonly Hotcode[] _codes;
        private bool _disposed;

        public event EventHandler? HotkeyPressed;
        public string? RegisteredShortcut { get; private set; }

        public HotkeyManager(params Hotcode[] codes)
        {
            _codes = codes ?? Array.Empty<Hotcode>();
        }

        public void SetRegisteredShortcut(string shortcutName)
        {
            RegisteredShortcut = shortcutName;
        }

        public bool Register(WpfWindow window, params Hotcode[] codes)
        {
            if (_disposed)
            {
                Logger.LogWarning("Attempted to register hotkey on disposed HotkeyManager instance");
                return false;
            }

            // Use passed codes for this attempt, otherwise fall back to constructor-injected defaults
            var order = (codes != null && codes.Length > 0) ? codes : _codes;

            foreach (Hotcode code in order)
            {
                Logger.LogInfo("Trying hotkey: " + code.Name);
                bool ok = TryRegister(window, code);
                if (ok)
                    return true;
            }

            Logger.LogError("All hot keys conflicted - TypeMate will not respond to shortcuts until restarted");
            return false;
        }

        private bool TryRegister(WpfWindow window, Hotcode code)
        {
            var helper = new WpfHelper(window);
            IntPtr hwnd = helper.EnsureHandle();

            if (hwnd == IntPtr.Zero)
            {
                Logger.LogError("Hot key registration failed: Window handle is zero");
                return false;
            }

            _hwndSource = HwndSrc.FromHwnd(hwnd);
            if (_hwndSource == null)
            {
                Logger.LogError("Hot key registration failed: Could not get HwndSource");
                return false;
            }

            _hwndSource.AddHook(WndProc);

            bool success = NativeMethods.RegisterHotKey(hwnd, HOTKEY_ID, code.Modifiers, code.Key);

            if (!success)
            {
                uint errorCode = NativeMethods.GetLastError();
                string msg2 = "Hotkey registration failed for '" + code.Name;
                Logger.LogWarning(msg2);
                Logger.LogWarning(errorCode.ToString());

                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
                return false;
            }

            _registeredHwnd = hwnd;
            RegisteredShortcut = code.Name;
            return true;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.LogError("Hot key handler error", ex);
            }
            handled = true;
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            try
            {
                NativeMethods.UnregisterHotKey(_registeredHwnd, HOTKEY_ID);
                _hwndSource?.RemoveHook(WndProc);
                _hwndSource = null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Dispose error", ex);
            }
        }
    }
}
