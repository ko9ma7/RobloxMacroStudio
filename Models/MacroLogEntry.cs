using System;

namespace RobloxMacroStudio.Models
{
    public enum MacroLogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Debug
    }

    public class MacroLogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public MacroLogLevel Level { get; set; } = MacroLogLevel.Info;
        public string Message { get; set; } = string.Empty;

        public string TimeString => Timestamp.ToString("HH:mm:ss.fff");

        public string BadgeColor => Level switch
        {
            MacroLogLevel.Success => "#10B981", // Emerald green
            MacroLogLevel.Warning => "#F59E0B", // Amber
            MacroLogLevel.Error => "#EF4444",   // Red
            MacroLogLevel.Debug => "#8B5CF6",   // Purple
            _ => "#3B82F6"                      // Blue info
        };
    }
}
