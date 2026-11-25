using CommunityToolkit.Mvvm.ComponentModel;

namespace Mosaic.Desktop.Tests.Shell.Navigation;

// A screen with no dependencies, so lifetime can be checked without a network or a window.
internal sealed class FakeScreen : ObservableObject;

internal sealed class DisposableScreen : ObservableObject, IDisposable
{
    public bool WasDisposed { get; private set; }

    public void Dispose() => WasDisposed = true;
}
