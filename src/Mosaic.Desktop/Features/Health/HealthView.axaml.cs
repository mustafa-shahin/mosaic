using Avalonia.Controls;

namespace Mosaic.Desktop.Features.Health;

public partial class HealthView : UserControl
{
    public HealthView()
    {
        InitializeComponent();
    }

    // Off the DataContext rather than Loaded: the content presenter recycles a view whose template
    // still matches, so a fresh view model can arrive in a view that is already loaded. This fires
    // for a new view and again for a recycled one, and a check already running is left alone.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (!Design.IsDesignMode && DataContext is HealthViewModel viewModel)
        {
            viewModel.CheckIfIdle();
        }
    }
}
