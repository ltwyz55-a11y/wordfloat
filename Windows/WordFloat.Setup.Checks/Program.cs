using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using WordFloat.Setup;

static class Program
{
    private static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    private static void Fails(Action action){try{action();}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or System.Runtime.Serialization.SerializationException){return;}throw new Exception("Invalid installation accepted");}
    private static MemoryStream Archive(string path)
    {
        var data=new MemoryStream();using(var zip=new ZipArchive(data,ZipArchiveMode.Create,true))using(var writer=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false)))writer.Write("hello");data.Position=0;return data;
    }
    private static string Hash(MemoryStream stream){using var sha=SHA256.Create();var value=Convert.ToHexString(sha.ComputeHash(stream));stream.Position=0;return value;}
    private static void Main()
    {
        var root=Path.Combine(Path.GetTempPath(),"wordfloat-setup-checks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            using(var zip=Archive("app/中文 words.txt"))SetupArchive.Extract(zip,root,Hash(zip),5,(_,_)=>{});
            Require(File.ReadAllText(Path.Combine(root,"app/中文 words.txt"))=="hello","Payload extraction");
            using(var zip=Archive("../outside.txt"))Fails(()=>SetupArchive.Extract(zip,root,Hash(zip),5,(_,_)=>{}));
            Require(!File.Exists(Path.Combine(Path.GetDirectoryName(root)!,"outside.txt")),"Zip traversal escaped extraction root");
            using(var zip=Archive("bad.txt"))Fails(()=>SetupArchive.Extract(zip,root,new string('0',64),5,(_,_)=>{}));
            Require(!File.Exists(Path.Combine(root,"bad.txt")),"Hash failure wrote payload files");
            using(var zip=Archive("too-large.txt"))Fails(()=>SetupArchive.Extract(zip,root,Hash(zip),4,(_,_)=>{}));
            Console.WriteLine("PASS embedded payload extraction, hash verification, size limits and unsafe-path rejection");
            var protocol=new InstallProtocol("test");Require(protocol.Receive("normal worker output") is null,"Console noise");
            Require(protocol.Receive("WORDFLOAT_SETUP:{\"kind\":\"progress\",\"percent\":50,\"message\":\"安装中\"}")!.Percent==57,"Visible progress mapping");
            Fails(()=>protocol.Finish(0));
            protocol.Receive("WORDFLOAT_SETUP:{\"kind\":\"complete\",\"version\":\"test\",\"directory\":\"example\",\"desktopShortcutCreated\":true,\"warnings\":[]}");
            Require(protocol.Finish(0).DesktopShortcutCreated,"Completed result");Fails(()=>protocol.Finish(1));
            var failed=new InstallProtocol("test");failed.Receive("WORDFLOAT_SETUP:{\"kind\":\"error\",\"message\":\"disk full\"}");Fails(()=>failed.Finish(0));
            Fails(()=>new InstallProtocol("test").Receive("WORDFLOAT_SETUP:{\"kind\":\"complete\",\"version\":\"wrong\",\"directory\":\"example\"}"));
            Fails(()=>new InstallProtocol("test").Receive("WORDFLOAT_SETUP:{\"kind\":\"progress\",\"percent\":101}"));
            Console.WriteLine("PASS completion requires correct version, successful process exit and an explicit success event");
            Console.WriteLine("SETUP_CHECKS_PASSED (payload/protocol only; Windows UI not executed)");
        }
        finally{Directory.Delete(root,true);}
    }
}
