using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class AboutWindow
{
    internal static void Show(Window owner)
    {
        var assembly=typeof(AboutWindow).Assembly;
        var content=new StringBuilder($"浮词 Windows {AppInfo.Version}\n\n本机离线词库与系统语音 · 学习数据保存在本机\n\n");
        foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("WordFloat.Notice.",System.StringComparison.Ordinal)).Order())
        {
            using var stream=assembly.GetManifestResourceStream(name)!;using var reader=new StreamReader(stream);
            content.AppendLine(reader.ReadToEnd()).AppendLine().AppendLine();
        }
        var text=new TextBox{Text=content.ToString(),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(20),Background=Brushes.White,Foreground=UI.Brush("#253B31"),BorderThickness=new Thickness(0),FontSize=12};
        new Window{Owner=owner,Title="关于浮词与词库来源",Width=650,Height=650,MinWidth=450,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=text,Icon=owner.Icon}.ShowDialog();
    }
}
