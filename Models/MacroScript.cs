using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace RobloxMacroStudio.Models
{
    public class MacroScript
    {
        public string Title { get; set; } = "New Macro Script";
        public string Description { get; set; } = "Roblox 3D automation workflow";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public int LoopCount { get; set; } = 0; // 0 = Infinite loop
        public int LoopIntervalMs { get; set; } = 1000;
        public bool StopOnError { get; set; } = false;

        public ObservableCollection<MacroAction> Actions { get; set; } = new();

        public static MacroScript LoadFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Script file not found", path);

            var json = File.ReadAllText(path);
            var script = JsonSerializer.Deserialize<MacroScript>(json, JsonOptions);
            return script ?? new MacroScript();
        }

        public void SaveToFile(string path)
        {
            UpdatedAt = DateTime.Now;
            var json = JsonSerializer.Serialize(this, JsonOptions);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, json);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
