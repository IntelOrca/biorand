using System;
using System.IO;
using System.Text.Json;

namespace rerandoui
{
    public class RandoAppSettings
    {
        public string Seed { get; set; }
        public string GamePath { get; set; }

        public static string GetSettingsPath()
        {
            return Path.Combine(GetSettingsDirectory(), "settings.json");
        }

        public static string GetGenerationLogPath()
        {
            return Path.Combine(GetSettingsDirectory(), "generation.log");
        }

        public static string GetSettingsDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "RERando");
        }

        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            return new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                WriteIndented = true
            };
        }

        public static RandoAppSettings Load()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var instance = JsonSerializer.Deserialize<RandoAppSettings>(json, GetJsonSerializerOptions());
                    return instance;
                }
            }
            catch
            {
            }
            return new RandoAppSettings();
        }

        public void Save()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                var json = JsonSerializer.Serialize(this, GetJsonSerializerOptions());
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, json);
            }
            catch
            {
            }
        }

        public static void LogGeneration(string seed, string gamePath)
        {
            var dt = DateTime.Now;

            var path = GetGenerationLogPath();
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write))
            {
                var sw = new StreamWriter(fs);
                sw.WriteLine($"Generated randomizer [{seed}] @ {dt} in \"{gamePath}\"");
                sw.Flush();
            }
        }
    }
}
