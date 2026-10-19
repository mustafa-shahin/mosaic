using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mosaic.Contracts;
using Mosaic.Desktop.Localization;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.Services.Identity;
using Mosaic.Desktop.Shell.Start;

namespace Mosaic.Desktop.Tests.Shell.Start;

public sealed class StartViewTests
{
    private static (Window Window, StartView View) Show(StartViewModel viewModel)
    {
        var view = new StartView { DataContext = viewModel };
        var window = new Window { Width = 1360, Height = 848, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view);
    }

    private static StartViewModel ViewModelWith(HttpMessageHandler handler) =>
        new(new AuthApi(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5175/") }, new CurrentSession())),
            Localizer.Load(LanguageCatalog.Resolve("en")));

    private static void Click(Window window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    // nothing else asks in the running app, so a view that forgot to would leave the window waiting forever
    [Fact]
    public void ShowingTheScreen_StartsTheCheck()
    {
        var handler = new NeverRespondingHandler();

        Show(ViewModelWith(handler));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void WhileTheCheckRuns_TheScreenSaysItIsConnectingAndOffersNoButton()
    {
        var viewModel = ViewModelWith(new NeverRespondingHandler());
        var (_, view) = Show(viewModel);

        var connecting = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(block => block.Text == viewModel.Strings.Translate("start.connecting"));

        Assert.True(connecting.IsEffectivelyVisible);
        Assert.False(view.GetVisualDescendants().OfType<Button>().Single().IsEffectivelyVisible);
    }

    [Fact]
    public async Task AFailedCheck_ShowsTheReasonAndTryAgainAsksOnceMore()
    {
        var recorder = new RecordingHandler(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"error.server_busy"}}""");
        var viewModel = ViewModelWith(recorder);
        var (window, view) = Show(viewModel);

        await viewModel.CheckCommand.ExecutionTask!;
        Dispatcher.UIThread.RunJobs();

        var tryAgain = view.GetVisualDescendants().OfType<Button>().Single();
        var connecting = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(block => block.Text == viewModel.Strings.Translate("start.connecting"));

        Assert.True(tryAgain.IsEffectivelyVisible);
        Assert.False(connecting.IsEffectivelyVisible);
        Assert.Equal(viewModel.Strings.Translate(ErrorCodes.ServerBusy), viewModel.ErrorMessage);

        Click(window, tryAgain);
        await viewModel.CheckCommand.ExecutionTask!;

        Assert.Equal(2, recorder.Calls);
    }

    [Fact]
    public void TheFooter_NamesTheServerItIsAsking()
    {
        var (_, view) = Show(ViewModelWith(new NeverRespondingHandler()));

        var footer = view.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Single(block => block.Classes.Contains("mono"));

        Assert.Equal("localhost:5175", footer.Text);
    }
}
