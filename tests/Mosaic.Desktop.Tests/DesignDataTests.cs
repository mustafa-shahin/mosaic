using System.Reflection;
using System.Text.RegularExpressions;
using Mosaic.Desktop.Features.Health;
using Mosaic.Desktop.Features.Settings;
using Mosaic.Desktop.Shell;
using Mosaic.Desktop.Shell.FirstRun;
using Mosaic.Desktop.Shell.SignIn;
using Mosaic.Desktop.Shell.Start;

namespace Mosaic.Desktop.Tests;

// Only the XAML previewer builds these, so a break in them would show only as a broken preview pane.
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

    [Fact]
    public void TheSignInSampleShowsTheNoteARefusalAndTheServer()
    {
        var signIn = SignInDesignData.Refused;

        Assert.Equal(SignInNote.AccountReady, signIn.Note);
        Assert.False(string.IsNullOrWhiteSpace(signIn.NoteText));
        Assert.True(signIn.HasError);
        Assert.False(string.IsNullOrWhiteSpace(signIn.ErrorMessage));
        Assert.False(string.IsNullOrWhiteSpace(signIn.Server));
    }

    [Fact]
    public void TheStartSampleShowsAFailedCheckAndTheServer()
    {
        var start = StartDesignData.Unreachable;

        Assert.True(start.HasError);
        Assert.False(string.IsNullOrWhiteSpace(start.ErrorMessage));
        Assert.False(string.IsNullOrWhiteSpace(start.Server));
    }

    [Fact]
    public void TheFirstRunSampleShowsARefusedTokenAndTheGuidance()
    {
        var firstRun = FirstRunDesignData.Refused;

        Assert.True(firstRun.SetupToken.HasError);
        Assert.False(string.IsNullOrWhiteSpace(firstRun.PasswordGuidance));
        Assert.False(string.IsNullOrWhiteSpace(firstRun.Server));
    }

    [Fact]
    public void TheShellSampleIsBuiltFromTheRealComposition()
    {
        var shell = ShellDesignData.Default;

        Assert.Equal(2, shell.Sections.Count);
        Assert.IsType<HealthViewModel>(shell.Current);
        Assert.Equal("nav.dashboard", shell.ActiveKey);
        Assert.False(string.IsNullOrWhiteSpace(shell.Initials));
        Assert.False(string.IsNullOrWhiteSpace(shell.Email));
    }

    // Compiled XAML drops Design.* before resolving it, so a Design.DataContext naming a missing member still compiles.
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
