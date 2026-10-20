using System;
using System.IO;
using System.Runtime.InteropServices;
using WordFloat.Core;

namespace WordFloat.Desktop;

// Keep this boundary free of WPF types so assembly/JIT failures can still be reported.
internal static class StartupLog
{
    internal static string FilePath => Path.Combine(Program.DataDirectory,"startup.log");
    internal static void Begin()
    {
        try
        {
            Directory.CreateDirectory(Program.DataDirectory);
            if(File.Exists(FilePath) && new FileInfo(FilePath).Length>1024*1024)
                File.Move(FilePath,FilePath+".previous",true);
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { }
        Write($"START {AppInfo.Version}; OS={RuntimeInformation.OSDescription}; OSArch={RuntimeInformation.OSArchitecture}; ProcessArch={RuntimeInformation.ProcessArchitecture}; PID={Environment.ProcessId}");
    }
    internal static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Program.DataDirectory);
            File.AppendAllText(FilePath,$"{DateTimeOffset.Now:O} [{Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { }
    }
    internal static void Failure(Exception error)
    {
        Program.RecordError(error);Write("FAILED "+error);
        MessageBox(IntPtr.Zero,"无法启动浮词："+error.Message+"\n\n诊断日志："+FilePath,AppInfo.Name,0x10);
    }
    [DllImport("user32.dll",EntryPoint="MessageBoxW",CharSet=CharSet.Unicode)]
    private static extern int MessageBox(IntPtr window,string text,string caption,uint flags);
}
