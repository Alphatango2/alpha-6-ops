using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop;

internal sealed class ProgramMonitor : IDisposable
{
    private readonly DispatcherTimer timer;
    private readonly Action<string> statusChanged;
    private readonly string statusPath;
    private readonly string testLogDirectory;
    private readonly string crashDirectory;
    private readonly LogFileDatabase database;
    private string connection = "Disconnected";
    private string? aircraft;
    private DateTimeOffset? simulatorUtc;
    private long samples;
    private bool stopped;

    internal ProgramMonitor(string testLogDirectory, Action<string> statusChanged, string? rootDirectory = null)
    {
        this.testLogDirectory = testLogDirectory;
        this.statusChanged = statusChanged;
        var root = rootDirectory ?? CrashReporter.RootDirectory;
        crashDirectory = Path.Combine(root, "CrashReports");
        Directory.CreateDirectory(root);
        statusPath = Path.Combine(root, "program-status.json");
        DetectUncleanExit();
        database = new LogFileDatabase(root);
        timer = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, (_, _) => WriteHeartbeat(), Dispatcher.CurrentDispatcher);
        WriteHeartbeat();
        timer.Start();
    }

    internal LogFileDatabase Database => database;
    internal void Update(string connectionState, string? currentAircraft = null, DateTimeOffset? lastSimulatorUtc = null, bool sampleReceived = false)
    {
        connection = connectionState;
        if (!string.IsNullOrWhiteSpace(currentAircraft)) aircraft = currentAircraft;
        if (lastSimulatorUtc is not null) simulatorUtc = lastSimulatorUtc;
        if (sampleReceived) samples++;
    }

    internal void WriteHeartbeat()
    {
        if (stopped) return;
        try
        {
            var process = Process.GetCurrentProcess();
            var fileCount = database.Refresh(testLogDirectory, crashDirectory);
            WriteStatus(new { schemaVersion = 1, running = true, heartbeatUtc = DateTimeOffset.UtcNow,
                appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
                processId = Environment.ProcessId, processStartUtc = process.StartTime.ToUniversalTime(), workingSetBytes = process.WorkingSet64,
                connection, aircraft, samples, lastSimulatorUtc = simulatorUtc, indexedFiles = fileCount, databasePath = database.DatabasePath,
                lastCrashReport = CrashReporter.LastReportPath });
            statusChanged($"PROGRAM HEALTHY • {connection.ToUpperInvariant()} • {fileCount} DIAGNOSTIC FILES • {DateTime.UtcNow:HH:mm:ss}Z");
        }
        catch (Exception error)
        {
            statusChanged("PROGRAM MONITOR ERROR • " + error.GetBaseException().Message);
        }
    }

    private void DetectUncleanExit()
    {
        try
        {
            if (!File.Exists(statusPath)) return;
            using var json = JsonDocument.Parse(File.ReadAllText(statusPath));
            if (json.RootElement.TryGetProperty("running", out var running) && running.GetBoolean())
                CrashReporter.Write("previous_unclean_exit", null, new { previousStatusPath = statusPath, previousStatus = json.RootElement.Clone() }, directory: crashDirectory);
        }
        catch (Exception error) { CrashReporter.Write("status_recovery", error, directory: crashDirectory); }
    }

    private void WriteStatus(object value)
    {
        var temporary = statusPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, statusPath, true);
    }

    internal void Stop(string reason)
    {
        if (stopped) return;
        stopped = true;
        timer.Stop();
        try { WriteStatus(new { schemaVersion = 1, running = false, stoppedAtUtc = DateTimeOffset.UtcNow, reason,
            appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown", processId = Environment.ProcessId,
            connection, aircraft, samples, lastSimulatorUtc = simulatorUtc, databasePath = database.DatabasePath }); }
        catch { }
        database.Dispose();
    }
    public void Dispose() => Stop("disposed");
}
