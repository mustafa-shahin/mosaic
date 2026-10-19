using Mosaic.Desktop.Localization;

namespace Mosaic.Desktop.Shell.Navigation;

// Pure. Turns a composition, the listed ids and a language into what the sidebar shows.
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

            // a section with nothing this user may see gets no heading
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

    // Recursive. A parent this user may not see hides its children too.
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

    // A wrong composition throws when the shell is built after sign-in, naming the offender.
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

    // A child always sits under its parent, so a section key that differs from the parent's throws.
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
