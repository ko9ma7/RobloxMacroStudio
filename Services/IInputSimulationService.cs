using System;
using System.Threading.Tasks;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public interface IInputSimulationService
    {
        bool UseBackgroundMode { get; set; }

        Task ClickAsync(IntPtr hWnd, int clientX, int clientY, MouseButtonType button = MouseButtonType.Left, int holdMs = 50);
        Task DragAsync(IntPtr hWnd, int startX, int startY, int endX, int endY, int durationMs = 300, MouseButtonType button = MouseButtonType.Right);
        Task KeyPressAsync(IntPtr hWnd, string keyName, int holdMs = 100);
        Task KeyDownAsync(IntPtr hWnd, string keyName);
        Task KeyUpAsync(IntPtr hWnd, string keyName);

        IntPtr GetEffectiveInputHandle(IntPtr hWnd);
    }
}
