using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace WordFloat.Setup;

internal sealed class SetupInstaller : IDisposable
{
    private readonly SetupMetadata metadata;
    private readonly StreamWriter log;
    private string? extracted;
    internal static string InstallDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","WordFloat");
    internal string LogFile { get; }
    internal SetupInstaller(SetupMetadata metadata)
    {
        this.metadata=metadata;
        var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WordFloat","SetupLogs");
        Directory.CreateDirectory(folder);LogFile=Path.Combine(folder,"setup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".log");
        log=new StreamWriter(new FileStream(LogFile,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false)){AutoFlush=true};
        log.WriteLine("WordFloat setup "+metadata.Version+"; "+DateTimeOffset.Now.ToString("O"));
    }
    internal InstallEvent Install(bool desktopShortcut,IProgress<InstallProgress> progress)
    {
        try
        {
            var source=Prepare(progress);
            var script=Path.Combine(source,"WordFloat.ps1");
            if(!File.Exists(script)||!File.Exists(Path.Combine(source,"app","package.json")))throw new InvalidDataException("安装文件不完整，请重新下载安装程序。");
            using(var manifest=File.OpenRead(Path.Combine(source,"app","package.json")))metadata.ValidatePayload(manifest);
            var protocol=new InstallProtocol(metadata.Version);var gate=new object();Exception? protocolFailure=null;
            using var worker=new Process{StartInfo=new ProcessStartInfo(PowerShellPath)
            {
                Arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+script+"\" -Mode InstallWorker"+(desktopShortcut?"":" -NoDesktopShortcut"),
                WorkingDirectory=source,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
                StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
            }};
            worker.OutputDataReceived+=(_,e)=>
            {
                if(e.Data is null)return;
                lock(gate)
                {
                    log.WriteLine(e.Data);
                    try{var update=protocol.Receive(e.Data);if(update is not null)progress.Report(update);}
                    catch(Exception error){protocolFailure=error;}
                }
            };
            worker.ErrorDataReceived+=(_,e)=>{if(e.Data is not null)lock(gate)log.WriteLine("STDERR "+e.Data);};
            progress.Report(new InstallProgress(15,"正在启动安装进程…"));
            if(!worker.Start())throw new InvalidOperationException("无法启动安装进程。");
            worker.BeginOutputReadLine();worker.BeginErrorReadLine();
            if(!worker.WaitForExit(300000))
            {
                try{worker.Kill();worker.WaitForExit();}catch(InvalidOperationException){ }
                throw new TimeoutException("安装超过预计时间，未能确认完成。请查看安装日志后重试。");
            }
            worker.WaitForExit();
            lock(gate)
            {
                if(protocolFailure is not null)throw new InvalidDataException("无法读取安装结果。",protocolFailure);
                var result=protocol.Finish(worker.ExitCode);
                if(!string.Equals(Path.GetFullPath(result.Directory).TrimEnd('\\'),Path.GetFullPath(InstallDirectory).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)
                    ||!File.Exists(Path.Combine(result.Directory,"app","WordFloat.exe"))||!File.Exists(Path.Combine(result.Directory,"WordFloat.ps1")))
                    throw new InvalidDataException("安装结果校验未通过，请查看安装日志。");
                log.WriteLine("INSTALLATION_CONFIRMED "+result.Directory);return result;
            }
        }
        catch(Exception error){log.WriteLine(error);throw;}
    }
    private string Prepare(IProgress<InstallProgress> progress)
    {
        var installDrive=Path.GetPathRoot(InstallDirectory)!;var tempDrive=Path.GetPathRoot(Path.GetTempPath())!;
        var required=metadata.InstalledBytes*(string.Equals(installDrive,tempDrive,StringComparison.OrdinalIgnoreCase)?2:1)+50L*1024*1024;
        if(new DriveInfo(installDrive).AvailableFreeSpace<required||new DriveInfo(tempDrive).AvailableFreeSpace<metadata.InstalledBytes+50L*1024*1024)
            throw new IOException("磁盘空间不足。请为安装预留约 "+Math.Ceiling(required/1048576.0)+" MB 可用空间后重试。");
        using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("WordFloat.Setup.Payload");
        if(resource is null)return AppDomain.CurrentDomain.BaseDirectory;
        extracted=Path.Combine(Path.GetTempPath(),"WordFloatSetup-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extracted);
        SetupArchive.Extract(resource,extracted,metadata.PayloadSha256,metadata.PayloadBytes,(percent,message)=>progress.Report(new InstallProgress(percent,message)));
        return extracted;
    }
    private static string PowerShellPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
    internal static void LaunchInstalled()
    {
        var script=Path.Combine(InstallDirectory,"WordFloat.ps1");
        Process.Start(new ProcessStartInfo(PowerShellPath){Arguments="-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+script+"\" -Mode Launch",UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=InstallDirectory});
    }
    public void Dispose()
    {
        if(extracted is not null)
        {
            try{Directory.Delete(extracted,true);}
            catch(Exception error)when(error is IOException||error is UnauthorizedAccessException){log.WriteLine("Temporary file cleanup: "+error.Message);}
        }
        log.Dispose();
    }
}
