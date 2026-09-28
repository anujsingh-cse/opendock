using System.Text.Json;
using System.Text.Json.Serialization;
using OpenDock.Core.Models;

namespace OpenDock.Core.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON.
/// Corrupt or missing files fall back to defaults instead of crashing startup.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenDock");

    public static string SettingsPath { get; } = Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                if (JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) is { } loaded)
                    Current = loaded;
            }
        }
        catch
        {
            Current = new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, JsonOptions));
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Mutates the settings and persists them in one step.</summary>
    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        Save();
    }
}
