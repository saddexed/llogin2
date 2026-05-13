using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace llogin
{
    public class Credentials
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class AppConfig
    {
        public string? DefaultUsername { get; set; }
        public List<Credentials> Users { get; set; } = new List<Credentials>();
    }

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(AppConfig))]
    internal partial class AppConfigJsonContext : JsonSerializerContext
    {
    }

    public static class StorageService
    {
        public static string GetInstallDir()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "llogin");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        }

        public static string GetConfigDir()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetInstallDir();
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "llogin");
        }

        public static string GetCredentialsPath() => Path.Combine(GetConfigDir(), "credentials.json");
        public static string GetLogPath() => Path.Combine(GetConfigDir(), "log.txt");
        public static string GetDebugHtmlPath() => Path.Combine(GetConfigDir(), "debug_response.html");

        private static AppConfig LoadConfig()
        {
            string path = GetCredentialsPath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig) ?? new AppConfig();
                }
                catch { }
            }
            return new AppConfig();
        }

        private static void SaveConfig(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(GetConfigDir())) Directory.CreateDirectory(GetConfigDir());
                string json = JsonSerializer.Serialize(config, AppConfigJsonContext.Default.AppConfig);
                File.WriteAllText(GetCredentialsPath(), json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        public static void WriteLogEntry(string username, string action, string status, string url = "")
        {
            try
            {
                if (!Directory.Exists(GetConfigDir())) Directory.CreateDirectory(GetConfigDir());
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logMessage = $"{timestamp} - {action} {status} for user: {username}";
                if (!string.IsNullOrEmpty(url)) logMessage += $" via {url}";
                File.AppendAllText(GetLogPath(), logMessage + Environment.NewLine);
            }
            catch { }
        }

        public static Credentials? GetDefaultCredentials()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config.DefaultUsername)) return null;
            return config.Users.FirstOrDefault(u => string.Equals(u.Username, config.DefaultUsername, StringComparison.OrdinalIgnoreCase));
        }

        public static Credentials? GetCredentials(string username)
        {
            var config = LoadConfig();
            return config.Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        }

        public static void AddUser(string username, string password)
        {
            var config = LoadConfig();
            var existing = config.Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
            {
                existing.Password = password;
                Console.WriteLine($"Updated password for {username}");
            }
            else
            {
                config.Users.Add(new Credentials { Username = username, Password = password });
                Console.WriteLine($"Added user: {username}");
            }

            if (string.IsNullOrEmpty(config.DefaultUsername))
            {
                config.DefaultUsername = username;
                Console.WriteLine($"Set {username} as default user.");
            }

            SaveConfig(config);
        }

        public static void RemoveUser(string username)
        {
            var config = LoadConfig();
            int removedCount = config.Users.RemoveAll(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            
            if (removedCount > 0)
            {
                if (string.Equals(config.DefaultUsername, username, StringComparison.OrdinalIgnoreCase))
                {
                    config.DefaultUsername = config.Users.FirstOrDefault()?.Username;
                }
                SaveConfig(config);
                Console.WriteLine($"Removed user: {username}");
            }
            else
            {
                Console.WriteLine($"User not found: {username}");
            }
        }

        public static void ListUsers()
        {
            var config = LoadConfig();
            if (config.Users.Count == 0)
            {
                Console.WriteLine("No users stored.");
                return;
            }
            for (int i = 0; i < config.Users.Count; i++)
            {
                var user = config.Users[i];
                string marker = string.Equals(user.Username, config.DefaultUsername, StringComparison.OrdinalIgnoreCase) ? " [Default]" : "";
                Console.WriteLine($"{i + 1}. {user.Username}{marker}");
            }
        }

        public static void SetDefaultUser(string username)
        {
            var config = LoadConfig();
            var user = config.Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                Console.WriteLine($"Error: User {username} not found. Please use -a to add them first.");
                return;
            }

            config.DefaultUsername = user.Username;
            SaveConfig(config);
            Console.WriteLine($"Set {user.Username} as default user.");
        }

        public static bool RemoveAllCredentials()
        {
            string path = GetCredentialsPath();
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Console.WriteLine("All stored credentials removed.");
                    WriteLogEntry("system", "Credentials", "cleared all");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing credentials: {ex.Message}");
                return false;
            }
        }
    }
}