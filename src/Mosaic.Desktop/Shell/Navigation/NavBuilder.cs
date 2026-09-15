using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell.Navigation;

// Pure. Turns a composition, the ids the session listed and a language into what the sidebar shows.
// The filter and grouping rules live only here, which is why they can be tested without a window.
internal static class NavBuilder
{
    public static IReadOnlyList<NavSection> Build(
        IDesktopComposition composition, IReadOnlySet<string> visible, Localizer strings)
    {
        Validate(composition);

        var sections = new List<NavSection>();

        foreach (var section in composition.Sections)
        {
            var own = composition.Contributions.Where(contribution => contribution.SectionKey == section.Key);
            var entries = Entries(own, visible, strings);

            // a section with nothing this user may see is absent, not empty
            if (entries.Count > 0)
            {
                sections.Add(new NavSection(strings.Translate(section.CaptionKey), entries));
            }
        }

        return sections;
    }

    // The previewer has no session, so it is handed every key and shows the whole sidebar. It is only for the designer
    public static IReadOnlySet<string> Keys(IDesktopComposition composition)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contribution in composition.Contributions)
        {
            AddKeys(contribution, keys);
        }

        return keys;
    }

    // Every entry at every depth, for the opening lookup and for finding the active one.
    public static List<NavEntry> Flatten(IReadOnlyList<NavSection> sections)
    {
        var all = new List<NavEntry>();

        foreach (var section in sections)
        {
            Collect(section.Entries, all);
        }

        return all;
    }

    // A tree costs this one recursion and not a line anywhere else.
    private static List<NavEntry> Entries(
        IEnumerable<NavContribution> contributions, IReadOnlySet<string> visible, Localizer strings)
    {
        var entries = new List<NavEntry>();

        foreach (var contribution in contributions)
        {
            // a listed id with no screen in this build is ignored, because the loop walks screens and not ids
            if (!contribution.Local && !visible.Contains(contribution.Key))
            {
                continue;
            }

            var children = Entries(contribution.Children, visible, strings);

            entries.Add(new NavEntry(contribution, strings.Translate(contribution.Key), children));
        }

        return entries;
    }

    private static void Collect(IReadOnlyList<NavEntry> entries, List<NavEntry> into)
    {
        foreach (var entry in entries)
        {
            into.Add(entry);
            Collect(entry.Children, into);
        }
    }

    // A wrong composition is a mistake in the build, so it fails loudly at startup and names the
    // offender instead of quietly dropping a screen nobody notices is missing.
    private static void Validate(IDesktopComposition composition)
    {
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

            AddKeys(contribution, keys);
        }
    }

    // Every key once at any depth, and a child in its parent's section: the sidebar has nowhere else
    // to show it, so a different section key on a child is a mistake and not a request.
    private static void AddKeys(NavContribution contribution, HashSet<string> keys)
    {
        if (!keys.Add(contribution.Key))
        {
            throw new InvalidOperationException($"Nav key '{contribution.Key}' is contributed twice.");
        }

        foreach (var child in contribution.Children)
        {
            if (child.SectionKey != contribution.SectionKey)
            {
                throw new InvalidOperationException(
                    $"'{child.Key}' names section '{child.SectionKey}', but its parent '{contribution.Key}' is in '{contribution.SectionKey}'.");
            }

            AddKeys(child, keys);
        }
    }
}
