using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using Mosaic.Desktop.Shell.Navigation;

namespace Mosaic.Desktop.Tests;

// Lets a test declare exactly the entries it needs, including ones core has no example of yet, such
// as a permission-gated or pro-only screen.
internal sealed class FakeComposition(
    IReadOnlyList<NavSectionDefinition> sections, IReadOnlyList<NavContribution> contributions)
    : IDesktopComposition
{
    public IReadOnlyList<NavSectionDefinition> Sections { get; } = sections;

    public IReadOnlyList<NavContribution> Contributions { get; } = contributions;

    public IReadOnlyList<IDataTemplate> Templates { get; } = [];
}

// A screen with no dependencies, so lifetime can be checked without a network or a window.
internal sealed class FakeScreen : ObservableObject;

internal sealed class DisposableScreen : ObservableObject, IDisposable
{
    public bool WasDisposed { get; private set; }

    public void Dispose() => WasDisposed = true;
}
