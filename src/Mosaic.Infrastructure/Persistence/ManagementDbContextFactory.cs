using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mosaic.Infrastructure.Persistence;

// Only used by dotnet ef. The running application gets its context from AddInfrastructure.
internal sealed class ManagementDbContextFactory : IDesignTimeDbContextFactory<ManagementDbContext>
{
    // The configuration provider reads the double underscore as ConnectionStrings:Management, so the
    // same variable serves dotnet ef and an API started from the command line.
    private const string ConnectionVariable = "ConnectionStrings__Management";

    // Used when the variable is not set. migrations add and remove never connect, so they work with
    // this alone; commands that do connect need the variable. The port is the one compose.yaml
    // publishes, so a connection made by mistake never lands on whatever else listens on 5432.
    private const string Placeholder = "Host=127.0.0.1;Port=5433;Database=mosaic";

    public ManagementDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>();

        DatabaseOptions.Configure(
            options, Environment.GetEnvironmentVariable(ConnectionVariable) ?? Placeholder);

        return new ManagementDbContext(options.Options);
    }
}
