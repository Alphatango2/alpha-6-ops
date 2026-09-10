using System;
using System.Threading;

namespace Alpha6Ops.Desktop;

// A named event reaches the primary process even while its WPF window has no visible HWND in the
// taskbar. The primary process owns the mutex and listens for explicit restore requests.
internal static class SingleInstance
{
    private const string MutexName="Local\\Alpha6Designs.Alpha6OPS.SingleInstance";
    private const string ActivationName="Local\\Alpha6Designs.Alpha6OPS.Activate";
    private static Mutex? mutex;
    private static EventWaitHandle? activation;
    private static RegisteredWaitHandle? listener;

    internal static bool TryAcquire()
    {
        mutex=new Mutex(true,MutexName,out var createdNew);
        if(createdNew){activation=new EventWaitHandle(false,EventResetMode.AutoReset,ActivationName);return true;}
        mutex.Dispose();mutex=null;
        try{using var signal=EventWaitHandle.OpenExisting(ActivationName);signal.Set();}catch(WaitHandleCannotBeOpenedException){}
        return false;
    }

    internal static void Listen(Action restore)
    {
        if(activation is null)return;
        listener=ThreadPool.RegisterWaitForSingleObject(activation,(_,_)=>restore(),null,Timeout.Infinite,true);
    }

    internal static void Release()
    {
        listener?.Unregister(null);listener=null;activation?.Dispose();activation=null;
        try{mutex?.ReleaseMutex();}catch(ApplicationException){}
        mutex?.Dispose();mutex=null;
    }
}
