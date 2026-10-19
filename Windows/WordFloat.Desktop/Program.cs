using System;
using System.Windows;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new Application();
        app.Run(new Window { Title = AppInfo.Name, Width = 900, Height = 620 });
    }
}
