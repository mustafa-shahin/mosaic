using Avalonia.Controls;
using Avalonia.Threading;

namespace Mosaic.Desktop.Shell.Start;

public partial class StartView : UserControl
{
    public StartView()
    {
        InitializeComponent();
    }

    // posted: an answer that comes back at once would replace this view while it is still being attached
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (!Design.IsDesignMode && DataContext is StartViewModel viewModel)
        {
            Dispatcher.UIThread.Post(viewModel.CheckIfIdle);
        }
    }
}
