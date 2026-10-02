using System;
using System.Threading;

namespace Alpha6Ops.Desktop;

// A named event reaches the primary process even while its WPF window has no visible HWND in the
// taskbar. The primary process owns the mutex and listens for explicit restore requests.
internal static class SingleInstance
{
    private static readonly string[] MutexNames=["Local\\Sierra4.SingleInstance","Local\\Alpha6Designs.Alpha6OPS.SingleInstance"];
    private static readonly string[] ActivationNames=["Local\\Sierra4.Activate","Local\\Alpha6Designs.Alpha6OPS.Activate"];
    private static readonly System.Collections.Generic.List<Mutex> mutexes=[];
    private static readonly System.Collections.Generic.List<EventWaitHandle> activations=[];
    private static readonly System.Collections.Generic.List<RegisteredWaitHandle> listeners=[];

    internal static bool TryAcquire()
    {
        Release();
        var ownsAll=true;
        foreach(var name in MutexNames)
        {
            var mutex=new Mutex(true,name,out var createdNew);mutexes.Add(mutex);
            if(!createdNew)ownsAll=false;
        }
        if(ownsAll)
        {
            foreach(var name in ActivationNames)activations.Add(new EventWaitHandle(false,EventResetMode.AutoReset,name));
            return true;
        }
        foreach(var name in ActivationNames)try{using var signal=EventWaitHandle.OpenExisting(name);signal.Set();}catch(WaitHandleCannotBeOpenedException){}
        Release();return false;
    }

    internal static void Listen(Action restore)
    {
        foreach(var activation in activations)listeners.Add(ThreadPool.RegisterWaitForSingleObject(activation,(_,_)=>restore(),null,Timeout.Infinite,true));
    }

    internal static void Release()
    {
        foreach(var listener in listeners)listener.Unregister(null);listeners.Clear();
        foreach(var activation in activations)activation.Dispose();activations.Clear();
        foreach(var mutex in mutexes){try{mutex.ReleaseMutex();}catch(ApplicationException){}mutex.Dispose();}mutexes.Clear();
    }
}
