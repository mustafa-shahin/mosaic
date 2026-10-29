using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mosaic.Api.Tests;

// Program refuses to start without a connection string, and EF Core connects only at the first query.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Same host and port as ManagementDbContextFactory and, like it, no credentials: a test that did
    // connect would fail instead of touching a database.
    public const string ConnectionString = "Host=127.0.0.1;Port=5433;Database=mosaic";

    // the default sits under the content root, which for a test host is the API's source folder
    public static readonly string SetupTokenPath =
        Path.Combine(Path.GetTempPath(), $"mosaic-setup-token-{Guid.CreateVersion7():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder
            .UseSetting("ConnectionStrings:Management", ConnectionString)
            .UseSetting("Setup:TokenPath", SetupTokenPath);
}
