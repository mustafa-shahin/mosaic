using Avalonia.Controls;

namespace Mosaic.Desktop.Features.Health;

public partial class HealthView : UserControl
{
    public HealthView()
    {
        InitializeComponent();
    }

    // On DataContext and not on Loaded: the content presenter reuses a view whose template still matches,
    // so a new view model can arrive in an already loaded view, where Loaded does not fire again.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (!Design.IsDesignMode && DataContext is HealthViewModel viewModel)
        {
            viewModel.CheckIfIdle();
        }
    }
}
