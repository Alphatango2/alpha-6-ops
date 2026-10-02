using System.IO;
using System.Windows.Threading;
using Alpha6Ops.Core;
using Alpha6Ops.Desktop;

internal static partial class TerminalShutdownChecks
{
    private static void FamilyQualificationChecks()
    {
        var adapter = ShutdownIntentAdapters.Create("FenixA320 IAE WF", catalog);
        Check(adapter is not null, "JST771 installed A320 shared cockpit selects a family candidate");
        if (adapter is null) return;
        ShutdownIntentFrame Next(int second, double master = 1, double combustion = 1) =>
            Frame(second, master, master, combustion, combustion) with { Aircraft = "FenixA320 IAE WF" };
        var invalidCoverage = adapter.Observe(Next(0) with { EngineCount = null });
        Check(!invalidCoverage.Qualified, "missing engine coverage cannot establish future shutdown authority");
        var off = Next(1, 0, 0);
        Check(adapter.Observe(off).Status(off.ReceivedUtc, off.SimulatorUtc) == TerminalShutdownStatus.Invalid,
            "valid count arriving with default zeros cannot inherit incomplete running qualification");
        Check(adapter.Observe(Next(2)).Qualified, "fresh two-engine running evidence qualifies A320");
        Check(!adapter.Observe(Next(3) with { Aircraft = "Another aircraft" }).Qualified,
            "wrong identity withdraws qualification and cannot requalify in the same frame");
        Check(!adapter.Observe(Next(4, 0, 0)).Qualified, "returning aircraft must supply fresh running evidence");
        Check(adapter.Observe(Next(5)).Qualified, "returning aircraft requalifies from fresh positive evidence");
        Check(!adapter.Observe(Next(6) with { EngineCount = 4 }).Qualified,
            "incompatible engine coverage withdraws previously granted qualification");
        Check(!adapter.Observe(Next(7, 0, 0)).Qualified, "two-engine OFF cannot inherit four-engine running evidence");
        Check(adapter.Observe(Next(8)).Qualified, "coherent engine coverage can qualify again");
        Check(!adapter.Observe(Next(9, 0, 0) with { ConnectionId = "new-connection" }).Qualified,
            "connection change cannot import old ON qualification");
        Check(adapter.Observe(Next(10) with { ConnectionId = "new-connection" }).Qualified,
            "fresh running post-reconnect grants current-connection qualification");
    }

