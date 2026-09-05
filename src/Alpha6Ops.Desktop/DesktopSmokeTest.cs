using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Exercises the real WPF window and event loop. Only runs with an explicit diagnostic flag.
internal static class DesktopSmokeTest
{
    internal static async Task RunAsync(MainWindow window, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        try
        {
            window.Show();
            await DashboardSmokeTest.RunAsync(window, outputDirectory);
            window.SetAdvanced(true);
            window.FixtureCombo.SelectedIndex = 0; // ignore any saved preference so the fixture-dependent assertions below stay deterministic
            var replay = window.RunReplayAsync(20);
            window.Close(); // normal close must hide, not dispose the window or end replay
            if (window.IsVisible || !window.TrayVisible || !window.IsRunning)
                throw new InvalidOperationException("Close-to-tray did not preserve the running replay.");
            await replay;
            var legs = window.Projection;
            if (legs[1].DepartureDelayMinutes != 30 || legs[2].DepartureDelayMinutes != 5 || !legs[0].Completed)
                throw new InvalidOperationException("Desktop replay results differ from the domain contract.");
            if (window.FlightHistory is null) throw new InvalidOperationException("Flight history database did not initialize.");
            var flights = window.FlightHistory.ReadRecentFlights();
            if (flights.Count != 1 || flights[0].Source != "replay" || flights[0].Aircraft != "N600A6" || flights[0].EventCount != 4 || flights[0].FinalPhase != "Complete")
                throw new InvalidOperationException("Flight history did not record the replay run correctly.");
            window.RestoreWindow();
            if (!window.IsVisible) throw new InvalidOperationException("Tray restore failed.");
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var image = File.Create(Path.Combine(outputDirectory, "desktop-preview.png"))) encoder.Save(image);
            window.ResetPreview();
            var catalog = FleetDatabase.Read();
            if (catalog.Count != 1006 || catalog.Count(a => a.Status == "Parked") != 7 || catalog.Single(a => a.Registration == "N414DZ").SerialNumber != "1996")
                throw new InvalidOperationException("Aircraft database did not match the verified import.");
            var fleet = new FleetWindow();
            fleet.Show();
            if (fleet.Search("n414dz") != 1 || fleet.Search("no-such-registration") != 0) throw new InvalidOperationException("Fleet search failed.");
            fleet.Search("");
            if (fleet.FilterStatus("Parked") != 7) throw new InvalidOperationException("Parked filter failed.");
            fleet.FilterStatus("All statuses");
            fleet.Search("330-94");
            fleet.UpdateLayout();
            var fleetBitmap = new RenderTargetBitmap((int)fleet.ActualWidth,(int)fleet.ActualHeight,96,96,PixelFormats.Pbgra32);
            fleetBitmap.Render(fleet);
            var fleetEncoder = new PngBitmapEncoder();
            fleetEncoder.Frames.Add(BitmapFrame.Create(fleetBitmap));
            using (var image = File.Create(Path.Combine(outputDirectory,"fleet-preview.png"))) fleetEncoder.Save(image);
            fleet.Close();
            var assignment = new ActiveFlightWindow(null);
            assignment.Show(); assignment.UpdateLayout();
            var assignmentBitmap = new RenderTargetBitmap((int)assignment.ActualWidth, (int)assignment.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            assignmentBitmap.Render(assignment);
            var assignmentEncoder = new PngBitmapEncoder(); assignmentEncoder.Frames.Add(BitmapFrame.Create(assignmentBitmap));
            using (var image = File.Create(Path.Combine(outputDirectory, "active-flight-preview.png"))) assignmentEncoder.Save(image);
            assignment.Close();
            var onTimeTimeline = await TimelineBuilder.BuildAsync(new EmbeddedReplay("on-time-flight.jsonl"));
            if (onTimeTimeline.Snapshots.Count != 12 || onTimeTimeline.FinalPhase != FlightPhase.Complete || onTimeTimeline.Snapshots[^1].EventsFiredCount != 4)
                throw new InvalidOperationException("Desktop timeline build did not match the domain contract.");
            var timelineWindow = new TimelineWindow(onTimeTimeline);
            timelineWindow.Show(); timelineWindow.UpdateLayout();
            var timelineBitmap = new RenderTargetBitmap((int)timelineWindow.ActualWidth, (int)timelineWindow.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            timelineBitmap.Render(timelineWindow);
            var timelineEncoder = new PngBitmapEncoder(); timelineEncoder.Frames.Add(BitmapFrame.Create(timelineBitmap));
            using (var image = File.Create(Path.Combine(outputDirectory, "timeline-preview.png"))) timelineEncoder.Save(image);
            timelineWindow.Close();
            var debriefSession = new FlightSession(Demo.Rotation());
            var debriefEvents = new List<FlightEvent>();
            await foreach (var sample in new EmbeddedReplay("delayed-flight.jsonl").ReadAsync())
                if (debriefSession.Observe(sample) is { } ev) debriefEvents.Add(ev);
            var segments = DebriefSummary.Segments(debriefEvents);
            var debriefLegs = RotationPlanner.Project(debriefSession.Rotation);
            if (segments.Count != 3 || debriefLegs[0].ArrivalDelayMinutes != 30 || debriefLegs[0].DepartureDelayMinutes != 25)
                throw new InvalidOperationException("Desktop debrief summary did not match the domain contract.");
            var debriefWindow = new DebriefWindow(debriefSession.Phase, debriefEvents, segments, debriefLegs);
            debriefWindow.Show(); debriefWindow.UpdateLayout();
            var debriefBitmap = new RenderTargetBitmap((int)debriefWindow.ActualWidth, (int)debriefWindow.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            debriefBitmap.Render(debriefWindow);
            var debriefEncoder = new PngBitmapEncoder(); debriefEncoder.Frames.Add(BitmapFrame.Create(debriefBitmap));
            using (var image = File.Create(Path.Combine(outputDirectory, "debrief-preview.png"))) debriefEncoder.Save(image);
            debriefWindow.Close();
            // No SimConnect session can be simulated here, but the live path now feeds the exact
            // same windows through an incrementally-driven TimelineRecorder instead of the batch
            // TimelineBuilder — prove that recorder produces a correct, renderable result too.
            var liveRecorder = new TimelineRecorder(new PhaseDetector());
            await foreach (var sample in new EmbeddedReplay("delayed-flight.jsonl").ReadAsync())
                liveRecorder.Observe(sample);
            if (liveRecorder.Snapshots.Count != 12 || liveRecorder.Phase != FlightPhase.Complete || liveRecorder.Events.Count != 4)
                throw new InvalidOperationException("Incrementally recorded live timeline did not match the domain contract.");
            var liveTimelineWindow = new TimelineWindow(liveRecorder.ToTimeline());
            liveTimelineWindow.Show(); liveTimelineWindow.UpdateLayout(); liveTimelineWindow.Close();
            var liveDebriefWindow = new DebriefWindow(liveRecorder.Phase, liveRecorder.Events, DebriefSummary.Segments(liveRecorder.Events), []);
            liveDebriefWindow.Show(); liveDebriefWindow.UpdateLayout(); liveDebriefWindow.Close();
            const string simBriefFixture = """{"general":{"icao_airline":"JBU","flight_number":"124","route":"DCT TEST","initial_altitude":"35000"},"origin":{"icao_code":"KLAX"},"destination":{"icao_code":"KJFK"},"times":{"sched_out":"1788450000","est_in":"1788470000"},"aircraft":{"icao_code":"A321","reg":"N123JB"},"fuel":{"plan_ramp":"42000"},"params":{"time_generated":"1788449000","units":"lbs"}}""";
            var imported = SimBriefImporter.Parse(simBriefFixture, "test-pilot", false);
            if (imported.Plan.FlightNumber != "JBU124" || imported.Plan.Origin != "KLAX" || imported.Plan.Destination != "KJFK" || imported.Plan.Registration != "N123JB" || imported.AircraftType != "A321" || imported.CruiseAltitudeFeet != 35000 || imported.RampFuel != 42000 || imported.FuelUnits != "LBS")
                throw new InvalidOperationException("SimBrief briefing fields were not mapped into the active flight.");
            var diagnostics = Path.Combine(outputDirectory, "diagnostic-database-test");
            var testLogs = Path.Combine(diagnostics, "TestLogs");
            var crashes = Path.Combine(diagnostics, "CrashReports");
            Directory.CreateDirectory(testLogs); Directory.CreateDirectory(crashes);
            File.WriteAllText(Path.Combine(testLogs, "Alpha6OPS-test.jsonl"), "{\"kind\":\"session_started\"}\n");
            File.WriteAllText(Path.Combine(crashes, "Alpha6OPS-crash-test.json"), "{}");
            using (var diagnosticDatabase = new LogFileDatabase(diagnostics))
            {
                if (diagnosticDatabase.Refresh(testLogs, crashes) != 2 || diagnosticDatabase.ReadRecent().Count != 2)
                    throw new InvalidOperationException("Diagnostic file database did not index flight and crash logs.");
            }
            var crashReport = CrashReporter.Write("smoke_test", new InvalidOperationException("Expected diagnostic test"), directory: outputDirectory);
            if (crashReport is null || !File.Exists(crashReport)) throw new InvalidOperationException("Crash report was not saved.");
            using (var crashJson = JsonDocument.Parse(File.ReadAllText(crashReport)))
                if (crashJson.RootElement.GetProperty("source").GetString() != "smoke_test" || crashJson.RootElement.GetProperty("exception").GetProperty("type").GetString() != typeof(InvalidOperationException).FullName)
                    throw new InvalidOperationException("Crash report did not preserve the exception details.");
            if (window.Projection.Any(leg => leg.DepartureDelayMinutes != 0)) throw new InvalidOperationException("Reset failed.");
            File.WriteAllText(Path.Combine(outputDirectory, "desktop-smoke.json"), JsonSerializer.Serialize(new
            {
                passed = true, checks = new[] { "WPF startup", "embedded replay", "close-to-tray preserves replay", "downstream delays", "tray restore", "reset", "SQLite fleet counts and N414DZ identity", "case-insensitive fleet search and no-results state", "active-flight assignment window", "timeline scrubber window and snapshot contract", "debrief window and segment/delay contract", "live-tracking recorder feeds the same timeline/debrief windows", "SimBrief JSON mapping", "SQLite diagnostic file index", "crash report serialization", "flight history records a replay run" },
                runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory(), legs
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(outputDirectory, "desktop-smoke-error.txt"), error.ToString());
            Environment.ExitCode = 1;
        }
        finally { window.ExitApplication(outputDirectory); } // redirect the exit-time preferences write away from the real profile, matching CrashReporter's own directory override
    }
}
