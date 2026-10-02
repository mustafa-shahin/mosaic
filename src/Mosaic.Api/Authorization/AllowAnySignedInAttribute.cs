using Microsoft.AspNetCore.Authorization;

namespace Mosaic.Api.Authorization;

// any signed-in person passes, so it belongs only on what concerns the caller's own session or account
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowAnySignedInAttribute : AuthorizeAttribute;
