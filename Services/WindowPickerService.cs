using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public class WindowPickerService
    {
        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr GetStockObject(int fnObject);

        [DllImport("gdi32.dll")]
        private static extern bool Rectangle(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        private const int NULL_BRUSH = 5;
        private const int PS_SOLID = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        public WindowInfo? GetWindowUnderCursor()
        {
            if (!GetCursorPos(out var pt))
                return null;

            var rawHwnd = WindowFromPoint(pt);
            if (rawHwnd == IntPtr.Zero)
                return null;

            // Resolve top-level window
            var rootHwnd = GetAncestor(rawHwnd, GA_ROOT);
            var targetHwnd = rootHwnd != IntPtr.Zero ? rootHwnd : rawHwnd;

            return ExtractWindowInfo(targetHwnd);
        }

        public void HighlightWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            GetWindowRect(hWnd, out var r);
            IntPtr hdc = GetWindowDC(hWnd);
            if (hdc == IntPtr.Zero) return;

            try
            {
                IntPtr hPen = CreatePen(PS_SOLID, 4, 0x0000FF); // Red border (BGR: 0x0000FF)
                IntPtr hOldPen = SelectObject(hdc, hPen);
                IntPtr hOldBrush = SelectObject(hdc, GetStockObject(NULL_BRUSH));

                Rectangle(hdc, 0, 0, r.Width, r.Height);

                SelectObject(hdc, hOldBrush);
                SelectObject(hdc, hOldPen);
                DeleteObject(hPen);
            }
            finally
            {
                ReleaseDC(hWnd, hdc);
            }
        }

        private static WindowInfo ExtractWindowInfo(IntPtr hWnd)
        {
            GetWindowRect(hWnd, out var rect);
            GetClientRect(hWnd, out var clientRect);

            var sbTitle = new StringBuilder(256);
            GetWindowText(hWnd, sbTitle, sbTitle.Capacity);

            var sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, sbClass.Capacity);

            GetWindowThreadProcessId(hWnd, out var pid);
            string procName = string.Empty;
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                procName = proc.ProcessName;
            }
            catch { }

            return new WindowInfo
            {
                Handle = hWnd,
                Title = sbTitle.ToString(),
                ClassName = sbClass.ToString(),
                ProcessName = procName,
                ProcessId = (int)pid,
                X = rect.Left,
                Y = rect.Top,
                Width = rect.Width,
                Height = rect.Height,
                ClientWidth = clientRect.Width,
                ClientHeight = clientRect.Height
            };
        }
    }
}
