using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace Alpha6Ops.Desktop.MapV2;

// Explicit heartbeat diagnostic only. Never consumes input, handles a message,
// changes priority, posts a probe, or subscribes to CompositionTarget.Rendering.
internal sealed class HeartbeatNativeObserver : IDisposable
{
    private readonly DispatcherSchedulingTrace trace;
    private readonly CoreWebView2? browser;
    private long lastQuery;
    private long messages, queries, costTicks;
    [DllImport("user32.dll")] private static extern uint MsgWaitForMultipleObjectsEx(uint count,IntPtr handles,uint milliseconds,uint mask,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    private readonly uint processQueue=RegisterWindowMessage("DispatcherProcessQueue");

    internal HeartbeatNativeObserver(DispatcherSchedulingTrace trace,WebMapRenderer? renderer)
    {
        this.trace=trace;browser=renderer?.Core;
        ComponentDispatcher.ThreadFilterMessage+=Message;
        InputManager.Current.PreProcessInput+=Input;
        if(browser is not null)browser.WebMessageReceived+=Web;
        trace.Mark("native-observer-start",$"dispatcher-message={processQueue}; zero-time queue query; no message removal");
    }
    private void Message(ref MSG message,ref bool handled)
    {
        long start=Stopwatch.GetTimestamp();messages++;
        // One row per retrieved native message; no payload/coordinates recorded.
        trace.Mark("native-message",message.message==processQueue?"DispatcherProcessQueue":$"0x{message.message:X}",
            message.wParam.ToInt64(),message.hwnd.ToInt64());
        if(Stopwatch.GetElapsedTime(lastQuery).TotalMilliseconds>=10)
        {
            lastQuery=start;queries++;
            // Same categories WPF IsInputPending uses: QS_INPUT, QS_POSTMESSAGE,
            // QS_EVENT (WPF private event category). MWMO_INPUTAVAILABLE=4.
            int mask=0;
            if(MsgWaitForMultipleObjectsEx(0,IntPtr.Zero,0,0x0407,4)==0)mask|=1;
            if(MsgWaitForMultipleObjectsEx(0,IntPtr.Zero,0,0x0008,4)==0)mask|=2;
            if(MsgWaitForMultipleObjectsEx(0,IntPtr.Zero,0,0x2000,4)==0)mask|=4;
            trace.Mark("native-pending","input=1;posted=2;event=4",mask);
        }
        costTicks+=Stopwatch.GetTimestamp()-start;
    }
    private void Input(object sender,PreProcessInputEventArgs e)
    {
        var input=e.StagingItem.Input;
        trace.Mark("wpf-input",input.RoutedEvent?.Name??input.GetType().Name);
    }
    private void Web(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var json=JsonDocument.Parse(e.WebMessageAsJson);
        trace.Mark("web-message",json.RootElement.TryGetProperty("type",out var type)?type.GetString()??"unknown":"unknown");
    }
    public void Dispose()
    {
        ComponentDispatcher.ThreadFilterMessage-=Message;
        InputManager.Current.PreProcessInput-=Input;
        if(browser is not null)browser.WebMessageReceived-=Web;
        trace.Mark("native-observer-cost",$"messages={messages}; queries={queries}",costTicks*1000d/Stopwatch.Frequency);
    }
}
