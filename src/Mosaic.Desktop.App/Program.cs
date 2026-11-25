using Avalonia;

namespace Mosaic.Desktop;

// Avalonia and its SynchronizationContext are not set up until StartWithClassicDesktopLifetime runs.
public static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Parameterless for the visual designer.
    public static AppBuilder BuildAvaloniaApp() => DesktopHost.Build();
}
