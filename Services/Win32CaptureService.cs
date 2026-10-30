using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public class Win32CaptureService : IWindowCaptureService
    {
        private const uint PW_CLIENTONLY = 0x00000001;
        private const uint PW_RENDERFULLCONTENT = 0x00000002;
        private const int SRCCOPY = 0x00CC0020;

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public IReadOnlyList<WindowInfo> GetOpenWindows()
        {
            var list = new List<WindowInfo>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                GetWindowRect(hWnd, out var rect);
                GetClientRect(hWnd, out var clientRect);

                // Exclude tiny or offscreen tooltips/hidden windows
                if (rect.Width <= 100 || rect.Height <= 100 || clientRect.Width <= 0 || clientRect.Height <= 0)
                    return true;

                var sbTitle = new StringBuilder(256);
                GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                var title = sbTitle.ToString();

                var sbClass = new StringBuilder(256);
                GetClassName(hWnd, sbClass, sbClass.Capacity);
                var className = sbClass.ToString();

                // Skip known desktop/taskbar system shells
                if (className == "Progman" || className == "Shell_TrayWnd")
                    return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                string processName = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    processName = proc.ProcessName;
                }
                catch
                {
                    // Process may have exited or insufficient permissions
                }

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(processName))
                    return true;

                list.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ClassName = className,
                    ProcessName = processName,
                    ProcessId = (int)pid,
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height,
                    ClientWidth = clientRect.Width,
                    ClientHeight = clientRect.Height
                });

                return true;
            }, IntPtr.Zero);

            return list;
        }

        public WindowInfo? GetWindowInfo(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd))
                return null;

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

        public Mat? CaptureWindow(IntPtr hWnd, bool clientOnly = true)
        {
            if (hWnd == IntPtr.Zero)
                return null;

            int width, height;
            if (clientOnly)
            {
                GetClientRect(hWnd, out var clientRect);
                width = clientRect.Width;
                height = clientRect.Height;
            }
            else
            {
                GetWindowRect(hWnd, out var winRect);
                width = winRect.Width;
                height = winRect.Height;
            }

            if (width <= 0 || height <= 0)
                return null;

            IntPtr hdcWindow = clientOnly ? GetDC(hWnd) : GetWindowDC(hWnd);
            if (hdcWindow == IntPtr.Zero)
                return null;

            IntPtr hdcMem = CreateCompatibleDC(hdcWindow);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
            IntPtr hOld = SelectObject(hdcMem, hBitmap);

            bool success = false;

            try
            {
                // Attempt PrintWindow first (works even if window is partially covered)
                uint flags = PW_RENDERFULLCONTENT | (clientOnly ? PW_CLIENTONLY : 0);
                success = PrintWindow(hWnd, hdcMem, flags);

                // Fallback to BitBlt if PrintWindow returns false or empty
                if (!success)
                {
                    success = BitBlt(hdcMem, 0, 0, width, height, hdcWindow, 0, 0, SRCCOPY);
                }

                if (!success)
                    return null;

                using var bmp = Image.FromHbitmap(hBitmap);
                // Convert System.Drawing.Bitmap to OpenCvSharp.Mat
                return BitmapConverter.ToMat(bmp);
            }
            catch
            {
                return null;
            }
            finally
            {
                SelectObject(hdcMem, hOld);
                DeleteObject(hBitmap);
                DeleteDC(hdcMem);
                ReleaseDC(hWnd, hdcWindow);
            }
        }

        public System.Drawing.Rectangle GetClientRect(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return System.Drawing.Rectangle.Empty;

            GetClientRect(hWnd, out var r);
            return new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height);
        }

        public System.Drawing.Point ClientToScreen(IntPtr hWnd, int clientX, int clientY)
        {
            var pt = new POINT { X = clientX, Y = clientY };
            ClientToScreen(hWnd, ref pt);
            return new System.Drawing.Point(pt.X, pt.Y);
        }

        public System.Drawing.Point ScreenToClient(IntPtr hWnd, int screenX, int screenY)
        {
            var pt = new POINT { X = screenX, Y = screenY };
            ScreenToClient(hWnd, ref pt);
            return new System.Drawing.Point(pt.X, pt.Y);
        }
    }
}
