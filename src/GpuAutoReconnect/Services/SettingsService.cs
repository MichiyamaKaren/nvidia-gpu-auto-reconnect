using System.Text.Json;
using System.Text.Json.Serialization;
using GpuAutoReconnect.Models;

namespace GpuAutoReconnect.Services;

public class SettingsService
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GpuAutoReconnect");

    private static readonly string SettingsFilePath = Path.Combine(AppDataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                Current = new AppSettings();
                Save();
                return;
            }

            var json = File.ReadAllText(SettingsFilePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataDir);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }
}
