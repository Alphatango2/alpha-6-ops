using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop;

internal interface IFlightRuntimeDispatcher
{
    void VerifyAccess();
    void Post(Action action);
}

internal sealed class WpfFlightRuntimeDispatcher(Dispatcher dispatcher) : IFlightRuntimeDispatcher
{
    public void VerifyAccess()=>dispatcher.VerifyAccess();
    public void Post(Action action)=>dispatcher.BeginInvoke(action);
}

internal interface ISimulatorConnectionSource
{
    bool IsRunning();
    void Launch();
    Task RunAsync(Action<string> opened, Action<LiveReading> received, CancellationToken cancellation);
}

internal sealed class SimConnectConnectionSource : ISimulatorConnectionSource
{
    public bool IsRunning()=>SimulatorLauncher.IsRunning();
    public void Launch()=>SimulatorLauncher.LaunchIfNeeded();
    public Task RunAsync(Action<string> opened,Action<LiveReading> received,CancellationToken cancellation)=>SimConnectSource.RunAsync(opened,received,cancellation);
}
