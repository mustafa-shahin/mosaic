using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using System;

namespace Mosaic.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            // Inter has no Arabic. Per-glyph fallback, so Latin stays Inter and only the missing glyphs
            // come from Noto. The name after # is the family name inside the ttf files, not the file name.
            .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback
                    {
                        FontFamily = new FontFamily("avares://Mosaic.Desktop/Assets/Fonts#Noto Sans Arabic"),
                    },
                ],
            })
            .LogToTrace();
}
