using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace StealthSnip;

public class HotkeyManager : IDisposable
{
    public const int HOTKEY_SNIP = 1;
    public const int HOTKEY_FULLSCREEN = 2;
    public const int HOTKEY_ACTIVE_WINDOW = 3;

    private readonly HotkeyWindow _window;
    private readonly Dictionary<int, Action> _actions = new();
    private bool _disposed = false;

    public HotkeyManager()
    {
        _window = new HotkeyWindow(OnHotkeyPressed);
    }

    public bool Register(int id, uint modifiers, Keys key, Action action)
    {
        if (_disposed) return false;

        // Add MOD_NOREPEAT to prevent duplicate rapid triggers if held down
        uint flags = modifiers | NativeMethods.MOD_NOREPEAT;

        bool success = NativeMethods.RegisterHotKey(_window.Handle, id, flags, (uint)key);
        if (success)
        {
            _actions[id] = action;
        }
        return success;
    }

    private void OnHotkeyPressed(int id)
    {
        if (_actions.TryGetValue(id, out var action))
        {
            action.Invoke();
        }
    }

    public void UnregisterAll()
    {
        foreach (int id in _actions.Keys)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, id);
        }
        _actions.Clear();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            UnregisterAll();
            _window.Dispose();
            _disposed = true;
        }
    }

    private class HotkeyWindow : NativeWindow, IDisposable
    {
        private readonly Action<int> _callback;
        private readonly System.Threading.SynchronizationContext? _syncContext;

        public HotkeyWindow(Action<int> callback)
        {
            _callback = callback;
            _syncContext = System.Threading.SynchronizationContext.Current;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (_syncContext != null)
                {
                    _syncContext.Post(_ => _callback(id), null);
                }
                else
                {
                    _callback(id);
                }
            }
            else if (m.Msg == Program.WM_TRIGGER_SNIP)
            {
                if (_syncContext != null)
                {
                    _syncContext.Post(_ => _callback(HOTKEY_SNIP), null);
                }
                else
                {
                    _callback(HOTKEY_SNIP);
                }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }
}
