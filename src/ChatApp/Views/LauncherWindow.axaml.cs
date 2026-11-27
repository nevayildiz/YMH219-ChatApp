using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChatApp.Views;

public partial class LauncherWindow : Window
{
    public LauncherWindow()
    {
        InitializeComponent();
    }

    private void OnOpenServerClick(object? sender, RoutedEventArgs e)
    {
        // Sunucu penceresi sonraki adımda eklenecek.
    }

    private void OnOpenClientClick(object? sender, RoutedEventArgs e)
    {
        // İstemci penceresi sonraki adımda eklenecek.
    }
}
