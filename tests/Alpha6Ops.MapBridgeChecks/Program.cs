using Alpha6Ops.Desktop.MapV2;
var count=0;
void Check(bool condition,string name) { if(!condition)throw new Exception(name); Console.WriteLine("PASS "+name);count++; }
var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var applied=new List<string>();
var errors=new List<Exception>();
await using(var box=new LatestMapMailbox<string>(async (value,token)=>{
    lock(applied)applied.Add(value);
    if(value=="first"){entered.SetResult();await release.Task.WaitAsync(token);}
},error=>errors.Add(error),TimeSpan.FromMilliseconds(2)))
{
    box.Offer("first");await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    var timer=System.Diagnostics.Stopwatch.StartNew();
    for(var i=0;i<10000;i++)box.Offer(i.ToString());
    Check(timer.ElapsedMilliseconds<500,"Producer does not await stalled consumer");
    Check(box.HasPending&&box.Coalesced==9999,"Only latest pending value retained under pressure");
    release.SetResult();
    for(var i=0;i<100&&box.Consumed<2;i++)await Task.Delay(5);
    Check(applied.SequenceEqual(new[]{"first","9999"}),"Stall ends with newest complete snapshot only");
    Check(box.Offered==10001&&box.Consumed==2,"Counters account for coalesced visual snapshots");
}
Check(errors.Count==0,"Normal disposal is not a renderer failure");
var canceled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var blocking=new LatestMapMailbox<string>(async (_,token)=>{canceled.SetResult();await Task.Delay(Timeout.Infinite,token);},_=>{},TimeSpan.Zero);
blocking.Offer("a");await canceled.Task.WaitAsync(TimeSpan.FromSeconds(2));await blocking.DisposeAsync();
blocking.Offer("after-disposal");Check(!blocking.HasPending,"Disposal cancels in-flight work and rejects later offers");
var failure=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
await using(var broken=new LatestMapMailbox<string>((_,_)=>throw new InvalidOperationException("diagnostic"),_=>failure.SetResult(),TimeSpan.Zero))
{
    broken.Offer("a");await failure.Task.WaitAsync(TimeSpan.FromSeconds(2));broken.Offer("b");
    Check(!broken.HasPending,"Consumer failure closes mailbox without unbounded retries");
}
Console.WriteLine($"{count} checks passed.");
