using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Mosaic.Api;
using Mosaic.Api.Authorization;
using Mosaic.Api.Health;
using Mosaic.Api.Identity;
using Mosaic.Api.Identity.Sessions;
using Mosaic.Contracts;
using Mosaic.Persistence;

UnicodeNormalization.EnsureAvailable();

var builder = WebApplication.CreateBuilder(args);

var trustedNetworks = TrustedProxies.Read(builder.Configuration, builder.Environment);

builder.Services.AddPersistence(
    builder.Configuration.GetConnectionString("Management")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Management is not configured. appsettings.Development.json carries the "
        + "local one; elsewhere it arrives as the ConnectionStrings__Management variable."));

builder.Services.AddPermissions();

// the default is next to the API, so a recreated container writes a new token and the copied one stops working
builder.Services.AddAccounts(
    builder.Configuration["Setup:TokenPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "setup-token"));

builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.ConfigureHttpJsonOptions(options => MosaicJson.ApplyTo(options.SerializerOptions));

// the default throws only in Development, and elsewhere an unreadable body gets a bare 400 without the envelope
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<MosaicExceptionHandler>();
builder.Services.AddRateLimiter(RateLimits.Configure);
builder.Services.Configure<ForwardedHeadersOptions>(options => TrustedProxies.Apply(options, trustedNetworks));

builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, configureOptions: null);

// an endpoint that declares no access rule needs a session, so a forgotten attribute never opens it to everyone
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// UseExceptionHandler() throws at startup if no fallback is registered. Never actually reached.
builder.Services.AddProblemDetails();

var app = builder.Build();

// first, so a refusal from the rate limiter or from authorization carries it too
app.UseMiddleware<NoStoreMiddleware>();

// before the rate limiter, which would otherwise count every client behind a proxy as the proxy
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseAuthentication();

// before authorization, so a request refused for lack of a session is still counted
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealth();
app.MapIdentity();

app.Run();