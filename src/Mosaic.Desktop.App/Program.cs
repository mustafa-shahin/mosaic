using Avalonia;

namespace Mosaic.Desktop;

// Initialization code. Don't use any Avalonia, third-party APIs or any
// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
// yet and stuff might break.
public static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Parameterless for the visual designer, and it composes the core edition.
    public static AppBuilder BuildAvaloniaApp() => DesktopHost.Build(services => new CoreComposition(services));
}
