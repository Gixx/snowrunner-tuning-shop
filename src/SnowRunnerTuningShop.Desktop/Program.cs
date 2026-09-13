using Avalonia;
using System;
using SnowRunnerTuningShop.Core.Diagnostics;

namespace SnowRunnerTuningShop.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Register before Avalonia setup so startup failures (e.g. XOpenDisplay) still
        // write a crash log — the UI dialog may be unavailable if the display never opens.
        GlobalExceptionHandler.Register();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.Handle(ex, isTerminating: true);
            Environment.ExitCode = 1;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
