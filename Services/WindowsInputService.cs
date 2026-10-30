using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public class WindowsInputService : IInputSimulationService
    {
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MBUTTONDOWN = 0x0207;
        private const uint WM_MBUTTONUP = 0x0208;

        private const int MK_LBUTTON = 0x0001;
        private const int MK_RBUTTON = 0x0002;

        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const uint KEYEVENTF_KEYUP = 0x0002;

        public bool UseBackgroundMode { get; set; } = true;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private static IntPtr MakeLParam(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        /// <summary>
        /// Detects if the target window is an emulator (e.g. LDPlayer) and resolves its child RenderWindow handle.
        /// </summary>
        public IntPtr GetEffectiveInputHandle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr candidate = IntPtr.Zero;

            EnumChildWindows(hWnd, (child, lParam) =>
            {
                var sb = new StringBuilder(256);
                GetClassName(child, sb, sb.Capacity);
                var cls = sb.ToString();

                // LDPlayer render window classes
                if (cls.Equals("RenderWindow", StringComparison.OrdinalIgnoreCase) ||
                    cls.Equals("subWin", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = child;
                    return false; // Stop enumeration
                }

                return true;
            }, IntPtr.Zero);

            return candidate != IntPtr.Zero ? candidate : hWnd;
        }

        public async Task ClickAsync(IntPtr hWnd, int clientX, int clientY, MouseButtonType button = MouseButtonType.Left, int holdMs = 50)
        {
            var targetHwnd = GetEffectiveInputHandle(hWnd);

            if (UseBackgroundMode)
            {
                uint downMsg = button switch
                {
                    MouseButtonType.Right => WM_RBUTTONDOWN,
                    MouseButtonType.Middle => WM_MBUTTONDOWN,
                    _ => WM_LBUTTONDOWN
                };
                uint upMsg = button switch
                {
                    MouseButtonType.Right => WM_RBUTTONUP,
                    MouseButtonType.Middle => WM_MBUTTONUP,
                    _ => WM_LBUTTONUP
                };
                IntPtr wParamDown = button switch
                {
                    MouseButtonType.Right => (IntPtr)MK_RBUTTON,
                    _ => (IntPtr)MK_LBUTTON
                };

                IntPtr lParam = MakeLParam(clientX, clientY);

                PostMessage(targetHwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
                await Task.Delay(10);
                PostMessage(targetHwnd, downMsg, wParamDown, lParam);
                await Task.Delay(Math.Max(10, holdMs));
                PostMessage(targetHwnd, upMsg, IntPtr.Zero, lParam);

                if (button == MouseButtonType.DoubleLeft)
                {
                    await Task.Delay(60);
                    PostMessage(targetHwnd, downMsg, wParamDown, lParam);
                    await Task.Delay(Math.Max(10, holdMs));
                    PostMessage(targetHwnd, upMsg, IntPtr.Zero, lParam);
                }
            }
            else
            {
                // Foreground mode with SendInput
                SetForegroundWindow(hWnd);
                await Task.Delay(30);

                var pt = new POINT { X = clientX, Y = clientY };
                ClientToScreen(targetHwnd, ref pt);

                int screenW = GetSystemMetrics(SM_CXSCREEN);
                int screenH = GetSystemMetrics(SM_CYSCREEN);

                int absX = (int)((pt.X * 65535.0f) / screenW);
                int absY = (int)((pt.Y * 65535.0f) / screenH);

                uint downFlag = button == MouseButtonType.Right ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
                uint upFlag = button == MouseButtonType.Right ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

                var moveInput = new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new InputUnion
                    {
                        mi = new MOUSEINPUT
                        {
                            dx = absX,
                            dy = absY,
                            dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE
                        }
                    }
                };

                var downInput = new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new InputUnion
                    {
                        mi = new MOUSEINPUT { dwFlags = downFlag }
                    }
                };

                var upInput = new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new InputUnion
                    {
                        mi = new MOUSEINPUT { dwFlags = upFlag }
                    }
                };

                SendInput(1, new[] { moveInput }, Marshal.SizeOf<INPUT>());
                await Task.Delay(10);
                SendInput(1, new[] { downInput }, Marshal.SizeOf<INPUT>());
                await Task.Delay(Math.Max(10, holdMs));
                SendInput(1, new[] { upInput }, Marshal.SizeOf<INPUT>());

                if (button == MouseButtonType.DoubleLeft)
                {
                    await Task.Delay(60);
                    SendInput(1, new[] { downInput }, Marshal.SizeOf<INPUT>());
                    await Task.Delay(Math.Max(10, holdMs));
                    SendInput(1, new[] { upInput }, Marshal.SizeOf<INPUT>());
                }
            }
        }

        public async Task DragAsync(IntPtr hWnd, int startX, int startY, int endX, int endY, int durationMs = 300, MouseButtonType button = MouseButtonType.Right)
        {
            var targetHwnd = GetEffectiveInputHandle(hWnd);

            uint downMsg = button == MouseButtonType.Right ? WM_RBUTTONDOWN : WM_LBUTTONDOWN;
            uint upMsg = button == MouseButtonType.Right ? WM_RBUTTONUP : WM_LBUTTONUP;
            IntPtr wParam = button == MouseButtonType.Right ? (IntPtr)MK_RBUTTON : (IntPtr)MK_LBUTTON;

            int steps = Math.Max(5, durationMs / 20);
            int stepDelay = durationMs / steps;

            PostMessage(targetHwnd, WM_MOUSEMOVE, IntPtr.Zero, MakeLParam(startX, startY));
            await Task.Delay(15);
            PostMessage(targetHwnd, downMsg, wParam, MakeLParam(startX, startY));
            await Task.Delay(20);

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                int curX = (int)(startX + (endX - startX) * t);
                int curY = (int)(startY + (endY - startY) * t);

                PostMessage(targetHwnd, WM_MOUSEMOVE, wParam, MakeLParam(curX, curY));
                await Task.Delay(stepDelay);
            }

            PostMessage(targetHwnd, upMsg, IntPtr.Zero, MakeLParam(endX, endY));
            await Task.Delay(20);
        }

        public async Task KeyPressAsync(IntPtr hWnd, string keyName, int holdMs = 100)
        {
            var targetHwnd = GetEffectiveInputHandle(hWnd);
            var vk = ResolveVirtualKey(keyName);

            if (UseBackgroundMode)
            {
                PostMessage(targetHwnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
                await Task.Delay(Math.Max(20, holdMs));
                PostMessage(targetHwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
            }
            else
            {
                SetForegroundWindow(hWnd);
                await Task.Delay(20);

                var down = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion { ki = new KEYBDINPUT { wVk = vk } }
                };
                var up = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
                };

                SendInput(1, new[] { down }, Marshal.SizeOf<INPUT>());
                await Task.Delay(Math.Max(20, holdMs));
                SendInput(1, new[] { up }, Marshal.SizeOf<INPUT>());
            }
        }

        public Task KeyDownAsync(IntPtr hWnd, string keyName)
        {
            var targetHwnd = GetEffectiveInputHandle(hWnd);
            var vk = ResolveVirtualKey(keyName);

            if (UseBackgroundMode)
            {
                PostMessage(targetHwnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
            }
            else
            {
                SetForegroundWindow(hWnd);
                var down = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion { ki = new KEYBDINPUT { wVk = vk } }
                };
                SendInput(1, new[] { down }, Marshal.SizeOf<INPUT>());
            }

            return Task.CompletedTask;
        }

        public Task KeyUpAsync(IntPtr hWnd, string keyName)
        {
            var targetHwnd = GetEffectiveInputHandle(hWnd);
            var vk = ResolveVirtualKey(keyName);

            if (UseBackgroundMode)
            {
                PostMessage(targetHwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
            }
            else
            {
                var up = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
                };
                SendInput(1, new[] { up }, Marshal.SizeOf<INPUT>());
            }

            return Task.CompletedTask;
        }

        private static ushort ResolveVirtualKey(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName))
                return 0x20; // Space

            var clean = keyName.Trim().ToUpperInvariant();

            return clean switch
            {
                "W" => 0x57,
                "A" => 0x41,
                "S" => 0x53,
                "D" => 0x44,
                "E" => 0x45,
                "Q" => 0x51,
                "R" => 0x52,
                "F" => 0x46,
                "I" => 0x49, // Roblox zoom in / inventory
                "O" => 0x4F, // Roblox zoom out
                "SPACE" => 0x20,
                "SHIFT" => 0x10,
                "LSHIFT" => 0xA0,
                "CTRL" => 0x11,
                "ALT" => 0x12,
                "ENTER" => 0x0D,
                "ESC" => 0x1B,
                "TAB" => 0x09,
                "1" => 0x31,
                "2" => 0x32,
                "3" => 0x33,
                "4" => 0x34,
                "5" => 0x35,
                "COMMA" or "<" => 0xBC,  // Roblox camera rotate left
                "PERIOD" or ">" => 0xBE, // Roblox camera rotate right
                _ => (ushort)(clean.Length == 1 ? clean[0] : 0x20)
            };
        }
    }
}
