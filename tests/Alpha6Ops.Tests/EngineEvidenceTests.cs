using Alpha6Ops.Core;

internal static class EngineEvidenceTests
{
    internal static void Run(Action<bool,string> check)
    {
        var tracker=new EngineEvidenceTracker();var at=DateTimeOffset.Parse("2026-09-17T21:00Z");
        EngineReading[] Raw(double flow,double n1,double n2,double starter=0)=>Enumerable.Range(1,4).Select(i=>new EngineReading(i,0,flow,n1,n2,starter)).ToArray();
        for(var i=0;i<4;i++)tracker.Observe(Raw(0,0,0),at.AddSeconds(i));
        check(tracker.Observe(Raw(0,0,0),at.AddSeconds(4)).All(x=>x.Condition==EngineCondition.Unknown),"four all-zero engines remain unknown before positive evidence");
        for(var i=5;i<9;i++)tracker.Observe(Raw(500,0,0),at.AddSeconds(i));
        check(tracker.Observe(Raw(500,0,0),at.AddSeconds(9)).All(x=>x.Condition==EngineCondition.Unknown),"fuel flow alone does not confirm engine running");
        for(var i=10;i<14;i++)tracker.Observe(Raw(500,20,60,1),at.AddSeconds(i));
        check(tracker.Observe(Raw(500,20,60,1),at.AddSeconds(14)).All(x=>x.Condition==EngineCondition.Starting),"starter with fuel/spool is starting until starter disengages");
        for(var i=15;i<19;i++)tracker.Observe(Raw(500,20,60),at.AddSeconds(i));
        check(tracker.Observe(Raw(500,20,60),at.AddSeconds(19)).All(x=>x.Condition==EngineCondition.Running),"four engines corroborated by fuel and spool");
        for(var i=20;i<24;i++)tracker.Observe(Raw(0,0,0),at.AddSeconds(i));
        check(tracker.Observe(Raw(0,0,0),at.AddSeconds(24)).All(x=>x.Condition==EngineCondition.Off),"observed engines can subsequently confirm stopped");
        for(var i=25;i<29;i++)tracker.Observe(Raw(500,20,60),at.AddSeconds(i));
        check(tracker.Observe(Raw(500,20,60),at.AddSeconds(29)).All(x=>x.Condition==EngineCondition.Running),"engine restart is supported");
        check(tracker.Observe([new(1,0,null,null,null,null)],at.AddSeconds(30))[0].Condition==EngineCondition.Unknown,"missing alternate readings invalidate stale state");
        var a220=new EngineEvidenceTracker();
        for(var i=0;i<3;i++)a220.Observe([new(1,0,0.0002,4,12,0)],at.AddSeconds(i));
        check(a220.Observe([new(1,0,0.0002,4,12,0)],at.AddSeconds(3))[0].Condition==EngineCondition.Starting,"A220 dual-spool rise identifies starting without generic fuel flow");
        for(var i=4;i<7;i++)a220.Observe([new(1,0,0.0002,18.9,64.5,0)],at.AddSeconds(i));
        var a220Running=a220.Observe([new(1,0,0.0002,18.9,64.5,0)],at.AddSeconds(7))[0];
        check(a220Running.Condition==EngineCondition.Running&&a220Running.Source=="N1 / N2 spool","A220 sustained N1/N2 confirms running without generic fuel flow");
        EngineAssessment? a220Shutdown=null;
        for(var i=8;i<12;i++)a220Shutdown=a220.Observe([new(1,0,0.0002,14-(i-8),38-(i-8)*2,0)],at.AddSeconds(i))[0];
        check(a220Shutdown?.Condition==EngineCondition.Stopping&&!a220Shutdown.Source.Contains("combustion"),"shutdown evidence never claims loss of combustion when combustion was never reported");
        var oil=new EngineEvidenceTracker();
        for(var i=0;i<4;i++)oil.Observe([new(1,0,0,16,20,0,5000)],at.AddSeconds(i));
        check(oil.Observe([new(1,0,0,16,20,0,5000)],at.AddSeconds(4))[0].Condition==EngineCondition.Running,"oil pressure corroborates a single available spool channel");
        var shutdown=new EngineEvidenceTracker();
        for(var i=0;i<4;i++)shutdown.Observe([new(1,1,420,21,66,0,5200)],at.AddSeconds(i));
        shutdown.Observe([new(1,0,240,16,50,0,4100)],at.AddSeconds(4));
        shutdown.Observe([new(1,0,190,13,39,0,3300)],at.AddSeconds(5));
        shutdown.Observe([new(1,0,150,11,32,0,2700)],at.AddSeconds(6));
        var decaying=shutdown.Observe([new(1,0,120,9,27,0,2200)],at.AddSeconds(7))[0];
        check(decaying.Condition==EngineCondition.Stopping,"confirmed running engine enters shutdown while residual fuel flow and spool decay");
        var detector=new PhaseDetector(initialPhase:FlightPhase.TaxiIn);
        var sample=new Telemetry(at,true,0,true,false,EngineDataValid:false);
        for(var i=0;i<20;i++)detector.Observe(sample with{At=at.AddSeconds(i)});
        check(detector.Phase==FlightPhase.TaxiIn,"unknown engines cannot automatically complete a flight");
        for(var i=20;i<25;i++)detector.Observe(sample with{At=at.AddSeconds(i),PilotConfirmedBlockIn=true});
        check(detector.Phase==FlightPhase.Complete,"explicit parked confirmation permits stable block-in with unavailable sensors");
        var away=new PhaseDetector(initialPhase:FlightPhase.TaxiIn);
        for(var i=0;i<10;i++)away.Observe(sample with{At=at.AddSeconds(i),EngineDataValid=true,BlockInAllowed=false});
        check(away.Phase==FlightPhase.TaxiIn,"unverified destination cannot automatically complete");
    }
}
