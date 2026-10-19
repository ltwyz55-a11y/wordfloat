using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace WordFloat.Desktop;

internal sealed class SingleInstance : IDisposable
{
    private static string UserId => WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
    private static string MutexName => @"Local\WordFloat."+UserId;
    private static string PipeName => "WordFloat."+UserId;
    private readonly Mutex mutex = new(false,MutexName);
    private readonly CancellationTokenSource cancellation = new();
    internal bool IsPrimary { get; }
    internal SingleInstance()
    {
        try { IsPrimary=mutex.WaitOne(0); } catch(AbandonedMutexException) { IsPrimary=true; }
    }
    internal void Listen(Action<string> receive)
    {
        if(!IsPrimary)return;
        _=Task.Run(async()=>
        {
            while(!cancellation.IsCancellationRequested)
            {
                try
                {
                    using var server=new NamedPipeServerStream(PipeName,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(cancellation.Token);
                    using var reader=new StreamReader(server);
                    var command=await reader.ReadLineAsync(cancellation.Token);
                    if(command is "open" or "quit")receive(command);
                }
                catch(OperationCanceledException){break;}
                catch(IOException){await Task.Delay(100);}
            }
        });
    }
    internal static bool Send(string command)
    {
        try
        {
            using var client=new NamedPipeClientStream(".",PipeName,PipeDirection.Out,PipeOptions.CurrentUserOnly);
            client.Connect(1800);using var writer=new StreamWriter(client){AutoFlush=true};writer.WriteLine(command);return true;
        }
        catch(Exception e) when(e is IOException or TimeoutException or UnauthorizedAccessException){return false;}
    }
    internal static bool WaitUntilStopped(int milliseconds)
    {
        try
        {
            using var probe=new Mutex(false,MutexName);
            var acquired=false;
            try { acquired=probe.WaitOne(milliseconds); } catch(AbandonedMutexException){acquired=true;}
            if(acquired)probe.ReleaseMutex();return acquired;
        }
        catch(UnauthorizedAccessException){return false;}
    }
    public void Dispose()
    {
        cancellation.Cancel();
        if(IsPrimary)mutex.ReleaseMutex();mutex.Dispose();
    }
}
