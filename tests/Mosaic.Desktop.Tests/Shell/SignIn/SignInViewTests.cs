using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mosaic.Desktop.Controls;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.SignIn;

namespace Mosaic.Desktop.Tests.Shell.SignIn;

public sealed class SignInViewTests
{
    private static (Window Window, SignInView View) Show(SignInViewModel viewModel)
    {
        var view = new SignInView { DataContext = viewModel };
        var window = new Window { Width = 1360, Height = 848, FlowDirection = viewModel.Strings.FlowDirection, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view);
    }

    private static SignInViewModel ViewModelWith(HttpMessageHandler handler, string language = "en") =>
        new(new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession())),
            Localizer.Load(LanguageCatalog.Resolve(language)));

    private static void Click(Window window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void TheEmailField_HasTheFocusWhenTheScreenOpens()
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler()));

        Assert.True(view.FindControl<TextBox>("EmailBox")!.IsFocused);
    }

    [Fact]
    public void ThePasswordField_HasTheFocusWhenTheAddressIsAlreadyThere()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());

        viewModel.Email = "ada@example.com";

        var (_, view) = Show(viewModel);

        Assert.True(view.FindControl<TextBox>("PasswordBox")!.IsFocused);
    }

    [Fact]
    public void AFocusedField_HasTheBronzeBorder()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler()));

        var border = view.FindControl<TextBox>("EmailBox")!
            .GetVisualDescendants()
            .OfType<Border>()
            .Single(element => element.Name == "PART_BorderElement");

        Assert.Same(window.FindResource(window.ActualThemeVariant, "Accent"), border.BorderBrush);
    }

    [Fact]
    public async Task TypingBothFieldsAndPressingEnter_SendsTheSignIn()
    {
        var recorder = new RecordingHandler(HttpStatusCode.OK, SignInBodies.Accepted);
        var viewModel = ViewModelWith(recorder);
        var (window, _) = Show(viewModel);

        window.KeyTextInput("ada@example.com");
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        window.KeyTextInput("correct horse battery staple");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        await viewModel.SignInCommand.ExecutionTask!;

        Assert.Equal(1, recorder.Calls);
        Assert.Equal("""{"email":"ada@example.com","password":"correct horse battery staple"}""", recorder.Body);
    }

    [Fact]
    public async Task ARefusal_SelectsThePasswordSoTheNextAttemptReplacesIt()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.Forbidden, SignInBodies.WrongPassword));
        var (window, view) = Show(viewModel);
        var password = view.FindControl<TextBox>("PasswordBox")!;

        viewModel.Password = "wrong horse";

        Click(window, view.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("primary")));

        await viewModel.SignInCommand.ExecutionTask!;
        Dispatcher.UIThread.RunJobs();

        Assert.True(password.IsFocused);
        Assert.Equal("wrong horse", password.SelectedText);
    }

    [Fact]
    public void AHiddenPassword_CannotBeCopiedUntilTheEyeShowsIt()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler()));
        var password = view.FindControl<TextBox>("PasswordBox")!;

        password.Focus();
        window.KeyTextInput("correct horse battery staple");
        password.SelectAll();

        Assert.False(password.CanCopy);

        password.RevealPassword = true;

        Assert.True(password.CanCopy);
    }

    [Fact]
    public async Task ARefusal_ShowsTheReasonInTheErrorBox()
    {
        var viewModel = ViewModelWith(new CannedResponseHandler(HttpStatusCode.Forbidden, SignInBodies.WrongPassword));
        var (_, view) = Show(viewModel);

        await viewModel.SignInCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        var box = view.GetVisualDescendants().OfType<ErrorBox>().Single();
        var text = box.GetVisualDescendants().OfType<SelectableTextBlock>().Single();

        Assert.True(box.IsEffectivelyVisible);
        Assert.Equal(viewModel.ErrorMessage, text.Text);
    }

    [Theory]
    [InlineData(SignInNote.AccountReady)]
    [InlineData(SignInNote.SessionEnded)]
    [InlineData(SignInNote.SignedOutEverywhere)]
    public void AHandover_ShowsItsNote(SignInNote note)
    {
        var viewModel = new SignInViewModel(
            new AuthApi(new ApiClient(new HttpClient(new NeverRespondingHandler()) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession())),
            Localizer.Load(LanguageCatalog.Resolve("en")))
        {
            Note = note
        };
        var (_, view) = Show(viewModel);

        var shown = view.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Classes.Contains("note"));

        Assert.True(shown.IsEffectivelyVisible);
        Assert.Equal(viewModel.NoteText, shown.Text);
    }

    [Fact]
    public void AnOrdinarySignIn_ShowsNoNote()
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler()));

        var note = view.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Classes.Contains("note"));

        Assert.False(note.IsEffectivelyVisible);
    }

    [Fact]
    public void TheFooter_NamesTheServerInTheMonoFont()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler()));

        var footer = view.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Single(block => block.Classes.Contains("mono"));

        Assert.Equal("localhost:5175", footer.Text);
        Assert.Equal(window.FindResource(window.ActualThemeVariant, "MonoFont"), footer.FontFamily);
    }

    [Fact]
    public void TheEye_ShowsThePasswordAndHidesItAgain()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler()));
        var password = view.FindControl<TextBox>("PasswordBox")!;

        password.Focus();
        window.KeyTextInput("correct horse battery staple");
        Dispatcher.UIThread.RunJobs();

        var eye = password.GetVisualDescendants().OfType<ToggleButton>().Single();

        Click(window, eye);
        Assert.True(password.RevealPassword);

        Click(window, eye);
        Assert.False(password.RevealPassword);
    }

    [Fact]
    public void TheEye_WhileShowing_UsesTheBronzeAccentAndNoFill()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler()));
        var password = view.FindControl<TextBox>("PasswordBox")!;

        password.Focus();
        window.KeyTextInput("correct horse battery staple");
        Dispatcher.UIThread.RunJobs();

        var eye = password.GetVisualDescendants().OfType<ToggleButton>().Single();

        Click(window, eye);

        var presenter = eye.GetVisualDescendants().OfType<ContentPresenter>().First();
        var accent = window.FindResource(window.ActualThemeVariant, "Accent");

        Assert.Same(accent, presenter.Foreground);
        Assert.Equal(Avalonia.Media.Brushes.Transparent, presenter.Background);
    }

    [Fact]
    public void InArabic_TheAddressStaysInTheOrderItWasTyped()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler(), "ar"));
        var email = view.FindControl<TextBox>("EmailBox")!;

        email.Focus();
        window.KeyTextInput("ada@mail.");
        Dispatcher.UIThread.RunJobs();

        Assert.True(TextOrder.IsLeftToRight(email));
    }

    [Fact]
    public void InArabic_TheEyeShowsThePasswordInTheOrderItWasTyped()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler(), "ar"));
        var password = view.FindControl<TextBox>("PasswordBox")!;

        password.Focus();
        window.KeyTextInput("2026 summer trip!");
        password.RevealPassword = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(TextOrder.IsLeftToRight(password));
    }

    [Fact]
    public void InArabic_TheArrowKeysMoveTheWayTheyPoint()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler(), "ar"));
        var email = view.FindControl<TextBox>("EmailBox")!;

        email.Focus();
        window.KeyTextInput("abcd");
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);

        Assert.Equal(2, email.CaretIndex);
    }

    [Theory]
    [InlineData("ar", TextAlignment.Right)]
    [InlineData("en", TextAlignment.Start)]
    public void TheAddressAndThePassword_SitUnderTheirLabels(string language, TextAlignment alignment)
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler(), language));

        Assert.Equal(alignment, view.FindControl<TextBox>("EmailBox")!.TextAlignment);
        Assert.Equal(alignment, view.FindControl<TextBox>("PasswordBox")!.TextAlignment);
    }

    // only the text turns left to right, the box keeps its mirrored layout
    [Fact]
    public void InArabic_TheEyeStaysOnTheLeft()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler(), "ar"));
        var password = view.FindControl<TextBox>("PasswordBox")!;

        password.Focus();
        window.KeyTextInput("correct horse battery staple");
        Dispatcher.UIThread.RunJobs();

        var eye = password.GetVisualDescendants().OfType<ToggleButton>().Single();
        var eyeCentre = eye.TranslatePoint(new Point(eye.Bounds.Width / 2, 0), window)!.Value.X;
        var boxCentre = password.TranslatePoint(new Point(password.Bounds.Width / 2, 0), window)!.Value.X;

        Assert.True(eyeCentre < boxCentre);
    }

    [Fact]
    public void InArabic_TheServerAddressReadsLeftToRight()
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler(), "ar"));

        var footer = view.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Single(block => block.Classes.Contains("mono"));

        Assert.Equal(FlowDirection.LeftToRight, footer.FlowDirection);
    }
}
