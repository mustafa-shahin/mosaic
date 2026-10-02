using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Mosaic.Api;
using Mosaic.Api.Identity;
using Mosaic.Contracts;
using Mosaic.Infrastructure;

UnicodeNormalization.EnsureAvailable();

var builder = WebApplication.CreateBuilder(args);

var trustedNetworks = TrustedProxies.Read(builder.Configuration, builder.Environment);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Management")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Management is not configured. appsettings.Development.json carries the "
        + "local one; elsewhere it arrives as the ConnectionStrings__Management variable."),
    // the installer points this at a volume, so a recreated container keeps the token the operator copied
    builder.Configuration["Setup:TokenPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "setup-token"));

builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc();

builder.Services.AddControllers(options => options.Filters.Add(new ResponseCacheAttribute { NoStore = true }))
    .AddJsonOptions(options => MosaicJson.ApplyTo(options.JsonSerializerOptions))
    // otherwise [ApiController] answers a failed model check with ProblemDetails, outside the envelope
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ModelStateEnvelope.Build);

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
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

// everything after it reads the client's address, the rate limiter first
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseAuthentication();

// before authorization, so a request refused for lack of a session is still counted
app.UseRateLimiter();
app.UseAuthorization();

// don't expose the API description in production
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapControllers();

app.Run();