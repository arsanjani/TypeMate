using System;
using System.Windows;

namespace TypeMate.Core.Platform
{
    public interface IHotkeyManager : IDisposable
    {
        event EventHandler HotkeyPressed;
        string? RegisteredShortcut { get; }
        bool Register(Window window, params Hotcode[] codes);
    }
}
