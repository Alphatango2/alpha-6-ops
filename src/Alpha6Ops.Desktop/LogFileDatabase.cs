using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Alpha6Ops.Desktop;

internal record DiagnosticFile(string Kind, string Name, string Status, long SizeBytes, string ModifiedUtc, string Path);

internal sealed class LogFileDatabase : IDisposable
{
    private readonly object gate = new();
    private IntPtr database;
    internal string DatabasePath { get; }

    internal LogFileDatabase(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        DatabasePath = Path.Combine(rootDirectory, "Alpha6OPS-logs.sqlite");
        if (Open(DatabasePath, out database) != 0 || database == IntPtr.Zero) throw new IOException("Could not open the OPS log database.");
        Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS diagnostic_file(path TEXT PRIMARY KEY, kind TEXT NOT NULL, name TEXT NOT NULL, status TEXT NOT NULL, size_bytes INTEGER NOT NULL, created_utc TEXT NOT NULL, modified_utc TEXT NOT NULL, indexed_utc TEXT NOT NULL); CREATE INDEX IF NOT EXISTS ix_diagnostic_file_modified ON diagnostic_file(modified_utc DESC);");
    }

    internal int Refresh(string testLogDirectory, string crashDirectory)
    {
        lock (gate)
        {
            IndexDirectory(testLogDirectory, "flight_log");
            IndexDirectory(crashDirectory, "crash_report");
            return ScalarInt("SELECT COUNT(*) FROM diagnostic_file;");
        }
    }

    internal IReadOnlyList<DiagnosticFile> ReadRecent(int limit = 250)
    {
        lock (gate)
        {
            var rows = new List<DiagnosticFile>();
            var context = GCHandle.Alloc(rows);
            try
            {
                var sql = $"SELECT kind,name,status,size_bytes,modified_utc,path FROM diagnostic_file ORDER BY modified_utc DESC LIMIT {Math.Clamp(limit, 1, 1000)};";
                var result = Exec(database, sql, ReadRow, GCHandle.ToIntPtr(context), out var error);
                if (result != 0) throw DatabaseError(error, "Could not read the OPS log database.");
            }
            finally { context.Free(); }
            return rows;
        }
    }

    private void IndexDirectory(string directory, string kind)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "Alpha6OPS-*", SearchOption.TopDirectoryOnly))
        {
            var file = new FileInfo(path);
            var status = kind == "crash_report" ? "Crash" : path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ? "Journal" : "Export";
            Execute($"INSERT INTO diagnostic_file(path,kind,name,status,size_bytes,created_utc,modified_utc,indexed_utc) VALUES('{Q(file.FullName)}','{Q(kind)}','{Q(file.Name)}','{Q(status)}',{file.Length},'{file.CreationTimeUtc:O}','{file.LastWriteTimeUtc:O}','{DateTime.UtcNow:O}') ON CONFLICT(path) DO UPDATE SET status=excluded.status,size_bytes=excluded.size_bytes,modified_utc=excluded.modified_utc,indexed_utc=excluded.indexed_utc;");
        }
    }

    private int ScalarInt(string sql)
    {
        var value = new[] { 0 };
        var context = GCHandle.Alloc(value);
        try
        {
            var result = Exec(database, sql, ReadInt, GCHandle.ToIntPtr(context), out var error);
            if (result != 0) throw DatabaseError(error, "Could not query the OPS log database.");
            return value[0];
        }
        finally { context.Free(); }
    }

    private void Execute(string sql)
    {
        var result = Exec(database, sql, null, IntPtr.Zero, out var error);
        if (result != 0) throw DatabaseError(error, "Could not update the OPS log database.");
    }

    private static int ReadInt(IntPtr context, int columns, IntPtr values, IntPtr names)
    {
        var target = (int[])GCHandle.FromIntPtr(context).Target!;
        if (columns > 0 && int.TryParse(Utf8(Marshal.ReadIntPtr(values)), out var value)) target[0] = value;
        return 0;
    }

    private static int ReadRow(IntPtr context, int columns, IntPtr values, IntPtr names)
    {
        if (columns < 6) return 0;
        string Cell(int index) => Utf8(Marshal.ReadIntPtr(values, index * IntPtr.Size));
        ((List<DiagnosticFile>)GCHandle.FromIntPtr(context).Target!).Add(new(Cell(0), Cell(1), Cell(2), long.TryParse(Cell(3), out var size) ? size : 0, Cell(4), Cell(5)));
        return 0;
    }

    private static string Utf8(IntPtr value) => value == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(value) ?? "";
    private static string Q(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static IOException DatabaseError(IntPtr error, string fallback)
    {
        var message = error == IntPtr.Zero ? fallback : Marshal.PtrToStringUTF8(error) ?? fallback;
        if (error != IntPtr.Zero) Free(error);
        return new IOException(message);
    }
    public void Dispose() { lock (gate) { if (database != IntPtr.Zero) { Close(database); database = IntPtr.Zero; } } }

    private delegate int ExecCallback(IntPtr context, int columns, IntPtr values, IntPtr names);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)] private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)] private static extern int Exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, ExecCallback? callback, IntPtr context, out IntPtr error);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_free", CallingConvention = CallingConvention.Cdecl)] private static extern void Free(IntPtr value);
    [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)] private static extern int Close(IntPtr db);
}
