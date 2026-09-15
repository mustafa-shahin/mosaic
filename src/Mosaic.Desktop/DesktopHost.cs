using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop;

// Fonts, platform and logging, shared by every entry point. Pro's Program calls this with its own
// composition, so the only line that differs between the two exes is which composition is built.
public static class DesktopHost
{
    public static AppBuilder Build(Func<DesktopServices, IDesktopComposition> compose)
        => AppBuilder.Configure(() => new App(compose))
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
