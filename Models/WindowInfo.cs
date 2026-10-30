using System;

namespace RobloxMacroStudio.Models
{
    /// <summary>
    /// Represents detailed information about a detectable OS window.
    /// </summary>
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public int ClientWidth { get; set; }
        public int ClientHeight { get; set; }

        public bool IsEmulator =>
            Title.Contains("LDPlayer", StringComparison.OrdinalIgnoreCase) ||
            Title.Contains("BlueStacks", StringComparison.OrdinalIgnoreCase) ||
            Title.Contains("Nox", StringComparison.OrdinalIgnoreCase) ||
            Title.Contains("MuMu", StringComparison.OrdinalIgnoreCase) ||
            ClassName.Contains("LDPlayer", StringComparison.OrdinalIgnoreCase) ||
            ProcessName.Contains("dnplayer", StringComparison.OrdinalIgnoreCase) ||
            ProcessName.Contains("HD-Player", StringComparison.OrdinalIgnoreCase);

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Title)
                ? $"[{ProcessName}] (HWND: 0x{Handle.ToInt64():X8})"
                : $"{Title} - [{ProcessName}] ({ClientWidth}x{ClientHeight})";

        public override string ToString() => DisplayName;
    }
}
