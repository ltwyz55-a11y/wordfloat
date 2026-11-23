using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Win32;
using WordFloat.Core;
using Forms = System.Windows.Forms;

namespace WordFloat.Desktop;

internal static class NativeWindow
{
    internal static bool SafeMode { get; set; }
    internal static bool? DiagnosticDarkMode { get; set; }
    internal static bool IsDark
    {
        get
        {
            if (DiagnosticDarkMode is bool forced) return forced;
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
    }
    internal static void PrepareCard(Window window)
    {
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        // One per-pixel alpha surface on both Windows 10 and 11; no second DWM contour.
        window.AllowsTransparency = true;
        window.Background = Brushes.Transparent;
        window.BorderThickness = new Thickness(0);
        window.UseLayoutRounding = true;
        window.Topmost = true;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        WindowChrome.SetWindowChrome(window, null);
    }
    internal static HwndSource Attach(Window window, Action<double> horizontalWheel, Action screenChanged)
    {
        var handle = new WindowInteropHelper(window).Handle;
        SetWindowLongPtr(handle,-20,new IntPtr(GetWindowLongPtr(handle,-20).ToInt64() | 0x08000000L | 0x80L));
        var source = HwndSource.FromHwnd(handle)!;
        source.AddHook((IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (msg == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE, still deliver the click.
            if (msg == 0x020E) { horizontalWheel(unchecked((short)((w.ToInt64() >> 16) & 0xffff))); handled = true; }
            if (msg is 0x007E or 0x02E0) window.Dispatcher.BeginInvoke(screenChanged);
            return IntPtr.Zero;
        });
        return source;
    }
    internal static void Restore(Window window, SettingsStore settings)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle,out var rect)) return;
        var screens = Forms.Screen.AllScreens.OrderByDescending(s => s.Primary).Select(s => new ScreenRect(s.WorkingArea.X,s.WorkingArea.Y,s.WorkingArea.Width,s.WorkingArea.Height)).ToArray();
        var position = WindowPlacement.Restore(settings.Value.CardLeft,settings.Value.CardTop,rect.Right-rect.Left,rect.Bottom-rect.Top,screens);
        SetWindowPos(handle,IntPtr.Zero,(int)Math.Round(position.X),(int)Math.Round(position.Y),0,0,0x0010|0x0001|0x0004);
    }
    internal static void Remember(Window window, SettingsStore settings)
    {
        if (GetWindowRect(new WindowInteropHelper(window).Handle,out var rect))
        { settings.Value.CardLeft=rect.Left; settings.Value.CardTop=rect.Top; settings.Save(); }
    }
    internal static ScreenRect CardFrame(Window window)
    {
        var scale=VisualTreeHelper.GetDpi(window);
        if(!GetWindowRect(new WindowInteropHelper(window).Handle,out var rect))return new(0,0,window.Width,window.Height);
        return new(rect.Left/scale.DpiScaleX,rect.Top/scale.DpiScaleY,(rect.Right-rect.Left)/scale.DpiScaleX,(rect.Bottom-rect.Top)/scale.DpiScaleY);
    }
    internal static void ResizeCard(Window window,ScreenRect frame)
    {
        var scale=VisualTreeHelper.GetDpi(window);
        window.Width=frame.Width;window.Height=frame.Height;
        SetWindowPos(new WindowInteropHelper(window).Handle,IntPtr.Zero,(int)Math.Round(frame.X*scale.DpiScaleX),(int)Math.Round(frame.Y*scale.DpiScaleY),0,0,0x0010|0x0001|0x0004);
    }
    internal static int DoubleClickMilliseconds => (int)GetDoubleClickTime();
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
}
