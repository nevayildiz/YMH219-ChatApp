using Avalonia;

namespace ChatApp;

internal static class Program
{
    // Avalonia başlatılmadan önce hiçbir arayüz kodu çalıştırılmamalıdır.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
