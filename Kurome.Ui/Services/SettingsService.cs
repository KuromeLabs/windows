using System.IO;
using System.Text.Json;
using Serilog;

namespace Kurome.Ui.Services;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public class UiSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
}

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger _logger = Log.ForContext<SettingsService>();
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kurome", "ui.json");

    private UiSettings? _cached;

    public UiSettings Current => _cached ??= Load();

    private UiSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new UiSettings();
            return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(_path)) ?? new UiSettings();
        }
        catch (Exception e)
        {
            _logger.Warning(e, "Could not read UI settings from {Path}, falling back to defaults", _path);
            return new UiSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (Exception e)
        {
            _logger.Error(e, "Could not save UI settings to {Path}", _path);
        }
    }
}
