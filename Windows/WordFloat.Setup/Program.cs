using System;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WordFloat.Setup;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                if(!Environment.Is64BitOperatingSystem||!int.TryParse(Convert.ToString(key?.GetValue("CurrentBuildNumber")),out var build)||build<19045)
                    throw new InvalidOperationException("需要 Windows 10 22H2 或更新的 64 位系统。");
            using var identity=WindowsIdentity.GetCurrent();using var mutex=new Mutex(false,@"Local\WordFloat.Setup."+(identity.User?.Value??Environment.UserName));
            var acquired=false;
            try
            {
                try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                if(!acquired){MessageBox.Show("安装向导已在运行，请切换到现有安装窗口继续。","浮词安装向导");return 0;}
                using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("WordFloat.Setup.Metadata")??throw new InvalidOperationException("安装信息缺失，请重新下载安装程序。");
                Application.Run(new SetupForm(SetupMetadata.Read(resource)));return 0;
            }
            finally{if(acquired)mutex.ReleaseMutex();}
        }
        catch(Exception error){MessageBox.Show("无法启动安装向导："+error.Message,"浮词安装向导",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}
