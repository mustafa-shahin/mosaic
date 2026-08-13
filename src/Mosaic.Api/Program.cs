using Mosaic.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => MosaicJson.ApplyTo(options.JsonSerializerOptions));

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// Development only: a production control plane publishing a map of its own API is free reconnaissance.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();