using System;
using System.IO;
using System.Windows;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class Program
{
    internal static string DataDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WordFloat");
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if(Installer.Handle(args) is int installerResult)return installerResult;
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
            controller.Start();return app.Run();
        }
        catch(Exception e)
        {
            RecordError(e);MessageBox.Show("无法启动浮词："+e.Message,AppInfo.Name,MessageBoxButton.OK,MessageBoxImage.Error);return 1;
        }
    }
    internal static void RecordError(Exception error)
    {
        try{Directory.CreateDirectory(DataDirectory);File.AppendAllText(Path.Combine(DataDirectory,"error.log"),DateTimeOffset.Now+" "+error+Environment.NewLine);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){ }
    }
}
