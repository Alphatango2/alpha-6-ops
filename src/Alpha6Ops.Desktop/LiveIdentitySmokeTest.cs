using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

internal static class LiveIdentitySmokeTest
{
    internal static void Run(MainWindow window, string directory)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object? value) => typeof(MainWindow).GetField(name, flags)!.SetValue(window, value);
        T Get<T>(string name) => (T)typeof(MainWindow).GetField(name, flags)!.GetValue(window)!;
        var observe = typeof(MainWindow).GetMethod("ObserveLive", flags)!.CreateDelegate<Action<LiveReading>>(window);
        var reset = typeof(MainWindow).GetMethod("ResetObservedSession", flags)!.CreateDelegate<Action<string>>(window);
        var stale = typeof(MainWindow).GetMethod("MarkLiveUnavailable", flags)!.CreateDelegate<Action>(window);
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        var originalPlan = Get<ActiveFlightPlan?>("activePlan");
        var at = DateTimeOffset.Parse("2026-09-08T22:00:00Z");
        var sydney = AirportCatalog.Find("YSSY")!;
        var bankstown = AirportCatalog.Find("YSBK")!;
        var lax = AirportCatalog.Find("KLAX")!;
        using var cancellation = new CancellationTokenSource();
        try
        {
            Set("liveCancellation", cancellation); reset("diagnostic start");
            Set("activePlan", new ActiveFlightPlan("JBU124", "G-FBIG", "KLAX", "KJFK", at.AddDays(-5), at.AddDays(-5).AddHours(5)));
            LiveReading Sydney(int seconds, bool ground = true, double speed = 0, bool brake = true, bool engines = true, SimulatorRoute? route = null) =>
                new("AT802 Aerial Application Sprayer", new(at.AddSeconds(seconds), ground, speed, brake, engines),
                    new(sydney.Position, "VH-TEST", route, true, new(sydney, 0), route is null ? "No simulator flight plan" : "MSFS active flight plan"));
            observe(Sydney(0));
            Check(window.HeroFlightText.Text == "FREE FLIGHT" && window.OriginCodeText.Text == "YSSY", "Sydney flight shows free flight and actual airport vicinity");
            Check(window.DestinationCodeText.Text == "—" && window.HeroArrivalText.Text == "—", "unknown destination and ETA remain unknown");
            Check(!window.AircraftText.Text.Contains("G-FBIG") && window.AircraftText.Text.Contains("VH-TEST"), "stale briefing registration never replaces live aircraft");
            Check(window.DashboardFlights.Count == 0 && Get<AircraftRotation?>("liveRotation") is null && window.ReplayProgress.Value == 0, "unrelated briefing produces no scheduled rows, delays or invented progress");
            Check(window.HeroTimingText.Text.Contains("review"), "assignment conflict is visible on dashboard");
            DashboardSmokeTest.Capture(window, Path.Combine(directory, "sydney-free-flight.png"));
            stale();
            Check(window.HeroStatusText.Text == "LAST OBSERVED" && window.TrackerModeText.Text.Contains("UNAVAILABLE"), "disconnected data is clearly marked last observed");
            observe(Sydney(1, route: new("YSSY", "YSBK")));
            Check(window.HeroFlightText.Text == "MSFS FLIGHT" && window.DestinationCodeText.Text == "YSBK", "simulator plan supplies route independently of saved briefing");
            Check(window.DashboardFlights.Count == 0 && window.HeroArrivalText.Text == "—", "simulator plan without schedule does not invent scheduled time");
            reset("diagnostic airborne join"); Set("activePlan", null);
            observe(Sydney(2, false, 120, false));
            Check(Get<TimelineRecorder>("liveRecorder").Phase == FlightPhase.Airborne && window.HeroDepartureText.Text == "—", "joining an airborne free flight records phase without fabricating departure");
            observe(Sydney(3) with { Aircraft = "Cessna 172" });
            Check(Get<DateTimeOffset?>("observedSessionStart") == at.AddSeconds(3) && Get<TimelineRecorder>("liveRecorder").Events.Count == 0, "aircraft swap resets observed session and milestones");
            observe(Sydney(4) with { Aircraft = "Cessna 172", Evidence = new(lax.Position, "VH-TEST", SimulationRunning: true, Nearby: new(lax, 0)) });
            Check(window.OriginCodeText.Text == "KLAX" && Get<DateTimeOffset?>("observedSessionStart") == at.AddSeconds(4), "teleport updates vicinity and resets session");
            reset("diagnostic arrival mismatch");
            Set("activePlan", new ActiveFlightPlan("LOCAL1", "VH-TEST", "YSSY", "YMML", at, at.AddHours(1)));
            observe(Sydney(0)); observe(Sydney(1, speed: 5, brake: false)); observe(Sydney(4, speed: 5, brake: false));
            observe(Sydney(5, false, 120, false)); observe(Sydney(8, false, 120, false));
            observe(Sydney(9, speed: 30, brake: false)); observe(Sydney(12, speed: 30, brake: false));
            observe(Sydney(13, engines: false)); observe(Sydney(16, engines: false));
            Check(window.HeroStatusText.Text == "AIRPORT REVIEW" && Get<AircraftRotation>("liveRotation").Legs[0].ActualIn is null,
                "landing away from assigned destination flags review and does not complete wrong route");
            DashboardSmokeTest.Capture(window, Path.Combine(directory, "arrival-airport-review.png"));
            Check(AirportCatalog.Nearest(bankstown.Position)?.Airport.Ident == "YSBK", "global reference distinguishes Bankstown from Sydney international");
            Check(AirportCatalog.Nearest(new(double.NaN, 0)) is null, "invalid geographic data has no airport result");
            using var xml = new MemoryStream(Encoding.UTF8.GetBytes("<SimBase.Document><FlightPlan.FlightPlan><DepartureID>YSSY</DepartureID><DestinationID>YSSY</DestinationID></FlightPlan.FlightPlan></SimBase.Document>"));
            Check(SimulatorFlightPlan.Parse(xml).Route == new SimulatorRoute("YSSY", "YSSY"), "simulator round-trip PLN is read without schedule invention");
            using var incomplete = new MemoryStream(Encoding.UTF8.GetBytes("<SimBase.Document><FlightPlan.FlightPlan><DepartureID>YSSY</DepartureID></FlightPlan.FlightPlan></SimBase.Document>"));
            Check(SimulatorFlightPlan.Parse(incomplete).Route is null, "incomplete PLN does not produce a guessed destination");
            using var malicious = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE test [<!ENTITY x SYSTEM 'file:///C:/Windows/win.ini'>]><test>&x;</test>"));
            var blocked = false; try { SimulatorFlightPlan.Parse(malicious); } catch (XmlException) { blocked = true; }
            Check(blocked, "PLN cannot expand external XML entities");
            Check(SimulatorFlightPlan.Read(@"\\server\plan.pln").Route is null, "simulator plan cannot initiate network file access");
            Check(SimulatorFlightPlan.Read(Path.Combine(directory,"missing.pln")).Route is null, "missing flight-plan file is recoverable");
            window.SimulatorReadyAction = "LAUNCH & CONNECT";
            window.ConnectButton.IsEnabled = true;
            Check(window.ConnectionActionText.Text == "LAUNCH & CONNECT", "connection card offers simulator launch when closed");
            window.ConnectButton.IsEnabled = false;
            Check(window.ConnectionActionText.Text == "VIEW CONTROLS", "launch/connection in progress cannot trigger another launch");
            File.WriteAllText(Path.Combine(directory, "live-identity-smoke.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            reset("diagnostic complete"); Set("liveCancellation", null); Set("activePlan", originalPlan);
            window.ConnectButton.IsEnabled = true; window.ResetPreview();
        }
    }
}
