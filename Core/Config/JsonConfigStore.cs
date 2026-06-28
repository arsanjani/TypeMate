using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TypeMate;

namespace TypeMate.Core.Config
{
    public class JsonConfigStore : IConfigStore
    {
        private const string AppFolderName = "TypeMate";
        private const string ConfigFileName = "config.json";

        public Task<AppConfig?> GetAsync()
        {
            return GetConfigAsync();
        }

        public async Task<bool> SaveAsync(AppConfig config)
        {
            try
            {
                string directory = GetAppDirectory();
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string configPath = GetConfigPath();
                await using FileStream stream = File.Create(configPath);
                await JsonSerializer.SerializeAsync(stream, config, options);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save configuration", ex);
                return false;
            }
        }

        private Task<AppConfig?> GetConfigAsync()
        {
            try
            {
                string configPath = GetConfigPath();
                if (!File.Exists(configPath))
                {
                    return Task.FromResult<AppConfig?>(null);
                }

                return DeserializeConfigAsync(configPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read configuration", ex);
                return Task.FromResult<AppConfig?>(null);
            }
        }

        private static async Task<AppConfig?> DeserializeConfigAsync(string configPath)
        {
            await using FileStream stream = File.OpenRead(configPath);
            AppConfig? config = await JsonSerializer.DeserializeAsync<AppConfig>(stream);
            return config;
        }

        private static string GetAppDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppFolderName);
        }

        private static string GetConfigPath()
        {
            return Path.Combine(GetAppDirectory(), ConfigFileName);
        }
    }
}
