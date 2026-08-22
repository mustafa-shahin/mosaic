using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Mosaic.Desktop.Services;
using Mosaic.Desktop.ViewModels;

namespace Mosaic.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = BuildServices();

            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<HealthViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // One HttpClient for the life of the app, which is the documented pattern for a client
        // talking to a single host. A handler chain for auth arrives with login.
        services.AddSingleton(new HttpClient { BaseAddress = ApiEndpoint.FromEnvironment() });
        services.AddSingleton<ApiClient>();
        services.AddTransient<HealthViewModel>();

        return services.BuildServiceProvider();
    }
}
