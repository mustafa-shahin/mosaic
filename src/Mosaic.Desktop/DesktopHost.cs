using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Mosaic.Desktop;

// The app builder with its fonts, platform and logging.
public static class DesktopHost
{
    public static AppBuilder Build()
        => AppBuilder.Configure<App>()
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
