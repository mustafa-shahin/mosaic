using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Tests.Shell;

// The shell holds the algorithm and the composition holds the data, so adding a screen never changes the shell.
public sealed class ShellDriftTests
{
    private const string FeatureNamespace = "Mosaic.Desktop.Features";

    [Fact]
    public void OnlyTheCompositionAndTheTemplatesNameAFeature()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(RepoLayout.Desktop, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepoLayout.Desktop, file);

            if (IsHandWritten(file)
                && !MayNameAFeature(relative)
                && File.ReadAllText(file).Contains(FeatureNamespace, StringComparison.Ordinal))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(offenders.Count == 0, $"Names a feature outside the composition: {string.Join(", ", offenders)}");
    }

    // NavBuilder translates nav keys held in variables, so the Translate("literal") check never sees them.
    [Fact]
    public void EveryNavKeyInTheCompositionHasATranslation()
    {
        var english = Localizer.Load(LanguageCatalog.Resolve(LanguageCatalog.DefaultCode));
        var composition = Composition();
        var missing = new List<string>();

        foreach (var section in composition.Sections)
        {
            if (!english.Has(section.CaptionKey))
            {
                missing.Add(section.CaptionKey);
            }
        }

        foreach (var contribution in composition.Contributions)
        {
            if (!english.Has(contribution.Key))
            {
                missing.Add(contribution.Key);
            }
        }

        Assert.True(missing.Count == 0, $"No translation for: {string.Join(", ", missing)}");
    }

    // Building a composition only records the factories, so no screen and no request is made here.
    private static DesktopComposition Composition()
    {
        var session = new CurrentSession();
        var api = new ApiClient(new HttpClient(), session);
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), "mosaic-tests"));
        var language = new LanguageChoice(store, LanguageCatalog.Resolve(LanguageCatalog.DefaultCode));

        return CoreComposition.Build(new DesktopServices(api, store, new SignOut(new AuthApi(api), session), language));
    }

    // a feature names itself, and the two seams exist to name features
    private static bool MayNameAFeature(string relative) =>
        relative.StartsWith($"Features{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || relative == "CoreComposition.cs"
        || relative == "App.axaml";

    private static bool IsHandWritten(string file) =>
        (file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".axaml", StringComparison.Ordinal))
        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
