using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Alpha6Ops.Desktop;

// Reads only disposable databases. The production view/query is exercised, including
// selection of two independent histories with the same callsign. Never connects MSFS.
internal static class LogbookChecks
{
    private sealed class InlineDispatcher : IFlightRuntimeDispatcher
    {
        public void VerifyAccess() => Application.Current.Dispatcher.VerifyAccess();
        public void Post(Action action) => Application.Current.Dispatcher.BeginInvoke(action);
    }
    private static readonly List<object> Checks = [];
    private static int failed;
    private static string output = "";
    private static void Check(string name, bool pass, string detail)
    {
        Checks.Add(new { name, passed = pass, detail });
        if (!pass) failed++;
        Console.WriteLine($"{(pass ? "PASS" : "FAIL")} {name}: {detail}");
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-29T20:00:00Z");
    private static void Tracking(FlightHistoryDatabase db, string id, int seconds, string title) =>
        db.RecordEvent(id, "tracking_event", Start.AddSeconds(seconds), new { Title = title, Summary = "Recorded observation", Detail = "GS 0 KT", Kind = "operation" });
    private static string AddFlight(FlightHistoryDatabase db, string callsign = "SAME500", string source = "live") =>
        db.BeginFlight("Regression", source, null, "Fenix A320", "YMML", "YSSY", callsign, "0.22.0");
    private static string Display(PilotLogbookWorkspace view, string id)
    {
        var grid = (DataGrid)view.FindName("FlightsGrid");
        grid.SelectedItem = grid.Items.Cast<PilotLogbookRow>().Single(row => row.Id == id);
        view.UpdateLayout();
        return ((TextBlock)view.FindName("DetailTimesText")).Text;
    }
    private static string Expected(string o, string f, string n, string i) => $"OUT {o}  •  OFF {f}\nON {n}  •  IN {i}";
    private static void Capture(FrameworkElement visual, string name)
    {
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
    }
    internal static int Run(string[] args)
    {
        output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var name in new[] { "DashboardStyles", "SierraDesignTokens", "SierraComponents" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Sierra4;component/{name}.xaml") });
        app.Resources["Yellow"] = Brushes.Yellow;
        try
        {
            Synthetic();
            if (args.Length > 2) SavedFlights(args[2]);
        }
        catch (Exception e) { Check("HARNESS", false, e.ToString()); }
        File.WriteAllText(Path.Combine(output, "logbook-checks.json"), JsonSerializer.Serialize(new {
            executed = Checks.Count, passed = Checks.Count - failed, failed, checks = Checks,
            desktopAssembly = typeof(MainWindow).Assembly.Location, desktopSha256 = Hash(typeof(MainWindow).Assembly.Location),
            simulatorConnected = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        app.Shutdown(); return failed == 0 ? 0 : 1;
    }
    private static void Synthetic()
    {
        using var db = new FlightHistoryDatabase(Path.Combine(output, "synthetic"));
        var complete = AddFlight(db);
        Tracking(db, complete, 1, "Block-out / taxi out"); Tracking(db, complete, 15, "Takeoff roll");
        Tracking(db, complete, 33, "Liftoff"); Tracking(db, complete, 60, "Touchdown");
        Tracking(db, complete, 90, "Block-in / flight complete"); db.EndFlight(complete, "Complete");
        db.SubmitPirep(complete, new { actualOut = Start.AddSeconds(2), actualOff = Start.AddTicks(338616943), actualOn = Start.AddSeconds(62), actualIn = Start.AddSeconds(92), status = "Completed" });
        var interrupted = AddFlight(db); Tracking(db, interrupted, 5, "Block-out / taxi out");
        Tracking(db, interrupted, 16, "Takeoff roll"); Tracking(db, interrupted, 20, "Engine 1 shutdown"); db.EndFlight(interrupted, "Interrupted");
        var legacy = AddFlight(db, "LEGACY"); Tracking(db, legacy, 1, "Block-out / taxi out");
        Tracking(db, legacy, 15, "Takeoff roll"); Tracking(db, legacy, 33, "Liftoff");
        Tracking(db, legacy, 60, "Touchdown"); Tracking(db, legacy, 90, "Block-in / flight complete"); db.EndFlight(legacy, "Complete");
        var phase = AddFlight(db, "REPLAY", "replay");
        foreach (var (seconds, name) in new[] { (2, "TaxiOut"), (34, "Airborne"), (61, "TaxiIn"), (91, "Complete") })
            db.RecordEvent(phase, "phase", Start.AddSeconds(seconds), new { phase = name, label = "Renamed presentation text" });
        Tracking(db, phase, 15, "Takeoff roll"); Tracking(db, phase, 30, "Liftoff"); db.EndFlight(phase, "Complete");
        var missing = AddFlight(db, "MISSING"); Tracking(db, missing, 15, "Takeoff roll");
        Tracking(db, missing, 20, "Touchdown rate unavailable"); Tracking(db, missing, 25, "Engine shutdown completed"); db.EndFlight(missing, "Interrupted");
        var explicitNull = AddFlight(db, "NULLS"); Tracking(db, explicitNull, 33, "Liftoff"); db.EndFlight(explicitNull, "Complete");
        db.SubmitPirep(explicitNull, new { actualOut = (string?)null, actualOff = (string?)null, actualOn = (string?)null, actualIn = (string?)null });
        var partial = AddFlight(db, "PARTIAL"); Tracking(db, partial, 1, "Block-out / taxi out"); Tracking(db, partial, 33, "Liftoff"); db.EndFlight(partial, "Complete");
        db.SubmitPirep(partial, new { actualOff = Start.AddSeconds(35) });
        var imported = AddFlight(db, "IMPORT", "FlyVirtual"); Tracking(db, imported, 33, "Liftoff"); db.EndFlight(imported, "Complete");
        var rows = db.ReadRecentFlights(); var original = rows.ToDictionary(r => r.Id, r => JsonSerializer.Serialize(db.ReadFlightEvents(r.Id)));
        var view = new PilotLogbookWorkspace(); var window = new Window { Content = view, Width = 1440, Height = 900 }; window.Show(); view.Render(db);
        try
        {
            var display = Display(view, complete);
            Check("AUTHORITATIVE_OFF_NOT_TAKEOFF_ROLL", display.Contains("OFF 20:00:33Z"), display);
            Check("AUTHORITATIVE_ALL_OOOI_NOT_DESCRIPTIVE_EVENTS", display == Expected("20:00:02Z", "20:00:33Z", "20:01:02Z", "20:01:32Z"), display);
            Check("RESOLUTION_RETAINS_FULL_PRECISION", PilotLogbookOperationalTimes.Read(db.ReadFlightEvents(complete)).Off == DateTimeOffset.Parse("2026-09-29T20:00:33.8616943Z"), "Only the visible format truncates to seconds.");
            Check("SAME_CALLSIGN_INTERRUPTED_ID_NO_BORROWED_ACTUALS", Display(view, interrupted) == Expected("20:00:05Z", "—", "—", "—"), Display(view, interrupted));
            Check("LEGACY_EXACT_MILESTONE_FALLBACK", Display(view, legacy) == Expected("20:00:01Z", "20:00:33Z", "20:01:00Z", "20:01:30Z"), Display(view, legacy));
            Check("STRUCTURED_PHASES_IGNORE_LABELS_AND_DETAILS", Display(view, phase) == Expected("20:00:02Z", "20:00:34Z", "20:01:01Z", "20:01:31Z"), Display(view, phase));
            Check("DESCRIPTIVE_EVENTS_CANNOT_FABRICATE_MILESTONES", Display(view, missing) == Expected("—", "—", "—", "—"), Display(view, missing));
            Check("EXPLICIT_NULL_ACTUALS_STAY_UNKNOWN", Display(view, explicitNull) == Expected("—", "—", "—", "—"), Display(view, explicitNull));
            Check("OLDER_PARTIAL_PIREP_FALLBACK_ONLY_FOR_ABSENT_FIELDS", Display(view, partial) == Expected("20:00:01Z", "20:00:35Z", "—", "—"), Display(view, partial));
            Check("IMPORTED_HISTORY_NO_INVENTED_OOOI", Display(view, imported) == "Not recorded by FlyVirtual", Display(view, imported));
            Check("MALFORMED_PAYLOAD_NO_COMPUTER_TIME_SUBSTITUTION", PilotLogbookOperationalTimes.Read([
                new(1,"pirep_submitted",Start.ToString("O"),null,"{"),
                new(2,"phase",Start.ToString("O"),null,"[]"),
                new(3,"tracking_event",Start.ToString("O"),null,"{\"Title\":\"Liftoff\"}")]).Off is null, "Unreadable history remains unknown; wall time is not simulator OFF.");
            Check("INVALID_PRESENT_ACTUAL_DOES_NOT_FALL_BACK", PilotLogbookOperationalTimes.Read([
                new(1,"tracking_event",Start.ToString("O"),Start.ToString("O"),"{\"Title\":\"Liftoff\"}"),
                new(2,"pirep_submitted",Start.ToString("O"),null,"{\"actualOff\":42}")]).Off is null, "Malformed explicit actual is not replaced by a plausible detail.");
            Check("SELECTION_PRESERVES_HISTORY_TIMELINE_PIREP_AND_DURATION_INPUTS", rows.SequenceEqual(db.ReadRecentFlights()) && rows.All(r => original[r.Id] == JsonSerializer.Serialize(db.ReadFlightEvents(r.Id))), "All rows and event payloads unchanged after selection.");
        }
        finally { window.Close(); }
    }
    private static void SavedFlights(string sourceDatabase)
    {
        var originalHash = Hash(sourceDatabase);
        var state = Path.Combine(output, "saved-profile"); Directory.CreateDirectory(state);
        File.Copy(sourceDatabase, Path.Combine(state, "Alpha6OPS-flights.sqlite"));
        using var owner = new FlightSessionCoordinator(state, true, new InlineDispatcher());
        var window = new MainWindow(owner, state);
        window.Show(); window.ShowPilotLogbook(); window.UpdateLayout();
        try
        {
            var view = window.PilotLogbookView;
            var db = window.FlightHistory!;
            var beforeRows = db.ReadRecentFlights(1000); var beforeEvents = beforeRows.ToDictionary(r => r.Id, r => JsonSerializer.Serialize(db.ReadFlightEvents(r.Id)));
            var beforeOwner = JsonSerializer.Serialize(owner.Snapshot);
            var cases = new[] {
                ("e475984bc9f54f8ab466e75a0d443759", "JST500", "19:57:34Z", "20:07:33Z", "21:16:56Z", "21:25:10Z", "1H 28M"),
                ("4208cd32c171441c891cb4429b5f19d8", "JST771", "21:41:34Z", "21:52:01Z", "22:55:18Z", "23:03:55Z", "1H 22M"),
                ("0bbf3ce8af1d4880ad8d9ed5f0ce4fbe", "JST500 interrupted", "20:00:32Z", "—", "—", "—", "0H 21M")
            };
            foreach (var (id, name, o, f, n, i, duration) in cases)
            {
                var display = Display(view, id);
                Check(name + "_OFF", display.Contains("OFF " + f) && (f != "—" || display.EndsWith("IN —")), display);
                Check(name + "_ALL_ACTUALS", display == Expected(o, f, n, i), display);
                Check(name + "_DURATION_UNCHANGED", ((TextBlock)view.FindName("DetailDurationText")).Text == duration, ((TextBlock)view.FindName("DetailDurationText")).Text);
                Capture(window, name.Replace(' ', '-') + "-disconnected-logbook");
            }
            foreach (var row in beforeRows.Where(r => r.Source == "live" && r.FinalPhase == "Complete"))
            {
                var pirep = db.ReadFlightEvents(row.Id).SingleOrDefault(e => e.Kind == "pirep_submitted"); if (pirep is null) continue;
                using var doc = JsonDocument.Parse(pirep.DetailJson);
                if (!doc.RootElement.TryGetProperty("actualOff", out _)) continue; // Old PIREPs deliberately lacked OOOI; verified separately below.
                string Time(string field) => doc.RootElement.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var time) ? time.UtcDateTime.ToString("HH:mm:ss'Z'") : "—";
                var expected = Expected(Time("actualOut"), Time("actualOff"), Time("actualOn"), Time("actualIn"));
                Check("SAVED_PIREP_ACTUALS_" + row.Id, Display(view, row.Id) == expected, row.FlightNumber + " " + Display(view, row.Id));
            }
            foreach (var (id, o, f, n, i) in new[] {
                ("1678754d9da347018ad30522117c33b5", "21:37:22Z", "21:51:26Z", "01:23:21Z", "01:27:53Z"),
                ("ddec578123094c3397e3246988aee779", "09:31:53Z", "09:41:00Z", "11:12:20Z", "11:16:56Z"),
                ("4a4006094e48455e9708bbae1f8e3d8d", "02:05:49Z", "02:15:43Z", "03:54:41Z", "03:57:34Z"),
                ("4cca7523ff6540c79ffe36e74528d43a", "21:01:02Z", "21:15:25Z", "22:52:35Z", "23:02:45Z"),
                ("29f7eac7cc1a405898568de1da283922", "03:37:53Z", "03:50:14Z", "05:08:29Z", "05:11:54Z"),
                ("aaa8c591d4fb459a92ecc2ea71199c94", "21:20:56Z", "21:29:31Z", "00:20:55Z", "00:28:37Z"),
                ("62c4bb09115045bc9fab49e32223667d", "22:21:59Z", "22:43:30Z", "01:09:53Z", "01:15:45Z") })
                Check("SAVED_LEGACY_MILESTONES_" + id, Display(view,id) == Expected(o,f,n,i), Display(view,id));
            Check("JST500_FULL_PRECISION_OFF", PilotLogbookOperationalTimes.Read(db.ReadFlightEvents("e475984bc9f54f8ab466e75a0d443759")).Off == DateTimeOffset.Parse("2026-09-29T20:07:33.8616943Z"), "Authoritative 100ns timestamp retained.");
            Check("SAVED_ROWS_TIMELINES_PIREPS_UNCHANGED", beforeRows.SequenceEqual(db.ReadRecentFlights(1000)) && beforeRows.All(r => beforeEvents[r.Id] == JsonSerializer.Serialize(db.ReadFlightEvents(r.Id))), "All 502 histories checked.");
            Check("DISCONNECTED_RUNTIME_UNCHANGED", beforeOwner == JsonSerializer.Serialize(owner.Snapshot), "Logbook selection never connects or modifies the session snapshot.");
            Check("ORIGINAL_SAVED_DATABASE_UNCHANGED", Hash(sourceDatabase) == originalHash, originalHash);
        }
        finally
        {
            typeof(MainWindow).GetMethod("DisposePresentation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            typeof(MainWindow).GetField("exiting", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
            window.Close();
        }
    }
}
