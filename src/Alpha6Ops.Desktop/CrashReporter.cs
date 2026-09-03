using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Alpha6Ops.Desktop;

internal static class CrashReporter
{
    private static int writing;
    internal static string RootDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alpha6Designs", "Alpha6OPS");
    internal static string CrashDirectory => Path.Combine(RootDirectory, "CrashReports");
    internal static string? LastReportPath { get; private set; }

    internal static void Install(Application app)
    {
        Forms.Application.SetUnhandledExceptionMode(Forms.UnhandledExceptionMode.CatchException);
        Forms.Application.ThreadException += (_, e) => Write("windows_forms_thread", e.Exception);
        app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("app_domain", e.ExceptionObject as Exception,
            new { terminating = e.IsTerminating, value = e.ExceptionObject?.ToString() });
        TaskScheduler.UnobservedTaskException += (_, e) => Write("unobserved_task", e.Exception);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Write("dispatcher", e.Exception);
        // Preserve the normal fail-fast behavior after the report has been flushed.
    }

    internal static string? Write(string source, Exception? exception, object? extra = null, string? directory = null)
    {
        if (System.Threading.Interlocked.Exchange(ref writing, 1) != 0) return LastReportPath;
        try
        {
            directory ??= CrashDirectory;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"Alpha6OPS-crash-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            var error = exception?.GetBaseException();
            var report = new
            {
                schemaVersion = 1,
                occurredAtUtc = DateTimeOffset.UtcNow,
                appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
                source,
                process = new { id = Environment.ProcessId, executable = Environment.ProcessPath, workingDirectory = Environment.CurrentDirectory,
                    operatingSystem = Environment.OSVersion.VersionString, framework = Environment.Version.ToString(), is64BitProcess = Environment.Is64BitProcess },
                exception = error is null ? null : new { type = error.GetType().FullName, error.Message, stackTrace = exception?.ToString() },
                extra
            };
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path);
            LastReportPath = path;
            return path;
        }
        catch { return null; }
        finally { System.Threading.Interlocked.Exchange(ref writing, 0); }
    }
}
