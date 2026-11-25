using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell.Navigation;

// Pure. Turns a composition and a language into what the sidebar shows.
internal static class NavBuilder
{
    public static IReadOnlyList<NavSection> Build(DesktopComposition composition, Localizer strings)
    {
        Validate(composition);

        var sections = new List<NavSection>();

        foreach (var section in composition.Sections)
        {
            var entries = new List<NavEntry>();

            foreach (var contribution in composition.Contributions)
            {
                if (contribution.SectionKey == section.Key)
                {
                    entries.Add(new NavEntry(contribution, strings.Translate(contribution.Key)));
                }
            }

            sections.Add(new NavSection(strings.Translate(section.CaptionKey), entries));
        }

        return sections;
    }

    // A wrong composition throws when the shell is built after sign-in, naming the offender.
    private static void Validate(DesktopComposition composition)
    {
        if (composition.Contributions.Count == 0)
        {
            throw new InvalidOperationException("The composition contributes no screen.");
        }

        var sections = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in composition.Sections)
        {
            if (!sections.Add(section.Key))
            {
                throw new InvalidOperationException($"Section '{section.Key}' is declared twice.");
            }
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contribution in composition.Contributions)
        {
            if (!sections.Contains(contribution.SectionKey))
            {
                throw new InvalidOperationException(
                    $"'{contribution.Key}' names section '{contribution.SectionKey}', which the composition does not declare.");
            }

            if (!keys.Add(contribution.Key))
            {
                throw new InvalidOperationException($"Nav key '{contribution.Key}' is contributed twice.");
            }
        }
    }
}
