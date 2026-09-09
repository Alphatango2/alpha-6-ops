using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Characterizes the real rendering paths without starting a simulator or writing a live journal.
// Reflection is confined to this diagnostic harness so production orchestration stays private.
internal static class RefreshSmokeTest
{
    internal static async Task RunAsync(MainWindow window, string directory)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object? value) => typeof(MainWindow).GetField(name, flags)!.SetValue(window, value);
        T Get<T>(string name) => (T)typeof(MainWindow).GetField(name, flags)!.GetValue(window)!;
        var refresh = typeof(MainWindow).GetMethod("RefreshRotation", flags)!.CreateDelegate<Action>(window);
        var observe = typeof(MainWindow).GetMethod("ObserveLive", flags)!.CreateDelegate<Action<LiveReading>>(window);
        var tracker = typeof(MainWindow).GetMethod("RefreshLiveTracker", flags)!
            .CreateDelegate<Action<string?, DateTimeOffset?, FlightPhase?, string>>(window);
        var resetIdentity = typeof(MainWindow).GetMethod("ResetLiveIdentity", flags)!.CreateDelegate<Action>(window);
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        var states = new List<object>();
        object State(string mode, int index) => new
        {
            mode, index, rows = window.DashboardFlights.Select(f => new { f.Key, f.Leg, f.Aircraft }).ToArray(),
            hero = window.HeroFlightText.Text, window.DashboardIsLive,
            tracker = window.TrackerModeText.Text, phase = window.PhaseText.Text,
            arrival = window.ArrivalValueText.Text, departed = window.DepartedValueText.Text,
            elapsed = window.ElapsedValueText.Text, progress = window.ReplayProgress.Value,
            gate = window.HeroDepartureGateText.Text, provenance = window.HeroDepartureGateText.ToolTip,
            brand = window.AirlineBrandMarkText.Text,
            selected = (window.DashboardFlightsGrid.SelectedItem as DashboardFlightRow)?.Key
        };
        var timings = new List<object>();
        var savedSession = Get<FlightSession>("session");
        var savedPlan = Get<ActiveFlightPlan?>("activePlan");
        try
        {
            foreach (var fixture in EmbeddedReplay.Fixtures)
            {
                resetIdentity();
                var samples = new List<Telemetry>();
                await foreach (var sample in new EmbeddedReplay(fixture).ReadAsync()) samples.Add(sample);
                var replay = new FlightSession(Demo.Rotation());
                Set("session", replay); window.SelectFlightTab("all"); refresh();
                window.DashboardFlightsGrid.SelectedItem = window.DashboardFlights[1];
                var selected = window.DashboardFlights[1].Key;
                for (var i = 0; i < samples.Count; i++)
                {
                    replay.Observe(samples[i]); refresh();
                    Check(window.DashboardFlights.Select(f => f.Leg).SequenceEqual(RotationPlanner.Project(replay.Rotation)), $"{fixture} replay projection sample {i}");
                    Check((window.DashboardFlightsGrid.SelectedItem as DashboardFlightRow)?.Key == selected, $"{fixture} selection sample {i}");
                    states.Add(State(fixture + ":replay", i));
                }
                Check(window.HeroFlightText.Text == "A602", fixture + " completed hero advances");
                window.SelectFlightTab("assigned");
                Check(window.DashboardFlightsGrid.Items.Count == 2, fixture + " assigned excludes completed");
                window.SelectFlightTab("all");

                // Repeated fixture sequences measure synchronous rendering work, not flight speed.
                refresh();
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var clock = Stopwatch.StartNew();
                const int repetitions = 10;
                for (var run = 0; run < repetitions; run++)
                {
                    replay = new FlightSession(Demo.Rotation()); Set("session", replay);
                    foreach (var sample in samples) { replay.Observe(sample); refresh(); }
                }
                clock.Stop();
                timings.Add(new { fixture, mode = "replay-render", samples = samples.Count * repetitions,
                    elapsedMs = clock.Elapsed.TotalMilliseconds, allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
                    dashboardRowsCreated = samples.Count * repetitions * 3, tableReplacements = samples.Count * repetitions });

                var first = Demo.Rotation().Legs[0];
                var plan = new ActiveFlightPlan("JBU124", "N123JB", first.Origin, first.Destination,
                    first.ScheduledOut, first.ScheduledIn, DepartureGate: "52A", ArrivalGate: "12",
                    GateAssignmentSource: "SimBrief dispatch notes", GateAssignmentConfidence: "High", AirlineIcao: "JBU");
                Set("activePlan", plan); Set("liveRotation", null);
                tracker(null, null, null, "Assignment ready");
                Check(window.HeroFlightText.Text == plan.FlightNumber && window.HeroDepartureGateText.Text == "52A", "unarmed assignment keeps gates and identity");
                states.Add(State(fixture + ":unarmed", 0));
                using var cancellation = new CancellationTokenSource(); Set("liveCancellation", cancellation);
                Set("liveRecorder", null); Set("liveLast", null); Set("liveAircraft", null); 
                // Real fixtures contain intentionally sparse time jumps. Fill only this live test's
                // gaps with the preceding state at 10s intervals to respect live continuity rules.
                var liveSamples = new List<Telemetry>();
                foreach (var sample in samples)
                {
                    if (liveSamples.Count > 0)
                    {
                        var previous = liveSamples[^1];
                        for (var at = previous.At.AddSeconds(10); at < sample.At; at = at.AddSeconds(10))
                            liveSamples.Add(previous with { At = at });
                    }
                    liveSamples.Add(sample);
                }
                var expected = new TimelineRecorder(new PhaseDetector());
                var origin = AirportCatalog.Find(plan.Origin)!;
                var destination = AirportCatalog.Find(plan.Destination)!;
                var airborneSamples = liveSamples.Where(s => !s.OnGround).ToArray();
                for (var i = 0; i < liveSamples.Count; i++)
                {
                    var fraction = Math.Clamp((liveSamples[i].At - airborneSamples[0].At).TotalSeconds / (airborneSamples[^1].At - airborneSamples[0].At).TotalSeconds, 0, 1);
                    var position = new GeoPosition(origin.Position.Latitude + (destination.Position.Latitude - origin.Position.Latitude) * fraction,
                        origin.Position.Longitude + (destination.Position.Longitude - origin.Position.Longitude) * fraction);
                    expected.Observe(liveSamples[i]); observe(new("Test aircraft", liveSamples[i], new(position, plan.Registration,
                        new(plan.Origin, plan.Destination), true)));
                    var rotation = Get<AircraftRotation>("liveRotation");
                    Check(window.DashboardFlights.Select(f => f.Leg).SequenceEqual(RotationPlanner.Project(rotation)), $"{fixture} live projection sample {i}");
                    states.Add(State(fixture + ":live", i));
                }
                var recorder = Get<TimelineRecorder>("liveRecorder");
                Check(recorder.Snapshots.SequenceEqual(expected.Snapshots) && recorder.Events.SequenceEqual(expected.Events), fixture + " live records every sample in order");
                Check(window.DashboardIsLive && window.AirlineBrandMarkText.Text == "B6" && window.HeroDepartureGateText.Text == "52A", fixture + " live branding and gates");
                Check(window.ElapsedValueText.Text != "—" && window.ReplayProgress.Value == 100, fixture + " elapsed and completion progress");
                DashboardSmokeTest.Capture(window, Path.Combine(directory, fixture + "-live.png"));
                Set("liveCancellation", null);
            }
            Set("activePlan", null); Set("liveRotation", null);
            resetIdentity();
            tracker("Test aircraft", null, null, "Waiting");
            Check(window.DashboardFlights.Count == 0 && window.HeroFlightText.Text == "NO FLIGHT", "no-assignment empty state");
            states.Add(State("no-assignment", 0));
            var crash = CrashReporter.Write("isolated_default_path", new IOException("Expected diagnostic failure"));
            Check(crash is not null && Path.GetRelativePath(directory, crash).StartsWith("CrashReports" + Path.DirectorySeparatorChar, StringComparison.Ordinal), "default exception reporting stays in diagnostic root");
            File.WriteAllText(Path.Combine(directory, "refresh-states.json"), JsonSerializer.Serialize(states));
            File.WriteAllText(Path.Combine(directory, "refresh-smoke.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks, timings }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            resetIdentity();
            Set("liveCancellation", null); Set("liveRecorder", null); Set("liveRotation", null);
            Set("liveLast", null); Set("liveAircraft", null); 
            Set("activePlan", savedPlan); Set("session", savedSession); window.ResetPreview();
        }
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
