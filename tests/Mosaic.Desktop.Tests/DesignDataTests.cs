using System.Reflection;
using System.Text.RegularExpressions;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Shell;

namespace Mosaic.Desktop.Tests;

// Only the XAML previewer builds these, so a break in them appears as a broken preview pane in Visual
// Studio and nowhere else. Nothing at runtime touches them, so nothing else would ever notice.
public sealed partial class DesignDataTests
{
    [Fact]
    public void TheHealthSampleShowsTheStateWithTheMostOnScreen()
    {
        var health = HealthDesignData.Reachable;

        Assert.Equal(ConnectionState.Reachable, health.State);
        Assert.False(string.IsNullOrWhiteSpace(health.Service));
        Assert.NotNull(health.ServerTimeUtc);
    }

    [Fact]
    public void TheSettingsSampleOpensOnALanguage()
    {
        Assert.NotNull(SettingsDesignData.Default.SelectedLanguage);
        Assert.NotEmpty(SettingsDesignData.Default.Languages);
    }

    // The previewed sidebar is only worth looking at if it is the one the app builds.
    [Fact]
    public void TheShellSampleIsBuiltFromTheRealComposition()
    {
        var shell = ShellDesignData.Default;

        Assert.Equal(2, shell.Sections.Count);
        Assert.IsType<HealthViewModel>(shell.Current);
        Assert.Equal("nav.dashboard", shell.ActiveKey);
    }

    // The runtime build strips Design.* before it resolves anything, so a view whose Design.DataContext
    // points at a member that does not exist still compiles clean. Only the previewer would notice, and
    // this stands in for it: every reference is followed to a static member that yields a value.
    [Fact]
    public void EveryDesignDataContextInTheXamlResolves()
    {
        var assembly = typeof(App).Assembly;
        var wrong = new List<string>();
        var found = 0;

        foreach (var file in Directory.EnumerateFiles(RepoLayout.Desktop, "*.axaml", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
            {
                continue;
            }

            var xaml = File.ReadAllText(file);
            var view = Path.GetFileName(file);
            var namespaces = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var declared in Prefix().Matches(xaml).Cast<Match>())
            {
                namespaces[declared.Groups[1].Value] = declared.Groups[2].Value;
            }

            foreach (var reference in DesignReference().Matches(xaml).Cast<Match>())
            {
                found++;

                var prefix = reference.Groups[1].Value;
                var typeName = reference.Groups[2].Value;
                var member = reference.Groups[3].Value;

                if (!namespaces.TryGetValue(prefix, out var ns))
                {
                    wrong.Add($"{view} uses '{prefix}:' without an xmlns for it");
                    continue;
                }

                var type = assembly.GetType($"{ns}.{typeName}");

                if (type is null)
                {
                    wrong.Add($"{view} points at '{ns}.{typeName}', which does not exist");
                    continue;
                }

                if (StaticValue(type, member) is null)
                {
                    wrong.Add($"{view} points at '{typeName}.{member}', which is not a static member with a value");
                }
            }
        }

        Assert.True(found > 0, "No view declares a Design.DataContext, so the previewer has no data anywhere.");
        Assert.True(wrong.Count == 0, $"Design data the previewer cannot find: {string.Join("; ", wrong)}");
    }

    private static object? StaticValue(Type type, string member)
    {
        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        return type.GetProperty(member, any)?.GetValue(null) ?? type.GetField(member, any)?.GetValue(null);
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [GeneratedRegex(@"xmlns:(\w+)=""using:([\w.]+)""")]
    private static partial Regex Prefix();

    [GeneratedRegex(@"Design\.DataContext=""\{x:Static (\w+):(\w+)\.(\w+)\}""")]
    private static partial Regex DesignReference();
}
