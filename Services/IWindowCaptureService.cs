using System;
using System.Collections.Generic;
using OpenCvSharp;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public interface IWindowCaptureService
    {
        IReadOnlyList<WindowInfo> GetOpenWindows();
        WindowInfo? GetWindowInfo(IntPtr hWnd);
        Mat? CaptureWindow(IntPtr hWnd, bool clientOnly = true);
        System.Drawing.Rectangle GetClientRect(IntPtr hWnd);
        System.Drawing.Point ClientToScreen(IntPtr hWnd, int clientX, int clientY);
        System.Drawing.Point ScreenToClient(IntPtr hWnd, int screenX, int screenY);
    }
}
