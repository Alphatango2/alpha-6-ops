using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha6Ops.Desktop;

internal sealed partial class FlightSessionCoordinator
{
    private long connectionGeneration;
    private static readonly TimeSpan[] ReconnectBackoff = [TimeSpan.FromSeconds(3),TimeSpan.FromSeconds(5),TimeSpan.FromSeconds(10),TimeSpan.FromSeconds(20),TimeSpan.FromSeconds(30)];

    internal async Task ConnectSimulatorAsync()
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(exiting,this);
        if(liveCancellation is not null)return;
        ResetTerminalConfirmation();
        if(activePlan is null)suppressObservedFlightAfterCloseout=false;
        var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        liveCancellation=cancellation;
        if(liveRecorder is null)ResetLiveIdentity();
        else{currentLiveReading=null;liveDataCurrent=false;liveWasInMenu=false;}
        liveNeedsBaseline=liveRecorder is not null;liveSource="MSFS 2024";
        if(liveRecorder is null){liveLast=null;liveAircraft=null;liveTelemetry=null;liveRouteProgress=null;trackingEventMonitor=new();trackingEvents.Clear();liveRotation=null;milestones.Clear();}
        StartLog("live_simconnect");
        var attempt=0;
        try
        {
            if(!diagnosticMode&&!simulator.IsRunning())
            {
                connectionState=SimulatorConnectionState.Launching;
                programMonitor?.Update("Launching simulator");
                connectionMessage="Launching MSFS 2024. Load a flight; OPS will connect automatically. Disconnect cancels the connection attempt.";
                PublishSnapshot();
                await Task.Run(simulator.Launch);
                cancellation.Token.ThrowIfCancellationRequested();
                RecordLog("simulator_launch_requested",null,new{edition="Microsoft Store"});
            }
            while(true)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                attempt++;
                var generation=++connectionGeneration;
                connectionState=SimulatorConnectionState.Connecting;
                programMonitor?.Update("Connecting");
                connectionMessage=attempt==1?"Connecting to the local simulator…":$"Reconnecting to the local simulator (attempt {attempt})…";
                PublishSnapshot();
                try
                {
                    var currentAttempt=attempt;
                    bool Current()=>!exiting&&!cancellation.IsCancellationRequested&&ReferenceEquals(liveCancellation,cancellation)&&generation==connectionGeneration;
                    Action<string> opened=message=>dispatcher.Post(()=>
                    {
                        if(!Current())return;
                        RecordLog("connection_opened",null,new{message,source=liveSource,attempt=currentAttempt});
                        connectionMessage=message;connectionState=SimulatorConnectionState.Connected;programMonitor?.Update("Connected");PublishSnapshot();
                    });
                    Action<LiveReading> received=reading=>
                    {
                        reading=reading with{ReceivedUtc=reading.ReceivedUtc??DateTimeOffset.UtcNow};
                        var traceArrival=MapV2.NativeMapInputTrace.Arrival(reading.Telemetry.At);
                        dispatcher.Post(()=>
                        {
                            if(!Current())return;
                            ObserveLive(reading);
                            MapV2.NativeMapInputTrace.Processed(reading.Telemetry.At,traceArrival);
                        });
                    };
                    await simulator.RunAsync(opened,received,cancellation.Token);
                    break;
                }
                catch(IOException error)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    // Invalidate queued callbacks from this attempt before retrying.
                    connectionGeneration++;
                    MarkLiveUnavailable();
                    RecordLog("connection_error",liveLast,new{message=error.GetBaseException().Message,type=error.GetType().Name,attempt});
                    if(trackingEventMonitor is {} monitor)AddTrackingEvent(monitor.SystemEvent(liveLast??DateTimeOffset.UtcNow,"Simulator connection lost",error.GetBaseException().Message,"alert") with{Code=TrackingEventCode.ConnectionLost});
                    var delay=ReconnectBackoff[Math.Min(attempt-1,ReconnectBackoff.Length-1)];
                    connectionMessage=$"{error.GetBaseException().Message} Retrying in {delay.TotalSeconds:0}s… Click Disconnect to stop.";
                    connectionState=SimulatorConnectionState.Reconnecting;programMonitor?.Update("Reconnecting");PublishSnapshot();
                    await Task.Delay(delay,cancellation.Token);
                }
            }
            connectionMessage="Disconnected. Live milestones remain visible until the next connection or replay.";
            connectionState=SimulatorConnectionState.Disconnected;
            programMonitor?.Update("Disconnected");
        }
        catch(Exception error) when(error is IOException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or TypeInitializationException or OperationCanceledException)
        {
            if(!exiting)
            {
                RecordLog("connection_error",liveLast,new{message=error.GetBaseException().Message,type=error.GetType().Name});
                connectionMessage=error is OperationCanceledException?"Disconnected.":error.GetBaseException().Message;
                connectionState=error is OperationCanceledException?SimulatorConnectionState.Disconnected:SimulatorConnectionState.Failed;
                programMonitor?.Update(error is OperationCanceledException?"Disconnected":"Connection failed");
            }
        }
        finally
        {
            if(ReferenceEquals(liveCancellation,cancellation))
            {
                ResetTerminalConfirmation();
                connectionGeneration++;
                liveCancellation=null;liveDataCurrent=false;
                if(!exiting)
                {
                    FinishLog("connection_closed");
                    if(liveFlightHistoryId is not null){EndFlightHistory(liveFlightHistoryId,"Interrupted");SaveFlightRecovery();}
                    if(currentLiveReading is not null)trackingStatus="Telemetry unavailable. Waiting for reconnection.";
                    PublishSnapshot();
                }
            }
            cancellation.Dispose();
        }
    }

    internal void DisconnectSimulator()
    {
        dispatcher.VerifyAccess();
        connectionGeneration++;
        liveCancellation?.Cancel();
        if(!exiting)MarkLiveUnavailable();
    }
}
