using Avalonia.Controls;
using Avalonia.Interactivity;
using Mosaic.Desktop.ViewModels;

namespace Mosaic.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is HealthViewModel viewModel)
        {
            viewModel.CheckCommand.Execute(null);
        }
    }
}
