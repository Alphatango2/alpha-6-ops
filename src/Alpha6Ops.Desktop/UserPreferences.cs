using System;
using System.IO;
using System.Text.Json;

namespace Alpha6Ops.Desktop;

internal sealed record UserPreferences(double Width, double Height, bool Maximized, bool Advanced, string Fixture, string PilotName = "");

internal static class UserPreferencesStore
{
    private static string PathName(string? directory) => Path.Combine(directory ?? CrashReporter.RootDirectory, "preferences.json");

    internal static UserPreferences? Load()
    {
        var path = PathName(null);
        try { return File.Exists(path) ? JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(path)) : null; }
        catch (Exception error) when (error is IOException or JsonException) { CrashReporter.Write("preferences_load", error); return null; }
    }

    internal static void Save(UserPreferences preferences, string? directory = null)
    {
        var path = PathName(directory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
