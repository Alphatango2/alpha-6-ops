using System;
using System.IO;
using System.Text.Json;

namespace Alpha6Ops.Desktop;

internal sealed record GeneralSettings(
    bool MinimizeToTray = true,
    bool FlashOnNotification = true,
    bool NotificationSound = true,
    string WeightUnit = "LBS",
    string AltitudeUnit = "FT",
    string LandingDistanceUnit = "FT",
    bool ShowAdvancedControls = true)
{
    internal static GeneralSettings Defaults { get; } = new();
}

internal static class GeneralSettingsStore
{
    private static string PathName(string? directory) => Path.Combine(directory ?? CrashReporter.RootDirectory, "general-settings.json");

    internal static GeneralSettings Load(string? directory = null)
    {
        try
        {
            var path = PathName(directory);
            return File.Exists(path) ? JsonSerializer.Deserialize<GeneralSettings>(File.ReadAllText(path)) ?? GeneralSettings.Defaults : GeneralSettings.Defaults;
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            CrashReporter.Write("general_settings_load", error, directory: directory);
            return GeneralSettings.Defaults;
        }
    }

    internal static void Save(GeneralSettings settings, string? directory = null)
    {
        var path = PathName(directory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
