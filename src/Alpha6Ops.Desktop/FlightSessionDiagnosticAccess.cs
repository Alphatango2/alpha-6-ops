using System;
using System.Linq;
using System.Reflection;
using Alpha6Ops.Core;

namespace Alpha6Ops.Desktop;

// Existing diagnostic harnesses inspect private state. Keep that reflection in
// their adapter, not in the application API. Production never calls this class.
internal static class FlightSessionDiagnosticAccess
{
    // Explicit synthetic intent fixture for live integration checks. No production
    // simulator path infers cutoff commands from the old EnginesRunning flag.
    internal static LiveReading WithTerminalFixture(LiveReading reading)
    {
        var now=DateTimeOffset.UtcNow;var sample=reading.Telemetry;var operating=sample.EnginesRunning;
        return reading with{ReceivedUtc=now,Telemetry=sample with{TerminalShutdown=new(ShutdownIntentSupport.Enhanced,
            "synthetic-validated-intent","diagnostic-source",2,[new(1,!operating,operating,now),new(2,!operating,operating,now)],sample.At,now,true,true)}};
    }
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    internal static FlightSessionCoordinator Owner(MainWindow window)=>(FlightSessionCoordinator)typeof(MainWindow).GetField("coordinator",Flags)!.GetValue(window)!;
    internal static object? Get(MainWindow window,string name)
    {
        var owner=Owner(window);var field=owner.GetType().GetField(name,Flags);
        return field is not null?field.GetValue(owner):typeof(MainWindow).GetField(name,Flags)?.GetValue(window)??typeof(MainWindow).GetProperty(name,Flags)?.GetValue(window);
    }
    internal static void Set(MainWindow window,string name,object? value)
    {
        if(typeof(MainWindow).GetField(name,Flags) is {} presentationField){presentationField.SetValue(window,value);return;}
        var owner=Owner(window);var field=owner.GetType().GetField(name,Flags);
        if(field is not null){field.SetValue(owner,value);Publish(window);}
        else typeof(MainWindow).GetField(name,Flags)!.SetValue(window,value);
    }
    internal static void Publish(MainWindow window)=>typeof(FlightSessionCoordinator).GetMethod("PublishSnapshot",Flags)!.Invoke(Owner(window),null);
    internal static object? Invoke(MainWindow window,string name,params object?[] arguments)
    {
        var owner=Owner(window);
        var method=owner.GetType().GetMethods(Flags|BindingFlags.DeclaredOnly).SingleOrDefault(m=>m.Name==name&&m.GetParameters().Length==arguments.Length);
        if(method is null)return typeof(MainWindow).GetMethods(Flags|BindingFlags.DeclaredOnly).Single(m=>m.Name==name&&m.GetParameters().Length==arguments.Length).Invoke(window,arguments);
        var result=method.Invoke(owner,arguments);
        if(name is "ResetLiveTrackingState" or "ResetLiveIdentity" or "ResetObservedSession" or "AddTrackingEvent")Publish(window);
        return result;
    }
}
