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
        Create(owner).ShowDialog();
    }
    internal static Window Create(Window owner)
    {
        var assembly=typeof(AboutWindow).Assembly;
        var content=new StringBuilder($"浮词 Windows {AppInfo.Version}\n\n本机离线词库与系统语音 · 学习数据保存在本机\n\n");
        content.AppendLine("隐私说明\n\n浮词无需账号，不包含广告、统计或追踪 SDK，不主动收集或上传个人信息、查词内容、学习进度或壁纸。应用没有接收这些信息的服务器。\n\n词库随程序提供；学习位置、口音、卡片位置、尺寸和外观保存在本机。壁纸由用户主动选择后复制到应用自己的本地目录，不修改原文件。\n\n发音使用 Windows 系统语音，已安装的声音可离线使用。声音的安装与系统服务遵循 Windows 设置。只有主动点击来源或许可链接时才打开浏览器，外部网站有各自的隐私政策。\n\n启动与安装诊断日志保存在本机，用于定位文件、系统版本或启动错误，不自动上传；日志可能包含本机路径、系统版本、进程及错误信息，不包含其他应用截图。只有用户主动运行 Diagnose.cmd 时才导出浮词自身的诊断截图。\n\n学习数据、外观、壁纸副本和日志位于 %LOCALAPPDATA%\\WordFloat。更换或移除壁纸不改动原图；恢复默认外观不删除学习进度。卸载保留上述本地数据，方便重新安装恢复。\n\n词库来源与许可\n");
        foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("WordFloat.Notice.",System.StringComparison.Ordinal)).Order())
        {
            using var stream=assembly.GetManifestResourceStream(name)!;using var reader=new StreamReader(stream);
            content.AppendLine(reader.ReadToEnd()).AppendLine().AppendLine();
        }
        var text=new TextBox{Text=content.ToString(),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(20),Background=LibraryTheme.Canvas,Foreground=LibraryTheme.Ink,BorderThickness=new Thickness(0),FontSize=12};
        return new Window{Owner=owner,Title="关于、隐私与词库来源",Width=650,Height=650,MinWidth=450,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=text,Icon=owner.Icon};
    }
}
