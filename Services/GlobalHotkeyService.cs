using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace RobloxMacroStudio.Services
{
    public class GlobalHotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        public const int HOTKEY_ID_START = 9001; // F5
        public const int HOTKEY_ID_STOP = 9002;  // F6
        public const int HOTKEY_ID_PAUSE = 9003; // F7

        private const uint VK_F5 = 0x74;
        private const uint VK_F6 = 0x75;
        private const uint VK_F7 = 0x76;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _hWnd = IntPtr.Zero;
        private HwndSource? _source;

        public event Action? OnStartHotkeyPressed;
        public event Action? OnStopHotkeyPressed;
        public event Action? OnPauseHotkeyPressed;

        public void Register(IntPtr hWnd)
        {
            _hWnd = hWnd;
            _source = HwndSource.FromHwnd(hWnd);
            _source?.AddHook(HwndHook);

            RegisterHotKey(hWnd, HOTKEY_ID_START, 0, VK_F5);
            RegisterHotKey(hWnd, HOTKEY_ID_STOP, 0, VK_F6);
            RegisterHotKey(hWnd, HOTKEY_ID_PAUSE, 0, VK_F7);
        }

        public void Unregister()
        {
            if (_hWnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hWnd, HOTKEY_ID_START);
                UnregisterHotKey(_hWnd, HOTKEY_ID_STOP);
                UnregisterHotKey(_hWnd, HOTKEY_ID_PAUSE);
            }

            _source?.RemoveHook(HwndHook);
            _source = null;
            _hWnd = IntPtr.Zero;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                switch (id)
                {
                    case HOTKEY_ID_START:
                        OnStartHotkeyPressed?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_ID_STOP:
                        OnStopHotkeyPressed?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_ID_PAUSE:
                        OnPauseHotkeyPressed?.Invoke();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
        }
    }
}
