using System.Net;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mosaic.Contracts.Identity;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.FirstRun;

namespace Mosaic.Desktop.Tests.Shell.FirstRun;

public sealed class FirstRunViewTests
{
    private static (Window Window, FirstRunView View) Show(FirstRunViewModel viewModel, double height = 848)
    {
        var view = new FirstRunView { DataContext = viewModel };
        var window = new Window { Width = 1360, Height = height, FlowDirection = viewModel.Strings.FlowDirection, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view);
    }

    private static FirstRunViewModel ViewModelWith(HttpMessageHandler handler, string language = "en") =>
        new(new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession())),
            Localizer.Load(LanguageCatalog.Resolve(language)),
            new PasswordRule { MinLength = 15, MaxLength = 128 });

    private static TextBox Box(FirstRunView view, string name) => view.FindControl<TextBox>(name)!;

    private static TextBlock Showing(FirstRunView view, string? text) =>
        view.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == text);

    private static void Type(Window window, TextBox box, string text)
    {
        box.Focus();
        window.KeyTextInput(text);
    }

    [Fact]
    public void TheTokenField_HasTheFocusWhenTheScreenOpens()
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler()));

        Assert.True(Box(view, "SetupTokenBox").IsFocused);
    }

    [Fact]
    public async Task FillingEveryFieldAndPressingEnter_CreatesTheOwnerAndSignsIn()
    {
        var handler = new RoutingHandler(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/v1/auth/register"] = (HttpStatusCode.Created, RegisterBodies.Owner),
            ["/v1/auth/sign-in"] = (HttpStatusCode.OK, SignInBodies.Accepted),
        });
        var viewModel = ViewModelWith(handler);
        var (window, view) = Show(viewModel);
        var signedIn = 0;

        viewModel.SignedIn += (_, _) => signedIn++;

        Type(window, Box(view, "SetupTokenBox"), "the-token-from-the-file");
        Type(window, Box(view, "EmailBox"), "ada@example.com");
        Type(window, Box(view, "FirstNameBox"), "Ada");
        Type(window, Box(view, "LastNameBox"), "Lovelace");
        Type(window, Box(view, "PasswordBox"), "correct horse battery staple");
        Type(window, Box(view, "RepeatPasswordBox"), "correct horse battery staple");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        await viewModel.CreateCommand.ExecutionTask!;

        Assert.Equal(
            """{"email":"ada@example.com","firstName":"Ada","lastName":"Lovelace","password":"correct horse battery staple","setupToken":"the-token-from-the-file"}""",
            handler.Bodies[0]);
        Assert.Equal(1, signedIn);
    }

    [Fact]
    public void TheGuidance_ShowsTheServersMinimumBeforeAnyRefusal()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());
        var (_, view) = Show(viewModel);

        Assert.True(Showing(view, viewModel.PasswordGuidance).IsEffectivelyVisible);
    }

    [Fact]
    public async Task ARefusal_TakesTheGuidancesPlaceAndFocusesTheFirstRefusedField()
    {
        var viewModel = ViewModelWith(new RoutingHandler(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/v1/auth/register"] = (HttpStatusCode.BadRequest, RegisterBodies.InvalidEmailAndTooShort),
        }));
        var (_, view) = Show(viewModel);

        viewModel.Email.Value = "ada.example.com";
        viewModel.Password.Value = "short";
        viewModel.RepeatPassword.Value = "short";

        await viewModel.CreateCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        var email = Box(view, "EmailBox");

        Assert.True(email.IsFocused);
        Assert.Equal("ada.example.com", email.SelectedText);
        Assert.False(Showing(view, viewModel.PasswordGuidance).IsEffectivelyVisible);
        Assert.True(Showing(view, viewModel.Password.Error).IsEffectivelyVisible);
    }

    [Fact]
    public async Task AMismatch_FocusesTheRepeatField()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());
        var (_, view) = Show(viewModel);

        viewModel.Password.Value = "correct horse battery staple";
        viewModel.RepeatPassword.Value = "correct horse battery stapler";

        await viewModel.CreateCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Box(view, "RepeatPasswordBox").IsFocused);
        Assert.True(Showing(view, viewModel.RepeatPassword.Error).IsEffectivelyVisible);
    }

    [Theory]
    [InlineData("PasswordBox")]
    [InlineData("RepeatPasswordBox")]
    public void BothPasswordFields_HideWhatIsTypedAndOfferTheEye(string name)
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler()));
        var box = Box(view, name);

        Assert.Equal('•', box.PasswordChar);
        Assert.Contains("revealPasswordButton", box.Classes);
    }

    // pasted once and read like a hash, so it stays visible and in the font for identifiers
    [Fact]
    public void TheTokenField_ShowsItsTextInTheMonoFont()
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler()));
        var token = Box(view, "SetupTokenBox");

        Assert.Equal(default, token.PasswordChar);
        Assert.Equal(window.FindResource(window.ActualThemeVariant, "MonoFont"), token.FontFamily);
    }

    [Fact]
    public void TheForm_IsCentredWhereItFitsAndScrollsWhereItDoesNot()
    {
        var (_, tall) = Show(ViewModelWith(new NeverRespondingHandler()), height: 848);
        var (_, small) = Show(ViewModelWith(new NeverRespondingHandler()), height: 600);

        var fits = tall.FindControl<ScrollViewer>("Scroller")!;
        var form = (Control)fits.Content!;
        var centred = (fits.Viewport.Height - form.Bounds.Height) / 2;
        var squeezed = small.FindControl<ScrollViewer>("Scroller")!;

        Assert.InRange(form.Bounds.Top, centred - 1, centred + 1);
        Assert.True(squeezed.Extent.Height > squeezed.Viewport.Height);
    }

    [Fact]
    public void TheFooter_NamesTheServerTheOwnersPasswordGoesTo()
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler()));

        var footer = view.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Single(block => block.Classes.Contains("mono"));

        Assert.Equal("localhost:5175", footer.Text);
    }

    [Theory]
    [InlineData("SetupTokenBox", "-9fK2pQx7LmN4vB8rT1yW6hJ3cZ5sE0uGdAoYiRkVw_")]
    [InlineData("EmailBox", "123@abc.com")]
    [InlineData("PasswordBox", "correct horse battery staple")]
    [InlineData("RepeatPasswordBox", "correct horse battery staple")]
    public void InArabic_LatinTextStaysInTheOrderItWasTyped(string name, string typed)
    {
        var (window, view) = Show(ViewModelWith(new NeverRespondingHandler(), "ar"));
        var box = Box(view, name);

        Type(window, box, typed);
        Dispatcher.UIThread.RunJobs();

        Assert.True(TextOrder.IsLeftToRight(box));
    }

    // a name may be written in Arabic, so these boxes follow the language
    [Theory]
    [InlineData("FirstNameBox")]
    [InlineData("LastNameBox")]
    public void InArabic_TheNamesKeepTheWindowsDirection(string name)
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler(), "ar"));

        var presenter = Box(view, name).GetVisualDescendants().OfType<TextPresenter>().Single();

        Assert.Equal(FlowDirection.RightToLeft, presenter.FlowDirection);
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
