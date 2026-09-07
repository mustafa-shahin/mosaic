using Mosaic.Api;
using Mosaic.Contracts;
using Mosaic.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Management")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Management is not configured. appsettings.Development.json carries the "
        + "local one; elsewhere it arrives as the ConnectionStrings__Management variable."));

builder.Services.AddControllers()
    .AddJsonOptions(options => MosaicJson.ApplyTo(options.JsonSerializerOptions));

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddExceptionHandler<MosaicExceptionHandler>();

// UseExceptionHandler() throws at startup if no fallback is registered. Never actually reached.
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

// don't expose the API description in production
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();