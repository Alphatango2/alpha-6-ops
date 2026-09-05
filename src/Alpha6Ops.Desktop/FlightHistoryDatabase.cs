using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Alpha6Ops.Desktop;

internal sealed record FlightHistoryEntry(string Pilot, string Source, string Aircraft, string Route, string StartedUtc, string? EndedUtc, string? FinalPhase, int EventCount);

// Structured flight history: one row per flight run (replay or live), with its phase/milestone
// events attached. Distinct from LogFileDatabase, which only indexes exported diagnostic files.
// Text fields here (aircraft titles, SimBrief-imported routes) are more externally influenced than
// LogFileDatabase's file paths, so writes go through bound parameters rather than string-escaping.
internal sealed class FlightHistoryDatabase : IDisposable
{
    private readonly object gate = new();
    private IntPtr database;
    private readonly Dictionary<string, int> sequences = new();
    internal string DatabasePath { get; }

    internal FlightHistoryDatabase(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        DatabasePath = Path.Combine(rootDirectory, "Alpha6OPS-flights.sqlite");
        if (Open(DatabasePath, out database) != 0 || database == IntPtr.Zero) throw new IOException("Could not open the flight history database.");
        Execute("""
            PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL;
            CREATE TABLE IF NOT EXISTS flight(
              id TEXT PRIMARY KEY, pilot TEXT NOT NULL, source TEXT NOT NULL, source_detail TEXT,
              aircraft TEXT NOT NULL, origin TEXT, destination TEXT, flight_number TEXT,
              started_utc TEXT NOT NULL, ended_utc TEXT, final_phase TEXT, app_version TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS flight_event(
              flight_id TEXT NOT NULL REFERENCES flight(id), sequence INTEGER NOT NULL,
              kind TEXT NOT NULL, recorded_utc TEXT NOT NULL, simulator_utc TEXT, detail_json TEXT NOT NULL,
              PRIMARY KEY(flight_id, sequence));
            CREATE INDEX IF NOT EXISTS ix_flight_started ON flight(started_utc DESC);
            """);
    }

    internal string BeginFlight(string pilot, string source, string? sourceDetail, string aircraft, string? origin, string? destination, string? flightNumber, string appVersion)
    {
        var id = Guid.NewGuid().ToString("N");
        lock (gate)
        {
            sequences[id] = 0;
            using var statement = Prepare("INSERT INTO flight(id,pilot,source,source_detail,aircraft,origin,destination,flight_number,started_utc,app_version) VALUES(?,?,?,?,?,?,?,?,?,?);");
            statement.BindText(1, id);
            statement.BindText(2, pilot);
            statement.BindText(3, source);
            statement.BindTextOrNull(4, sourceDetail);
            statement.BindText(5, aircraft);
            statement.BindTextOrNull(6, origin);
            statement.BindTextOrNull(7, destination);
            statement.BindTextOrNull(8, flightNumber);
            statement.BindText(9, DateTimeOffset.UtcNow.ToString("O"));
            statement.BindText(10, appVersion);
            statement.ExecuteNonQuery();
        }
        return id;
    }

    internal void RecordEvent(string flightId, string kind, DateTimeOffset? simulatorUtc, object detail)
    {
        lock (gate)
        {
            var sequence = sequences.TryGetValue(flightId, out var current) ? current + 1 : 1;
            sequences[flightId] = sequence;
            using var statement = Prepare("INSERT INTO flight_event(flight_id,sequence,kind,recorded_utc,simulator_utc,detail_json) VALUES(?,?,?,?,?,?);");
            statement.BindText(1, flightId);
            statement.BindInt64Value(2, sequence);
            statement.BindText(3, kind);
            statement.BindText(4, DateTimeOffset.UtcNow.ToString("O"));
            statement.BindTextOrNull(5, simulatorUtc?.ToString("O"));
            statement.BindText(6, JsonSerializer.Serialize(detail));
            statement.ExecuteNonQuery();
        }
    }

    internal void EndFlight(string flightId, string finalPhase)
    {
        lock (gate)
        {
            using var statement = Prepare("UPDATE flight SET ended_utc=?, final_phase=? WHERE id=?;");
            statement.BindText(1, DateTimeOffset.UtcNow.ToString("O"));
            statement.BindText(2, finalPhase);
            statement.BindText(3, flightId);
            statement.ExecuteNonQuery();
            sequences.Remove(flightId);
        }
    }

