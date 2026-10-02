using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal sealed record FlightRecoveryState(
    string FlightNumber, DateTimeOffset PlannedDepartureUtc, string Source, string Aircraft,
    FlightPhase Phase, DateTimeOffset? LastSimulatorUtc, Telemetry? LastTelemetry, double? RouteProgress,
    DateTimeOffset? ActualOut, DateTimeOffset? ActualIn, IReadOnlyList<FlightEvent> PhaseEvents,
    IReadOnlyList<TrackingEventEntry> TrackingEvents, FlightTrackingMonitorState? MonitorState,
    DateTimeOffset SavedUtc,
    string? FlightHistoryId=null, string? CompletedFlightHistoryId=null, bool JoinedInProgress=false,
    DateTimeOffset? ObservedSessionStart=null, GeoPosition? ObservedStartPosition=null, bool ObservedStartOnGround=false,
    bool AssignmentAccepted=false, string? AssignmentOrigin=null, string? AssignmentDestination=null,
    bool PilotConfirmedBlockIn=false, string? ArrivalMismatch=null, ArrivalEstimatorState? ArrivalEstimatorState=null);

internal static class FlightRecoveryStore
{
    internal static readonly ActivitySource Timing = new("Alpha6Ops.Recovery");
    private static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly int[] RetryDelays = [10, 20, 40, 80];
    private static string PathName(string? root=null)=>Path.GetFullPath(Path.Combine(root??CrashReporter.RootDirectory,"active-flight-state.json"));

    internal static FlightRecoveryState? Load(ActiveFlightPlan plan,string? root=null)
    {
        try
        {
            var path = PathName(root);
            using var lease = Enter(path);
            FlightRecoveryState? state = null;
            try
            {
                Retry(() =>
                {
                    // Published files are immutable. Sharing delete permits a rename while an
                    // external reader holds the old complete file; no write sharing is needed.
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                    using var document = JsonDocument.Parse(stream);
                    foreach (var name in new[] { "FlightNumber", "PlannedDepartureUtc", "Source", "Aircraft", "Phase", "PhaseEvents", "TrackingEvents", "SavedUtc" })
                        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(name, out _))
                            throw new JsonException("Incomplete flight recovery: " + name);
                    state = document.Deserialize<FlightRecoveryState>(JsonOptions);
                    Validate(state);
                }, path, "read");
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            return state!.FlightNumber.Equals(plan.FlightNumber,StringComparison.OrdinalIgnoreCase)&&state.PlannedDepartureUtc==plan.PlannedDepartureUtc?state:null;
        }
        catch(Exception error) when(error is IOException or JsonException or UnauthorizedAccessException){CrashReporter.Write("flight_recovery_load",error,directory:root);return null;}
    }

    internal static void Save(FlightRecoveryState state,string? root=null)
    {
        using var timing = Timing.StartActivity("Save");
        Validate(state);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
        var path = PathName(root);
        using var lease = Enter(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            Retry(() =>
            {
                // The fixed staging name is safe under the per-path lock and bounds leftovers
                // to one file even after a process crash. It is never a recovery source.
                using var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }, temporary, "stage/flush");
            // Same-directory rename: never delete or truncate the authoritative file first.
            // A short external Windows handle can deny this even after our stream is closed.
            Retry(() => File.Move(temporary, path, overwrite: true), path, "replace");
        }
        finally
        {
            try { Retry(() => File.Delete(temporary), temporary, "cleanup"); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Do not mask a replacement failure. The one bounded leftover is overwritten
                // by the next save or removed by Delete, and must never be loaded as truth.
                CrashReporter.Write("flight_recovery_cleanup", error, directory: root);
            }
        }
    }

    internal static void Delete(string? root=null)
    {
        var path = PathName(root);
        using var lease = Enter(path);
        try
        {
            Retry(() => File.Delete(path), path, "delete");
            Retry(() => File.Delete(path + ".tmp"), path + ".tmp", "cleanup");
        }
        catch (DirectoryNotFoundException) { }
    }

    private static void Validate(FlightRecoveryState? state)
    {
        if (state is null || string.IsNullOrWhiteSpace(state.FlightNumber) || string.IsNullOrWhiteSpace(state.Source)
            || string.IsNullOrWhiteSpace(state.Aircraft) || state.PlannedDepartureUtc == default || state.SavedUtc == default
            || !Enum.IsDefined(state.Phase) || state.PhaseEvents is null || state.TrackingEvents is null
            || state.MonitorState is { } monitor && (monitor.Fired is null || monitor.Fired.Any(e => e is null))
            || state.PhaseEvents.Any(e => e is null || !Enum.IsDefined(e.Phase)) || state.TrackingEvents.Any(e => e is null))
            throw new JsonException("Incomplete or invalid flight recovery state.");
    }

    private static void Retry(Action operation, string path, string stage)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { operation(); return; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= RetryDelays.Length || !Transient(error, path))
                {
                    // Preserve exception type/HResult for existing callers. The coordinator
                    // includes this context in its existing unsuppressed save report.
                    error.Data["RecoveryStage"] = stage;
                    error.Data["RecoveryAttempts"] = attempt + 1;
                    error.Data["RecoveryPath"] = path;
                    throw;
                }
                // No listener in ordinary operation. Diagnostics can observe an actual
                // transient violation without replacing the I/O or changing retry policy.
                using var retryTiming=Timing.StartActivity("Retry",ActivityKind.Internal,default(ActivityContext),tags:new Dictionary<string,object?>{{"path",path},{"stage",stage},{"attempt",attempt+1}});
                Thread.Sleep(RetryDelays[attempt]);
            }
        }
    }

    private static bool Transient(Exception error, string path)
    {
        var code = error.HResult & 0xffff;
        if (code is 32 or 33) return true; // sharing / lock violation
        if (code != 5 || Directory.Exists(path)) return false;
        // AccessDenied is also the observed transient rename failure. Known read-only
        // targets fail immediately; an unknown ACL denial gets only the same 150 ms budget.
        try { return (File.GetAttributes(path) & FileAttributes.ReadOnly) == 0; }
        catch (FileNotFoundException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
        catch (IOException) { return false; }
    }

    private static IDisposable Enter(string path)
    {
        var gate = Gates.GetOrAdd(path, _ => new object());
        if (!Monitor.TryEnter(gate, TimeSpan.FromSeconds(2)))
            throw new IOException("Timed out waiting for flight recovery persistence: " + path);
        return new Lease(gate);
    }

    private sealed class Lease(object gate) : IDisposable
    {
        public void Dispose() => Monitor.Exit(gate);
    }
}
