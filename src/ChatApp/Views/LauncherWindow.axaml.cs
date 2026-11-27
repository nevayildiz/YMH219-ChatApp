using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChatApp.Views;

public partial class LauncherWindow : Window
{
    public LauncherWindow()
    {
        InitializeComponent();
    }

    private void OnOpenServerClick(object? sender, RoutedEventArgs e) =>
        new ServerWindow().Show();

    private void OnOpenClientClick(object? sender, RoutedEventArgs e) =>
        new ClientWindow().Show();
}
