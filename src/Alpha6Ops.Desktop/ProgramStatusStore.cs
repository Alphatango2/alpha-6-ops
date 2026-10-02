using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Alpha6Ops.Desktop;

internal sealed class ProgramStatusStore
{
    private static readonly ConcurrentDictionary<string,object> Gates=new(StringComparer.OrdinalIgnoreCase);
    private static readonly int[] RetryDelays=[10,20,40,80];
    private static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true};
    private readonly string path;
    private readonly object gate;

    internal ProgramStatusStore(string path)
    {
        this.path=Path.GetFullPath(path);gate=Gates.GetOrAdd(this.path,_=>new object());
    }

    internal JsonDocument? Read()
    {
        lock(gate)
        {
            try
            {
                using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
                return JsonDocument.Parse(stream);
            }
            catch(FileNotFoundException){return null;}
            catch(DirectoryNotFoundException){return null;}
        }
    }

    internal void Write(object value)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,JsonOptions);var temporary=path+".tmp";
        lock(gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);Exception? primary=null;
            try
            {
                Retry(()=>
                {
                    using var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None);
                    stream.Write(bytes);stream.Flush(flushToDisk:true);
                },temporary,"stage/flush");
                Retry(()=>File.Move(temporary,path,overwrite:true),path,"replace");
            }
            catch(Exception error){primary=error;throw;}
            finally
            {
                try{Retry(()=>File.Delete(temporary),temporary,"cleanup");}
                catch(Exception) when(primary is not null){ }
            }
        }
    }

    private static void Retry(Action operation,string path,string stage)
    {
        for(var attempt=0;;attempt++)
        {
            try{operation();return;}
            catch(Exception error) when(error is IOException or UnauthorizedAccessException)
            {
                if(attempt>=RetryDelays.Length||!Transient(error,path))
                {
                    error.Data["StatusStage"]=stage;error.Data["StatusAttempts"]=attempt+1;error.Data["StatusPath"]=path;throw;
                }
                Thread.Sleep(RetryDelays[attempt]);
            }
        }
    }

    private static bool Transient(Exception error,string target)
    {
        var code=error.HResult&0xffff;if(code is 32 or 33)return true;if(code!=5||Directory.Exists(target))return false;
        try{return !File.Exists(target)||(File.GetAttributes(target)&FileAttributes.ReadOnly)==0;}
        catch(FileNotFoundException){return true;}
        catch(DirectoryNotFoundException){return false;}
        catch(UnauthorizedAccessException){return true;}
        catch(IOException){return false;}
    }
}
