using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mosaic.Desktop.Shell.FirstRun;

public partial class FirstRunView : UserControl
{
    private FirstRunViewModel? _viewModel;

    public FirstRunView()
    {
        InitializeComponent();
    }

    // the token first, because it is the one thing the person has to fetch from the server
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        SetupTokenBox.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.Refused -= OnRefused;
        }

        _viewModel = DataContext as FirstRunViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Refused += OnRefused;
        }
    }

    // typing replaces what was refused, and the eye can still show a refused password
    private void OnRefused(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        (FormField Field, TextBox Box)[] inOrder =
        [
            (_viewModel.SetupToken, SetupTokenBox),
            (_viewModel.Email, EmailBox),
            (_viewModel.FirstName, FirstNameBox),
            (_viewModel.LastName, LastNameBox),
            (_viewModel.Password, PasswordBox),
            (_viewModel.RepeatPassword, RepeatPasswordBox),
        ];

        foreach (var (field, box) in inOrder)
        {
            if (field.HasError)
            {
                box.Focus();
                box.SelectAll();
                return;
            }
        }
    }
}
