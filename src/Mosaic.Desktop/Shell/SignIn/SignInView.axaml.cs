using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mosaic.Desktop.Shell.SignIn;

public partial class SignInView : UserControl
{
    private SignInViewModel? _viewModel;

    public SignInView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // after a sign-out or a handover the address is already filled in, so the password is what comes next
        (string.IsNullOrEmpty(_viewModel?.Email) ? EmailBox : PasswordBox).Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.Refused -= OnRefused;
        }

        _viewModel = DataContext as SignInViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Refused += OnRefused;
        }
    }

    // typing replaces the password, and the eye can still show what was sent
    private void OnRefused(object? sender, EventArgs e)
    {
        PasswordBox.Focus();
        PasswordBox.SelectAll();
    }
}
