using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace WordFloat.Setup;

[DataContract]
internal sealed class SetupMetadata
{
    [DataMember(Name="product")] public string Product { get; set; } = "";
    [DataMember(Name="version")] public string Version { get; set; } = "";
    [DataMember(Name="installedBytes")] public long InstalledBytes { get; set; }
    [DataMember(Name="payloadBytes")] public long PayloadBytes { get; set; }
    [DataMember(Name="payloadSha256")] public string PayloadSha256 { get; set; } = "";
    internal static SetupMetadata Read(Stream source)
    {
        var result=(SetupMetadata?)new DataContractJsonSerializer(typeof(SetupMetadata)).ReadObject(source);
        if(result is null||result.Product!="WordFloat"||string.IsNullOrWhiteSpace(result.Version)||result.InstalledBytes<=0)
            throw new InvalidDataException("安装信息无效，请重新下载安装程序。");
        return result;
    }
}

[DataContract]
internal sealed class InstallEvent
{
    [DataMember(Name="kind")] public string Kind { get; set; } = "";
    [DataMember(Name="percent")] public int Percent { get; set; }
    [DataMember(Name="message")] public string Message { get; set; } = "";
    [DataMember(Name="version")] public string Version { get; set; } = "";
    [DataMember(Name="directory")] public string Directory { get; set; } = "";
    [DataMember(Name="desktopShortcutCreated")] public bool DesktopShortcutCreated { get; set; }
    [DataMember(Name="warnings")] public string[] Warnings { get; set; } = Array.Empty<string>();
}

internal sealed class InstallProgress
{
    internal int Percent { get; }
    internal string Message { get; }
    internal InstallProgress(int percent,string message){Percent=percent;Message=message;}
}

internal sealed class InstallProtocol
{
    private const string Prefix="WORDFLOAT_SETUP:";
    private readonly string version;
    private InstallEvent? completed;
    private string? failure;
    internal InstallProtocol(string version){this.version=version;}
    internal InstallProgress? Receive(string line)
    {
        if(!line.StartsWith(Prefix,StringComparison.Ordinal))return null;
        using var bytes=new MemoryStream(Encoding.UTF8.GetBytes(line.Substring(Prefix.Length)));
        var item=(InstallEvent?)new DataContractJsonSerializer(typeof(InstallEvent)).ReadObject(bytes)??throw new InvalidDataException("安装进程返回了空消息。");
        switch(item.Kind)
        {
            case "progress":
                if(item.Percent<0||item.Percent>100)throw new InvalidDataException("安装进度无效。");
                return new InstallProgress(15+(int)Math.Round(item.Percent*0.85),item.Message);
            case "error":failure=item.Message;break;
            case "complete":
                if(completed is not null||item.Version!=version||string.IsNullOrWhiteSpace(item.Directory))throw new InvalidDataException("安装结果与当前版本不一致。");
                completed=item;break;
            default:throw new InvalidDataException("无法识别安装进程的消息。");
        }
        return null;
    }
    internal InstallEvent Finish(int exitCode)
    {
        if(exitCode!=0||failure is not null||completed is null)
            throw new InvalidOperationException(failure??"安装进程未能正常完成，错误代码："+exitCode+"。请查看安装日志后重试。");
        completed.Warnings??=Array.Empty<string>();return completed;
    }
}

internal static class SetupArchive
{
    internal static void Extract(Stream source,string directory,string expectedHash,long expectedBytes,Action<int,string> progress)
    {
        progress(0,"正在校验安装程序…");
        using(var sha=SHA256.Create())
        {
            var actual=BitConverter.ToString(sha.ComputeHash(source)).Replace("-","");
            if(expectedHash.Length!=64||!string.Equals(actual,expectedHash,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装文件校验失败，请重新下载安装程序。");
        }
        source.Position=0;
        var root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        using var archive=new ZipArchive(source,ZipArchiveMode.Read,true);
        if(archive.Entries.Count>10000||expectedBytes<=0)throw new InvalidDataException("安装资源数量无效。");
        long bytes=0;
        foreach(var entry in archive.Entries)
        {
            var path=Path.GetFullPath(Path.Combine(root,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
            if(entry.FullName.Contains(":")||!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("安装资源包含无效路径。");
            if(entry.FullName.EndsWith("/",StringComparison.Ordinal)){System.IO.Directory.CreateDirectory(path);continue;}
            bytes=checked(bytes+entry.Length);
            if(bytes>expectedBytes)throw new InvalidDataException("安装资源大小异常。");
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using(var input=entry.Open())using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
            progress(2+(int)Math.Round(12.0*bytes/expectedBytes),"正在准备安装文件…");
        }
        if(bytes!=expectedBytes)throw new InvalidDataException("安装资源不完整。");
    }
}
