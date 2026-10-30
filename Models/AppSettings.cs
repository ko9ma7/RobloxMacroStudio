using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RobloxMacroStudio.Models
{
    public class AppSettings
    {
        public bool AutoLoadLastScript { get; set; } = true;
        public string LastScriptPath { get; set; } = string.Empty;
        public string PreferredProcessName { get; set; } = "dnplayer";
        public bool UseBackgroundMode { get; set; } = true;
        public int DefaultLoopIntervalMs { get; set; } = 500;
        public List<string> RecentScripts { get; set; } = new();

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RobloxMacroStudio");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null) return settings;
                }
            }
            catch { }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }

        public void AddRecentScript(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            RecentScripts.Remove(path);
            RecentScripts.Insert(0, path);

            if (RecentScripts.Count > 10)
            {
                RecentScripts.RemoveAt(RecentScripts.Count - 1);
            }

            LastScriptPath = path;
            Save();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };
    }
}
