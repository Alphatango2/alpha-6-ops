using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class FlightLabSmokeTest
{
    internal static async Task RunAsync(MainWindow window, string directory, Action<bool,string> check)
    {
        var pipeName=FlightLabProtocol.PipeName+"."+Guid.NewGuid().ToString("N");
        var at=new DateTimeOffset(2026,9,8,15,0,0,TimeSpan.Zero);
        var frame=new FlightLabFrame(FlightLabProtocol.SchemaVersion,"Alpha 6 Test A321",at,true,18,false,true,false,false,"TAXI OUT","SMOKE_TEST");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var server=Task.Run(async()=>
        {
            using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(timeout.Token);
            using var writer=new System.IO.StreamWriter(pipe){AutoFlush=true};
            await writer.WriteLineAsync(JsonSerializer.Serialize(frame));
            await Task.Delay(250,timeout.Token);
        },timeout.Token);
        LiveReading? received=null;var statuses=new List<string>();
        try{await FlightLabSource.RunAsync(statuses.Add,value=>{received=value;timeout.Cancel();},timeout.Token,pipeName);}
        catch(OperationCanceledException)when(received is not null){}
        try{await server;}catch(OperationCanceledException)when(received is not null){}
        check(statuses.Exists(s=>s.Contains("Connected to Alpha 6 Flight Lab",StringComparison.Ordinal)),"Flight Lab named-pipe source reports a connection");
        check(received is {Source:"FLIGHT LAB",Aircraft:"Alpha 6 Test A321",ScenarioEvent:"SMOKE_TEST"}&&received.Telemetry.OnGround&&received.Telemetry.GroundSpeedKnots==18,"Flight Lab telemetry enters the live-reading boundary unchanged");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object? value) => typeof(MainWindow).GetField(name, flags)!.SetValue(window, value);
        T Get<T>(string name) => (T)typeof(MainWindow).GetField(name, flags)!.GetValue(window)!;
        var observe = typeof(MainWindow).GetMethod("ObserveLive", flags)!.CreateDelegate<Action<LiveReading>>(window);
        var reset = typeof(MainWindow).GetMethod("ResetObservedSession", flags)!.CreateDelegate<Action<string>>(window);
        var stale = typeof(MainWindow).GetMethod("MarkLiveUnavailable", flags)!.CreateDelegate<Action>(window);
        var savedPlan = Get<ActiveFlightPlan?>("activePlan");
        using var session = new CancellationTokenSource();
        try
        {
            Set("liveCancellation", session);
            window.ConnectButton.IsEnabled = window.ConnectFlightLabButton.IsEnabled = false;
            reset("Flight Lab integration test");
            Set("activePlan", new ActiveFlightPlan("JBU124", "N123JB", "KLAX", "KJFK", at, at.AddHours(5)));
            observe(received!);
            check(Get<TimelineRecorder?>("liveRecorder")?.Phase == FlightPhase.TaxiOut,
                "Lab data without geographic context still arms phase tracking");
            check(window.HeroFlightText.Text == "LAB FLIGHT" && window.HeroAircraftTypeText.Text.Contains("FLIGHT LAB") &&
                window.TrackerModeText.Text.Contains("FLIGHT LAB") && window.LiveText.Text.StartsWith("FLIGHT LAB"),
                "Lab hero, tracker and live reading never claim to be real MSFS");
            check(window.DashboardFlights.Count == 0 && Get<AircraftRotation?>("liveRotation") is null && window.DestinationCodeText.Text == "—",
                "Lab telemetry cannot attach or complete the saved real flight assignment");
            window.SetConnectionBadge("FLIGHT LAB CONNECTION LOST — RETRYING", "#FFCA45", "#433817");
            check(window.ConnectionSourceText.Text == "FLIGHT LAB" && window.ConnectionBadgeText.Text == "LAB RECONNECTING",
                "Lab reconnect remains visibly distinct from MSFS");
            stale();
            check(window.HeroStatusText.Text == "LAST OBSERVED", "Frozen Lab telemetry is marked stale");
            void Sample(int seconds, bool ground, double speed, bool brake = false, bool engines = true) =>
                observe(FlightLabSource.ParseFrame(frame with { SimulatorUtc = at.AddSeconds(seconds), OnGround = ground,
                    GroundSpeedKnots = speed, ParkingBrake = brake, EnginesRunning = engines }));
            Sample(1, false, 175); Sample(4, false, 175);
            check(Get<TimelineRecorder>("liveRecorder").Phase == FlightPhase.Airborne && window.HeroStatusText.Text == "AIRBORNE",
                "Resumed Lab frames recover from stale data and record takeoff");
            Sample(5, true, 30); Sample(8, true, 30);
            Sample(9, true, 0, true, false); Sample(12, true, 0, true, false);
            check(Get<TimelineRecorder>("liveRecorder").Phase == FlightPhase.Complete && window.LiveDebriefButton.IsEnabled,
                "Lab landing and shutdown produce a complete timeline and debrief");
            window.SetConnectionBadge("FLIGHT LAB CONNECTED", "#65E697", "#173D27");
            check(window.ConnectionActionText.Text == "VIEW CONTROLS", "Active Lab session guards the MSFS quick-launch action");
            DashboardSmokeTest.Capture(window, Path.Combine(directory, "flight-lab-integrated.png"));
            Sample(600, true, 0, true, false);
            check(Get<TimelineRecorder>("liveRecorder").Phase == FlightPhase.AtGate && Get<TimelineRecorder>("liveRecorder").Events.Count == 0,
                "Lab clock jump starts a fresh observed session instead of invalidating tracking permanently");
        }
        finally
        {
            reset("Flight Lab test complete"); Set("liveCancellation", null); Set("activePlan", savedPlan);
            Set("liveSource", "MSFS 2024"); Set("lastScenarioEvent", null);
            window.ConnectButton.IsEnabled = window.ConnectFlightLabButton.IsEnabled = true;
            window.SetConnectionBadge("SIMULATOR DISCONNECTED", "#A9AD9F", "#30342C");
            window.ResetPreview();
        }
    }
}
