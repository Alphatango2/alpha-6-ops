using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
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
    private readonly Dispatcher dispatcher;
    private Task<string>? heartbeatWorker;
    private Task<IReadOnlyList<DiagnosticFile>>? readWorker;
    private Task pendingOperation = Task.CompletedTask;
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
        dispatcher = Dispatcher.CurrentDispatcher;
        timer = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, OnTimerTick, dispatcher);
        _ = WriteHeartbeatAsync();
        timer.Start();
    }

    internal LogFileDatabase Database => database;
    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (!pendingOperation.IsCompleted) return;
        await WriteHeartbeatAsync();
    }
    internal void Update(string connectionState, string? currentAircraft = null, DateTimeOffset? lastSimulatorUtc = null, bool sampleReceived = false)
    {
        dispatcher.VerifyAccess();
        connection = connectionState;
        if (!string.IsNullOrWhiteSpace(currentAircraft)) aircraft = currentAircraft;
        if (lastSimulatorUtc is not null) simulatorUtc = lastSimulatorUtc;
        if (sampleReceived) samples++;
    }

    internal Task WriteHeartbeatAsync()
    {
        dispatcher.VerifyAccess();
        if (stopped) return Task.CompletedTask;
        // Timer ticks share the in-flight task instead of queuing unbounded work.
        if (!pendingOperation.IsCompleted) return pendingOperation;
        var snapshot = new MonitorSnapshot(connection, aircraft, simulatorUtc, samples);
        heartbeatWorker = Task.Run(() => WriteHeartbeat(snapshot));
        pendingOperation = PublishHeartbeatAsync(heartbeatWorker);
        return pendingOperation;
    }

    // Explicit log-view requests need a scan started after the request, even if the timer
    // was already indexing. All callers resume on the dispatcher before opening the view.
    internal async Task RefreshNowAsync()
    {
        dispatcher.VerifyAccess();
        while (!pendingOperation.IsCompleted) await pendingOperation;
        await WriteHeartbeatAsync();
    }

    internal async Task<IReadOnlyList<DiagnosticFile>> ReadRecentAsync()
    {
        dispatcher.VerifyAccess();
        // A timer can start between await continuations. Recheck ownership before starting
        // any read so the native handle has one operation, and the UI never waits on its lock.
        while (!pendingOperation.IsCompleted) await pendingOperation;
        if (stopped) return [];
        readWorker = Task.Run(() => database.ReadRecent());
        pendingOperation = readWorker;
        return await readWorker;
    }

    private async Task PublishHeartbeatAsync(Task<string> worker)
    {
        var status = await worker.ConfigureAwait(false);
        try { await dispatcher.InvokeAsync(() => { if (!stopped) statusChanged(status); }); }
        catch (TaskCanceledException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) { }
    }

    private sealed record MonitorSnapshot(string Connection, string? Aircraft, DateTimeOffset? SimulatorUtc, long Samples);

    // Only this worker touches refresh/status I/O. It never waits on the dispatcher.
    private string WriteHeartbeat(MonitorSnapshot snapshot)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var fileCount = database.Refresh(testLogDirectory, crashDirectory);
            WriteStatus(new { schemaVersion = 1, running = true, heartbeatUtc = DateTimeOffset.UtcNow,
                appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
                processId = Environment.ProcessId, processStartUtc = process.StartTime.ToUniversalTime(), workingSetBytes = process.WorkingSet64,
                connection = snapshot.Connection, aircraft = snapshot.Aircraft, samples = snapshot.Samples, lastSimulatorUtc = snapshot.SimulatorUtc, indexedFiles = fileCount, databasePath = database.DatabasePath,
                lastCrashReport = CrashReporter.LastReportPath });
            return $"PROGRAM HEALTHY • {snapshot.Connection.ToUpperInvariant()} • {fileCount} DIAGNOSTIC FILES • {DateTime.UtcNow:HH:mm:ss}Z";
        }
        catch (Exception error)
        {
            return "PROGRAM MONITOR ERROR • " + error.GetBaseException().Message;
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
        dispatcher.VerifyAccess();
        if (stopped) return;
        stopped = true;
        timer.Stop();
        // Join only the I/O worker, never its dispatcher continuation. This preserves the
        // final stopped record and prevents disposal while SQLite still owns statements.
        heartbeatWorker?.GetAwaiter().GetResult();
        // Read errors belong to their awaiting view; they must not prevent shutdown cleanup.
        try { readWorker?.GetAwaiter().GetResult(); } catch (IOException) { }
        try { WriteStatus(new { schemaVersion = 1, running = false, stoppedAtUtc = DateTimeOffset.UtcNow, reason,
            appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown", processId = Environment.ProcessId,
            connection, aircraft, samples, lastSimulatorUtc = simulatorUtc, databasePath = database.DatabasePath }); }
        catch { }
        database.Dispose();
    }
    public void Dispose() => Stop("disposed");
}
