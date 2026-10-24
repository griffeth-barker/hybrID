using System;
using System.IO;
using System.Text.Json;
using HybridID.Models;

namespace HybridID.Services;

/// <summary>
/// Loads/saves the user's config from a per-user profile directory (cross-platform via
/// Environment.SpecialFolder.ApplicationData), seeded on first run from the app's bundled
/// config.default.json. This replaces the original app's model of editing config.json in
/// place inside the install directory, which assumed a writable, single-user install location.
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string ConfigPath { get; }

    public ConfigService()
    {
        var profileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "hybrID");
        Directory.CreateDirectory(profileDir);
        ConfigPath = Path.Combine(profileDir, "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            SeedFromDefault();
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch (JsonException)
        {
            // A truncated/corrupt config.json (e.g. the process was killed mid-write) would
            // otherwise crash the app on every future launch with no in-app way to recover.
            // Fall back to defaults and let the next Save() overwrite the bad file.
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);

        // Write-then-replace so a crash mid-write can't leave config.json truncated.
        var tempPath = ConfigPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ConfigPath, overwrite: true);
    }

    private void SeedFromDefault()
    {
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "config.default.json");
        if (File.Exists(defaultPath))
        {
            File.Copy(defaultPath, ConfigPath);
        }
        else
        {
            Save(new AppConfig());
        }
    }
}
