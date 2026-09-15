using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mosaic.Integration.Tests;

// Program refuses to start without a connection string. None of these tests reach the database, and
// AddDbContext does not open a connection until something queries, so a well-formed string is enough.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Same host and port as ManagementDbContextFactory and, like it, no credentials: a test that did
    // connect would fail instead of touching a database.
    public const string ConnectionString = "Host=127.0.0.1;Port=5433;Database=mosaic";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:Management", ConnectionString);
}
