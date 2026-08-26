using System.Text.Json;
using Mosaic.Contracts;

namespace Mosaic.Desktop.Settings;

// what gets written to settings.json, don't rename
public static class Appearances
{
    public const string Dark = "dark";
    public const string Light = "light";

    // Anything other than "light" counts as dark, including a missing value.
    public static bool IsDark(string? stored) => stored != Light;
}

public sealed record DesktopSettings
{
    public string? LanguageCode { get; init; }

    public string? Appearance { get; init; }
}

public sealed class SettingsStore(string folder)
{
    // indented so the file is hand-editable
    private static readonly JsonSerializerOptions FileFormat =
        new(MosaicJson.Options) { WriteIndented = true };

    private readonly string _filePath = Path.Combine(folder, "settings.json");

    public SettingsStore() : this(DefaultFolder())
    {
    }

    // unreadable or corrupt file -> defaults, not worth failing startup over
    public DesktopSettings Read()
    {
        try
        {
            return File.Exists(_filePath)
                ? JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(_filePath), FileFormat)
                  ?? new DesktopSettings()
                : new DesktopSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new DesktopSettings();
        }
    }

    // same here, an exception would propagate into the UI
    public void Write(DesktopSettings settings)
    {
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, FileFormat));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string DefaultFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mosaic");
}
