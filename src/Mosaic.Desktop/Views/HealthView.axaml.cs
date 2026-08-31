using Avalonia.Controls;
using Avalonia.Interactivity;
using Mosaic.Desktop.ViewModels;

namespace Mosaic.Desktop.Views;

public partial class HealthView : UserControl
{
    public HealthView()
    {
        InitializeComponent();
    }

    // New view instance on every navigation, so this re-checks each time the dashboard is opened.
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is HealthViewModel viewModel)
        {
            viewModel.CheckIfIdle();
        }
    }
}
