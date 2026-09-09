using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alpha6Ops.Core;

namespace Alpha6Ops.FlightLab;

internal sealed class FlightLabServer(Action<string> status) : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly object sync = new();
    private FlightLabFrame? latest;
    private NamedPipeServerStream? active;
    private bool offline;
    internal bool Frozen { get; set; }

    internal void Start() => _ = Task.Run(RunAsync);
    internal void Update(FlightLabFrame frame) { lock(sync) latest=frame; }
    internal void DropClient() { lock(sync) active?.Dispose(); }
    internal void SetOffline(bool value) { offline=value;if(value)DropClient(); }

    private async Task RunAsync()
    {
        while(!lifetime.IsCancellationRequested)
        {
            if(offline){status("LAB LINK OFFLINE");await Task.Delay(250,lifetime.Token);continue;}
            try
            {
                using var pipe=new NamedPipeServerStream(FlightLabProtocol.PipeName,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
                lock(sync)active=pipe;status("WAITING FOR ALPHA 6 OPS");
                await pipe.WaitForConnectionAsync(lifetime.Token);status("ALPHA 6 OPS CONNECTED");
                using var writer=new StreamWriter(pipe){AutoFlush=true};
                while(pipe.IsConnected&&!lifetime.IsCancellationRequested)
                {
                    FlightLabFrame? frame;lock(sync)frame=latest;
                    if(!Frozen&&frame is not null)await writer.WriteLineAsync(JsonSerializer.Serialize(frame));
                    await Task.Delay(1000,lifetime.Token);
                }
            }
            catch(Exception error) when(error is IOException or ObjectDisposedException){status(offline?"LAB LINK OFFLINE":"CONNECTION DROPPED • WAITING");}
            catch(OperationCanceledException){break;}
            finally{lock(sync)active=null;}
        }
    }

    public void Dispose(){lifetime.Cancel();DropClient();lifetime.Dispose();}
}
