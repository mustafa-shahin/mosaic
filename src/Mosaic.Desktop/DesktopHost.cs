using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

// Fonts, platform and logging for every entry point, so core's and pro's Program differ only in the composition.
public static class DesktopHost
{
    public static AppBuilder Build(Func<DesktopServices, IDesktopComposition> compose)
        => AppBuilder.Configure(() => new App(compose))
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            // Inter has no Arabic, so each glyph Inter lacks comes from Noto and Latin text stays Inter
            .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback
                    {
                        // after the # comes the family name inside the ttf files, not a file name
                        FontFamily = new FontFamily("avares://Mosaic.Desktop/Assets/Fonts#Noto Sans Arabic"),
                    },
                ],
            })
            .LogToTrace();
}
