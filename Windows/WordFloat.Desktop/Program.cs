using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class Program
{
    internal static string DataDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WordFloat");
    [STAThread]
    private static int Main(string[] args)
    {
        StartupLog.Begin();
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>StartupLog.Write("UNHANDLED "+e.ExceptionObject);
        TaskScheduler.UnobservedTaskException+=(_,e)=>StartupLog.Write("BACKGROUND "+e.Exception);
        try
        {
            return RunDesktop(args);
        }
        catch(Exception e)
        {
            StartupLog.Failure(e);return 1;
        }
    }
    // Isolate desktop type loading from Main's exception handler.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunDesktop(string[] args)
    {
            UI.ConfigureTypography();
            StartupLog.Write("Loading desktop assemblies");
            NativeWindow.SafeMode=Array.IndexOf(args,"--safe-mode")>=0;
            if(NativeWindow.SafeMode)RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;
            var diagnosticIndex=Array.IndexOf(args,"--diagnostics");
            if(diagnosticIndex>=0)return diagnosticIndex+1<args.Length?WindowsDiagnostics.Run(args[diagnosticIndex+1]):2;
            if(Installer.Handle(args) is int installerResult)return installerResult;
            StartupLog.Write("Acquiring application instance");
            using var instance=new SingleInstance();
            if(!instance.IsPrimary)
            {
                if(!SingleInstance.Send("open"))MessageBox.Show("浮词已经运行，可从系统托盘打开。",AppInfo.Name);
                return 0;
            }
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            using var controller=new AppController(app,DataDirectory);
            instance.Listen(command=>controller.OnUi(command=="quit"?controller.Quit:controller.ShowLibrary));
            app.DispatcherUnhandledException+=(_,e)=>
            {
                RecordError(e.Exception);MessageBox.Show("操作未完成："+e.Exception.Message,AppInfo.Name,MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;
            };
            StartupLog.Write("Showing initial window");
            controller.Start();
            var result=app.Run();StartupLog.Write("EXIT "+result);return result;
    }
    internal static void RecordError(Exception error)
    {
        try{Directory.CreateDirectory(DataDirectory);File.AppendAllText(Path.Combine(DataDirectory,"error.log"),DateTimeOffset.Now+" "+error+Environment.NewLine);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){ }
    }
}
