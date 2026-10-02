using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Alpha6Ops.Core;
using Alpha6Ops.Desktop;

internal static partial class TerminalShutdownChecks
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-28T21:15:00Z");
    private const string NativeCaptureTitle = "FenixA321 IAE WF SC";
    private static FenixShutdownCapabilities catalog = FenixShutdownCapabilities.Empty;
    private static readonly List<object> Results = [];
    private static int failures;
    private static void Check(bool condition, string name) { Results.Add(new { name, passed = condition }); if (!condition) failures++; Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}"); }
    private static ShutdownIntentFrame Frame(int seconds, double master1 = 1, double master2 = 1, double combustion1 = 1, double combustion2 = 1)
    {
        var now = Start.AddSeconds(seconds);
        return new("FenixA321 IAE WF SC", 2, now, now, "native-1", new Dictionary<string, ShutdownSignal> {
            ["L:S_ENG_MASTER_1"] = new(master1,now), ["L:S_ENG_MASTER_2"] = new(master2,now),
            ["GENERAL ENG COMBUSTION:1"] = new(combustion1,now), ["GENERAL ENG COMBUSTION:2"] = new(combustion2,now) });
    }
    internal static int Run(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        try
        {
            FenixShutdownCapabilityChecks.Run(args[1], Check);
            catalog = FenixShutdownCapabilities.Read(FenixShutdownCapabilityChecks.CreateCatalog(Path.Combine(args[1], "family-package")));
            var adapter = ShutdownIntentAdapters.Create("FenixA321 IAE WF SC", catalog);
            Check(adapter is not null, "validated Fenix variant/version selects enhanced adapter");
            if (adapter is not null)
            {
                TerminalShutdownStatus Observe(ShutdownIntentFrame frame) => adapter.Observe(frame).Status(frame.ReceivedUtc, frame.SimulatorUtc);
                Check(Observe(Frame(0,0,0,0,0)) == TerminalShutdownStatus.Invalid, "fresh zero defaults are not qualified intent");
                Check(Observe(Frame(1)) == TerminalShutdownStatus.Operating, "positive ON and combustion qualify both channels");
                Check(Observe(Frame(2,1,0,1,0)) == TerminalShutdownStatus.Operating, "single-engine shutdown cannot complete");
                Check(Observe(Frame(3,0,0,1,0)) == TerminalShutdownStatus.Invalid, "master cutoff with combustion still present is contradictory");
                Check(Observe(Frame(4,0,0,0,0)) == TerminalShutdownStatus.Confirmed, "all verified cutoffs and combustion absent confirm intent");
                Check(Observe(Frame(5,1,1,0,0)) == TerminalShutdownStatus.Operating, "failure or fuel starvation with masters ON is not intent");
                Check(Observe(Frame(6,0,0,1,0)) == TerminalShutdownStatus.Invalid, "relight revokes terminal evidence");
                Check(Observe(Frame(7,double.NaN,0,0,0)) == TerminalShutdownStatus.Invalid, "nonfinite master invalidates evidence");
                Check(Observe(Frame(8,.5,0,0,0)) == TerminalShutdownStatus.Invalid, "intermediate master cannot be coerced to OFF");
                Check(Observe(Frame(9,0,0,0,0) with { EngineCount = 4 }) == TerminalShutdownStatus.Invalid, "two-channel adapter cannot cover four engines");
                var stale=Frame(10,0,0,0,0);var signals=new Dictionary<string,ShutdownSignal>(stale.Signals);signals["L:S_ENG_MASTER_1"]=new(0,Start);
                Check(Observe(stale with {Signals=signals})==TerminalShutdownStatus.Invalid,"stale channel cannot confirm");
                signals.Remove("L:S_ENG_MASTER_2");
                Check(Observe(stale with {Signals=signals})==TerminalShutdownStatus.Invalid,"missing channel cannot confirm");
                var fresh=ShutdownIntentAdapters.Create("FenixA321 IAE WF SC",catalog)!;var off=Frame(11,0,0,0,0);
                Check(fresh.Observe(off).Status(off.ReceivedUtc,off.SimulatorUtc)==TerminalShutdownStatus.Invalid,"new connection cannot inherit qualification from earlier adapter");
            }
            Check(ShutdownIntentAdapters.Create("Airbus A220-300",catalog) is null,"unvalidated aircraft remains limited");
            Check(ShutdownIntentAdapters.Create("FenixA321 IAE WF SC",FenixShutdownCapabilities.Empty) is null,"unrecognized installed package remains limited");
            Check(ShutdownIntentAdapters.Create("FenixA321 IAE WF SC",null) is null,"missing version cannot promote enhanced authority");
            FamilyQualificationChecks();
            FamilyRecoverySequence(args[1]);
            NativeCapture(args.Length > 2 ? args[2] : "docs/flight-tracking/evidence/7.24c-fenix-shutdown-native-20260928/raw/signals.jsonl");
            NativeBoundary(args[1]);
            CoordinatorChecks(args[1]);
        }
        catch(Exception error) { Check(false, "HARNESS: " + error); }
        File.WriteAllText(Path.Combine(args[1],"summary.json"),JsonSerializer.Serialize(new {executed=Results.Count,passed=Results.Count-failures,failed=failures,checks=Results},new JsonSerializerOptions{WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals}));
        return failures==0?0:1;
    }

    private static void NativeCapture(string path)
    {
        var adapter=ShutdownIntentAdapters.Create(NativeCaptureTitle,catalog)!;
        var values=new Dictionary<string,ShutdownSignal>();
        double? confirmed=null; var count=0; var premature=0;
        foreach(var line in File.ReadLines(path))
        {
            using var doc=JsonDocument.Parse(line);var row=doc.RootElement;
            if(row.GetProperty("kind").GetString()!="signal")continue;
            var name=row.GetProperty("name").GetString()!;var utc=row.GetProperty("utc").GetDateTimeOffset();var value=row.GetProperty("value");
            if(value.ValueKind==JsonValueKind.Number)values[name]=new(value.GetDouble(),utc,row.GetProperty("finite").GetBoolean());
            // Last field in each preserved native reply group; no future-value lookahead.
            if(name!="PLANE LONGITUDE")continue;
            count++;var elapsed=row.GetProperty("elapsedSeconds").GetDouble();
            var at=DateTimeOffset.MinValue.AddSeconds(values["ABSOLUTE TIME"].Value);
            var frame=new ShutdownIntentFrame(NativeCaptureTitle,values["NUMBER OF ENGINES"].Value,at,utc,"capture-native",values);
            if(adapter.Observe(frame).Status(utc,at)==TerminalShutdownStatus.Confirmed) {confirmed??=elapsed;if(elapsed<39)premature++;}
        }
        Check(count==180&&confirmed is >39 and <40&&premature==0,"native capture: all 180 groups replay, intent at 39.2s, never during single-engine shutdown");
    }

    private sealed class Inline : IFlightRuntimeDispatcher { public void VerifyAccess() { } public void Post(Action action)=>action(); }
    private static void NativeBoundary(string root)
    {
        var sensors=new SimConnectSource.TerminalSensors(catalog);var packet=Marshal.AllocHGlobal(48);
        try
        {
            void Send(int id,double value,int count=1)
            {
                Marshal.Copy(new byte[48],0,packet,48);Marshal.WriteInt32(packet,12,id);Marshal.WriteInt32(packet,20,id);Marshal.WriteInt32(packet,36,count);Marshal.Copy(new[]{value},0,IntPtr.Add(packet,40),1);
                Check(sensors.Dispatch(8,packet,48),"optional native packet routed independently "+id+"/"+value+"/"+count);
            }
            TerminalShutdownStatus Read(double[] combustion)
            {
                var now=DateTimeOffset.UtcNow;var sample=new Telemetry(now,true,0,true,false);
                return sensors.Observe(NativeCaptureTitle,sample,now,2,combustion,[0,0]).Status(now,now);
            }
            Send(301,1);Send(302,1);Check(Read([1,1])==TerminalShutdownStatus.Operating,"native boundary qualifies positive master signals");
            Send(301,0);Send(302,0);Check(Read([0,0])==TerminalShutdownStatus.Confirmed,"native boundary preserves independently sampled cutoffs");
            Send(301,0,2);Check(Read([0,0])==TerminalShutdownStatus.Invalid,"malformed optional payload withdraws previous OFF");
            Send(301,double.NaN);Check(Read([0,0])==TerminalShutdownStatus.Invalid,"native NaN cannot become OFF");
            Marshal.WriteInt32(packet,12,1);Check(!sensors.Dispatch(8,packet,48),"optional parser does not steal primary telemetry");
            var now=DateTimeOffset.UtcNow;var sample=new Telemetry(now,true,0,true,true);
            sensors.Observe("Previous aircraft",sample,now,2,[1,1],[0,0]);
            Send(301,1);Send(302,1);
            Check(Read([1,1])==TerminalShutdownStatus.Invalid,"aircraft switch cannot qualify from previous aircraft cached masters");
            Send(301,0);Send(302,0);
            Check(Read([0,0])==TerminalShutdownStatus.Invalid,"new aircraft default zeros remain unqualified after switch");
            Send(301,1);Send(302,1);Check(Read([1,1])==TerminalShutdownStatus.Operating,"new aircraft fresh ON observations qualify independently");
        }
        finally {Marshal.FreeHGlobal(packet);}
        var limited=new SimConnectSource.TerminalSensors(null);
        var received=DateTimeOffset.UtcNow;
        var unknown=new Telemetry(received,true,0,true,false,EngineDataValid:false,EngineReadings:[]);
        TerminalShutdownStatus Limited(double[] combustion,double?[] starters) => limited.Observe("Unsupported",unknown,received,null,combustion,starters).Status(received,received);
        Check(Limited([1,1,0,0],[])==TerminalShutdownStatus.Operating,"missing engine count cannot hide fresh primary combustion");
        Check(Limited([0,0,0,0],[1])==TerminalShutdownStatus.Operating,"missing engine count cannot hide positive starter evidence");
        Check(Limited([double.NaN,0,0,0],[])==TerminalShutdownStatus.Invalid,"invalid primary combustion cannot enable manual fallback");
        Check(Limited([],[])==TerminalShutdownStatus.Invalid,"missing primary combustion cannot enable manual fallback");
        Check(Limited([0,0,0,0],[])==TerminalShutdownStatus.Unavailable,"valid absent combustion permits guarded manual fallback only");
    }
    private sealed class Source : ISimulatorConnectionSource
    {
        internal Action<LiveReading>? Received;
        public bool IsRunning()=>true;
        public void Launch()=>throw new InvalidOperationException("No native simulator in terminal checks");
        public Task RunAsync(Action<string> opened,Action<LiveReading> received,CancellationToken token) {Received=received;opened("Synthetic terminal checks");return Task.Delay(Timeout.Infinite,token);}
    }
    private static void Pump(Task task)
    {
        if(!task.IsCompleted){var frame=new DispatcherFrame();_ = task.ContinueWith(_=>Dispatcher.CurrentDispatcher.BeginInvoke(()=>frame.Continue=false),TaskScheduler.FromCurrentSynchronizationContext());Dispatcher.PushFrame(frame);}
        task.GetAwaiter().GetResult();
    }
    private static void CoordinatorChecks(string root)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        foreach(var scenario in new[]{"enhanced","manual","wrong-destination","pause","slew","brake","movement","relight","disconnect","restart","duplicate","stale","missing","go-around"})
        {
            var directory=Path.Combine(root,scenario);var source=new Source();
            var owner=new FlightSessionCoordinator(directory,true,new Inline(),source);
            var plan=new ActiveFlightPlan("TERM724","VH-TEST","YSSY","YMHB",Start,Start.AddHours(2),AircraftType:"A321",RoutePoints:[new("YSSY",-33.946,151.177111),new("YMHB",-42.836111,147.510278)]);
            owner.AcceptAssignment(plan);var connection=owner.ConnectSimulatorAsync();
            void Emit(int second)
            {
                var ground=second<6||second>=7200;var parked=second==0||second>=7210;var engines=second<7210;
                var destination=scenario=="wrong-destination"?new GeoPosition(-35.3069,149.195):new GeoPosition(-42.836111,147.510278);
                var fraction=Math.Clamp((second-10)/7190d,0,1);
                var point=new GeoPosition(-33.946+(destination.Latitude+33.946)*fraction,151.177111+(destination.Longitude-151.177111)*fraction);
                var sample=new Telemetry(Start.AddSeconds(second),ground,parked?0:ground?3:350,parked,engines,LatitudeDegrees:point.Latitude,LongitudeDegrees:point.Longitude,EngineDataValid:false);
                if(scenario=="go-around"&&second>=7212)sample=sample with{OnGround=false,GroundSpeedKnots=160,ParkingBrake=false};
                var now=DateTimeOffset.UtcNow;
                if(scenario is not ("manual" or "wrong-destination"))sample=sample with{TerminalShutdown=new(ShutdownIntentSupport.Enhanced,"validated test adapter","source-1",2,[new(1,!engines,engines,now),new(2,!engines,engines,now)],sample.At,now,true,true)};
                if(second==7212)sample=scenario switch {
                    "pause"=>sample with{Paused=true},"slew"=>sample with{Slewing=true},"brake"=>sample with{ParkingBrake=false},"movement"=>sample with{GroundSpeedKnots=1},
                    "relight"=>sample with{TerminalShutdown=sample.TerminalShutdown! with{Engines=[new(1,false,true,now),new(2,true,false,now)]}},
                    "missing"=>sample with{TerminalShutdown=sample.TerminalShutdown! with{Engines=[new(1,true,false,now)]}},
                    "stale"=>sample with{TerminalShutdown=sample.TerminalShutdown! with{ReceivedUtc=now.AddSeconds(-4)}},_=>sample};
                source.Received!(new("Terminal fixture",sample,new(point,"VH-TEST",new("YSSY","YMHB"),true),ReceivedUtc:now));
            }
            for(int i=0;i<=10;i++)Emit(i);
            for(int i=120;i<7200;i+=120)Emit(i);
            for(int i=7200;i<=7220;i++)
            {
                Emit(i);
                if(i==7212)
                {
                    if(scenario=="duplicate")Emit(i);
                    if(scenario is "disconnect" or "restart")
                    {
                        if(scenario=="restart")owner.Dispose();else owner.DisconnectSimulator();
                        Pump(connection);
                        if(scenario=="restart")owner=new(directory,true,new Inline(),source);
                        Check(!owner.Snapshot.Complete,"interrupted candidate is not restored complete: "+scenario);
                        connection=owner.ConnectSimulatorAsync();
                    }
                }
                if(i==7213&&scenario is not ("enhanced" or "manual" or "wrong-destination"))Check(!owner.Snapshot.Complete,"interruption resets three-second phase confirmation: "+scenario);
            }
            if(scenario=="go-around")
            {
                Check(owner.Snapshot.Phase==FlightPhase.Airborne,"go-around still confirms TaxiIn to Airborne after ON");
                Check(!owner.Snapshot.Complete&&!owner.Snapshot.CanConfirmBlockIn,"go-around cancels pending terminal completion");
            }
            else if(scenario is not ("manual" or "wrong-destination"))
            {
                Check(owner.Snapshot.Complete&&owner.Snapshot.ActualIn==Start.AddSeconds(scenario=="enhanced"?7210:7213),"coordinator: stable evidence controls IN timestamp: "+scenario);
                Check(owner.Snapshot.Telemetry?.EngineDataValid==false,"completion does not forge mechanical engine validity");
                Check(owner.Snapshot.PhaseEvents.Count(e=>e.Phase==FlightPhase.Complete)==1,"coordinator produces exactly one IN");
                var history=owner.History!;var rows=history.ReadRecentFlights(100);
                Check(rows.Count==1&&rows[0].FinalPhase=="Complete","coordinator persists exactly one completed history row");
                Check(owner.SubmitPirep()&&!owner.SubmitPirep(),"PIREP submits exactly once");
                Check(rows.Count==1&&history.ReadFlightEvents(rows[0].Id).Count(e=>e.Kind=="pirep_submitted")==1,"exact history identity owns one durable PIREP");
            }
            else
            {
                Check(!owner.Snapshot.Complete,"unsupported mechanical absence does not auto-complete: "+scenario);
                Check(owner.Snapshot.CanConfirmBlockIn==(scenario=="manual"),"manual eligibility agrees with destination: "+scenario);
                owner.ConfirmBlockIn();
                for(int i=7221;i<=7225;i++)Emit(i);
                Check(owner.Snapshot.Complete==(scenario=="manual"),"direct manual command cannot bypass shared safety predicate: "+scenario);
            }
            owner.DisconnectSimulator();Pump(connection);owner.Dispose();
        }
    }
}
