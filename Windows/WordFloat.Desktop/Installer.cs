using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class Installer
{
    private const string RegistryPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\WordFloat";
    internal static string InstallDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","WordFloat");
    private static string Executable=>Path.Combine(InstallDirectory,"浮词.exe");
    private static string MenuDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"浮词");
    private static string DesktopLink=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"浮词.lnk");
    internal static int? Handle(string[] args)
    {
        var quiet=args.Contains("--quiet");
        if(args.Contains("--uninstall"))return Uninstall(quiet);
        var isInstaller=Path.GetFileNameWithoutExtension(Environment.ProcessPath!).EndsWith("-安装",StringComparison.Ordinal);
        if(!args.Contains("--install")&&!isInstaller)return null;
        try
        {
            StopExisting();
            var source=Environment.ProcessPath!;
            Directory.CreateDirectory(InstallDirectory);
            if(!string.Equals(Path.GetFullPath(source),Path.GetFullPath(Executable),StringComparison.OrdinalIgnoreCase))
            {
                var temporary=Path.Combine(InstallDirectory,"WordFloat."+Guid.NewGuid().ToString("N")+".new");
                try
                {
                    File.Copy(source,temporary);
                    using(var original=File.OpenRead(source))using(var copy=File.OpenRead(temporary))
                        if(!SHA256.HashData(original).SequenceEqual(SHA256.HashData(copy)))throw new IOException("安装文件校验失败，请重新下载。");
                    for(var attempt=0;;attempt++)
                    {
                        try{File.Move(temporary,Executable,true);break;}
                        catch(IOException)when(attempt<15){Thread.Sleep(200);}
                    }
                }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
            Directory.CreateDirectory(MenuDirectory);
            Shortcut(Path.Combine(MenuDirectory,"浮词.lnk"),"");Shortcut(DesktopLink,"");
            Shortcut(Path.Combine(MenuDirectory,"卸载浮词.lnk"),"--uninstall");
            using(var key=Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                key.SetValue("DisplayName",AppInfo.Name);key.SetValue("DisplayVersion",AppInfo.Version);
                key.SetValue("Publisher","浮词");key.SetValue("InstallLocation",InstallDirectory);
                key.SetValue("DisplayIcon",Executable+",0");key.SetValue("UninstallString","\""+Executable+"\" --uninstall");
                key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);
                key.SetValue("EstimatedSize",(int)Math.Min(int.MaxValue,new FileInfo(Executable).Length/1024),RegistryValueKind.DWord);
            }
            if(!quiet)
            {
                MessageBox.Show("浮词已安装。可随时从桌面或开始菜单打开。\n\n"+InstallDirectory,AppInfo.Name,MessageBoxButton.OK,MessageBoxImage.Information);
                Process.Start(new ProcessStartInfo(Executable){UseShellExecute=true});
            }
            return 0;
        }
        catch(Exception e)
        {
            Program.RecordError(e);if(!quiet)MessageBox.Show("安装未完成："+e.Message,AppInfo.Name,MessageBoxButton.OK,MessageBoxImage.Error);return 1;
        }
    }
    private static void StopExisting()
    {
        if(SingleInstance.WaitUntilStopped(0))return;
        if(!SingleInstance.Send("quit")||!SingleInstance.WaitUntilStopped(15000))throw new IOException("请先从系统托盘退出正在运行的浮词，再重新安装。");
    }
    private static void Shortcut(string path,string arguments)
    {
        var type=Type.GetTypeFromProgID("WScript.Shell")??throw new InvalidOperationException("系统快捷方式服务不可用。");
        dynamic shell=Activator.CreateInstance(type)!;
        try
        {
            dynamic link=shell.CreateShortcut(path);
            try{link.TargetPath=Executable;link.WorkingDirectory=InstallDirectory;link.Arguments=arguments;link.Description=AppInfo.Name;link.IconLocation=Executable+",0";link.Save();}
            finally{Marshal.FinalReleaseComObject(link);}
        }
        finally{Marshal.FinalReleaseComObject(shell);}
    }
    private static int Uninstall(bool quiet)
    {
        if(!quiet&&MessageBox.Show("卸载浮词？学习进度和外观设置会保留在本机。",AppInfo.Name,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return 0;
        try
        {
            StopExisting();
            foreach(var path in new[]{DesktopLink,Path.Combine(MenuDirectory,"浮词.lnk"),Path.Combine(MenuDirectory,"卸载浮词.lnk")})if(File.Exists(path))File.Delete(path);
            if(Directory.Exists(MenuDirectory)&&!Directory.EnumerateFileSystemEntries(MenuDirectory).Any())Directory.Delete(MenuDirectory);
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false);
            // The helper waits for this process before removing its executable. Unknown user files are retained.
            static string Quote(string value)=>"'"+value.Replace("'","''")+"'";
            var script="$ErrorActionPreference='Stop'; Wait-Process -Id "+Environment.ProcessId+" -ErrorAction SilentlyContinue; "+
                "for($i=0;$i -lt 25;$i++){try{if(Test-Path -LiteralPath "+Quote(Executable)+"){Remove-Item -LiteralPath "+Quote(Executable)+" -Force}; break}catch{Start-Sleep -Milliseconds 200}}; "+
                "if((Test-Path -LiteralPath "+Quote(InstallDirectory)+") -and -not (Get-ChildItem -LiteralPath "+Quote(InstallDirectory)+" -Force)){Remove-Item -LiteralPath "+Quote(InstallDirectory)+" -Force}";
            var encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe"))
                {UseShellExecute=false,CreateNoWindow=true,ArgumentList={"-NoProfile","-NonInteractive","-EncodedCommand",encoded}});
            if(!quiet)MessageBox.Show("浮词卸载即将完成，学习设置已保留。",AppInfo.Name,MessageBoxButton.OK,MessageBoxImage.Information);
            return 0;
        }
        catch(Exception e){Program.RecordError(e);if(!quiet)MessageBox.Show("卸载未完成："+e.Message,AppInfo.Name);return 1;}
    }
}
