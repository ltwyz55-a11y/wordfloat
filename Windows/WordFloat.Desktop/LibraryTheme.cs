using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WordFloat.Desktop;

// Main-window colors are independent of the user's floating-card appearance.
internal static class LibraryTheme
{
    internal static Brush Ink => UI.Brush(NativeWindow.IsDark ? "#F2F2F4" : "#212124");
    internal static Brush Muted => UI.Brush(NativeWindow.IsDark ? "#B0B0B8" : "#72727B");
    internal static Brush Canvas => UI.Brush(NativeWindow.IsDark ? "#202023" : "#FFFFFF");
    internal static Brush Sidebar => UI.Brush(NativeWindow.IsDark ? "#27272B" : "#F1F1F3");
    internal static Brush Surface => UI.Brush(NativeWindow.IsDark ? "#2D2D32" : "#F5F5F7");
    internal static Brush Line => UI.Brush(NativeWindow.IsDark ? "#45454D" : "#E2E2E5");
    internal static Brush Accent => UI.Brush(NativeWindow.IsDark ? "#70C49A" : "#32805B");
    internal static Brush Primary => UI.Brush("#32805B");
    internal static Brush Selection => UI.Brush(NativeWindow.IsDark ? "#293E34" : "#E6F0EA");

    internal static TextBlock Heading(string text)
    {
        var heading = UI.Text(text, 32, Ink);
        heading.FontWeight = FontWeights.Bold;
        return heading;
    }

    internal static Button PrimaryButton(string title, System.Action action, string id)
    {
        var button = UI.Button(title, action, Brushes.White, id);
        button.Background = Primary;
        button.FontSize = 14;
        button.FontWeight = FontWeights.Bold;
        button.Padding = new Thickness(22, 10, 22, 10);
        button.MinHeight = 42;
        return button;
    }

    internal static Border SearchField(TextBox input, string hint)
    {
        input.Margin = new Thickness(0); input.Padding = new Thickness(0);
        input.BorderThickness = new Thickness(0); input.Background = Brushes.Transparent;
        input.FontSize = 14; input.MinHeight = 22; input.VerticalContentAlignment = VerticalAlignment.Center;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = LibraryIcons.Create("search", Muted, 18); icon.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Children.Add(icon); Grid.SetColumn(input, 1); grid.Children.Add(input);
        var placeholder = UI.Text(hint, 14, Muted);
        placeholder.IsHitTestVisible = false; placeholder.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(placeholder, 1); grid.Children.Add(placeholder);
        void RefreshHint() => placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        input.TextChanged += (_, _) => RefreshHint(); RefreshHint();
        var border = new Border { Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 20), Child = grid };
        input.GotKeyboardFocus += (_, _) => border.BorderBrush = Accent;
        input.LostKeyboardFocus += (_, _) => border.BorderBrush = Line;
        return border;
    }
}