    internal IReadOnlyList<FlightHistoryEntry> ReadRecentFlights(int limit = 200)
    {
        lock (gate)
        {
            var rows = new List<FlightHistoryEntry>();
            var context = GCHandle.Alloc(rows);
            try
            {
                var sql = $"""
                    SELECT f.pilot, f.source, f.aircraft,
                           COALESCE(f.origin,'') || CASE WHEN f.destination IS NULL THEN '' ELSE ' -> ' || f.destination END,
                           f.started_utc, f.ended_utc, f.final_phase, (SELECT COUNT(*) FROM flight_event e WHERE e.flight_id = f.id)
                    FROM flight f ORDER BY f.started_utc DESC LIMIT {Math.Clamp(limit, 1, 1000)};
                    """;
                var result = Exec(database, sql, ReadRow, GCHandle.ToIntPtr(context), out var error);
                if (result != 0) throw DatabaseError(error, "Could not read the flight history database.");
            }
            finally { context.Free(); }
            return rows;
        }
    }

    private static int ReadRow(IntPtr context, int columns, IntPtr values, IntPtr names)
    {
        if (columns < 8) return 0;
        string? Cell(int index) { var value = Marshal.ReadIntPtr(values, index * IntPtr.Size); return value == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(value); }
        ((List<FlightHistoryEntry>)GCHandle.FromIntPtr(context).Target!).Add(new(
            Cell(0) ?? "", Cell(1) ?? "", Cell(2) ?? "", Cell(3) ?? "", Cell(4) ?? "", Cell(5), Cell(6),
            int.TryParse(Cell(7), out var count) ? count : 0));
        return 0;
    }

    private void Execute(string sql)
    {
        var result = Exec(database, sql, null, IntPtr.Zero, out var error);
        if (result != 0) throw DatabaseError(error, "Could not initialize the flight history database.");
    }

    private PreparedStatement Prepare(string sql)
    {
        if (PrepareV2(database, sql, -1, out var statement, out _) != 0 || statement == IntPtr.Zero)
            throw new IOException("Could not prepare a flight history statement.");
        return new PreparedStatement(statement);
    }

    private static IOException DatabaseError(IntPtr error, string fallback)
    {
        var message = error == IntPtr.Zero ? fallback : Marshal.PtrToStringUTF8(error) ?? fallback;
        if (error != IntPtr.Zero) Free(error);
        return new IOException(message);
    }

    public void Dispose() { lock (gate) { if (database != IntPtr.Zero) { Close(database); database = IntPtr.Zero; } } }

    private delegate int ExecCallback(IntPtr context, int columns, IntPtr values, IntPtr names);
    private static readonly IntPtr Transient = new(-1);

    private readonly struct PreparedStatement(IntPtr handle) : IDisposable
    {
        internal void BindText(int index, string value) => FlightHistoryDatabase.BindText(handle, index, value, -1, Transient);
        internal void BindTextOrNull(int index, string? value) { if (value is null) FlightHistoryDatabase.BindNull(handle, index); else BindText(index, value); }
        internal void BindInt64Value(int index, long value) => FlightHistoryDatabase.BindInt64(handle, index, value);
        internal void ExecuteNonQuery() { var result = FlightHistoryDatabase.Step(handle); if (result != 101 /* SQLITE_DONE */) throw new IOException($"Flight history write failed (0x{result:X})."); }
        public void Dispose() => FlightHistoryDatabase.FinalizeStatement(handle);
    }

    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)] private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)] private static extern int Close(IntPtr db);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)] private static extern int Exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, ExecCallback? callback, IntPtr context, out IntPtr error);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_free", CallingConvention = CallingConvention.Cdecl)] private static extern void Free(IntPtr value);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)] private static extern int PrepareV2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int nByte, out IntPtr stmt, out IntPtr tail);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)] private static extern int BindText(IntPtr stmt, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int nBytes, IntPtr destructor);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl)] private static extern int BindInt64(IntPtr stmt, int index, long value);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_null", CallingConvention = CallingConvention.Cdecl)] private static extern int BindNull(IntPtr stmt, int index);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)] private static extern int Step(IntPtr stmt);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)] private static extern int FinalizeStatement(IntPtr stmt);
}
