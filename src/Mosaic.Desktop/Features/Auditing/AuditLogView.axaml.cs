using Avalonia.Controls;
using Mosaic.Contracts;

namespace Mosaic.Desktop.Features.Auditing;

public partial class AuditLogView : UserControl
{
    public AuditLogView()
    {
        InitializeComponent();
    }

    // a view model never asks the server by itself, so the view asks when it receives one
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (!Design.IsDesignMode && DataContext is AuditLogViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(Paging.FirstPage);
            viewModel.LoadChoicesCommand.Execute(null);
        }
    }
}
