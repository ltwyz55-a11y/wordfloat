using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordFloat.Setup;

internal sealed class SetupForm : Form
{
    private enum Page { Welcome,Options,Installing,Complete,Failed }
    private readonly SetupMetadata metadata;
    private readonly Panel body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(30,8,30,12),AutoScroll=true};
    private readonly Label title=new Label{AutoSize=true,Font=new Font("Microsoft YaHei UI",20,FontStyle.Bold)};
    private readonly Label step=new Label{AutoSize=true,ForeColor=Color.FromArgb(112,128,119),Margin=new Padding(0,10,0,0)};
    private readonly Button primary=Button("下一步",true),back=Button("上一步"),cancel=Button("取消");
    private readonly CheckBox desktop=new CheckBox{Text="创建桌面快捷方式",Checked=true,AutoSize=true,Margin=new Padding(0,16,0,8)};
    private readonly CheckBox launch=new CheckBox{Text="完成后立即启动浮词",Checked=true,AutoSize=true,Margin=new Padding(0,20,0,0)};
    private readonly ProgressBar bar=new ProgressBar{Width=620,Height=14,Style=ProgressBarStyle.Continuous,Margin=new Padding(0,16,0,10)};
    private readonly Label status=new Label{AutoSize=true,Margin=new Padding(0,8,0,0)};
    private readonly Label percent=new Label{AutoSize=true,ForeColor=Color.FromArgb(112,128,119),Margin=new Padding(0,0,0,14)};
    private readonly TextBox stages=new TextBox{Width=620,Height=116,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.FromArgb(244,247,243)};
    private Page page;
    private bool installing;
    private string? logFile,lastMessage;
    internal SetupForm(SetupMetadata metadata)
    {
        this.metadata=metadata;Text="浮词安装向导";ClientSize=new Size(720,500);FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Microsoft YaHei UI",9);BackColor=Color.FromArgb(250,251,248);ForeColor=Color.FromArgb(37,59,49);
        using(var icon=Assembly.GetExecutingAssembly().GetManifestResourceStream("WordFloat.Setup.Icon"))if(icon is not null)Icon=new Icon(icon);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,112));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,66));
        var header=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new Padding(30,22,20,0),WrapContents=false};
        header.Controls.Add(title);header.Controls.Add(step);root.Controls.Add(header,0,0);root.Controls.Add(body,0,1);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(22,12,22,12),BackColor=Color.FromArgb(239,244,238)};
        footer.Controls.Add(cancel);footer.Controls.Add(primary);footer.Controls.Add(back);root.Controls.Add(footer,0,2);Controls.Add(root);
        primary.Click+=async(_,_)=>
        {
            if(page==Page.Welcome||page==Page.Failed)ShowOptions();
            else if(page==Page.Options)await Install();
            else if(page==Page.Complete)Finish();
        };
        back.Click+=(_,_)=>ShowWelcome();cancel.Click+=(_,_)=>Close();AcceptButton=primary;CancelButton=cancel;
        FormClosing+=(_,e)=>{if(installing)e.Cancel=true;};
        ShowWelcome();
    }
    private static Button Button(string text,bool accent=false)=>new Button
    {
        Text=text,Width=106,Height=36,FlatStyle=FlatStyle.Flat,FlatAppearance={BorderSize=0},
        BackColor=accent?Color.FromArgb(52,108,83):Color.FromArgb(228,235,225),ForeColor=accent?Color.White:Color.FromArgb(37,59,49),Margin=new Padding(5,0,5,0),Cursor=Cursors.Hand
    };
    private FlowLayoutPanel PageBody(Page next,string heading,string caption)
    {
        page=next;title.Text=heading;step.Text=caption;body.Controls.Clear();
        var content=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};body.Controls.Add(content);return content;
    }
    private static Label TextBlock(string text,float size=10)=>new Label{Text=text,AutoSize=true,MaximumSize=new Size(620,0),Font=new Font("Microsoft YaHei UI",size),Margin=new Padding(0,8,0,8)};
    private void ShowWelcome()
    {
        var content=PageBody(Page.Welcome,"欢迎安装浮词","第 1 步 / 共 4 步　·　Windows "+metadata.Version);
        content.Controls.Add(TextBlock("让单词留在眼前",17));
        content.Controls.Add(TextBlock("悬浮单词卡、离线词库和双语例句，随时从桌面开始学习。"));
        content.Controls.Add(TextBlock("安装到当前用户，无需管理员权限。\n已有学习进度和卡片外观会保留。"));
        primary.Text="下一步";primary.Enabled=true;back.Visible=false;cancel.Visible=true;cancel.Enabled=true;cancel.Text="取消";
    }
    private void ShowOptions()
    {
        var content=PageBody(Page.Options,"确认安装设置","第 2 步 / 共 4 步　·　选择启动选项");
        content.Controls.Add(TextBlock("安装位置（当前用户）"));
        content.Controls.Add(new TextBox{Text=SetupInstaller.InstallDirectory,ReadOnly=true,Width=620,BorderStyle=BorderStyle.FixedSingle});
        content.Controls.Add(TextBlock("程序约占用 "+Math.Ceiling(metadata.InstalledBytes/1048576.0)+" MB，安装时另需临时空间。",9));
        content.Controls.Add(desktop);content.Controls.Add(TextBlock("同时添加开始菜单入口，可在 Windows 设置中卸载。",9));
        var existing=Directory.Exists(Path.Combine(SetupInstaller.InstallDirectory,"app"))||File.Exists(Path.Combine(SetupInstaller.InstallDirectory,"浮词.exe"));
        content.Controls.Add(TextBlock(existing?"检测到已有浮词，将更新程序并保留学习进度和外观。":"点击开始安装后，将校验文件并安装到上述位置。",9));
        primary.Text=existing?"开始更新":"开始安装";back.Visible=true;cancel.Visible=true;cancel.Text="取消";
    }
    private async Task Install()
    {
        var content=PageBody(Page.Installing,"正在安装浮词","第 3 步 / 共 4 步　·　请稍候");
        bar.Value=0;percent.Text="0%";status.Text="正在准备安装文件…";stages.Clear();lastMessage=null;
        content.Controls.Add(status);content.Controls.Add(bar);content.Controls.Add(percent);content.Controls.Add(stages);
        primary.Enabled=false;back.Visible=false;cancel.Enabled=false;ControlBox=false;installing=true;
        try
        {
            using var installer=new SetupInstaller(metadata);logFile=installer.LogFile;
            var reporter=new Progress<InstallProgress>(UpdateProgress);var createDesktop=desktop.Checked;
            var result=await Task.Run(()=>installer.Install(createDesktop,reporter));
            installing=false;ShowComplete(result);
        }
        catch(Exception error){installing=false;ShowFailure(error);}
        finally{ControlBox=true;primary.Enabled=true;cancel.Enabled=true;}
    }
    private void UpdateProgress(InstallProgress update)
    {
        if(page!=Page.Installing)return;
        bar.Value=Math.Max(bar.Value,Math.Min(100,Math.Max(0,update.Percent)));percent.Text=bar.Value+"%";status.Text=update.Message;
        if(lastMessage!=update.Message){stages.AppendText((stages.TextLength==0?"":Environment.NewLine)+update.Message);lastMessage=update.Message;}
    }
    private void ShowComplete(InstallEvent result)
    {
        var content=PageBody(Page.Complete,"安装完成","第 4 步 / 共 4 步　·　浮词 "+result.Version+" 已安装");
        content.Controls.Add(TextBlock("浮词已准备就绪",17));
        content.Controls.Add(TextBlock(result.Warnings.Length>0?"程序已安装，部分启动入口需要手动处理。":result.DesktopShortcutCreated?"现在可从桌面或开始菜单打开浮词。":"现在可从开始菜单打开浮词。"));
        content.Controls.Add(TextBlock(result.Directory,9));
        if(result.Warnings.Length>0)content.Controls.Add(TextBlock(string.Join(Environment.NewLine,result.Warnings),9));
        content.Controls.Add(launch);content.Controls.Add(Link("打开安装文件夹",()=>OpenFolder(SetupInstaller.InstallDirectory)));
        primary.Text="完成";back.Visible=false;cancel.Visible=false;
    }
    private void ShowFailure(Exception error)
    {
        var content=PageBody(Page.Failed,"安装未完成","未确认安装成功　·　可以重试");
        content.Controls.Add(TextBlock(error.Message));
        content.Controls.Add(TextBlock("请检查磁盘空间，确认旧版浮词已退出后重试。安装日志保存在本机，方便定位问题。",9));
        if(logFile is not null)content.Controls.Add(Link("查看安装日志",()=>Process.Start(new ProcessStartInfo("notepad.exe","\""+logFile+"\""){UseShellExecute=true})));
        primary.Text="重试";back.Visible=false;cancel.Visible=true;cancel.Text="关闭";
    }
    private static LinkLabel Link(string text,Action action)
    {
        var link=new LinkLabel{Text=text,AutoSize=true,Margin=new Padding(0,14,0,0),LinkColor=Color.FromArgb(52,108,83)};
        link.LinkClicked+=(_,_)=>{try{action();}catch(Exception error){MessageBox.Show(error.Message,"浮词安装向导");}};return link;
    }
    private static void OpenFolder(string directory)=>Process.Start(new ProcessStartInfo("explorer.exe","\""+directory+"\""){UseShellExecute=true});
    private void Finish()
    {
        try{if(launch.Checked)SetupInstaller.LaunchInstalled();Close();}
        catch(Exception error){launch.Checked=false;MessageBox.Show("安装已完成，但未能启动浮词。可从安装文件夹中的 Start.cmd 打开。\n\n"+error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
}