    // Actual adapter, native optional-packet parser, coordinator, recovery store and
    // SQLite. Only the simulator transport is synthetic. No frozen flight is rewritten.
    private static void FamilyRecoverySequence(string root)
    {
        if (ShutdownIntentAdapters.Create("FenixA320 IAE WF", catalog) is null)
        { Check(false, "JST771 recovery sequence cannot start without A320 family candidate"); return; }
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var directory = Path.Combine(root, "jst771-family-recovery");
        var source = new Source();
        var owner = new FlightSessionCoordinator(directory, true, new Inline(), source);
        var origin = new GeoPosition(-34.945, 138.531);
        var destination = new GeoPosition(-37.673, 144.843);
        var plan = new ActiveFlightPlan("JST771", "VH-VGF", "YPAD", "YMML", Start, Start.AddHours(2),
            AircraftType: "A320", RoutePoints: [new("YPAD", origin.Latitude, origin.Longitude), new("YMML", destination.Latitude, destination.Longitude)]);
        owner.AcceptAssignment(plan);
        var sensors = new SimConnectSource.TerminalSensors(catalog);
        var connection = owner.ConnectSimulatorAsync();
        void Emit(int second, double master1 = 1, double master2 = 1, double combustion1 = 1, double combustion2 = 1, bool sendMasters = true)
        {
            var ground = second < 6 || second >= 7200;
            var parked = second == 0 || second >= 7210;
            var fraction = Math.Clamp((second - 10) / 7190d, 0, 1);
            var position = new GeoPosition(origin.Latitude + (destination.Latitude - origin.Latitude) * fraction,
                origin.Longitude + (destination.Longitude - origin.Longitude) * fraction);
            var now = DateTimeOffset.UtcNow;
            var telemetry = new Telemetry(Start.AddSeconds(second), ground, parked ? 0 : ground ? 3 : 350, parked,
                combustion1 == 1 || combustion2 == 1, LatitudeDegrees: position.Latitude, LongitudeDegrees: position.Longitude,
                EngineDataValid: false);
            if (sendMasters)
            {
                var packet = System.Runtime.InteropServices.Marshal.AllocHGlobal(48);
                try
                {
                    foreach (var (id, value) in new[] { (301, master1), (302, master2) })
                    {
                        System.Runtime.InteropServices.Marshal.Copy(new byte[48], 0, packet, 48);
                        System.Runtime.InteropServices.Marshal.WriteInt32(packet, 12, id);
                        System.Runtime.InteropServices.Marshal.WriteInt32(packet, 20, id);
                        System.Runtime.InteropServices.Marshal.WriteInt32(packet, 36, 1);
                        System.Runtime.InteropServices.Marshal.Copy(new[] { value }, 0, IntPtr.Add(packet, 40), 1);
                        sensors.Dispatch(8, packet, 48);
                    }
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(packet); }
                now = DateTimeOffset.UtcNow;
            }
            telemetry = telemetry with { TerminalShutdown = sensors.Observe("FenixA320 IAE WF", telemetry, now, 2,
                [combustion1, combustion2, 0, 0], [0, 0, 0, 0]) };
            source.Received!(new("FenixA320 IAE WF", telemetry, new(position, "VH-VGF", new("YPAD", "YMML"), true), ReceivedUtc: now));
        }
        for (var i = 0; i <= 10; i++) Emit(i);
        for (var i = 120; i <= 3600; i += 120) Emit(i);
        var originalOut = owner.Snapshot.ActualOut;
        var originalOff = owner.Snapshot.PhaseEvents.Single(e => e.Phase == FlightPhase.Airborne).At;
        var flightId = owner.History!.ReadRecentFlights(10).Single().Id;
        var prefix = owner.Snapshot.Events.ToArray();
        Check(owner.Snapshot.Telemetry?.TerminalShutdown?.Qualified == true, "family candidate qualified before cruise disconnect");
        foreach (var restart in new[] { false, true })
        {
            var second = restart ? 3720 : 3601;
            if (restart) owner.Dispose(); else owner.DisconnectSimulator();
            Pump(connection);
            if (restart) owner = new(directory, true, new Inline(), source);
            sensors = new(catalog);
            connection = owner.ConnectSimulatorAsync();
            Emit(second, sendMasters: false);
            Check(owner.Snapshot.Telemetry?.TerminalShutdown?.Qualified == false,
                "no cached master authority after " + (restart ? "application reconstruction" : "reconnect"));
            Emit(second + 1, 0, 0, 0, 0);
            Check(owner.Snapshot.Telemetry?.TerminalShutdown?.Qualified == false && !owner.Snapshot.Complete,
                "fresh zero defaults do not establish capability after interruption");
            Emit(second + 2);
            Check(owner.Snapshot.Telemetry?.TerminalShutdown?.Qualified == true, "fresh cruise running signals requalify family adapter");
            Check(owner.Snapshot.Assignment?.FlightNumber == "JST771" && owner.Snapshot.ActualOut == originalOut
                && owner.Snapshot.PhaseEvents.Single(e => e.Phase == FlightPhase.Airborne).At == originalOff
                && !owner.Snapshot.JoinedInProgress && owner.Snapshot.Events.Take(prefix.Length).SequenceEqual(prefix),
                "assignment, OUT/OFF and event prefix survive interruption without reacceptance");
        }
        for (var i = 3840; i < 7200; i += 120) Emit(i);
        for (var i = 7200; i < 7225; i++) Emit(i);
        Check(owner.Snapshot.Phase == FlightPhase.TaxiIn && !owner.Snapshot.Complete && !owner.Snapshot.PirepEligible,
            "destination brake and 15-second both-running hold cannot complete");
        for (var i = 7225; i < 7245; i++) Emit(i, 0, 1, 0, 1);
        Check(!owner.Snapshot.Complete && !owner.Snapshot.PirepEligible,
            "20-second single-engine taxi shutdown cannot complete");
        for (var i = 7245; i <= 7247; i++) Emit(i, 0, 0, 0, 0);
        Check(!owner.Snapshot.Complete, "all OFF requires unchanged three-second confirmation");
        Emit(7248, 0, 0, 0, 0);
        Check(owner.Snapshot.Complete && owner.Snapshot.PirepEligible && owner.Snapshot.ActualIn == Start.AddSeconds(7245),
            "recovered family flight automatically records first valid all-OFF IN after three seconds");
        for (var i = 7249; i <= 7255; i++) Emit(i, 0, 0, 0, 0);
        Check(new[] { FlightPhase.TaxiOut, FlightPhase.Airborne, FlightPhase.TaxiIn, FlightPhase.Complete }
            .All(phase => owner.Snapshot.PhaseEvents.Count(e => e.Phase == phase) == 1), "family recovery records each OOOI exactly once");
        Check(owner.Snapshot.Telemetry?.EngineDataValid == false, "intent completion does not relabel mechanical spool-down validity");
        Check(owner.SubmitPirep() && !owner.SubmitPirep(), "family automatic completion submits exactly one PIREP");
        owner.DisconnectSimulator(); Pump(connection); owner.Dispose();
        using var cold = new FlightSessionCoordinator(directory, true, new Inline(), new Source());
        var rows = cold.History!.ReadRecentFlights(10);
        Check(cold.Snapshot.Assignment is null && !cold.Snapshot.PirepEligible && rows.Count == 1
            && rows[0].Id == flightId && rows[0].FinalPhase == "Complete"
            && cold.History.ReadFlightEvents(flightId).Count(e => e.Kind == "pirep_submitted") == 1,
            "cold reconstruction preserves one original completed flight/PIREP without resurrected assignment");
    }
}
