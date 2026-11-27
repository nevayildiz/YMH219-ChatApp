using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChatApp.Views;

namespace ChatApp;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Son pencere kapanınca uygulama kapanır; böylece başlatıcı kapatılsa da
            // açık sunucu/istemci pencereleri çalışmaya devam eder.
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            desktop.MainWindow = new LauncherWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
