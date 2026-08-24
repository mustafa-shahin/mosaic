using Mosaic.Api;
using Mosaic.Contracts;

var builder = WebApplication.CreateBuilder(args);

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