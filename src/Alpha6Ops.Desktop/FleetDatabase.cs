using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Alpha6Ops.Desktop;

public record FleetAircraft(string Registration, string Family, string Model, string SerialNumber,
    string LineNumber, string Status, string DeliveryDate, string SourceUrl, string ObservedDate);

// Windows' system SQLite library, opened read-only. This catalog is reference data,
// not a writable tenant/VA fleet or an aircraft operational-availability database.
internal static class FleetDatabase
{
    internal static IReadOnlyList<FleetAircraft> Read()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fleet", "aircraft.sqlite");
        IntPtr db = IntPtr.Zero, statement = IntPtr.Zero;
        try
        {
            if (Open(path, out db, 1, IntPtr.Zero) != 0) throw new IOException("Cannot open the fleet catalog. Keep the fleet folder beside the application.");
            const string sql = "SELECT a.registration,a.family,a.model,a.manufacturer_serial,COALESCE(a.line_number,''),a.status,a.delivery_date_text,a.source_url,s.observed_date FROM aircraft_reference a JOIN source_snapshot s ON a.snapshot_id=s.snapshot_id WHERE s.operator_code='DAL' ORDER BY a.registration";
            if (Prepare(db, sql, -1, out statement, IntPtr.Zero) != 0) throw new IOException("Fleet catalog schema is not supported.");
            var rows = new List<FleetAircraft>();
            int result;
            while ((result = Step(statement)) == 100)
            {
                string Cell(int col) => Marshal.PtrToStringUTF8(ColumnText(statement, col)) ?? "";
                rows.Add(new(Cell(0), Cell(1), Cell(2), Cell(3), Cell(4), Cell(5), Cell(6), Cell(7), Cell(8)));
            }
            if (result != 101) throw new IOException("Could not read the complete fleet catalog.");
            if (rows.Count == 0) throw new IOException("Fleet catalog is empty.");
            return rows;
        }
        finally { if (statement != IntPtr.Zero) FinalizeStatement(statement); if (db != IntPtr.Zero) Close(db); }
    }
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_open_v2", CallingConvention=CallingConvention.Cdecl)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_prepare_v2", CallingConvention=CallingConvention.Cdecl)]
    private static extern int Prepare(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr statement, IntPtr tail);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_step", CallingConvention=CallingConvention.Cdecl)] private static extern int Step(IntPtr statement);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_column_text", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr ColumnText(IntPtr statement, int col);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_finalize", CallingConvention=CallingConvention.Cdecl)] private static extern int FinalizeStatement(IntPtr statement);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_close", CallingConvention=CallingConvention.Cdecl)] private static extern int Close(IntPtr db);
}
