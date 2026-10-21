using System;
using System.Windows.Forms;

namespace WordFloat.Setup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form{Text="浮词安装向导",Width=720,Height=500,StartPosition=FormStartPosition.CenterScreen});
    }
}
